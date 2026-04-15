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

    /// <param name="leadAuthoritative">When true (sales lead sync): resolve org via contact links + normalized name key; refresh OrganisationV6 and primary contact.</param>
    /// <param name="noUpdateExisting">When true: if organization exists, return it without updating its fields.</param>
    public async Task<OrganisationUpsertResult> UpsertOrganisationAsync(
        string organisation,
        string? address,
        decimal? contactId,
        CancellationToken ct,
        bool leadAuthoritative = false,
        bool noUpdateExisting = false)
    {
        var org = Normalize(organisation);
        var addr = Normalize(address);

        if (string.IsNullOrWhiteSpace(org) && string.IsNullOrWhiteSpace(addr))
            return new OrganisationUpsertResult(null, "skipped");

        try
        {
            TblCust? existing = null;

            if (leadAuthoritative && contactId.HasValue && contactId.Value > 0)
                existing = await FindOrganisationByContactIdAsync(contactId.Value, org, ct);

            if (existing is null)
            {
                if (leadAuthoritative)
                    existing = await FindByOrganisationKeyAsync(org, ct);
                else
                {
                    existing = await _db.TblCusts
                        .AsTracking()
                        .FirstOrDefaultAsync(c =>
                            c.OrganisationV6 != null &&
                            c.OrganisationV6.ToLower() == org.ToLower(), ct);
                }
            }

            if (existing is null)
            {
                var now = NowAest();
                var row = new TblCust
                {
                    OrganisationV6 = Trunc(org, 50),
                    Address_l1V6 = Trunc(addr, 50),
                    Customer_code = MakeTempCustomerCode(),
                    ILink_ContactID = contactId ?? 0,
                    CustCDate = now
                };

                await _db.TblCusts.AddAsync(row, ct);
                await _db.SaveChangesAsync(ct);

                row.Customer_code = MakeCustomerCodeFromId(row.ID);
                await _db.SaveChangesAsync(ct);

                return new OrganisationUpsertResult(row.ID, "created");
            }

            if (noUpdateExisting)
            {
                return new OrganisationUpsertResult(existing.ID, "reused");
            }

            if (!string.IsNullOrWhiteSpace(org))
                existing.OrganisationV6 = Trunc(org, 50);
            if (!string.IsNullOrWhiteSpace(addr))
                existing.Address_l1V6 = Trunc(addr, 50);

            if (string.IsNullOrWhiteSpace(existing.Customer_code))
                existing.Customer_code = MakeCustomerCodeFromId(existing.ID);

            if (leadAuthoritative && contactId.HasValue && contactId.Value > 0)
                existing.ILink_ContactID = contactId;
            else if (contactId.HasValue && contactId.Value > 0 &&
                     (!existing.ILink_ContactID.HasValue || existing.ILink_ContactID.Value <= 0))
                existing.ILink_ContactID = contactId;

            await _db.SaveChangesAsync(ct);
            return new OrganisationUpsertResult(existing.ID, "updated");
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException($"Failed to save organisation record: {detail}", ex);
        }
    }

    private async Task<TblCust?> FindOrganisationByContactIdAsync(decimal contactId, string submittedOrg, CancellationToken ct)
    {
        var key = ContactLookupNormalization.NormalizeOrganisationKey(submittedOrg);

        var byPrimary = await _db.TblCusts
            .AsTracking()
            .Where(c => c.ILink_ContactID == contactId)
            .ToListAsync(ct);
        if (byPrimary.Count == 1)
            return byPrimary[0];
        if (byPrimary.Count > 1)
            return PickBestOrgByKey(byPrimary, key);

        var codes = await _db.TblLinkCustContacts
            .AsNoTracking()
            .Where(l => l.ContactID == contactId && l.Customer_Code != null)
            .Select(l => l.Customer_Code!)
            .Distinct()
            .ToListAsync(ct);
        if (codes.Count == 0)
            return null;

        var linked = await _db.TblCusts
            .AsTracking()
            .Where(c => c.Customer_code != null && codes.Contains(c.Customer_code))
            .ToListAsync(ct);
        if (linked.Count == 0)
            return null;
        if (linked.Count == 1)
            return linked[0];
        return PickBestOrgByKey(linked, key);
    }

    private static TblCust? PickBestOrgByKey(List<TblCust> rows, string normalizedKey)
    {
        if (rows.Count == 0) return null;
        if (string.IsNullOrEmpty(normalizedKey))
            return rows[0];
        var hit = rows.FirstOrDefault(r =>
            ContactLookupNormalization.NormalizeOrganisationKey(r.OrganisationV6) == normalizedKey);
        return hit ?? rows[0];
    }

    /// <summary>Suffix-stripped key match; narrows with prefix then verifies in memory.</summary>
    private async Task<TblCust?> FindByOrganisationKeyAsync(string orgDisplay, CancellationToken ct)
    {
        var key = ContactLookupNormalization.NormalizeOrganisationKey(orgDisplay);
        if (string.IsNullOrEmpty(key))
            return null;

        var prefixLen = Math.Min(4, key.Length);
        var prefix = key[..prefixLen];
        var candidates = await _db.TblCusts
            .AsTracking()
            .Where(c => c.OrganisationV6 != null && c.OrganisationV6.ToLower().Contains(prefix))
            .ToListAsync(ct);

        return candidates.FirstOrDefault(c =>
            ContactLookupNormalization.NormalizeOrganisationKey(c.OrganisationV6) == key);
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
            ? MakeCustomerCodeFromId(org.ID)
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
            ? MakeCustomerCodeFromId(c.ID)
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

    /// <summary>
    /// Search organisations by name for autocomplete. Returns top <paramref name="limit"/> matches.
    /// </summary>
    public async Task<List<OrganisationSearchResult>> SearchOrganisationsAsync(
        string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
            return new List<OrganisationSearchResult>();

        var q = query.Trim().ToLower();

        var rows = await _db.TblCusts
            .AsNoTracking()
            .Where(c => c.OrganisationV6 != null && c.OrganisationV6.ToLower().Contains(q))
            .OrderByDescending(c => c.CustCDate)
            .Take(limit)
            .Select(c => new { c.ID, c.Customer_code, c.OrganisationV6, c.Address_l1V6 })
            .ToListAsync(ct);

        return rows.Select(r => new OrganisationSearchResult
        {
            Id = r.ID,
            CustomerCode = string.IsNullOrWhiteSpace(r.Customer_code)
                ? MakeCustomerCodeFromId(r.ID)
                : r.Customer_code!,
            Name = r.OrganisationV6 ?? "",
            Address = r.Address_l1V6,
        }).ToList();
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

    /// <summary>
    /// Check if a contact is linked to an organization (either as primary or in tblLinkCustContact)
    /// </summary>
    public async Task<bool> IsContactLinkedToOrganisationAsync(decimal orgId, decimal contactId, CancellationToken ct)
    {
        if (orgId <= 0 || contactId <= 0) return false;

        // 1. Check primary link on tblcust
        var isPrimary = await _db.TblCusts
            .AnyAsync(c => c.ID == orgId && c.ILink_ContactID == contactId, ct);
        if (isPrimary) return true;

        // 2. Check tblLinkCustContact
        var code = await GetCustomerCodeByIdAsync(orgId, ct);
        if (string.IsNullOrWhiteSpace(code)) return false;

        return await _db.Set<TblLinkCustContact>()
            .AnyAsync(l => l.Customer_Code == code && l.ContactID == contactId, ct);
    }
}

