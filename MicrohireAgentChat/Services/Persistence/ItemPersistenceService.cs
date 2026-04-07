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
public sealed partial class ItemPersistenceService
{
    /// <summary>trans_type_v41 value required by Rental Point for quote totals to display correctly</summary>
    private const byte RentalPointQuoteTransType = 2;

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
    /// - trans_type_v41 (tinyint) - transaction type (2 = required for RP quote totals)
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

            var bookingId = decimal.ToInt32(booking.ID);

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

                // Prevent known placeholder/meta item from being persisted into quotes.
                if (string.Equals(productCode, "ELEVIND", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(productCode, "THRVIND", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(productCode, "WBIND", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Skipping blocked product code {ProductCode}", productCode);
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
                if (!string.IsNullOrWhiteSpace(description) &&
                    description.Contains("independent items", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Skipping blocked placeholder item by description: {Description}", description);
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(item.Comment))
                {
                    var commentSuffix = $" (Client requested: {item.Comment})";
                    description = (description + commentSuffix).Length <= 70
                        ? description + commentSuffix
                        : description.Substring(0, Math.Max(0, 70 - commentSuffix.Length)) + commentSuffix;
                }
                var isPackage = item.IsPackage ?? await IsPackageAsync(productCode, ct);
                var unitRate = await GetProductRateAsync(productCode, ct);

                // If a selected code is a component, persist its parent package + components.
                // Prefer explicit metadata from selection payload, then fallback to DB lookup.
                if (!isPackage)
                {
                    ParentPackageInfo? parentPackage = null;
                    if (!string.IsNullOrWhiteSpace(item.ParentPackageCode))
                    {
                        parentPackage = await GetParentPackageByCodeAsync(item.ParentPackageCode!, ct);
                    }
                    parentPackage ??= await GetParentPackageAsync(productCode, ct);

                    if (parentPackage != null)
                    {
                        _logger.LogInformation("Product {ProductCode} is a component of package {ParentCode}", productCode, parentPackage.ParentCode);

                        // Only add the parent package once (with ALL its components)
                        if (!addedPackages.Contains(parentPackage.ParentCode))
                        {
                            addedPackages.Add(parentPackage.ParentCode);
                            await AddPackageWithComponentsAsync(
                                bookingNo,
                                bookingId,
                                parentPackage.ParentCode,
                                item.Quantity,
                                subSeqNo,
                                ct);
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
                    TransTypeV41 = RentalPointQuoteTransType,
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
                    await AddPackageComponentsRowsAsync(
                        bookingNo,
                        bookingId,
                        productCode,
                        item.Quantity,
                        subSeqNo,
                        ct);
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

        var bookingId = decimal.ToInt32(booking.ID);

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
                TransTypeV41 = RentalPointQuoteTransType,
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
                        TransTypeV41 = RentalPointQuoteTransType,
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

}
