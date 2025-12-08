using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Handles organization/customer persistence to tblcust table
/// </summary>
public sealed class OrganizationPersistenceService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<OrganizationPersistenceService> _logger;

    public OrganizationPersistenceService(BookingDbContext db, ILogger<OrganizationPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Upserts organization by name (case-insensitive lookup).
    /// Schema: tblcust with columns:
    /// - ID (decimal 10,0 PK)
    /// - Customer_code (varchar 30) UNIQUE - generated as "C#####" from ID
    /// - OrganisationV6 (varchar 50) - used for lookup
    /// - Address_l1V6 (char 50)
    /// - iLink_ContactID (decimal 10,0) - CRITICAL: link to tblContact.ID
    /// - CustCDate (datetime) - creation date
    /// </summary>
    public async Task<decimal?> UpsertOrganisationAsync(
        string organisation,
        string? address,
        decimal? contactId,
        CancellationToken ct)
    {
        var org = Normalize(organisation);
        var addr = Normalize(address);

        if (string.IsNullOrWhiteSpace(org) && string.IsNullOrWhiteSpace(addr))
            return null;

        try
        {
            // NOTE: No transaction here - caller (BookingOrchestrationService) manages the transaction
            
            // Try existing by name (case-insensitive)
            var existing = await _db.TblCusts
                .AsTracking()
                .FirstOrDefaultAsync(c =>
                    c.OrganisationV6 != null &&
                    c.OrganisationV6.ToLower() == org.ToLower(), ct);

            if (existing is null)
            {
                // 1) Insert with unique placeholder to satisfy UNIQUE constraint on Customer_code
                var now = NowAest();
                var row = new TblCust
                {
                    OrganisationV6 = Trunc(org, 50),
                    Address_l1V6 = Trunc(addr, 50),
                    Customer_code = MakeTempCustomerCode(), // unique placeholder
                    ILink_ContactID = contactId, // CRITICAL: link to contact
                    CustCDate = now
                };

                await _db.TblCusts.AddAsync(row, ct);
                await _db.SaveChangesAsync(ct); // ID is now generated

                // 2) Replace placeholder with final ID-based code (C#####)
                row.Customer_code = MakeCustomerCodeFromId(row.ID);
                await _db.SaveChangesAsync(ct);

                return row.ID;
            }
            else
            {
                // UPDATE existing
                if (!string.IsNullOrWhiteSpace(addr))
                    existing.Address_l1V6 = Trunc(addr, 50);

                // Ensure Customer_code is set
                if (string.IsNullOrWhiteSpace(existing.Customer_code))
                    existing.Customer_code = MakeCustomerCodeFromId(existing.ID);

                // Update contact link if provided and not already set
                if (contactId.HasValue && !existing.ILink_ContactID.HasValue)
                    existing.ILink_ContactID = contactId;

                await _db.SaveChangesAsync(ct);
                return existing.ID;
            }
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException($"Failed to save organisation record: {detail}", ex);
        }
    }

    /// <summary>
    /// Find existing organization by name (case-insensitive)
    /// </summary>
    public async Task<(decimal Id, string Code, string Name)?> FindOrganisationAsync(
        string organisation,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(organisation)) return null;
        var norm = organisation.Trim().ToLower();

        var org = await _db.TblCusts
            .Where(c => c.OrganisationV6 != null && c.OrganisationV6.ToLower() == norm)
            .Select(c => new { c.ID, c.Customer_code, c.OrganisationV6 })
            .FirstOrDefaultAsync(ct);

        if (org == null) return null;

        var code = string.IsNullOrWhiteSpace(org.Customer_code)
            ? "C" + Convert.ToInt32(org.ID)
            : org.Customer_code!;

        return (org.ID, code, org.OrganisationV6!);
    }

    /// <summary>
    /// Get customer code by organization ID
    /// </summary>
    public async Task<string?> GetCustomerCodeByIdAsync(decimal orgId, CancellationToken ct)
    {
        var c = await _db.TblCusts
            .Where(x => x.ID == orgId)
            .Select(x => new { x.ID, x.Customer_code })
            .FirstOrDefaultAsync(ct);

        if (c == null) return null;

        return string.IsNullOrWhiteSpace(c.Customer_code)
            ? "C" + Convert.ToInt32(c.ID)
            : c.Customer_code!;
    }

    /// <summary>
    /// Link contact to organization via tblLinkCustContact
    /// Schema: Customer_Code (varchar 30), ContactID (decimal 10,0)
    /// </summary>
    public async Task LinkContactToOrganisationAsync(
        string customerCode,
        decimal contactId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerCode) || contactId <= 0) return;

        var exists = await _db.Set<TblLinkCustContact>()
            .AnyAsync(x => x.Customer_Code == customerCode && x.ContactID == contactId, ct);

        if (exists) return;

        _db.Set<TblLinkCustContact>().Add(new TblLinkCustContact
        {
            Customer_Code = customerCode,
            ContactID = contactId
        });

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

    private static string? Trunc(string? s, int len)
        => string.IsNullOrWhiteSpace(s) ? s : (s!.Length <= len ? s : s[..len]);

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var t = Regex.Replace(s, @"\s+", " ").Trim();
        return t.Trim(';', ':', '.', ',');
    }

    /// <summary>
    /// Generate customer code from ID: ID=14415 → "C14415"
    /// Schema constraint: Customer_code varchar(30)
    /// </summary>
    private static string MakeCustomerCodeFromId(decimal id)
    {
        var n = (long)id;
        return $"C{n:D5}"; // e.g. C14415
    }

    /// <summary>
    /// Generate temporary unique customer code to satisfy UNIQUE constraint during insert
    /// Format: "C_TMP_{ticks}_{randomGuid4chars}" (under 35 chars)
    /// </summary>
    private static string MakeTempCustomerCode()
    {
        var ticks = DateTime.UtcNow.Ticks;
        var rand = Guid.NewGuid().ToString("N")[..4];
        return $"C_TMP_{ticks}_{rand}";
    }
}

