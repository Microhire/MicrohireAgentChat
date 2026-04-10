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
/// Form submission model classes, capture methods (TryCapture*),
/// and session persistence (Save*ToSession).
/// </summary>
public sealed partial class ChatController
{
    private sealed class ContactFormSubmission
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Organisation { get; set; } = "";
        public string Location { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
    }

    private sealed class EventFormSubmission
    {
        public string Venue { get; set; } = "";
        public string EventType { get; set; } = "";
        public string SetupStyle { get; set; } = "";
        public int Attendees { get; set; }
        public string Date { get; set; } = "";
        public string SetupTime { get; set; } = "";
        public string RehearsalTime { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string PackupTime { get; set; } = "";
    }

    private sealed class AvExtrasFormSubmission
    {
        public int Presenters { get; set; }
        public int Speakers { get; set; }
        public string Clicker { get; set; } = "no";
        public string Recording { get; set; } = "no";
        public string TechStart { get; set; } = "";
        public string TechEnd { get; set; } = "";
        public string TechWholeEvent { get; set; } = "no";
    }

    private sealed class VenueConfirmFormSubmission
    {
        public string VenueField { get; set; } = "";
        /// <summary>Westin room slug from the venue-confirm dropdown (client sends <c>room=</c> in the payload).</summary>
        public string RoomSlug { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public int Attendees { get; set; }
    }

    private sealed class EventDetailsFormSubmission
    {
        public string EventType { get; set; } = "";
        public string SetupStyle { get; set; } = "";
        public string SetupTime { get; set; } = "";
        public string RehearsalTime { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string PackupTime { get; set; } = "";
        public string WantsRehearsalOperator { get; set; } = "no";
        public string WantsOperator { get; set; } = "no";
    }

    private sealed class BaseAvFormSubmission
    {
        public bool BuiltInProjector { get; set; } = true;
        public bool BuiltInScreen { get; set; } = true;
        public bool BuiltInSpeakers { get; set; } = true;
        public string ProjectorPlacement { get; set; } = "";
        public int Presenters { get; set; }
        public string Flipchart { get; set; } = "no";
        public string LaptopMode { get; set; } = "none";
        public int LaptopQty { get; set; }
        public string AdapterOwnLaptops { get; set; } = "no";
    }

    private sealed class FollowUpAvFormSubmission
    {
        public string MicType { get; set; } = "";
        public int MicQty { get; set; }
        public string Lectern { get; set; } = "";
        public string FoldbackMonitor { get; set; } = "no";
        public string WirelessPresenter { get; set; } = "no";
        public string LaptopSwitcher { get; set; } = "no";
        public string StageLaptop { get; set; } = "no";
        public string VideoConference { get; set; } = "no";
    }

    private static bool TryCaptureEmailFormSubmission(string text, out string normalizedEmail)
    {
        normalizedEmail = "";
        var m = EmailFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        var raw = GetDecodedValue(data, "email");
        return TryValidateEmailFormat(raw, out normalizedEmail);
    }

    private static bool TryCaptureVenueConfirmFormSubmission(string text, out VenueConfirmFormSubmission submission, out string userMessage)
    {
        submission = new VenueConfirmFormSubmission();
        userMessage = "";
        var m = VenueConfirmFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.VenueField = GetDecodedValue(data, "venue");
        submission.RoomSlug = GetDecodedValue(data, "room");
        submission.StartDate = GetDecodedValue(data, "startDate");
        submission.EndDate = GetDecodedValue(data, "endDate");
        userMessage = GetDecodedValue(data, "userMessage");
        if (!int.TryParse(GetDecodedValue(data, "attendees"), out var att) || att < 1)
            return false;
        submission.Attendees = att;
        return !string.IsNullOrWhiteSpace(submission.VenueField)
               && !string.IsNullOrWhiteSpace(submission.StartDate);
    }

