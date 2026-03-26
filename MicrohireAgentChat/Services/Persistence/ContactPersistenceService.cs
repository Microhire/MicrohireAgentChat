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
    /// Upserts contact: lookup by email, then normalized phone (when email unmatched), then contact name.
    /// Schema: tblContact — Email / Cell / Contactname used for matching.
    /// </summary>
    public async Task<ContactUpsertResult> UpsertContactAsync(
        string? fullName,
        string? email,
        string? phoneE164,
        string? position,
        CancellationToken ct)
    {
        try
        {
            var now = NowAest();

            var (first, middle, last, displayRaw) = SplitName(fullName);

            if (string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase))
                middle = null;

            string? display = LooksLikeAssistantName(displayRaw) ? null : displayRaw;

            if (LooksLikeAssistantName(displayRaw))
            {
                first = null;
                middle = null;
                last = null;
            }

            var existing = await FindExistingContactAsync(
                email,
                phoneE164,
                display,
                allowPhoneLookup: true,
                ct);

            if (existing != null)
                RepairAssistantNameArtifacts(existing, display, first, middle, last);

            string? pos = Trunc(NormalizePosition(position), 35);

            if (existing is null)
            {
                if (string.IsNullOrWhiteSpace(display) &&
                    string.IsNullOrWhiteSpace(email) &&
                    string.IsNullOrWhiteSpace(phoneE164) &&
                    string.IsNullOrWhiteSpace(pos))
                    return new ContactUpsertResult(null, "skipped");

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
                return new ContactUpsertResult(row.Id, "created");
            }

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
            return new ContactUpsertResult(existing.Id, "updated");
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateException ex)
        {
            var root = GetRootException(ex);
            throw new InvalidOperationException($"tblContact upsert failed: {root.Message}", ex);
        }
    }

    /// <summary>
    /// Upserts contact by email only (no phone or name fallback). Used by ChatController.
    /// </summary>
    public async Task<ContactUpsertResult> UpsertContactByEmailAsync(
        string? fullName,
        string? email,
        string? phoneE164,
        string? position,
        CancellationToken ct)
    {
        try
        {
            var now = NowAest();

            var (first, middle, last, displayRaw) = SplitName(fullName);

            if (string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase))
                middle = null;

            string? display = LooksLikeAssistantName(displayRaw) ? null : displayRaw;

            if (LooksLikeAssistantName(displayRaw))
            {
                first = null;
                middle = null;
                last = null;
            }

            TblContact? existing = null;

            if (!string.IsNullOrWhiteSpace(email))
            {
                var e = email.Trim().ToLowerInvariant();
                existing = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == e, ct);

                if (existing != null)
                    RepairAssistantNameArtifacts(existing, display, first, middle, last);
            }

            string? pos = Trunc(NormalizePosition(position), 35);

            if (existing is null)
            {
                if (string.IsNullOrWhiteSpace(display) &&
                    string.IsNullOrWhiteSpace(email) &&
                    string.IsNullOrWhiteSpace(phoneE164) &&
                    string.IsNullOrWhiteSpace(pos))
                    return new ContactUpsertResult(null, "skipped");

                var row = new TblContact
                {
                    Contactname = Trunc(display, 35),
                    Firstname = Trunc(first, 25),
                    MidName = string.IsNullOrWhiteSpace(middle) ? null : Trunc(middle, 35),
                    Surname = Trunc(last, 35),
                    Email = Trunc(email, 80),
                    Cell = Trunc(phoneE164, 16),
                    Active = "Y",
                    CreateDate = now,
                    LastContact = now,
                    LastAttempt = now,
                    LastUpdate = now,
                    Position = pos
                };

                _db.Contacts.Add(row);
                await _db.SaveChangesAsync(ct);
                return new ContactUpsertResult(row.Id, "created");
            }

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
            return new ContactUpsertResult(existing.Id, "updated");
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateException ex)
        {
            var root = GetRootException(ex);
            throw new InvalidOperationException($"tblContact upsert failed: {root.Message}", ex);
        }
    }

    /// <summary>Email first, then optional phone (digits), then contact name.</summary>
    public async Task<TblContact?> FindExistingContactAsync(
        string? email,
        string? phoneE164,
        string? displayForNameMatch,
        bool allowPhoneLookup,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var e = email.Trim().ToLowerInvariant();
            var byEmail = await _db.Contacts
                .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == e, ct);
            if (byEmail != null) return byEmail;
        }

        if (allowPhoneLookup)
        {
            var phoneKey = ContactLookupNormalization.NormalizePhoneDigits(phoneE164);
            if (!string.IsNullOrEmpty(phoneKey) && phoneKey.Length >= 8)
            {
                var last4 = phoneKey[^4..];
                var candidates = await _db.Contacts
                    .AsNoTracking()
                    .Where(c => c.Cell != null && c.Cell.Contains(last4))
                    .ToListAsync(ct);

                var match = candidates.FirstOrDefault(c =>
                    ContactLookupNormalization.NormalizePhoneDigits(c.Cell) == phoneKey);
                if (match != null)
                {
                    return await _db.Contacts
                        .FirstOrDefaultAsync(c => c.Id == match.Id, ct);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(displayForNameMatch))
        {
            var nameNorm = displayForNameMatch.Trim().ToLowerInvariant();
            return await _db.Contacts
                .FirstOrDefaultAsync(c => c.Contactname != null && c.Contactname.ToLower() == nameNorm, ct);
        }

        return null;
    }

    /// <summary>
    /// Creates a brand new contact record without checking for existing ones.
    /// Used when the "no-update" policy requires a new record instead of reusing an unlinked one.
    /// </summary>
    public async Task<ContactUpsertResult> CreateNewContactAsync(
        string? fullName,
        string? email,
        string? phoneE164,
        string? position,
        CancellationToken ct)
    {
        var now = NowAest();
        var (first, middle, last, displayRaw) = SplitName(fullName);
        if (string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase))
            middle = null;

        string? display = LooksLikeAssistantName(displayRaw) ? null : displayRaw;
        if (LooksLikeAssistantName(displayRaw))
        {
            first = null;
            middle = null;
            last = null;
        }

        string? pos = Trunc(NormalizePosition(position), 35);

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
        return new ContactUpsertResult(row.Id, "created");
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

    private static void RepairAssistantNameArtifacts(
        TblContact existing,
        string? display,
        string? first,
        string? middle,
        string? last)
    {
        // Only heal fields when the row contains known assistant artifacts.
        var hasArtifacts = LooksLikeAssistantName(existing.Contactname)
            || LooksLikeAssistantNamePart(existing.Firstname)
            || LooksLikeAssistantNamePart(existing.MidName)
            || LooksLikeAssistantNamePart(existing.Surname)
            || string.Equals(existing.MidName?.Trim(), "from", StringComparison.OrdinalIgnoreCase);

        if (!hasArtifacts)
            return;

        if (LooksLikeAssistantName(existing.Contactname))
            existing.Contactname = !string.IsNullOrWhiteSpace(display) ? Trunc(display, 35) : null;

        if (LooksLikeAssistantNamePart(existing.Firstname))
            existing.Firstname = !string.IsNullOrWhiteSpace(first) ? Trunc(first, 25) : null;

        if (LooksLikeAssistantNamePart(existing.MidName) ||
            string.Equals(existing.MidName?.Trim(), "from", StringComparison.OrdinalIgnoreCase))
        {
            existing.MidName = !string.IsNullOrWhiteSpace(middle) &&
                               !string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase)
                ? Trunc(middle, 35)
                : null;
        }

        if (LooksLikeAssistantNamePart(existing.Surname))
            existing.Surname = !string.IsNullOrWhiteSpace(last) ? Trunc(last, 35) : null;
    }

    /// <summary>True if the full name is the assistant's (Isla, Microhire, or "Isla from Microhire"). Allows e.g. "Isla Smith".</summary>
    private static bool LooksLikeAssistantName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().ToLowerInvariant();
        return t == "isla" || t == "microhire" || (t.Contains("isla") && t.Contains("microhire"));
    }

    /// <summary>True if the part (first/middle/last) is the assistant's name token, so we should not persist it.</summary>
    private static bool LooksLikeAssistantNamePart(string? part)
    {
        if (string.IsNullOrWhiteSpace(part)) return false;
        var t = part.Trim().ToLowerInvariant();
        return t == "isla" || t == "microhire";
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

