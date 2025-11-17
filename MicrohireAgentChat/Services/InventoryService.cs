using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Services
{
    /// <summary>
    /// Service for querying inventory (products, categories, packages)
    /// from the Rental Point database.
    /// </summary>
    public sealed class InventoryService
    {
        private readonly BookingDbContext _db;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(BookingDbContext db, ILogger<InventoryService> logger)
        {
            _db = db;
            _logger = logger;
        }

        #region Product Queries

        /// <summary>
        /// Get all Windows laptops (from LAPTOP category tree)
        /// </summary>
        public async Task<List<TblInvmas>> GetWindowsLaptopsAsync(CancellationToken ct = default)
        {
            var categoryCodes = await _db.TblCategories
                .AsNoTracking()
                .Where(c => c.GroupCode == "COMPUTER" &&
                           (c.CategoryCode == "LAPTOP" || c.ParentCategoryCode == "LAPTOP"))
                .Select(c => c.CategoryCode)
                .ToListAsync(ct);

            if (!categoryCodes.Any())
                return new List<TblInvmas>();

            return await _db.TblInvmas
                .AsNoTracking()
                .Where(i => categoryCodes.Contains(i.category ?? "") &&
                           i.groupFld != "Y") // not in trash
                .OrderBy(i => i.product_code)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Get all Apple Macbooks (from MACBOOK category tree)
        /// </summary>
        public async Task<List<TblInvmas>> GetMacbooksAsync(CancellationToken ct = default)
        {
            var categoryCodes = await _db.TblCategories
                .AsNoTracking()
                .Where(c => c.GroupCode == "COMPUTER" &&
                           (c.CategoryCode == "MACBOOK" || c.ParentCategoryCode == "MACBOOK"))
                .Select(c => c.CategoryCode)
                .ToListAsync(ct);

            if (!categoryCodes.Any())
                return new List<TblInvmas>();

            return await _db.TblInvmas
                .AsNoTracking()
                .Where(i => categoryCodes.Contains(i.category ?? "") &&
                           i.groupFld != "Y")
                .OrderBy(i => i.product_code)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Get all laptops (both Windows and Mac)
        /// </summary>
        public async Task<List<TblInvmas>> GetAllLaptopsAsync(CancellationToken ct = default)
        {
            var categoryCodes = await _db.TblCategories
                .AsNoTracking()
                .Where(c => c.GroupCode == "COMPUTER" &&
                           ((c.CategoryCode == "LAPTOP" || c.ParentCategoryCode == "LAPTOP") ||
                            (c.CategoryCode == "MACBOOK" || c.ParentCategoryCode == "MACBOOK")))
                .Select(c => c.CategoryCode)
                .ToListAsync(ct);

            if (!categoryCodes.Any())
                return new List<TblInvmas>();

            return await _db.TblInvmas
                .AsNoTracking()
                .Where(i => categoryCodes.Contains(i.category ?? "") &&
                           i.groupFld != "Y")
                .OrderBy(i => i.category)
                .ThenBy(i => i.product_code)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Get products by category (e.g., NETWORK, IPAD, DESKTOP, PRINTER, etc.)
        /// </summary>
        public async Task<List<TblInvmas>> GetProductsByCategoryAsync(string categoryCode, CancellationToken ct = default)
        {
            var categoryCodes = await _db.TblCategories
                .AsNoTracking()
                .Where(c => c.CategoryCode == categoryCode || c.ParentCategoryCode == categoryCode)
                .Select(c => c.CategoryCode)
                .ToListAsync(ct);

            if (!categoryCodes.Any())
                return new List<TblInvmas>();

            return await _db.TblInvmas
                .AsNoTracking()
                .Where(i => categoryCodes.Contains(i.category ?? "") &&
                           i.groupFld != "Y")
                .OrderBy(i => i.product_code)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Get products by group (e.g., COMPUTER)
        /// </summary>
        public async Task<List<TblInvmas>> GetProductsByGroupAsync(string groupCode, CancellationToken ct = default)
        {
            var categoryCodes = await _db.TblCategories
                .AsNoTracking()
                .Where(c => c.GroupCode == groupCode)
                .Select(c => c.CategoryCode)
                .ToListAsync(ct);

            if (!categoryCodes.Any())
                return new List<TblInvmas>();

            return await _db.TblInvmas
                .AsNoTracking()
                .Where(i => categoryCodes.Contains(i.category ?? "") &&
                           i.groupFld != "Y")
                .OrderBy(i => i.category)
                .ThenBy(i => i.product_code)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Search products by description or code
        /// </summary>
        public async Task<List<TblInvmas>> SearchProductsAsync(string searchTerm, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<TblInvmas>();

            var term = searchTerm.ToLower().Trim();

            return await _db.TblInvmas
                .AsNoTracking()
                .Where(i => (i.product_code != null && i.product_code.ToLower().Contains(term)) ||
                           (i.descriptionv6 != null && i.descriptionv6.ToLower().Contains(term)) ||
                           (i.PrintedDesc != null && i.PrintedDesc.ToLower().Contains(term)))
                .Where(i => i.groupFld != "Y")
                .OrderBy(i => i.product_code)
                .Take(50) // limit results
                .ToListAsync(ct);
        }

        /// <summary>
        /// Get product by code
        /// </summary>
        public async Task<TblInvmas?> GetProductByCodeAsync(string productCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return null;

            return await _db.TblInvmas
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.product_code == productCode.Trim(), ct);
        }

        #endregion

        #region Package Resolution

        /// <summary>
        /// Get all components of a package product using vwProdsComponents view.
        /// Returns list of (productCode, quantity) tuples.
        /// </summary>
        public async Task<List<(string ProductCode, double Quantity)>> GetPackageComponentsAsync(
            string packageCode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(packageCode))
                return new List<(string, double)>();

            var components = await _db.VwProdsComponents
                .AsNoTracking()
                .Where(v => v.ParentCode == packageCode.Trim())
                .Select(v => new { v.ProductCode, v.Qty })
                .ToListAsync(ct);

            return components
                .Select(c => (c.ProductCode, c.Qty ?? 1.0))
                .ToList();
        }

        /// <summary>
        /// Recursively resolve all components of a package (handles nested packages).
        /// Returns flattened list of (productCode, totalQuantity) with aggregated quantities.
        /// </summary>
        public async Task<List<(string ProductCode, double TotalQuantity)>> ResolvePackageRecursiveAsync(
            string packageCode,
            double multiplier = 1.0,
            CancellationToken ct = default,
            HashSet<string>? visited = null)
        {
            visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // prevent circular references
            if (visited.Contains(packageCode))
                return new List<(string, double)>();

            visited.Add(packageCode);

            var components = await GetPackageComponentsAsync(packageCode, ct);
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var (childCode, childQty) in components)
            {
                // check if this component is itself a package
                var childComponents = await GetPackageComponentsAsync(childCode, ct);

                if (childComponents.Any())
                {
                    // it's a package, recurse
                    var nested = await ResolvePackageRecursiveAsync(
                        childCode,
                        multiplier * childQty,
                        ct,
                        visited);

                    foreach (var (nestedCode, nestedQty) in nested)
                    {
                        if (result.ContainsKey(nestedCode))
                            result[nestedCode] += nestedQty;
                        else
                            result[nestedCode] = nestedQty;
                    }
                }
                else
                {
                    // it's a leaf product
                    var qty = multiplier * childQty;
                    if (result.ContainsKey(childCode))
                        result[childCode] += qty;
                    else
                        result[childCode] = qty;
                }
            }

            return result.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        }

        /// <summary>
        /// Check if a product code is a package (has components)
        /// </summary>
        public async Task<bool> IsPackageAsync(string productCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return false;

            return await _db.VwProdsComponents
                .AsNoTracking()
                .AnyAsync(v => v.ParentCode == productCode.Trim(), ct);
        }

        #endregion

        #region Pricing

        /// <summary>
        /// Get pricing for a product (first day rate from rate table 0)
        /// </summary>
        public async Task<double?> GetProductPriceAsync(string productCode, byte tableNo = 0, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return null;

            var rate = await _db.TblRatetbls
                .AsNoTracking()
                .Where(r => r.product_code == productCode.Trim() && r.TableNo == tableNo)
                .Select(r => r.rate_1st_day)
                .FirstOrDefaultAsync(ct);

            return rate;
        }

        /// <summary>
        /// Get pricing for multiple products at once
        /// </summary>
        public async Task<Dictionary<string, double>> GetProductPricesAsync(
            IEnumerable<string> productCodes,
            byte tableNo = 0,
            CancellationToken ct = default)
        {
            var codes = productCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();

            if (!codes.Any())
                return new Dictionary<string, double>();

            var rates = await _db.TblRatetbls
                .AsNoTracking()
                .Where(r => codes.Contains(r.product_code) && r.TableNo == tableNo)
                .Select(r => new { r.product_code, r.rate_1st_day })
                .ToListAsync(ct);

            return rates
                .Where(r => r.rate_1st_day > 0)
                .ToDictionary(r => r.product_code, r => r.rate_1st_day);
        }

        #endregion

        #region Categories

        /// <summary>
        /// Get all categories in a group
        /// </summary>
        public async Task<List<TblCategory>> GetCategoriesByGroupAsync(string groupCode, CancellationToken ct = default)
        {
            return await _db.TblCategories
                .AsNoTracking()
                .Where(c => c.GroupCode == groupCode)
                .OrderBy(c => c.CategoryType)
                .ThenBy(c => c.CategoryCode)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Get category hierarchy (parent -> children)
        /// </summary>
        public async Task<Dictionary<string, List<TblCategory>>> GetCategoryHierarchyAsync(
            string groupCode,
            CancellationToken ct = default)
        {
            var categories = await GetCategoriesByGroupAsync(groupCode, ct);

            var hierarchy = new Dictionary<string, List<TblCategory>>();

            // group by parent
            var grouped = categories.GroupBy(c => c.ParentCategoryCode ?? "ROOT");

            foreach (var group in grouped)
            {
                hierarchy[group.Key] = group.ToList();
            }

            return hierarchy;
        }

        #endregion
    }
}

