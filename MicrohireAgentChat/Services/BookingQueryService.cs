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
}

