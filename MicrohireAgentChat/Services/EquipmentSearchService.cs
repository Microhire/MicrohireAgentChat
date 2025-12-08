using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Intelligent equipment search service that:
/// - Parses user requirements dynamically
/// - Maps keywords to proper database categories  
/// - Searches for actual matching equipment with pricing
/// - Detects if items are part of packages
/// - Returns recommendations with availability
/// </summary>
public sealed class EquipmentSearchService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<EquipmentSearchService> _logger;
    private readonly AIEquipmentQueryService? _aiQuery;

    public EquipmentSearchService(
        BookingDbContext db, 
        ILogger<EquipmentSearchService> logger,
        AIEquipmentQueryService? aiQuery = null)
    {
        _db = db;
        _logger = logger;
        _aiQuery = aiQuery;
    }

    #region Equipment Category Mapping

    /// <summary>
    /// Maps user-friendly equipment keywords to database categories and search terms
    /// NOTE: Database categories may have trailing spaces - we trim them during search
    /// </summary>
    private static readonly Dictionary<string, EquipmentCategoryMapping> CategoryMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // === LAPTOPS (DB category: "LAPTOP  " with spaces, "MACBOOK ") ===
        ["laptop"] = new("Laptops", new[] { "LAPTOP", "MACBOOK" }, "COMPUTER", new[] { "laptop", "production level", "dell", "lenovo" }, false),
        ["laptops"] = new("Laptops", new[] { "LAPTOP", "MACBOOK" }, "COMPUTER", new[] { "laptop", "production level", "dell", "lenovo" }, false),
        ["macbook"] = new("MacBooks", new[] { "MACBOOK" }, "COMPUTER", new[] { "macbook", "mac book", "apple" }, false),
        ["mac"] = new("MacBooks", new[] { "MACBOOK" }, "COMPUTER", new[] { "macbook", "mac", "apple" }, false),
        ["mac laptop"] = new("MacBooks", new[] { "MACBOOK" }, "COMPUTER", new[] { "macbook", "mac" }, false),
        ["windows laptop"] = new("Windows Laptops", new[] { "LAPTOP" }, "COMPUTER", new[] { "dell", "lenovo", "hp", "production level", "pc" }, false),
        ["windows"] = new("Windows Laptops", new[] { "LAPTOP" }, "COMPUTER", new[] { "dell", "lenovo", "hp", "production level" }, false),
        ["pc laptop"] = new("Windows Laptops", new[] { "LAPTOP" }, "COMPUTER", new[] { "dell", "lenovo", "hp", "production level" }, false),
        ["pc"] = new("Windows Laptops", new[] { "LAPTOP" }, "COMPUTER", new[] { "dell", "lenovo", "production level" }, false),
        ["notebook"] = new("Laptops", new[] { "LAPTOP", "MACBOOK" }, "COMPUTER", new[] { "laptop", "notebook" }, false),

        // === PROJECTORS (DB category: "PROJECTR") ===
        ["projector"] = new("Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "projector", "lumen", "laser" }, true),
        ["projectors"] = new("Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "projector", "lumen", "laser" }, true),
        ["big projector"] = new("High Brightness Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "10000", "12k", "20k", "lumen", "projector" }, true),
        ["big projectors"] = new("High Brightness Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "10000", "12k", "20k", "lumen", "projector" }, true),
        ["large projector"] = new("High Brightness Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "10000", "12k", "20k", "lumen", "projector" }, true),
        ["hd projector"] = new("HD Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "hd", "1080", "wuxga", "projector", "fhd" }, true),
        ["4k projector"] = new("4K Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "4k", "uhd", "projector" }, true),
        ["laser projector"] = new("Laser Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "laser", "projector" }, true),
        ["8000 lumen"] = new("8000+ Lumen Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "8000", "8500", "8k", "lumen" }, true),
        ["high brightness projector"] = new("High Brightness Projectors", new[] { "PROJECTR", "EPSON" }, "VISION", new[] { "8000", "10000", "12k", "20k", "lumen" }, true),

        // === SCREENS (DB category: "SCREEN") ===
        ["screen"] = new("Projection Screens", new[] { "SCREEN", "GRNDVW" }, "VISION", new[] { "screen", "fastfold", "projection", "stumpfl" }, true),
        ["screens"] = new("Projection Screens", new[] { "SCREEN", "GRNDVW" }, "VISION", new[] { "screen", "fastfold", "projection", "stumpfl" }, true),
        ["projection screen"] = new("Projection Screens", new[] { "SCREEN", "GRNDVW" }, "VISION", new[] { "screen", "fastfold", "projection", "stumpfl" }, true),
        ["projection screens"] = new("Projection Screens", new[] { "SCREEN", "GRNDVW" }, "VISION", new[] { "screen", "fastfold", "projection", "stumpfl" }, true),
        ["big screen"] = new("Large Projection Screens", new[] { "SCREEN" }, "VISION", new[] { "screen", "stumpfl", "fastfold", "10", "16", "20", "metre" }, true),
        ["big screens"] = new("Large Projection Screens", new[] { "SCREEN" }, "VISION", new[] { "screen", "stumpfl", "fastfold", "10", "16", "20", "metre" }, true),
        ["large screen"] = new("Large Projection Screens", new[] { "SCREEN" }, "VISION", new[] { "screen", "stumpfl", "fastfold", "10", "16", "20", "metre" }, true),
        ["large screens"] = new("Large Projection Screens", new[] { "SCREEN" }, "VISION", new[] { "screen", "stumpfl", "fastfold", "10", "16", "20", "metre" }, true),
        ["fastfold screen"] = new("Fastfold Screens", new[] { "SCREEN" }, "VISION", new[] { "fastfold", "screen" }, true),
        ["front projection screen"] = new("Front Projection Screens", new[] { "SCREEN" }, "VISION", new[] { "front", "screen", "fp" }, true),
        ["rear projection screen"] = new("Rear Projection Screens", new[] { "SCREEN" }, "VISION", new[] { "rear", "screen", "rp" }, true),
        ["display"] = new("Displays & Screens", new[] { "LCD-AV", "SCREEN" }, "VISION", new[] { "screen", "display", "lcd", "monitor" }, true),
        ["monitor"] = new("Monitors", new[] { "LCD-AV" }, "VISION", new[] { "monitor", "display", "lcd" }, true),
        ["tv"] = new("TVs & Displays", new[] { "LCD-AV" }, "VISION", new[] { "tv", "television", "display" }, true),

        // === MICROPHONES (DB category: "W/MIC", "MICROPH") ===
        ["wireless microphone"] = new("Wireless Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "wireless", "radio mic", "shure", "mipro" }, true),
        ["wireless microphones"] = new("Wireless Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "wireless", "radio mic", "shure", "mipro" }, true),
        ["wireless mic"] = new("Wireless Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "wireless", "radio mic", "shure", "mipro" }, true),
        ["wireless mics"] = new("Wireless Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "wireless", "radio mic", "shure", "mipro" }, true),
        ["microphone"] = new("Microphones", new[] { "W/MIC", "MICROPH" }, "AUDIO", new[] { "microphone", "mic", "wireless", "shure" }, true),
        ["microphones"] = new("Microphones", new[] { "W/MIC", "MICROPH" }, "AUDIO", new[] { "microphone", "mic", "wireless", "shure" }, true),
        ["mic"] = new("Microphones", new[] { "W/MIC", "MICROPH" }, "AUDIO", new[] { "mic", "wireless", "shure" }, true),
        ["mics"] = new("Microphones", new[] { "W/MIC", "MICROPH" }, "AUDIO", new[] { "mic", "wireless", "shure" }, true),
        ["handheld mic"] = new("Handheld Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "handheld", "wireless handheld" }, true),
        ["handheld microphone"] = new("Handheld Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "handheld", "wireless handheld" }, true),
        ["lapel mic"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "clip" }, true),
        ["lapel microphone"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "clip" }, true),
        ["lavalier"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack" }, true),
        ["clip on mic"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "clip", "wireless" }, true),
        ["clip-on mic"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "clip", "wireless" }, true),
        ["clip on microphone"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "clip", "wireless" }, true),
        ["wireless clip on mic"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "wireless" }, true),
        ["wireless clip on mics"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "wireless" }, true),
        ["wireless lapel mic"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "wireless" }, true),
        ["wireless lapel microphone"] = new("Lapel Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "lapel", "lavalier", "beltpack", "wireless" }, true),
        ["headset mic"] = new("Headset Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "headset", "earset", "head set" }, true),
        ["headset microphone"] = new("Headset Microphones", new[] { "W/MIC" }, "AUDIO", new[] { "headset", "earset", "head set" }, true),

        // === SPEAKERS (DB category: "SPEAKER") ===
        ["speaker"] = new("Speakers", new[] { "SPEAKER" }, "AUDIO", new[] { "speaker", "pa", "loudspeaker" }, true),
        ["speakers"] = new("Speakers", new[] { "SPEAKER" }, "AUDIO", new[] { "speaker", "pa", "loudspeaker" }, true),
        ["pa system"] = new("PA Systems", new[] { "SPEAKER" }, "AUDIO", new[] { "pa", "speaker", "sound" }, true),

        // === AUDIO (DB category: "MIXER  ", "AUDIO   ") ===
        ["audio"] = new("Audio Equipment", new[] { "AUDIO", "MIXER", "SPEAKER", "W/MIC" }, "AUDIO", new[] { "audio", "sound" }, true),
        ["mixer"] = new("Audio Mixers", new[] { "MIXER" }, "AUDIO", new[] { "mixer", "mixing", "console" }, true),
        ["sound system"] = new("Sound Systems", new[] { "SPEAKER", "MIXER" }, "AUDIO", new[] { "sound", "audio", "speaker" }, true),

        // === LIGHTING (DB category: "LED     ", various) ===
        ["lighting"] = new("Lighting", new[] { "LED", "LXFLOOD", "PROFILE", "FRESNEL" }, "LIGHTING", new[] { "light", "led", "wash" }, true),
        ["lights"] = new("Lighting", new[] { "LED", "LXFLOOD", "PROFILE" }, "LIGHTING", new[] { "light", "led" }, true),
        ["led"] = new("LED Lighting", new[] { "LED" }, "LIGHTING", new[] { "led", "light" }, true),
        ["spotlight"] = new("Spotlights", new[] { "PROFILE", "FOLLOWS" }, "LIGHTING", new[] { "spotlight", "spot", "followspot" }, true),

        // === STAGING (DB category: "STAGING ", "LECTERN ") ===
        ["stage"] = new("Staging", new[] { "STAGING" }, "STAGING", new[] { "stage", "platform", "riser" }, true),
        ["staging"] = new("Staging", new[] { "STAGING" }, "STAGING", new[] { "stage", "platform", "deck" }, true),
        ["lectern"] = new("Lecterns", new[] { "LECTERN" }, "STAGING", new[] { "lectern", "podium" }, true),
        ["podium"] = new("Lecterns & Podiums", new[] { "LECTERN" }, "STAGING", new[] { "lectern", "podium" }, true),

        // === OTHER ===
        ["camera"] = new("Cameras", new[] { "CAMERAS" }, "VISION", new[] { "camera", "ptz", "camcorder" }, true),
        ["cameras"] = new("Cameras", new[] { "CAMERAS" }, "VISION", new[] { "camera", "ptz" }, true),
        ["ipad"] = new("iPads", new[] { "IPAD" }, "COMPUTER", new[] { "ipad", "tablet" }, false),
        ["tablet"] = new("Tablets", new[] { "IPAD", "TABLET" }, "COMPUTER", new[] { "ipad", "tablet" }, false),
    };

    private record EquipmentCategoryMapping(
        string DisplayName,
        string[]? Categories,
        string? Group,
        string[] SearchTerms,
        bool HasPricedPackages
    );

    #endregion

    #region Parse User Requirements

    /// <summary>
    /// Parses user requirement text like "2 laptops, 2 projectors, 2 screens, 2 wireless microphones"
    /// Returns structured list of equipment requirements
    /// </summary>
    public List<EquipmentRequirement> ParseUserRequirements(string userText)
    {
        var requirements = new List<EquipmentRequirement>();
        if (string.IsNullOrWhiteSpace(userText)) return requirements;

        var text = userText.ToLowerInvariant();

        // Pattern: number + equipment type (e.g., "2 laptops", "3 wireless microphones")
        var patterns = new[]
        {
            // Specific patterns first
            @"(\d+)\s*(wireless\s+microphones?|wireless\s+mics?)",
            @"(\d+)\s*(handheld\s+mics?|handheld\s+microphones?)",
            @"(\d+)\s*(lapel\s+mics?|lavalier\s+mics?|clip[-\s]?on\s+mics?)",
            @"(\d+)\s*(wireless\s+clip[-\s]?on\s+mics?|wireless\s+lapel\s+mics?)",
            @"(\d+)\s*(headset\s+mics?)",
            @"(\d+)\s*(projection\s+screens?|fastfold\s+screens?)",
            @"(\d+)\s*(hd\s+projectors?|4k\s+projectors?|laser\s+projectors?)",
            @"(\d+)\s*(pa\s+systems?|sound\s+systems?)",
            @"(\d+)\s*(windows\s+laptops?|pc\s+laptops?)",
            // Generic patterns
            @"(\d+)\s*(laptops?|notebooks?)",
            @"(\d+)\s*(macbooks?)",
            @"(\d+)\s*(projectors?)",
            @"(\d+)\s*(screens?|displays?|monitors?)",
            @"(\d+)\s*(microphones?|mics?)",
            @"(\d+)\s*(speakers?)",
            @"(\d+)\s*(cameras?)",
            @"(\d+)\s*(ipads?|tablets?)",
            @"(\d+)\s*(lights?|lighting)",
            @"(\d+)\s*(lecterns?|podiums?)",
            @"(\d+)\s*(stages?|risers?)",
            @"(\d+)\s*(mixers?)",
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Success && int.TryParse(match.Groups[1].Value, out var qty) && qty > 0)
                {
                    var equipType = match.Groups[2].Value.Trim().ToLowerInvariant();
                    // Normalize plural forms
                    var normalized = NormalizeEquipmentType(equipType);
                    
                    // Avoid duplicates
                    if (!requirements.Any(r => r.NormalizedType == normalized))
                    {
                        requirements.Add(new EquipmentRequirement
                        {
                            OriginalText = match.Groups[2].Value.Trim(),
                            NormalizedType = normalized,
                            Quantity = qty
                        });
                    }
                }
            }
        }

        // Also check for equipment mentioned without numbers (assume 1)
        var implicitPatterns = new[]
        {
            "laptop", "macbook", "projector", "screen", "microphone", "mic",
            "speaker", "camera", "ipad", "tablet", "lectern", "podium"
        };

        foreach (var equip in implicitPatterns)
        {
            if (text.Contains(equip) && !requirements.Any(r => r.NormalizedType == equip))
            {
                // Check context - make sure it's not part of "no laptop" or similar
                var negativePattern = $@"(no|don't need|not|without)\s+{equip}";
                if (!Regex.IsMatch(text, negativePattern, RegexOptions.IgnoreCase))
                {
                    // Already captured by number patterns above most likely, skip
                }
            }
        }

        return requirements;
    }

    private static string NormalizeEquipmentType(string equipType)
    {
        // Remove plural 's' and normalize
        var normalized = equipType.ToLowerInvariant()
            .Replace("wireless clip-on mics", "wireless clip on mic")
            .Replace("wireless clip on mics", "wireless clip on mic")
            .Replace("clip-on mics", "clip on mic")
            .Replace("clip on mics", "clip on mic")
            .Replace("wireless lapel mics", "wireless lapel mic")
            .Replace("wireless lapel microphones", "wireless lapel mic")
            .Replace("wireless microphones", "wireless microphone")
            .Replace("wireless mics", "wireless mic")
            .Replace("microphones", "microphone")
            .Replace("laptops", "laptop")
            .Replace("notebooks", "notebook")
            .Replace("macbooks", "macbook")
            .Replace("projectors", "projector")
            .Replace("screens", "screen")
            .Replace("displays", "display")
            .Replace("monitors", "monitor")
            .Replace("speakers", "speaker")
            .Replace("cameras", "camera")
            .Replace("ipads", "ipad")
            .Replace("tablets", "tablet")
            .Replace("mics", "mic")
            .Replace("lights", "lighting")
            .Replace("lecterns", "lectern")
            .Replace("podiums", "podium")
            .Replace("stages", "stage")
            .Replace("risers", "riser")
            .Replace("mixers", "mixer")
            .Trim();

        return normalized;
    }

    #endregion

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
    public int Quantity { get; set; } = 1;
    public bool IsVariable { get; set; }
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
    public double DayRate { get; set; }
    public List<PackageComponent> Components { get; set; } = new();
    public string ReasonToRecommend { get; set; } = "";
}

#endregion

