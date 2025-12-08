using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Handles item persistence to tblitemtran table with package/component support
/// </summary>
public sealed class ItemPersistenceService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<ItemPersistenceService> _logger;

    public ItemPersistenceService(BookingDbContext db, ILogger<ItemPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Upsert items from equipment summary or selected equipment.
    /// Schema: tblitemtran with columns (per guide):
    /// - ID (decimal 18,0) PK identity
    /// - booking_no_v32 (varchar 20) FK - booking number
    /// - booking_id (decimal 10,0) FK - booking ID (REQUIRED per guide)
    /// - heading_no (tinyint) - usually 1 for equipment (per guide)
    /// - seq_no (decimal 10,0) - sequence number (usually 1)
    /// - sub_seq_no (int) - sub-sequence (increments: 1, 2, 3...)
    /// - trans_type_v41 (tinyint) - transaction type (1 = hire per guide)
    /// - product_code_v42 (varchar 50) - product code
    /// - Comment_desc_v42 (varchar) - item description
    /// - trans_qty (decimal 10,0) - quantity
    /// - price (decimal 19,4) - total price
    /// - item_type (byte) - 0=normal item, 1=package, 2=component
    /// - ParentCode (varchar 50) - for components only
    /// </summary>
    public async Task UpsertItemsFromSummaryAsync(
        string bookingNo,
        Dictionary<string, string> facts,
        CancellationToken ct)
    {
        // Try new format first: selected_equipment JSON
        var selectedEquipmentJson = GetFact(facts, "selected_equipment");
        if (!string.IsNullOrWhiteSpace(selectedEquipmentJson))
        {
            await UpsertSelectedEquipmentAsync(bookingNo, selectedEquipmentJson, ct);
            return;
        }

        // Fallback to legacy format: equipment_summary
        var summary = GetFact(facts, "equipment_summary");
        if (string.IsNullOrWhiteSpace(summary)) return;

        // Parse equipment summary
        var items = ParseEquipmentSummary(summary);
        if (!items.Any()) return;

        await InsertItemsAsync(bookingNo, items, ct);
    }

    /// <summary>
    /// Insert selected equipment items with product codes directly.
    /// Handles three scenarios:
    /// 1. Item is a package (has components) - insert package with price, then components
    /// 2. Item is a component of a package - insert parent package with price, then component
    /// 3. Item is standalone - insert with its own price
    /// </summary>
    public async Task UpsertSelectedEquipmentAsync(
        string bookingNo,
        string selectedEquipmentJson,
        CancellationToken ct)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<SelectedEquipmentItem>>(selectedEquipmentJson);
            if (items == null || !items.Any())
            {
                _logger.LogWarning("No equipment items in JSON for booking {BookingNo}", bookingNo);
                return;
            }

            _logger.LogInformation("Inserting {Count} equipment items for booking {BookingNo}", items.Count, bookingNo);

            // Get existing items for this booking
            var existing = await _db.TblItemtrans
                .Where(x => x.BookingNoV32 == bookingNo)
                .ToListAsync(ct);

            // Delete all existing items (we'll recreate them)
            if (existing.Any())
            {
                _db.TblItemtrans.RemoveRange(existing);
                await _db.SaveChangesAsync(ct);
            }

            // Get booking ID (required per guide)
            var booking = await _db.TblBookings
                .Where(b => b.booking_no == bookingNo)
                .Select(b => new { b.ID })
                .FirstOrDefaultAsync(ct);

            if (booking == null)
            {
                _logger.LogError("Booking {BookingNo} not found - cannot add items", bookingNo);
                return;
            }

            var bookingId = booking.ID;

            // Track which packages we've already added (to avoid duplicates)
            var addedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Insert items
            int subSeqNo = 1;
            foreach (var item in items)
            {
                var productCode = item.ProductCode?.Trim();
                if (string.IsNullOrWhiteSpace(productCode))
                {
                    _logger.LogWarning("Skipping item with empty product code");
                    continue;
                }

                // Verify product exists in inventory
                var product = await _db.TblInvmas
                    .Where(p => p.product_code.Trim() == productCode)
                    .Select(p => new { p.product_code, p.descriptionv6, p.PrintedDesc })
                    .FirstOrDefaultAsync(ct);

                if (product == null)
                {
                    _logger.LogWarning("Product {ProductCode} not found in inventory - inserting anyway", productCode);
                }

                var description = item.Description ?? product?.descriptionv6?.Trim() ?? product?.PrintedDesc?.Trim() ?? productCode;
                var isPackage = await IsPackageAsync(productCode, ct);
                var unitRate = await GetProductRateAsync(productCode, ct);

                // Check if this item is a COMPONENT of a package (has $0 or low price)
                if (!isPackage && (unitRate == null || unitRate <= 0))
                {
                    var parentPackage = await GetParentPackageAsync(productCode, ct);
                    if (parentPackage != null)
                    {
                        _logger.LogInformation("Product {ProductCode} is a component of package {ParentCode}", productCode, parentPackage.ParentCode);

                        // Only add the parent package once (with ALL its components)
                        if (!addedPackages.Contains(parentPackage.ParentCode))
                        {
                            addedPackages.Add(parentPackage.ParentCode);

                            // Get parent package details
                            var parentProduct = await _db.TblInvmas
                                .Where(p => p.product_code.Trim() == parentPackage.ParentCode)
                                .Select(p => new { p.product_code, p.descriptionv6, p.PrintedDesc })
                                .FirstOrDefaultAsync(ct);

                            var parentRate = await GetProductRateAsync(parentPackage.ParentCode, ct);
                            var parentDesc = parentProduct?.descriptionv6?.Trim() ?? parentProduct?.PrintedDesc?.Trim() ?? parentPackage.ParentCode;

                            // PER GUIDE: Insert the parent package as main item
                            // Package and components should have same seq_no
                            var pkgSeqNo = subSeqNo;
                            var packageRow = new TblItemtran
                            {
                                BookingNoV32 = bookingNo,
                                BookingId = bookingId,
                                HeadingNo = 1,
                                SeqNo = pkgSeqNo,
                                SubSeqNo = 0, // PER GUIDE: Package has sub_seq_no = 0
                                TransTypeV41 = 1, // 1 = hire
                                ProductCodeV42 = parentPackage.ParentCode,
                                CommentDescV42 = parentDesc,
                                TransQty = item.Quantity,
                                UnitRate = parentRate,
                                Price = (parentRate ?? 0) * item.Quantity,
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
                                parentPackage.ParentCode, item.Quantity, parentRate);

                            // PER GUIDE: Insert ALL components from vwProdsComponents with variable_part = 0
                            // Use sub_seq_no from the view for proper ordering
                            var allComponents = await GetPackageComponentsWithSubSeqAsync(parentPackage.ParentCode, ct);
                            _logger.LogInformation("Adding {Count} components for package {Package}", 
                                allComponents.Count, parentPackage.ParentCode);
                            
                            foreach (var comp in allComponents)
                            {
                                // Get component description
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
                                    SeqNo = pkgSeqNo, // PER GUIDE: Same seq_no as package
                                    SubSeqNo = comp.SubSeqNo, // PER GUIDE: Pull from vwProdsComponents
                                    TransTypeV41 = 1, // 1 = hire
                                    ProductCodeV42 = comp.ComponentCode,
                                    CommentDescV42 = compDesc,
                                    TransQty = comp.Quantity * item.Quantity,
                                    Price = 0, // Component - price is in parent
                                    UnitRate = 0,
                                    ItemType = 2, // PER GUIDE: Component = 2
                                    ParentCode = parentPackage.ParentCode, // PER GUIDE: Components must have parentcode
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
                            subSeqNo++; // Increment for next item
                        }

                        continue; // Move to next item - we've added the whole package
                    }
                }

                // Regular item (package or standalone with valid price)
                // PER GUIDE: itemtype 0=normal, 1=package, 2=component
                var row = new TblItemtran
                {
                    BookingNoV32 = bookingNo,
                    BookingId = bookingId,
                    HeadingNo = 1,
                    SeqNo = subSeqNo,
                    SubSeqNo = 0, // Package/standalone has sub_seq_no = 0
                    TransTypeV41 = 1, // 1 = hire
                    ProductCodeV42 = productCode,
                    CommentDescV42 = description,
                    TransQty = item.Quantity,
                    UnitRate = unitRate,
                    Price = (unitRate ?? 0) * item.Quantity,
                    ItemType = (byte)(isPackage ? 1 : 0), // PER GUIDE: 1=package, 0=normal
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

                _db.TblItemtrans.Add(row);
                _logger.LogInformation("Added item: {ProductCode} x {Qty} @ ${Rate}", productCode, item.Quantity, unitRate);

                // If it's a package, add all its components
                // PER GUIDE: Use sub_seq_no from vwProdsComponents
                if (isPackage)
                {
                    addedPackages.Add(productCode);
                    var components = await GetPackageComponentsWithSubSeqAsync(productCode, ct);
                    foreach (var comp in components)
                    {
                        var compRow = new TblItemtran
                        {
                            BookingNoV32 = bookingNo,
                            BookingId = bookingId,
                            HeadingNo = 1,
                            SeqNo = subSeqNo, // PER GUIDE: Same seq_no as package
                            SubSeqNo = comp.SubSeqNo, // PER GUIDE: Pull from vwProdsComponents
                            TransTypeV41 = 1, // 1 = hire
                            ProductCodeV42 = comp.ComponentCode,
                            TransQty = comp.Quantity * item.Quantity,
                            Price = 0,
                            UnitRate = 0,
                            ItemType = 2, // PER GUIDE: Component = 2
                            ParentCode = productCode, // PER GUIDE: Components must have parentcode
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

                subSeqNo++;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully saved equipment items for booking {BookingNo}", bookingNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert selected equipment for booking {BookingNo}", bookingNo);
            throw;
        }
    }

    private async Task InsertItemsAsync(string bookingNo, List<ParsedItem> items, CancellationToken ct)
    {
        // Get existing items for this booking
        var existing = await _db.TblItemtrans
            .Where(x => x.BookingNoV32 == bookingNo)
            .ToListAsync(ct);

        // Delete all existing items (we'll recreate them)
        if (existing.Any())
        {
            _db.TblItemtrans.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);
        }

        // Get booking ID (required per guide)
        var booking = await _db.TblBookings
            .Where(b => b.booking_no == bookingNo)
            .Select(b => new { b.ID })
            .FirstOrDefaultAsync(ct);
        
        if (booking == null)
        {
            _logger.LogError("Booking {BookingNo} not found - cannot add items", bookingNo);
            return;
        }

        var bookingId = booking.ID;

        // Insert new items
        int seqNo = 1;
        foreach (var item in items)
        {
            var productCode = await ResolveProductCodeAsync(item.Name, ct);
            if (productCode == null)
            {
                _logger.LogWarning("Could not resolve product code for: {ItemName}", item.Name);
                continue;
            }

            var isPackage = await IsPackageAsync(productCode, ct);

            // Insert main item (per guide requirements)
            // PER GUIDE: itemtype 0=normal, 1=package, 2=component
            var row = new TblItemtran
            {
                BookingNoV32 = bookingNo,
                BookingId = bookingId, // REQUIRED per guide
                HeadingNo = 1, // Per guide: usually 1 for equipment
                SeqNo = seqNo, // Package & components share same seq_no
                SubSeqNo = 0, // Package/standalone has sub_seq_no = 0
                TransTypeV41 = 1, // Per guide: 1 = hire
                ProductCodeV42 = productCode,
                CommentDescV42 = item.Name, // Item description
                TransQty = item.Quantity,
                Price = (double?)item.TotalPrice,
                UnitRate = (double?)item.UnitRate,
                ItemType = (byte)(isPackage ? 1 : 0), // PER GUIDE: 1=package, 0=normal
                ParentCode = null,
                // Required NOT NULL fields
                SubRentalLinkID = 0,
                AssignType = 0,
                QtyShort = 0,
                AvailRecFlag = false,
                // PER GUIDE: Default to Westin Brisbane location
                FromLocn = 20,
                TransToLocn = 20,
                ReturnToLocn = 20
            };

            _db.TblItemtrans.Add(row);

            // If it's a package, add components
            // PER GUIDE: Use sub_seq_no from vwProdsComponents
            if (isPackage)
            {
                var components = await GetPackageComponentsWithSubSeqAsync(productCode, ct);

                foreach (var comp in components)
                {
                    var compRow = new TblItemtran
                    {
                        BookingNoV32 = bookingNo,
                        BookingId = bookingId,
                        HeadingNo = 1,
                        SeqNo = seqNo, // PER GUIDE: Same seq_no as package
                        SubSeqNo = comp.SubSeqNo, // PER GUIDE: Pull from vwProdsComponents
                        TransTypeV41 = 1, // hire
                        ProductCodeV42 = comp.ComponentCode,
                        TransQty = comp.Quantity * item.Quantity, // multiply by parent qty
                        Price = 0, // components typically have $0 price (parent has price)
                        UnitRate = 0,
                        ItemType = 2, // PER GUIDE: 2=component
                        ParentCode = productCode, // PER GUIDE: Components must have parentcode
                        // Required NOT NULL fields
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

            seqNo++; // Increment for next main item
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<double?> GetProductRateAsync(string productCode, CancellationToken ct)
    {
        var trimmedCode = productCode?.Trim();
        if (string.IsNullOrEmpty(trimmedCode)) return null;

        // First try to get rate from tblRatetbl (tableNo=0 is default/retail rate)
        var rate = await _db.TblRatetbls
            .Where(r => r.product_code != null && r.product_code.Trim() == trimmedCode && r.TableNo == 0)
            .Select(r => r.rate_1st_day)
            .FirstOrDefaultAsync(ct);

        if (rate.HasValue && rate.Value > 0)
            return rate.Value;

        // Fallback to retail_price from product master
        var product = await _db.TblInvmas
            .Where(p => p.product_code.Trim() == trimmedCode)
            .Select(p => new { p.retail_price })
            .FirstOrDefaultAsync(ct);

        return product?.retail_price;
    }

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
            
            // Get parent details
            var parentProduct = await _db.TblInvmas
                .Where(p => p.product_code.Trim() == trimmedParent)
                .Select(p => new { p.descriptionv6, p.category })
                .FirstOrDefaultAsync(ct);

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

