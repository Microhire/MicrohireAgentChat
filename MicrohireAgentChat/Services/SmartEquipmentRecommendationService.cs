using MicrohireAgentChat.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Intelligently recommends equipment PACKAGES based on event context WITHOUT asking technical questions.
/// 
/// KEY CONCEPT: Equipment items are organized in packages. When a user asks for a "laptop",
/// we recommend the appropriate PACKAGE (e.g., PCLPRO, PCLP-L1) which includes:
/// - Components (standard items included in the package)
/// - Accessories (optional add-ons)  
/// - Alternatives (items that can be swapped)
/// 
/// The package has the pricing - individual components often don't have their own prices.
/// We query packages that have pricing from tblRatetbl and return their component breakdown.
/// </summary>
public sealed class SmartEquipmentRecommendationService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<SmartEquipmentRecommendationService> _logger;

    public SmartEquipmentRecommendationService(
        BookingDbContext db,
        ILogger<SmartEquipmentRecommendationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get intelligent equipment recommendations based on event context.
    /// Returns packages with their full component breakdown and pricing.
    /// </summary>
    public async Task<SmartEquipmentRecommendation> GetRecommendationsAsync(
        EventContext context,
        CancellationToken ct = default)
    {
        var result = new SmartEquipmentRecommendation
        {
            EventContext = context,
            Items = new List<RecommendedEquipmentItem>()
        };

        _logger.LogInformation("Getting smart equipment recommendations for {EventType} with {Attendees} attendees",
            context.EventType, context.ExpectedAttendees);

        // Process each equipment request
        foreach (var request in context.EquipmentRequests)
        {
            var recommendations = await GetEquipmentForRequestAsync(request, context, ct);
            result.Items.AddRange(recommendations);
        }

        // Calculate totals
        result.TotalDayRate = result.Items.Sum(i => i.UnitPrice * i.Quantity);

        _logger.LogInformation("Smart recommendations complete: {Count} items, ${Total}/day",
            result.Items.Count, result.TotalDayRate);

        return result;
    }

    /// <summary>
    /// Find the best package containing a specific equipment item.
    /// Use this when someone asks for a specific item (e.g., "Dell 3580") 
    /// to find which package they should rent.
    /// </summary>
    public async Task<PackageRecommendation?> FindPackageForComponentAsync(
        string componentCode,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Finding package for component: {ComponentCode}", componentCode);

        var trimmedCode = componentCode.Trim().ToUpperInvariant();

        // Find all packages that contain this component
        var parentPackages = await _db.VwProdsComponents
            .AsNoTracking()
            .Where(v => (v.ProductCode ?? "").Trim().ToUpper() == trimmedCode)
            .Select(v => (v.ParentCode ?? "").Trim())
            .Distinct()
            .ToListAsync(ct);

        if (parentPackages.Count == 0)
        {
            _logger.LogWarning("No packages found containing component: {ComponentCode}", componentCode);
            return null;
        }

        // Get package details with pricing
        var packages = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => parentPackages.Contains((p.product_code ?? "").Trim()))
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.PictureFileName
            })
            .ToListAsync(ct);

        // Get pricing for these packages
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && parentPackages.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days, r.rate_week })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        // Find the best package (prefer same category as component, with pricing)
        var componentInfo = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => (p.product_code ?? "").Trim().ToUpper() == trimmedCode)
            .Select(p => new { p.category })
            .FirstOrDefaultAsync(ct);

        var bestPackage = packages
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim();
                var hasPrice = priceLookup.TryGetValue(code, out var priceInfo);
                var rate = hasPrice ? (priceInfo?.rate_1st_day ?? 0) : 0;
                var sameCategory = (p.category ?? "").Trim() == (componentInfo?.category ?? "").Trim();

                return new
                {
                    Product = p,
                    Rate = rate,
                    SameCategory = sameCategory,
                    Score = (rate > 0 ? 100 : 0) + (sameCategory ? 50 : 0)
                };
            })
            .Where(x => x.Rate > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Rate) // Prefer cheaper if same score
            .FirstOrDefault();

        if (bestPackage == null)
        {
            _logger.LogWarning("No priced packages found for component: {ComponentCode}", componentCode);
            return null;
        }

        var packageCode = (bestPackage.Product.product_code ?? "").Trim();

        // Get components of this package
        var components = await GetPackageComponentsAsync(packageCode, ct);

        return new PackageRecommendation
        {
            PackageCode = packageCode,
            PackageDescription = (bestPackage.Product.descriptionv6 ?? bestPackage.Product.PrintedDesc ?? "").Trim(),
            Category = bestPackage.Product.category,
            DayRate = bestPackage.Rate,
            ExtraDayRate = priceLookup.TryGetValue(packageCode, out var p) ? (p.rate_extra_days ?? 0) : 0,
            WeeklyRate = priceLookup.TryGetValue(packageCode, out var pw) ? (pw.rate_week ?? 0) : 0,
            PictureFileName = bestPackage.Product.PictureFileName,
            Components = components,
            RequestedComponent = componentCode
        };
    }

    /// <summary>
    /// Get the full component breakdown for a package.
    /// Returns components, accessories, and alternatives.
    /// </summary>
    public async Task<List<PackageComponent>> GetPackageComponentsAsync(
        string packageCode,
        CancellationToken ct = default)
    {
        var trimmedCode = packageCode.Trim();

        var components = await _db.VwProdsComponents
            .AsNoTracking()
            .Where(v => (v.ParentCode ?? "").Trim() == trimmedCode)
            .OrderBy(v => v.VariablePart)
            .ThenBy(v => v.SubSeqNo)
            .Select(v => new
            {
                v.ProductCode,
                v.Description,
                v.VariablePart,
                v.Qty,
                v.SelectComp
            })
            .ToListAsync(ct);

        // Get pricing for components (some may have individual prices)
        var componentCodes = components.Select(c => (c.ProductCode ?? "").Trim()).ToList();
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && componentCodes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p.rate_1st_day ?? 0, StringComparer.OrdinalIgnoreCase);

        return components.Select(c =>
        {
            var code = (c.ProductCode ?? "").Trim();
            return new PackageComponent
            {
                ProductCode = code,
                Description = (c.Description ?? "").Trim(),
                ComponentType = c.VariablePart switch
                {
                    0 => ComponentType.Standard,
                    1 => ComponentType.Accessory,
                    2 => ComponentType.Alternative,
                    _ => ComponentType.Standard
                },
                Quantity = c.Qty ?? 1,
                IsSelectable = c.SelectComp == "Y",
                IndividualRate = priceLookup.TryGetValue(code, out var rate) ? rate : 0
            };
        }).ToList();
    }

    private async Task<List<RecommendedEquipmentItem>> GetEquipmentForRequestAsync(
        EquipmentRequest request,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        var equipmentType = request.EquipmentType.ToLowerInvariant();
        var quantity = request.Quantity;

        // Handle combined equipment types like "projector+screen" or "projector and screen"
        var separators = new[] { "+", " and ", "&", ", " };
        var combinedTypes = new List<string> { equipmentType };
        
        foreach (var sep in separators)
        {
            if (equipmentType.Contains(sep))
            {
                combinedTypes = equipmentType.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
                break;
            }
        }

        // Process each equipment type (handles combined types)
        foreach (var eqType in combinedTypes)
        {
            var processedItems = await GetSingleEquipmentTypeAsync(eqType, quantity, request.Preference, request.MicrophoneType, context, ct);
            items.AddRange(processedItems);
        }

        return items;
    }

    private async Task<List<RecommendedEquipmentItem>> GetSingleEquipmentTypeAsync(
        string equipmentType,
        int quantity,
        string? preference,
        string? microphoneType,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();
        var normalizedType = equipmentType.Trim().ToLowerInvariant();

        // Determine the right specs based on event context
        switch (normalizedType)
        {
            case "laptop":
            case "laptops":
                items.AddRange(await RecommendLaptopPackagesAsync(quantity, preference, context, ct));
                break;

            case "projector":
            case "projectors":
                items.AddRange(await RecommendProjectorPackagesAsync(quantity, context, ct));
                break;

            case "screen":
            case "screens":
                items.AddRange(await RecommendScreenPackagesAsync(quantity, context, ct));
                break;

            case "wireless mic":
            case "wireless mics":
            case "wireless microphone":
            case "wireless microphones":
            case "microphone":
            case "microphones":
            case "mic":
            case "mics":
                items.AddRange(await RecommendMicrophonePackagesAsync(quantity, microphoneType, context, ct));
                break;

            case "speaker":
            case "speakers":
            case "pa":
            case "pa system":
            case "audio":
                items.AddRange(await RecommendSpeakerPackagesAsync(quantity, context, ct));
                break;

            default:
                _logger.LogWarning("Unknown equipment type: {Type}", normalizedType);
                break;
        }

        return items;
    }

    #region Laptop Package Recommendations

    /// <summary>
    /// Recommends laptop packages from specific package containers:
    /// - LAPPACK (PC Laptop Packages) contains: PCLPRO, PCLP-L1, PCLP-L2, PCLP-L3, PCPROLT1
    /// - MBPPACK (Macbook Pro Packages) contains: 13MBP-LM, 13MBP-LT, 13MBP-L1, 13MBP-L2
    /// Each package contains components (laptops, mouse, case, etc.), accessories, and alternatives.
    /// </summary>
    private async Task<List<RecommendedEquipmentItem>> RecommendLaptopPackagesAsync(
        int quantity,
        string? preference,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        // Determine laptop type based on preference or event type
        bool isWindows = preference?.ToLower().Contains("windows") == true ||
                        preference?.ToLower().Contains("pc") == true;
        bool isMac = preference?.ToLower().Contains("mac") == true;

        if (!isWindows && !isMac)
        {
            isWindows = context.EventType.ToLower() switch
            {
                "hackathon" => true,
                "conference" => true,
                "networking" => true,
                "corporate" => true,
                "training" => true,
                "seminar" => true,
                "wedding" => true,
                "creative" => false,
                "film" => false,
                "music" => false,
                _ => true
            };
            isMac = !isWindows;
        }

        // Determine performance level based on event type
        bool isHighPerformance = context.EventType.ToLower() switch
        {
            "hackathon" => true,
            "creative" => true,
            "video production" => true,
            "film" => true,
            _ => false
        };

        // Get packages from the correct SubCategory:
        // - Windows: category=LAPTOP, SubCategory=LAPPACK, product_Config=1
        // - Mac: category=MACBOOK, SubCategory=MBPPACK, product_Config=1
        string targetCategory = isMac ? "MACBOOK" : "LAPTOP";
        string targetSubCategory = isMac ? "MBPPACK" : "LAPPACK";

        _logger.LogInformation("Looking for laptop packages: category={Category}, SubCategory={SubCategory} (isMac={IsMac}, isHighPerf={IsHighPerf})",
            targetCategory, targetSubCategory, isMac, isHighPerformance);

        // Get all packages from the correct SubCategory with product_Config=1 (package)
        var packages = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => (p.category ?? "").Trim() == targetCategory)
            .Where(p => (p.SubCategory ?? "").Trim() == targetSubCategory)
            .Where(p => p.ProductConfig == 1) // Only packages, not individual items
            .Where(p =>
                !(p.descriptionv6 ?? "").ToLower().Contains("long term hire") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("discontinued"))
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.PictureFileName
            })
            .ToListAsync(ct);

        if (packages.Count == 0)
        {
            _logger.LogWarning("No packages found in category={Category}, SubCategory={SubCategory}", targetCategory, targetSubCategory);
            return items;
        }

        var packageCodes = packages.Select(p => (p.product_code ?? "").Trim()).ToList();
        _logger.LogInformation("Found {Count} packages in {SubCategory}: {Codes}",
            packages.Count, targetSubCategory, string.Join(", ", packageCodes));

        // Get pricing for these packages
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && packageCodes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { 
                Code = (r.product_code ?? "").Trim(), 
                r.rate_1st_day,
                r.rate_extra_days,
                r.rate_week 
            })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        // Score and rank packages - select ONE best package
        var ranked = packages
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim().ToUpperInvariant();
                var desc = (p.descriptionv6 ?? "").ToLower();
                var hasPrice = priceLookup.TryGetValue(code, out var priceInfo);
                var rate = hasPrice ? (priceInfo?.rate_1st_day ?? 0) : 0;

                int score = 0;
                if (rate > 0) score += 100; // Must have pricing

                // For Windows packages (LAPPACK children):
                // PCLPRO = Production Level Pro (~$195/day) - high performance
                // PCLP-L1 = Level 1 (~$163/day) - standard
                // PCLP-L2 = Level 2 (~$163/day) - standard  
                // PCLP-L3 = Level 3 (~$130/day) - budget
                if (isHighPerformance)
                {
                    if (code == "PCLPRO" || code.Contains("PRO")) score += 50; // Pro package
                    if (desc.Contains("production level pro")) score += 30;
                }
                else
                {
                    // Standard use - prefer L1 or L2 packages
                    if (code == "PCLP-L1" || code == "PCLP-L2") score += 50;
                    if (code == "13MBP-L1" || code == "13MBP-L2") score += 50;
                    if (desc.Contains("level 1") || desc.Contains("level 2")) score += 30;
                    // Avoid pro packages for standard use (overkill)
                    if (code.Contains("PRO")) score -= 10;
                }

                // Penalize long-term hire packages for short events
                if (code.Contains("LT") || desc.Contains("long term")) score -= 50;

                return new { Product = p, Score = score, Rate = rate, PriceInfo = priceInfo };
            })
            .Where(x => x.Rate > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => isHighPerformance ? -x.Rate : x.Rate)
            .FirstOrDefault();

        if (ranked != null)
        {
            var packageCode = (ranked.Product.product_code ?? "").Trim();
            
            _logger.LogInformation("Selected package: {PackageCode} @ ${Rate}/day (Score: {Score})",
                packageCode, ranked.Rate, ranked.Score);

            // Get components of THIS specific package
            var components = await GetPackageComponentsAsync(packageCode, ct);

            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = packageCode,
                Description = (ranked.Product.descriptionv6 ?? ranked.Product.PrintedDesc ?? "").Trim(),
                Category = ranked.Product.category,
                Quantity = quantity,
                UnitPrice = ranked.Rate,
                ExtraDayRate = ranked.PriceInfo?.rate_extra_days ?? 0,
                WeeklyRate = ranked.PriceInfo?.rate_week ?? 0,
                PictureFileName = ranked.Product.PictureFileName,
                IsPackage = true,
                Components = components,
                RecommendationReason = isHighPerformance
                    ? $"Production-grade {(isMac ? "Mac" : "Windows")} laptop package - ideal for {context.EventType}"
                    : $"Standard {(isMac ? "Mac" : "Windows")} laptop package - perfect for presentations at your {context.EventType}"
            });

            _logger.LogInformation("Package {PackageCode} has {ComponentCount} components",
                packageCode, components.Count);
        }
        else
        {
            _logger.LogWarning("No suitable laptop package found with pricing");
        }

        return items;
    }

    #endregion

    #region Projector Package Recommendations

    private async Task<List<RecommendedEquipmentItem>> RecommendProjectorPackagesAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        // Determine projector specs based on room size and attendees
        int minLumens, maxLumens;
        string sizeCategory;

        if (context.ExpectedAttendees <= 50)
        {
            minLumens = 3000;
            maxLumens = 5500;
            sizeCategory = "small room";
        }
        else if (context.ExpectedAttendees <= 150)
        {
            minLumens = 5000;
            maxLumens = 10000;
            sizeCategory = "medium room";
        }
        else if (context.ExpectedAttendees <= 300)
        {
            minLumens = 8000;
            maxLumens = 15000;
            sizeCategory = "large room";
        }
        else
        {
            minLumens = 12000;
            maxLumens = 25000;
            sizeCategory = "very large venue";
        }

        // Search for projectors with pricing
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => (p.category ?? "").Trim() == "PROJECTR")
            .Where(p =>
                !(p.descriptionv6 ?? "").ToLower().Contains("lens") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("bracket") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("stand") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("plate") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("remote") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("discontinued"))
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.PictureFileName
            })
            .Take(50)
            .ToListAsync(ct);

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

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        var ranked = products
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim();
                var desc = (p.descriptionv6 ?? "").ToLower();
                var hasPrice = priceLookup.TryGetValue(code, out var priceInfo);
                var rate = hasPrice ? (priceInfo?.rate_1st_day ?? 0) : 0;

                int lumens = ExtractLumens(desc);

                int score = 0;
                if (lumens >= minLumens && lumens <= maxLumens) score += 30;
                if (lumens > 0 && lumens < minLumens) score += 5;
                if (lumens > maxLumens) score += 10;
                if (desc.Contains("laser")) score += 5;
                if (rate > 0) score += 50;

                return new { Product = p, Score = score, Rate = rate, Lumens = lumens, PriceInfo = priceInfo };
            })
            .Where(x => x.Rate > 0 && x.Lumens > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Rate)
            .FirstOrDefault();

        if (ranked != null)
        {
            var productCode = (ranked.Product.product_code ?? "").Trim();
            var components = await GetPackageComponentsAsync(productCode, ct);

            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = productCode,
                Description = (ranked.Product.descriptionv6 ?? ranked.Product.PrintedDesc ?? "").Trim(),
                Category = ranked.Product.category,
                Quantity = quantity,
                UnitPrice = ranked.Rate,
                ExtraDayRate = ranked.PriceInfo?.rate_extra_days ?? 0,
                PictureFileName = ranked.Product.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"Recommended for {sizeCategory} with {context.ExpectedAttendees} attendees - {ranked.Lumens} lumens provides excellent visibility"
            });
        }

        return items;
    }

    private static int ExtractLumens(string description)
    {
        var patterns = new[]
        {
            @"(\d{4,5})\s*(?:lumen|ansi)",
            @"(\d+)k\s*(?:lumen|ansi)?",
            @"(\d+)\s*ansi"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(description, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                if (int.TryParse(value, out var lumens))
                {
                    return lumens < 100 ? lumens * 1000 : lumens;
                }
            }
        }

        return 0;
    }

    #endregion

    #region Screen Package Recommendations

    private async Task<List<RecommendedEquipmentItem>> RecommendScreenPackagesAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        string[] sizeTerms;
        string sizeCategory;

        if (context.ExpectedAttendees <= 50)
        {
            sizeTerms = new[] { "10", "8", "6ft", "tripod" };
            sizeCategory = "small to medium";
        }
        else if (context.ExpectedAttendees <= 150)
        {
            sizeTerms = new[] { "16", "13", "12", "fastfold" };
            sizeCategory = "medium to large";
        }
        else if (context.ExpectedAttendees <= 300)
        {
            sizeTerms = new[] { "16", "20", "fastfold", "stumpfl" };
            sizeCategory = "large";
        }
        else
        {
            sizeTerms = new[] { "20", "30", "stumpfl" };
            sizeCategory = "extra large";
        }

        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => (p.category ?? "").Trim() == "SCREEN")
            .Where(p =>
                !(p.descriptionv6 ?? "").ToLower().Contains("drape") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("discontinued"))
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.PictureFileName
            })
            .Take(50)
            .ToListAsync(ct);

        var productCodes = products.Select(p => (p.product_code ?? "").Trim()).ToList();
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && productCodes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        var ranked = products
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim();
                var desc = (p.descriptionv6 ?? "").ToLower();
                var hasPrice = priceLookup.TryGetValue(code, out var priceInfo);
                var rate = hasPrice ? (priceInfo?.rate_1st_day ?? 0) : 0;

                int score = 0;
                if (sizeTerms.Any(t => desc.Contains(t.ToLower()))) score += 20;
                if (desc.Contains("fastfold") || desc.Contains("stumpfl")) score += 10;
                if (desc.Contains("16:9")) score += 5;
                if (rate > 0) score += 50;

                return new { Product = p, Score = score, Rate = rate, PriceInfo = priceInfo };
            })
            .Where(x => x.Rate > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Rate)
            .FirstOrDefault();

        if (ranked != null)
        {
            var productCode = (ranked.Product.product_code ?? "").Trim();
            var components = await GetPackageComponentsAsync(productCode, ct);

            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = productCode,
                Description = (ranked.Product.descriptionv6 ?? ranked.Product.PrintedDesc ?? "").Trim(),
                Category = ranked.Product.category,
                Quantity = quantity,
                UnitPrice = ranked.Rate,
                ExtraDayRate = ranked.PriceInfo?.rate_extra_days ?? 0,
                PictureFileName = ranked.Product.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"{sizeCategory} screen size - optimal viewing for {context.ExpectedAttendees} attendees in {context.RoomName ?? "the venue"}"
            });
        }

        return items;
    }

    #endregion

    #region Microphone Package Recommendations

    private async Task<List<RecommendedEquipmentItem>> RecommendMicrophonePackagesAsync(
        int quantity,
        string? micType,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        bool isHandheld = micType?.ToLower().Contains("handheld") == true ||
                         micType?.ToLower().Contains("hand held") == true;
        bool isLapel = micType?.ToLower().Contains("lapel") == true ||
                      micType?.ToLower().Contains("clip") == true ||
                      micType?.ToLower().Contains("lavalier") == true;

        if (!isHandheld && !isLapel)
        {
            isHandheld = context.EventType.ToLower() switch
            {
                "networking" => true,
                "conference" => true,
                "panel" => true,
                "q&a" => true,
                "training" => false,
                "seminar" => false,
                "presentation" => false,
                "keynote" => false,
                "wedding" => true,
                _ => true
            };
            isLapel = !isHandheld;
        }

        string[] searchTerms = isHandheld
            ? new[] { "handheld", "wireless handheld", "hand held" }
            : new[] { "lapel", "lavalier", "beltpack", "clip" };

        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => (p.category ?? "").Trim() == "W/MIC")
            .Where(p =>
                !(p.descriptionv6 ?? "").ToLower().Contains("receiver") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("charger") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("antenna") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("cable") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("discontinued"))
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.PictureFileName
            })
            .Take(50)
            .ToListAsync(ct);

        var productCodes = products.Select(p => (p.product_code ?? "").Trim()).ToList();
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && productCodes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        var ranked = products
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim();
                var desc = (p.descriptionv6 ?? "").ToLower();
                var hasPrice = priceLookup.TryGetValue(code, out var priceInfo);
                var rate = hasPrice ? (priceInfo?.rate_1st_day ?? 0) : 0;

                int score = 0;
                if (searchTerms.Any(t => desc.Contains(t.ToLower()))) score += 30;
                if (isHandheld && (desc.Contains("handheld") || desc.Contains("hand held"))) score += 20;
                if (isLapel && (desc.Contains("lapel") || desc.Contains("lavalier") || desc.Contains("beltpack"))) score += 20;
                if (desc.Contains("shure")) score += 10;
                if (desc.Contains("wireless")) score += 5;
                if (rate > 0) score += 50;

                return new { Product = p, Score = score, Rate = rate, PriceInfo = priceInfo };
            })
            .Where(x => x.Rate > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Rate)
            .FirstOrDefault();

        if (ranked != null)
        {
            var micTypeName = isHandheld ? "Handheld" : "Lapel";
            var productCode = (ranked.Product.product_code ?? "").Trim();
            var components = await GetPackageComponentsAsync(productCode, ct);

            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = productCode,
                Description = (ranked.Product.descriptionv6 ?? ranked.Product.PrintedDesc ?? "").Trim(),
                Category = ranked.Product.category,
                Quantity = quantity,
                UnitPrice = ranked.Rate,
                ExtraDayRate = ranked.PriceInfo?.rate_extra_days ?? 0,
                PictureFileName = ranked.Product.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"{micTypeName} wireless microphone - ideal for {context.EventType} with presenters who " +
                    (isHandheld ? "share microphones or host Q&A sessions" : "need hands-free mobility during presentations")
            });
        }

        return items;
    }

    #endregion

    #region Speaker Package Recommendations

    private async Task<List<RecommendedEquipmentItem>> RecommendSpeakerPackagesAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        string[] searchTerms;
        string sizeCategory;

        if (context.ExpectedAttendees <= 50)
        {
            searchTerms = new[] { "compact", "portable", "bose", "s1" };
            sizeCategory = "compact";
        }
        else if (context.ExpectedAttendees <= 150)
        {
            searchTerms = new[] { "powered", "active", "jbl", "qsc", "12" };
            sizeCategory = "powered";
        }
        else
        {
            searchTerms = new[] { "array", "line", "showmatch" };
            sizeCategory = "line array";
        }

        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => (p.category ?? "").Trim() == "SPEAKER")
            .Where(p =>
                !(p.descriptionv6 ?? "").ToLower().Contains("stand") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("bracket") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("pole") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("discontinued"))
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.PictureFileName
            })
            .Take(50)
            .ToListAsync(ct);

        var productCodes = products.Select(p => (p.product_code ?? "").Trim()).ToList();
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && productCodes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        var ranked = products
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim();
                var desc = (p.descriptionv6 ?? "").ToLower();
                var hasPrice = priceLookup.TryGetValue(code, out var priceInfo);
                var rate = hasPrice ? (priceInfo?.rate_1st_day ?? 0) : 0;

                int score = 0;
                if (searchTerms.Any(t => desc.Contains(t.ToLower()))) score += 20;
                if (rate > 0) score += 50;

                return new { Product = p, Score = score, Rate = rate, PriceInfo = priceInfo };
            })
            .Where(x => x.Rate > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Rate)
            .FirstOrDefault();

        if (ranked != null)
        {
            var productCode = (ranked.Product.product_code ?? "").Trim();
            var components = await GetPackageComponentsAsync(productCode, ct);

            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = productCode,
                Description = (ranked.Product.descriptionv6 ?? ranked.Product.PrintedDesc ?? "").Trim(),
                Category = ranked.Product.category,
                Quantity = quantity,
                UnitPrice = ranked.Rate,
                ExtraDayRate = ranked.PriceInfo?.rate_extra_days ?? 0,
                PictureFileName = ranked.Product.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"{sizeCategory} speaker system - provides clear audio coverage for {context.ExpectedAttendees} attendees"
            });
        }

        return items;
    }

    #endregion
}

