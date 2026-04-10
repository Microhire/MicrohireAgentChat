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
/// Key-value parsing utilities, synthetic message builders, EnsureStructuredFormsInChat,
/// form detection helpers, and form UI JSON builders (BuildXxxFormUiJson).
/// </summary>
public sealed partial class ChatController
{
    private static Dictionary<string, string> ParseKeyValueBlob(string blob)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(blob)) return map;

        foreach (var segment in blob.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            map[parts[0]] = parts[1];
        }

        return map;
    }

    private static string GetDecodedValue(Dictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return "";
        try
        {
            return Uri.UnescapeDataString(raw).Trim();
        }
        catch
        {
            return raw.Trim();
        }
    }

    private static (string venueName, string roomName) ResolveVenueAndRoom(string venueField)
    {
        var value = (venueField ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return ("", "");

        var lower = value.ToLowerInvariant();
        if (lower.Contains("westin ballroom 1", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Westin Ballroom 1");
        if (lower.Contains("westin ballroom 2", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Westin Ballroom 2");
        if (lower.Contains("westin ballroom full", StringComparison.Ordinal) || lower.Equals("westin ballroom", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Westin Ballroom");
        if (lower.Contains("elevate 1", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Elevate 1");
        if (lower.Contains("elevate 2", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Elevate 2");
        if (lower.Contains("elevate full", StringComparison.Ordinal) || lower.Equals("elevate", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Elevate");
        if (lower.Contains("thrive", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Thrive Boardroom");

        // Venue dropdown label only (no specific room) — do not store the venue name as Draft:RoomName.
        if (string.Equals(value, "Westin Brisbane", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "The Westin Brisbane", StringComparison.OrdinalIgnoreCase))
            return ("The Westin Brisbane", "");

        return ("The Westin Brisbane", value);
    }

    /// <summary>
    /// Maps the client venue-confirm <c>room=</c> token (slug or display name) to the canonical room name used in session.
    /// </summary>
    private static string? ResolveRoomDisplayNameFromVenueConfirmRoomToken(
        string? roomToken,
        IReadOnlyList<(string Slug, string Name)> options)
    {
        var t = (roomToken ?? "").Trim();
        if (string.IsNullOrEmpty(t))
            return null;

        foreach (var (slug, name) in options)
        {
            if (string.Equals(slug, t, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        foreach (var (slug, name) in options)
        {
            if (string.Equals(name, t, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        var matchedSlug = WestinRoomCatalog.MatchDraftRoomNameToSlug(t, options);
        if (matchedSlug == null)
            return null;

        foreach (var (slug, name) in options)
        {
            if (string.Equals(slug, matchedSlug, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return null;
    }

    /// <summary>
    /// True when session <c>Draft:RoomName</c> matches a quotable Westin venue-confirm room (not the venue label alone).
    /// </summary>
    private bool HasQuotableWestinRoomInDraft()
    {
        var room = (HttpContext.Session.GetString("Draft:RoomName") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(room))
            return false;
        var options = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        return WestinRoomCatalog.MatchDraftRoomNameToSlug(room, options) != null;
    }

    private static string NormalizeToIsoDateOrEmpty(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var t = raw.Trim().Replace('/', '-');
        var formats = new[] { "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd-M-yyyy", "d-MM-yyyy" };
        if (DateOnly.TryParseExact(t, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw.Trim();
    }

    private bool IsLeadEntry() =>
        string.Equals(HttpContext.Session.GetString("Draft:EntrySource"), "lead", StringComparison.OrdinalIgnoreCase);

    private static bool TryValidateEmailFormat(string email, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new MailAddress(email.Trim());
            normalized = addr.Address.ToLowerInvariant();
            return normalized.Contains('@', StringComparison.Ordinal) && normalized.Length <= 254;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyBookingPrefillToSession(BookingPrefillFromEmailResult lookup)
    {
        var c = lookup.Contact;
        var name = (c.Contactname ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"{c.Firstname ?? ""} {c.Surname ?? ""}".Trim();
        if (!string.IsNullOrWhiteSpace(name))
            HttpContext.Session.SetString("Draft:ContactName", name);

        if (!string.IsNullOrWhiteSpace(c.Email))
            HttpContext.Session.SetString("Draft:ContactEmail", c.Email.Trim().ToLowerInvariant());

        var b = lookup.Booking!;
        if (!string.IsNullOrWhiteSpace(b.OrganizationV6))
            HttpContext.Session.SetString("Draft:Organisation", b.OrganizationV6.Trim());
        else if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:Organisation")))
            HttpContext.Session.SetString("Draft:Organisation", "Event");

        if (!string.IsNullOrWhiteSpace(b.booking_no))
            HttpContext.Session.SetString("Draft:BookingNo", b.booking_no);

        var venueName = (lookup.VenueDisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(venueName))
            venueName = "The Westin Brisbane";
        HttpContext.Session.SetString("Draft:VenueName", venueName);

        var room = (b.VenueRoom ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(room))
            HttpContext.Session.SetString("Draft:RoomName", room);

        var startDay = b.ShowSDate ?? b.dDate ?? b.SDate;
        if (startDay.HasValue)
            HttpContext.Session.SetString("Draft:EventDate", startDay.Value.ToString("yyyy-MM-dd"));

        var endDay = b.ShowEdate ?? b.rDate;
        if (endDay.HasValue)
            HttpContext.Session.SetString("Draft:EventEndDate", endDay.Value.ToString("yyyy-MM-dd"));
        else if (startDay.HasValue)
            HttpContext.Session.SetString("Draft:EventEndDate", startDay.Value.ToString("yyyy-MM-dd"));

        if (b.expAttendees is > 0)
        {
            var exp = b.expAttendees.Value.ToString(CultureInfo.InvariantCulture);
            HttpContext.Session.SetString("Draft:ExpectedAttendees", exp);
            HttpContext.Session.SetString("Draft:LeadSeededExpectedAttendees", exp);
        }

        HttpContext.Session.SetString("Draft:ContactFormSubmitted", "1");
        HttpContext.Session.Remove("Draft:NeedManualContact");
    }

    private void ApplyContactOnlyFromLookup(TblContact c)
    {
        var name = (c.Contactname ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"{c.Firstname ?? ""} {c.Surname ?? ""}".Trim();
        if (!string.IsNullOrWhiteSpace(name))
            HttpContext.Session.SetString("Draft:ContactName", name);
        if (!string.IsNullOrWhiteSpace(c.Email))
            HttpContext.Session.SetString("Draft:ContactEmail", c.Email.Trim().ToLowerInvariant());
    }

    private static string BuildEmailIntakeSyntheticMessage(string email, BookingPrefillFromEmailResult? lookup)
    {
        if (lookup?.Booking != null)
            return $"Structured intake: email {email}; booking lookup found proposal {lookup.Booking.booking_no}.";
        if (lookup != null)
            return $"Structured intake: email {email}; booking lookup: no upcoming booking — manual contact required.";
        return $"Structured intake: email {email}; contact not found in system — manual contact required.";
    }

    private static string BuildVenueConfirmSyntheticMessage(VenueConfirmFormSubmission s) =>
        $"Venue confirm: venue {s.VenueField}; start {s.StartDate}; end {s.EndDate}; attendees {s.Attendees}.";

    private static string BuildEventDetailsSyntheticMessage(EventDetailsFormSubmission s) =>
        $"Event details provided: event type {s.EventType}; setup style {s.SetupStyle}; " +
        $"rehearsal operator {s.WantsRehearsalOperator}; operator {s.WantsOperator}; schedule setup {s.SetupTime}, rehearsal {s.RehearsalTime}, " +
        $"start {s.StartTime}, end {s.EndTime}.";

    private static string BuildBaseAvSyntheticMessage(BaseAvFormSubmission s) =>
        $"Base AV provided: built-in projector {s.BuiltInProjector}; screen {s.BuiltInScreen}; speakers {s.BuiltInSpeakers}; " +
        $"placement {s.ProjectorPlacement}; presenters {s.Presenters}; flipchart {s.Flipchart}; " +
        $"laptop mode {s.LaptopMode}; laptop qty {s.LaptopQty}; adapter for own laptops {s.AdapterOwnLaptops}.";

    private string BuildFollowUpAvSyntheticMessage(FollowUpAvFormSubmission s)
    {
        var pr = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0";
        return
            $"AV extras provided: presenters {pr}; speakers 0; wireless clicker no; audio video recording no; " +
            $"technician from {HttpContext.Session.GetString("Draft:TechStartTime")} to {HttpContext.Session.GetString("Draft:TechEndTime")}; " +
            $"whole event coverage yes. " +
            $"Follow-up: mic {s.MicType} qty {s.MicQty}; lectern {s.Lectern}; foldback {s.FoldbackMonitor}; " +
            $"wireless presenter {s.WirelessPresenter}; laptop switcher {s.LaptopSwitcher}; stage laptop {s.StageLaptop}; " +
            $"video conference {s.VideoConference}.";
    }

    /// <summary>Matches Azure SDK "Agent" and OpenAI-style "assistant" (see <see cref="AzureAgentChatService"/> transcript normalization).</summary>
    private static bool IsAssistantMessageRole(string? role) =>
        string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "agent", StringComparison.OrdinalIgnoreCase);

    private void EnsureStructuredFormsInChat(List<DisplayMessage> messages)
    {
        if (messages == null) return;
        var index = (messages.Count > 0 &&
                     IsAssistantMessageRole(messages[0].Role) &&
                     (messages[0].FullText ?? "").Contains("Hello, my name is Isla from Microhire", StringComparison.OrdinalIgnoreCase))
                     ? 1 : 0;

        var followUpAvSubmitted = HttpContext.Session.GetString("Draft:FollowUpAvSubmitted") == "1";

        // When the wizard is incomplete (quote not generated yet), strip any leftover
        // "Quote Ready" messages from a previous run so they don't render prematurely
        // (e.g. after Edit Quote resets the form flags but the Azure transcript still
        // contains the old quote-success message).
        if (HttpContext.Session.GetString("Draft:QuoteComplete") != "1")
        {
            messages.RemoveAll(m =>
            {
                if (!IsAssistantMessageRole(m.Role)) return false;
                var raw = string.Join("\n", (m.Parts ?? new List<string>()).Select(p => p ?? string.Empty))
                    .ToLowerInvariant();
                if (raw.Contains("\"quoteurl\"") || raw.Contains("\"quote_url\""))
                    return true;
                if (raw.Contains("generated your quote") && (raw.Contains("quote ready") || raw.Contains("view quote")))
                    return true;
                return false;
            });
        }

        var entrySource = HttpContext.Session.GetString("Draft:EntrySource") ?? "general";
        var emailGateComplete = HttpContext.Session.GetString("Draft:EmailGateCompleted") == "1";
        var needManualContact = HttpContext.Session.GetString("Draft:NeedManualContact") == "1";
        var contactFormSubmitted = HttpContext.Session.GetString("Draft:ContactFormSubmitted") == "1";
        var venueConfirmSubmitted = HttpContext.Session.GetString("Draft:VenueConfirmSubmitted") == "1";
        var eventFormSubmitted = HttpContext.Session.GetString("Draft:EventFormSubmitted") == "1";
        var baseAvSubmitted = HttpContext.Session.GetString("Draft:BaseAvSubmitted") == "1";

        // Lead links: after email verification, show venue wizard even if org/name are missing on the lead row.
        var hasContactDraft = contactFormSubmitted
            || (IsLeadEntry()
                && emailGateComplete
                && (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactEmail"))
                    || !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactPhone"))))
            || (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactName"))
                && !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:Organisation"))
                && (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactEmail"))
                    || !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactPhone"))));

        var attendeesRaw = HttpContext.Session.GetString("Draft:ExpectedAttendees");
        _ = int.TryParse(attendeesRaw, out var attendees);
        var hasEventCoreDraft =
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:VenueName")) &&
            HasQuotableWestinRoomInDraft() &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EventType")) &&
            attendees > 0 &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EventDate")) &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:StartTime")) &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EndTime"));

        // 1) Email gate (direct visitors and sales-portal lead links — confirm email before continuing)
        if ((string.Equals(entrySource, "general", StringComparison.OrdinalIgnoreCase)
             || string.Equals(entrySource, "lead", StringComparison.OrdinalIgnoreCase))
            && !emailGateComplete)
        {
            messages.RemoveAll(IsLegacyContactPromptMessage);
            if (!messages.Any(m => MessageContainsUiType(m, "emailForm")))
            {
                var emailIntro = string.Equals(entrySource, "lead", StringComparison.OrdinalIgnoreCase)
                    ? "Please confirm the email address you used for your enquiry so we can verify it’s you."
                    : "Please enter your email address so I can verify your booking request.";
                messages.Insert(index++, BuildUiAssistantMessage(emailIntro, BuildEmailFormUiJson()));
            }
            return;
        }

        // 2) Manual contact when lookup did not return a booking
        if (needManualContact && !contactFormSubmitted)
        {
            messages.RemoveAll(IsLegacyContactPromptMessage);
            if (!messages.Any(m => MessageContainsUiType(m, "contactForm")))
            {
                messages.Insert(index++, BuildUiAssistantMessage("Please complete this quick contact form:", BuildContactFormUiJson()));
            }
            return;
        }

        // 3) Contact details on file
        if (!hasContactDraft)
        {
            messages.RemoveAll(IsLegacyContactPromptMessage);
            if (!messages.Any(m => MessageContainsUiType(m, "contactForm")))
            {
                messages.Insert(index++, BuildUiAssistantMessage("Please complete this quick contact form:", BuildContactFormUiJson()));
            }
            return;
        }
        else if (contactFormSubmitted && HttpContext.Session.GetString("Draft:ShowContactSummary") == "1")
        {
            messages.RemoveAll(m => MessageContainsUiType(m, "contactForm"));
            if (!messages.Any(m => MessageContainsUiType(m, "submittedForm") && (m.FullText ?? "").Contains("Contact details submitted")))
            {
                messages.Insert(index++, BuildUiAssistantMessage("Contact details submitted:", BuildSubmittedContactFormViewJson()));
            }
            else index++;
        }

        // 4) Venue + dates + attendees — always inject so submitted forms stay visible (view shows Confirmed + disabled).
        // While two-phase contact save is in flight ("One moment, please!"), do not inject the venue form —
        // it appeared above the follow-up "saved successfully" message and confused users.
        if (string.Equals(HttpContext.Session.GetString("ContactSavePending"), "1", StringComparison.Ordinal))
        {
            messages.RemoveAll(m => MessageContainsUiType(m, "venueConfirmForm"));
            return;
        }

        if (!messages.Any(m => MessageContainsUiType(m, "venueConfirmForm")))
        {
            var intro = HttpContext.Session.GetString("Draft:BookingLookupApplied") == "1"
                ? "I've found your booking details. Please review and confirm."
                : "Review your venue, room, and event dates in the form below.";
            messages.Insert(index++, BuildUiAssistantMessage(intro, BuildVenueConfirmFormUiJson()));
        }
        else index++;

        if (!venueConfirmSubmitted)
            return;

        // 5) Event type + schedule + operator — always (re-)inject when missing from transcript so session
        // values stay visible after thread resets; ViewData EventFormSubmitted drives Confirmed + disabled UI.
        if (!messages.Any(m => MessageContainsUiType(m, "eventDetailsForm")))
        {
            messages.Insert(index++, BuildUiAssistantMessage("Please tell me more about your event.", BuildEventDetailsFormUiJson()));
        }
        else index++;

        if (!hasEventCoreDraft || !eventFormSubmitted)
            return;

        // 6) Base AV package — always inject when missing from thread, embedding per-form submitted state
        // directly in the JSON so the Razor template is independent of the global session flag.
        // "Actually submitted" means the session flag is set AND the submission came from this same
        // Azure thread (matched via Draft:BaseAvSubmittedForThread), OR — legacy fallback — the thread
        // already contains a "Base AV provided:" user message.
        var currentThreadId = HttpContext.Session.GetString("AgentThreadId") ?? "";
        var baseAvForThread = HttpContext.Session.GetString("Draft:BaseAvSubmittedForThread") ?? "";
        var baseAvActuallySubmitted = baseAvSubmitted && (
            (!string.IsNullOrEmpty(baseAvForThread)
                && string.Equals(baseAvForThread, currentThreadId, StringComparison.Ordinal))
            || messages.Any(m =>
                string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (m.FullText ?? string.Join(" ", m.Parts ?? Enumerable.Empty<string>()))
                    .StartsWith("Base AV provided:", StringComparison.OrdinalIgnoreCase)));

        if (!messages.Any(m => MessageContainsUiType(m, "baseAvForm")))
        {
            messages.Insert(index++, BuildUiAssistantMessage(
                "Please review the included equipment for this room and confirm the options below.",
                BuildBaseAvFormUiJson(baseAvActuallySubmitted)));
        }
        else index++;

        if (!baseAvActuallySubmitted)
            return;

        // 7) Follow-up AV questions
        // Strip legacy tool-built avExtrasForm messages — they duplicate the server-injected followUpAvForm.
        messages.RemoveAll(IsAvExtrasFormMessage);

        if (!messages.Any(m => MessageContainsUiType(m, "followUpAvForm")))
        {
            messages.Insert(index++, BuildUiAssistantMessage(
                "Thanks for confirming the base AV package. I have a few follow-up questions.",
                BuildFollowUpAvFormUiJson()));
        }
        else index++;

        if (!followUpAvSubmitted)
            return;
    }

    private static bool IsLegacyContactPromptMessage(DisplayMessage message)
    {
        if (!string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;
        var text = (message.FullText ?? string.Join("\n", message.Parts ?? Enumerable.Empty<string>())).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (text.Contains("{\"ui\":", StringComparison.OrdinalIgnoreCase))
            return false;
        var lower = text.ToLowerInvariant();
        return lower.Contains("what is your full name")
            || lower.Contains("share your full name")
            || lower.Contains("please share your full name")
            || lower.Contains("could you please share your full name")
            || lower.Contains("full name to get started");
    }

    private static bool MessageContainsUiType(DisplayMessage message, string type)
    {
        var marker = $"\"type\":\"{type}\"";
        foreach (var part in message.Parts ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(part) && part.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return !string.IsNullOrWhiteSpace(message.FullText)
            && message.FullText.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAvExtrasFormMessage(DisplayMessage message)
    {
        if (MessageContainsUiType(message, "avExtrasForm"))
            return true;

        if (!string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;

        var text = (message.FullText ?? string.Join("\n", message.Parts ?? Enumerable.Empty<string>())).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("Please confirm these AV extras:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Please complete this AV extras form:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Finally, confirm your AV extras", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFollowUpAvFormMessage(DisplayMessage message)
    {
        if (MessageContainsUiType(message, "followUpAvForm"))
            return true;
        if (!string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;
        var text = (message.FullText ?? string.Join("\n", message.Parts ?? Enumerable.Empty<string>())).Trim();
        return text.Contains("I have a few follow-up questions.", StringComparison.OrdinalIgnoreCase);
    }

    private static DisplayMessage BuildUiAssistantMessage(string preface, string uiJson)
    {
        var body = $"{preface}\n\n{uiJson}";
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { body },
            FullText = body,
            Html = preface
        };
    }

    private static string BuildContactFormUiJson()
    {
        var payload = new
        {
            ui = new
            {
                type = "contactForm",
                title = "Before we begin, please share your details",
                submitLabel = "Send details"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildEmailFormUiJson()
    {
        var defaultEmail = IsLeadEntry()
            ? ""
            : (HttpContext.Session.GetString("Draft:ContactEmail") ?? "");
        var payload = new { ui = new { type = "emailForm", title = "Email", submitLabel = "Continue", defaultEmail } };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildVenueConfirmFormUiJson()
    {
        var todayIso = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var start = HttpContext.Session.GetString("Draft:EventDate") ?? todayIso;
        var end = HttpContext.Session.GetString("Draft:EventEndDate") ?? start;
        var rooms = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var draftRoom = HttpContext.Session.GetString("Draft:RoomName")?.Trim() ?? "";
        var selectedSlug = WestinRoomCatalog.MatchDraftRoomNameToSlug(draftRoom, rooms);
        var roomOptions = rooms.Select(r => new { id = r.Slug, label = r.Name }).ToArray();
        var payload = new
        {
            ui = new
            {
                type = "venueConfirmForm",
                title = "Confirm your event",
                submitLabel = "Continue",
                attendees = HttpContext.Session.GetString("Draft:ExpectedAttendees") ?? "",
                startDate = start,
                endDate = end,
                minDate = todayIso,
                venueLabel = WestinRoomCatalog.VenueName,
                roomOptions,
                selectedRoomSlug = selectedSlug
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildEventDetailsFormUiJson()
    {
        var todayIso = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var payload = new
        {
            ui = new
            {
                type = "eventDetailsForm",
                title = "Event details",
                submitLabel = "Next",
                eventType = HttpContext.Session.GetString("Draft:EventType") ?? "",
                wantsRehearsalOperator = HttpContext.Session.GetString("Draft:WantsRehearsalOperator") ?? "",
                wantsOperator = HttpContext.Session.GetString("Draft:WantsOperator") ?? "",
                minDate = todayIso,
                schedule = new
                {
                    setup = HttpContext.Session.GetString("Draft:SetupTime") ?? "07:00",
                    rehearsal = HttpContext.Session.GetString("Draft:RehearsalTime") ?? "09:30",
                    start = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00",
                    end = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00",
                    packup = HttpContext.Session.GetString("Draft:PackupTime") ?? "18:00",
                    stepMinutes = 30
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildBaseAvFormUiJson(bool submitted = false)
    {
        var room = HttpContext.Session.GetString("Draft:RoomName") ?? "";
        var showPlacement = RoomSupportsProjectorPlacement(room);
        var floorPlanUrl = showPlacement ? "/images/westin/westin-ballroom/floor-plan.png" : "";
        var venueForPackages = HttpContext.Session.GetString("Draft:VenueName")?.Trim();
        if (string.IsNullOrWhiteSpace(venueForPackages))
            venueForPackages = WestinRoomCatalog.VenueName;
        var roomTrim = room.Trim();
        var roomOptionsForTitle = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var roomTitle = roomTrim;
        if (WestinRoomCatalog.MatchDraftRoomNameToSlug(roomTrim, roomOptionsForTitle) is { } titleSlug)
        {
            foreach (var (s, name) in roomOptionsForTitle)
            {
                if (string.Equals(s, titleSlug, StringComparison.OrdinalIgnoreCase))
                {
                    roomTitle = name;
                    break;
                }
            }
        }
        else if (string.IsNullOrWhiteSpace(roomTrim))
            roomTitle = "your room";

        var placementFromJson = VenueRoomPackagesCache.TryGetProjectorPlacementOptions(_env, venueForPackages, roomTrim);
        if (placementFromJson == null)
        {
            var packageRoomKey = ResolveWestinPackageRoomKeyForPlacement(roomTrim);
            if (!string.IsNullOrWhiteSpace(packageRoomKey)
                && !string.Equals(packageRoomKey, roomTrim, StringComparison.OrdinalIgnoreCase))
            {
                placementFromJson = VenueRoomPackagesCache.TryGetProjectorPlacementOptions(_env, venueForPackages, packageRoomKey);
            }
        }

        var baseEquipmentLabels = VenueRoomPackagesCache.TryGetBaseEquipmentLabels(_env, venueForPackages, roomTrim);
        var baseEquipment = baseEquipmentLabels.Count > 0
            ? baseEquipmentLabels.ToArray()
            : new[] { "Inbuilt projector", "Inbuilt screen", "Inbuilt speakers" };

        var payload = new
        {
            ui = new
            {
                type = "baseAvForm",
                submitted,
                title = $"Base AV package for {roomTitle}",
                submitLabel = "Next",
                roomName = room,
                baseEquipment,
                showProjectorPlacement = showPlacement,
                ballroomMode = showPlacement,
                floorPlanUrl,
                projectorPlacement = HttpContext.Session.GetString("Draft:ProjectorPlacementChoice") ?? "",
                placementOptions = placementFromJson ?? GetProjectorPlacementOptionsFallback(room),
                presenters = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0",
                flipchart = HttpContext.Session.GetString("Draft:Flipchart") ?? "no",
                laptopMode = HttpContext.Session.GetString("Draft:LaptopMode") ?? "none",
                laptopQty = HttpContext.Session.GetString("Draft:LaptopQty") ?? "0",
                adapterOwnLaptops = HttpContext.Session.GetString("Draft:AdapterOwnLaptops") ?? "no",
                builtInProjector = HttpContext.Session.GetString("Draft:BuiltInProjector") ?? "yes",
                builtInScreen = HttpContext.Session.GetString("Draft:BuiltInScreen") ?? "yes",
                builtInSpeakers = HttpContext.Session.GetString("Draft:BuiltInSpeakers") ?? "yes"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildFollowUpAvFormUiJson()
    {
        var presenters = 0;
        _ = int.TryParse(HttpContext.Session.GetString("Draft:PresenterCount"), out presenters);
        var laptopQty = 0;
        _ = int.TryParse(HttpContext.Session.GetString("Draft:LaptopQty"), out laptopQty);
        var roomName = HttpContext.Session.GetString("Draft:RoomName") ?? "";
        var payload = new
        {
            ui = new
            {
                type = "followUpAvForm",
                title = "Follow-up AV",
                submitLabel = "Generate quote",
                presenterCount = presenters,
                laptopQty,
                roomName,
                micType = HttpContext.Session.GetString("Draft:MicType") ?? "",
                micQty = HttpContext.Session.GetString("Draft:MicQty") ?? "0",
                lectern = HttpContext.Session.GetString("Draft:Lectern") ?? "",
                foldbackMonitor = HttpContext.Session.GetString("Draft:FoldbackMonitor") ?? "no",
                wirelessPresenter = HttpContext.Session.GetString("Draft:WirelessPresenter") ?? "no",
                laptopSwitcher = HttpContext.Session.GetString("Draft:LaptopSwitcher") ?? "no",
                stageLaptop = HttpContext.Session.GetString("Draft:StageLaptop") ?? "no",
                videoConference = HttpContext.Session.GetString("Draft:VideoConference") ?? "no"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private static bool RoomSupportsProjectorPlacement(string? roomName)
    {
        var r = (roomName ?? "").ToLowerInvariant();
        if (r.Contains("elevate", StringComparison.Ordinal))
            return false;
        // Only enable projector placement for the three specific Westin ballroom configurations
        var isBallroom1 = r.Contains("ballroom 1", StringComparison.Ordinal) || r.Contains("ballroom-1", StringComparison.Ordinal);
        var isBallroom2 = r.Contains("ballroom 2", StringComparison.Ordinal) || r.Contains("ballroom-2", StringComparison.Ordinal);
        // Full ballroom (but not the split rooms) - must contain "ballroom" but NOT "1" or "2"
        var isFullBallroom = r.Contains("ballroom", StringComparison.Ordinal)
            && !r.Contains("ballroom 1", StringComparison.Ordinal)
            && !r.Contains("ballroom-1", StringComparison.Ordinal)
            && !r.Contains("ballroom 2", StringComparison.Ordinal)
            && !r.Contains("ballroom-2", StringComparison.Ordinal);
        return isBallroom1 || isBallroom2 || isFullBallroom;
    }

    /// <summary>Maps draft/slug room labels to <c>venue-room-packages.json</c> keys for Westin ballroom variants.</summary>
    private static string? ResolveWestinPackageRoomKeyForPlacement(string? roomName)
    {
        var roomNorm = (roomName ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(roomNorm))
            return null;
        if (roomNorm.Contains("ballroom 1") || roomNorm.Contains("ballroom-1"))
            return "Westin Ballroom 1";
        if (roomNorm.Contains("ballroom 2") || roomNorm.Contains("ballroom-2"))
            return "Westin Ballroom 2";
        if (roomNorm.Contains("ballroom"))
            return "Westin Ballroom";
        return null;
    }

    /// <summary>Legacy placement list when <c>venue-room-packages.json</c> has no <c>projectorPlacementOptions</c> for the room.</summary>
    private static object[] GetProjectorPlacementOptionsFallback(string? roomName)
    {
        var allowed = GetAllowedProjectorAreasForRoom(roomName);
        var list = new List<object>();
        foreach (var a in allowed)
            list.Add(new { id = a, label = a });

        var room = (roomName ?? "").Trim().ToLowerInvariant();
        var isSplit = room is "westin ballroom 1" or "ballroom 1" or "westin ballroom 2" or "ballroom 2";
        if (!isSplit && allowed.Count == 6)
        {
            list.Add(new { id = "B+C", label = "B+C" });
            list.Add(new { id = "E+F", label = "E+F" });
        }

        return list.ToArray();
    }

    /// <summary>Parses projector placement (single area or A+B dual) against areas allowed for the Westin ballroom variant.</summary>
    private static List<string> ParseProjectorPlacementToAllowedAreas(string? placement, string? roomName)
    {
        var allowed = GetAllowedProjectorAreasForRoom(roomName);
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var p = (placement ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        if (string.IsNullOrEmpty(p))
            return result;

        var m = Regex.Match(p, @"^([A-F])\+([A-F])$");
        if (m.Success)
        {
            foreach (var g in new[] { m.Groups[1].Value, m.Groups[2].Value })
            {
                if (allowedSet.Contains(g) && !result.Exists(x => string.Equals(x, g, StringComparison.OrdinalIgnoreCase)))
                    result.Add(g);
            }

            return result;
        }

        if (p.Length == 1 && p[0] is >= 'A' and <= 'F' && allowedSet.Contains(p))
            result.Add(p);
        return result;
    }

    private string BuildSubmittedFollowUpAvViewJson()
    {
        var items = new List<(string label, string value)>();
        if (int.TryParse(HttpContext.Session.GetString("Draft:PresenterCount"), out var pc) && pc > 0)
        {
            items.Add(("Microphone", HttpContext.Session.GetString("Draft:MicType") ?? ""));
            items.Add(("Video conference", HttpContext.Session.GetString("Draft:VideoConference") ?? ""));
        }
        var payload = new { ui = new { type = "submittedForm", title = "AV selections confirmed", items } };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildEventFormUiJson()
    {
        var todayIso = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var rooms = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var draftRoom = HttpContext.Session.GetString("Draft:RoomName")?.Trim() ?? "";
        var selectedSlug = WestinRoomCatalog.MatchDraftRoomNameToSlug(draftRoom, rooms);
        var roomOptions = rooms.Select(r => new { id = r.Slug, label = r.Name }).ToArray();
        var payload = new
        {
            ui = new
            {
                type = "eventForm",
                title = "Great, now let's capture your event details",
                submitLabel = "Send event details",
                eventType = HttpContext.Session.GetString("Draft:EventType") ?? "",
                attendees = HttpContext.Session.GetString("Draft:ExpectedAttendees") ?? "",
                eventDate = HttpContext.Session.GetString("Draft:EventDate") ?? todayIso,
                minDate = todayIso,
                setupStyle = HttpContext.Session.GetString("Draft:SetupStyle") ?? "",
                venueLabel = WestinRoomCatalog.VenueName,
                roomOptions,
                selectedRoomSlug = selectedSlug,
                setupOptions = new[] { "Theatre", "Classroom", "Banquet", "Cocktail", "U-Shape", "Boardroom" },
                schedule = new
                {
                    setup = HttpContext.Session.GetString("Draft:SetupTime") ?? "07:00",
                    rehearsal = HttpContext.Session.GetString("Draft:RehearsalTime") ?? "09:30",
                    start = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00",
                    end = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00",
                    packup = HttpContext.Session.GetString("Draft:PackupTime") ?? "18:00",
                    stepMinutes = 30
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildAvExtrasFormUiJson()
    {
        var eventStart = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00";
        var eventEnd = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00";
        var payload = new
        {
            ui = new
            {
                type = "avExtrasForm",
                title = "Finally, confirm your AV extras",
                submitLabel = "Send AV extras",
                presenters = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0",
                speakers = HttpContext.Session.GetString("Draft:SpeakerCount") ?? "0",
                clicker = string.Equals(HttpContext.Session.GetString("Draft:NeedsClicker"), "yes", StringComparison.OrdinalIgnoreCase),
                recording = string.Equals(HttpContext.Session.GetString("Draft:NeedsRecording"), "yes", StringComparison.OrdinalIgnoreCase),
                techStart = HttpContext.Session.GetString("Draft:TechStartTime") ?? eventStart,
                techEnd = HttpContext.Session.GetString("Draft:TechEndTime") ?? eventEnd,
                eventStart,
                eventEnd,
                stepMinutes = 30
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildSubmittedContactFormViewJson()
    {
        var firstName = HttpContext.Session.GetString("Draft:ContactFirstName") ?? "";
        var lastName = HttpContext.Session.GetString("Draft:ContactLastName") ?? "";
        var org = HttpContext.Session.GetString("Draft:Organisation") ?? "";
        var location = HttpContext.Session.GetString("Draft:OrganisationAddress") ?? "";
        var email = HttpContext.Session.GetString("Draft:ContactEmail") ?? "";
        var phone = HttpContext.Session.GetString("Draft:ContactPhone") ?? "";

        var items = new List<(string label, string value)>();
        if (!string.IsNullOrWhiteSpace(firstName)) items.Add(("First name", firstName));
        if (!string.IsNullOrWhiteSpace(lastName)) items.Add(("Last name", lastName));
        if (!string.IsNullOrWhiteSpace(org)) items.Add(("Organisation", org));
        if (!string.IsNullOrWhiteSpace(location)) items.Add(("Location", location));
        if (!string.IsNullOrWhiteSpace(email)) items.Add(("Email", email));
        if (!string.IsNullOrWhiteSpace(phone)) items.Add(("Phone", phone));

        var payload = new
        {
            ui = new
            {
                type = "submittedForm",
                title = "Contact details submitted",
                items
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildSubmittedAvExtrasFormViewJson()
    {
        var presenters = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0";
        var speakers = HttpContext.Session.GetString("Draft:SpeakerCount") ?? "0";
        var clicker = HttpContext.Session.GetString("Draft:NeedsClicker") ?? "no";
        var recording = HttpContext.Session.GetString("Draft:NeedsRecording") ?? "no";
        var techStart = HttpContext.Session.GetString("Draft:TechStartTime") ?? "";
        var techEnd = HttpContext.Session.GetString("Draft:TechEndTime") ?? "";

        var items = new List<(string label, string value)>();
        if (int.TryParse(presenters, out var pCount) && pCount > 0) items.Add(("Presenters", presenters));
        if (int.TryParse(speakers, out var sCount) && sCount > 0) items.Add(("Speakers", speakers));
        items.Add(("Wireless clicker", clicker.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No"));
        items.Add(("Audio/video recording", recording.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No"));
        if (!string.IsNullOrWhiteSpace(techStart) && !string.IsNullOrWhiteSpace(techEnd))
            items.Add(("Technician coverage", $"{techStart} to {techEnd}"));

        var payload = new
        {
            ui = new
            {
                type = "submittedForm",
                title = "AV extras confirmed",
                items
            }
        };
        return JsonSerializer.Serialize(payload);
    }

}
