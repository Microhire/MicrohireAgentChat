using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Handles all booking-related database queries - extracted from AzureAgentChatService
/// </summary>
public sealed class BookingQueryService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<BookingQueryService> _logger;

    public BookingQueryService(BookingDbContext db, ILogger<BookingQueryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Find booking by booking number
    /// </summary>
    public async Task<TblBooking?> FindBookingByNumberAsync(string bookingNo, CancellationToken ct)
    {
        return await _db.TblBookings
            .FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
    }

    /// <summary>
    /// Find customer by organization name (case-insensitive)
    /// </summary>
    public async Task<TblCust?> FindCustomerByNameAsync(string orgName, CancellationToken ct)
    {
        var normalized = orgName.Trim().ToLower();
        return await _db.TblCusts
            .FirstOrDefaultAsync(c => c.OrganisationV6 != null && c.OrganisationV6.ToLower() == normalized, ct);
    }

    /// <summary>
    /// Find contact by email (case-insensitive)
    /// </summary>
    public async Task<TblContact?> FindContactByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLower();
        return await _db.Contacts
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalized, ct);
    }

    /// <summary>
    /// Find contact by name (case-insensitive)
    /// </summary>
    public async Task<TblContact?> FindContactByNameAsync(string name, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        return await _db.Contacts
            .FirstOrDefaultAsync(c => c.Contactname != null && c.Contactname.ToLower() == normalized, ct);
    }

    /// <summary>
    /// Get all items for a booking
    /// </summary>
    public async Task<List<TblItemtran>> GetBookingItemsAsync(string bookingNo, CancellationToken ct)
    {
        return await _db.TblItemtrans
            .Where(i => i.BookingNoV32 == bookingNo)
            .OrderBy(i => i.SubSeqNo)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Get all crew for a booking
    /// </summary>
    public async Task<List<TblCrew>> GetBookingCrewAsync(string bookingNo, CancellationToken ct)
    {
        return await _db.TblCrews
            .Where(c => c.BookingNoV32 == bookingNo)
            .OrderBy(c => c.SubSeqNo)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Check if booking number exists
    /// </summary>
    public async Task<bool> BookingExistsAsync(string bookingNo, CancellationToken ct)
    {
        return await _db.TblBookings
            .AnyAsync(b => b.booking_no == bookingNo, ct);
    }

    /// <summary>
    /// Get recent bookings for a customer
    /// </summary>
    public async Task<List<TblBooking>> GetRecentBookingsForCustomerAsync(
        string customerCode,
        int limit = 10,
        CancellationToken ct = default)
    {
        return await _db.TblBookings
            .Where(b => b.CustCode == customerCode)
            .OrderByDescending(b => b.order_date)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Get bookings by date range (using delivery date)
    /// </summary>
    public async Task<List<TblBooking>> GetBookingsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct)
    {
        return await _db.TblBookings
            .Where(b => b.dDate >= startDate && b.dDate <= endDate)
            .OrderBy(b => b.dDate)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Finds contact by email in RentalPoint (AITEST), then the best booking for pre-fill:
    /// soonest event day on/after today; if none, the most recently ordered booking.
    /// Queries are scoped in SQL (no loading all rows for the contact) so email verification stays fast.
    /// </summary>
    public async Task<BookingPrefillFromEmailResult?> FindLatestUpcomingBookingForEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var contact = await _db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == normalized, ct);
        if (contact == null)
            return null;

        var cid = contact.Id;
        var today = DateTime.Today;

        // Same event-day rule as before: ShowSDate, else dDate, else SDate — evaluated on the server.
        var upcoming = await _db.TblBookings
            .AsNoTracking()
            .Where(b => b.ContactID == cid && b.booking_no != null)
            .Where(b => (b.ShowSDate ?? b.dDate ?? b.SDate) != null)
            .Where(b => (b.ShowSDate ?? b.dDate ?? b.SDate)!.Value.Date >= today)
            .OrderBy(b => b.ShowSDate ?? b.dDate ?? b.SDate)
            .FirstOrDefaultAsync(ct);

        var pick = upcoming;
        if (pick == null)
        {
            pick = await _db.TblBookings
                .AsNoTracking()
                .Where(b => b.ContactID == cid && b.booking_no != null)
                .OrderByDescending(b => b.order_date ?? b.ShowSDate ?? b.dDate ?? b.SDate)
                .FirstOrDefaultAsync(ct);
        }

        if (pick == null)
            return new BookingPrefillFromEmailResult { Contact = contact, Booking = null, VenueDisplayName = null };

        string? venueDisplay = null;
        try
        {
            var vid = (decimal)pick.VenueID;
            venueDisplay = await _db.TblVenues.AsNoTracking()
                .Where(v => v.ID == vid)
                .Select(v => v.VenueName)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Venue lookup failed for VenueID {VenueId}", pick.VenueID);
        }

        return new BookingPrefillFromEmailResult
        {
            Contact = contact,
            Booking = pick,
            VenueDisplayName = venueDisplay
        };
    }
}

/// <summary>Result of email-based booking lookup for chat pre-fill.</summary>
public sealed class BookingPrefillFromEmailResult
{
    public required TblContact Contact { get; init; }
    public TblBooking? Booking { get; init; }
    public string? VenueDisplayName { get; init; }
}

