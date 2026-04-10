using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
    /// <summary>
    /// Apply equipment edits (remove types, add requests) to current Draft:SelectedEquipment and return updated quote summary.
    /// </summary>
    private async Task<string> HandleUpdateEquipmentAsync(string argsJson, CancellationToken ct)
    {
        var session = _http.HttpContext?.Session;
        if (session == null)
            return JsonSerializer.Serialize(new { error = "Session unavailable.", instruction = "Do NOT call update_equipment or recommend_equipment_for_event again in this response. Ask the user to refresh and try again." });

        _logger.LogInformation("[update_equipment] Invoked with args (length={Len})", argsJson?.Length ?? 0);

        var selectedEquipmentJson = session.GetString("Draft:SelectedEquipment");
        if (string.IsNullOrWhiteSpace(selectedEquipmentJson))
        {
            _logger.LogWarning("[update_equipment] No Draft:SelectedEquipment in session");
            return JsonSerializer.Serialize(new
            {
                error = "No quote summary to update.",
                instruction = "Do NOT call update_equipment again in this response. There is no equipment list in session yet — call recommend_equipment_for_event first with full event context and equipment_requests, then call update_equipment if the user wants edits."
            });
        }

        List<SelectedEquipmentItem> currentItems;
        try
        {
            currentItems = JsonSerializer.Deserialize<List<SelectedEquipmentItem>>(selectedEquipmentJson) ?? new List<SelectedEquipmentItem>();
        }
        catch
        {
            currentItems = new List<SelectedEquipmentItem>();
        }

        _logger.LogInformation("[update_equipment] Current session has {Count} items", currentItems.Count);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var root = doc.RootElement;

        // Parse remove_types
        var removeTypes = new List<string>();
        if (root.TryGetProperty("remove_types", out var rt) && rt.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in rt.EnumerateArray())
                if (r.ValueKind == JsonValueKind.String)
                    removeTypes.Add((r.GetString() ?? "").Trim().ToLowerInvariant());
        }
        removeTypes.RemoveAll(string.IsNullOrWhiteSpace);
        if (removeTypes.Count > 0)
            _logger.LogInformation("[update_equipment] remove_types: [{Types}]", string.Join(", ", removeTypes));

        // Apply removals: drop items whose description contains any remove_type
        if (removeTypes.Count > 0)
        {
            currentItems = currentItems.Where(item =>
            {
                var desc = (item.Description ?? "").ToLowerInvariant();
                var match = removeTypes.Any(t => desc.Contains(t));
                if (match) _logger.LogInformation("[update_equipment] Removing item: {Description} (matched remove_types)", item.Description);
                return !match;
            }).ToList();
            _logger.LogInformation("[update_equipment] After removals: {Count} items", currentItems.Count);
        }

        // Event context: args first, then session fallback so add_requests can resolve (e.g. room-specific packages)
        var venueName = root.TryGetProperty("venue_name", out var vnArg) && vnArg.ValueKind == JsonValueKind.String ? vnArg.GetString() : null;
        var roomName = root.TryGetProperty("room_name", out var rnArg) && rnArg.ValueKind == JsonValueKind.String ? rnArg.GetString() : null;
        var eventType = root.TryGetProperty("event_type", out var etArg) && etArg.ValueKind == JsonValueKind.String ? etArg.GetString() : null;
        var expectedAttendees = root.TryGetProperty("expected_attendees", out var eaArg) && eaArg.ValueKind == JsonValueKind.Number ? eaArg.GetInt32() : (int?)null;
        var setupStyle = root.TryGetProperty("setup_style", out var ssArg) && ssArg.ValueKind == JsonValueKind.String ? ssArg.GetString() : null;
        if (string.IsNullOrWhiteSpace(venueName)) venueName = session.GetString("Draft:VenueName");
        if (string.IsNullOrWhiteSpace(roomName)) roomName = session.GetString("Draft:RoomName");
        if (string.IsNullOrWhiteSpace(eventType)) eventType = session.GetString("Draft:EventType");
        if (!expectedAttendees.HasValue && int.TryParse(session.GetString("Draft:ExpectedAttendees"), out var eaSession)) expectedAttendees = eaSession;
        if (!expectedAttendees.HasValue && int.TryParse(session.GetString("Ack:Attendees"), out var ackAttendees)) expectedAttendees = ackAttendees;
        if (string.IsNullOrWhiteSpace(setupStyle)) setupStyle = session.GetString("Draft:SetupStyle");
        if (string.IsNullOrWhiteSpace(setupStyle)) setupStyle = session.GetString("Ack:SetupStyle");
        if (string.IsNullOrWhiteSpace(setupStyle) &&
            !string.IsNullOrWhiteSpace(roomName) &&
            roomName.Contains("Thrive", StringComparison.OrdinalIgnoreCase))
        {
            setupStyle = "boardroom";
        }
        _logger.LogInformation("[update_equipment] Event context: Venue={Venue}, Room={Room}, EventType={EventType}, Attendees={Attendees}",
            venueName ?? "(null)", roomName ?? "(null)", eventType ?? "(null)", expectedAttendees?.ToString() ?? "(null)");
        if (string.IsNullOrWhiteSpace(eventType) || !expectedAttendees.HasValue || expectedAttendees.Value <= 0 || string.IsNullOrWhiteSpace(setupStyle))
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(eventType)) missing.Add("event type");
            if (!expectedAttendees.HasValue || expectedAttendees.Value <= 0) missing.Add("number of attendees");
            if (string.IsNullOrWhiteSpace(setupStyle)) missing.Add("room setup style");
            _logger.LogWarning("[update_equipment] Blocked due to missing fields: {Fields}", string.Join(", ", missing));
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show updated quote summary - missing required event details",
                missingFields = missing,
                instruction = $"Do NOT call update_equipment or recommend_equipment_for_event again in this response. Ask ONE clear follow-up question to collect the next missing item ({string.Join(", ", missing)}), then wait for user reply before updating the summary."
            });
        }

        var addCouldNotFind = new List<string>();

        // Parse add_requests and optional event context; normalize "windows laptop" / "mac laptop" to type + preference
        if (root.TryGetProperty("add_requests", out var addArr) && addArr.ValueKind == JsonValueKind.Array && addArr.GetArrayLength() > 0)
        {
            var eventContext = new EventContext
            {
                EventType = !string.IsNullOrWhiteSpace(eventType) ? eventType : "",
                ExpectedAttendees = expectedAttendees ?? 0,
                VenueName = venueName,
                RoomName = roomName,
                ProjectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorAreas")),
                DurationDays = 1
            };
            if (eventContext.ProjectorAreas.Count == 0)
                eventContext.ProjectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorArea"));
            foreach (var add in addArr.EnumerateArray())
            {
                var eqTypeRaw = add.TryGetProperty("equipment_type", out var eqt) ? eqt.GetString() ?? "" : "";
                var qty = add.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt32() : 1;
                var preferenceFromArg = add.TryGetProperty("preference", out var pref) && pref.ValueKind == JsonValueKind.String ? pref.GetString() : null;
                if (string.IsNullOrWhiteSpace(eqTypeRaw)) continue;

                var eqType = eqTypeRaw.Trim().ToLowerInvariant();
                string? preference = preferenceFromArg;
                if (eqType.Contains("windows") || eqType.Contains("pc"))
                {
                    eqType = "laptop";
                    preference ??= "windows";
                    _logger.LogInformation("[update_equipment] Normalized add to laptop with preference=windows");
                }
                else if (eqType.Contains("mac"))
                {
                    eqType = "laptop";
                    preference ??= "mac";
                    _logger.LogInformation("[update_equipment] Normalized add to laptop with preference=mac");
                }

                eventContext.EquipmentRequests.Add(new EquipmentRequest
                {
                    EquipmentType = eqType,
                    Quantity = qty,
                    Preference = preference
                });
            }

            var recommendations = await _smartEquipment.GetRecommendationsAsync(eventContext, ct);
            if (recommendations.Items.Count == 0)
            {
                var requested = string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}"));
                _logger.LogWarning("[update_equipment] Add returned 0 items for: {Requested}. Keeping list after removals.", requested);
                foreach (var r in eventContext.EquipmentRequests)
                    addCouldNotFind.Add($"{r.Quantity}x {r.EquipmentType}");
            }
            else
            {
                foreach (var item in recommendations.Items)
                {
                    var code = (item.ProductCode ?? "").Trim();
                    var desc = (item.Description ?? "").Trim();
                    var qty = item.Quantity;
                    var existing = currentItems.FirstOrDefault(i =>
                        (!string.IsNullOrEmpty(code) && string.Equals((i.ProductCode ?? "").Trim(), code, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(desc) && string.Equals((i.Description ?? "").Trim(), desc, StringComparison.OrdinalIgnoreCase)));
                    if (existing != null)
                    {
                        existing.Quantity += qty;
                        if (item.IsPackage)
                            existing.IsPackage = true;
                        _logger.LogInformation("[update_equipment] Merged into existing: {Qty} more → {Total}x {Description}", qty, existing.Quantity, existing.Description);
                    }
                    else
                    {
                        currentItems.Add(new SelectedEquipmentItem
                        {
                            ProductCode = code,
                            Description = desc,
                            Quantity = qty,
                            IsPackage = item.IsPackage,
                            ParentPackageCode = null,
                            Comment = item.Comment
                        });
                        _logger.LogInformation("[update_equipment] Added item: {Qty}x {Description}", qty, desc);
                    }
                }
            }
        }

        if (currentItems.Count == 0)
        {
            _logger.LogWarning("[update_equipment] Equipment list would be empty after changes");
            return JsonSerializer.Serialize(new
            {
                error = "Equipment list would be empty after your changes.",
                instruction = "Do NOT call update_equipment or recommend_equipment_for_event again in this response. Do NOT output a quote summary card or an empty equipment list. Tell the user their change would leave the quote empty and suggest keeping or adding at least one item (e.g. microphone, screen, laptop). Then they can request edits again."
            });
        }

        // Look up unit prices for summary
        var productCodes = currentItems.Select(i => i.ProductCode).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        var ratesList = await _db.TblRatetbls.AsNoTracking()
            .Where(r => productCodes.Contains(r.product_code ?? "") && r.TableNo == 0)
            .Select(r => new { r.product_code, r.rate_1st_day })
            .ToListAsync(ct);
        var rates = ratesList
            .GroupBy(x => x.product_code ?? "")
            .ToDictionary(g => g.Key, g => (double)(g.First().rate_1st_day ?? 0));

        double totalDayRate = 0;
        var summaryRequests = GetSummaryRequestsFromSession(session, currentItems);
        ApplySummaryRequestRemovals(summaryRequests, removeTypes);
        AppendSummaryRequests(summaryRequests, root);

        _logger.LogInformation("[update_equipment] Calculating total day rate from {Count} selected items", currentItems.Count);
        foreach (var item in currentItems)
        {
            _logger.LogInformation("[update_equipment] Processing item: {Qty}x {Desc} (Code: {Code})", item.Quantity, item.Description, item.ProductCode);
            var rate = rates.GetValueOrDefault(item.ProductCode ?? "", 0);
            totalDayRate += rate * item.Quantity;
        }
        if (addCouldNotFind.Count > 0)
            _logger.LogWarning("[update_equipment] Could not find {Count} requested items: {Items}", addCouldNotFind.Count, string.Join(", ", addCouldNotFind));

        // Recalculate technician support from updated equipment so Technician Support section is not dropped
        List<RecommendedLaborItem> laborItems = new List<RecommendedLaborItem>();
        try
        {
            var equipmentForLabor = currentItems.Select(i => new EquipmentItemForLabor
            {
                ProductCode = i.ProductCode ?? "",
                Description = i.Description ?? "",
                Quantity = i.Quantity
            }).ToList();
            var updateEventContext = new EventContext
            {
                EventType = !string.IsNullOrWhiteSpace(eventType) ? eventType : "",
                ExpectedAttendees = expectedAttendees ?? 0,
                VenueName = venueName,
                RoomName = roomName,
                DurationDays = 1,
                IsContentHeavy = string.Equals(session.GetString("Draft:IsContentHeavy"), "1", StringComparison.OrdinalIgnoreCase),
                IsContentLight = string.Equals(session.GetString("Draft:IsContentLight"), "1", StringComparison.OrdinalIgnoreCase)
            };
            var recommended = await _smartEquipment.RecommendLaborForEquipmentAsync(equipmentForLabor, updateEventContext, ct);
            laborItems = recommended?.ToList() ?? new List<RecommendedLaborItem>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[update_equipment] Labor recalculation failed; falling back to existing Draft:SelectedLabor if any");
            var existingLaborJson = session.GetString("Draft:SelectedLabor");
            if (!string.IsNullOrWhiteSpace(existingLaborJson))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<List<SelectedLaborItem>>(existingLaborJson);
                    if (existing != null && existing.Count > 0)
                    {
                        laborItems = existing.Select(l => new RecommendedLaborItem
                        {
                            ProductCode = string.IsNullOrWhiteSpace(l.ProductCode) ? "AVTECH" : l.ProductCode,
                            Description = l.Description ?? "",
                            Task = l.Task ?? "",
                            Quantity = l.Quantity,
                            Hours = l.Hours,
                            Minutes = l.Minutes,
                            RecommendationReason = "From previous recommendation"
                        }).ToList();
                    }
                }
                catch { /* ignore */ }
            }
        }

        if (laborItems.Count > 0)
        {
            var storedCoverage = session.GetString("Draft:TechnicianCoverage");
            if (!string.IsNullOrWhiteSpace(storedCoverage))
            {
                try
                {
                    using var coverageDoc = JsonDocument.Parse(storedCoverage);
                    var coverageRoot = coverageDoc.RootElement;
                    var hasCoverage = coverageRoot.ValueKind == JsonValueKind.Object;
                    if (hasCoverage)
                    {
                        var noTechnician = coverageRoot.TryGetProperty("NoTechnicianSupport", out var noTechProp) && noTechProp.ValueKind == JsonValueKind.True;
                        var stored = new TechnicianCoveragePreference(
                            hasCoverage,
                            noTechnician,
                            coverageRoot.TryGetProperty("Setup", out var setupProp) && setupProp.ValueKind == JsonValueKind.True,
                            coverageRoot.TryGetProperty("Rehearsal", out var rehearsalProp) && rehearsalProp.ValueKind == JsonValueKind.True,
                            coverageRoot.TryGetProperty("Operate", out var operateProp) && operateProp.ValueKind == JsonValueKind.True,
                            coverageRoot.TryGetProperty("Packdown", out var packdownProp) && packdownProp.ValueKind == JsonValueKind.True
                        );

                        laborItems = stored.NoTechnicianSupport
                            ? new List<RecommendedLaborItem>()
                            : ApplyTechnicianCoveragePreference(laborItems, stored);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[update_equipment] Failed to parse Draft:TechnicianCoverage");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Source: "Would you like an operator for your rehearsal?" (Draft:RehearsalOperator)
        //   YES → Adds Rehearsal task (30 mins) using current operate template
        //   NO  → Removes any existing Rehearsal labor items
        // ─────────────────────────────────────────────────────────────────────
        var sessionRehearsalOp = (session.GetString("Draft:RehearsalOperator") ?? "").Trim().ToLowerInvariant();
        if (sessionRehearsalOp == "yes" && !laborItems.Any(l => IsRehearsalLaborTask(l.Task)))
        {
            var operateTemplate = laborItems.FirstOrDefault(l => IsOperateLaborTask(l.Task));
            var productCode = operateTemplate?.ProductCode ?? "AVTECH";
            var description = operateTemplate?.Description ?? "AV Technician";
            laborItems.Add(new RecommendedLaborItem
            {
                ProductCode = productCode,
                Description = description,
                Task = "Rehearsal",
                Quantity = 1,
                Hours = 0,
                Minutes = 30,
                RecommendationReason = "Customer confirmed they would like an operator for their rehearsal."
            });
            laborItems = laborItems
                .OrderBy(GetLaborTaskSortOrder)
                .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else if (sessionRehearsalOp == "no")
        {
            laborItems = laborItems
                .Where(l => !IsRehearsalLaborTask(l.Task))
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Source: "Would you like a microphone operator?" (Draft:MicrophoneOperator)
        //   YES → Adds 30 mins AXTECH Rehearsal (only if rehearsal op = NO)
        //         Adds AXTECH Operate (event duration, only if operator throughout = NO)
        //         If V1HD switcher present, uses AVTECH instead of AXTECH (combo rule)
        //   NO  → Removes any AXTECH Rehearsal/Operate items
        // ─────────────────────────────────────────────────────────────────────
        var sessionMicOp = (session.GetString("Draft:MicrophoneOperator") ?? "").Trim().ToLowerInvariant();

        if (sessionMicOp != "yes")
        {
            laborItems.RemoveAll(l =>
                string.Equals(l.ProductCode, "AXTECH", StringComparison.OrdinalIgnoreCase) &&
                (IsRehearsalLaborTask(l.Task) || IsOperateLaborTask(l.Task)));
        }

        if (sessionMicOp == "yes")
        {
            var hasSwitcherInEquipment = currentItems.Any(i =>
                string.Equals((i.ProductCode ?? "").Trim(), "V1HD", StringComparison.OrdinalIgnoreCase));
            var techCode = hasSwitcherInEquipment ? "AVTECH" : "AXTECH";
            var techDesc = hasSwitcherInEquipment ? "AV Technician" : "Audio Technician";

            // When combo (mic + switcher), also remove any VXTECH Rehearsal/Operate that was added by V1HD alone
            if (hasSwitcherInEquipment)
            {
                laborItems.RemoveAll(l =>
                    string.Equals(l.ProductCode, "VXTECH", StringComparison.OrdinalIgnoreCase) &&
                    (IsRehearsalLaborTask(l.Task) || IsOperateLaborTask(l.Task)));
            }

            // Add Rehearsal 30 mins if not already present AND rehearsal operator was not already confirmed
            if (sessionRehearsalOp != "yes"
                && !laborItems.Any(l =>
                    string.Equals(l.ProductCode, techCode, StringComparison.OrdinalIgnoreCase) &&
                    IsRehearsalLaborTask(l.Task)))
            {
                laborItems.Add(new RecommendedLaborItem
                {
                    ProductCode = techCode,
                    Description = techDesc,
                    Task = "Rehearsal",
                    Quantity = 1,
                    Hours = 0,
                    Minutes = 30,
                    RecommendationReason = "Customer confirmed they would like a microphone operator."
                });
            }

            // Add Operate (event duration) if not already present AND "operator throughout" was not already selected
            var storedCoverageForMicOp = TryLoadTechnicianCoverageFromSession(session);
            if ((storedCoverageForMicOp == null || !storedCoverageForMicOp.Operate)
                && !laborItems.Any(l =>
                    string.Equals(l.ProductCode, techCode, StringComparison.OrdinalIgnoreCase) &&
                    IsOperateLaborTask(l.Task)))
            {
                laborItems.Add(new RecommendedLaborItem
                {
                    ProductCode = techCode,
                    Description = techDesc,
                    Task = "Operate",
                    Quantity = 1,
                    Hours = 0,
                    Minutes = 0,
                    RecommendationReason = "Customer confirmed they would like a microphone operator for the duration of the event."
                });
            }

            laborItems = laborItems
                .OrderBy(GetLaborTaskSortOrder)
                .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Source: "Do you need to seamlessly switch between the laptops?" = YES
        //         (V1HD switcher in equipment) AND mic operator NOT confirmed
        // Adds: VXTECH Rehearsal (30 mins, only if rehearsal op = NO)
        //       VXTECH Operate (event duration, only if operator throughout = NO)
        // ─────────────────────────────────────────────────────────────────────
        if (sessionMicOp != "yes")
        {
            var hasSwitcherInItems = currentItems.Any(i =>
                string.Equals((i.ProductCode ?? "").Trim(), "V1HD", StringComparison.OrdinalIgnoreCase));
            if (hasSwitcherInItems)
            {
                var wantsOperator = (session.GetString("Draft:WantsOperator") ?? "").Trim().ToLowerInvariant();
                if (wantsOperator != "yes")
                {
                    if (!laborItems.Any(l =>
                        string.Equals(l.ProductCode, "VXTECH", StringComparison.OrdinalIgnoreCase) &&
                        IsOperateLaborTask(l.Task)))
                    {
                        laborItems.Add(new RecommendedLaborItem
                        {
                            ProductCode = "VXTECH",
                            Description = "Vision Technician",
                            Task = "Operate",
                            Quantity = 1,
                            Hours = 0,
                            Minutes = 0,
                            RecommendationReason = "Seamless laptop switching: Vision Technician operates for event duration."
                        });
                    }
                }
                if (sessionRehearsalOp != "yes"
                    && !laborItems.Any(l =>
                        string.Equals(l.ProductCode, "VXTECH", StringComparison.OrdinalIgnoreCase) &&
                        IsRehearsalLaborTask(l.Task)))
                {
                    laborItems.Add(new RecommendedLaborItem
                    {
                        ProductCode = "VXTECH",
                        Description = "Vision Technician",
                        Task = "Rehearsal",
                        Quantity = 1,
                        Hours = 0,
                        Minutes = 30,
                        RecommendationReason = "Seamless laptop switching requires rehearsal."
                    });
                }
                laborItems = laborItems
                    .OrderBy(GetLaborTaskSortOrder)
                    .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FINAL CLEANUP — Defensive enforcement of session-level operator answers
        // ═══════════════════════════════════════════════════════════════════════
        // Catches edge cases where filters/clones may have leaked specialist labor
        // (matched by either ProductCode OR Description) past prior cleanup steps.
        {
            var finalMicOp = (session.GetString("Draft:MicrophoneOperator") ?? "").Trim().ToLowerInvariant();
            var finalHasSwitcher = currentItems.Any(i =>
                string.Equals((i.ProductCode ?? "").Trim(), "V1HD", StringComparison.OrdinalIgnoreCase));

            // Source: "Would you like a microphone operator?" = NO (or unanswered)
            // Removes: any Audio Technician (AXTECH) Rehearsal/Operate items
            if (finalMicOp != "yes")
            {
                laborItems.RemoveAll(l =>
                    (string.Equals(l.ProductCode, "AXTECH", StringComparison.OrdinalIgnoreCase) ||
                     (l.Description ?? "").Contains("Audio Technician", StringComparison.OrdinalIgnoreCase)) &&
                    (IsRehearsalLaborTask(l.Task) || IsOperateLaborTask(l.Task)));
            }

            // Source: V1HD switcher NOT in equipment (i.e. "Do you need to seamlessly switch?" = NO)
            // Removes: any Vision Technician (VXTECH) Rehearsal/Operate items
            if (!finalHasSwitcher)
            {
                laborItems.RemoveAll(l =>
                    (string.Equals(l.ProductCode, "VXTECH", StringComparison.OrdinalIgnoreCase) ||
                     (l.Description ?? "").Contains("Vision Technician", StringComparison.OrdinalIgnoreCase)) &&
                    (IsRehearsalLaborTask(l.Task) || IsOperateLaborTask(l.Task)));
            }

            // Source: "Would you like an operator throughout your event?" = YES
            // Adds: AVTECH Operate (event duration) if no Operate task remains after cleanup
            var finalWantsOperator = (session.GetString("Draft:WantsOperator") ?? "").Trim().ToLowerInvariant();
            if (finalWantsOperator == "yes" && !laborItems.Any(l => IsOperateLaborTask(l.Task)))
            {
                laborItems.Add(new RecommendedLaborItem
                {
                    ProductCode = "AVTECH",
                    Description = "AV Technician",
                    Task = "Operate",
                    Quantity = 1,
                    Hours = 0,
                    Minutes = 0,
                    RecommendationReason = "Customer requested operator throughout event."
                });
                laborItems = laborItems
                    .OrderBy(GetLaborTaskSortOrder)
                    .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        var projectorAreasForSummary = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorAreas"));
        if (projectorAreasForSummary.Count == 0)
            projectorAreasForSummary = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorArea"));
        var showProjectorPlacement = SelectedEquipmentSuggestsProjectorPlacementForSummary(currentItems)
            && projectorAreasForSummary.Count > 0;

        var scheduleInfo = new TechnicianScheduleInfo(
            session.GetString("Draft:SetupTime"),
            session.GetString("Draft:RehearsalTime"),
            session.GetString("Draft:StartTime"),
            session.GetString("Draft:EndTime"),
            session.GetString("Draft:PackupTime"));

        var summaryLines = new List<string>
        {
            "## 📋 Quote Summary\n",
            "### Event Details"
        };
        if (!string.IsNullOrWhiteSpace(venueName)) summaryLines.Add($"**Venue:** {venueName}");
        if (!string.IsNullOrWhiteSpace(roomName)) summaryLines.Add($"**Room:** {roomName}");
        if (showProjectorPlacement)
        {
            if (projectorAreasForSummary.Count == 1)
                summaryLines.Add($"**Projector Placement Area:** {projectorAreasForSummary[0]}");
            else
                summaryLines.Add($"**Projector Placement Areas:** {string.Join(", ", projectorAreasForSummary)}");
        }

        summaryLines.Add($"**Event Type:** {eventType}");
        summaryLines.Add($"**Attendees:** {expectedAttendees}");
        summaryLines.Add("");
        summaryLines.Add("### Requirement Summary\n");
        foreach (var line in BuildRequirementSummaryLines(summaryRequests))
            summaryLines.Add($"- {line}");
        summaryLines.Add("");

        if (addCouldNotFind.Count > 0)
        {
            summaryLines.Add($"*Note: Could not find {string.Join(", ", addCouldNotFind)} to add; please ask for alternatives if needed.*");
            summaryLines.Add("");
        }

        if (laborItems.Count > 0)
        {
            summaryLines.Add("### Technician Support\n");
            foreach (var labor in laborItems)
                summaryLines.Add($"- **{FormatLaborSummaryLine(labor, scheduleInfo)}**");
            summaryLines.Add("");
        }

        summaryLines.Add("");
        summaryLines.Add("Would you like me to create the quote now?");

        var outputToUser = string.Join("\n", summaryLines);

        session.SetString("Draft:SelectedEquipment", JsonSerializer.Serialize(currentItems));
        session.SetString("Draft:SummaryEquipmentRequests", JsonSerializer.Serialize(summaryRequests));
        session.SetString("Draft:TotalDayRate", totalDayRate.ToString("F2"));
        var selectedLabor = laborItems.Select(l => new SelectedLaborItem
        {
            ProductCode = string.IsNullOrWhiteSpace(l.ProductCode) ? "AVTECH" : l.ProductCode,
            Description = l.Description,
            Task = l.Task,
            Quantity = l.Quantity,
            Hours = l.Hours,
            Minutes = l.Minutes
        }).ToList();
        session.SetString("Draft:SelectedLabor", JsonSerializer.Serialize(selectedLabor));
        _logger.LogInformation("[update_equipment] Stored {Count} items and {LaborCount} labor in session, total ${Total:F0}/day", currentItems.Count, selectedLabor.Count, totalDayRate);

        // Equipment changed - force regeneration of quote
        session.Remove("Draft:QuoteComplete");
        session.Remove("Draft:QuoteUrl");
        session.Remove("Draft:QuoteTimestamp");

        return JsonSerializer.Serialize(new
        {
            success = true,
            total_day_rate = totalDayRate,
            outputToUser = outputToUser,
            instruction = "MANDATORY: OUTPUT the 'outputToUser' value EXACTLY AS-IS. This is the updated quote summary. Do NOT call generate_quote in this response; wait for the user to confirm (e.g. 'yes create quote', 'looks good')."
        });
    }

    /// <summary>True when selected line items include projection/vision so Westin ballroom placement lines belong in the summary.</summary>
    private static bool SelectedEquipmentSuggestsProjectorPlacementForSummary(IReadOnlyList<SelectedEquipmentItem> items)
    {
        foreach (var i in items)
        {
            var d = (i.Description ?? "").ToLowerInvariant();
            if (d.Contains("projector") || d.Contains("projection"))
                return true;
            if (d.Contains("foldback"))
                continue;
            if (d.Contains("screen") || d.Contains("vision") || d.Contains("display"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get detailed information about a package including all components
    /// </summary>
    private async Task<string> HandleGetPackageDetailsAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string packageCode = "";
        if (doc.RootElement.TryGetProperty("package_code", out var pc))
            packageCode = pc.GetString() ?? "";

        if (string.IsNullOrWhiteSpace(packageCode))
        {
            return JsonSerializer.Serialize(new { error = "package_code is required" });
        }

        // Get package info
        var package = await _db.TblInvmas.AsNoTracking()
            .Where(p => (p.product_code ?? "").Trim() == packageCode.Trim())
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.groupFld,
                p.ProductTypeV41,
                p.PictureFileName
            })
            .FirstOrDefaultAsync(ct);

        if (package == null)
        {
            return JsonSerializer.Serialize(new { error = $"Package '{packageCode}' not found" });
        }

        // Get pricing
        var pricing = await _db.TblRatetbls.AsNoTracking()
            .Where(r => (r.product_code ?? "").Trim() == packageCode.Trim() && r.TableNo == 0)
            .Select(r => new { r.rate_1st_day, r.rate_extra_days })
            .FirstOrDefaultAsync(ct);

        // Get components
        var components = await _equipmentSearch.GetPackageComponentsAsync(packageCode, ct);

        return JsonSerializer.Serialize(new
        {
            package_code = package.product_code?.Trim(),
            description = (package.descriptionv6 ?? package.PrintedDesc ?? "").Trim(),
            category = package.category,
            group = package.groupFld,
            is_package = package.ProductTypeV41 == 1,
            picture = package.PictureFileName,
            day_rate = pricing?.rate_1st_day ?? 0,
            extra_day_rate = pricing?.rate_extra_days ?? 0,
            components = components.Select(c => new
            {
                product_code = c.ProductCode,
                description = c.Description,
                quantity = c.Quantity,
                is_variable = c.IsVariable
            }).ToList(),
            component_count = components.Count,
            message = $"The {package.descriptionv6?.Trim()} package includes {components.Count} items"
        });
    }

    private string? GetBaseUrl()
    {
        var request = _http.HttpContext?.Request;
        if (request == null) return null;
        return $"{request.Scheme}://{request.Host}";
    }

    private string ToAbsoluteUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return "";

        var request = _http.HttpContext?.Request;
        if (request == null) return relativePath;

        var scheme = request.Scheme;
        var host = request.Host.ToUriComponent();
        return $"{scheme}://{host}{relativePath}";
    }

    /// <summary>
    /// Validates that equipment mentioned in conversation is included in the request.
    /// Returns a list of warnings for potentially missing equipment.
    /// </summary>
    private List<string> ValidateEquipmentContextFromConversation(string argsJson)
    {
        var warnings = new List<string>();

        try
        {
            // Get conversation from session (if available)
            var session = _http.HttpContext?.Session;
            var conversationText = session?.GetString("Draft:ConversationSummary") ?? "";

            // If no summary, try to get it from other session fields that might contain requirements
            if (string.IsNullOrWhiteSpace(conversationText))
            {
                var eventNotes = session?.GetString("Draft:EventNotes") ?? "";
                var avRequirements = session?.GetString("Draft:AVRequirements") ?? "";
                conversationText = $"{eventNotes} {avRequirements}";
            }

            // Parse the equipment requests from the args
            var requestedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(argsJson))
            {
                using var doc = JsonDocument.Parse(argsJson);
                if (doc.RootElement.TryGetProperty("equipment_requests", out var eqArray) && eqArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in eqArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("equipment_type", out var eqt))
                        {
                            var type = eqt.GetString()?.ToLowerInvariant() ?? "";
                            requestedTypes.Add(type);
                            // Also add related types
                            if (type.Contains("projector") || type.Contains("screen"))
                            {
                                requestedTypes.Add("projector");
                                requestedTypes.Add("screen");
                            }
                        }
                    }
                }
            }

            // Define keyword to equipment mappings
            var keywordMappings = new Dictionary<string[], string[]>(new ArrayComparer())
            {
                { new[] { "teams", "zoom", "video call", "video conference", "remote", "hybrid", "webcam" }, new[] { "camera", "microphone", "speaker", "display" } },
                { new[] { "presentation", "slides", "powerpoint", "present" }, new[] { "projector", "screen" } },
                { new[] { "video with sound", "play video", "audio playback", "music", "sound system" }, new[] { "speaker" } },
                { new[] { "speech", "presenter", "speaker at event", "speaking" }, new[] { "microphone" } },
                { new[] { "record", "recording", "film", "capture video" }, new[] { "camera" } }
            };

            // Check conversation for keywords
            var convLower = conversationText.ToLowerInvariant();
            foreach (var mapping in keywordMappings)
            {
                var keywords = mapping.Key;
                var requiredEquipment = mapping.Value;

                // Check if any keyword is mentioned
                var keywordFound = keywords.FirstOrDefault(k => convLower.Contains(k));
                if (keywordFound != null)
                {
                    // Check if all required equipment is in the request
                    foreach (var equipment in requiredEquipment)
                    {
                        if (!requestedTypes.Any(rt => rt.Contains(equipment) || equipment.Contains(rt)))
                        {
                            warnings.Add($"User mentioned '{keywordFound}' but '{equipment}' is not in the equipment request. Please confirm if this is needed.");
                        }
                    }
                }
            }

            if (warnings.Count > 0)
            {
                _logger.LogWarning("Context validation found potential missing equipment: {Warnings}", string.Join("; ", warnings));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating equipment context from conversation");
        }

        return warnings;
    }

    /// <summary>
    /// Custom comparer for string arrays in dictionary
    /// </summary>
    private class ArrayComparer : IEqualityComparer<string[]>
    {
        public bool Equals(string[]? x, string[]? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.SequenceEqual(y);
        }

        public int GetHashCode(string[] obj)
        {
            return obj.Aggregate(0, (a, b) => HashCode.Combine(a, b?.GetHashCode() ?? 0));
        }
    }
}
