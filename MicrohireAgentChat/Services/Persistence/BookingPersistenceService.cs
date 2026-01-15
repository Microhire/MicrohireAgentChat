using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Handles booking persistence to tblbookings table
/// </summary>
public sealed class BookingPersistenceService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<BookingPersistenceService> _logger;

    public BookingPersistenceService(BookingDbContext db, ILogger<BookingPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Save or update booking record.
    /// Schema: tblbookings with columns (key ones):
    /// - booking_no (varchar 20) PK
    /// - order_no (varchar 20)
    /// - SDate (datetime) - event start date
    /// - RDate (datetime) - return date
    /// - SetDate (datetime) - setup date
    /// - ShowSdate (datetime) - show start datetime
    /// - ShowEdate (datetime) - show end datetime
    /// - RehDate (datetime) - rehearsal date
    /// - showStartTime (int) - minutes from midnight
    /// - ShowEndTime (int) - minutes from midnight
    /// - setupTimeV61 (int) - minutes from midnight
    /// - RehearsalTime (int) - minutes from midnight
    /// - StrikeTime (int) - minutes from midnight
    /// - VenueRoom (varchar 50)
    /// - EventType (varchar 35)
    /// - price_quoted (decimal 19,4)
    /// - hire_price (decimal 19,4)
    /// - labour (decimal 19,4)
    /// - insurance_v5 (decimal 19,4)
    /// - CustID (decimal 10,0) - FK to tblcust.ID
    /// - VenueID (decimal 10,0) - FK to tblcust.ID (venue)
    /// - ContactID (decimal 10,0) - FK to tblContact.ID
    /// - OrganizationV6 (varchar 50)
    /// - Salesperson (varchar 50)
    /// - BookingProgressStatus (varchar 30)
    /// </summary>
    public async Task<string?> SaveBookingAsync(
        string? existingBookingNo,
        Dictionary<string, string> facts,
        decimal? contactId,
        decimal? organizationId,
        string? customerCode,
        string? contactName,
        CancellationToken ct)
    {
        try
        {
            var now = NowAest();
            var bookingNo = existingBookingNo ?? await GenerateBookingNumberAsync(ct);

            // Extract structured data from facts
            var eventDate = ExtractDate(facts, "event_date");
            var showStart = ExtractDateTime(facts, "show_start");
            var showEnd = ExtractDateTime(facts, "show_end");
            var setupTime = ExtractTime(facts, "setup_time");
            var strikeTime = ExtractTime(facts, "strike_time");
            var rehearsalTime = ExtractTime(facts, "rehearsal_time");
            var showStartTime = ExtractTime(facts, "show_start_time");
            var showEndTime = ExtractTime(facts, "show_end_time");

            var venueName = GetFact(facts, "venue_name");
            var venueRoom = GetFact(facts, "venue_room");
            var eventType = GetFact(facts, "event_type");
            var showName = GetFact(facts, "show_name") ?? GetFact(facts, "event_type");
            var salesperson = GetFact(facts, "salesperson") ?? "Isla";
            var attendees = ParseInt(GetFact(facts, "expected_attendees"));

            // Financial fields
            var priceQuoted = ParseDecimal(GetFact(facts, "price_quoted"));
            var hirePrice = ParseDecimal(GetFact(facts, "hire_price"));
            var labour = ParseDecimal(GetFact(facts, "labour"));
            var insurance = ParseDecimal(GetFact(facts, "insurance"));
            var tax2 = ParseDecimal(GetFact(facts, "gst"));

            // Status
            var status = GetFact(facts, "booking_status") ?? "Enquiry";
            var bookingType = ParseByte(GetFact(facts, "booking_type")) ?? (byte)2; // 2 = Quote/Booking

            // Venue lookup - check tblVenues for venue ID
            int? venueId = null; // null means don't change existing venue
            if (!string.IsNullOrWhiteSpace(venueName))
            {
                // Check if it's Westin Brisbane (our primary supported venue)
                var normalizedVenue = venueName.ToLower().Trim();
                if (normalizedVenue.Contains("westin") && normalizedVenue.Contains("brisbane"))
                {
                    // Use Westin Brisbane ID directly - this is our primary partner venue
                    venueId = 20; // The Westin Brisbane ID
                }
                else
                {
                    // Look up in tblVenues by name
                    var venue = await _db.TblVenues
                        .Where(v => v.VenueName != null && v.VenueName.ToLower().Contains(normalizedVenue))
                        .Select(v => new { v.ID })
                        .FirstOrDefaultAsync(ct);
                    
                    if (venue != null)
                    {
                        venueId = (int)venue.ID;
                    }
                    else
                    {
                        // Fallback: check tblCusts for venue
                        var custVenue = await _db.TblCusts
                            .Where(c => c.OrganisationV6 != null && c.OrganisationV6.ToLower().Contains(normalizedVenue))
                            .Select(c => new { c.ID })
                            .FirstOrDefaultAsync(ct);
                        venueId = custVenue != null ? (int)custVenue.ID : 20; // Default to Westin Brisbane
                    }
                }
            }

            var existing = await _db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);

            // CRITICAL: CustID is required by the database - get default if not provided
            var finalCustId = organizationId ?? await GetDefaultCustomerIdAsync(ct);
            var finalCustCode = customerCode ?? (finalCustId.HasValue ? await GetCustomerCodeByIdAsync(finalCustId.Value, ct) : null);

            if (existing == null)
            {
                // CREATE new booking
                var booking = new TblBooking
                {
                    booking_no = bookingNo,
                    order_no = bookingNo, // default same as booking_no
                    booking_type_v32 = bookingType,
                    status = 0, // 0 = enquiry/quote
                    BookingProgressStatus = (byte)TryParseStatus(status),
                    bBookingIsComplete = false,
                    
                    // Dates (CRITICAL: must set dDate and rDate per guide)
                    dDate = eventDate, // CRITICAL: Delivery date (OUT date in UI)
                    rDate = eventDate, // CRITICAL: Return date (IN date in UI)
                    SDate = eventDate,
                    SetDate = eventDate,
                    ShowSDate = showStart ?? eventDate,
                    ShowEdate = showEnd ?? eventDate,
                    RehDate = eventDate,
                    order_date = now, // CRITICAL: must set or shows as 1980
                    EntryDate = now,
                    
                    // Times (HHmm format as strings)
                    showStartTime = ToHHmmString(showStartTime),
                    ShowEndTime = ToHHmmString(showEndTime),
                    setupTimeV61 = ToHHmmString(setupTime),
                    RehearsalTime = ToHHmmString(rehearsalTime),
                    StrikeTime = ToHHmmString(strikeTime),
                    del_time_h = (byte?)setupTime?.Hours,
                    del_time_m = (byte?)setupTime?.Minutes,
                    ret_time_h = (byte?)strikeTime?.Hours,
                    ret_time_m = (byte?)strikeTime?.Minutes,
                    
                    // Venue & Event
                    VenueRoom = Trunc(venueRoom, 50),
                    EventType = Trunc(eventType, 35),
                    showName = Trunc(showName, 100),
                    expAttendees = attendees,
                    VenueID = venueId ?? 1,
                    
                    // Financial
                    price_quoted = (double?)priceQuoted,
                    hire_price = (double?)hirePrice,
                    labour = (double?)labour,
                    insurance_v5 = (double?)insurance,
                    sundry_total = (double?)insurance, // service charge
                    Tax2 = (double?)tax2,
                    days_using = 1,
                    
                    // Customer/Contact (CRITICAL: CustID cannot be NULL)
                    CustID = finalCustId,
                    CustCode = finalCustCode,
                    ContactID = contactId,
                    contact_nameV6 = Trunc(contactName, 35),
                    OrganizationV6 = finalCustId.HasValue
                        ? await GetOrganizationNameAsync(finalCustId.Value, ct)
                        : null,
                    Salesperson = Trunc(salesperson, 50),
                    
                    // Defaults
                    From_locn = 20,
                    Trans_to_locn = 20,
                    return_to_locn = 20,
                    invoiced = "N",
                    perm_casual = "Y",
                    TaxAuthority1 = 0,
                    TaxAuthority2 = 1
                };

                _db.TblBookings.Add(booking);
            }
            else
            {
                // UPDATE existing booking
                if (eventDate.HasValue)
                {
                    existing.dDate = eventDate; // CRITICAL: Delivery date
                    existing.rDate = eventDate; // CRITICAL: Return date
                    existing.SDate = eventDate;
                    existing.SetDate = eventDate;
                    existing.RehDate = eventDate;
                }
                if (showStart.HasValue) existing.ShowSDate = showStart;
                if (showEnd.HasValue) existing.ShowEdate = showEnd;
                
                if (setupTime.HasValue)
                {
                    existing.setupTimeV61 = ToHHmmString(setupTime);
                    existing.del_time_h = (byte?)setupTime.Value.Hours;
                    existing.del_time_m = (byte?)setupTime.Value.Minutes;
                }
                if (strikeTime.HasValue)
                {
                    existing.StrikeTime = ToHHmmString(strikeTime);
                    existing.ret_time_h = (byte?)strikeTime.Value.Hours;
                    existing.ret_time_m = (byte?)strikeTime.Value.Minutes;
                }
                if (rehearsalTime.HasValue) existing.RehearsalTime = ToHHmmString(rehearsalTime);
                if (showStartTime.HasValue) existing.showStartTime = ToHHmmString(showStartTime);
                if (showEndTime.HasValue) existing.ShowEndTime = ToHHmmString(showEndTime);

                if (!string.IsNullOrWhiteSpace(venueRoom)) existing.VenueRoom = Trunc(venueRoom, 50);
                if (!string.IsNullOrWhiteSpace(eventType)) existing.EventType = Trunc(eventType, 35);
                if (!string.IsNullOrWhiteSpace(showName)) existing.showName = Trunc(showName, 100);
                if (!string.IsNullOrWhiteSpace(salesperson)) existing.Salesperson = Trunc(salesperson, 50);
                if (!string.IsNullOrWhiteSpace(status)) existing.BookingProgressStatus = (byte)TryParseStatus(status);
                if (attendees.HasValue) existing.expAttendees = attendees;

                if (priceQuoted.HasValue) existing.price_quoted = (double?)priceQuoted;
                if (hirePrice.HasValue) existing.hire_price = (double?)hirePrice;
                if (labour.HasValue) existing.labour = (double?)labour;
                if (insurance.HasValue)
                {
                    existing.insurance_v5 = (double?)insurance;
                    existing.sundry_total = (double?)insurance;
                }
                if (tax2.HasValue) existing.Tax2 = (double?)tax2;

                if (contactId.HasValue) existing.ContactID = contactId;
                if (!string.IsNullOrWhiteSpace(contactName)) existing.contact_nameV6 = Trunc(contactName, 35);
                if (!string.IsNullOrWhiteSpace(finalCustCode)) existing.CustCode = finalCustCode;
                if (finalCustId.HasValue)
                {
                    existing.CustID = finalCustId;
                    existing.OrganizationV6 = await GetOrganizationNameAsync(finalCustId.Value, ct);
                }
                if (venueId.HasValue) existing.VenueID = venueId.Value;
            }

            await _db.SaveChangesAsync(ct);
            return bookingNo;
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException($"Failed to save booking: {detail}", ex);
        }
    }

    /// <summary>
    /// Save full transcript to tblbooknote
    /// Schema: booking_no_v32 (varchar 20), booknote (text), log_dt (datetime)
    /// </summary>
    public async Task SaveTranscriptAsync(
        string bookingNo,
        IEnumerable<DisplayMessage> messages,
        CancellationToken ct)
    {
        var transcript = BuildTranscript(messages);
        if (string.IsNullOrWhiteSpace(transcript)) return;

        var noteRow = new TblBooknote
        {
            BookingNo = bookingNo,
            TextLine = transcript,
            NoteType = 1 // 1 = transcript
        };

        _db.Set<TblBooknote>().Add(noteRow);
        await _db.SaveChangesAsync(ct);
    }

    // ==================== PRIVATE HELPERS ====================

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

    private static int TryParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return 1;
        // Map common status strings to byte values
        return status.ToLowerInvariant() switch
        {
            "enquiry" or "quote" => 1,
            "confirmed" => 2,
            "completed" => 3,
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

