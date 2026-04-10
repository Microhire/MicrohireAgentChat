using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Controllers;

/// <summary>
/// Quote generation pipeline, booking creation, quote card builders,
/// review prompt helpers, disk recovery, and projector/time capture helpers.
/// </summary>
public sealed partial class ChatController
{
    private const string GenerateQuoteFlag = "Draft:GenerateQuote";

    /// <summary>
    /// After server-side <c>recommend_equipment_for_event</c> for follow-up AV, run the same contact / schedule /
    /// Westin projector guards and booking + <see cref="GenerateQuoteForBookingAsync"/> as the explicit consent path,
    /// without requiring the user to type "yes create quote".
    /// </summary>
    /// <returns>Non-null to short-circuit SendPartial; null when processing completed (caller refreshes transcript).</returns>
    private async Task<IActionResult?> TryFollowUpAvQuotePipelineAsync(List<DisplayMessage> msgList, string threadId, CancellationToken ct)
    {
        // Ensure forms are in chat BEFORE adding the quote so the quote stays at the bottom.
        EnsureStructuredFormsInChat(msgList);

        var summaryKey = ComputeSummaryKey(msgList);
        var lastPersistedKey = HttpContext.Session.GetString("Draft:PersistedSummaryKey");
        var quoteCompleteNow = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";
        var quoteUrlNow = HttpContext.Session.GetString("Draft:QuoteUrl");
        var quoteSuccessRecently = msgList
            .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .TakeLast(3)
            .Any(m =>
            {
                var raw = (m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>())).ToLowerInvariant();
                return raw.Contains("successfully generated your quote") && raw.Contains("booking");
            });

