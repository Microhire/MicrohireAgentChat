using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

public sealed partial class ItemPersistenceService
{
    // ==================== PRIVATE HELPERS ====================

    private record ParsedItem(string Name, decimal Quantity, decimal? UnitRate, decimal? TotalPrice);

    private static string? GetFact(Dictionary<string, string> facts, string key)
    {
        if (facts.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val.Trim();
        return null;
    }

    /// <summary>
    /// Parse equipment summary text into structured items.
    /// Expected format examples:
    /// - "2x Handheld Microphone @ $50 = $100"
    /// - "1x PA System $500"
    /// - "4x LED Par Can"
    /// </summary>
    private static List<ParsedItem> ParseEquipmentSummary(string summary)
    {
        var items = new List<ParsedItem>();

        // Pattern: "2x Item Name @ $50 = $100" or "2x Item Name $100" or "2x Item Name"
        var pattern = @"(\d+)\s*x\s+([^@$\n]+?)(?:\s*@\s*\$?([\d,\.]+))?(?:\s*=\s*\$?([\d,\.]+))?(?:\n|$)";
        var matches = Regex.Matches(summary, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match m in matches)
        {
            var qty = decimal.Parse(m.Groups[1].Value);
            var name = m.Groups[2].Value.Trim();
            var unitRate = ParseDecimal(m.Groups[3].Value);
            var totalPrice = ParseDecimal(m.Groups[4].Value);

            // Calculate missing values
            if (totalPrice.HasValue && !unitRate.HasValue && qty > 0)
                unitRate = totalPrice / qty;
            else if (unitRate.HasValue && !totalPrice.HasValue)
                totalPrice = unitRate * qty;

            items.Add(new ParsedItem(name, qty, unitRate, totalPrice));
        }

        return items;
    }

    private static decimal? ParseDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        val = Regex.Replace(val, @"[^\d\.]", ""); // strip non-numeric
        if (decimal.TryParse(val, out var d)) return d;
        return null;
    }

    /// <summary>
    /// Resolve product name to product code from tblinvmas (inventory master)
    /// Schema: tblinvmas columns: ID (varchar 50) PK, Description (varchar 250)
    /// </summary>
    private async Task<string?> ResolveProductCodeAsync(string itemName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return null;

        var normalized = itemName.Trim().ToLower();

        // Try exact match first
        var product = await _db.TblInvmas
            .Where(p => p.descriptionv6 != null && p.descriptionv6.ToLower() == normalized)
            .Select(p => p.product_code)
            .FirstOrDefaultAsync(ct);

        if (product != null) return product;

        // Try partial match (contains)
        product = await _db.TblInvmas
            .Where(p => p.descriptionv6 != null && p.descriptionv6.ToLower().Contains(normalized))
            .Select(p => p.product_code)
            .FirstOrDefaultAsync(ct);

        return product;
    }

    /// <summary>
    /// Check if product code represents a package (has components in vwProdsComponents)
    /// Schema: vwProdsComponents columns: ProdCode, ComponentCode, Quantity
    /// </summary>
    private async Task<bool> IsPackageAsync(string productCode, CancellationToken ct)
    {
        return await _db.VwProdsComponents
            .AnyAsync(p => p.ParentCode != null && p.ParentCode.Trim() == productCode, ct);
    }