    private static bool TryCaptureEventDetailsFormSubmission(string text, out EventDetailsFormSubmission submission)
    {
        submission = new EventDetailsFormSubmission();
        var m = EventDetailsFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.EventType = GetDecodedValue(data, "eventType");
        submission.SetupStyle = GetDecodedValue(data, "setupStyle");
        submission.SetupTime = GetDecodedValue(data, "setup");
        submission.RehearsalTime = GetDecodedValue(data, "rehearsal");
        submission.StartTime = GetDecodedValue(data, "start");
        submission.EndTime = GetDecodedValue(data, "end");
        submission.PackupTime = GetDecodedValue(data, "packup");
        if (string.IsNullOrWhiteSpace(submission.PackupTime))
            submission.PackupTime = submission.EndTime;
        submission.WantsRehearsalOperator = GetDecodedValue(data, "wantsRehearsalOperator");
        submission.WantsOperator = GetDecodedValue(data, "wantsOperator");
        return !string.IsNullOrWhiteSpace(submission.EventType)
               && !string.IsNullOrWhiteSpace(submission.SetupTime)
               && !string.IsNullOrWhiteSpace(submission.RehearsalTime)
               && !string.IsNullOrWhiteSpace(submission.StartTime)
               && !string.IsNullOrWhiteSpace(submission.EndTime);
    }

    private static bool TryCaptureBaseAvFormSubmission(string text, out BaseAvFormSubmission submission, out string userMessage)
    {
        submission = new BaseAvFormSubmission();
        userMessage = "";
        var m = BaseAvFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        userMessage = GetDecodedValue(data, "userMessage");
        submission.BuiltInProjector = string.Equals(GetDecodedValue(data, "builtInProjector"), "yes", StringComparison.OrdinalIgnoreCase);
        submission.BuiltInScreen = string.Equals(GetDecodedValue(data, "builtInScreen"), "yes", StringComparison.OrdinalIgnoreCase);
        submission.BuiltInSpeakers = string.Equals(GetDecodedValue(data, "builtInSpeakers"), "yes", StringComparison.OrdinalIgnoreCase);
        submission.ProjectorPlacement = GetDecodedValue(data, "projectorPlacement");
        if (int.TryParse(GetDecodedValue(data, "presenters"), out var p))
            submission.Presenters = Math.Max(0, p);
        submission.Flipchart = GetDecodedValue(data, "flipchart");
        submission.LaptopMode = GetDecodedValue(data, "laptopMode");
        if (int.TryParse(GetDecodedValue(data, "laptopQty"), out var lq))
            submission.LaptopQty = Math.Max(0, lq);
        submission.AdapterOwnLaptops = GetDecodedValue(data, "adapterOwnLaptops");
        return true;
    }

    private static bool TryCaptureFollowUpAvFormSubmission(string text, out FollowUpAvFormSubmission submission)
    {
        submission = new FollowUpAvFormSubmission();
        var m = FollowUpAvFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.MicType = GetDecodedValue(data, "micType");
        if (int.TryParse(GetDecodedValue(data, "micQty"), out var mq))
            submission.MicQty = Math.Max(0, mq);
        submission.Lectern = GetDecodedValue(data, "lectern");
        submission.FoldbackMonitor = GetDecodedValue(data, "foldbackMonitor");
        submission.WirelessPresenter = GetDecodedValue(data, "wirelessPresenter");
        submission.LaptopSwitcher = GetDecodedValue(data, "laptopSwitcher");
        submission.StageLaptop = GetDecodedValue(data, "stageLaptop");
        submission.VideoConference = GetDecodedValue(data, "videoConference");
        return true;
    }

    private static bool TryCaptureContactFormSubmission(string text, out ContactFormSubmission submission)
    {
        submission = new ContactFormSubmission();
        var m = ContactFormRe.Match(text ?? "");
        if (!m.Success) return false;

        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.FirstName = GetDecodedValue(data, "firstName");
        submission.LastName = GetDecodedValue(data, "lastName");
        submission.Organisation = GetDecodedValue(data, "organisation");
        submission.Location = GetDecodedValue(data, "location");
        submission.Email = GetDecodedValue(data, "email");
        submission.Phone = GetDecodedValue(data, "phone");

        return !string.IsNullOrWhiteSpace(submission.FirstName)
               && !string.IsNullOrWhiteSpace(submission.LastName)
               && !string.IsNullOrWhiteSpace(submission.Organisation)
               && !string.IsNullOrWhiteSpace(submission.Location)
               && (!string.IsNullOrWhiteSpace(submission.Email) || !string.IsNullOrWhiteSpace(submission.Phone));
    }