        if (quoteCompleteNow && !string.IsNullOrWhiteSpace(quoteUrlNow) && quoteSuccessRecently)
        {
            _logger.LogInformation("[FollowUpAv] Skipping quote generation — quote already generated in recent messages.");
            if (!string.IsNullOrEmpty(summaryKey))
                HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey);

            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        if (!string.IsNullOrEmpty(summaryKey) &&
            string.Equals(summaryKey, lastPersistedKey, StringComparison.Ordinal))
        {
            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        var contactName = HttpContext.Session.GetString("Draft:ContactName");
        var contactEmail = HttpContext.Session.GetString("Draft:ContactEmail");
        var contactPhone = HttpContext.Session.GetString("Draft:ContactPhone");
        var organisation = HttpContext.Session.GetString("Draft:Organisation");

        var missingContactFields = new List<string>();
        if (string.IsNullOrWhiteSpace(contactName))
            missingContactFields.Add("customer name");
        if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
            missingContactFields.Add("contact email or phone number");
        if (string.IsNullOrWhiteSpace(organisation) && !IsLeadEntry())
            missingContactFields.Add("organisation name");

        if (missingContactFields.Count > 0)
        {
            _logger.LogWarning("[FollowUpAv] Quote blocked - missing contact fields: {Fields}",
                string.Join(", ", missingContactFields));

            var requestInfoMessage = new DisplayMessage
            {
                Role = "assistant",
                Timestamp = DateTimeOffset.UtcNow,
                Parts = new List<string> { $"Before I can create your quote, I need a few quick details: {string.Join(", ", missingContactFields)}. Could you please provide this information?" },
                FullText = $"Before I can create your quote, I need: {string.Join(", ", missingContactFields)}.",
                Html = $"<p>Before I can create your quote, I need a few quick details: <strong>{string.Join(", ", missingContactFields)}</strong>. Could you please provide this information?</p>"
            };
            msgList.Add(requestInfoMessage);

            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        bool hasSchedule = msgList.Any(m => (m.Parts ?? Enumerable.Empty<string>()).Any(p => p != null && (
            p.TrimStart().StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase) ||
            p.TrimStart().StartsWith("I've selected this schedule:", StringComparison.OrdinalIgnoreCase))));
        if (!hasSchedule)
        {
            var sessionStart = HttpContext.Session.GetString("Draft:StartTime");
            var sessionDateConfirmed = HttpContext.Session.GetString("Draft:DateConfirmed");
            if (!string.IsNullOrWhiteSpace(sessionStart) || sessionDateConfirmed == "1")
                hasSchedule = true;
        }
        if (!hasSchedule)
        {
            _logger.LogWarning("[FollowUpAv] Quote blocked - schedule not yet submitted");
            var scheduleRequestMessage = new DisplayMessage
            {
                Role = "assistant",
                Timestamp = DateTimeOffset.UtcNow,
                Parts = new List<string> { "I need your event schedule before I can create the quote. Please confirm your setup, start, and end times using the time picker." },
                FullText = "I need your event schedule before I can create the quote. Please confirm your setup, start, and end times using the time picker.",
                Html = "<p>I need your event schedule before I can create the quote. Please confirm your setup, start, and end times using the time picker.</p>"
            };
            msgList.Add(scheduleRequestMessage);
            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        var draftVenueName = HttpContext.Session.GetString("Draft:VenueName");
        var draftRoomName = HttpContext.Session.GetString("Draft:RoomName");
        var draftProjectorAreas = ParseProjectorAreas(HttpContext.Session.GetString("Draft:ProjectorAreas"));
        if (draftProjectorAreas.Count == 0)
            draftProjectorAreas = ParseProjectorAreas(HttpContext.Session.GetString("Draft:ProjectorArea"));
        // Recovery: Draft:ProjectorAreas may be lost between requests; re-parse from placement choice
        if (draftProjectorAreas.Count == 0
            && string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal))
        {
            var pc = HttpContext.Session.GetString("Draft:ProjectorPlacementChoice");
            if (!string.IsNullOrEmpty(pc) && !string.Equals(pc, "none", StringComparison.OrdinalIgnoreCase))
            {
                draftProjectorAreas = ParseProjectorPlacementToAllowedAreas(pc, draftRoomName);
                if (draftProjectorAreas.Count > 0)
                {
                    HttpContext.Session.SetString("Draft:ProjectorAreas", string.Join(",", draftProjectorAreas));
                    HttpContext.Session.SetString("Draft:ProjectorArea", draftProjectorAreas[0]);
                    _logger.LogInformation("[PROJECTOR_AREA_RECOVERY] Recovered areas [{Areas}] from PlacementChoice={Choice} (FollowUpAv)",
                        string.Join(",", draftProjectorAreas), pc);
                }
            }
        }
        var summaryReqJson = HttpContext.Session.GetString("Draft:SummaryEquipmentRequests") ?? string.Empty;
        var selectedEquipmentJsonForArea = HttpContext.Session.GetString("Draft:SelectedEquipment") ?? string.Empty;

        var isWestinBallroomFamily = IsDraftWestinBallroomFamily(draftVenueName, draftRoomName);

        var projectionNeeded =
            summaryReqJson.Contains("projector", StringComparison.OrdinalIgnoreCase) ||
            summaryReqJson.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
            summaryReqJson.Contains("display", StringComparison.OrdinalIgnoreCase) ||
            selectedEquipmentJsonForArea.Contains("projector", StringComparison.OrdinalIgnoreCase) ||
            selectedEquipmentJsonForArea.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
            selectedEquipmentJsonForArea.Contains("display", StringComparison.OrdinalIgnoreCase);

        if (!projectionNeeded)
        {
            HttpContext.Session.Remove("Draft:ProjectorArea");
            HttpContext.Session.Remove("Draft:ProjectorAreas");
        }

        var projectorCount = GetRequestedProjectorCount(summaryReqJson);
        if (projectorCount <= 0 && selectedEquipmentJsonForArea.Contains("projector", StringComparison.OrdinalIgnoreCase))
            projectorCount = 1;
        var requiredAreaCount = 0;
        if (projectionNeeded)
        {
            if (isWestinBallroomFamily)
                requiredAreaCount = Math.Max(IsFullWestinBallroomRoomName(draftRoomName) ? 2 : 1,
                    projectorCount > 1 ? Math.Min(projectorCount, 3) : 1);
            else
                requiredAreaCount = projectorCount > 1 ? Math.Min(projectorCount, 3) : 1;
        }
        var allowedAreas = GetAllowedProjectorAreasForRoom(draftRoomName);
        var validSelectedAreas = draftProjectorAreas
            .Where(a => allowedAreas.Contains(a, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var promptAlreadyShown =
            HttpContext.Session.GetString(ProjectorPromptShownKey) == "1" &&
            HttpContext.Session.GetString(ProjectorPromptThreadIdKey) == threadId;
        var areaAlreadyCapturedInSession =
            HttpContext.Session.GetString(ProjectorAreaCapturedKey) == "1" &&
            HttpContext.Session.GetString(ProjectorAreaThreadIdKey) == threadId;

        // Base AV wizard submission is authoritative — skip the stale-session guard entirely.
        var baseAvSubmittedForQuote = string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal)
            && validSelectedAreas.Count > 0;

        if (!baseAvSubmittedForQuote && !promptAlreadyShown && !areaAlreadyCapturedInSession)
        {
            SyncProjectorPromptMarkers(msgList, threadId);
            promptAlreadyShown = HttpContext.Session.GetString(ProjectorPromptShownKey) == "1" &&
                                HttpContext.Session.GetString(ProjectorPromptThreadIdKey) == threadId;
        }

        if (!baseAvSubmittedForQuote && !promptAlreadyShown && !areaAlreadyCapturedInSession)
            validSelectedAreas.Clear();

        var allowSingleBaseAvFullBallroomFv = isWestinBallroomFamily
            && projectionNeeded
            && requiredAreaCount > 1
            && validSelectedAreas.Count == 1
            && IsFullWestinBallroomRoomName(draftRoomName)
            && string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal);

        if (isWestinBallroomFamily && projectionNeeded && validSelectedAreas.Count < requiredAreaCount && !allowSingleBaseAvFullBallroomFv)
        {
            _logger.LogWarning("[FollowUpAv] Quote blocked - missing projector area (Westin Ballroom with projection)");
            var allowedText = string.Join(", ", allowedAreas);
            var areaRequestMessage = new DisplayMessage
            {
                Role = "assistant",
                Timestamp = DateTimeOffset.UtcNow,
                Parts = new List<string>
                {
                    requiredAreaCount == 1
                        ? $"Before I create the quote, please choose the **projector placement area** for Westin Ballroom.\n\nValid areas for this room: **{allowedText}**.\n\nReply with one area.\n\n![Westin Ballroom projector placement areas](/images/westin/westin-ballroom/floor-plan.png)"
                        : $"Before I create the quote, please choose **{requiredAreaCount} projector placement areas** for Westin Ballroom.\n\nValid areas for this room: **{allowedText}**.\n\nReply with any two areas (e.g. `{allowedAreas[0]} & {allowedAreas[1]}`).\n\n![Westin Ballroom projector placement areas](/images/westin/westin-ballroom/floor-plan.png)"
                },
                FullText = "Before I create the quote, please choose projector placement area A-F for Westin Ballroom.",
                Html = "<p>Before I create the quote, please choose the <strong>projector placement area</strong> for Westin Ballroom (<strong>A-F</strong>).</p>"
            };
            HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
            HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
            await AddAssistantMessageAndPersistAsync(msgList, areaRequestMessage, ct);
            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        var existingBookingNo = HttpContext.Session.GetString("Draft:BookingNo");

        if (!string.IsNullOrWhiteSpace(existingBookingNo))
        {
            _logger.LogInformation("[FollowUpAv] Quote generation for existing booking {BookingNo}", existingBookingNo);
            await GenerateQuoteForBookingAsync(existingBookingNo, msgList, ct);
            HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey ?? string.Empty);
        }
        else
        {
            _logger.LogInformation("[FollowUpAv] No existing booking — creating booking then quote");

            var additionalFacts = new Dictionary<string, string>();
            var selectedEquipment = HttpContext.Session.GetString("Draft:SelectedEquipment");
            if (!string.IsNullOrWhiteSpace(selectedEquipment))
            {
                additionalFacts["selected_equipment"] = selectedEquipment;
                _logger.LogInformation("[FollowUpAv] Adding stored equipment to booking: {Equipment}", selectedEquipment);
            }
            var selectedLabor = HttpContext.Session.GetString("Draft:SelectedLabor");
            if (!string.IsNullOrWhiteSpace(selectedLabor))
            {
                additionalFacts["selected_labor"] = selectedLabor;
                var laborSummary = TryBuildLaborSummaryFromSelectedLabor(selectedLabor);
                if (!string.IsNullOrWhiteSpace(laborSummary))
                    additionalFacts["labor_summary"] = laborSummary;
            }
            var totalDayRate = HttpContext.Session.GetString("Draft:TotalDayRate");
            if (!string.IsNullOrWhiteSpace(totalDayRate))
                additionalFacts["price_quoted"] = totalDayRate;
            var eventType = HttpContext.Session.GetString("Draft:EventType");
            if (!string.IsNullOrWhiteSpace(eventType))
                additionalFacts["event_type"] = eventType;
            if (projectionNeeded)
            {
                var projectorAreaValue = HttpContext.Session.GetString("Draft:ProjectorArea");
                if (!string.IsNullOrWhiteSpace(projectorAreaValue))
                    additionalFacts["projector_area"] = projectorAreaValue;
                var projectorAreasValue = HttpContext.Session.GetString("Draft:ProjectorAreas");
                if (!string.IsNullOrWhiteSpace(projectorAreasValue))
                    additionalFacts["projector_areas"] = projectorAreasValue;
            }
            var venueName = HttpContext.Session.GetString("Draft:VenueName");
            if (!string.IsNullOrWhiteSpace(venueName))
                additionalFacts["venue_name"] = venueName;
            var roomName = HttpContext.Session.GetString("Draft:RoomName");
            if (!string.IsNullOrWhiteSpace(roomName))
                additionalFacts["venue_room"] = roomName;
            var expectedAttendeesVal = HttpContext.Session.GetString("Draft:ExpectedAttendees") ?? HttpContext.Session.GetString("Ack:Attendees");
            if (!string.IsNullOrWhiteSpace(expectedAttendeesVal))
                additionalFacts["expected_attendees"] = expectedAttendeesVal;

            var result = await _orchestration.ProcessConversationAsync(msgList, existingBookingNo, ct, additionalFacts);

            if (result.Success && !string.IsNullOrWhiteSpace(result.BookingNo))
            {
                HttpContext.Session.SetString("Draft:BookingNo", result.BookingNo!);
                if (result.ContactId.HasValue)
                    HttpContext.Session.SetString("Draft:ContactId", result.ContactId.Value.ToString());
                if (!string.IsNullOrWhiteSpace(result.CustomerCode))
                    HttpContext.Session.SetString("Draft:CustomerCode", result.CustomerCode!);

                HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey ?? string.Empty);
                SetConsent(HttpContext.Session, false);
                HttpContext.Session.SetString("Draft:ShowedBookingNo", "1");

                await GenerateQuoteForBookingAsync(result.BookingNo!, msgList, ct);
            }
            else if (result.Errors.Any())
            {
                _logger.LogError("[FollowUpAv] Booking creation failed: {Errors}", string.Join("; ", result.Errors));

                if (!string.IsNullOrWhiteSpace(existingBookingNo))
                {
                    await GenerateQuoteForBookingAsync(existingBookingNo, msgList, ct);
                }
                else
                {
                    var errorMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> {
                            "Our team will follow up with you to help complete your booking."
                        },
                        FullText = "Our team will follow up with you to help complete your booking.",
                        Html = "<p>Our team will follow up with you to help complete your booking.</p>"
                    };
                    msgList = msgList.Concat(new[] { errorMessage }).ToList();
                }
            }
        }

        // Return a PartialView with the locally-modified msgList so the quote card
        // (added by GenerateQuoteForBookingAsync) is guaranteed to be in the response.
        // Returning null previously caused the caller to re-fetch the transcript from Azure,
        // which could miss the card if persistence lagged or failed silently.
        EnsureQuoteReadyCardInMessages(msgList);
        RedactPricesForUiInPlace(msgList);
        ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
        ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
        SetScheduleTimesInViewData();
        ViewData["ProgressStep"] = DetermineProgressStep(msgList);
        return PartialView("_Messages", msgList);
    }

    private async Task GenerateQuoteForBookingAsync(string bookingNo, List<DisplayMessage> msgList, CancellationToken ct)
    {
        try
        {
            var venueOrRoomChanged = await _bookingPersistence.SyncVenueAndRoomForBookingAsync(bookingNo, HttpContext.Session, ct);
            if (venueOrRoomChanged)
            {
                HttpContext.Session.Remove("Draft:QuoteUrl");
                HttpContext.Session.Remove("Draft:QuoteComplete");
                HttpContext.Session.Remove("Draft:QuoteTimestamp");
            }

            try
            {
                var (contactOrOrgChanged, renamedBookingNo) = await _bookingPersistence.SyncContactAndOrganisationForBookingAsync(bookingNo, HttpContext.Session, ct);
                if (!string.IsNullOrWhiteSpace(renamedBookingNo))
                {
                    bookingNo = renamedBookingNo;
                    HttpContext.Session.SetString("Draft:BookingNo", bookingNo);
                }
                if (contactOrOrgChanged)
                {
                    HttpContext.Session.Remove("Draft:QuoteUrl");
                    HttpContext.Session.Remove("Draft:QuoteComplete");
                    HttpContext.Session.Remove("Draft:QuoteTimestamp");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync session contact/organisation to booking {BookingNo} before quote generation", bookingNo);
            }

            try
            {
                var eventDetailsChanged = await _bookingPersistence.SyncEventDetailsForBookingAsync(bookingNo, HttpContext.Session, ct);
                if (eventDetailsChanged)
                {
                    HttpContext.Session.Remove("Draft:QuoteUrl");
                    HttpContext.Session.Remove("Draft:QuoteComplete");
                    HttpContext.Session.Remove("Draft:QuoteTimestamp");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync session event details (schedule/times) to booking {BookingNo} before quote generation", bookingNo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync session venue/room to booking {BookingNo} before quote generation", bookingNo);
        }

        // ========== IDEMPOTENCY CHECK - Return existing quote if already generated ==========
        var existingQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
        var quoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";
        
        if (quoteComplete && !string.IsNullOrWhiteSpace(existingQuoteUrl))
        {
            _logger.LogInformation("Quote already exists for booking {BookingNo}, returning existing quote: {QuoteUrl}", bookingNo, existingQuoteUrl);
            
            // Check if any assistant message already announced quote success for this booking.
            var quoteMessageAlreadyExists = msgList
                .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                .Any(m =>
                {
                    var raw = m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
                    if (string.IsNullOrWhiteSpace(raw)) return false;
                    return raw.Contains("successfully generated your quote", StringComparison.OrdinalIgnoreCase)
                        && raw.Contains(bookingNo, StringComparison.OrdinalIgnoreCase);
                });
            if (quoteMessageAlreadyExists)
            {
                _logger.LogInformation("Quote link already in last message, skipping duplicate");
                EnsureQuoteReadyCardInMessages(msgList);
                MarkQuoteReviewPromptPending();
                return;
            }
            
            // Add success message with existing quote
            var existingQuoteMessage = BuildQuoteReadyMessage(bookingNo, existingQuoteUrl);
            await AddAssistantMessageAndPersistAsync(msgList, existingQuoteMessage, ct);
            AppendQuoteReviewPromptImmediately(msgList);
            return;
        }
        
        // Disk fallback is for session-expiry resilience only.
        // If session still has equipment data, QuoteComplete was deliberately cleared
        // (e.g. Edit Quote) — skip the fallback and regenerate fresh.
        var sessionHasEquipment = !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:SelectedEquipment"));
        var quotesDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
        if (!sessionHasEquipment && Directory.Exists(quotesDir))
        {
            var existingQuoteFiles = Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                .ToList();

            if (existingQuoteFiles.Any())
            {
                var mostRecentQuote = existingQuoteFiles.First();
                var quoteAge = DateTime.UtcNow - System.IO.File.GetCreationTimeUtc(mostRecentQuote);

                // If quote is less than 1 hour old, reuse it
                if (quoteAge.TotalHours < 1)
                {
                    var reusedQuoteUrl = $"/files/quotes/{Path.GetFileName(mostRecentQuote)}";

                    // Update session with existing quote
                    HttpContext.Session.SetString("Draft:QuoteUrl", reusedQuoteUrl);
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1");
                    HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));

                    _logger.LogInformation("Found existing quote file for booking {BookingNo} (age: {Age}), reusing: {QuoteUrl}",
                        bookingNo, quoteAge, reusedQuoteUrl);

                    // Add success message with existing quote
                    var reusedQuoteMessage = BuildQuoteReadyMessage(bookingNo, reusedQuoteUrl);
                    await AddAssistantMessageAndPersistAsync(msgList, reusedQuoteMessage, ct);
                    AppendQuoteReviewPromptImmediately(msgList);
                    return;
                }
            }
        }
        
        // ========== SYNC SESSION EQUIPMENT TO BOOKING BEFORE QUOTE ==========
        var selectedEquipmentJson = HttpContext.Session.GetString("Draft:SelectedEquipment");
        if (!string.IsNullOrWhiteSpace(selectedEquipmentJson))
        {
            try
            {
                await _itemPersistence.UpsertSelectedEquipmentAsync(bookingNo, selectedEquipmentJson, ct);
                _logger.LogInformation("Synced session equipment to booking {BookingNo} before quote generation", bookingNo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync session equipment to booking {BookingNo} before quote; continuing with existing booking items", bookingNo);
            }
        }

        try
        {
            await _bookingPersistence.SyncLaborFromSessionForBookingAsync(bookingNo, HttpContext.Session, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync session labor to booking {BookingNo} before quote generation", bookingNo);
        }
        
        // ========== GENERATE QUOTE HTML ==========
        try
        {
            _logger.LogInformation("Starting HTML quote generation for booking {BookingNo}", bookingNo);
            // HTML quote generation can exceed 30s on cold start / remote DB; align with chat client timeout.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            try
            {
                var (quoteSuccess, quoteUrl, quoteError) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(bookingNo, linkedCts.Token, HttpContext.Session);

                // Retry once on failure — the booking may have just been created and DB propagation can lag.
                if (!quoteSuccess || string.IsNullOrWhiteSpace(quoteUrl))
                {
                    _logger.LogWarning("HTML quote generation failed for booking {BookingNo} (first attempt): {Error}. Retrying after 2s...", bookingNo, quoteError);
                    await Task.Delay(2000, linkedCts.Token);
                    (quoteSuccess, quoteUrl, quoteError) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(bookingNo, linkedCts.Token, HttpContext.Session);
                }
                _logger.LogInformation("HTML quote generation completed for booking {BookingNo}, success: {Success}", bookingNo, quoteSuccess);

                if (quoteSuccess && !string.IsNullOrWhiteSpace(quoteUrl))
                {
                    // ========== SYNC QUOTE STATE ==========
                    HttpContext.Session.SetString("Draft:QuoteUrl", quoteUrl);
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1"); // Mark quote as complete
                    HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
                    _logger.LogInformation("HTML quote generated for booking {BookingNo}: {QuoteUrl}. State synchronized.", bookingNo, quoteUrl);

                    // Check if the messages list contains confusing quote language - if so, remove them
                    var confusingMessages = msgList.Where(m => m.Role == "assistant" && ContainsConfusingQuoteLanguage(new[] { m })).ToList();
                    foreach (var cm in confusingMessages)
                    {
                        _logger.LogInformation("Removing confusing AI message: {Text}", cm.FullText);
                        msgList.Remove(cm);
                    }

                    // Add success message directly to the conversation (bypass AI agent)
                    var quoteMessage = BuildQuoteReadyMessage(bookingNo, quoteUrl);
                    await AddAssistantMessageAndPersistAsync(msgList, quoteMessage, ct);
                    AppendQuoteReviewPromptImmediately(msgList);
                    _logger.LogInformation("Success message added to msgList. Total messages: {Count}. Quote URL: {QuoteUrl}", msgList.Count, quoteUrl);
                }
                else
                {
                    _logger.LogWarning("HTML quote generation failed for booking {BookingNo}: {Error}", bookingNo, quoteError);

                    // Before we show an error, double-check whether a quote actually exists
                    var verifyQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
                    var verifyQuoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";

                    if (verifyQuoteComplete && !string.IsNullOrWhiteSpace(verifyQuoteUrl))
                    {
                        _logger.LogWarning("Quote generation reported failure for booking {BookingNo}, but quote state indicates success. Using existing quote instead of showing error.", bookingNo);

                        // Avoid duplicating an existing success message with the same URL
                        var lastAssistant = msgList.LastOrDefault(m => m.Role == "assistant");
                        if (lastAssistant == null || lastAssistant.FullText == null || !lastAssistant.FullText.Contains(verifyQuoteUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            await AddAssistantMessageAndPersistAsync(msgList, BuildQuoteReadyMessage(bookingNo, verifyQuoteUrl), ct);
                            AppendQuoteReviewPromptImmediately(msgList);
                        }
                    }
                    else
                    {
                        // As a secondary safety net, check for a recently-created quote file on disk
                        var verifyQuoteFile = Directory.Exists(quotesDir)
                            ? Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                                .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                                .FirstOrDefault()
                            : null;

                        if (!string.IsNullOrEmpty(verifyQuoteFile))
                        {
                            var recoveredUrl = $"/files/quotes/{Path.GetFileName(verifyQuoteFile)}";

                            HttpContext.Session.SetString("Draft:QuoteUrl", recoveredUrl);
                            HttpContext.Session.SetString("Draft:QuoteComplete", "1");
                            HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));

                            _logger.LogInformation("Recovered quote for booking {BookingNo} from disk despite reported failure: {QuoteUrl}", bookingNo, recoveredUrl);

                            await AddAssistantMessageAndPersistAsync(msgList, BuildQuoteReadyMessage(bookingNo, recoveredUrl), ct);
                            AppendQuoteReviewPromptImmediately(msgList);
                        }
                        else
                        {
                            // Never surface an internal quote-generation error to the user.
                            // Keep the experience forward-moving while backend retries/recovery can happen.
                            var failureMessage = new DisplayMessage
                            {
                                Role = "assistant",
                                Timestamp = DateTimeOffset.UtcNow,
                                Parts = new List<string> {
                                    $"Your quote for booking {bookingNo} is being finalized now. Please wait a moment and refresh, and I will share the live quote link as soon as it is ready."
                                },
                                FullText = $"Your quote for booking {bookingNo} is being finalized now. Please wait a moment and refresh, and I will share the live quote link as soon as it is ready.",
                                Html = $"<p>Your quote for booking <strong>{bookingNo}</strong> is being finalized now. Please wait a moment and refresh, and I will share the live quote link as soon as it is ready.</p>"
                            };
                            await AddAssistantMessageAndPersistAsync(msgList, failureMessage, ct);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("HTML quote generation timed out for booking {BookingNo}", bookingNo);
                await RecoverQuoteFromDiskOrAnnounceWaitAsync(msgList, bookingNo, quotesDir, ct, "QuoteGenTimeout");
            }
            catch (OperationCanceledException oce)
            {
                // Client/gateway disconnected or request aborted while HTML/PDF still running — not the in-app 5-minute timeout.
                _logger.LogWarning(oce, "HTML quote generation cancelled (external) for booking {BookingNo}", bookingNo);
                await RecoverQuoteFromDiskOrAnnounceWaitAsync(msgList, bookingNo, quotesDir, ct, "QuoteGenCancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during quote generation for booking {BookingNo}", bookingNo);
            try
            {
                await RecoverQuoteFromDiskOrAnnounceWaitAsync(msgList, bookingNo, quotesDir, ct, "QuoteGenException");
            }
            catch (Exception persistEx)
            {
                _logger.LogError(persistEx, "Failed to persist quote-interrupted assistant message for booking {BookingNo}", bookingNo);
            }
        }

        // Final verification: if a quote exists, strip out any lingering error messages
        try
        {
            var finalQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            var finalQuoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";

            if (finalQuoteComplete && !string.IsNullOrWhiteSpace(finalQuoteUrl))
            {
                // Remove any assistant messages that look like quote-generation error apologies
                msgList.RemoveAll(m =>
                {
                    if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                        return false;

                    var text = (m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>())).ToLowerInvariant();

                    // Keep clear success messages
                    if (text.Contains("great news!") && text.Contains("generated your quote"))
                        return false;

                    return text.Contains("issue generating your quote") ||
                           text.Contains("issue with generating your quote") ||
                           (text.Contains("there was an issue") && text.Contains("your quote")) ||
                           (text.Contains("our team will follow up") && text.Contains("quote")) ||
                           text.Contains("couldn't create the quote") ||
                           text.Contains("couldn't create the quote automatically") ||
                           text.Contains("team will follow up with you") ||
                           text.Contains("follow up with you soon");
                });
            }
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx, "Failed to clean up confusing quote error messages after quote generation for booking {BookingNo}", bookingNo);
        }
    }


    private static bool TryCaptureTimeSelection(string text, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;

        var m = ChooseTimeRe.Match(text);
        if (!m.Success) return false;

        if (TimeSpan.TryParse(m.Groups[1].Value, out start) &&
            TimeSpan.TryParse(m.Groups[2].Value, out end))
        {
            return true;
        }
        return false;
    }

    private static bool TryCaptureProjectorAreaSelections(string text, out List<string> areas, bool inProjectorContext = false)
    {
        areas = ParseProjectorAreasFromUserText(text, inProjectorContext);
        return areas.Count > 0;
    }

    /// <summary>
    /// Parses projector area identifiers from stored session values (e.g. "A" or "A,D").
    /// Session values are written by us, so a simple split on known delimiters is safe.
    /// </summary>
    private static List<string> ParseProjectorAreas(string? sessionValue)
    {
        if (string.IsNullOrWhiteSpace(sessionValue)) return new List<string>();
        return sessionValue
            .Split(new[] { ',', ';', '&', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToUpperInvariant())
            .Where(p => p.Length == 1 && p[0] >= 'A' && p[0] <= 'F')
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Parses projector area identifiers from free-form user text.
    /// When <paramref name="inProjectorContext"/> is true (prompt was already shown), bare
    /// single-letter and short multi-letter replies are accepted. Otherwise only explicit
    /// "area X" / "projector area X" tokens are matched to avoid false positives.
    /// </summary>
    private static List<string> ParseProjectorAreasFromUserText(string? text, bool inProjectorContext = false)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var trimmed = text.Trim();

        if (inProjectorContext)
        {
            // User is responding to the projector prompt — accept bare A-F letters
            foreach (Match m in Regex.Matches(trimmed, @"\b([A-F])\b", RegexOptions.IgnoreCase))
            {
                var area = m.Groups[1].Value.ToUpperInvariant();
                if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                    result.Add(area);
            }
        }
        else
        {
            // No prompt context — require explicit "area" or "projector area" prefix to avoid
            // capturing incidental A-F letters from unrelated messages.
            foreach (Match m in Regex.Matches(trimmed, @"\b(?:projector\s+area|area)\s*[:\-]?\s*([A-F])\b", RegexOptions.IgnoreCase))
            {
                var area = m.Groups[1].Value.ToUpperInvariant();
                if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                    result.Add(area);
            }
        }

        return result;
    }

    private static int GetRequestedProjectorCount(string summaryRequestsJson)
    {
        if (string.IsNullOrWhiteSpace(summaryRequestsJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(summaryRequestsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;
            var total = 0;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.TryGetProperty("EquipmentType", out var t1) ? t1.GetString()
                    : item.TryGetProperty("equipment_type", out var t2) ? t2.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(type) || !type.Contains("projector", StringComparison.OrdinalIgnoreCase))
                    continue;
                var qty = item.TryGetProperty("Quantity", out var q1) && q1.ValueKind == JsonValueKind.Number ? q1.GetInt32()
                    : item.TryGetProperty("quantity", out var q2) && q2.ValueKind == JsonValueKind.Number ? q2.GetInt32()
                    : 1;
                total += Math.Max(1, qty);
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static List<string> GetAllowedProjectorAreasForRoom(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        if (room.Contains("ballroom 1", StringComparison.Ordinal) || room.Contains("ballroom-1", StringComparison.Ordinal))
            return new List<string> { "E", "D", "C" };
        if (room.Contains("ballroom 2", StringComparison.Ordinal) || room.Contains("ballroom-2", StringComparison.Ordinal))
            return new List<string> { "A", "F", "B" };
        return new List<string> { "A", "B", "C", "D", "E", "F" };
    }

    /// <summary>Matches <see cref="AgentToolHandlerService"/> Westin Ballroom family for quote/projector guards.</summary>
    private static bool IsDraftWestinBallroomFamily(string? draftVenueName, string? draftRoomName)
    {
        if (string.IsNullOrWhiteSpace(draftVenueName)
            || !draftVenueName.Contains("westin", StringComparison.OrdinalIgnoreCase)
            || !draftVenueName.Contains("brisbane", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(draftRoomName)) return false;
        return string.Equals(draftRoomName, "Westin Ballroom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Westin Ballroom 1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Westin Ballroom 2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Ballroom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Ballroom 1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Ballroom 2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFullWestinBallroomRoomName(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        return room is "full westin ballroom" or "westin ballroom full" or "full ballroom";
    }

    private void TryPersistProjectorPlacementFromBaseAv(string threadId, BaseAvFormSubmission baseAv)
    {
        var venue = HttpContext.Session.GetString("Draft:VenueName");
        var room = HttpContext.Session.GetString("Draft:RoomName");
        if (!IsDraftWestinBallroomFamily(venue, room)) return;

        var areas = ParseProjectorPlacementToAllowedAreas(baseAv.ProjectorPlacement, room);
        if (areas.Count == 0) return;

        PersistProjectorAreaSelection(threadId, areas);
    }


    private async Task AddAssistantMessageAndPersistAsync(List<DisplayMessage> msgList, DisplayMessage message, CancellationToken ct)
    {
        msgList.Add(message);

        try
        {
            var persistedText = (message.Parts != null && message.Parts.Count > 0)
                ? string.Join("\n\n", message.Parts.Where(p => !string.IsNullOrWhiteSpace(p)))
                : (message.FullText ?? string.Empty);
            await _chat.AppendAssistantMessageAsync(HttpContext.Session, persistedText, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist assistant message to thread.");
        }
    }

    /// <summary>
    /// After timeout, client disconnect, or unexpected errors, either attach a success message if a quote file exists
    /// or a "wait and refresh" assistant line. Ensures SendPartial sees an assistant delta so we do not append the generic
    /// "temporary issue" fallback on top of a quote attempt.
    /// </summary>
    private async Task RecoverQuoteFromDiskOrAnnounceWaitAsync(
        List<DisplayMessage> msgList,
        string bookingNo,
        string quotesDir,
        CancellationToken ct,
        string logContext)
    {
        var quoteFile = Directory.Exists(quotesDir)
            ? Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                .FirstOrDefault()
            : null;

        if (!string.IsNullOrEmpty(quoteFile))
        {
            var quoteUrl = $"/files/quotes/{Path.GetFileName(quoteFile)}";
            HttpContext.Session.SetString("Draft:QuoteUrl", quoteUrl);
            HttpContext.Session.SetString("Draft:QuoteComplete", "1");
            HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
            _logger.LogInformation("[{LogContext}] Recovered quote on disk for booking {BookingNo}: {QuoteUrl}", logContext, bookingNo, quoteUrl);
            await AddAssistantMessageAndPersistAsync(msgList, BuildQuoteReadyMessage(bookingNo, quoteUrl), ct);
            AppendQuoteReviewPromptImmediately(msgList);
        }
        else
        {
            _logger.LogWarning("[{LogContext}] No quote file on disk yet for booking {BookingNo}", logContext, bookingNo);
            var waitMessage = new DisplayMessage
            {
                Role = "assistant",
                Timestamp = DateTimeOffset.UtcNow,
                Parts = new List<string> {
                    $"Your quote for booking {bookingNo} is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance."
                },
                FullText = $"Your quote for booking {bookingNo} is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance.",
                Html = $"<p>Your quote for booking <strong>{bookingNo}</strong> is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance.</p>"
            };
            await AddAssistantMessageAndPersistAsync(msgList, waitMessage, ct);
        }
    }

    private static int CountAssistantMessages(IEnumerable<DisplayMessage> messages)
        => messages.Count(m =>
            string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Role, "agent", StringComparison.OrdinalIgnoreCase));

    private static bool HasAssistantDelta(int assistantCountBefore, IEnumerable<DisplayMessage> afterMessages)
        => CountAssistantMessages(afterMessages) > assistantCountBefore;

    private static DisplayMessage BuildTransientFailureFallbackMessage()
    {
        const string fallbackText = "Sorry, I hit a temporary issue and could not complete that step. Please send your last message again and I will continue.";
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { fallbackText },
            FullText = fallbackText,
            Html = $"<p>{fallbackText}</p>"
        };
    }

    private static string? TryBuildLaborSummaryFromSelectedLabor(string selectedLaborJson)
    {
        if (string.IsNullOrWhiteSpace(selectedLaborJson))
        {
            return null;
        }

        try
        {
            var laborItems = JsonSerializer.Deserialize<List<SelectedLaborItem>>(
                selectedLaborJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (laborItems == null || laborItems.Count == 0)
            {
                return null;
            }

            return string.Join(
                "\n",
                laborItems.Select(i =>
                {
                    var durationHours = i.Hours + (i.Minutes / 60.0);
                    if (durationHours <= 0) durationHours = 1;
                    var task = string.IsNullOrWhiteSpace(i.Task) ? "Operate" : i.Task;
                    var quantity = i.Quantity <= 0 ? 1 : i.Quantity;
                    return $"{quantity}x {task} @ {durationHours:0.##} hours";
                }));
        }
        catch
        {
            return null;
        }
    }

    private void MarkQuoteReviewPromptPending()
    {
        HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "1");
        HttpContext.Session.SetString(QuoteReviewPromptShownKey, "0");
        HttpContext.Session.SetString("Draft:QuoteAccepted", "0");
    }

    private void AppendQuoteReviewPromptImmediately(List<DisplayMessage> msgList)
    {
        if (msgList.Count > 0 && WasLastAssistantAQuoteAcceptanceAsk(msgList))
        {
            return;
        }

        msgList.Add(BuildPostQuoteReviewPromptMessage());
        HttpContext.Session.SetString("Draft:QuoteAccepted", "0");
        HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "0");
        HttpContext.Session.SetString(QuoteReviewPromptShownKey, "1");
    }

    private void EnsureImmediateQuoteReviewPromptAfterQuoteSuccess(List<DisplayMessage> msgList)
    {
        if (HttpContext.Session.GetString("Draft:QuoteAccepted") == "1")
        {
            return;
        }

        if (msgList.Count == 0 || WasLastAssistantAQuoteAcceptanceAsk(msgList))
        {
            return;
        }

        var lastAssistant = msgList.LastOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        if (lastAssistant == null)
        {
            return;
        }

        var text = (lastAssistant.FullText ?? string.Join("\n\n", lastAssistant.Parts ?? Enumerable.Empty<string>()))
            .ToLowerInvariant();

        if (text.Contains("generated your quote") && text.Contains("booking"))
        {
            AppendQuoteReviewPromptImmediately(msgList);
        }
    }

    /// <summary>
    /// When the AI agent tool loop generates a quote, its text response lacks the {"ui":...} JSON
    /// that _Messages.cshtml needs to render the Quote Ready card. This method finds such messages
    /// and replaces them with the proper BuildQuoteReadyMessage that includes the UI card JSON.
    /// </summary>
    private void EnsureQuoteReadyCardInMessages(List<DisplayMessage> messages)
    {
        try
        {
            if (messages == null || messages.Count == 0)
                return;

            var quoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";
            if (!quoteComplete)
                return;

            var quoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");

            if (string.IsNullOrWhiteSpace(quoteUrl) || string.IsNullOrWhiteSpace(bookingNo))
                return;

            var foundQuoteCard = false;

            for (int i = 0; i < messages.Count; i++)
            {
                var m = messages[i];
                if (m == null || !IsAssistantMessageRole(m.Role))
                    continue;

                var raw = m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var rawLower = raw.ToLowerInvariant();
                var isQuoteSuccessText = rawLower.Contains("successfully generated your quote")
                    && rawLower.Contains(bookingNo.ToLowerInvariant());
                var isQuotePendingText = rawLower.Contains("being finali")
                    && rawLower.Contains(bookingNo.ToLowerInvariant());

                if (!isQuoteSuccessText && !isQuotePendingText)
                    continue;

                // Already has the UI card JSON — leave it alone
                var partsRaw = string.Join("\n", m.Parts ?? Enumerable.Empty<string>());
                if (partsRaw.Contains("\"quoteUrl\"", StringComparison.OrdinalIgnoreCase))
                {
                    foundQuoteCard = true;
                    continue;
                }

                // Replace plain text / pending text with proper card message
                _logger.LogInformation("[QUOTE CARD] Replacing plain-text quote message with Quote Ready card for booking {BookingNo}", bookingNo);
                messages[i] = BuildQuoteReadyMessage(bookingNo, quoteUrl);
                foundQuoteCard = true;
            }

            // If quote is complete but no card message exists at all (e.g. persistence lag/failure
            // after GenerateQuoteForBookingAsync), inject one so the user sees the card.
            if (!foundQuoteCard)
            {
                _logger.LogInformation("[QUOTE CARD] No quote card message found in transcript — injecting Quote Ready card for booking {BookingNo}", bookingNo);
                messages.Add(BuildQuoteReadyMessage(bookingNo, quoteUrl));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[QUOTE CARD] EnsureQuoteReadyCardInMessages failed (non-fatal)");
        }
    }

    private static DisplayMessage BuildPostQuoteReviewPromptMessage()
    {
        const string promptText = "Would you like to proceed and accept this quote?\n\nSelecting \"Accept Quote\" will notify Microhire to finalize the details.";
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { promptText },
            FullText = promptText,
            Html = $"<p>{promptText}</p>"
        };
    }

    private static DisplayMessage BuildQuoteReadyMessage(string bookingNo, string quoteUrl)
    {
        var text = $"Great news! I have successfully generated your quote for booking {bookingNo}.";
        var uiJson = JsonSerializer.Serialize(new
        {
            ui = new { quoteUrl, bookingNo, isHtml = true }
        });
        var combined = text + "\n\n" + uiJson;
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { combined },
            FullText = text,
            Html = text
        };
    }

    private static string BuildSignedQuoteActionsHtml(string quoteUrl, string bookingNo)
    {
        var safeUrl = System.Net.WebUtility.HtmlEncode(quoteUrl ?? "");
        var safeRef = System.Net.WebUtility.HtmlEncode(bookingNo ?? "");
        var refHtml = string.IsNullOrWhiteSpace(bookingNo)
            ? ""
            : $"<p class=\"isla-quote-ref\">Quote Number: {safeRef}</p>";

        // Matches the Quote Ready card in _Messages.cshtml (isla-quote-ready + icon + CTA with external-link SVG).
        return $"""
<div class="isla-quote-ready">
<div class="isla-quote-icon">
<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
<polyline points="14 2 14 8 20 8"></polyline>
<line x1="16" y1="13" x2="8" y2="13"></line>
<line x1="16" y1="17" x2="8" y2="17"></line>
<polyline points="10 9 9 9 8 9"></polyline>
</svg>
</div>
<div class="isla-quote-title">Signed Quote Ready!</div>
<p class="isla-quote-message">Your signed quote is ready to view.</p>
{refHtml}
<a href="{safeUrl}" target="_blank" rel="noopener noreferrer" class="isla-quote-download-btn isla-quote-open" data-quote-open="1">
<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
<path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
<polyline points="15 3 21 3 21 9"></polyline>
<line x1="10" y1="14" x2="21" y2="3"></line>
</svg>
View Signed Quote
</a>
</div>
""";
    }

}
