using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

public sealed partial class EquipmentSearchService
{
    #region Search Equipment

    /// <summary>
    /// Search for equipment matching a category/keyword with pricing and package info
    /// </summary>
    public async Task<EquipmentSearchResult> SearchEquipmentAsync(
        string keyword, 
        int maxResults = 10,
        CancellationToken ct = default)
    {
        var result = new EquipmentSearchResult
        {
            SearchTerm = keyword,
            CategoryName = keyword
        };

        if (string.IsNullOrWhiteSpace(keyword))
        {
            result.Error = "No search term provided";
            return result;
        }

        var normalizedKeyword = keyword.ToLowerInvariant().Trim();
        _logger.LogInformation("SearchEquipment: Searching for '{Keyword}' (normalized: '{NormalizedKeyword}')", keyword, normalizedKeyword);
        
        // Try to find a category mapping
        EquipmentCategoryMapping? mapping = null;
        if (CategoryMappings.TryGetValue(normalizedKeyword, out mapping))
        {
            result.CategoryName = mapping.DisplayName;
            _logger.LogInformation("SearchEquipment: Found mapping '{DisplayName}' with categories [{Categories}], searchTerms [{SearchTerms}]", 
                mapping.DisplayName, 
                string.Join(", ", mapping.Categories ?? Array.Empty<string>()),
                string.Join(", ", mapping.SearchTerms));
        }
        else
        {
            // Try fallback: strip common prefixes like "big", "large", "small" and retry
            var prefixes = new[] { "big ", "large ", "small ", "medium ", "standard " };
            foreach (var prefix in prefixes)
            {
                if (normalizedKeyword.StartsWith(prefix))
                {
                    var baseKeyword = normalizedKeyword.Substring(prefix.Length).Trim();
                    if (CategoryMappings.TryGetValue(baseKeyword, out mapping))
                    {
                        result.CategoryName = mapping.DisplayName;
                        _logger.LogInformation("SearchEquipment: Found fallback mapping for '{BaseKeyword}' from '{Original}' → '{DisplayName}'", 
                            baseKeyword, normalizedKeyword, mapping.DisplayName);
                        break;
                    }
                }
            }
            
            if (mapping == null)
            {
                _logger.LogWarning("SearchEquipment: No category mapping found for '{Keyword}', trying AI fallback", normalizedKeyword);
                
                // Try AI-powered interpretation if available
                if (_aiQuery != null)
                {
                    try
                    {
                        var aiResults = await _aiQuery.SearchWithAIAsync(keyword, maxResults, ct);
                        if (aiResults.Count > 0)
                        {
                            _logger.LogInformation("SearchEquipment: AI fallback found {Count} results for '{Keyword}'", aiResults.Count, keyword);
                            result.CategoryName = "AI-Recommended Equipment";
                            result.Items = aiResults.Select(r => new EquipmentItem
                            {
                                ProductCode = r.ProductCode,
                                Description = r.Description,
                                Category = r.Category,
                                Group = r.Group,
                                PictureFileName = r.Picture,
                                DayRate = r.DayRate,
                                IsPackage = r.IsPackage
                            }).ToList();
                            result.TotalCount = aiResults.Count;
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AI fallback failed for '{Keyword}', continuing with basic search", keyword);
                    }
                }
            }
        }

        try
        {
            // Build query - filter out trashed items
            IQueryable<TblInvmas> query = _db.TblInvmas.AsNoTracking();

            if (mapping != null)
            {
                // Use category mapping - NOTE: DB categories have trailing spaces, so we trim for comparison
                if (mapping.Categories != null && mapping.Categories.Length > 0)
                {
                    var categories = mapping.Categories;
                    // Use TRIM to handle trailing spaces in database categories
                    query = query.Where(p => categories.Contains((p.category ?? "").Trim()));
                }
                else if (!string.IsNullOrEmpty(mapping.Group))
                {
                    var group = mapping.Group;
                    query = query.Where(p => (p.groupFld ?? "").Trim() == group);
                }

                // Filter by search terms in description - at least one term must match
                if (mapping.SearchTerms.Length > 0)
                {
                    var terms = mapping.SearchTerms;
                    query = query.Where(p => 
                        terms.Any(t => (p.descriptionv6 ?? "").ToLower().Contains(t)) ||
                        terms.Any(t => (p.PrintedDesc ?? "").ToLower().Contains(t)));
                }
            }
            else
            {
                // Fallback: search in description and product code
                query = query.Where(p =>
                    (p.product_code ?? "").ToLower().Contains(normalizedKeyword) ||
                    (p.descriptionv6 ?? "").ToLower().Contains(normalizedKeyword) ||
                    (p.PrintedDesc ?? "").ToLower().Contains(normalizedKeyword) ||
                    (p.category ?? "").Trim().ToLower().Contains(normalizedKeyword));
            }

            // Exclude long-term hire, discontinued, and internal items
            query = query.Where(p => 
                !(p.descriptionv6 ?? "").ToLower().Contains("long term hire") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("discontinued") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("internal") &&
                !(p.PrintedDesc ?? "").ToLower().Contains("long term hire") &&
                !(p.PrintedDesc ?? "").ToLower().Contains("discontinued") &&
                (p.category ?? "").Trim().ToUpper() != "INTERNAL");

            // Get product codes for pricing lookup
            var products = await query
                .Select(p => new
                {
                    p.product_code,
                    p.descriptionv6,
                    p.PrintedDesc,
                    p.category,
                    p.groupFld,
                    p.PictureFileName,
                    p.ProductTypeV41,
                    p.OnHand
                })
                .Take(100) // Get more initially for sorting by price
                .ToListAsync(ct);

            if (products.Count == 0)
            {
                _logger.LogWarning("SearchEquipment: No products found for keyword: {Keyword}, Mapping: {Mapping}", keyword, mapping?.DisplayName ?? "none");
                result.Items = new List<EquipmentItem>();
                return result;
            }

            _logger.LogInformation("SearchEquipment: Found {Count} initial products for keyword: {Keyword}", products.Count, keyword);
            
            // Log sample of products found
            foreach (var p in products.Take(3))
            {
                _logger.LogDebug("SearchEquipment: Sample product: {Code} - {Desc} [{Category}]", 
                    p.product_code?.Trim(), p.descriptionv6?.Trim(), p.category?.Trim());
            }

            // Get pricing for these products - use LTRIM/RTRIM for proper matching
            var productCodes = products.Select(p => (p.product_code ?? "").Trim()).ToList();
            var pricing = await _db.TblRatetbls
                .AsNoTracking()
                .Where(r => r.TableNo == 0 && productCodes.Contains((r.product_code ?? "").Trim()))
                .Select(r => new { 
                    Code = (r.product_code ?? "").Trim(), 
                    r.rate_1st_day,
                    r.rate_extra_days
                })
                .ToListAsync(ct);

            _logger.LogInformation("SearchEquipment: Found pricing for {PricingCount}/{ProductCount} products", pricing.Count, products.Count);
            
            // Log warning if many products don't have pricing
            if (pricing.Count < products.Count / 2)
            {
                _logger.LogWarning("SearchEquipment: Many products ({MissingCount}) don't have pricing - check product_code matching", 
                    products.Count - pricing.Count);
            }

            var priceLookup = pricing
                .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Check which products are part of packages
            var packageInfo = await GetPackageInfoForProductsAsync(productCodes, ct);

            // Build result items with pricing
            var items = products
                .Select(p =>
                {
                    var code = (p.product_code ?? "").Trim();
                    var price = priceLookup.TryGetValue(code, out var r) ? r.rate_1st_day ?? 0 : 0;
                    var extraDayPrice = priceLookup.TryGetValue(code, out var r2) ? r2.rate_extra_days ?? 0 : 0;
                    var isPackage = p.ProductTypeV41 == 1;
                    var partOfPackages = packageInfo.TryGetValue(code, out var pkgs) ? pkgs : null;

                    return new EquipmentItem
                    {
                        ProductCode = code,
                        Description = (p.descriptionv6 ?? p.PrintedDesc ?? "").Trim(),
                        PrintedDescription = (p.PrintedDesc ?? "").Trim(),
                        Category = p.category,
                        Group = p.groupFld,
                        PictureFileName = p.PictureFileName,
                        DayRate = price,
                        ExtraDayRate = extraDayPrice,
                        IsPackage = isPackage,
                        StockOnHand = (int)(p.OnHand ?? 0),
                        PartOfPackages = partOfPackages
                    };
                })
                .Where(i => i.DayRate > 0) // Only items with pricing
                .OrderByDescending(i => i.DayRate) // Sort by price (highest first = premium)
                .ThenBy(i => i.Description)
                .Take(maxResults)
                .ToList();

            result.Items = items;
            result.TotalCount = items.Count;
            
            _logger.LogInformation("SearchEquipment: Final result for '{Keyword}': {ItemCount} items with pricing > 0", keyword, items.Count);
            if (items.Count == 0 && products.Count > 0)
            {
                _logger.LogWarning("SearchEquipment: All {ProductCount} products filtered out due to pricing. Check rate table matching.", products.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchEquipment: Error searching for keyword: {Keyword}", keyword);
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Get package information for a list of product codes
    /// Returns which packages each product is part of
    /// </summary>
    private async Task<Dictionary<string, List<PackageInfo>>> GetPackageInfoForProductsAsync(
        List<string> productCodes, 
        CancellationToken ct)
    {
        var result = new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Find all packages that contain these products
            var packageData = await _db.VwProdsComponents
                .AsNoTracking()
                .Where(v => productCodes.Contains((v.ProductCode ?? "").Trim()))
                .Select(v => new
                {
                    ComponentCode = (v.ProductCode ?? "").Trim(),
                    ParentCode = (v.ParentCode ?? "").Trim(),
                    Quantity = v.Qty ?? 1
                })
                .ToListAsync(ct);

            if (packageData.Count == 0) return result;

            // Get package details
            var parentCodes = packageData.Select(p => p.ParentCode).Distinct().ToList();
            var packageDetails = await _db.TblInvmas
                .AsNoTracking()
                .Where(p => parentCodes.Contains((p.product_code ?? "").Trim()) && p.ProductTypeV41 == 1)
                .Select(p => new
                {
                    Code = (p.product_code ?? "").Trim(),
                    Description = p.descriptionv6 ?? p.PrintedDesc ?? ""
                })
                .ToListAsync(ct);

            var packageLookup = packageDetails.ToDictionary(p => p.Code, p => p.Description, StringComparer.OrdinalIgnoreCase);

            // Get package pricing
            var packagePricing = await _db.TblRatetbls
                .AsNoTracking()
                .Where(r => r.TableNo == 0 && parentCodes.Contains((r.product_code ?? "").Trim()))
                .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day })
                .ToListAsync(ct);

            var priceLookup = packagePricing.ToDictionary(p => p.Code, p => p.rate_1st_day ?? 0, StringComparer.OrdinalIgnoreCase);

            // Build result
            foreach (var pd in packageData)
            {
                if (!packageLookup.TryGetValue(pd.ParentCode, out var pkgDesc)) continue;

                var packageInfo = new PackageInfo
                {
                    PackageCode = pd.ParentCode,
                    PackageDescription = pkgDesc.Trim(),
                    DayRate = priceLookup.TryGetValue(pd.ParentCode, out var pr) ? pr : 0,
                    ComponentQuantity = (int)pd.Quantity
                };

                if (!result.ContainsKey(pd.ComponentCode))
                    result[pd.ComponentCode] = new List<PackageInfo>();

                result[pd.ComponentCode].Add(packageInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting package info");
        }

        return result;
    }

    /// <summary>
    /// Get all components of a package
    /// </summary>
    public async Task<List<PackageComponent>> GetPackageComponentsAsync(string packageCode, CancellationToken ct)
    {
        var components = await _db.VwProdsComponents
            .AsNoTracking()
            .Where(v => (v.ParentCode ?? "").Trim() == packageCode.Trim())
            .OrderBy(v => v.SubSeqNo)
            .Select(v => new PackageComponent
            {
                ProductCode = (v.ProductCode ?? "").Trim(),
                Description = v.Description ?? "",
                Quantity = (int)(v.Qty ?? 1),
                IsVariable = v.VariablePart == 1
            })
            .ToListAsync(ct);

        return components;
    }

    #endregion

    #region Build Recommendations

    /// <summary>
    /// Build equipment recommendations based on parsed requirements
    /// </summary>
    public async Task<EquipmentRecommendations> GetRecommendationsAsync(
        List<EquipmentRequirement> requirements,
        CancellationToken ct)
    {
        var recommendations = new EquipmentRecommendations();

        foreach (var req in requirements)
        {
            var searchResult = await SearchEquipmentAsync(req.NormalizedType, 5, ct);
            
            if (searchResult.Items.Count > 0)
            {
                var topItem = searchResult.Items.First();
                
                // Check if there's a package that would be better value
                PackageRecommendation? packageRec = null;
                if (topItem.PartOfPackages != null && topItem.PartOfPackages.Count > 0)
                {
                    var bestPackage = topItem.PartOfPackages
                        .Where(p => p.DayRate > 0)
                        .OrderByDescending(p => p.DayRate)
                        .FirstOrDefault();

                    if (bestPackage != null)
                    {
                        var components = await GetPackageComponentsAsync(bestPackage.PackageCode, ct);
                        packageRec = new PackageRecommendation
                        {
                            PackageCode = bestPackage.PackageCode,
                            PackageDescription = bestPackage.PackageDescription,
                            DayRate = bestPackage.DayRate,
                            Components = components,
                            ReasonToRecommend = $"Includes {topItem.Description} plus accessories"
                        };
                    }
                }

                recommendations.Categories.Add(new CategoryRecommendation
                {
                    CategoryName = searchResult.CategoryName,
                    RequestedQuantity = req.Quantity,
                    OriginalRequest = req.OriginalText,
                    TopRecommendation = topItem,
                    AlternativeOptions = searchResult.Items.Skip(1).Take(4).ToList(),
                    PackageOption = packageRec
                });
            }
            else
            {
                recommendations.Categories.Add(new CategoryRecommendation
                {
                    CategoryName = searchResult.CategoryName,
                    RequestedQuantity = req.Quantity,
                    OriginalRequest = req.OriginalText,
                    NotFound = true,
                    NotFoundMessage = $"No equipment found matching '{req.OriginalText}'"
                });
            }
        }

        // Calculate totals
        recommendations.EstimatedDayTotal = recommendations.Categories
            .Where(c => c.TopRecommendation != null)
            .Sum(c => (c.TopRecommendation?.DayRate ?? 0) * c.RequestedQuantity);

        return recommendations;
    }

    #endregion
}

#region Models

public class EquipmentRequirement
{
    public string OriginalText { get; set; } = "";
    public string NormalizedType { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public class EquipmentSearchResult
{
    public string SearchTerm { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public List<EquipmentItem> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public string? Error { get; set; }
}

public class EquipmentItem
{
    public string ProductCode { get; set; } = "";
    public string Description { get; set; } = "";
    public string PrintedDescription { get; set; } = "";
    public string? Category { get; set; }
    public string? Group { get; set; }
    public string? PictureFileName { get; set; }
    public double DayRate { get; set; }
    public double ExtraDayRate { get; set; }
    public bool IsPackage { get; set; }
    public int StockOnHand { get; set; }
    public List<PackageInfo>? PartOfPackages { get; set; }
}

public class PackageInfo
{
    public string PackageCode { get; set; } = "";
    public string PackageDescription { get; set; } = "";
    public double DayRate { get; set; }
    public int ComponentQuantity { get; set; } = 1;
}

public class PackageComponent
{
    public string ProductCode { get; set; } = "";
    public string Description { get; set; } = "";
    public double Quantity { get; set; } = 1;
    public bool IsVariable { get; set; }
    public ComponentType ComponentType { get; set; } = ComponentType.Standard;
    public bool IsSelectable { get; set; }
    public double IndividualRate { get; set; }
}

public enum ComponentType
{
    Standard = 0,   // Always included in the package
    Accessory = 1,  // Optional add-on
    Alternative = 2 // Can be swapped for another item
}

public class EquipmentRecommendations
{
    public List<CategoryRecommendation> Categories { get; set; } = new();
    public double EstimatedDayTotal { get; set; }
}

public class CategoryRecommendation
{
    public string CategoryName { get; set; } = "";
    public int RequestedQuantity { get; set; }
    public string OriginalRequest { get; set; } = "";
    public EquipmentItem? TopRecommendation { get; set; }
    public List<EquipmentItem> AlternativeOptions { get; set; } = new();
    public PackageRecommendation? PackageOption { get; set; }
    public bool NotFound { get; set; }
    public string? NotFoundMessage { get; set; }
}

public class PackageRecommendation
{
    public string PackageCode { get; set; } = "";
    public string PackageDescription { get; set; } = "";
    public string? Category { get; set; }
    public double DayRate { get; set; }
    public double ExtraDayRate { get; set; }
    public double WeeklyRate { get; set; }
    public string? PictureFileName { get; set; }
    public List<PackageComponent> Components { get; set; } = new();
    public string ReasonToRecommend { get; set; } = "";
    public string RequestedComponent { get; set; } = ""; // For reverse lookup - the component that was searched for
}

#endregion