    private static bool TryCaptureEventFormSubmission(string text, out EventFormSubmission submission)
    {
        submission = new EventFormSubmission();
        var m = EventFormRe.Match(text ?? "");
        if (!m.Success) return false;

        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.Venue = GetDecodedValue(data, "venue");
        submission.EventType = GetDecodedValue(data, "eventType");
        submission.SetupStyle = GetDecodedValue(data, "setupStyle");
        submission.Date = GetDecodedValue(data, "date");
        submission.SetupTime = GetDecodedValue(data, "setup");
        submission.RehearsalTime = GetDecodedValue(data, "rehearsal");
        submission.StartTime = GetDecodedValue(data, "start");
        submission.EndTime = GetDecodedValue(data, "end");
        submission.PackupTime = GetDecodedValue(data, "packup");
        if (string.IsNullOrWhiteSpace(submission.PackupTime))
            submission.PackupTime = submission.EndTime;
        var attendeesText = GetDecodedValue(data, "attendees");
        if (!int.TryParse(attendeesText, out var attendees))
        {
            attendees = 0;
        }
        submission.Attendees = attendees;

        return !string.IsNullOrWhiteSpace(submission.Venue)
               && !string.IsNullOrWhiteSpace(submission.EventType)
               && submission.Attendees > 0
               && !string.IsNullOrWhiteSpace(submission.Date);
    }

    private static bool TryCaptureAvExtrasFormSubmission(string text, out AvExtrasFormSubmission submission)
    {
        submission = new AvExtrasFormSubmission();
        var m = AvExtrasFormRe.Match(text ?? "");
        if (!m.Success) return false;

        var data = ParseKeyValueBlob(m.Groups[1].Value);
        if (int.TryParse(GetDecodedValue(data, "presenters"), out var presenters))
            submission.Presenters = Math.Max(0, presenters);
        if (int.TryParse(GetDecodedValue(data, "speakers"), out var speakers))
            submission.Speakers = Math.Max(0, speakers);
        submission.Clicker = GetDecodedValue(data, "clicker");
        submission.Recording = GetDecodedValue(data, "recording");
        submission.TechStart = GetDecodedValue(data, "techStart");
        submission.TechEnd = GetDecodedValue(data, "techEnd");
        submission.TechWholeEvent = GetDecodedValue(data, "techWholeEvent");

        return true;
    }

    private void SaveContactFormToSession(ContactFormSubmission submission)
    {
        var wasManual = HttpContext.Session.GetString("Draft:NeedManualContact") == "1";
        var fullName = $"{submission.FirstName} {submission.LastName}".Trim();
        HttpContext.Session.SetString("Draft:ContactName", fullName);
        HttpContext.Session.SetString("Draft:ContactFirstName", submission.FirstName);
        HttpContext.Session.SetString("Draft:ContactLastName", submission.LastName);
        HttpContext.Session.SetString("Draft:Organisation", submission.Organisation);
        HttpContext.Session.SetString("Draft:OrganisationAddress", submission.Location);
        HttpContext.Session.SetString("Draft:ContactFormSubmitted", "1");
        if (!string.IsNullOrWhiteSpace(submission.Email))
            HttpContext.Session.SetString("Draft:ContactEmail", submission.Email);
        if (!string.IsNullOrWhiteSpace(submission.Phone))
            HttpContext.Session.SetString("Draft:ContactPhone", submission.Phone);
        HttpContext.Session.Remove("Draft:NeedManualContact");
        if (wasManual)
            HttpContext.Session.SetString("Draft:ShowContactSummary", "1");
    }

    private void SaveEventFormToSession(EventFormSubmission submission)
    {
        var (venueName, roomName) = ResolveVenueAndRoom(submission.Venue);
        if (!string.IsNullOrWhiteSpace(venueName))
            HttpContext.Session.SetString("Draft:VenueName", venueName);
        if (!string.IsNullOrWhiteSpace(roomName))
            HttpContext.Session.SetString("Draft:RoomName", roomName);

        HttpContext.Session.SetString("Draft:EventType", submission.EventType);
        HttpContext.Session.SetString("Draft:ExpectedAttendees", submission.Attendees.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:EventFormSubmitted", "1");
        if (!string.IsNullOrWhiteSpace(submission.SetupStyle))
            HttpContext.Session.SetString("Draft:SetupStyle", submission.SetupStyle);

        var eventDateNormalized = NormalizeToIsoDateOrEmpty(submission.Date);
        if (DateTime.TryParse(eventDateNormalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate)
            && TimeSpan.TryParse(submission.SetupTime, CultureInfo.InvariantCulture, out var setup)
            && TimeSpan.TryParse(submission.RehearsalTime, CultureInfo.InvariantCulture, out var rehearsal)
            && TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endForPackup))
        {
            TimeSpan? start = TimeSpan.TryParse(submission.StartTime, CultureInfo.InvariantCulture, out var startTs) ? startTs : null;
            TimeSpan? end = TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endTs) ? endTs : null;
            var packup = endForPackup;
            SaveScheduleToSession(setup, rehearsal, start, end, packup, eventDate);
        }
    }

