using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Handles contact (person) persistence to tblContact table
/// </summary>
public sealed class ContactPersistenceService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<ContactPersistenceService> _logger;

    public ContactPersistenceService(BookingDbContext db, ILogger<ContactPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Upserts contact by email (primary lookup key).
    /// Schema: tblContact with columns: 
    /// - ID (decimal 10,0 PK)
    /// - Contactname (varchar 35)
    /// - firstname (varchar 25)
    /// - MidName (varchar 35)
    /// - surname (varchar 35)
    /// - Email (varchar 80) - used for lookup
    /// - Cell (varchar 16) - phone
    /// - position (varchar 50)
    /// - Active (char 1)
    /// - CreateDate, LastContact, LastAttempt, LastUpdate (datetime)
    /// </summary>
    public async Task<decimal?> UpsertContactAsync(
        string? fullName,
        string? email,
        string? phoneE164,
        string? position,
        CancellationToken ct)
    {
        try
        {
            var now = NowAest();

            // Split name into components
            var (first, middle, last, displayRaw) = SplitName(fullName);
            
            // Filter out assistant name artifacts
            if (string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase))
                middle = null;

            string? display = LooksLikeAssistantName(displayRaw) ? null : displayRaw;

            // Lookup by email first (most reliable), then by name if no email
            TblContact? existing = null;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var e = email.Trim().ToLowerInvariant();
                existing = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == e, ct);

                // Fix assistant name if found
                if (existing != null &&
                    LooksLikeAssistantName(existing.Contactname) &&
                    !string.IsNullOrWhiteSpace(display))
                {
                    existing.Contactname = Trunc(display, 35);
                }
            }
            // If no email match, try lookup by full name (case-insensitive)
            else if (!string.IsNullOrWhiteSpace(display))
            {
                var nameNorm = display.Trim().ToLowerInvariant();
                existing = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.Contactname != null && c.Contactname.ToLower() == nameNorm, ct);
            }

            // Normalize position
            string? pos = Trunc(NormalizePosition(position), 50);

            // CREATE new contact
            if (existing is null)
            {
                // Require at least one piece of info
                if (string.IsNullOrWhiteSpace(display) &&
                    string.IsNullOrWhiteSpace(email) &&
                    string.IsNullOrWhiteSpace(phoneE164) &&
                    string.IsNullOrWhiteSpace(pos))
                    return null;

                var row = new TblContact
                {
                    Contactname = Trunc(display, 35),
                    Firstname = Trunc(first, 25),
                    MidName = string.IsNullOrWhiteSpace(middle) ? null : Trunc(middle, 35),
                    Surname = Trunc(last, 35),
                    Email = Trunc(email, 80),
                    Cell = Trunc(phoneE164, 16),
                    Position = pos,
                    Active = "Y",
                    CreateDate = now,
                    LastContact = now,
                    LastAttempt = now,
                    LastUpdate = now
                };

                _db.Contacts.Add(row);
                await _db.SaveChangesAsync(ct);
                return row.Id;
            }

            // UPDATE existing contact
            if (!string.IsNullOrWhiteSpace(display))
                existing.Contactname = Trunc(display, 35);

            if (!string.IsNullOrWhiteSpace(first))
                existing.Firstname = Trunc(first, 25);

            if (!string.IsNullOrWhiteSpace(middle) &&
                !string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase))
                existing.MidName = Trunc(middle, 35);

            if (!string.IsNullOrWhiteSpace(last))
                existing.Surname = Trunc(last, 35);

            if (!string.IsNullOrWhiteSpace(email))
                existing.Email = Trunc(email, 80);

            if (!string.IsNullOrWhiteSpace(phoneE164))
                existing.Cell = Trunc(phoneE164, 16);

            if (!string.IsNullOrWhiteSpace(pos))
                existing.Position = pos;

            existing.Active = existing.Active ?? "Y";
            existing.LastContact = now;
            existing.LastAttempt = now;
            existing.LastUpdate = now;

            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateException ex)
        {
            var root = GetRootException(ex);
            throw new InvalidOperationException($"tblContact upsert failed: {root.Message}", ex);
        }
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

    private static string? Trunc(string? s, int len)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= len ? s : s[..len]);

    private static bool LooksLikeAssistantName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().ToLowerInvariant();
        return t.Contains("isla") || t.Contains("microhire");
    }

    private static bool LooksLikeMissing(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var v = value.Trim().ToLowerInvariant();
        return v is "no" or "none" or "n/a" or "na" or "nil" or "unknown" or "tbc" or "not sure" or "dont know" or "don't know";
    }

    private static string? NormalizePosition(string? p)
    {
        if (LooksLikeMissing(p)) return null;
        if (string.IsNullOrWhiteSpace(p)) return null;
        p = Regex.Replace(p, @"\s+", " ").Trim();
        return p.Length < 2 ? null : p;
    }

    /// <summary>
    /// Split full name into first, middle, last components
    /// Schema: firstname (varchar 25), MidName (varchar 35), surname (varchar 35)
    /// </summary>
    private static (string? first, string? middle, string? last, string? displayRaw) SplitName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (null, null, null, null);

        static string Cap(string s) =>
            CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
            return (Cap(parts[0]), null, null, Cap(parts[0]));

        if (parts.Length == 2)
            return (Cap(parts[0]), null, Cap(parts[1]), Cap(name));

        return (
            Cap(parts[0]),
            Cap(string.Join(' ', parts.Skip(1).Take(parts.Length - 2))),
            Cap(parts[^1]),
            Cap(name)
        );
    }

    private static Exception GetRootException(Exception ex)
    {
        Exception root = ex;
        while (root.InnerException != null)
            root = root.InnerException;
        return root;
    }
}