    /// <summary>
    /// Check if a product is a COMPONENT of a package.
    /// Returns the best parent package based on:
    /// 1. Matching category (component and parent should be in same category)
    /// 2. SelectComp='Y' preferred (user was meant to select this component)
    /// 3. Has a valid price
    /// </summary>
    private async Task<ParentPackageInfo?> GetParentPackageAsync(string productCode, CancellationToken ct)
    {
        // First get the component's category
        var component = await _db.TblInvmas
            .Where(p => p.product_code.Trim() == productCode)
            .Select(p => new { p.category, p.groupFld })
            .FirstOrDefaultAsync(ct);

        var componentCategory = component?.category?.Trim();
        _logger.LogInformation("Component {ProductCode} has category: {Category}", productCode, componentCategory);

        // Find all packages that contain this product as a component, with details
        var parentInfos = await _db.VwProdsComponents
            .Where(p => p.ProductCode != null && p.ProductCode.Trim() == productCode)
            .Select(p => new { p.ParentCode, p.SelectComp, p.VariablePart })
            .ToListAsync(ct);

        if (!parentInfos.Any())
            return null;

        // Score each parent package
        var candidates = new List<(ParentPackageInfo Info, int Score)>();

        foreach (var pi in parentInfos.Where(p => p.ParentCode != null))
        {
            var trimmedParent = pi.ParentCode!.Trim();
            if (string.Equals(trimmedParent, "ELEVIND", StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Get parent details
            var parentProduct = await _db.TblInvmas
                .Where(p => p.product_code.Trim() == trimmedParent)
                .Select(p => new { p.descriptionv6, p.category })
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(parentProduct?.descriptionv6) &&
                parentProduct.descriptionv6.Contains("independent items", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parentCategory = parentProduct?.category?.Trim();

            // Get rate for this parent package
            var rate = await _db.TblRatetbls
                .Where(r => r.product_code != null && r.product_code.Trim() == trimmedParent && r.TableNo == 0)
                .Select(r => r.rate_1st_day)
                .FirstOrDefaultAsync(ct);

            var actualRate = rate ?? 0;

            // Calculate score:
            // +100 for matching category
            // +50 for SelectComp='Y' (component is selectable)
            // +10 for having a valid price
            int score = 0;
            if (!string.IsNullOrEmpty(componentCategory) && 
                string.Equals(parentCategory, componentCategory, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            if (pi.SelectComp == "Y")
            {
                score += 50;
            }
            if (actualRate > 0)
            {
                score += 10;
            }

            _logger.LogInformation("Parent {ParentCode} (category: {Category}): score={Score}, rate=${Rate}", 
                trimmedParent, parentCategory, score, actualRate);

            candidates.Add((new ParentPackageInfo(
                trimmedParent,
                parentProduct?.descriptionv6?.Trim(),
                actualRate
            ), score));
        }

        // Pick the best candidate (highest score, then highest rate as tiebreaker)
        var best = candidates
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.Info.Rate)
            .FirstOrDefault();

        if (best.Info != null)
        {
            _logger.LogInformation("Selected parent package: {ParentCode} with score {Score}", 
                best.Info.ParentCode, best.Score);
        }

        return best.Info;
    }

    private record ParentPackageInfo(string ParentCode, string? Description, double Rate);

    private async Task<ParentPackageInfo?> GetParentPackageByCodeAsync(string parentCode, CancellationToken ct)
    {
        var trimmedParent = parentCode?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedParent))
            return null;

        var parentProduct = await _db.TblInvmas
            .Where(p => p.product_code.Trim() == trimmedParent)
            .Select(p => new { p.descriptionv6, p.PrintedDesc })
            .FirstOrDefaultAsync(ct);

        var parentRate = await GetProductRateAsync(trimmedParent, ct) ?? 0;
        return new ParentPackageInfo(
            trimmedParent,
            parentProduct?.descriptionv6?.Trim() ?? parentProduct?.PrintedDesc?.Trim() ?? trimmedParent,
            parentRate);
    }

    private async Task AddPackageWithComponentsAsync(
        string bookingNo,
        int bookingId,
        string packageCode,
        int quantity,
        int seqNo,
        CancellationToken ct)
    {
        var packageInfo = await GetParentPackageByCodeAsync(packageCode, ct);
        if (packageInfo == null)
        {
            _logger.LogWarning("Package {PackageCode} not found in inventory; skipping package insert", packageCode);
            return;
        }

        var packageRow = new TblItemtran
        {
            BookingNoV32 = bookingNo,
            BookingId = bookingId,
            HeadingNo = 1,
            SeqNo = seqNo,
            SubSeqNo = 0, // PER GUIDE: Package has sub_seq_no = 0
            TransTypeV41 = RentalPointQuoteTransType,
            ProductCodeV42 = packageInfo.ParentCode,
            CommentDescV42 = packageInfo.Description,
            TransQty = quantity,
            UnitRate = packageInfo.Rate,
            Price = packageInfo.Rate * quantity,
            ItemType = 1, // PER GUIDE: Package = 1
            ParentCode = null,
            SubRentalLinkID = 0,
            AssignType = 0,
            QtyShort = 0,
            AvailRecFlag = false,
            // PER GUIDE: Default to Westin Brisbane location
            FromLocn = 20,
            TransToLocn = 20,
            ReturnToLocn = 20
        };
        _db.TblItemtrans.Add(packageRow);
        _logger.LogInformation("Added parent package: {ProductCode} x {Qty} @ ${Rate}",
            packageInfo.ParentCode, quantity, packageInfo.Rate);

        await AddPackageComponentsRowsAsync(bookingNo, bookingId, packageInfo.ParentCode, quantity, seqNo, ct);
    }

    private async Task AddPackageComponentsRowsAsync(
        string bookingNo,
        int bookingId,
        string packageCode,
        int packageQuantity,
        int seqNo,
        CancellationToken ct)
    {
        var components = await GetPackageComponentsWithSubSeqAsync(packageCode, ct);
        _logger.LogInformation("Adding {Count} components for package {Package}", components.Count, packageCode);

        foreach (var comp in components)
        {
            var compProduct = await _db.TblInvmas
                .Where(p => p.product_code.Trim() == comp.ComponentCode)
                .Select(p => new { p.descriptionv6, p.PrintedDesc })
                .FirstOrDefaultAsync(ct);
            var compDesc = compProduct?.descriptionv6?.Trim() ?? compProduct?.PrintedDesc?.Trim() ?? comp.ComponentCode;

            var compRow = new TblItemtran
            {
                BookingNoV32 = bookingNo,
                BookingId = bookingId,
                HeadingNo = 1,
                SeqNo = seqNo, // PER GUIDE: Same seq_no as package
                SubSeqNo = comp.SubSeqNo, // PER GUIDE: Pull from vwProdsComponents
                TransTypeV41 = RentalPointQuoteTransType,
                ProductCodeV42 = comp.ComponentCode,
                CommentDescV42 = compDesc,
                TransQty = comp.Quantity * packageQuantity,
                Price = 0, // Component - price is in parent
                UnitRate = 0,
                ItemType = 2, // PER GUIDE: Component = 2
                ParentCode = packageCode, // PER GUIDE: Components must have parentcode
                SubRentalLinkID = 0,
                AssignType = 0,
                QtyShort = 0,
                AvailRecFlag = false,
                // PER GUIDE: Default to Westin Brisbane location
                FromLocn = 20,
                TransToLocn = 20,
                ReturnToLocn = 20
            };
            _db.TblItemtrans.Add(compRow);
        }
    }

    /// <summary>
    /// Get package components from vwProdsComponents view
    /// </summary>
    private async Task<List<(string ComponentCode, decimal Quantity)>> GetPackageComponentsAsync(
        string packageCode,
        CancellationToken ct)
    {
        var components = await _db.VwProdsComponents
            .Where(p => p.ParentCode != null && p.ParentCode.Trim() == packageCode)
            .Select(p => new { ComponentCode = p.ProductCode, Quantity = p.Qty })
            .ToListAsync(ct);

        return components
            .Where(c => c.ComponentCode != null && c.Quantity.HasValue)
            .Select(c => (c.ComponentCode!.Trim(), (decimal)c.Quantity!.Value))
            .ToList();
    }

    /// <summary>
    /// Get package components from vwProdsComponents view with sub_seq_no
    /// PER GUIDE: Filter using parent_code and variable_part = 0
    /// Pull sub_seq_no from the view for proper component ordering
    /// </summary>
    private async Task<List<(string ComponentCode, decimal Quantity, int SubSeqNo)>> GetPackageComponentsWithSubSeqAsync(
        string packageCode,
        CancellationToken ct)
    {
        var components = await _db.VwProdsComponents
            .Where(p => p.ParentCode != null && 
                        p.ParentCode.Trim() == packageCode &&
                        (p.VariablePart == null || p.VariablePart == 0)) // PER GUIDE: variable_part = 0
            .OrderBy(p => p.SubSeqNo)
            .Select(p => new { 
                ComponentCode = p.ProductCode, 
                Quantity = p.Qty,
                SubSeqNo = p.SubSeqNo
            })
            .ToListAsync(ct);

        return components
            .Where(c => c.ComponentCode != null && c.Quantity.HasValue)
            .Select(c => (
                c.ComponentCode!.Trim(), 
                (decimal)c.Quantity!.Value,
                (int)(c.SubSeqNo ?? 1) // Default to 1 if null
            ))
            .ToList();
    }
}