    private void SaveAvExtrasToSession(AvExtrasFormSubmission submission)
    {
        HttpContext.Session.SetString("Draft:PresenterCount", submission.Presenters.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:SpeakerCount", submission.Speakers.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:NeedsClicker", submission.Clicker);
        HttpContext.Session.SetString("Draft:NeedsRecording", submission.Recording);
        HttpContext.Session.SetString("Draft:TechStartTime", submission.TechStart);
        HttpContext.Session.SetString("Draft:TechEndTime", submission.TechEnd);
        HttpContext.Session.SetString("Draft:TechWholeEvent", submission.TechWholeEvent);
        HttpContext.Session.SetString("Draft:AvExtrasSubmitted", "1");
    }

    private void SaveVenueConfirmToSession(VenueConfirmFormSubmission submission)
    {
        var (venueName, roomName) = ResolveVenueAndRoom(submission.VenueField);
        if (!string.IsNullOrWhiteSpace(venueName))
            HttpContext.Session.SetString("Draft:VenueName", venueName);

        var roomOptions = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var roomFromSlug = ResolveRoomDisplayNameFromVenueConfirmRoomToken(submission.RoomSlug, roomOptions);
        if (!string.IsNullOrWhiteSpace(roomFromSlug))
            HttpContext.Session.SetString("Draft:RoomName", roomFromSlug);
        else if (!string.IsNullOrWhiteSpace(roomName))
            HttpContext.Session.SetString("Draft:RoomName", roomName);
        else
            HttpContext.Session.Remove("Draft:RoomName");

        var startNorm = NormalizeToIsoDateOrEmpty(submission.StartDate);
        var endNorm = string.IsNullOrWhiteSpace(submission.EndDate) ? startNorm : NormalizeToIsoDateOrEmpty(submission.EndDate);
        HttpContext.Session.SetString("Draft:EventDate", startNorm);
        HttpContext.Session.SetString("Draft:EventEndDate", endNorm);
        HttpContext.Session.SetString("Draft:ExpectedAttendees", submission.Attendees.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:VenueConfirmSubmitted", "1");
    }

    /// <summary>
    /// Maps free-text event type to a canonical Westin setup for capacity and equipment (UI no longer asks explicitly).
    /// </summary>
    private static string InferSetupStyleFromEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return "Theatre";
        var t = eventType.ToLowerInvariant();

        static bool Has(string s, params string[] needles)
        {
            foreach (var n in needles)
                if (s.Contains(n, StringComparison.Ordinal)) return true;
            return false;
        }

        // Boardroom / small formal meetings
        if (Has(t, "board meeting", "boardroom", "board room", "executive meeting", "board retreat"))
            return "Boardroom";

        // Classroom-style / collaborative learning
        if (Has(t, "hackathon", "workshop", "training", "coding", "bootcamp", "classroom", "school", "course", "tutorial", "seminar", "lecture"))
            return "Classroom";

        // Seated dining / awards
        if (Has(t, "banquet", "gala dinner", "awards dinner", "seated dinner", "wedding breakfast", "wedding reception"))
            return "Banquet";

        // Standing / mingling
        if (Has(t, "cocktail", "networking", "drinks reception", "reception only", "mixer"))
            return "Cocktail";

        // Collaborative table layout
        if (Has(t, "u-shape", "u shape", "hollow square", "breakout"))
            return "U-Shape";

        // Presentations, showcases, keynotes
        if (Has(t, "showcase", "product launch", "product demo", "demo day", "keynote", "pitch", "theatre", "theater",
                "conference", "presentation", "town hall", "all-hands", "all hands", "webinar", "annual general", "agm"))
            return "Theatre";

        return "Theatre";
    }

