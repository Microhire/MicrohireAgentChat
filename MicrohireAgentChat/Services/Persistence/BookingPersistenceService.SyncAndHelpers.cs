using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

public sealed partial class BookingPersistenceService
{
    private const int VenueRoomMaxLength = 35;
    private const int EventTypeMaxLength = 20;
    private const int ShowNameMaxLength = 50;
    private const int SalespersonMaxLength = 30;
    private const int CustomerCodeMaxLength = 30;

    /// <summary>
    /// Save full transcript to tblbooknote.
    /// Persists to both legacy transcript note type and Rental Point visible notes.
    /// </summary>
    public async Task SaveTranscriptAsync(
        string bookingNo,
        IEnumerable<DisplayMessage> messages,
        CancellationToken ct)
    {
        var transcript = BuildTranscriptForBooknote(messages);
        if (string.IsNullOrWhiteSpace(transcript)) return;

        await UpsertLegacyTranscriptNoteAsync(bookingNo, transcript, ct);
        await UpsertVisibleTranscriptNoteAsync(bookingNo, transcript, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sync latest session venue/room to an existing booking before quote generation.
    /// </summary>
    public async Task<bool> SyncVenueAndRoomForBookingAsync(string bookingNo, ISession? session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingNo) || session == null)
            return false;

        var booking = await _db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
        if (booking == null)
        {
            _logger.LogWarning("[QUOTE GEN] Cannot sync venue/room - booking {BookingNo} not found", bookingNo);
            return false;
        }

        var sessionVenue = session.GetString("Draft:VenueName");
        var sessionRoom = session.GetString("Draft:RoomName") ?? session.GetString("Draft:VenueRoom");
        var projectorArea = session.GetString("Draft:ProjectorArea");
        var projectorAreas = session.GetString("Draft:ProjectorAreas");
        var projectionNeeded = SessionRequiresProjectorPlacement(session);
        sessionRoom = MergeProjectorAreasIntoVenueRoom(sessionRoom, projectorAreas, projectorArea, projectionNeeded);
        if (!projectionNeeded)
        {
            // Prevent stale projector selections leaking into non-projection bookings.
            session.Remove("Draft:ProjectorArea");
            session.Remove("Draft:ProjectorAreas");
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(sessionVenue))
        {
            var venueId = await ResolveVenueIdAsync(sessionVenue, ct);
            if (venueId.HasValue && booking.VenueID != venueId)
            {
                booking.VenueID = venueId.Value;
                changed = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(sessionRoom))
        {
            var normalizedRoom = Trunc(sessionRoom, VenueRoomMaxLength);
            if (!string.Equals(booking.VenueRoom, normalizedRoom, StringComparison.Ordinal))
            {
                booking.VenueRoom = normalizedRoom;
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("[QUOTE GEN] Synced session venue/room to booking {BookingNo}", bookingNo);
        }
        return changed;
    }

    /// <summary>
    /// Sync latest session contact and organisation to an existing booking before quote generation.
    /// </summary>
    public async Task<bool> SyncContactAndOrganisationForBookingAsync(string bookingNo, ISession? session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingNo) || session == null)
            return false;

        var booking = await _db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
        if (booking == null)
        {
            _logger.LogWarning("[QUOTE GEN] Cannot sync contact/org - booking {BookingNo} not found", bookingNo);
            return false;
        }

        var contactName = session.GetString("Draft:ContactName");
        var contactEmail = session.GetString("Draft:ContactEmail");
        var contactPhone = session.GetString("Draft:ContactPhone");
        var organisation = session.GetString("Draft:Organisation");

        var res = await _contactResolution.ResolveAsync(
            contactName,
            contactEmail,
            contactPhone,
            contactPosition: null,
            organisation,
            orgAddress: null,
            ct,
            leadAuthoritative: false);

        var changed = false;

        if (res.contactId.HasValue)
        {
            if (booking.ContactID != res.contactId)
            {
                booking.ContactID = res.contactId;
                changed = true;
            }
            var normalizedName = Trunc(contactName, 35);
            if (!string.Equals(booking.contact_nameV6, normalizedName, StringComparison.Ordinal))
            {
                booking.contact_nameV6 = normalizedName;
                changed = true;
            }
        }

        if (res.orgId.HasValue)
        {
            if (booking.CustID != res.orgId)
            {
                booking.CustID = res.orgId;
                changed = true;
            }
            var orgName = Trunc(organisation, 50);
            if (!string.Equals(booking.OrganizationV6, orgName, StringComparison.Ordinal))
            {
                booking.OrganizationV6 = orgName;
                changed = true;
            }
            var normalizedOrgCode = Trunc(res.customerCode, CustomerCodeMaxLength);
            if (!string.IsNullOrWhiteSpace(normalizedOrgCode) &&
                !string.Equals(booking.CustCode, normalizedOrgCode, StringComparison.Ordinal))
            {
                booking.CustCode = normalizedOrgCode;
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("[QUOTE GEN] Synced session contact/org (no-update policy) to booking {BookingNo}", bookingNo);
        }
        return changed;
    }

    /// <summary>
    /// Progressive sync: update an existing booking with latest session data
    /// (venue, room, contact, org, event date, times, equipment).
    /// Called after every user message so the booking stays current.
    /// </summary>
    public async Task<bool> SyncBookingFromSessionAsync(string bookingNo, ISession? session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingNo) || session == null)
            return false;

