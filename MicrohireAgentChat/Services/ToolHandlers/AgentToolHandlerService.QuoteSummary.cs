using Azure.AI.Agents.Persistent;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
    #region Quote Summary Tool Handlers

    private async Task<string> HandleSmartEquipmentRecommendationAsync(string argsJson, string threadId, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        // VALIDATION: Check required contact fields before showing quote summary
        // This prevents the AI from offering to generate a quote before collecting customer info
        var session = _http.HttpContext?.Session;
        var contactName = session?.GetString("Draft:ContactName");
        var contactEmail = session?.GetString("Draft:ContactEmail");
        var contactPhone = session?.GetString("Draft:ContactPhone");
        var organisation = session?.GetString("Draft:Organisation");

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(contactName))
            missingFields.Add("customer name");
        if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
            missingFields.Add("contact email or phone number");
        if (string.IsNullOrWhiteSpace(organisation))
            missingFields.Add("organisation name");

        if (missingFields.Count > 0)
        {
            _logger.LogWarning("Equipment recommendation blocked - missing contact fields: {Fields}", string.Join(", ", missingFields));
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required customer information",
                missingFields = missingFields,
                instruction = $"Do NOT call recommend_equipment_for_event again in this response. Before recommending equipment, you MUST collect: {string.Join(", ", missingFields)}. Politely ask the user for this information (e.g. 'Before I can provide equipment recommendations and a quote, I just need a few quick details about you first.'). Collect the missing information first; then on the next user message you may call the tool."
            });
        }

        // VALIDATION: Schedule must be submitted via time picker before showing quote summary
        var chatService = _http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService;
        var conversationMessages = new List<DisplayMessage>();
        if (chatService != null && !string.IsNullOrEmpty(threadId))
        {
            var (_, messages) = chatService.GetTranscript(threadId);
            conversationMessages = messages.ToList();
            var scheduleTimes = _extraction.ExtractScheduleTimes(messages);
            bool hasSchedule = scheduleTimes.Count > 0 && (scheduleTimes.ContainsKey("show_start_time") || scheduleTimes.ContainsKey("show_end_time"));
            if (!hasSchedule)
            {
                var hasChooseSchedule = messages.Any(m => (m.Parts ?? Enumerable.Empty<string>()).Any(p => p?.StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase) == true));
                if (!hasChooseSchedule)
                {
                    // Session fallback: time picker saves to session even when transcript has reformatted message
                    var sessionStart = session?.GetString("Draft:StartTime");
                    var sessionEnd = session?.GetString("Draft:EndTime");
                    var sessionEventDate = session?.GetString("Draft:EventDate");
                    var sessionDateConfirmed = session?.GetString("Draft:DateConfirmed");
                    if (!string.IsNullOrWhiteSpace(sessionStart) && (!string.IsNullOrWhiteSpace(sessionEventDate) || sessionDateConfirmed == "1"))
                        hasSchedule = true;
                    if (!hasSchedule && !string.IsNullOrWhiteSpace(sessionEnd) && (!string.IsNullOrWhiteSpace(sessionEventDate) || sessionDateConfirmed == "1"))
                        hasSchedule = true;
                }
                if (!hasSchedule)
                {
                    _logger.LogWarning("Equipment recommendation blocked - schedule not yet submitted for thread {ThreadId}", threadId);
                    return JsonSerializer.Serialize(new
                    {
                        error = "Cannot show quote summary - event schedule not yet submitted",
                        instruction = "Do NOT call recommend_equipment_for_event again in this response. You MUST collect the event schedule before recommending equipment. Call build_time_picker with the confirmed event date, output the returned content exactly so the time picker appears, and wait for the user to submit their schedule (setup, start, end, pack up times). Collect the schedule first; then on the next user message you may call recommend_equipment_for_event."
                    });
                }
            }
        }

        // ========== CONTEXT VALIDATION - Scan conversation for mentioned equipment ==========
        var conversationContextWarnings = ValidateEquipmentContextFromConversation(argsJson);
        
        // Parse event context
        var setupStyleFromArgs = doc.RootElement.TryGetProperty("setup_style", out var ss) ? ss.GetString() : null;
        var setupStyleFromSession = session?.GetString("Ack:SetupStyle") ?? session?.GetString("Draft:SetupStyle");
        var setupStyle = !string.IsNullOrWhiteSpace(setupStyleFromArgs) ? setupStyleFromArgs : setupStyleFromSession;

        var eventType = doc.RootElement.TryGetProperty("event_type", out var et) ? et.GetString() : null;
        if (string.IsNullOrWhiteSpace(eventType))
            eventType = session?.GetString("Draft:EventType");
        if (string.IsNullOrWhiteSpace(eventType) && chatService != null && !string.IsNullOrWhiteSpace(threadId))
        {
            var (_, transcriptMessages) = chatService.GetTranscript(threadId);
            var (extractedEventType, _) = _extraction.ExtractEventType(transcriptMessages);
            if (!string.IsNullOrWhiteSpace(extractedEventType))
            {
                eventType = extractedEventType;
                if (session != null && string.IsNullOrWhiteSpace(session.GetString("Draft:EventType")))
                    session.SetString("Draft:EventType", extractedEventType);
            }
        }

        var attendeesFromArgs = doc.RootElement.TryGetProperty("expected_attendees", out var ea) && ea.ValueKind == JsonValueKind.Number ? ea.GetInt32() : 0;
        var attendeesFromSession = session?.GetString("Draft:ExpectedAttendees") ?? session?.GetString("Ack:Attendees");
        int attendeesFromSessionParsed = 0;
        if (!string.IsNullOrWhiteSpace(attendeesFromSession))
            int.TryParse(attendeesFromSession, out attendeesFromSessionParsed);

        var expectedAttendees = attendeesFromArgs > 0 ? attendeesFromArgs : (attendeesFromSessionParsed > 0 ? attendeesFromSessionParsed : 0);

        // ========== TRANSCRIPT VALIDATION ==========
        // Guard against AI-hallucinated values: if a value came from tool args (not session),
        // verify the user actually stated it in the conversation.
        if (conversationMessages.Count > 0)
        {
            // Validate attendees: if from tool args and not backed by session, verify user said it
            if (attendeesFromArgs > 0 && attendeesFromSessionParsed <= 0)
            {
                var userStatedAttendees = ExtractAttendeesFromUserMessages(conversationMessages);
                if (userStatedAttendees <= 0)
                {
                    _logger.LogWarning("Attendees={Attendees} from tool args not found in user messages - rejecting fabricated value for thread {ThreadId}", attendeesFromArgs, threadId);
                    expectedAttendees = 0;
                }
            }

            // Validate setup style: if from tool args and not backed by session, verify user said it
            if (!string.IsNullOrWhiteSpace(setupStyleFromArgs) && string.IsNullOrWhiteSpace(setupStyleFromSession))
            {
                var userStatedStyle = ExtractSetupStyleFromUserMessages(conversationMessages);
                if (userStatedStyle == null)
                {
                    _logger.LogWarning("SetupStyle={Style} from tool args not found in user messages - rejecting fabricated value for thread {ThreadId}", setupStyleFromArgs, threadId);
                    setupStyle = null;
                }
            }
        }

        var eventContext = new EventContext
        {
            EventType = eventType ?? "",
            ExpectedAttendees = expectedAttendees,
            VenueName = doc.RootElement.TryGetProperty("venue_name", out var vn) ? vn.GetString() : null,
            RoomName = doc.RootElement.TryGetProperty("room_name", out var rn) ? rn.GetString() : null,
            DurationDays = doc.RootElement.TryGetProperty("duration_days", out var dd) && dd.ValueKind == JsonValueKind.Number ? dd.GetInt32() : 1,
            NumberOfPresentations = doc.RootElement.TryGetProperty("presenter_count", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt32() : 0,
            NumberOfSpeakers = doc.RootElement.TryGetProperty("speaker_count", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetInt32() : 0,
            IsContentHeavy = doc.RootElement.TryGetProperty("is_content_heavy", out var ch) && ch.ValueKind == JsonValueKind.True,
            IsContentLight = doc.RootElement.TryGetProperty("is_content_light", out var cl) && cl.ValueKind == JsonValueKind.True,
            NeedsRecording = doc.RootElement.TryGetProperty("needs_recording", out var nr) && nr.ValueKind == JsonValueKind.True,
            NeedsStreaming = doc.RootElement.TryGetProperty("needs_streaming", out var nst) && nst.ValueKind == JsonValueKind.True,
            NeedsHeavyStreaming = doc.RootElement.TryGetProperty("needs_heavy_streaming", out var nhs) && nhs.ValueKind == JsonValueKind.True,
            NeedsLighting = doc.RootElement.TryGetProperty("needs_lighting", out var nl) && nl.ValueKind == JsonValueKind.True,
            NeedsAdvancedLighting = doc.RootElement.TryGetProperty("needs_advanced_lighting", out var nal) && nal.ValueKind == JsonValueKind.True
        };
        if (string.IsNullOrWhiteSpace(eventContext.VenueName))
            eventContext.VenueName = session?.GetString("Draft:VenueName");
        if (string.IsNullOrWhiteSpace(eventContext.RoomName))
            eventContext.RoomName = session?.GetString("Draft:RoomName");

        if (string.IsNullOrWhiteSpace(setupStyle)
            && !string.IsNullOrWhiteSpace(eventContext.RoomName)
            && eventContext.RoomName.Contains("Thrive", StringComparison.OrdinalIgnoreCase))
        {
            setupStyle = "boardroom";
        }

        // Parse equipment requests
        if (doc.RootElement.TryGetProperty("equipment_requests", out var eqArray) && eqArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in eqArray.EnumerateArray())
            {
                var request = new EquipmentRequest
                {
                    EquipmentType = item.TryGetProperty("equipment_type", out var eqt) ? eqt.GetString() ?? "" : "",
                    Quantity = item.TryGetProperty("quantity", out var qty) && qty.ValueKind == JsonValueKind.Number ? qty.GetInt32() : 1,
                    Preference = item.TryGetProperty("preference", out var pref) ? pref.GetString() : null,
                    MicrophoneType = item.TryGetProperty("microphone_type", out var mt) ? mt.GetString() : null,
                    SpeakerStyle = item.TryGetProperty("speaker_style", out var speakerStyleProp) ? speakerStyleProp.GetString() : null
                };
                
                if (!string.IsNullOrWhiteSpace(request.EquipmentType))
                {
                    eventContext.EquipmentRequests.Add(request);
                }
            }
        }

        eventContext.SpeakerStylePreference = eventContext.EquipmentRequests
            .Where(r => (r.EquipmentType ?? string.Empty).Contains("speaker", StringComparison.OrdinalIgnoreCase) ||
                        (r.EquipmentType ?? string.Empty).Contains("audio", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.SpeakerStyle)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (eventContext.EquipmentRequests.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                error = "No equipment requests provided",
                message = "Please specify what equipment is needed (e.g., laptops, projectors, screens, microphones)",
                instruction = "Build the full equipment_requests array from the ENTIRE conversation (all AV items discussed: e.g. speakers, projector, screen, clicker, 2x laptop with preference mac). Do NOT call recommend_equipment_for_event again in this response with an empty or partial list. Reply to the user once asking them to confirm what equipment they need, or list what you have noted and ask if anything is missing."
            });
        }

        // Laptop preference must be captured before showing quote summary when a provided laptop is requested.
        var laptopRequests = eventContext.EquipmentRequests
            .Where(r => IsLaptopEquipmentType(r.EquipmentType))
            .ToList();
        if (laptopRequests.Count > 0)
        {
            var hasMissingLaptopPreference = laptopRequests.Any(r => string.IsNullOrWhiteSpace(r.Preference));
            if (hasMissingLaptopPreference)
            {
                var laptopState = conversationMessages.Count > 0
                    ? _extraction.ExtractLaptopAnswerState(conversationMessages)
                    : new LaptopAnswerState();

                if (laptopState.PreferenceAnswered && !string.IsNullOrWhiteSpace(laptopState.Preference))
                {
                    foreach (var req in laptopRequests.Where(r => string.IsNullOrWhiteSpace(r.Preference)))
                        req.Preference = laptopState.Preference;
                    _logger.LogInformation("Hydrated laptop preference from transcript state: {Preference}", laptopState.Preference);
                }
                else if (!laptopState.OwnershipAnswered || laptopState.NeedsProvidedLaptop)
                {
                    _logger.LogWarning("Equipment recommendation blocked - laptop preference missing for thread {ThreadId}", threadId);
                    return JsonSerializer.Serialize(new
                    {
                        error = "Cannot show quote summary - missing laptop preference",
                        missingFields = new[] { "laptop preference (Windows or Mac)" },
                        instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly one question: 'Would you prefer a Windows or Mac laptop?' Wait for the user's answer, then call recommend_equipment_for_event again with equipment_type='laptop' and preference='windows' or preference='mac'."
                    });
                }
            }
        }

        // Westin Ballroom parent room is ambiguous until user confirms full vs split.
        var isWestinBrisbaneVenue = (eventContext.VenueName ?? "").Trim().Contains("westin", StringComparison.OrdinalIgnoreCase) &&
                                    (eventContext.VenueName ?? "").Trim().Contains("brisbane", StringComparison.OrdinalIgnoreCase);
        if (isWestinBrisbaneVenue &&
            IsAmbiguousWestinBallroomParentRoom(eventContext.RoomName) &&
            !UserExplicitlyConfirmedFullWestinBallroom(session, conversationMessages))
        {
            _logger.LogWarning("Equipment recommendation blocked - Westin Ballroom split not clarified for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - Westin Ballroom split not specified",
                missingFields = new[] { "Westin Ballroom selection (Full / Ballroom 1 / Ballroom 2)" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly: 'Is that the full Westin Ballroom, Westin Ballroom 1, or Westin Ballroom 2?' Wait for the user to confirm one option, then call recommend_equipment_for_event again."
            });
        }

        // Projector-area capture for Westin Ballroom family.
        var projectionNeeded = RequiresProjectorPlacementArea(eventContext.EquipmentRequests);
        var requestedProjectorCount = GetRequestedProjectorCount(eventContext.EquipmentRequests);
        var isWestinBallroomFamily = IsWestinBallroomFamilyRoom(eventContext.VenueName, eventContext.RoomName);
        // Only require as many areas as the selected projector quantity (default 1).
        // Dual-projector handling is specific to full Westin Ballroom and should not be forced
        // for Ballroom 1 or Ballroom 2 unless explicitly requested.
        var requiredAreaCount = projectionNeeded
            ? (requestedProjectorCount > 1 ? Math.Min(requestedProjectorCount, 3) : 1)
            : 0;

        List<string> projectorAreas;
        bool hasProjectorAreaPromptInConversation = conversationMessages.Any(m =>
            string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) &&
            (m.FullText?.Contains("/images/westin/westin-ballroom/floor-plan.png", StringComparison.OrdinalIgnoreCase) == true ||
             (m.Parts ?? Enumerable.Empty<string>()).Any(p =>
                 p.Contains("/images/westin/westin-ballroom/floor-plan.png", StringComparison.OrdinalIgnoreCase) ||
                 p.Contains("projector placement area", StringComparison.OrdinalIgnoreCase))));
        if (projectionNeeded && isWestinBallroomFamily)
        {
            // CRITICAL: Only trust session for Westin Ballroom projector areas.
            // Do NOT use tool args or extraction - the AI may pass "A" to bypass, or extraction may have false positives.
            // Session is only set when the user explicitly replied after seeing the floor plan (ChatController.TryCaptureProjectorAreaSelections).
            projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorAreas"));
            if (projectorAreas.Count == 0)
                projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorArea"));

            // Guard against stale session carry-over: if this thread has never shown the floor plan prompt,
            // force re-collection from the user instead of silently reusing old projector areas.
            if (!hasProjectorAreaPromptInConversation)
                projectorAreas.Clear();
        }
        else
        {
            projectorAreas = GetNormalizedProjectorAreasFromArgs(doc.RootElement);
            if (projectorAreas.Count == 0)
                projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorAreas"));
            if (projectorAreas.Count == 0)
                projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorArea"));
            if (projectorAreas.Count == 0)
                projectorAreas = _extraction.ExtractProjectorAreas(conversationMessages);
        }
        if (projectionNeeded && isWestinBallroomFamily)
        {
            var allowedAreas = GetAllowedProjectorAreas(eventContext.RoomName);
            var invalidAreas = projectorAreas.Where(a => !allowedAreas.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
            var validAreas = projectorAreas.Where(a => allowedAreas.Contains(a, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (invalidAreas.Count > 0 || validAreas.Count < requiredAreaCount)
            {
            var areaImageUrl = "/images/westin/westin-ballroom/floor-plan.png";
            var allowedText = string.Join(", ", allowedAreas);
            var countText = requiredAreaCount == 1
                ? "please choose the projector placement area"
                : $"please choose {requiredAreaCount} projector placement areas";
            var exampleAreas = allowedAreas.Count >= 2
                ? $"`{allowedAreas[0]} & {allowedAreas[1]}`"
                : "`A & B`";
            var outputToUser = string.Join("\n", new[]
            {
                $"Great — before I finalise the AV summary, {countText} for the Westin Ballroom layout.",
                "",
                $"For this room, valid areas are: **{allowedText}**.",
                requiredAreaCount == 1
                    ? "Please reply with one area."
                    : $"Please reply with any two areas (e.g. {exampleAreas}).",
                "",
                $"![Westin Ballroom projector placement areas]({areaImageUrl})"
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing projector placement area",
                missingFields = new[] { requiredAreaCount == 1 ? "projector placement area" : "projector placement areas" },
                outputToUser,
                instruction = "Do NOT call recommend_equipment_for_event again in this response. OUTPUT the 'outputToUser' value EXACTLY AS-IS so the customer can select projector placement area(s). After the customer replies, call recommend_equipment_for_event again."
            });
        }
            projectorAreas = validAreas.Take(requiredAreaCount).ToList();
        }
        var projectorArea = projectorAreas.FirstOrDefault();
        eventContext.ProjectorAreas = projectorAreas;
        if (session != null && projectorAreas.Count > 0)
        {
            session.SetString("Draft:ProjectorAreas", string.Join(",", projectorAreas));
            session.SetString("Draft:ProjectorArea", projectorAreas[0]);
        }
        if (string.IsNullOrWhiteSpace(eventContext.EventType))
        {
            _logger.LogWarning("Equipment recommendation blocked - event type missing for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required event type",
                missingFields = new[] { "event type" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Before recommending equipment, you MUST collect the event type (for example: conference, presentation, wedding, workshop). Ask one clear question to capture the event type first; then on the next user message you may call recommend_equipment_for_event."
            });
        }
        if (eventContext.ExpectedAttendees <= 0)
        {
            _logger.LogWarning("Equipment recommendation blocked - attendees missing for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required attendee count",
                missingFields = new[] { "number of attendees" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Before recommending equipment, you MUST collect the number of attendees. Ask one clear question to capture attendee count first; then on the next user message you may call recommend_equipment_for_event."
            });
        }
        if (string.IsNullOrWhiteSpace(setupStyle))
        {
            _logger.LogWarning("Equipment recommendation blocked - setup style missing for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required room setup style",
                missingFields = new[] { "room setup style" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Before showing the quote summary, you MUST collect the room setup style (for example: theatre, boardroom, classroom, banquet, u-shape). Ask one clear question to capture setup style first; then on the next user message you may call recommend_equipment_for_event."
            });
        }

        // Westin Brisbane and Four Points Brisbane require a specific room - do not infer from "board meeting" or "boardroom style"
        var venueNorm = (eventContext.VenueName ?? "").Trim().ToLowerInvariant();
        var isWestinOrFourPoints = (venueNorm.Contains("westin") && venueNorm.Contains("brisbane")) ||
            (venueNorm.Contains("four points") && venueNorm.Contains("brisbane"));
        if (isWestinOrFourPoints && string.IsNullOrWhiteSpace(eventContext.RoomName))
        {
            _logger.LogWarning("Equipment recommendation blocked - room not specified for Westin/Four Points Brisbane for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - room not specified for this venue",
                missingFields = new[] { "room name" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. The user has not specified which room at this venue. Ask which room they would like (e.g. Westin Ballroom, Westin Ballroom 1, Westin Ballroom 2, Elevate, Elevate 1, Elevate 2, Thrive Boardroom). Do NOT infer a room from 'board meeting' or 'boardroom style' - those describe setup, not the room. Wait for the user to specify the room, then call recommend_equipment_for_event."
            });
        }

        _logger.LogInformation("Smart equipment recommendation for {EventType} with {Attendees} attendees, {Count} equipment types: [{Types}]",
            eventContext.EventType, eventContext.ExpectedAttendees, eventContext.EquipmentRequests.Count,
            string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}")));

        // Capacity check: if Westin + room known, validate attendees vs room capacity for the setup
        string? capacityWarning = null;
        string? capacityOkLine = null;
        if (!string.IsNullOrWhiteSpace(eventContext.VenueName) && !string.IsNullOrWhiteSpace(eventContext.RoomName) &&
            eventContext.VenueName.Trim().Contains("Westin", StringComparison.OrdinalIgnoreCase) && eventContext.VenueName.Trim().Contains("Brisbane", StringComparison.OrdinalIgnoreCase))
        {
            var (warning, okLine) = await TryGetCapacityCheckAsync(
                eventContext.VenueName, eventContext.RoomName, eventContext.ExpectedAttendees,
                setupStyle, ct);
            capacityWarning = warning;
            capacityOkLine = okLine;
        }

        // Get smart recommendations
        var recommendations = await _smartEquipment.GetRecommendationsAsync(eventContext, ct);
        
        // IMPORTANT: If no recommendations returned but equipment was requested, log and handle gracefully
        if (recommendations.Items.Count == 0 && eventContext.EquipmentRequests.Count > 0)
        {
            _logger.LogWarning("No equipment recommendations returned despite {Count} requests: [{Types}]. This may indicate database/pricing issues.",
                eventContext.EquipmentRequests.Count,
                string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}")));
            
            // Return a message asking the AI to proceed with generate_quote, not retry recommend_equipment_for_event
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Equipment search returned no results",
                requested_equipment = eventContext.EquipmentRequests.Select(r => new { r.EquipmentType, r.Quantity }).ToList(),
                message = $"I couldn't find specific products for the requested equipment. Let me prepare a custom quote based on your requirements: {string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}"))}.",
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Tell the user that you will prepare a custom quote for their equipment needs and call generate_quote to create the booking with these requirements noted."
            });
        }

        // Log what we found
        _logger.LogInformation("Smart recommendations returned {Count} items:", recommendations.Items.Count);
        foreach (var item in recommendations.Items)
        {
            _logger.LogInformation("  - {Qty}x {Desc} (Code: {Code}) @ ${Price}/day - {Reason}", 
                item.Quantity, item.Description, item.ProductCode, item.UnitPrice, item.RecommendationReason);
        }

        // Build response with full component breakdown
        var recommendedItems = recommendations.Items.Select(item => new
        {
            product_code = item.ProductCode,
            description = item.Description,
            category = item.Category,
            quantity = item.Quantity,
            unit_price = item.UnitPrice,
            extra_day_rate = item.ExtraDayRate,
            weekly_rate = item.WeeklyRate,
            total_price = item.UnitPrice * item.Quantity,
            picture = item.PictureFileName,
            recommendation_reason = item.RecommendationReason,
            is_package = item.IsPackage,
            components = item.Components.Select(c => new
            {
                product_code = c.ProductCode,
                description = c.Description,
                component_type = c.ComponentType.ToString().ToLower(),
                quantity = c.Quantity,
                is_selectable = c.IsSelectable,
                individual_rate = c.IndividualRate
            }).ToList()
        }).ToList();

        // Build COMPLETE booking summary with event details AND equipment
        // This summary triggers the quote confirmation buttons in the UI
        var summaryLines = new List<string>();
        
        // Retrieve date/time from session for display
        var eventDateStr = session?.GetString("Draft:EventDate");
        var startTimeStr = session?.GetString("Draft:StartTime");
        var endTimeStr = session?.GetString("Draft:EndTime");
        var setupTimeStr = session?.GetString("Draft:SetupTime");
        
        // Fallback: extract from conversation transcript if not in session
        // Note: chatService is already declared earlier in the method for schedule validation
        if ((string.IsNullOrWhiteSpace(eventDateStr) || string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr)) && !string.IsNullOrEmpty(threadId))
        {
            try
            {
                // Reuse chatService from earlier in the method if available, otherwise get it
                var chatServiceForExtraction = chatService ?? (_http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService);
                if (chatServiceForExtraction != null)
                {
                    var (_, messages) = chatServiceForExtraction.GetTranscript(threadId);
                    
                    // Extract date if not in session
                    if (string.IsNullOrWhiteSpace(eventDateStr))
                    {
                        var (dateDto, _) = _extraction.ExtractEventDate(messages);
                        if (dateDto.HasValue)
                        {
                            eventDateStr = dateDto.Value.ToString("yyyy-MM-dd");
                            _logger.LogInformation("[QUOTE_SUMMARY] Extracted date from conversation: {Date}", eventDateStr);
                        }
                    }
                    
                    // Extract times if not in session
                    if (string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr))
                    {
                        var scheduleTimes = _extraction.ExtractScheduleTimes(messages);
                        
                        if (string.IsNullOrWhiteSpace(startTimeStr) && scheduleTimes.ContainsKey("show_start_time"))
                        {
                            var extractedStart = scheduleTimes["show_start_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedStart.Length == 4 && int.TryParse(extractedStart, out var startMinutes))
                            {
                                var hours = startMinutes / 100;
                                var minutes = startMinutes % 100;
                                startTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[QUOTE_SUMMARY] Extracted start time from conversation: {Time}", startTimeStr);
                            }
                            else
                            {
                                startTimeStr = extractedStart;
                            }
                        }
                        
                        if (string.IsNullOrWhiteSpace(endTimeStr) && scheduleTimes.ContainsKey("show_end_time"))
                        {
                            var extractedEnd = scheduleTimes["show_end_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedEnd.Length == 4 && int.TryParse(extractedEnd, out var endMinutes))
                            {
                                var hours = endMinutes / 100;
                                var minutes = endMinutes % 100;
                                endTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[QUOTE_SUMMARY] Extracted end time from conversation: {Time}", endTimeStr);
                            }
                            else
                            {
                                endTimeStr = extractedEnd;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - use session values only if extraction fails
                _logger.LogWarning(ex, "[QUOTE_SUMMARY] Failed to extract date/time from conversation, using session values only");
            }
        }
        
        // Log if date/time are still missing
        if (string.IsNullOrWhiteSpace(eventDateStr))
        {
            _logger.LogWarning("[QUOTE_SUMMARY] Event date is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(startTimeStr))
        {
            _logger.LogWarning("[QUOTE_SUMMARY] Start time is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(endTimeStr))
        {
            _logger.LogWarning("[QUOTE_SUMMARY] End time is missing from both session and conversation");
        }
        
        // Header - complete quote summary
        summaryLines.Add("## 📋 Quote Summary\n");
        
        // Event Details Section
        summaryLines.Add("### Event Details");
        if (!string.IsNullOrWhiteSpace(eventContext.VenueName))
            summaryLines.Add($"**Venue:** {eventContext.VenueName}");
        if (!string.IsNullOrWhiteSpace(eventContext.RoomName))
            summaryLines.Add($"**Room:** {eventContext.RoomName}");
        if (projectorAreas.Count == 1)
            summaryLines.Add($"**Projector Placement Area:** {projectorAreas[0]}");
        else if (projectorAreas.Count > 1)
            summaryLines.Add($"**Projector Placement Areas:** {string.Join(", ", projectorAreas)}");
        summaryLines.Add($"**Event Type:** {(string.IsNullOrWhiteSpace(eventContext.EventType) ? "TBD" : eventContext.EventType)}");
        summaryLines.Add($"**Attendees:** {(eventContext.ExpectedAttendees <= 0 ? "TBD" : eventContext.ExpectedAttendees.ToString())}");
        
        // Add event date if available
        if (!string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out var eventDate))
        {
            summaryLines.Add($"**Date:** {eventDate:dddd d MMMM yyyy}");
        }
        
        // Add start and end times if available
        if (!string.IsNullOrWhiteSpace(startTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(startTimeStr, out var startTime))
            {
                summaryLines.Add($"**Start Time:** {startTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**Start Time:** {startTimeStr}");
            }
        }
        if (!string.IsNullOrWhiteSpace(endTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(endTimeStr, out var endTime))
            {
                summaryLines.Add($"**End Time:** {endTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**End Time:** {endTimeStr}");
            }
        }
        
        if (!string.IsNullOrEmpty(capacityOkLine) && eventContext.ExpectedAttendees > 0)
            summaryLines.Add(capacityOkLine);
        if (eventContext.DurationDays > 1)
            summaryLines.Add($"**Duration:** {eventContext.DurationDays} days");
        summaryLines.Add("");
        
        // Equipment Section
        summaryLines.Add("### Requirement Summary\n");
        
        // Ensure equipment is displayed - add validation
        if (eventContext.EquipmentRequests.Count == 0)
        {
            summaryLines.Add("*No equipment requirements collected yet. Please confirm required equipment first.*");
            _logger.LogWarning("[QUOTE_SUMMARY] No equipment requests available for summary rendering");
        }
        else
        {
            _logger.LogInformation("[QUOTE_SUMMARY] Displaying {Count} requirement-level equipment lines", eventContext.EquipmentRequests.Count);
            foreach (var request in BuildRequirementSummaryLines(eventContext.EquipmentRequests))
            {
                summaryLines.Add($"- {request}");
            }
            summaryLines.Add("");
        }
        
        // Technician Section
        if (recommendations.LaborItems.Count > 0)
        {
            summaryLines.Add("### Technician Support\n");
            foreach (var labor in recommendations.LaborItems)
            {
                summaryLines.Add($"- **{FormatLaborSummaryLine(labor)}**");
            }
            summaryLines.Add("");
        }
        
        // Total - Removed price display as per user request
        summaryLines.Add("");
        
        // This phrase triggers the quote confirmation buttons (Yes/No)
        summaryLines.Add("Would you like me to create the quote now?");

        var summaryMessage = string.Join("\n", summaryLines);
        // Prepend capacity warning if present so user and AI always see it
        var fullOutputToUser = !string.IsNullOrEmpty(capacityWarning)
            ? $"**⚠️ Room capacity:** {capacityWarning}\n\n{summaryMessage}"
            : summaryMessage;
        
        _logger.LogInformation("[QUOTE_SUMMARY] outputToUser length: {Length} chars (no alternatives section)", fullOutputToUser.Length);

        // Build response - includes phrase to trigger quote confirmation buttons
        // Include any context validation warnings
        var contextWarnings = conversationContextWarnings.Count > 0 
            ? $"\n\n**Note:** {string.Join(". ", conversationContextWarnings)}"
            : "";
        
        var hasCapacityWarning = !string.IsNullOrEmpty(capacityWarning);
        var instruction = hasCapacityWarning
            ? "CRITICAL: The room capacity is exceeded. Tell the user the room cannot fit their attendee count, state the room's capacity and suggest larger rooms or reducing attendees. Do NOT create the quote until they adjust. Then OUTPUT the 'outputToUser' EXACTLY AS-IS."
            : (conversationContextWarnings.Count > 0
                ? $"BEFORE outputting the summary, address these potential missing items: {string.Join(", ", conversationContextWarnings)}. Ask the user if they need these items. Then OUTPUT the 'outputToUser' EXACTLY AS-IS."
                : "MANDATORY: OUTPUT the 'outputToUser' EXACTLY AS-IS. This is the quote summary only. Do not add alternative pickers here; show alternatives only when the user explicitly asks (e.g. 'show me other microphones') using show_equipment_alternatives.");

        var response = JsonSerializer.Serialize(new
        {
            success = true,
            event_context = new
            {
                event_type = eventContext.EventType,
                attendees = eventContext.ExpectedAttendees,
                venue = eventContext.VenueName,
                room = eventContext.RoomName,
                projector_area = projectorArea,
                projector_areas = projectorAreas,
                duration_days = eventContext.DurationDays
            },
            recommendations = recommendedItems,
            total_day_rate = recommendations.TotalDayRate,
            alternatives_available = false,
            outputToUser = fullOutputToUser,
            capacity_warning = hasCapacityWarning ? capacityWarning : null,
            context_warnings = conversationContextWarnings.Count > 0 ? conversationContextWarnings : null,
            instruction = instruction
        });
        
        _logger.LogInformation("Full tool response JSON:\n{Response}", response);
        
            // Store equipment in session for later use when creating booking/quote
            try
            {
                // Note: session variable already declared at the start of this method for validation
                if (session != null)
                {
                    // Store as selected_equipment in format matching SelectedEquipmentItem class
                    var selectedEquipment = recommendations.Items.Select(item => new
                    {
                        ProductCode = item.ProductCode,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        IsPackage = item.IsPackage,
                        ParentPackageCode = (string?)null,
                        Comment = item.Comment
                    }).ToList();
                    
                    session.SetString("Draft:SelectedEquipment", JsonSerializer.Serialize(selectedEquipment));
                    
                    // Store as selected_labor in format matching SelectedLaborItem class
                    var selectedLabor = recommendations.LaborItems.Select(item => new SelectedLaborItem
                    {
                        ProductCode = string.IsNullOrWhiteSpace(item.ProductCode) ? "AVTECH" : item.ProductCode,
                        Description = item.Description,
                        Task = item.Task,
                        Quantity = item.Quantity,
                        Hours = item.Hours,
                        Minutes = item.Minutes
                    }).ToList();
                    session.SetString("Draft:SelectedLabor", JsonSerializer.Serialize(selectedLabor));

                    session.SetString("Draft:TotalDayRate", recommendations.TotalDayRate.ToString("F2"));
                    session.SetString("Draft:IsContentHeavy", eventContext.IsContentHeavy ? "1" : "0");
                    session.SetString("Draft:IsContentLight", eventContext.IsContentLight ? "1" : "0");
                    if (!string.IsNullOrWhiteSpace(eventContext.VenueName)) session.SetString("Draft:VenueName", eventContext.VenueName);
                    if (!string.IsNullOrWhiteSpace(eventContext.RoomName)) session.SetString("Draft:RoomName", eventContext.RoomName);
                    if (!string.IsNullOrWhiteSpace(eventContext.VenueName) || !string.IsNullOrWhiteSpace(eventContext.RoomName))
                        _logger.LogInformation("Session venue/room persisted for quote: Venue={Venue}, Room={Room}", eventContext.VenueName ?? "(null)", eventContext.RoomName ?? "(null)");
                    if (!string.IsNullOrWhiteSpace(eventContext.EventType)) session.SetString("Draft:EventType", eventContext.EventType);
                    session.SetString("Draft:ExpectedAttendees", eventContext.ExpectedAttendees.ToString());
                    if (!string.IsNullOrWhiteSpace(setupStyle)) session.SetString("Draft:SetupStyle", setupStyle);
                    if (projectorAreas.Count > 0)
                    {
                        session.SetString("Draft:ProjectorAreas", string.Join(",", projectorAreas));
                        session.SetString("Draft:ProjectorArea", projectorAreas[0]);
                    }
                    session.SetString("Draft:SummaryEquipmentRequests", JsonSerializer.Serialize(eventContext.EquipmentRequests));
                    
                    _logger.LogInformation("Stored {Count} equipment items in session for booking (Venue={Venue}, Room={Room})",
                        recommendations.Items.Count, eventContext.VenueName ?? "(null)", eventContext.RoomName ?? "(null)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store equipment in session");
            }
            
            return response;
        }
        catch (Exception ex)
        {
            var hasEquipmentRequests = !string.IsNullOrWhiteSpace(argsJson) && argsJson.IndexOf("equipment_requests", StringComparison.OrdinalIgnoreCase) >= 0;
            _logger.LogError(ex, "[QUOTE_SUMMARY] Exception in HandleSmartEquipmentRecommendationAsync: {Error}. Args length={Len}, hasEquipmentRequests={HasEq}", ex.Message, argsJson?.Length ?? 0, hasEquipmentRequests);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Failed to generate equipment recommendations",
                message = "I wasn't able to load the equipment summary just now. Could you please try sending your message again?",
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Reply to the user once with this exact message: 'I wasn't able to load the equipment summary just now. Could you please try sending your message again?' Then stop. Do not mention technical issues or apologise further."
            });
        }
    }

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
                instruction = "Do NOT call update_equipment or recommend_equipment_for_event again in this response. Please show a quote summary first (e.g. by confirming equipment with recommend_equipment_for_event), then I can apply your changes. Ask the user to confirm their equipment needs so we can show a summary, then they can request edits."
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
        if (string.IsNullOrWhiteSpace(setupStyle)
            && !string.IsNullOrWhiteSpace(roomName)
            && roomName.Contains("Thrive", StringComparison.OrdinalIgnoreCase))
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

        // Retrieve date/time from session for display
        var eventDateStr = session.GetString("Draft:EventDate");
        var startTimeStr = session.GetString("Draft:StartTime");
        var endTimeStr = session.GetString("Draft:EndTime");
        var setupTimeStr = session.GetString("Draft:SetupTime");
        
        // Fallback: extract from conversation transcript if not in session
        var threadId = session.GetString("AgentThreadId");
        if ((string.IsNullOrWhiteSpace(eventDateStr) || string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr)) && !string.IsNullOrEmpty(threadId))
        {
            try
            {
                var chatService = _http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService;
                if (chatService != null)
                {
                    var (_, messages) = chatService.GetTranscript(threadId);
                    
                    // Extract date if not in session
                    if (string.IsNullOrWhiteSpace(eventDateStr))
                    {
                        var (dateDto, _) = _extraction.ExtractEventDate(messages);
                        if (dateDto.HasValue)
                        {
                            eventDateStr = dateDto.Value.ToString("yyyy-MM-dd");
                            _logger.LogInformation("[update_equipment] Extracted date from conversation: {Date}", eventDateStr);
                        }
                    }
                    
                    // Extract times if not in session
                    if (string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr))
                    {
                        var scheduleTimes = _extraction.ExtractScheduleTimes(messages);
                        
                        if (string.IsNullOrWhiteSpace(startTimeStr) && scheduleTimes.ContainsKey("show_start_time"))
                        {
                            var extractedStart = scheduleTimes["show_start_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedStart.Length == 4 && int.TryParse(extractedStart, out var startMinutes))
                            {
                                var hours = startMinutes / 100;
                                var minutes = startMinutes % 100;
                                startTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[update_equipment] Extracted start time from conversation: {Time}", startTimeStr);
                            }
                            else
                            {
                                startTimeStr = extractedStart;
                            }
                        }
                        
                        if (string.IsNullOrWhiteSpace(endTimeStr) && scheduleTimes.ContainsKey("show_end_time"))
                        {
                            var extractedEnd = scheduleTimes["show_end_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedEnd.Length == 4 && int.TryParse(extractedEnd, out var endMinutes))
                            {
                                var hours = endMinutes / 100;
                                var minutes = endMinutes % 100;
                                endTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[update_equipment] Extracted end time from conversation: {Time}", endTimeStr);
                            }
                            else
                            {
                                endTimeStr = extractedEnd;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - use session values only if extraction fails
                _logger.LogWarning(ex, "[update_equipment] Failed to extract date/time from conversation, using session values only");
            }
        }
        
        // Log if date/time are still missing
        if (string.IsNullOrWhiteSpace(eventDateStr))
        {
            _logger.LogWarning("[update_equipment] Event date is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(startTimeStr))
        {
            _logger.LogWarning("[update_equipment] Start time is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(endTimeStr))
        {
            _logger.LogWarning("[update_equipment] End time is missing from both session and conversation");
        }
        
        double totalDayRate = 0;
        var summaryRequests = GetSummaryRequestsFromSession(session, currentItems);
        ApplySummaryRequestRemovals(summaryRequests, removeTypes);
        AppendSummaryRequests(summaryRequests, root);
        var projectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorAreas"));
        if (projectorAreas.Count == 0)
            projectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorArea"));
        var summaryLines = new List<string>();
        summaryLines.Add("## 📋 Quote Summary\n");
        summaryLines.Add("### Event Details");
        if (!string.IsNullOrWhiteSpace(venueName)) summaryLines.Add($"**Venue:** {venueName}");
        if (!string.IsNullOrWhiteSpace(roomName)) summaryLines.Add($"**Room:** {roomName}");
        if (projectorAreas.Count == 1) summaryLines.Add($"**Projector Placement Area:** {projectorAreas[0]}");
        else if (projectorAreas.Count > 1) summaryLines.Add($"**Projector Placement Areas:** {string.Join(", ", projectorAreas)}");
        summaryLines.Add($"**Event Type:** {eventType}");
        summaryLines.Add($"**Attendees:** {expectedAttendees}");
        
        // Add event date if available
        if (!string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out var eventDate))
        {
            summaryLines.Add($"**Date:** {eventDate:dddd d MMMM yyyy}");
        }
        
        // Add start and end times if available
        if (!string.IsNullOrWhiteSpace(startTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(startTimeStr, out var startTime))
            {
                summaryLines.Add($"**Start Time:** {startTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**Start Time:** {startTimeStr}");
            }
        }
        if (!string.IsNullOrWhiteSpace(endTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(endTimeStr, out var endTime))
            {
                summaryLines.Add($"**End Time:** {endTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**End Time:** {endTimeStr}");
            }
        }
        
        summaryLines.Add("");
        summaryLines.Add("### Requirement Summary\n");
        
        // Ensure equipment is displayed - add validation
        if (currentItems.Count == 0)
        {
            summaryLines.Add("*No equipment items in the quote. Please add equipment to continue.*");
            _logger.LogWarning("[update_equipment] Equipment list is empty after updates");
        }
        else
        {
            _logger.LogInformation("[update_equipment] Displaying {Count} requirement-level equipment lines", summaryRequests.Count);
            foreach (var line in BuildRequirementSummaryLines(summaryRequests))
                summaryLines.Add($"- {line}");
            summaryLines.Add("");

            _logger.LogInformation("[update_equipment] Calculating total day rate from {Count} selected items", currentItems.Count);
            foreach (var item in currentItems)
            {
                _logger.LogInformation("[update_equipment] Processing item: {Qty}x {Desc} (Code: {Code})", item.Quantity, item.Description, item.ProductCode);

                var rate = rates.GetValueOrDefault(item.ProductCode ?? "", 0);
                totalDayRate += rate * item.Quantity;
            }
            if (addCouldNotFind.Count > 0)
            {
                summaryLines.Add($"*Note: Could not find {string.Join(", ", addCouldNotFind)} to add; please ask for alternatives if needed.*\n");
                _logger.LogWarning("[update_equipment] Could not find {Count} requested items: {Items}", addCouldNotFind.Count, string.Join(", ", addCouldNotFind));
            }
        }

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
            summaryLines.Add("### Technician Support\n");
            foreach (var labor in laborItems)
            {
                summaryLines.Add($"- **{FormatLaborSummaryLine(labor)}**");
            }
            summaryLines.Add("");
        }
        
        // Total - Removed price display as per user request
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

        // Equipment changed — force regeneration of quote
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

    #endregion

    #region Quote Summary Helpers

    private static string FormatLaborSummaryLine(RecommendedLaborItem labor)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(labor.ProductCode))
            details.Add(labor.ProductCode.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(labor.Task))
            details.Add(labor.Task.Trim());

        var duration = FormatLaborDuration(labor.Hours, labor.Minutes);
        if (!string.IsNullOrWhiteSpace(duration))
            details.Add(duration);

        if (details.Count == 0)
            return $"{labor.Quantity}x {labor.Description}";

        return $"{labor.Quantity}x {labor.Description} ({string.Join(" | ", details)})";
    }

    private static string FormatLaborDuration(double hours, int minutes)
    {
        var totalMinutes = minutes;
        if (hours > 0)
            totalMinutes += (int)Math.Round(hours * 60);

        if (totalMinutes <= 0)
            return "";

        var wholeHours = totalMinutes / 60;
        var remainingMinutes = totalMinutes % 60;

        if (wholeHours > 0 && remainingMinutes > 0)
            return $"{wholeHours}h {remainingMinutes}m";
        if (wholeHours > 0)
            return $"{wholeHours}h";

        return $"{remainingMinutes}m";
    }

    private static List<string> BuildRequirementSummaryLines(IEnumerable<EquipmentRequest> requests)
    {
        var normalized = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.EquipmentType))
            .Select(r => new
            {
                EquipmentType = NormalizeEquipmentTypeLabel(r.EquipmentType),
                Quantity = Math.Max(1, r.Quantity)
            })
            .GroupBy(r => r.EquipmentType, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                EquipmentType = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .OrderBy(x => x.EquipmentType, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Quantity} {PluralizeEquipmentLabel(x.EquipmentType, x.Quantity)}")
            .ToList();

        return normalized;
    }

    private static string NormalizeEquipmentTypeLabel(string? rawType)
    {
        var value = (rawType ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            return "equipment item";

        return value switch
        {
            "mic" or "microphones" => "microphone",
            "speakers" => "speaker",
            "screens" => "screen",
            "projectors" => "projector",
            "laptops" => "laptop",
            "clickers" => "clicker",
            _ => value
        };
    }

    private static bool IsLaptopEquipmentType(string? equipmentType)
    {
        var value = (equipmentType ?? "").Trim().ToLowerInvariant();
        return value == "laptop"
            || value == "laptops"
            || value.Contains("laptop", StringComparison.OrdinalIgnoreCase)
            || value.Contains("macbook", StringComparison.OrdinalIgnoreCase)
            || value.Contains("notebook", StringComparison.OrdinalIgnoreCase);
    }

    private static string PluralizeEquipmentLabel(string label, int quantity)
    {
        if (quantity == 1) return label;
        return label switch
        {
            "microphone" => "microphones",
            "speaker" => "speakers",
            "screen" => "screens",
            "projector" => "projectors",
            "laptop" => "laptops",
            "clicker" => "clickers",
            _ when label.EndsWith("s", StringComparison.OrdinalIgnoreCase) => label,
            _ => $"{label}s"
        };
    }

    private static List<EquipmentRequest> GetSummaryRequestsFromSession(ISession session, List<SelectedEquipmentItem> currentItems)
    {
        var json = session.GetString("Draft:SummaryEquipmentRequests");
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<EquipmentRequest>>(json);
                if (parsed is { Count: > 0 })
                    return parsed;
            }
            catch
            {
                // Fall through to inferred requests.
            }
        }

        return currentItems
            .Where(i => !string.IsNullOrWhiteSpace(i.Description))
            .GroupBy(i => InferEquipmentTypeFromDescription(i.Description), StringComparer.OrdinalIgnoreCase)
            .Select(g => new EquipmentRequest
            {
                EquipmentType = g.Key,
                Quantity = Math.Max(1, g.Sum(x => x.Quantity))
            })
            .ToList();
    }

    private static string InferEquipmentTypeFromDescription(string? description)
    {
        var text = (description ?? "").ToLowerInvariant();
        if (text.Contains("mic")) return "microphone";
        if (text.Contains("screen")) return "screen";
        if (text.Contains("projector")) return "projector";
        if (text.Contains("speaker")) return "speaker";
        if (text.Contains("laptop at stage") || text.Contains("laptop on stage") || text.Contains("sdicross")
            || (text.Contains("sdi") && (text.Contains("cross") || text.Contains("extension"))))
            return "laptop_at_stage";
        if (text.Contains("laptop") || text.Contains("macbook") || text.Contains("notebook")) return "laptop";
        if (text.Contains("clicker") || text.Contains("presenter")) return "clicker";
        if (text.Contains("camera")) return "camera";
        if (text.Contains("flipchart")) return "flipchart";
        if (text.Contains("lectern") || text.Contains("podium")) return "lectern";
        if (text.Contains("mixer")) return "mixer";
        if (text.Contains("foldback") || text.Contains("confidence monitor")) return "foldback_monitor";
        if (text.Contains("switcher") || text.Contains("v1hd")) return "switcher";
        if (text.Contains("usbc") || text.Contains("hdmi adaptor")) return "hdmi_adaptor";
        return "equipment item";
    }

    private static void ApplySummaryRequestRemovals(List<EquipmentRequest> summaryRequests, List<string> removeTypes)
    {
        if (removeTypes.Count == 0) return;
        summaryRequests.RemoveAll(r =>
        {
            var type = (r.EquipmentType ?? "").Trim().ToLowerInvariant();
            return removeTypes.Any(remove => type.Contains(remove, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static void AppendSummaryRequests(List<EquipmentRequest> summaryRequests, JsonElement root)
    {
        if (!root.TryGetProperty("add_requests", out var addArr) || addArr.ValueKind != JsonValueKind.Array || addArr.GetArrayLength() == 0)
            return;

        foreach (var add in addArr.EnumerateArray())
        {
            var rawType = add.TryGetProperty("equipment_type", out var eqt) ? eqt.GetString() : null;
            if (string.IsNullOrWhiteSpace(rawType))
                continue;
            var quantity = add.TryGetProperty("quantity", out var qty) && qty.ValueKind == JsonValueKind.Number ? qty.GetInt32() : 1;
            var equipmentType = NormalizeEquipmentTypeLabel(rawType);
            var existing = summaryRequests.FirstOrDefault(x => string.Equals(x.EquipmentType, equipmentType, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                summaryRequests.Add(new EquipmentRequest
                {
                    EquipmentType = equipmentType,
                    Quantity = Math.Max(1, quantity)
                });
            }
            else
            {
                existing.Quantity += Math.Max(1, quantity);
            }
        }
    }

    private static bool RequiresProjectorPlacementArea(IEnumerable<EquipmentRequest> requests)
    {
        foreach (var request in requests)
        {
            var type = (request.EquipmentType ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(type)) continue;
            if (type.Contains("projector") || type.Contains("screen") || type.Contains("display"))
                return true;
        }
        return false;
    }

    private static int GetRequestedProjectorCount(IEnumerable<EquipmentRequest> requests)
    {
        var qty = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.EquipmentType))
            .Where(r => (r.EquipmentType ?? "").Contains("projector", StringComparison.OrdinalIgnoreCase))
            .Sum(r => Math.Max(1, r.Quantity));
        return qty <= 0 ? 1 : qty;
    }

    private static bool IsWestinBallroomFamilyRoom(string? venueName, string? roomName)
    {
        var venue = (venueName ?? "").Trim().ToLowerInvariant();
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        if (!(venue.Contains("westin") && venue.Contains("brisbane"))) return false;
        if (string.IsNullOrWhiteSpace(room)) return false;
        return room == "westin ballroom"
            || room == "westin ballroom 1"
            || room == "westin ballroom 2"
            || room == "full westin ballroom"
            || room == "westin ballroom full"
            || room == "ballroom"
            || room == "full ballroom"
            || room == "ballroom 1"
            || room == "ballroom 2";
    }

    private static bool IsAmbiguousWestinBallroomParentRoom(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        return room == "westin ballroom" || room == "ballroom";
    }

    private static bool UserExplicitlyConfirmedFullWestinBallroom(IEnumerable<DisplayMessage> messages)
    {
        foreach (var message in messages)
        {
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var part in message.Parts ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                var text = part.Trim().ToLowerInvariant();
                if (text.Contains("full westin ballroom", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("westin ballroom full", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("full ballroom", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var fullText = (message.FullText ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(fullText) &&
                (fullText.Contains("full westin ballroom", StringComparison.OrdinalIgnoreCase) ||
                 fullText.Contains("westin ballroom full", StringComparison.OrdinalIgnoreCase) ||
                 fullText.Contains("full ballroom", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetAllowedProjectorAreas(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        if (room is "westin ballroom 1" or "ballroom 1") return new List<string> { "E", "D", "C" };
        if (room is "westin ballroom 2" or "ballroom 2") return new List<string> { "A", "F", "B" };
        return new List<string> { "A", "B", "C", "D", "E", "F" };
    }

    private static List<string> GetNormalizedProjectorAreasFromArgs(JsonElement root)
    {
        if (root.TryGetProperty("projector_areas", out var arr))
        {
            if (arr.ValueKind == JsonValueKind.Array)
            {
                var merged = string.Join(",", arr.EnumerateArray().Select(x => x.GetString() ?? ""));
                return GetNormalizedProjectorAreas(merged);
            }
            if (arr.ValueKind == JsonValueKind.String)
                return GetNormalizedProjectorAreas(arr.GetString());
        }

        if (root.TryGetProperty("projector_area", out var single) && single.ValueKind == JsonValueKind.String)
            return GetNormalizedProjectorAreas(single.GetString());

        return new List<string>();
    }

    /// <summary>
    /// Verify the user actually mentioned an attendee count in the conversation.
    /// Returns the extracted count if found, or 0 if the user never stated one.
    /// </summary>
    private static int ExtractAttendeesFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.ToList();
        var userMessages = ordered
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var userText = string.Join(" ", userMessages.SelectMany(m => m.Parts ?? Enumerable.Empty<string>()));

        if (string.IsNullOrWhiteSpace(userText)) return 0;

        // "30 people", "30 attendees", "50 guests", "100 pax", "20 participants"
        var directMatch = Regex.Match(userText, @"\b(\d{1,4})\s*(?:people|attendees|pax|participants|guests)\b", RegexOptions.IgnoreCase);
        if (directMatch.Success && int.TryParse(directMatch.Groups[1].Value, out var direct) && direct > 0)
            return direct;

        // "expecting 30", "about 30", "around 50"
        var expectingMatch = Regex.Match(userText, @"\b(?:expecting|about|around|approximately|roughly)\s+(\d{1,4})\b", RegexOptions.IgnoreCase);
        if (expectingMatch.Success && int.TryParse(expectingMatch.Groups[1].Value, out var expecting) && expecting > 0)
            return expecting;

        // Context-aware: if assistant asked about attendees, check if user's next message contains a standalone number
        for (int i = 0; i < ordered.Count; i++)
        {
            var msg = ordered[i];
            if (!msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var assistantText = string.Join(" ", msg.Parts ?? Enumerable.Empty<string>());
            if (!Regex.IsMatch(assistantText, @"\b(?:attendees|how many|number of (?:people|guests|attendees|participants))\b", RegexOptions.IgnoreCase))
                continue;

            // Check the next user message for a number
            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null) continue;

            var nextText = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            var numberMatch = Regex.Match(nextText, @"\b(\d{1,4})\b");
            if (numberMatch.Success && int.TryParse(numberMatch.Groups[1].Value, out var contextual) && contextual > 0)
                return contextual;
        }

        return 0;
    }

    /// <summary>
    /// Verify the user actually mentioned a room setup style in the conversation.
    /// Returns the style keyword if found, or null if the user never stated one.
    /// </summary>
    private static string? ExtractSetupStyleFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var userText = string.Join(" ", messages
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .SelectMany(m => m.Parts ?? Enumerable.Empty<string>()));

        if (string.IsNullOrWhiteSpace(userText)) return null;

        // Match setup style keywords in user messages
        var match = Regex.Match(userText, @"\b(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim().ToLowerInvariant() : null;
    }

    private static List<string> GetNormalizedProjectorAreas(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        var text = raw.Trim().ToUpperInvariant().Replace(" ", "");
        if (text.Length == 1 && text[0] is >= 'A' and <= 'F')
            return new List<string> { text };

        var plusMatch = Regex.Match(text, @"^([A-F])\+([A-F])$");
        if (plusMatch.Success)
        {
            foreach (var g in new[] { plusMatch.Groups[1].Value, plusMatch.Groups[2].Value })
            {
                if (!result.Contains(g, StringComparer.OrdinalIgnoreCase))
                    result.Add(g);
            }

            return result;
        }

        foreach (var segment in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var seg = segment.Trim().ToUpperInvariant().Replace(" ", "");
            if (seg.Length == 1 && seg[0] is >= 'A' and <= 'F' && !result.Contains(seg, StringComparer.OrdinalIgnoreCase))
                result.Add(seg);
        }

        if (result.Count > 0)
            return result;

        foreach (Match m in Regex.Matches(text, @"\b(?:PROJECTOR\s+AREA|AREA)?\s*[:\-]?\s*([A-F])\b", RegexOptions.IgnoreCase))
        {
            var area = m.Groups[1].Value.ToUpperInvariant();
            if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                result.Add(area);
        }
        return result;
    }

    #endregion
}