    private void SaveEventDetailsToSession(EventDetailsFormSubmission submission)
    {
        HttpContext.Session.SetString("Draft:EventType", submission.EventType);
        if (!string.IsNullOrWhiteSpace(submission.SetupStyle))
            HttpContext.Session.SetString("Draft:SetupStyle", submission.SetupStyle);
        else
        {
            var room = HttpContext.Session.GetString("Draft:RoomName") ?? "";
            if (room.Contains("Thrive", StringComparison.OrdinalIgnoreCase))
                HttpContext.Session.SetString("Draft:SetupStyle", "Boardroom");
            else
                HttpContext.Session.SetString("Draft:SetupStyle", InferSetupStyleFromEventType(submission.EventType));
        }

        submission.SetupStyle = HttpContext.Session.GetString("Draft:SetupStyle") ?? submission.SetupStyle;
        HttpContext.Session.SetString("Draft:WantsRehearsalOperator", submission.WantsRehearsalOperator);
        HttpContext.Session.SetString("Draft:RehearsalOperator", submission.WantsRehearsalOperator);
        HttpContext.Session.SetString("Draft:WantsOperator", submission.WantsOperator);

        var dateStr = HttpContext.Session.GetString("Draft:EventDate") ?? "";
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate)
            && TimeSpan.TryParse(submission.SetupTime, CultureInfo.InvariantCulture, out var setup)
            && TimeSpan.TryParse(submission.RehearsalTime, CultureInfo.InvariantCulture, out var rehearsal)
            && TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endForPackup))
        {
            TimeSpan? start = TimeSpan.TryParse(submission.StartTime, CultureInfo.InvariantCulture, out var startTs) ? startTs : null;
            TimeSpan? end = TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endTs) ? endTs : null;
            var packup = endForPackup;
            SaveScheduleToSession(setup, rehearsal, start, end, packup, eventDate);
        }

        HttpContext.Session.SetString("Draft:EventFormSubmitted", "1");
    }

    private void SaveBaseAvToSession(BaseAvFormSubmission s)
    {
        HttpContext.Session.SetString("Draft:BuiltInProjector", s.BuiltInProjector ? "yes" : "no");
        HttpContext.Session.SetString("Draft:BuiltInScreen", s.BuiltInScreen ? "yes" : "no");
        HttpContext.Session.SetString("Draft:BuiltInSpeakers", s.BuiltInSpeakers ? "yes" : "no");

        // Detect combined "Inbuilt projector and screen" checkbox (e.g. Elevate rooms).
        // When this combined checkbox is unchecked, the user means "no projection at all",
        // not "I want external projection equipment".
        var venue = HttpContext.Session.GetString("Draft:VenueName");
        var room = HttpContext.Session.GetString("Draft:RoomName");
        var baseLabels = VenueRoomPackagesCache.TryGetBaseEquipmentLabels(_env, venue, room);
        var hasCombinedPS = baseLabels.Any(l =>
            l.Contains("projector", StringComparison.OrdinalIgnoreCase) &&
            l.Contains("screen", StringComparison.OrdinalIgnoreCase));
        HttpContext.Session.SetString("Draft:CombinedProjectorScreen", hasCombinedPS ? "1" : "0");

        HttpContext.Session.SetString("Draft:ProjectorPlacementChoice", s.ProjectorPlacement);

        // Ballroom: when placement is "none", user declined projection — override projector/screen flags.
        if (string.Equals(s.ProjectorPlacement?.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            && IsDraftWestinBallroomFamily(venue, room))
        {
            HttpContext.Session.SetString("Draft:BuiltInProjector", "no");
            HttpContext.Session.SetString("Draft:BuiltInScreen", "no");
        }

        HttpContext.Session.SetString("Draft:PresenterCount", s.Presenters.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:SpeakerCount", "0");
        HttpContext.Session.SetString("Draft:Flipchart", s.Flipchart);
        HttpContext.Session.SetString("Draft:LaptopMode", s.LaptopMode);
        HttpContext.Session.SetString("Draft:LaptopQty", s.LaptopQty.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:AdapterOwnLaptops", s.AdapterOwnLaptops);
        HttpContext.Session.SetString("Draft:NeedsClicker", "no");
        HttpContext.Session.SetString("Draft:NeedsRecording", "no");
        var ts = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00";
        var te = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00";
        HttpContext.Session.SetString("Draft:TechStartTime", ts);
        HttpContext.Session.SetString("Draft:TechEndTime", te);
        HttpContext.Session.SetString("Draft:TechWholeEvent", "yes");
        HttpContext.Session.SetString("Draft:BaseAvSubmitted", "1");
        HttpContext.Session.SetString("Draft:BaseAvSubmittedForThread", HttpContext.Session.GetString("AgentThreadId") ?? "");

        var roomForPlacement = HttpContext.Session.GetString("Draft:RoomName");
        var placementAreas = ParseProjectorPlacementToAllowedAreas(s.ProjectorPlacement, roomForPlacement);
        var threadId = HttpContext.Session.GetString("AgentThreadId") ?? "";
        if (placementAreas.Count > 0)
        {
            HttpContext.Session.SetString("Draft:ProjectorAreas", string.Join(",", placementAreas));
            HttpContext.Session.SetString("Draft:ProjectorArea", placementAreas[0]);
            // Mark projector area as captured so stale-session guards in both
            // AgentToolHandlerService and TryFollowUpAvQuotePipelineAsync trust these values.
            HttpContext.Session.SetString(ProjectorAreaCapturedKey, "1");
            HttpContext.Session.SetString(ProjectorAreaThreadIdKey, threadId);
            HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
            HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
        }
        else if (!string.IsNullOrWhiteSpace(s.ProjectorPlacement))
        {
            HttpContext.Session.SetString("Draft:ProjectorArea", s.ProjectorPlacement.Trim());
            HttpContext.Session.SetString(ProjectorAreaCapturedKey, "1");
            HttpContext.Session.SetString(ProjectorAreaThreadIdKey, threadId);
            HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
            HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
        }

        var mode = (s.LaptopMode ?? "").ToLowerInvariant();
        if (mode is "windows" or "mac")
        {
            HttpContext.Session.SetString("Draft:LaptopOwnershipAnswered", "1");
            HttpContext.Session.SetString("Draft:NeedsProvidedLaptop", "yes");
            HttpContext.Session.SetString("Draft:LaptopPreference", mode);
        }
        else
        {
            HttpContext.Session.SetString("Draft:LaptopOwnershipAnswered", "1");
            HttpContext.Session.SetString("Draft:NeedsProvidedLaptop", "no");
            HttpContext.Session.Remove("Draft:LaptopPreference");
        }
    }

    private void SaveFollowUpAvToSession(FollowUpAvFormSubmission s)
    {
        var presenterCount = 0;
        _ = int.TryParse(HttpContext.Session.GetString("Draft:PresenterCount"), out presenterCount);

        if (presenterCount <= 0)
        {
            HttpContext.Session.SetString("Draft:MicType", "");
            HttpContext.Session.SetString("Draft:MicQty", "0");
            HttpContext.Session.SetString("Draft:Lectern", "none");
            HttpContext.Session.SetString("Draft:FoldbackMonitor", "no");
            HttpContext.Session.SetString("Draft:WirelessPresenter", "no");
        }
        else
        {
            HttpContext.Session.SetString("Draft:MicType", s.MicType);
            HttpContext.Session.SetString("Draft:MicQty", s.MicQty.ToString(CultureInfo.InvariantCulture));
            HttpContext.Session.SetString("Draft:Lectern", s.Lectern);
            HttpContext.Session.SetString("Draft:FoldbackMonitor", s.FoldbackMonitor);
            HttpContext.Session.SetString("Draft:WirelessPresenter", s.WirelessPresenter);
        }

        HttpContext.Session.SetString("Draft:LaptopSwitcher", s.LaptopSwitcher);

        var lecternForStage = presenterCount <= 0 ? "none" : (s.Lectern ?? "").Trim();
        var lecternNotNone = lecternForStage.Length > 0
            && !string.Equals(lecternForStage, "none", StringComparison.OrdinalIgnoreCase);
        var stageQuestionApplicable = string.Equals(s.LaptopSwitcher, "yes", StringComparison.OrdinalIgnoreCase)
            && lecternNotNone;
        var stageLaptop = stageQuestionApplicable && string.Equals(s.StageLaptop, "yes", StringComparison.OrdinalIgnoreCase)
            ? "yes"
            : "no";
        s.StageLaptop = stageLaptop;
        HttpContext.Session.SetString("Draft:StageLaptop", stageLaptop);
        HttpContext.Session.SetString("Draft:VideoConference", s.VideoConference);
        HttpContext.Session.SetString("Draft:FollowUpAvSubmitted", "1");
        HttpContext.Session.SetString("Draft:AvExtrasSubmitted", "1");

        if (string.Equals(stageLaptop, "yes", StringComparison.OrdinalIgnoreCase))
            HttpContext.Session.SetString("Draft:NeedsSdiCross", "2");
        else
            HttpContext.Session.Remove("Draft:NeedsSdiCross");
    }

}