        var anyChanged = false;

        try { anyChanged |= await SyncVenueAndRoomForBookingAsync(bookingNo, session, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "[PROACTIVE] Venue/room sync failed for {BookingNo}", bookingNo); }

        try { anyChanged |= await SyncContactAndOrganisationForBookingAsync(bookingNo, session, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "[PROACTIVE] Contact/org sync failed for {BookingNo}", bookingNo); }

        try { anyChanged |= await SyncEventDetailsForBookingAsync(bookingNo, session, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "[PROACTIVE] Event details sync failed for {BookingNo}", bookingNo); }

        if (anyChanged)
            _logger.LogInformation("[PROACTIVE] Synced session data to booking {BookingNo}", bookingNo);

        return anyChanged;
    }

    /// <summary>
    /// Sync event date, times, event type, and equipment from session to booking.
    /// </summary>
    private async Task<bool> SyncEventDetailsForBookingAsync(string bookingNo, ISession session, CancellationToken ct)
    {
        var booking = await _db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
        if (booking == null) return false;

        var changed = false;

        var eventDateStr = session.GetString("Draft:EventDate");
        if (!string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out var eventDate))
        {
            if (booking.dDate != eventDate)
            {
                booking.dDate = eventDate;
                booking.rDate = eventDate;
                booking.SDate = eventDate;
                booking.SetDate = eventDate;
                booking.ShowSDate = eventDate;
                booking.ShowEdate = eventDate;
                booking.RehDate = eventDate;
                changed = true;
            }
        }

        var startTime = session.GetString("Draft:StartTime");
        if (!string.IsNullOrWhiteSpace(startTime))
        {
            var padded = PadTimeHHmm(startTime);
            if (!string.Equals(booking.showStartTime, padded, StringComparison.Ordinal))
            { booking.showStartTime = padded; changed = true; }
        }

        var endTime = session.GetString("Draft:EndTime");
        if (!string.IsNullOrWhiteSpace(endTime))
        {
            var padded = PadTimeHHmm(endTime);
            if (!string.Equals(booking.ShowEndTime, padded, StringComparison.Ordinal))
            { booking.ShowEndTime = padded; changed = true; }
        }

        var setupTime = session.GetString("Draft:SetupTime");
        if (!string.IsNullOrWhiteSpace(setupTime))
        {
            var padded = PadTimeHHmm(setupTime);
            if (!string.Equals(booking.setupTimeV61, padded, StringComparison.Ordinal))
            { booking.setupTimeV61 = padded; changed = true; }
        }

        var rehearsalTime = session.GetString("Draft:RehearsalTime");
        if (!string.IsNullOrWhiteSpace(rehearsalTime))
        {
            var padded = PadTimeHHmm(rehearsalTime);
            if (!string.Equals(booking.RehearsalTime, padded, StringComparison.Ordinal))
            { booking.RehearsalTime = padded; changed = true; }
        }

        var eventType = session.GetString("Draft:EventType");
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var truncated = Trunc(eventType, EventTypeMaxLength);
            if (!string.Equals(booking.EventType, truncated, StringComparison.Ordinal))
            { booking.EventType = truncated; changed = true; }
        }

        if (changed)
            await _db.SaveChangesAsync(ct);

        return changed;
    }

    /// <summary>
    /// Sync selected labor payload from session into tblcrew for an existing booking.
    /// </summary>
    public async Task<bool> SyncLaborFromSessionForBookingAsync(string bookingNo, ISession? session, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bookingNo) || session == null)
        {
            return false;
        }

        var selectedLaborJson = session.GetString("Draft:SelectedLabor");
        if (string.IsNullOrWhiteSpace(selectedLaborJson))
        {
            return false;
        }

        List<SelectedLaborItem>? laborItems;
        try
        {
            laborItems = JsonSerializer.Deserialize<List<SelectedLaborItem>>(
                selectedLaborJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[QUOTE GEN] Failed to parse Draft:SelectedLabor for booking {BookingNo}", bookingNo);
            return false;
        }

        if (laborItems == null || laborItems.Count == 0)
        {
            _logger.LogInformation("[QUOTE GEN] Draft:SelectedLabor is empty for booking {BookingNo}", bookingNo);
            return false;
        }

        var booking = await _db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
        if (booking == null)
        {
            _logger.LogWarning("[QUOTE GEN] Cannot sync labor - booking {BookingNo} not found", bookingNo);
            return false;
        }

        var facts = new Dictionary<string, string>
        {
            ["selected_labor"] = selectedLaborJson,
            ["labor_summary"] = BuildLaborSummaryFromSelectedLabor(laborItems)
        };

        var eventDateStr = session.GetString("Draft:EventDate");
        if (!string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out var eventDate))
        {
            facts["event_date"] = eventDate.ToString("yyyy-MM-dd");
        }
        else if (booking.dDate.HasValue)
        {
            facts["event_date"] = booking.dDate.Value.ToString("yyyy-MM-dd");
        }

        var startTime = session.GetString("Draft:StartTime");
        if (!string.IsNullOrWhiteSpace(startTime))
        {
            facts["show_start_time"] = PadTimeHHmm(startTime);
        }
        else if (!string.IsNullOrWhiteSpace(booking.showStartTime))
        {
            facts["show_start_time"] = booking.showStartTime!;
        }

        var endTime = session.GetString("Draft:EndTime");
        if (!string.IsNullOrWhiteSpace(endTime))
        {
            facts["show_end_time"] = PadTimeHHmm(endTime);
        }
        else if (!string.IsNullOrWhiteSpace(booking.ShowEndTime))
        {
            facts["show_end_time"] = booking.ShowEndTime!;
        }

        var setupTime = session.GetString("Draft:SetupTime");
        if (!string.IsNullOrWhiteSpace(setupTime))
        {
            facts["setup_time"] = PadTimeHHmm(setupTime);
        }
        else if (!string.IsNullOrWhiteSpace(booking.setupTimeV61))
        {
            facts["setup_time"] = booking.setupTimeV61!;
        }

        var rehearsalTime = session.GetString("Draft:RehearsalTime");
        if (!string.IsNullOrWhiteSpace(rehearsalTime))
        {
            facts["rehearsal_time"] = PadTimeHHmm(rehearsalTime);
        }
        else if (!string.IsNullOrWhiteSpace(booking.RehearsalTime))
        {
            facts["rehearsal_time"] = booking.RehearsalTime!;
        }

        var packupTime = session.GetString("Draft:PackupTime");
        if (!string.IsNullOrWhiteSpace(packupTime))
        {
            facts["packup_time"] = PadTimeHHmm(packupTime);
        }

        await _crewService.InsertCrewRowsAsync(bookingNo, facts, ct);
        _logger.LogInformation("[QUOTE GEN] Synced {Count} labor items to booking {BookingNo}", laborItems.Count, bookingNo);
        return true;
    }

    // ==================== PRIVATE HELPERS ====================

    private static string BuildLaborSummaryFromSelectedLabor(IEnumerable<SelectedLaborItem> laborItems)
    {
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

    private static DateTime NowAest()
    {
#if WINDOWS
        var tz = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
#else
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
#endif
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    private async Task<string> GenerateBookingNumberAsync(CancellationToken ct)
    {
        var now = NowAest();
        var fiscalYear = now.Month >= 7 ? now.Year : now.Year - 1;
        var yearShort = fiscalYear % 100;

        var prefix = $"{yearShort:D2}";
        var lastBooking = await _db.TblBookings
            .Where(b => b.booking_no != null && b.booking_no.StartsWith(prefix))
            .OrderByDescending(b => b.booking_no)
            .Select(b => b.booking_no)
            .FirstOrDefaultAsync(ct);

        int nextSeq = 1;
        if (lastBooking != null && lastBooking.Length >= 6)
        {
            var seqPart = lastBooking.Substring(2, 4);
            if (int.TryParse(seqPart, out var seq))
                nextSeq = seq + 1;
        }

        return $"{yearShort:D2}{nextSeq:D4}";
    }

    private async Task<string?> GetOrganizationNameAsync(decimal orgId, CancellationToken ct)
    {
        var org = await _db.TblCusts
            .Where(c => c.ID == orgId)
            .Select(c => c.OrganisationV6)
            .FirstOrDefaultAsync(ct);
        return org;
    }

    private async Task<int?> ResolveVenueIdAsync(string? venueName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(venueName))
            return null;

        var normalizedVenue = venueName.ToLowerInvariant().Trim();

        // Exact contains search against tblVenues
        var venue = await _db.TblVenues
            .Where(v => v.VenueName != null && v.VenueName.ToLower().Contains(normalizedVenue))
            .Select(v => new { v.ID })
            .FirstOrDefaultAsync(ct);
        if (venue != null)
            return (int)venue.ID;

        // Try partial keyword matching for known venues (e.g. "westin brisbane" → "The Westin Brisbane")
        var keywords = normalizedVenue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (keywords.Length >= 2)
        {
            var candidates = _db.TblVenues.Where(v => v.VenueName != null);
            foreach (var kw in keywords)
            {
                var kwLocal = kw;
                candidates = candidates.Where(v => v.VenueName!.ToLower().Contains(kwLocal));
            }
            var match = await candidates.Select(v => new { v.ID }).FirstOrDefaultAsync(ct);
            if (match != null)
                return (int)match.ID;
        }

        _logger.LogWarning("[VENUE RESOLVE] Could not find venue '{VenueName}' in tblVenues. Returning null.", venueName);
        return null;
    }

    /// <summary>
    /// Get a default customer ID for bookings without organization.
    /// First tries to find "General Enquiry" or similar, falls back to ID 1.
    /// </summary>
    private async Task<decimal?> GetDefaultCustomerIdAsync(CancellationToken ct)
    {
        // Try to find a general/default customer
        var defaultOrg = await _db.TblCusts
            .Where(c => c.OrganisationV6 != null && 
                   (c.OrganisationV6.ToLower().Contains("general") ||
                    c.OrganisationV6.ToLower().Contains("enquiry") ||
                    c.OrganisationV6.ToLower().Contains("walk-in") ||
                    c.OrganisationV6.ToLower().Contains("default")))
            .Select(c => c.ID)
            .FirstOrDefaultAsync(ct);

        if (defaultOrg != 0)
            return defaultOrg;

        // Fall back to first customer ID or 1
        var firstCust = await _db.TblCusts
            .OrderBy(c => c.ID)
            .Select(c => c.ID)
            .FirstOrDefaultAsync(ct);

        return firstCust != 0 ? firstCust : 1;
    }

    /// <summary>
    /// Get customer code by ID
    /// </summary>
    private async Task<string?> GetCustomerCodeByIdAsync(decimal custId, CancellationToken ct)
    {
        var code = await _db.TblCusts
            .Where(c => c.ID == custId)
            .Select(c => c.Customer_code)
            .FirstOrDefaultAsync(ct);
        return code;
    }

    private static string? GetFact(Dictionary<string, string> facts, string key)
    {
        if (facts.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val.Trim();
        return null;
    }

    private static DateTime? ExtractDate(Dictionary<string, string> facts, string key)
    {
        var val = GetFact(facts, key);
        if (val == null) return null;

        if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        return null;
    }

    private static DateTime? ExtractDateTime(Dictionary<string, string> facts, string key)
    {
        return ExtractDate(facts, key);
    }

    private static TimeSpan? ExtractTime(Dictionary<string, string> facts, string key)
    {
        var val = GetFact(facts, key);
        if (val == null) return null;

        // Try HH:mm format
        if (TimeSpan.TryParseExact(val, @"hh\:mm", CultureInfo.InvariantCulture, out var ts))
            return ts;

        // Try H:mm format
        if (TimeSpan.TryParseExact(val, @"h\:mm", CultureInfo.InvariantCulture, out var ts2))
            return ts2;

        return null;
    }

    private static decimal? ParseDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        val = Regex.Replace(val, @"[^\d\.]", ""); // strip currency symbols
        if (decimal.TryParse(val, out var d)) return d;
        return null;
    }

    private static int? MinutesFromMidnight(TimeSpan? ts)
    {
        if (!ts.HasValue) return null;
        return (int)ts.Value.TotalMinutes;
    }

    /// <summary>
    /// Convert TimeSpan to 4-digit HHmm string format.
    /// PER GUIDE: Format time so RP can read it properly - use "0930" not "930"
    /// </summary>
    private static string? ToHHmmString(TimeSpan? ts)
    {
        if (!ts.HasValue) return null;
        // D2 ensures 2-digit formatting with leading zeros: 09:30 → "0930"
        return $"{ts.Value.Hours:D2}{ts.Value.Minutes:D2}";
    }

    /// <summary>
    /// Zero-pad time string to 4-digit HHmm for RP (e.g. "9:30" → "0930").
    /// </summary>
    private static string PadTimeHHmm(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var digits = raw.Replace(":", "").Trim();
        return digits.Length >= 4 ? digits : digits.PadLeft(4, '0');
    }

    private static int TryParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return 1;
        // Map common status strings to byte values
        return status.ToLowerInvariant() switch
        {
            "enquiry" or "quote" or "light pencil" => 1,
            "heavy pencil" => 2,
            "confirmed" => 3,
            "completed" => 4,
            _ => 1 // default to enquiry
        };
    }

    private static int? ParseInt(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        val = Regex.Replace(val, @"[^\d]", ""); // strip non-numeric
        if (int.TryParse(val, out var i)) return i;
        return null;
    }

    private static byte? ParseByte(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        if (byte.TryParse(val, out var b)) return b;
        return null;
    }

    private static string? Trunc(string? s, int len)
        => string.IsNullOrWhiteSpace(s) ? s : (s!.Length <= len ? s : s[..len]);

    private static string? MergeProjectorAreasIntoVenueRoom(
        string? venueRoom,
        string? projectorAreasRaw,
        string? projectorAreaRaw,
        bool includeProjectorAreas = true)
    {
        // Projector placement areas are tracked in dedicated session/fact keys.
        // The room name field must stay clean — never embed projector area labels in it.
        return Trunc(NormalizeVenueRoomWithoutProjectorAreas(venueRoom), VenueRoomMaxLength);
    }

    private static string? NormalizeVenueRoomWithoutProjectorAreas(string? venueRoom)
    {
        if (string.IsNullOrWhiteSpace(venueRoom))
            return venueRoom;

        var normalized = venueRoom.Trim();
        normalized = Regex.Replace(
            normalized,
            @"\s*-\s*Projector\s+Area(?:s)?\s*$",
            "",
            RegexOptions.IgnoreCase);
        normalized = Regex.Replace(
            normalized,
            @"\s*-\s*Projector\s+Area(?:s)?\s+[A-F](?:/[A-F])*$",
            "",
            RegexOptions.IgnoreCase);
        normalized = Regex.Replace(
            normalized,
            @"\s*\(Proj(?:ector)?\s+[A-F](?:/[A-F])*\)$",
            "",
            RegexOptions.IgnoreCase);
        return normalized.Trim();
    }

    private static bool SessionRequiresProjectorPlacement(ISession session)
    {
        return TextContainsProjectionKeyword(session.GetString("Draft:SummaryEquipmentRequests"))
            || TextContainsProjectionKeyword(session.GetString("Draft:SelectedEquipment"));
    }

    private static bool FactsRequireProjectorPlacement(IReadOnlyDictionary<string, string> facts)
    {
        if (facts.Count == 0) return false;
        return TextContainsProjectionKeyword(TryGetFactValue(facts, "selected_equipment"))
            || TextContainsProjectionKeyword(TryGetFactValue(facts, "summary_equipment_requests"))
            || TextContainsProjectionKeyword(TryGetFactValue(facts, "equipment_requests"))
            || TextContainsProjectionKeyword(TryGetFactValue(facts, "equipment_requests_json"));
    }

    private static string? TryGetFactValue(IReadOnlyDictionary<string, string> facts, string key)
    {
        if (!facts.TryGetValue(key, out var value)) return null;
        return value;
    }

    private static bool TextContainsProjectionKeyword(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("projector", StringComparison.OrdinalIgnoreCase)
            || text.Contains("screen", StringComparison.OrdinalIgnoreCase)
            || text.Contains("display", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTranscript(IEnumerable<DisplayMessage> messages)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== CONVERSATION TRANSCRIPT ===");
        sb.AppendLine();

        foreach (var msg in messages)
        {
            var role = msg.Role == "user" ? "USER" : "ASSISTANT";
            var timestamp = msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            sb.AppendLine($"[{timestamp}] {role}:");
            sb.AppendLine(msg.FullText);
            sb.AppendLine();
        }

        return sb.ToString();
    }


}