#region Models

public class EventContext
{
    public string EventType { get; set; } = "";
    public int ExpectedAttendees { get; set; }
    public string? VenueName { get; set; }
    public string? RoomName { get; set; }
    public int DurationDays { get; set; } = 1;
    public int NumberOfSpeakers { get; set; }
    public int NumberOfPresentations { get; set; }
    public bool NeedsLaptops { get; set; }
    public List<EquipmentRequest> EquipmentRequests { get; set; } = new();
}

public class EquipmentRequest
{
    public string EquipmentType { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string? Preference { get; set; } // e.g., "windows", "mac"
    public string? MicrophoneType { get; set; } // e.g., "handheld", "lapel"
    public string? SpecificModel { get; set; } // e.g., "Dell 3580" for reverse lookup
}

public class SmartEquipmentRecommendation
{
    public EventContext? EventContext { get; set; }
    public List<RecommendedEquipmentItem> Items { get; set; } = new();
    public double TotalDayRate { get; set; }
}

public class RecommendedEquipmentItem
{
    public string ProductCode { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double ExtraDayRate { get; set; }
    public double WeeklyRate { get; set; }
    public string? PictureFileName { get; set; }
    public string RecommendationReason { get; set; } = "";
    
    /// <summary>
    /// True if this item is a package containing multiple components
    /// </summary>
    public bool IsPackage { get; set; }
    
    /// <summary>
    /// Components included in this package (if IsPackage is true)
    /// </summary>
    public List<PackageComponent> Components { get; set; } = new();
    
    /// <summary>
    /// Total price for this line item (UnitPrice * Quantity)
    /// </summary>
    public double TotalPrice => UnitPrice * Quantity;
}

// PackageComponent, ComponentType, and PackageRecommendation are defined in EquipmentSearchService.cs

#endregion
