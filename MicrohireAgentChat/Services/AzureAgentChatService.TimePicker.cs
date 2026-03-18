// AzureAgentChatService partial
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using Markdig;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services
{
    public sealed partial class AzureAgentChatService
    {
        private async Task MaybeAppendTimePickerAsync(string threadId, IEnumerable<DisplayMessage> messages, CancellationToken ct)
        {
            // 0) Session already has confirmed schedule times? Don't show picker again.
            var sess = _http.HttpContext?.Session;
            if (sess != null
                && !string.IsNullOrWhiteSpace(sess.GetString("Draft:StartTime"))
                && !string.IsNullOrWhiteSpace(sess.GetString("Draft:EndTime")))
            {
                _logger.LogInformation("TIME PICKER: Skipping – schedule already confirmed in session (Start={Start}, End={End})",
                    sess.GetString("Draft:StartTime"), sess.GetString("Draft:EndTime"));
                return;
            }

            // 1) Already shown?
            bool uiAlreadyShown = messages.Any(m =>
                (m.Parts ?? Enumerable.Empty<string>())
                    .Any(p => p.IndexOf("\"type\":\"timepicker\"", StringComparison.OrdinalIgnoreCase) >= 0
                           || p.IndexOf("\"type\":\"multitime\"", StringComparison.OrdinalIgnoreCase) >= 0));
            if (uiAlreadyShown) return;

            // 2) A single range already chosen?
            bool timeChosen = messages.Any(m =>
                (m.Parts ?? Enumerable.Empty<string>())
                    .Any(p => p.StartsWith("Choose time:", StringComparison.OrdinalIgnoreCase)));
            if (timeChosen) return;

            // 3) A multi-schedule already chosen? (original or reformatted form)
            bool scheduleChosen = messages.Any(m =>
                (m.Parts ?? Enumerable.Empty<string>())
                    .Any(p => p.StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase)
                           || p.StartsWith("I've selected this schedule:", StringComparison.OrdinalIgnoreCase)));
            if (scheduleChosen) return;

            // 3) Extract and validate the date
            _logger.LogInformation("TIME PICKER: Starting date extraction from {MessageCount} messages", messages.Count());
            var (dateDto, matchedText) = _chatExtraction.ExtractEventDate(messages);
            if (dateDto is null)
            {
                _logger.LogInformation("TIME PICKER: No date extracted from messages, returning early");
                return;
            }

            // Log what we extracted for debugging
            _logger.LogInformation("TIME PICKER: Extracted date: {Date} from text: '{Text}'", dateDto.Value, matchedText);
            _logger.LogInformation("TIME PICKER: Current date/time: {Now}, date only: {NowDate}", DateTimeOffset.Now, DateTimeOffset.Now.Date);
            _logger.LogInformation("TIME PICKER: Is extracted date in past? {IsPast} (extracted: {Extracted}, current: {Current})",
                dateDto.Value.Date < DateTimeOffset.Now.Date, dateDto.Value.Date, DateTimeOffset.Now.Date);

            // Validate the date is reasonable (not more than 2 years in the future, not in the past)
            var now = DateTimeOffset.Now;
            var twoYearsFromNow = now.AddYears(2);
            _logger.LogInformation("TIME PICKER: Validation bounds - Now: {Now}, TwoYearsFromNow: {TwoYears}", now.Date, twoYearsFromNow.Date);

            if (dateDto.Value.Date < now.Date || dateDto.Value.Date > twoYearsFromNow.Date)
            {
                _logger.LogWarning("TIME PICKER: Date validation failed - extracted: {Extracted}, is_past: {IsPast}, is_too_far_future: {IsTooFar}",
                    dateDto.Value.Date, dateDto.Value.Date < now.Date, dateDto.Value.Date > twoYearsFromNow.Date);

                // Try to fix it with smart detection
                var correctedDate = dateDto.Value;
                int forwardRolls = 0;
                int backwardRolls = 0;

                _logger.LogInformation("TIME PICKER: Starting correction from {OriginalDate}", correctedDate.Date);
                while (correctedDate.Date < now.Date)
                {
                    var beforeCorrection = correctedDate;
                    correctedDate = correctedDate.AddYears(1);
                    forwardRolls++;
                    _logger.LogInformation("TIME PICKER: Forward correction {Count}: {Before} -> {After}", forwardRolls, beforeCorrection.Date, correctedDate.Date);
                }
                while (correctedDate.Date > twoYearsFromNow.Date)
                {
                    var beforeCorrection = correctedDate;
                    correctedDate = correctedDate.AddYears(-1);
                    backwardRolls++;
                    _logger.LogInformation("TIME PICKER: Backward correction {Count}: {Before} -> {After}", backwardRolls, beforeCorrection.Date, correctedDate.Date);
                }
                dateDto = correctedDate;
                _logger.LogInformation("TIME PICKER: Final corrected date: {CorrectedDate} (forward rolls: {Forward}, backward rolls: {Backward})",
                    dateDto.Value, forwardRolls, backwardRolls);
            }

            var dateIso = dateDto.Value.ToString("yyyy-MM-dd");
            var prettyDate = dateDto.Value.ToString("d MMMM yyyy");
            _logger.LogInformation("TIME PICKER: Formatted date - ISO: '{Iso}', Pretty: '{Pretty}'", dateIso, prettyDate);

            // Final date sanity check
            var finalCheck = DateTimeOffset.Now;
            if (dateDto.Value.Date <= finalCheck.Date)
            {
                _logger.LogError("TIME PICKER: CRITICAL: Final validation failed! Date {Date} is not in the future. Current time: {Now}. Aborting time picker.", dateDto.Value, finalCheck);
                return; // Don't show time picker with invalid date
            }

            _logger.LogInformation("TIME PICKER: Date validation passed. Proceeding to evaluate time picker trigger for {Date}", prettyDate);
            var session = _http.HttpContext?.Session;
            var attendeesFromSession = GetExpectedAttendeesFromSession(session);
            var attendeesFromConversation = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
            var attendeesKnown = attendeesFromSession > 0 || attendeesFromConversation > 0;

            var setupStyle = session?.GetString("Draft:SetupStyle") ?? session?.GetString("Ack:SetupStyle");
            if (string.IsNullOrWhiteSpace(setupStyle))
            {
                setupStyle = TryExtractUserSetupStyle(messages);
            }

            var (_, venueName, _, _) = _chatExtraction.ExtractVenueAndEventDate(messages);
            var userRoom = TryExtractUserRoom(messages);
            var roomRequired = VenueRequiresExplicitRoom(venueName);
            var roomConfirmed = !roomRequired || !string.IsNullOrWhiteSpace(userRoom);
            var roomDisambiguated = !(venueName?.Contains("westin", StringComparison.OrdinalIgnoreCase) == true &&
                                      IsAmbiguousWestinBallroomParentRoom(userRoom));

            if (!attendeesKnown || string.IsNullOrWhiteSpace(setupStyle) || !roomConfirmed || !roomDisambiguated)
            {
                _logger.LogInformation(
                    "TIME PICKER: Skipping auto-append until event details are complete. AttendeesKnown={AttendeesKnown}, SetupKnown={SetupKnown}, RoomRequired={RoomRequired}, RoomConfirmed={RoomConfirmed}, RoomDisambiguated={RoomDisambiguated}, Venue={Venue}, Room={Room}",
                    attendeesKnown,
                    !string.IsNullOrWhiteSpace(setupStyle),
                    roomRequired,
                    roomConfirmed,
                    roomDisambiguated,
                    venueName ?? "(none)",
                    userRoom ?? "(none)");
                return;
            }

            // 4) Did Isla just ask for the time? (or earlier trigger: venue+date confirmed, no schedule yet)
            var lastAssistant = messages.LastOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
            string lastAssistantText = string.Join("\n\n", lastAssistant?.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(lastAssistantText))
                return;

            var lastLower = lastAssistantText.ToLowerInvariant();
            bool lastConfirmsVenueOrDate = (!string.IsNullOrWhiteSpace(venueName) && lastLower.Contains(venueName.ToLowerInvariant())) ||
                lastLower.Contains("confirmed") || lastLower.Contains("noted") || lastLower.Contains("great choice") || lastLower.Contains("perfect");
            bool lastMentionsDate = lastLower.Contains(prettyDate.ToLowerInvariant()) || lastLower.Contains(dateIso) ||
                Regex.IsMatch(lastLower, @"\b(march|april|may|june|july|august|september|october|november|december|january|february)\s+\d{1,2}");
            bool useEarlierTrigger = !string.IsNullOrWhiteSpace(venueName)
                && lastConfirmsVenueOrDate
                && lastMentionsDate
                && (lastLower.Contains("schedule") || lastLower.Contains("time picker") || lastLower.Contains("confirm your schedule"));

            // Check for various ways the AI might ask for time information
            var textLower = lastLower;
            bool isAskingForTime = textLower.Contains("what time") ||
                                   textLower.Contains("start time") ||
                                   textLower.Contains("end time") ||
                                   textLower.Contains("when does") ||
                                   textLower.Contains("schedule") ||
                                   textLower.Contains("timing") ||
                                   textLower.Contains("setup time") ||
                                   textLower.Contains("pack up") ||
                                   textLower.Contains("pack-up") ||
                                   textLower.Contains("packup") ||
                                   textLower.Contains("time picker") ||
                                   textLower.Contains("times for your") ||
                                   (textLower.Contains("choose") && textLower.Contains("time"));

            if (!isAskingForTime && !useEarlierTrigger)
            {
                _logger.LogInformation("TIME PICKER: Skipping – last assistant message is not asking for time and earlier trigger not met.\nAssistant: {AssistantText}", lastAssistantText);
                return;
            }

            // 4b) Also consider the last USER message to avoid interrupting unrelated questions
            var lastUser = messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            string lastUserText = string.Join("\n\n", lastUser?.Parts ?? Enumerable.Empty<string>());

            if (!string.IsNullOrWhiteSpace(lastUserText))
            {
                var lastUserLower = lastUserText.ToLowerInvariant();

                // Don't interrupt quote confirmation flow with a time picker
                if (lastUserLower.Contains("create quote") || lastUserLower.Contains("generate quote")
                    || lastUserLower.Contains("yes create") || lastUserLower.Contains("yes, create"))
                {
                    _logger.LogInformation("TIME PICKER: Skipping – user is confirming quote creation.\nUser: {UserText}", lastUserText);
                    return;
                }

                bool userAskedQuestion = lastUserText.TrimEnd().EndsWith("?", StringComparison.OrdinalIgnoreCase);

                // Any mention of time/schedule keywords means it's safe to proceed
                bool userMentionsTime =
                    lastUserLower.Contains("time") ||
                    lastUserLower.Contains("schedule") ||
                    lastUserLower.Contains("start") ||
                    lastUserLower.Contains("end") ||
                    lastUserLower.Contains("setup") ||
                    lastUserLower.Contains("rehearsal") ||
                    lastUserLower.Contains("pack up") ||
                    lastUserLower.Contains("pack-up") ||
                    lastUserLower.Contains("timing");

                // Heuristic: questions about room options/layout should not trigger the picker
                bool looksLikeRoomLayoutQuestion =
                    (lastUserLower.Contains("room") && lastUserLower.Contains("options")) ||
                    lastUserLower.Contains("layout") ||
                    lastUserLower.Contains("room layout");

                if (userAskedQuestion && !userMentionsTime)
                {
                    _logger.LogInformation(
                        "TIME PICKER: Skipping auto-append because the last user message is a non-time-related question.\nUser: {UserText}\nAssistantPrompt: {AssistantText}",
                        lastUserText, lastAssistantText);
                    return;
                }

                if (looksLikeRoomLayoutQuestion && !userMentionsTime)
                {
                    _logger.LogInformation(
                        "TIME PICKER: Skipping auto-append for room/layout follow-up.\nUser: {UserText}\nAssistantPrompt: {AssistantText}",
                        lastUserText, lastAssistantText);
                    return;
                }
            }

            // 5) Try to pre-fill schedule times from conversation if not already in session
            if (session != null)
            {
                var currentStart = session.GetString("Draft:StartTime");
                var currentEnd = session.GetString("Draft:EndTime");

                if (string.IsNullOrWhiteSpace(currentStart) || string.IsNullOrWhiteSpace(currentEnd))
                {
                    var (startTime, endTime, matchedTimeText) = _chatExtraction.ExtractEventTime(messages);
                    if (startTime.HasValue)
                    {
                        session.SetString("Draft:StartTime", startTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER: Prefilled Draft:StartTime from conversation match '{Matched}' => {Start}", matchedTimeText, startTime);
                    }
                    if (endTime.HasValue)
                    {
                        session.SetString("Draft:EndTime", endTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER: Prefilled Draft:EndTime from conversation match '{Matched}' => {End}", matchedTimeText, endTime);
                    }
                }
            }

            // 7) Read schedule times from session, fallback to defaults if not found
            var setupTime = session?.GetString("Draft:SetupTime") ?? "07:00";
            var rehearsalTime = session?.GetString("Draft:RehearsalTime") ?? "09:30";
            var sessionStartTime = session?.GetString("Draft:StartTime") ?? "10:00";
            var sessionEndTime = session?.GetString("Draft:EndTime") ?? "16:00";
            var packupTime = session?.GetString("Draft:PackupTime") ?? "18:00";

            // Build the inline MULTI timepicker JSON (Setup, Rehearsal, Pack Up)
            var uiPayload = new
            {
                ui = new
                {
                    type = "multitime",
                    title = $"Confirm your schedule for {prettyDate}",
                    date = dateIso,
                    pickers = new[]
                    {
                new { name = "setup",     label = "Setup Time",     @default = setupTime },
                new { name = "rehearsal", label = "Rehearsal Time", @default = rehearsalTime },
                new { name = "start",     label = "Event Start Time", @default = sessionStartTime },
                new { name = "end",       label = "Event End Time",   @default = sessionEndTime },
                new { name = "packup",    label = "Pack Up Time",   @default = packupTime },
            },
                    submitLabel = "Submit"
                }
            };
            var uiJson = JsonSerializer.Serialize(uiPayload);

            // Combine loading preface and time picker into a single assistant message
            var preface = "I'm preparing a time picker for your event schedule. One moment please...";
            var text = preface + "\n\n" +
                       $"Perfect! I've confirmed your event date as **{prettyDate}**. Here’s the time picker for you to confirm your schedule:\n\n" +
                       uiJson;

            AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, text);
            await Task.CompletedTask;
        }

        private static int GetExpectedAttendeesFromSession(ISession? session)
        {
            if (session == null) return 0;
            if (int.TryParse(session.GetString("Draft:ExpectedAttendees"), out var draftAttendees) && draftAttendees > 0)
                return draftAttendees;
            if (int.TryParse(session.GetString("Ack:Attendees"), out var ackAttendees) && ackAttendees > 0)
                return ackAttendees;
            return 0;
        }

        private static bool VenueRequiresExplicitRoom(string? venueName)
        {
            if (string.IsNullOrWhiteSpace(venueName)) return false;
            var venue = venueName.Trim().ToLowerInvariant();
            return (venue.Contains("westin") && venue.Contains("brisbane"))
                || (venue.Contains("four points") && venue.Contains("brisbane"));
        }

        private static bool IsAmbiguousWestinBallroomParentRoom(string? roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName)) return false;
            var room = roomName.Trim().ToLowerInvariant();
            return room == "westin ballroom" || room == "ballroom";
        }

        private static string? TryExtractUserSetupStyle(IEnumerable<DisplayMessage> messages)
        {
            var userText = string.Join(" ", messages
                .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                .SelectMany(m => m.Parts ?? Enumerable.Empty<string>()));

            if (string.IsNullOrWhiteSpace(userText)) return null;

            var explicitSetup = Regex.Match(
                userText,
                @"\b(theatre|theater|boardroom|classroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\s+(?:setup|style|layout)\b",
                RegexOptions.IgnoreCase);
            if (explicitSetup.Success)
            {
                return NormalizeSetupStyle(explicitSetup.Groups[1].Value);
            }

            var explicitLayout = Regex.Match(
                userText,
                @"\b(?:setup|style|layout)\s+(?:is|will\s+be|should\s+be)?\s*(?:a\s+)?(theatre|theater|boardroom|classroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\b",
                RegexOptions.IgnoreCase);
            return explicitLayout.Success ? NormalizeSetupStyle(explicitLayout.Groups[1].Value) : null;
        }

        private static string NormalizeSetupStyle(string value)
        {
            var normalized = value.Trim().ToLowerInvariant();
            if (normalized == "theater") return "theatre";
            if (normalized == "u shape") return "u-shape";
            return normalized;
        }

        private static string? TryExtractUserRoom(IEnumerable<DisplayMessage> messages)
        {
            var userParts = messages
                .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                .SelectMany(m => m.Parts ?? Enumerable.Empty<string>());

            foreach (var part in userParts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var text = part.ToLowerInvariant();

                if (text.Contains("westin ballroom 1") || Regex.IsMatch(text, @"\bballroom\s*1\b")) return "Westin Ballroom 1";
                if (text.Contains("westin ballroom 2") || Regex.IsMatch(text, @"\bballroom\s*2\b")) return "Westin Ballroom 2";
                if (text.Contains("full westin ballroom") || text.Contains("westin ballroom full") || text.Contains("full ballroom")) return "Westin Ballroom";
                if (text.Contains("westin ballroom")) return "Westin Ballroom";
                if (text.Contains("thrive boardroom") || text.Contains("thrive room")) return "Thrive Boardroom";
                if (Regex.IsMatch(text, @"\belevate\s*1\b")) return "Elevate 1";
                if (Regex.IsMatch(text, @"\belevate\s*2\b")) return "Elevate 2";
                if (Regex.IsMatch(text, @"\belevate\b")) return "Elevate";
                if (text.Contains("the podium") || Regex.IsMatch(text, @"\bpodium\b")) return "The Podium";
                if (text.Contains("meeting room") && text.Contains("four points")) return "Meeting Room";
            }

            return null;
        }

    }
}
