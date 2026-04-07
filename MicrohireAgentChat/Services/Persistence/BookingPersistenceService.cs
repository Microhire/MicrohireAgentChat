using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicrohireAgentChat.Services;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Handles booking persistence to tblbookings table
/// </summary>
public sealed partial class BookingPersistenceService
{
    private readonly BookingDbContext _db;
    private readonly ChatExtractionService _chatExtraction;
    private readonly ItemPersistenceService _itemService;
    private readonly CrewPersistenceService _crewService;
    private readonly ContactResolutionService _contactResolution;
    private readonly OrganizationPersistenceService _orgService;
    private readonly RentalPointDefaultsOptions _rpDefaults;
    private readonly ILogger<BookingPersistenceService> _logger;

    public BookingPersistenceService(
        BookingDbContext db,
        ChatExtractionService chatExtraction,
        ItemPersistenceService itemService,
        CrewPersistenceService crewService,
        ContactResolutionService contactResolution,
        OrganizationPersistenceService orgService,
        IOptions<RentalPointDefaultsOptions> rpDefaults,
        ILogger<BookingPersistenceService> logger)
    {
        _db = db;
        _chatExtraction = chatExtraction;
        _itemService = itemService;
        _crewService = crewService;
        _contactResolution = contactResolution;
        _orgService = orgService;
        _rpDefaults = rpDefaults.Value;
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
        MultiDayEventDetails? multiDayDetails,
        CancellationToken ct)
    {
        try
        {
            var now = NowAest();

            // Handle multi-day events by creating multiple booking records
            if (multiDayDetails != null && multiDayDetails.DurationDays > 1)
            {
                return await SaveMultiDayBookingAsync(
                    existingBookingNo,
                    facts,
                    contactId,
                    organizationId,
                    customerCode,
                    contactName,
                    multiDayDetails,
                    now,
                    ct);
            }

            // Single-day booking: resolve customer before generating booking number (CustomerCode + 5-digit sequence)
            var finalCustId = organizationId ?? await GetDefaultCustomerIdAsync(ct);
            var resolvedCustCode = customerCode
                ?? (finalCustId.HasValue ? await _orgService.GetCustomerCodeByIdAsync(finalCustId.Value, ct) : null);
            var bookingNo = existingBookingNo ?? await GenerateNextBookingNoAsync(resolvedCustCode!, ct);
            var finalCustCode = Trunc(resolvedCustCode, CustomerCodeMaxLength);

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
            var projectorArea = GetFact(facts, "projector_area");
            var projectorAreas = GetFact(facts, "projector_areas");
            var projectionNeeded = FactsRequireProjectorPlacement(facts);
            venueRoom = MergeProjectorAreasIntoVenueRoom(venueRoom, projectorAreas, projectorArea, projectionNeeded);
            var eventType = GetFact(facts, "event_type");
            var showName = GetFact(facts, "show_name") ?? GetFact(facts, "event_type");
            var salesperson = GetFact(facts, "salesperson") ?? _rpDefaults.Salesperson;
            var attendees = ParseInt(GetFact(facts, "expected_attendees"));

            // Financial fields
            var priceQuoted = ParseDecimal(GetFact(facts, "price_quoted"));
            var hirePrice = ParseDecimal(GetFact(facts, "hire_price"));
            var labour = ParseDecimal(GetFact(facts, "labour"));
            var insurance = ParseDecimal(GetFact(facts, "insurance"));
            var tax2 = ParseDecimal(GetFact(facts, "gst"));

            // Default 10% service charge on hire_price when not explicitly provided
            if (!insurance.HasValue && hirePrice.HasValue && hirePrice.Value > 0)
                insurance = Math.Round(hirePrice.Value * 0.10m, 2);

            // Status
            var statusFact = GetFact(facts, "booking_status");
            var initialStatus = statusFact ?? "Enquiry";
            var bookingType = ParseByte(GetFact(facts, "booking_type")) ?? (byte)2; // 2 = Quote/Booking

            // Venue lookup - check tblVenues (with safe fallback)
            int? venueId = await ResolveVenueIdAsync(venueName, ct);

            var existing = await _db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);

            if (existing == null)
            {
                // CREATE new booking
                var booking = new TblBooking
                {
                    booking_no = bookingNo,
                    order_no = bookingNo, // default same as booking_no
                    booking_type_v32 = bookingType,
                    status = 0, // 0 = enquiry/quote
                    BookingProgressStatus = (byte)TryParseStatus(initialStatus),
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
                    VenueRoom = Trunc(venueRoom, VenueRoomMaxLength),
                    EventType = Trunc(eventType, EventTypeMaxLength),
                    showName = Trunc(showName, ShowNameMaxLength),
                    expAttendees = attendees,
                    VenueID = venueId ?? 20,
                    
                    // Financial
                    price_quoted = (double?)priceQuoted,
                    hire_price = (double?)hirePrice,
                    labour = (double?)labour,
                    insurance_v5 = (double?)insurance,
                    insurance_type = 1, // default 10% service charge
                    Tax2 = (double?)tax2,
                    days_using = 1,
                    
                    // Customer/Contact (CRITICAL: CustID cannot be NULL)
                    CustID = finalCustId,
                    CustCode = finalCustCode,
                    ContactID = contactId,
                    contact_nameV6 = Trunc(contactName, 35),
                    OrganizationV6 = finalCustId.HasValue
                        ? Trunc(await GetOrganizationNameAsync(finalCustId.Value, ct), 50)
                        : null,
                    Salesperson = Trunc(salesperson, SalespersonMaxLength),
                    
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

                if (!string.IsNullOrWhiteSpace(venueRoom)) existing.VenueRoom = Trunc(venueRoom, VenueRoomMaxLength);
                if (!string.IsNullOrWhiteSpace(eventType)) existing.EventType = Trunc(eventType, EventTypeMaxLength);
                if (!string.IsNullOrWhiteSpace(showName)) existing.showName = Trunc(showName, ShowNameMaxLength);
                if (!string.IsNullOrWhiteSpace(salesperson)) existing.Salesperson = Trunc(salesperson, SalespersonMaxLength);
                if (!string.IsNullOrWhiteSpace(statusFact))
                {
                    var parsedStatus = (byte)TryParseStatus(statusFact);
                    var currentStatus = existing.BookingProgressStatus ?? 0;

                    // Once quote is accepted/signed (heavy pencil or above), avoid accidental downgrades.
                    if (currentStatus >= 2 && parsedStatus < 2)
                    {
                        _logger.LogInformation(
                            "Skipped BookingProgressStatus downgrade for {BookingNo}: current={Current}, incoming={Incoming}",
                            bookingNo, currentStatus, parsedStatus);
                    }
                    else
                    {
                        existing.BookingProgressStatus = parsedStatus;
                    }
                }
                if (attendees.HasValue) existing.expAttendees = attendees;

                if (priceQuoted.HasValue) existing.price_quoted = (double?)priceQuoted;
                if (hirePrice.HasValue) existing.hire_price = (double?)hirePrice;
                if (labour.HasValue) existing.labour = (double?)labour;
                if (insurance.HasValue)
                {
                    existing.insurance_v5 = (double?)insurance;
                    existing.insurance_type = (byte)(insurance.Value > 0 ? 1 : 0);
                }
                if (tax2.HasValue) existing.Tax2 = (double?)tax2;

                if (contactId.HasValue) existing.ContactID = contactId;
                if (!string.IsNullOrWhiteSpace(contactName)) existing.contact_nameV6 = Trunc(contactName, 35);
                if (!string.IsNullOrWhiteSpace(finalCustCode)) existing.CustCode = Trunc(finalCustCode, CustomerCodeMaxLength);
                if (finalCustId.HasValue)
                {
                    existing.CustID = finalCustId;
                    existing.OrganizationV6 = Trunc(await GetOrganizationNameAsync(finalCustId.Value, ct), 50);
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
    /// Save multi-day booking by creating multiple booking records
    /// </summary>
    private async Task<string?> SaveMultiDayBookingAsync(
        string? existingBookingNo,
        Dictionary<string, string> facts,
        decimal? contactId,
        decimal? organizationId,
        string? customerCode,
        string? contactName,
        MultiDayEventDetails multiDayDetails,
        DateTime now,
        CancellationToken ct)
    {
        // Resolve customer then generate base booking number (CustomerCode + 5-digit sequence)
        var finalCustId = organizationId ?? await GetDefaultCustomerIdAsync(ct);
        var resolvedCustCode = customerCode
            ?? (finalCustId.HasValue ? await _orgService.GetCustomerCodeByIdAsync(finalCustId.Value, ct) : null);
        var baseBookingNo = existingBookingNo ?? await GenerateNextBookingNoAsync(resolvedCustCode!, ct);
        var firstDayBookingNo = baseBookingNo;
        var finalCustCode = Trunc(resolvedCustCode, CustomerCodeMaxLength);

        // Extract common data that applies to all days
        var venueName = GetFact(facts, "venue_name");
        var venueRoom = GetFact(facts, "venue_room");
        var projectorArea = GetFact(facts, "projector_area");
        var projectorAreas = GetFact(facts, "projector_areas");
        var projectionNeeded = FactsRequireProjectorPlacement(facts);
        venueRoom = MergeProjectorAreasIntoVenueRoom(venueRoom, projectorAreas, projectorArea, projectionNeeded);
        var eventType = GetFact(facts, "event_type");
        var showName = GetFact(facts, "show_name") ?? GetFact(facts, "event_type");
        var salesperson = GetFact(facts, "salesperson") ?? _rpDefaults.Salesperson;
        var attendees = ParseInt(GetFact(facts, "expected_attendees"));

        // Financial fields (shared across all days)
        var priceQuoted = ParseDecimal(GetFact(facts, "price_quoted"));
        var hirePrice = ParseDecimal(GetFact(facts, "hire_price"));
        var labour = ParseDecimal(GetFact(facts, "labour"));
        var insurance = ParseDecimal(GetFact(facts, "insurance"));
        var tax2 = ParseDecimal(GetFact(facts, "gst"));

        // Default 10% service charge on hire_price when not explicitly provided
        if (!insurance.HasValue && hirePrice.HasValue && hirePrice.Value > 0)
            insurance = Math.Round(hirePrice.Value * 0.10m, 2);

        var status = GetFact(facts, "booking_status") ?? "Enquiry";
        var bookingType = ParseByte(GetFact(facts, "booking_type")) ?? (byte)2;

        // Venue lookup
        int? venueId = await ResolveVenueIdAsync(venueName, ct);

        // Create booking record for each day
        for (int dayNumber = 1; dayNumber <= multiDayDetails.DurationDays; dayNumber++)
        {
            var dayBookingNo = dayNumber == 1 ? firstDayBookingNo : $"{baseBookingNo}-D{dayNumber}";
            var dayDetails = multiDayDetails.GetDayDetails(dayNumber);

            // Extract day-specific data
            var dayDate = multiDayDetails.StartDate.AddDays(dayNumber - 1);
            var daySetupStyle = dayDetails?.SetupStyle;
            var dayStartTime = dayDetails?.StartTime;
            var dayEndTime = dayDetails?.EndTime;
            var daySpecialNotes = dayDetails?.SpecialNotes;

            // For multi-day events, distribute financial amounts across days
            var dayPriceQuoted = priceQuoted.HasValue ? (decimal?)(priceQuoted.Value / multiDayDetails.DurationDays) : null;
            var dayHirePrice = hirePrice.HasValue ? (decimal?)(hirePrice.Value / multiDayDetails.DurationDays) : null;
            var dayLabour = labour.HasValue ? (decimal?)(labour.Value / multiDayDetails.DurationDays) : null;
            var dayInsurance = insurance.HasValue ? (decimal?)(insurance.Value / multiDayDetails.DurationDays) : null;
            var dayTax2 = tax2.HasValue ? (decimal?)(tax2.Value / multiDayDetails.DurationDays) : null;

            var existing = await _db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == dayBookingNo, ct);

            if (existing == null)
            {
                // CREATE new booking for this day
                var booking = new TblBooking
                {
                    booking_no = dayBookingNo,
                    order_no = dayBookingNo,
                    booking_type_v32 = bookingType,
                    status = 0,
                    BookingProgressStatus = (byte)TryParseStatus(status),
                    bBookingIsComplete = false,

                    // Dates for this specific day
                    dDate = dayDate,
                    rDate = dayDate,
                    SDate = dayDate,
                    SetDate = dayDate,
                    ShowSDate = dayStartTime.HasValue ? dayDate.Date.Add(dayStartTime.Value) : dayDate,
                    ShowEdate = dayEndTime.HasValue ? dayDate.Date.Add(dayEndTime.Value) : dayDate,
                    RehDate = dayDate,
                    order_date = now,
                    EntryDate = now,

                    // Times
                    showStartTime = ToHHmmString(dayStartTime),
                    ShowEndTime = ToHHmmString(dayEndTime),
                    setupTimeV61 = ToHHmmString(dayStartTime), // Use start time as setup time
                    RehearsalTime = ToHHmmString(dayStartTime),
                    StrikeTime = ToHHmmString(dayEndTime),
                    del_time_h = dayStartTime.HasValue ? (byte?)dayStartTime.Value.Hours : null,
                    del_time_m = dayStartTime.HasValue ? (byte?)dayStartTime.Value.Minutes : null,
                    ret_time_h = dayEndTime.HasValue ? (byte?)dayEndTime.Value.Hours : null,
                    ret_time_m = dayEndTime.HasValue ? (byte?)dayEndTime.Value.Minutes : null,

                    // Venue & Event
                    VenueRoom = Trunc($"{venueRoom} - Day {dayNumber}", VenueRoomMaxLength),
                    EventType = Trunc($"{eventType} (Day {dayNumber})", EventTypeMaxLength),
                    showName = Trunc($"{showName} - Day {dayNumber}", ShowNameMaxLength),
                    expAttendees = attendees,
                    VenueID = venueId ?? 20,

                    // Financial (distributed across days)
                    price_quoted = (double?)dayPriceQuoted,
                    hire_price = (double?)dayHirePrice,
                    labour = (double?)dayLabour,
                    insurance_v5 = (double?)dayInsurance,
                    insurance_type = 1, // default 10% service charge
                    Tax2 = (double?)dayTax2,
                    days_using = 1, // Each day booking uses 1 day

                    // Customer/Contact
                    CustID = finalCustId,
                    CustCode = finalCustCode,
                    ContactID = contactId,
                    contact_nameV6 = Trunc(contactName, 35),
                    OrganizationV6 = finalCustId.HasValue
                        ? Trunc(await GetOrganizationNameAsync(finalCustId.Value, ct), 50)
                        : null,
                    Salesperson = Trunc(salesperson, SalespersonMaxLength),

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
                _logger.LogInformation("Created multi-day booking for day {Day}: {BookingNo}", dayNumber, dayBookingNo);
            }
            else
            {
                // UPDATE existing booking for this day
                existing.dDate = dayDate;
                existing.rDate = dayDate;
                existing.SDate = dayDate;
                existing.SetDate = dayDate;
                existing.RehDate = dayDate;

                if (dayStartTime.HasValue) existing.ShowSDate = dayDate.Date.Add(dayStartTime.Value);
                if (dayEndTime.HasValue) existing.ShowEdate = dayDate.Date.Add(dayEndTime.Value);

                if (dayStartTime.HasValue)
                {
                    existing.showStartTime = ToHHmmString(dayStartTime);
                    existing.setupTimeV61 = ToHHmmString(dayStartTime);
                    existing.RehearsalTime = ToHHmmString(dayStartTime);
                    existing.del_time_h = (byte?)dayStartTime.Value.Hours;
                    existing.del_time_m = (byte?)dayStartTime.Value.Minutes;
                }

                if (dayEndTime.HasValue)
                {
                    existing.ShowEndTime = ToHHmmString(dayEndTime);
                    existing.StrikeTime = ToHHmmString(dayEndTime);
                    existing.ret_time_h = (byte?)dayEndTime.Value.Hours;
                    existing.ret_time_m = (byte?)dayEndTime.Value.Minutes;
                }

                existing.VenueRoom = Trunc($"{venueRoom} - Day {dayNumber}", VenueRoomMaxLength);
                existing.EventType = Trunc($"{eventType} (Day {dayNumber})", EventTypeMaxLength);
                existing.showName = Trunc($"{showName} - Day {dayNumber}", ShowNameMaxLength);

                if (dayPriceQuoted.HasValue) existing.price_quoted = (double?)dayPriceQuoted;
                if (dayHirePrice.HasValue) existing.hire_price = (double?)dayHirePrice;
                if (dayLabour.HasValue) existing.labour = (double?)dayLabour;
                if (dayInsurance.HasValue)
                {
                    existing.insurance_v5 = (double?)dayInsurance;
                    existing.insurance_type = (byte)(dayInsurance.Value > 0 ? 1 : 0);
                }
                if (dayTax2.HasValue) existing.Tax2 = (double?)dayTax2;

                if (contactId.HasValue) existing.ContactID = contactId;
                if (!string.IsNullOrWhiteSpace(contactName)) existing.contact_nameV6 = Trunc(contactName, 35);
                if (!string.IsNullOrWhiteSpace(finalCustCode)) existing.CustCode = Trunc(finalCustCode, CustomerCodeMaxLength);
                if (finalCustId.HasValue)
                {
                    existing.CustID = finalCustId;
                    existing.OrganizationV6 = Trunc(await GetOrganizationNameAsync(finalCustId.Value, ct), 50);
                }
                if (venueId.HasValue) existing.VenueID = venueId.Value;

                _logger.LogInformation("Updated multi-day booking for day {Day}: {BookingNo}", dayNumber, dayBookingNo);
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Saved multi-day booking with {Days} days: {BaseBookingNo}", multiDayDetails.DurationDays, baseBookingNo);
        return firstDayBookingNo;
    }

}
