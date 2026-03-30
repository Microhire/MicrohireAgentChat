// AzureAgentChatService partial
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using Markdig;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services
{
    public sealed partial class AzureAgentChatService
    {
        /// <summary>
        /// Tools handled entirely by <see cref="AgentToolHandlerService.HandleToolCallAsync"/> that do not mutate session
        /// or booking state — safe to run with a scoped handler + isolated <see cref="BookingDbContext"/> in parallel.
        /// </summary>
        private static readonly HashSet<string> ParallelSafeReadOnlyTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "check_date_availability",
            "get_now_aest",
            "list_westin_rooms",
            "build_time_picker",
            "build_contact_form",
            "build_event_form",
            "build_av_extras_form",
            "get_room_images",
            "get_product_info",
            "get_product_images",
            "build_equipment_picker",
            "search_equipment",
            "get_equipment_recommendations",
            "get_package_details",
            "show_equipment_alternatives",
            "get_product_knowledge",
            "get_westin_venue_guide",
            "get_capacity_table",
            "get_room_capacity"
        };

        private static bool IsParallelSafeReadOnlyTool(string? name)
            => !string.IsNullOrEmpty(name) && ParallelSafeReadOnlyTools.Contains(name);

        private async Task<Azure.AI.Agents.Persistent.ThreadRun> RunAgentAndHandleToolsAsync(string threadId, string agentId, CancellationToken ct)
        {
            // AUTO-PERSIST CONTACT INFO BEFORE TOOL CALLS
            // Extract contact info from conversation and persist to session BEFORE any tools run
            // This ensures generate_quote and recommend_equipment_for_event find contact details
            try
            {
                var (_, existingMessages) = GetTranscript(threadId);
                AutoPersistContactInfoToSession(existingMessages);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-persist contact info before tool calls");
            }
            
            // Create the run with retry
            var run = await With429RetryAsync(
                () => Task.FromResult(AgentsClient.Runs.CreateRun(threadId, agentId).Value),
                "CreateRun", ct);
            // #region agent log
            EmitToolLoopDebugLog(
                run.Id,
                "H6",
                "AzureAgentChatService.ToolLoop.cs:run-created",
                "Run created for thread",
                new
                {
                    threadId,
                    initialStatus = run.Status.ToString()
                });
            // #endregion

            try
            {
                _http.HttpContext?.Session.SetString(SessionKeyActiveRunId, run.Id);

                var startUtc = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(5);
                var perToolTimeout = TimeSpan.FromSeconds(45);
                var delayMs = 250;          // responsive first polls; backoff when idle
                var maxDelayMs = 7000;      // ramp polling up to ~7s

                var lastStatus = run.Status;
                while (true)
                {
                    // Poll with retry
                    run = await With429RetryAsync(
                        () => Task.FromResult(AgentsClient.Runs.GetRun(threadId, run.Id).Value),
                        "GetRun", ct);

                    if (run.Status != lastStatus)
                    {
                        _logger.LogInformation("Run {RunId} on thread {ThreadId} changed status: {OldStatus} -> {NewStatus}", run.Id, threadId, lastStatus, run.Status);
                        lastStatus = run.Status;
                        delayMs = 250; // reset backoff after status change for faster follow-up polls
                    }

                    if (IsTerminal(run.Status)) return run;

                    if (run.Status == Azure.AI.Agents.Persistent.RunStatus.RequiresAction && run.RequiredAction != null)
                    {
                        _logger.LogInformation("Run {RunId} on thread {ThreadId} requires action. RequiredAction type: {ActionType}", run.Id, threadId, run.RequiredAction.GetType().FullName);
                        
                        dynamic action = run.RequiredAction!;
                        IEnumerable<dynamic>? toolCalls = null;

                        try { toolCalls = (IEnumerable<dynamic>)action.ToolCalls; } catch { }
                        if (toolCalls == null) { try { toolCalls = (IEnumerable<dynamic>)action.SubmitToolOutputs.ToolCalls; } catch { } }
                        if (toolCalls == null) { try { toolCalls = (IEnumerable<dynamic>)action.SubmitToolOutputsAction.ToolCalls; } catch { } }
                        
                        // Try a few more variations just in case the SDK changes
                        if (toolCalls == null) { try { toolCalls = (IEnumerable<dynamic>)action.RequiredAction.ToolCalls; } catch { } }
                        if (toolCalls == null) { try { toolCalls = (IEnumerable<dynamic>)action.RequiredAction.SubmitToolOutputs.ToolCalls; } catch { } }

                        if (toolCalls == null || !toolCalls.Any())
                        {
                            string statusStr = run.Status.ToString();
                            string actionType = action.GetType().FullName ?? "Unknown";
                            string actionJson = JsonSerializer.Serialize(run.RequiredAction);
                            _logger.LogWarning("RequiredAction present but ToolCalls not found or empty. Status={Status}, Type={ActionType}. RequiredAction: {ActionJson}", 
                                statusStr, actionType, actionJson);
                            // #region agent log
                            EmitToolLoopDebugLog(
                                run.Id,
                                "H5",
                                "AzureAgentChatService.ToolLoop.cs:requires-action-empty-toolcalls",
                                "Run entered requires_action but tool calls were empty",
                                new
                                {
                                    threadId,
                                    status = statusStr,
                                    actionType
                                });
                            // #endregion
                            
                            // Cancel the run to prevent it from hanging indefinitely
                            try
                            {
                                AgentsClient.Runs.CancelRun(threadId, run.Id);
                                _logger.LogInformation("Cancelled stuck run {RunId} for thread {ThreadId}", run.Id, threadId);
                            }
                            catch (Exception cancelEx)
                            {
                                _logger.LogWarning(cancelEx, "Failed to cancel stuck run {RunId}", run.Id);
                            }
                            
                            // Return the run so caller can handle appropriately
                            return run;
                        }

                        // toolCalls is not null and has items, process them
                        {
                            _logger.LogInformation("Processing {Count} tool calls for run {RunId}", toolCalls.Count(), run.Id);
                            var outputs = new List<(string Id, string Output)>();
                            var callList = new List<(string Id, string Name, string ArgsJson)>();
                            foreach (var call in toolCalls)
                            {
                                var tcId = (string)call.Id;
                                string tName = "unknown";
                                string tArgs = "{}";
                                try { tName = (string)call.Function.Name; } catch { try { tName = (string)call.Name; } catch { } }
                                try { tArgs = (string)(call.Function.Arguments ?? "{}"); } catch { try { tArgs = (string)(call.Arguments ?? "{}"); } catch { } }
                                tArgs ??= "{}";
                                callList.Add((tcId, tName, tArgs));
                            }

                            var parallelFilled = new ConcurrentDictionary<int, (string Id, string Output)>();
                            var parallelJobs = callList.Select((c, i) => (i, c)).Where(x => IsParallelSafeReadOnlyTool(x.c.Name)).ToList();
                            if (parallelJobs.Count > 0)
                            {
                                _logger.LogInformation("Running {ParallelCount} of {Total} tool calls in parallel for run {RunId}", parallelJobs.Count, callList.Count, run.Id);
                                await Task.WhenAll(parallelJobs.Select(async job =>
                                {
                                    var (i, c) = job;
                                    var (pToolCallId, pName, pArgsJson) = c;
                                    try
                                    {
                                        await using var scope = _scopeFactory.CreateAsyncScope();
                                        var handler = scope.ServiceProvider.GetRequiredService<AgentToolHandlerService>();
                                        var pStart = DateTime.UtcNow;
                                        string? pHandled;
                                        using (var toolCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                                        {
                                            toolCts.CancelAfter(perToolTimeout);
                                            pHandled = await handler.HandleToolCallAsync(pName, pArgsJson, threadId, toolCts.Token).ConfigureAwait(false);
                                        }
                                        var pDuration = DateTime.UtcNow - pStart;
                                        if (pDuration.TotalSeconds > 5)
                                            _logger.LogWarning("Slow tool call (parallel): {ToolName} took {Duration}s", pName, pDuration.TotalSeconds);
                                        else
                                            _logger.LogInformation("Tool call {ToolName} completed in {Duration}s (parallel)", pName, pDuration.TotalSeconds);
                                        if (pHandled != null)
                                            parallelFilled[i] = (pToolCallId, pHandled);
                                    }
                                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                                    {
                                        _logger.LogWarning("Tool {ToolName} timed out after {TimeoutSeconds}s for run {RunId} (parallel)", pName, perToolTimeout.TotalSeconds, run.Id);
                                        parallelFilled[i] = (pToolCallId, JsonSerializer.Serialize(new
                                        {
                                            error = $"Tool '{pName}' timed out after {(int)perToolTimeout.TotalSeconds} seconds.",
                                            instruction = "Tell the user there was a temporary processing delay and ask them to retry."
                                        }));
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Tool {ToolName} failed (parallel)", pName);
                                        parallelFilled[i] = (pToolCallId, JsonSerializer.Serialize(new { error = ex.Message }));
                                    }
                                }));
                            }

                            for (var idx = 0; idx < callList.Count; idx++)
                            {
                                var (toolCallId, name, argsJson) = callList[idx];
                                if (parallelFilled.TryGetValue(idx, out var parallelDone))
                                {
                                    TrackPendingGalleries(parallelDone.Output);
                                    outputs.Add((parallelDone.Id, parallelDone.Output));
                                    continue;
                                }

                                _logger.LogInformation("Tool call: {ToolName} (ID: {ToolCallId})", name, toolCallId);
                                // #region agent log
                                EmitToolLoopDebugLog(
                                    run.Id,
                                    "H6",
                                    "AzureAgentChatService.ToolLoop.cs:tool-call-dispatch",
                                    "Tool call dispatched for handling",
                                    new
                                    {
                                        threadId,
                                        toolName = name,
                                        toolCallId,
                                            argsLength = argsJson.Length
                                    });
                                // #endregion
                                if (string.Equals(name, "recommend_equipment_for_event", StringComparison.OrdinalIgnoreCase))
                                {
                                    var hasVenue = false;
                                    var hasRoom = false;
                                    var hasSetup = false;
                                    var equipmentTypes = new List<string>();
                                    try
                                    {
                                        using var argsDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
                                        var argsRoot = argsDoc.RootElement;
                                        hasVenue = argsRoot.TryGetProperty("venue_name", out var venueProp)
                                            && venueProp.ValueKind == JsonValueKind.String
                                            && !string.IsNullOrWhiteSpace(venueProp.GetString());
                                        hasRoom = argsRoot.TryGetProperty("room_name", out var roomProp)
                                            && roomProp.ValueKind == JsonValueKind.String
                                            && !string.IsNullOrWhiteSpace(roomProp.GetString());
                                        hasSetup = argsRoot.TryGetProperty("setup_style", out var setupProp)
                                            && setupProp.ValueKind == JsonValueKind.String
                                            && !string.IsNullOrWhiteSpace(setupProp.GetString());
                                        if (argsRoot.TryGetProperty("equipment_requests", out var eqProp) && eqProp.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var eq in eqProp.EnumerateArray())
                                            {
                                                if (eq.TryGetProperty("equipment_type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                                                {
                                                    var typeValue = (typeProp.GetString() ?? "").Trim().ToLowerInvariant();
                                                    if (!string.IsNullOrWhiteSpace(typeValue))
                                                        equipmentTypes.Add(typeValue);
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // Ignore parse errors for debug-only payload.
                                    }

                                    // #region agent log
                                    EmitToolLoopDebugLog(
                                        run.Id,
                                        "H3",
                                        "AzureAgentChatService.ToolLoop.cs:recommend-equipment-call",
                                        "recommend_equipment_for_event tool call captured",
                                        new
                                        {
                                            threadId,
                                            hasVenue,
                                            hasRoom,
                                            hasSetup,
                                            equipmentTypes = equipmentTypes.Distinct().OrderBy(x => x).ToList(),
                                            argsLength = argsJson.Length
                                        });
                                    // #endregion
                                }

                                try
                                {
                                    // Delegate tool handling to AgentToolHandlerService
                                    var toolStart = DateTime.UtcNow;
                                    string? handled;
                                    using (var toolCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                                    {
                                        toolCts.CancelAfter(perToolTimeout);
                                        handled = await _toolHandler.HandleToolCallAsync(name, argsJson, threadId, toolCts.Token);
                                    }
                                    var toolDuration = DateTime.UtcNow - toolStart;
                                    
                                    if (toolDuration.TotalSeconds > 5)
                                    {
                                        _logger.LogWarning("Slow tool call: {ToolName} took {Duration}s", name, toolDuration.TotalSeconds);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("Tool call {ToolName} completed in {Duration}s", name, toolDuration.TotalSeconds);
                                    }

                                    if (handled != null)
                                    {
                                        // Track any gallery content for potential injection
                                        TrackPendingGalleries(handled);
                                        outputs.Add((toolCallId, handled));
                                        continue;
                                    }

                                    // Handle remaining tools not yet extracted
                                    switch (name)
                                    {

                                        case "generate_quote":
                                            {
                                                // SIMPLIFIED FLOW: Generate HTML quote when equipment is confirmed
                                                // Auto-create booking if one doesn't exist
                                                
                                                var session = _http.HttpContext?.Session;
                                                var quoteGenerationApproved = string.Equals(session?.GetString("Draft:GenerateQuote"), "1", StringComparison.Ordinal);
                                                if (!quoteGenerationApproved)
                                                {
                                                    outputs.Add((toolCallId, QuoteGenerationToolOutput.SerializeAwaitingExplicitUserConfirmation()));
                                                    break;
                                                }
                                                session?.Remove("Draft:GenerateQuote");

                                                // ========== IDEMPOTENCY CHECK - Return existing quote if already generated ==========
                                                var existingQuoteUrl = session?.GetString("Draft:QuoteUrl");
                                                var quoteComplete = session?.GetString("Draft:QuoteComplete") == "1";
                                                var existingBookingNo = session?.GetString("Draft:BookingNo");
                                                if (!string.IsNullOrWhiteSpace(existingBookingNo))
                                                {
                                                    try
                                                    {
                                                        var venueOrRoomChanged = await _bookingPersistence
                                                            .SyncVenueAndRoomForBookingAsync(existingBookingNo, session, ct);
                                                        if (venueOrRoomChanged)
                                                        {
                                                            session?.Remove("Draft:QuoteUrl");
                                                            session?.Remove("Draft:QuoteComplete");
                                                            session?.Remove("Draft:QuoteTimestamp");
                                                            existingQuoteUrl = null;
                                                            quoteComplete = false;
                                                            _logger.LogInformation("[QUOTE GEN] Venue/room changed for booking {BookingNo}; forcing fresh quote generation", existingBookingNo);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        _logger.LogWarning(ex, "[QUOTE GEN] Failed to pre-sync venue/room for booking {BookingNo}; continuing", existingBookingNo);
                                                    }
                                                }
                                                
                                                if (quoteComplete && !string.IsNullOrWhiteSpace(existingQuoteUrl) && !string.IsNullOrWhiteSpace(existingBookingNo))
                                                {
                                                    _logger.LogInformation("Quote already exists for booking {BookingNo}, returning existing quote: {QuoteUrl}", existingBookingNo, existingQuoteUrl);
                                                    
                                                    // Build full URL for existing quote
                                                    var existingReq = _http.HttpContext?.Request;
                                                    var existingBaseUrl = (existingReq == null) ? "" : $"{existingReq.Scheme}://{existingReq.Host}";
                                                    var fullExistingUrl = existingQuoteUrl.StartsWith("http") ? existingQuoteUrl : $"{existingBaseUrl}{existingQuoteUrl}";
                                                    
                                                    outputs.Add((toolCallId, QuoteGenerationToolOutput.SerializeExistingQuoteReady(fullExistingUrl, existingBookingNo)));
                                                    break;
                                                }
                                                
                                                // Also check for existing quote files on disk (in case session was lost)
                                                // Disk fallback is for session-expiry resilience only.
                                                // If session still has equipment data, QuoteComplete was deliberately cleared
                                                // (equipment changed) — skip the fallback and regenerate fresh.
                                                var sessionHasEquipment = !string.IsNullOrWhiteSpace(session?.GetString("Draft:SelectedEquipment"));
                                                if (!string.IsNullOrWhiteSpace(existingBookingNo) && !sessionHasEquipment)
                                                {
                                                    var quotesDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
                                                    if (Directory.Exists(quotesDir))
                                                    {
                                                        var existingQuoteFiles = Directory.GetFiles(quotesDir, $"Quote-{existingBookingNo}-*.html")
                                                            .OrderByDescending(f => File.GetCreationTimeUtc(f))
                                                            .ToList();
                                                        
                                                        if (existingQuoteFiles.Any())
                                                        {
                                                            var mostRecentQuote = existingQuoteFiles.First();
                                                            var quoteAge = DateTime.UtcNow - File.GetCreationTimeUtc(mostRecentQuote);
                                                            
                                                            // If quote is less than 1 hour old, reuse it
                                                            if (quoteAge.TotalHours < 1)
                                                            {
                                                                var existingUrl = $"/files/quotes/{Path.GetFileName(mostRecentQuote)}";
                                                                
                                                                // Update session with existing quote
                                                                session?.SetString("Draft:QuoteUrl", existingUrl);
                                                                session?.SetString("Draft:QuoteComplete", "1");
                                                                session?.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
                                                                
                                                                _logger.LogInformation("Found existing quote file for booking {BookingNo} (age: {Age}), reusing: {QuoteUrl}", 
                                                                    existingBookingNo, quoteAge, existingUrl);
                                                                
                                                                var existingReq2 = _http.HttpContext?.Request;
                                                                var existingBaseUrl2 = (existingReq2 == null) ? "" : $"{existingReq2.Scheme}://{existingReq2.Host}";
                                                                var fullExistingUrl2 = $"{existingBaseUrl2}{existingUrl}";
                                                                
                                                                outputs.Add((toolCallId, QuoteGenerationToolOutput.SerializeExistingQuoteReady(fullExistingUrl2, existingBookingNo)));
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                                
                                                // ========== QUOTE GENERATION: DETAILED SESSION LOGGING ==========
                                                var contactName = session?.GetString("Draft:ContactName");
                                                var contactEmail = session?.GetString("Draft:ContactEmail");
                                                var contactPhone = session?.GetString("Draft:ContactPhone");
                                                var organisation = session?.GetString("Draft:Organisation");
                                                var eventDate = session?.GetString("Draft:EventDate");
                                                var startTime = session?.GetString("Draft:StartTime");
                                                var dateConfirmed = session?.GetString("Draft:DateConfirmed");
                                                // existingBookingNo already declared above in idempotency check
                                                
                                                _logger.LogInformation("[QUOTE GEN] Session state at quote generation start: " +
                                                    "ContactName={ContactName}, ContactEmail={ContactEmail}, ContactPhone={ContactPhone}, " +
                                                    "Organisation={Organisation}, EventDate={EventDate}, StartTime={StartTime}, " +
                                                    "DateConfirmed={DateConfirmed}, BookingNo={BookingNo}",
                                                    contactName ?? "(null)", contactEmail ?? "(null)", contactPhone ?? "(null)",
                                                    organisation ?? "(null)", eventDate ?? "(null)", startTime ?? "(null)",
                                                    dateConfirmed ?? "(null)", existingBookingNo ?? "(null)");
                                                
                                                // VALIDATION: Check required fields before generating quote
                                                var missingFields = new List<string>();
                                                
                                                if (string.IsNullOrWhiteSpace(contactName))
                                                    missingFields.Add("contact name");
                                                if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
                                                    missingFields.Add("contact email or phone number");
                                                if (string.IsNullOrWhiteSpace(organisation))
                                                    missingFields.Add("organisation name");
                                                if (string.IsNullOrWhiteSpace(eventDate))
                                                    missingFields.Add("event date");
                                                if (string.IsNullOrWhiteSpace(startTime))
                                                    missingFields.Add("schedule times (please use the time picker)");
                                                
                                                if (missingFields.Count > 0)
                                                {
                                                    _logger.LogWarning("[QUOTE GEN] Quote generation blocked - missing fields: {Fields}. Full session state logged above.", 
                                                        string.Join(", ", missingFields));
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "Cannot generate quote yet - missing required information",
                                                        missingFields = missingFields,
                                                        instruction = $"I need to collect the following information before proceeding: {string.Join(", ", missingFields)}. Please ask the user for this information politely."
                                                    })));
                                                    break;
                                                }
                                                
                                                _logger.LogInformation("[QUOTE GEN] All required fields present, proceeding with quote generation");
                                                
                                                var bookingNo = session?.GetString("Draft:BookingNo");
                                                
                                                // If no booking exists, create one on-the-fly using session data
                                                if (string.IsNullOrWhiteSpace(bookingNo))
                                                {
                                                    _logger.LogInformation("No booking found, creating one on-the-fly for quote generation");
                                                    try
                                                    {
                                                        var (_, fallbackMsgs) = GetTranscript(threadId);
                                                        bookingNo = await _bookingPersistence.CreateBookingOnTheFlyAsync(session, fallbackMsgs, ct);
                                                        if (!string.IsNullOrWhiteSpace(bookingNo))
                                                        {
                                                            session?.SetString("Draft:BookingNo", bookingNo);
                                                            _logger.LogInformation("Created booking {BookingNo} on-the-fly", bookingNo);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        _logger.LogError(ex, "Failed to create booking on-the-fly");
                                                    }
                                                }
                                                
                                                if (string.IsNullOrWhiteSpace(bookingNo))
                                                {
                                                    // Still no booking - check what's actually missing
                                                    _logger.LogError("[QUOTE GEN] Unable to create booking - no booking number generated. Session data: EventDate={EventDate}, StartTime={StartTime}, ContactName={ContactName}",
                                                        eventDate, startTime, contactName);
                                                    
                                                    // Only ask for schedule if it's actually missing
                                                    var errorInstruction = string.IsNullOrWhiteSpace(startTime)
                                                        ? "I need your schedule times to create the booking. Please use the time picker to select your event times."
                                                        : "Our team will follow up with you shortly to help complete your booking.";
                                                    
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "Unable to create booking automatically.",
                                                        instruction = errorInstruction
                                                    })));
                                                    break;
                                                }

                                                // Keep quote output aligned with latest chat/session venue + room.
                                                try
                                                {
                                                    await _bookingPersistence.SyncVenueAndRoomForBookingAsync(bookingNo, session, ct);
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.LogWarning(ex, "[QUOTE GEN] Failed to sync session venue/room to booking {BookingNo}; continuing", bookingNo);
                                                }

                                                try
                                                {
                                                    await _bookingPersistence.SyncContactAndOrganisationForBookingAsync(bookingNo, session, ct);
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.LogWarning(ex, "[QUOTE GEN] Failed to sync session contact/organisation to booking {BookingNo}; continuing", bookingNo);
                                                }

                                                // ========== SYNC SESSION EQUIPMENT TO BOOKING BEFORE QUOTE ==========
                                                var equipmentJson = session?.GetString("Draft:SelectedEquipment");
                                                if (!string.IsNullOrWhiteSpace(equipmentJson))
                                                {
                                                    try
                                                    {
                                                        await _itemPersistence.UpsertSelectedEquipmentAsync(bookingNo, equipmentJson, ct);
                                                        _logger.LogInformation("[QUOTE GEN] Synced session equipment to booking {BookingNo} before quote generation", bookingNo);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        _logger.LogWarning(ex, "[QUOTE GEN] Failed to sync session equipment to booking {BookingNo}; continuing with existing booking items", bookingNo);
                                                    }
                                                }

                                                try
                                                {
                                                    await _bookingPersistence.SyncLaborFromSessionForBookingAsync(bookingNo, session, ct);
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.LogWarning(ex, "[QUOTE GEN] Failed to sync session labor to booking {BookingNo}; continuing", bookingNo);
                                                }

                                                // Use HtmlQuoteGenerationService to generate HTML from booking data
                                                var toolTrace = _http.HttpContext?.TraceIdentifier;
                                                var (success, htmlUrl, error) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(bookingNo, ct, session, toolTrace);
                                                
                                                if (!success || string.IsNullOrEmpty(htmlUrl))
                                                {
                                                    _logger.LogError("[QUOTE GEN] HTML quote generation failed for booking {BookingNo}: {Error}", bookingNo, error);

                                                    // Before surfacing an error, double-check whether a quote already exists
                                                    var existingErrorQuoteUrl = session?.GetString("Draft:QuoteUrl");
                                                    var existingErrorQuoteComplete = session?.GetString("Draft:QuoteComplete") == "1";

                                                    if (existingErrorQuoteComplete && !string.IsNullOrWhiteSpace(existingErrorQuoteUrl))
                                                    {
                                                        _logger.LogWarning("[QUOTE GEN] Failure reported for booking {BookingNo}, but quote state indicates success. Returning existing quote instead of error.", bookingNo);

                                                        var reqExisting = _http.HttpContext?.Request;
                                                        var baseExisting = (reqExisting == null) ? "" : $"{reqExisting.Scheme}://{reqExisting.Host}";
                                                        var fullExistingQuoteUrl = existingErrorQuoteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                                            ? existingErrorQuoteUrl
                                                            : $"{baseExisting}{existingErrorQuoteUrl}";

                                                        outputs.Add((toolCallId, QuoteGenerationToolOutput.SerializeQuoteRecoveredAfterGenerationError(fullExistingQuoteUrl, bookingNo)));
                                                        break;
                                                    }
                                                    
                                                    // Keep user experience clean: avoid exposing backend failures directly.
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        success = true,
                                                        pending = true,
                                                        bookingNo,
                                                        message = $"Your quote for booking {bookingNo} is being finalized now. Please wait a moment and refresh, and I will share the live quote link as soon as it is ready.",
                                                        instruction = "OUTPUT ONLY the message field. Do not mention errors or internal issues."
                                                    })));
                                                    break;
                                                }

                                                // Build full URL
                                                var req = _http.HttpContext?.Request;
                                                var baseUrl = (req == null) ? "" : $"{req.Scheme}://{req.Host}";
                                                var fullQuoteUrl = $"{baseUrl}{htmlUrl}";

                                                // ========== SYNC QUOTE STATE ==========
                                                session?.SetString("Draft:QuoteUrl", htmlUrl);
                                                session?.SetString("Draft:QuoteComplete", "1");
                                                session?.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
                                                
                                                _logger.LogInformation("HTML quote generated successfully for booking {BookingNo}: {Url}. State synchronized.", bookingNo, fullQuoteUrl);

                                                try
                                                {
                                                    var (_, transcriptMsgs) = GetTranscript(threadId);
                                                    await _bookingPersistence.SaveFullTranscriptToBooknoteAsync(bookingNo, transcriptMsgs, ct);
                                                }
                                                catch (Exception txEx)
                                                {
                                                    _logger.LogWarning(txEx, "[QUOTE GEN] Failed to save transcript for booking {BookingNo} after quote generation", bookingNo);
                                                }

                                                outputs.Add((toolCallId, QuoteGenerationToolOutput.SerializeNewQuoteReady(fullQuoteUrl, bookingNo)));
                                                break;
                                            }

                                        case "regenerate_quote":
                                            {
                                                var regenSession = _http.HttpContext?.Session;
                                                var regenBookingNo = regenSession?.GetString("Draft:BookingNo");
                                                var regenEquipmentJson = regenSession?.GetString("Draft:SelectedEquipment");
                                                if (string.IsNullOrWhiteSpace(regenBookingNo))
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "No booking found to update.",
                                                        instruction = "A quote must exist first. Ask the user to create a quote, then they can request changes and you can call regenerate_quote."
                                                    })));
                                                    break;
                                                }
                                                if (string.IsNullOrWhiteSpace(regenEquipmentJson))
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "No equipment list to apply.",
                                                        instruction = "Ask the user what equipment they want on the quote, or call update_equipment first with their requested changes, then call regenerate_quote."
                                                    })));
                                                    break;
                                                }
                                                try
                                                {
                                                    await _itemPersistence.UpsertSelectedEquipmentAsync(regenBookingNo, regenEquipmentJson, ct);
                                                    var regenTrace = _http.HttpContext?.TraceIdentifier;
                                                    var (regenSuccess, regenUrl, regenError) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(regenBookingNo, ct, regenSession, regenTrace);
                                                    if (!regenSuccess || string.IsNullOrEmpty(regenUrl))
                                                    {
                                                        outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                        {
                                                            error = regenError ?? "Failed to regenerate quote.",
                                                            instruction = "Tell the user the quote could not be updated and suggest they try again or contact the team."
                                                        })));
                                                        break;
                                                    }
                                                    var regenReq = _http.HttpContext?.Request;
                                                    var regenBaseUrl = (regenReq == null) ? "" : $"{regenReq.Scheme}://{regenReq.Host}";
                                                    var fullRegenUrl = regenUrl.StartsWith("http") ? regenUrl : $"{regenBaseUrl}{regenUrl}";
                                                    regenSession?.SetString("Draft:QuoteUrl", regenUrl);
                                                    regenSession?.SetString("Draft:QuoteComplete", "1");
                                                    regenSession?.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
                                                    _logger.LogInformation("[REGENERATE_QUOTE] Regenerated quote for booking {BookingNo}: {Url}", regenBookingNo, fullRegenUrl);
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        success = true,
                                                        ui = new { quoteUrl = fullRegenUrl, bookingNo = regenBookingNo, isHtml = true },
                                                        message = $"I've updated your quote for booking {regenBookingNo}. <a href=\"{fullRegenUrl}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"isla-quote-open\" data-quote-open=\"1\">View Quote</a>\n\nWould you like to proceed and accept this quote?",
                                                        instruction = "OUTPUT the message with the View Quote link and the confirmation prompt. Do not add other text."
                                                    })));
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.LogError(ex, "[REGENERATE_QUOTE] Failed for booking {BookingNo}", regenBookingNo);
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "Failed to regenerate quote.",
                                                        instruction = "Tell the user the quote could not be updated and suggest they try again."
                                                    })));
                                                }
                                                break;
                                            }

                                        default:
                                            {
                                                outputs.Add((toolCallId, JsonSerializer.Serialize(new { error = $"Unknown tool '{name}'" })));
                                                break;
                                            }
                                    }
                                }
                                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                                {
                                    _logger.LogWarning(
                                        "Tool {ToolName} timed out after {TimeoutSeconds}s for run {RunId}",
                                        name,
                                        perToolTimeout.TotalSeconds,
                                        run.Id);
                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                    {
                                        error = $"Tool '{name}' timed out after {(int)perToolTimeout.TotalSeconds} seconds.",
                                        instruction = "Tell the user there was a temporary processing delay and ask them to retry."
                                    })));
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Tool {ToolName} failed", name);
                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new { error = ex.Message })));
                                }
                            }

                            var payloadObj = new
                            {
                                tool_outputs = outputs.Select(o => new { tool_call_id = o.Id, output = o.Output })
                            };

                            var json = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = null });
                            var content = RequestContent.Create(BinaryData.FromString(json));

                            dynamic runsDyn = AgentsClient.Runs;

                            // Submit tool outputs with retry (handles both method shapes)
                            await With429RetryAsync(() =>
                            {
                                try
                                {
                                    var resp = runsDyn.SubmitToolOutputsToRun(threadId, run.Id, content);
                                    try { run = resp.Value; } catch { run = AgentsClient.Runs.GetRun(threadId, run.Id).Value; }
                                }
                                catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                                {
                                    var resp = runsDyn.SubmitToolOutputs(threadId, run.Id, content);
                                    try { run = resp.Value; } catch { run = AgentsClient.Runs.GetRun(threadId, run.Id).Value; }
                                }
                                return Task.FromResult(run);
                            }, "SubmitToolOutputs", ct);

                            continue;
                        }
                    }

                    if (DateTime.UtcNow - startUtc > timeout)
                    {
                        _logger.LogWarning("Run {RunId} timed out in status {Status}. Cancelling run to prevent stale in-progress state.", run.Id, run.Status);
                        try
                        {
                            AgentsClient.Runs.CancelRun(threadId, run.Id);
                            run = AgentsClient.Runs.GetRun(threadId, run.Id).Value;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to cancel timed out run {RunId}", run.Id);
                        }
                        return run;
                    }

                    await Task.Delay(delayMs, ct);
                    if (delayMs < maxDelayMs) delayMs = Math.Min(delayMs + 400, maxDelayMs);
                }
            }
            finally
            {
                _http.HttpContext?.Session.Remove(SessionKeyActiveRunId);
            }
        }


        private static object BuildNowAestResponse()
        {
            DateTimeOffset nowLocal;
            try
            {
                var tzWin = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
                nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzWin);
            }
            catch
            {
                var tzIana = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
                nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzIana);
            }

            return new
            {
                iso = nowLocal.ToString("yyyy-MM-dd'T'HH:mm:ssK"),
                date = nowLocal.ToString("yyyy-MM-dd"),
                time24 = nowLocal.ToString("HH:mm"),
                weekday = nowLocal.ToString("dddd"),
                tz = "AEST"
            };
        }

        private string ToAbsoluteUrl(string pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return pathOrUrl;
            if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return pathOrUrl;

            var req = _http.HttpContext?.Request;
            if (req == null) return pathOrUrl;

            return $"{req.Scheme}://{req.Host}{pathOrUrl}";
        }

        #region Gallery Auto-Injection
        
        private const string SessionKeyPendingGalleries = "PendingGalleryContent";
        private static readonly Regex _galleryTagPattern = new Regex(
            @"\[\[ISLA_GALLERY\]\](.*?)\[\[\/ISLA_GALLERY\]\]",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        
        /// <summary>
        /// Stores gallery content from tool outputs for potential injection if AI doesn't include it
        /// </summary>
        private void TrackPendingGalleries(string toolOutput)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(toolOutput) || !toolOutput.Contains("[[ISLA_GALLERY]]", StringComparison.OrdinalIgnoreCase))
                    return;
                
                var session = _http.HttpContext?.Session;
                if (session == null) return;
                
                // Parse the tool output to find outputToUser containing galleries
                string? galleryContent = null;
                try
                {
                    using var doc = JsonDocument.Parse(toolOutput);
                    if (doc.RootElement.TryGetProperty("outputToUser", out var outputProp))
                    {
                        galleryContent = outputProp.GetString();
                    }
                }
                catch { /* Not JSON or doesn't have outputToUser */ }
                
                // If not in JSON, check if the raw output has galleries
                if (string.IsNullOrWhiteSpace(galleryContent) && toolOutput.Contains("[[ISLA_GALLERY]]"))
                {
                    galleryContent = toolOutput;
                }
                
                if (string.IsNullOrWhiteSpace(galleryContent)) return;
                
                // Extract just the gallery blocks
                var matches = _galleryTagPattern.Matches(galleryContent);
                if (matches.Count == 0) return;
                
                var galleries = new List<string>();
                foreach (Match m in matches)
                {
                    galleries.Add(m.Value); // The full [[ISLA_GALLERY]]...[[/ISLA_GALLERY]] block
                }
                
                // Store in session
                var existingJson = session.GetString(SessionKeyPendingGalleries);
                var existing = string.IsNullOrWhiteSpace(existingJson) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>(existingJson) ?? new List<string>();
                
                existing.AddRange(galleries);
                session.SetString(SessionKeyPendingGalleries, JsonSerializer.Serialize(existing));
                
                _logger.LogInformation("Tracked {Count} pending galleries from tool output", galleries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track pending galleries");
            }
        }
        
        /// <summary>
        /// Injects missing gallery content if the AI didn't include it in its response
        /// </summary>
        private Task InjectMissingGalleriesAsync(string threadId, IEnumerable<DisplayMessage> messages, CancellationToken ct)
        {
            try
            {
                var session = _http.HttpContext?.Session;
                if (session == null) return Task.CompletedTask;
                
                var pendingJson = session.GetString(SessionKeyPendingGalleries);
                if (string.IsNullOrWhiteSpace(pendingJson)) return Task.CompletedTask;
                
                var pendingGalleries = JsonSerializer.Deserialize<List<string>>(pendingJson);
                if (pendingGalleries == null || pendingGalleries.Count == 0) return Task.CompletedTask;
                
                // Clear pending galleries to prevent re-injection
                session.Remove(SessionKeyPendingGalleries);
                
                // Get the last assistant message
                var lastAssistantMessage = messages
                    .Where(m => m.Role?.Equals("Assistant", StringComparison.OrdinalIgnoreCase) == true || 
                               m.Role?.Equals("agent", StringComparison.OrdinalIgnoreCase) == true)
                    .LastOrDefault();
                
                if (lastAssistantMessage == null)
                {
                    _logger.LogInformation("No assistant message found, skipping gallery injection");
                    return Task.CompletedTask;
                }
                
                var responseText = lastAssistantMessage.FullText ?? string.Join("\n", lastAssistantMessage.Parts ?? new List<string>());
                
                // Check which galleries are missing from the response
                var missingGalleries = new List<string>();
                foreach (var gallery in pendingGalleries)
                {
                    // Check if this gallery (or similar) is already in the response
                    if (!responseText.Contains("[[ISLA_GALLERY]]", StringComparison.OrdinalIgnoreCase))
                    {
                        missingGalleries.Add(gallery);
                    }
                    else
                    {
                        // Gallery tags exist, but check if the specific content is there
                        // We'll be lenient here - if any gallery exists, assume AI handled it
                        _logger.LogInformation("Response already contains gallery content, skipping injection");
                        return Task.CompletedTask;
                    }
                }
                
                if (missingGalleries.Count == 0) return Task.CompletedTask;
                
                // Inject missing galleries by appending a new agent message
                var galleryMessage = "\n\n---\n**Here are your options:**\n\n" + string.Join("\n\n", missingGalleries);
                
                _logger.LogInformation("Injecting {Count} missing galleries into thread {ThreadId}", missingGalleries.Count, threadId);
                
                try
                {
                    AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, galleryMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to inject gallery message into thread");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gallery injection check failed");
            }
            
            return Task.CompletedTask;
        }
        
        #endregion

        private async Task WaitForRunToCompleteAsync(string threadId, string? runId, CancellationToken ct, TimeSpan? maxWait = null)
        {
            if (string.IsNullOrWhiteSpace(runId)) return;
            var deadline = maxWait.HasValue ? DateTime.UtcNow + maxWait.Value : DateTime.MaxValue;

            while (true)
            {
                var r = AgentsClient.Runs.GetRun(threadId, runId).Value;
                if (IsTerminal(r.Status)) return;
                if (DateTime.UtcNow >= deadline) 
                {
                    _logger.LogWarning("Timeout waiting for run {RunId} to complete on thread {ThreadId}. Status: {Status}", runId, threadId, r.Status);
                    return;
                }

                await Task.Delay(350, ct);
            }
        }

        private static void EmitToolLoopDebugLog(
            string runId,
            string hypothesisId,
            string location,
            string message,
            object data)
        {
            try
            {
                var payload = new
                {
                    sessionId = "7953da",
                    runId,
                    hypothesisId,
                    location,
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                var line = JsonSerializer.Serialize(payload);
                DevelopmentDebugLog.TryAppendLine("debug-7953da.log", line);
            }
            catch
            {
                /* never throw from debug instrumentation */
            }
        }

    }
}
