using MicrohireAgentChat.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

public sealed partial class SmartEquipmentRecommendationService
{
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
            var processedItems = await GetSingleEquipmentTypeAsync(eqType, quantity, request.Preference, request.MicrophoneType, context, ct, request.SpeakerStyle);
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
        CancellationToken ct,
        string? speakerStyle = null)
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
            case "display":
            case "displays":
            case "monitor":
            case "monitors":
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
                items.AddRange(await RecommendSpeakerPackagesAsync(quantity, context, speakerStyle, ct));
                break;

            case "camera":
            case "cameras":
            case "webcam":
            case "video camera":
            case "recording":
                items.AddRange(await RecommendCameraPackagesAsync(quantity, context, ct));
                break;

            case "clicker":
            case "clickers":
            case "presentation remote":
            case "presentation remotes":
            case "wireless presenter":
            case "wireless presenters":
                items.AddRange(await RecommendClickerPackagesAsync(quantity, context, ct));
                break;

            case "hdmi_adaptor":
            case "hdmi adaptor":
            case "usbc adaptor":
                items.AddRange(await RecommendProductByCodeAsync("USBCMX2", quantity, "USBC to HDMI adaptor for laptop connection", context, ct));
                break;

            case "switcher":
            case "laptop switcher":
            case "vision switcher":
                if (context.NumberOfPresentations > 0)
                {
                    // Presenter-based scaling: 1 presenter = no switcher; 2+ = ceil(count/4)
                    if (context.NumberOfPresentations <= 1)
                    {
                        _logger.LogInformation("No switcher added: only {Count} presenter", context.NumberOfPresentations);
                        break;
                    }
                    var presenterSwitcherQty = (int)Math.Ceiling(context.NumberOfPresentations / 4.0);
                    items.AddRange(await RecommendProductByCodeAsync("V1HD", presenterSwitcherQty,
                        context.NumberOfPresentations > 4
                            ? $"HDMI switcher for {context.NumberOfPresentations} presenters ({presenterSwitcherQty} units needed, max 4 inputs each)"
                            : $"HDMI switcher for {context.NumberOfPresentations} presenters (max 4 inputs)",
                        context, ct));
                }
                else
                {
                    // Fallback: laptop-count-based scaling (used when presenter_count not supplied)
                    var laptopCountForSwitcher = context.EquipmentRequests
                        .Where(r => (r.EquipmentType ?? "").Trim().ToLowerInvariant() is "laptop" or "laptops")
                        .Sum(r => r.Quantity);
                    var switcherQty = laptopCountForSwitcher > 4
                        ? (int)Math.Ceiling(laptopCountForSwitcher / 4.0)
                        : Math.Max(quantity, 1);
                    items.AddRange(await RecommendProductByCodeAsync("V1HD", switcherQty,
                        laptopCountForSwitcher > 4
                            ? $"HDMI switcher for {laptopCountForSwitcher} laptops ({switcherQty} units needed, max 4 inputs each)"
                            : "HDMI switcher for seamless laptop switching (max 4 inputs)",
                        context, ct));
                }
                break;

            case "laptop_at_stage":
            case "laptop at stage":
                // Laptop on stage = 2× SDICROSS (signal path for lectern/operator when switcher + lectern)
                items.AddRange(await RecommendProductByCodeAsync("SDICROSS", 2, "SDI/HDMI path for laptop on stage", context, ct));
                break;

            case "flipchart":
            case "flipcharts":
                items.AddRange(await RecommendProductByCodeAsync("NATFLIPC", quantity, "Flipchart with paper and markers", context, ct));
                break;

            case "lectern":
            case "lecterns":
            case "podium":
                items.AddRange(await RecommendLecternAsync(quantity, context, ct));
                break;

            case "foldback_monitor":
            case "foldback monitor":
            case "confidence monitor":
                items.AddRange(await RecommendFoldbackMonitorAsync(quantity, context, ct));
                break;

            case "video_conference_unit":
            case "video conference unit":
            case "videoconference":
                items.AddRange(await RecommendProductByCodeAsync("LOG4kCAM", quantity, "Video conference unit for Teams/Zoom", context, ct));
                break;

            case "av":
            case "base av":
            case "base_av":
                items.AddRange(await RecommendAvPackagesAsync(quantity, context, ct));
                break;
 
            default:
                _logger.LogWarning("Unknown equipment type: {Type}. Supported types: laptop, projector, screen, display, monitor, microphone, speaker, camera, clicker, hdmi_adaptor, switcher, laptop_at_stage, flipchart, lectern, foldback_monitor, video_conference_unit, av, base av", normalizedType);
                break;

        }

        if (items.Count == 0)
        {
            _logger.LogWarning("No products found for equipment type: {Type}", normalizedType);
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

        var aiCodes = await GetAiCatalogCodesAsync(ct);
        // Get all packages from the correct SubCategory with product_Config=1 (package)
        var packages = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
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

    #region AV / Base AV Package Recommendations

    /// <summary>
    /// Recommends integrated AV packages (Projector + Screen + Audio).
    /// Used by the "Base AV" form and general AV requests. 
    /// </summary>
    private async Task<List<RecommendedEquipmentItem>> RecommendAvPackagesAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        // Room-aware: try venue-installed WSB AV packages first
        var roomAvPackages = await TryGetRoomSpecificPackagesAsync(
            context.VenueName, context.RoomName, "av", quantity, context, ct);
            
        if (roomAvPackages.Count > 0)
        {
            _logger.LogInformation("Using room-specific AV packages: {Count} items", roomAvPackages.Count);
            return roomAvPackages;
        }

        // Fallback: If no integrated package, recommend projector + speaker packages separately
        _logger.LogInformation("No integrated AV package found; recommending standard projector and speaker packages.");
        
        var projectorItems = await RecommendProjectorPackagesAsync(quantity, context, ct);
        var speakerItems = await RecommendSpeakerPackagesAsync(1, context, null, ct);
        
        items.AddRange(projectorItems);
        items.AddRange(speakerItems);

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

        // Thrive must use its dedicated room package (THRVAVP) when projection/display is needed.
        var isThriveBoardroom = (context.RoomName ?? "").Contains("thrive", StringComparison.OrdinalIgnoreCase);
        // Other very small rooms can still fall back to attendee-based standalone projector logic.
        bool skipRoomPackages = !isThriveBoardroom && await IsSmallRoomAsync(context.VenueName, context.RoomName, ct);
        if (!skipRoomPackages)
        {
            // Room-aware: try venue-installed WSB vision/AV packages first when Westin/Four Points + room known
            var roomPkgs = await TryGetRoomSpecificPackagesAsync(
                context.VenueName, context.RoomName, "projector", quantity, context, ct);
            if (roomPkgs.Count > 0)
            {
                var mappedCode = ResolveWestinProjectorPackageCode(context.ProjectorAreas, context.RoomName);
                if (!string.IsNullOrWhiteSpace(mappedCode))
                {
                    var mappedPackage = roomPkgs.FirstOrDefault(i =>
                        string.Equals(i.ProductCode, mappedCode, StringComparison.OrdinalIgnoreCase));
                    if (mappedPackage != null)
                    {
                        _logger.LogInformation("Using projector-area mapped package {Code} for areas [{Areas}]",
                            mappedCode, string.Join(", ", context.ProjectorAreas));
                        return new List<RecommendedEquipmentItem> { mappedPackage };
                    }

                    _logger.LogWarning("Mapped projector package {Code} was not available in room package candidates [{Codes}]",
                        mappedCode,
                        string.Join(", ", roomPkgs.Select(p => p.ProductCode)));
                }

                _logger.LogInformation("Using room-specific projector packages: {Count} items", roomPkgs.Count);
                return roomPkgs;
            }
        }
        else
            _logger.LogInformation("Skipping room-specific projector packages for small room (capacity <= 15)");

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
        var aiCodes = await GetAiCatalogCodesAsync(ct);
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
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

    private static string? ResolveWestinProjectorPackageCode(IEnumerable<string>? projectorAreas, string? roomName = null)
    {
        if (projectorAreas == null) return null;

        var normalized = projectorAreas
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim().ToUpperInvariant())
            .Where(a => a.Length == 1 && a[0] is >= 'A' and <= 'F')
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0) return null;

        var room = (roomName ?? "").Trim().ToLowerInvariant();
        var isBallroomOne = room.Contains("ballroom 1") || room.Contains("ballroom-1");
        var isBallroomTwo = room.Contains("ballroom 2") || room.Contains("ballroom-2");
        var isFullBallroom = (room is "westin ballroom" or "ballroom" or "full westin ballroom" or "westin ballroom full" or "full ballroom")
            || (room.Contains("ballroom") && !isBallroomOne && !isBallroomTwo);

        // Dual projector (WBDPROJ): full ballroom B+C or E+F; Ballroom 1 E+D or D+C; Ballroom 2 A+F, A+B, or B+F.
        if (normalized.Count >= 2)
        {
            if (isFullBallroom)
            {
                var hasBC = normalized.Contains("B") && normalized.Contains("C");
                var hasEF = normalized.Contains("E") && normalized.Contains("F");
                if (hasBC || hasEF) return "WBDPROJ";
            }

            if (isBallroomOne)
            {
                var hasED = normalized.Contains("E") && normalized.Contains("D");
                var hasDC = normalized.Contains("D") && normalized.Contains("C");
                if (hasED || hasDC) return "WBDPROJ";
            }

            if (isBallroomTwo)
            {
                var hasAF = normalized.Contains("A") && normalized.Contains("F");
                var hasAB = normalized.Contains("A") && normalized.Contains("B");
                var hasBF = normalized.Contains("B") && normalized.Contains("F");
                if (hasAF || hasAB || hasBF) return "WBDPROJ";
            }
        }

        var first = normalized.First();
        if (first == "A") return "WBSSPROJ";
        if (first == "D") return "WBSNPROJ";
        return "WBSPROJ";
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

        // For Westin/Four Points with built-in vision package, do not add standalone screen (room package already includes it)
        var roomVisionPackages = await TryGetRoomSpecificPackagesAsync(
            context.VenueName, context.RoomName, "screen", 1, context, ct);
        if (roomVisionPackages.Count > 0)
        {
            _logger.LogInformation("Room has built-in vision package; skipping standalone screen recommendation");
            return items;
        }

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

        var aiCodes = await GetAiCatalogCodesAsync(ct);
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
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

        var aiCodes = await GetAiCatalogCodesAsync(ct);
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
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
            var desc = (ranked.Product.descriptionv6 ?? ranked.Product.PrintedDesc ?? "").Trim();

            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = productCode,
                Description = desc,
                Category = ranked.Product.category,
                Quantity = quantity,
                UnitPrice = ranked.Rate,
                ExtraDayRate = ranked.PriceInfo?.rate_extra_days ?? 0,
                PictureFileName = ranked.Product.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"{micTypeName} wireless microphone - ideal for {context.EventType} with presenters who " +
                    (isHandheld ? "share microphones or host Q&A sessions" : "need hands-free mobility during presentations"),
                Comment = (isHandheld || isLapel) ? $"Client requested: {micTypeName}" : null
            });
        }

        return items;
    }

    #endregion

    #region Speaker Package Recommendations

    private async Task<List<RecommendedEquipmentItem>> RecommendSpeakerPackagesAsync(
        int quantity,
        EventContext context,
        string? speakerStyle,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();
        var style = NormalizeSpeakerStyle(speakerStyle, context.SpeakerStylePreference);
        var wantsExternalPortable = style is "external" or "portable";
        var wantsInbuilt = style == "inbuilt";

        // Room-aware: try venue-installed WSB audio packages first when Westin/Four Points + room known
        if (!wantsExternalPortable)
        {
            var roomPkgs = await TryGetRoomSpecificPackagesAsync(
                context.VenueName, context.RoomName, "speaker", quantity, context, ct);
            if (roomPkgs.Count > 0)
            {
                _logger.LogInformation("Using room-specific speaker packages: {Count} items", roomPkgs.Count);
                return roomPkgs;
            }

            if (wantsInbuilt)
            {
                _logger.LogInformation("Speaker style 'inbuilt' requested but no room package found; falling back to portable speaker search.");
            }
        }

        string[] searchTerms;
        string sizeCategory;

        if (context.ExpectedAttendees <= 50)
        {
            searchTerms = wantsExternalPortable
                ? new[] { "portable", "compact", "bose", "s1", "battery" }
                : new[] { "compact", "portable", "bose", "s1" };
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

        var aiCodes = await GetAiCatalogCodesAsync(ct);
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
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
                if (wantsExternalPortable && (desc.Contains("portable") || desc.Contains("compact"))) score += 15;
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
                RecommendationReason = wantsExternalPortable
                    ? $"{sizeCategory} portable speaker system - selected based on your external speaker preference"
                    : $"{sizeCategory} speaker system - provides clear audio coverage for {context.ExpectedAttendees} attendees"
            });
        }

        return items;
    }

    private static string? NormalizeSpeakerStyle(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalized = candidate.Trim().ToLowerInvariant();
            if (normalized.Contains("inbuilt") || normalized.Contains("built in") || normalized.Contains("ceiling"))
                return "inbuilt";
            if (normalized.Contains("external") || normalized.Contains("portable") || normalized.Contains("pa"))
                return "external";
        }

        return null;
    }

    #endregion

    #region Camera Package Recommendations

    private async Task<List<RecommendedEquipmentItem>> RecommendCameraPackagesAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();

        // Determine camera type based on use case
        string[] searchTerms;
        string useCase;

        var eventTypeLower = (context.EventType ?? "").ToLower();
        bool isVideoCall = eventTypeLower.Contains("teams") ||
                          eventTypeLower.Contains("zoom") ||
                          eventTypeLower.Contains("conference") ||
                          eventTypeLower.Contains("webinar") ||
                          eventTypeLower.Contains("hybrid");

        if (isVideoCall)
        {
            // For video calls, prefer PTZ or webcam-style cameras
            searchTerms = new[] { "ptz", "webcam", "conference", "usb", "logitech", "poly" };
            useCase = "video conferencing";
        }
        else
        {
            // For recording, prefer camcorders or broadcast cameras
            searchTerms = new[] { "camcorder", "broadcast", "sony", "canon", "panasonic" };
            useCase = "recording";
        }

        // Search for cameras - support CAMERA, CAMERAS, and VIDEO category variants.
        var aiCodes = await GetAiCatalogCodesAsync(ct);
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
            .Where(p => (p.category ?? "").Trim() == "CAMERA" || (p.category ?? "").Trim() == "CAMERAS" || (p.category ?? "").Trim() == "VIDEO")
            .Where(p =>
                !(p.descriptionv6 ?? "").ToLower().Contains("cable") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("battery") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("charger") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("tripod") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("mount") &&
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
                if (isVideoCall && (desc.Contains("ptz") || desc.Contains("webcam") || desc.Contains("usb"))) score += 30;
                if (!isVideoCall && (desc.Contains("camcorder") || desc.Contains("broadcast"))) score += 30;
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
                RecommendationReason = $"Camera for {useCase} - captures high-quality video for your {context.EventType}"
            });
        }

        return items;
    }

    #endregion

    #region Clicker / Presentation Remote Recommendations

    /// <summary>
    /// Recommends wireless clickers/presentation remotes.
    /// Uses LOGISPOT first, then WIRPRES fallback, then description search.
    /// </summary>
    private async Task<List<RecommendedEquipmentItem>> RecommendClickerPackagesAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();
        var preferredCodes = new[] { "LOGISPOT", "WIRPRES" };
        foreach (var preferredCode in preferredCodes)
        {
            var codeLower = preferredCode.Trim().ToLowerInvariant();
            var aiCodes = await GetAiCatalogCodesAsync(ct);
            var byCode = await _db.TblInvmas
                .AsNoTracking()
                .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
                .Where(p => p.product_code != null && p.product_code.Trim().ToLower() == codeLower)
                .Select(p => new { p.product_code, p.descriptionv6, p.PrintedDesc, p.category, p.PictureFileName })
                .FirstOrDefaultAsync(ct);

            if (byCode == null)
                continue;

            var productCodes = new List<string> { (byCode.product_code ?? "").Trim() };
            var pricing = await _db.TblRatetbls
                .AsNoTracking()
                .Where(r => r.TableNo == 0 && productCodes.Contains((r.product_code ?? "").Trim()))
                .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days })
                .ToListAsync(ct);
            var priceInfo = pricing.FirstOrDefault();

            if (priceInfo == null || (priceInfo.rate_1st_day ?? 0) <= 0)
                continue;

            var components = await GetPackageComponentsAsync(byCode.product_code!.Trim(), ct);
            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = byCode.product_code!.Trim(),
                Description = (byCode.descriptionv6 ?? byCode.PrintedDesc ?? "").Trim(),
                Category = byCode.category,
                Quantity = quantity,
                UnitPrice = priceInfo.rate_1st_day ?? 0,
                ExtraDayRate = priceInfo.rate_extra_days ?? 0,
                PictureFileName = byCode.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"Wireless presentation remote for slide control - ideal for your {context.EventType}"
            });
            return items;
        }

        // Fallback: search by description for presenter/clicker (exclude microphone)
        var aiCodesFallback = await GetAiCatalogCodesAsync(ct);
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodesFallback.Count == 0 || aiCodesFallback.Contains((p.product_code ?? "").Trim()))
            .Where(p =>
                ((p.descriptionv6 ?? "").ToLower().Contains("presenter") || (p.descriptionv6 ?? "").ToLower().Contains("clicker")) &&
                !(p.descriptionv6 ?? "").ToLower().Contains("microphone") &&
                !(p.descriptionv6 ?? "").ToLower().Contains(" mic ") &&
                !(p.descriptionv6 ?? "").ToLower().Contains("discontinued"))
            .Select(p => new { p.product_code, p.descriptionv6, p.PrintedDesc, p.category, p.PictureFileName })
            .Take(20)
            .ToListAsync(ct);

        if (products.Count == 0)
        {
            _logger.LogWarning("No clicker/presentation remote products found (tried LOGISPOT, WIRPRES, and description search)");
            return items;
        }

        var codes = products.Select(p => (p.product_code ?? "").Trim()).ToList();
        var rates = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && codes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days })
            .ToListAsync(ct);
        var priceLookup = rates.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        var best = products
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim();
                priceLookup.TryGetValue(code, out var info);
                var rate = info?.rate_1st_day ?? 0;
                return new { Product = p, Rate = rate, PriceInfo = info };
            })
            .Where(x => x.Rate > 0)
            .OrderBy(x => x.Rate)
            .FirstOrDefault();

        if (best != null)
        {
            var productCode = (best.Product.product_code ?? "").Trim();
            var components = await GetPackageComponentsAsync(productCode, ct);
            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = productCode,
                Description = (best.Product.descriptionv6 ?? best.Product.PrintedDesc ?? "").Trim(),
                Category = best.Product.category,
                Quantity = quantity,
                UnitPrice = best.Rate,
                ExtraDayRate = best.PriceInfo?.rate_extra_days ?? 0,
                PictureFileName = best.Product.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"Wireless presentation remote for slide control - ideal for your {context.EventType}"
            });
        }

        return items;
    }

    #endregion

    #region Product-by-Code and Accessory Recommendations

    /// <summary>
    /// Recommends a product by exact product code. Used for fixed accessories (USBCMX2, V1HD, SDICROSS, NATFLIPC, LOG4kCAM).
    /// </summary>
    private async Task<List<RecommendedEquipmentItem>> RecommendProductByCodeAsync(
        string productCode,
        int quantity,
        string reason,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();
        var codeTrim = productCode.Trim();
        if (string.IsNullOrEmpty(codeTrim)) return items;

        // Batched path: one IN query per wave of distinct codes per recommendation request.
        if (_recommendationInvBatch != null && _recommendationRateBatch != null)
        {
            await EnsureRecommendationProductsLoadedAsync(new[] { codeTrim }, ct);

            ProductInvSnap? invOpt = _recommendationInvBatch.TryGetValue(codeTrim, out var invEntry) ? invEntry : null;

            if (invOpt == null && codeTrim.Equals("LCD40", StringComparison.OrdinalIgnoreCase))
            {
                var aiCatalogForLcd = await GetAiCatalogCodesAsync(ct);
                var lcdRow = await _db.TblInvmas
                    .AsNoTracking()
                    .Where(p => aiCatalogForLcd.Count == 0 || aiCatalogForLcd.Contains((p.product_code ?? "").Trim()))
                    .Where(p =>
                        (p.descriptionv6 ?? "").ToLower().Contains("foldback") ||
                        (p.descriptionv6 ?? "").ToLower().Contains("confidence monitor") ||
                        ((p.descriptionv6 ?? "").ToLower().Contains("40") && (p.descriptionv6 ?? "").ToLower().Contains("monitor")))
                    .Select(p => new ProductInvSnap(
                        (p.product_code ?? "").Trim(),
                        p.descriptionv6,
                        p.PrintedDesc,
                        p.category,
                        p.PictureFileName))
                    .FirstOrDefaultAsync(ct);

                if (!string.IsNullOrEmpty(lcdRow.ProductCode))
                {
                    _recommendationInvBatch[lcdRow.ProductCode] = lcdRow;
                    _recommendationInvBatch[codeTrim] = lcdRow;
                    await EnsureRecommendationProductsLoadedAsync(new[] { lcdRow.ProductCode }, ct);
                    invOpt = lcdRow;
                }
            }

            if (invOpt is { } bySnap)
            {
                var resolvedCode = bySnap.ProductCode;
                _recommendationRateBatch.TryGetValue(resolvedCode, out var rateOpt);
                var rate = rateOpt?.Rate1stDay ?? 0;
                if (rate <= 0)
                {
                    _logger.LogWarning("Product {Code} has no pricing", productCode);
                    return items;
                }

                var components = await GetPackageComponentsAsync(resolvedCode, ct);
                items.Add(new RecommendedEquipmentItem
                {
                    ProductCode = resolvedCode,
                    Description = (bySnap.Descriptionv6 ?? bySnap.PrintedDesc ?? "").Trim(),
                    Category = bySnap.Category,
                    Quantity = quantity,
                    UnitPrice = rate,
                    ExtraDayRate = rateOpt?.RateExtraDays ?? 0,
                    PictureFileName = bySnap.PictureFileName,
                    IsPackage = components.Count > 0,
                    Components = components,
                    RecommendationReason = reason
                });
                return items;
            }

            _logger.LogWarning("Product code {Code} not found in inventory", productCode);
            return items;
        }

        // Legacy path (no request batch — callers outside GetRecommendationsAsync).
        var codeLower = codeTrim.ToLowerInvariant();
        var aiCodes = await GetAiCatalogCodesAsync(ct);
        var byCode = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
            .Where(p => p.product_code != null && p.product_code.Trim().ToLower() == codeLower)
            .Select(p => new { p.product_code, p.descriptionv6, p.PrintedDesc, p.category, p.PictureFileName })
            .FirstOrDefaultAsync(ct);

        if (byCode == null && codeTrim.Equals("LCD40", StringComparison.OrdinalIgnoreCase))
        {
            byCode = await _db.TblInvmas
                .AsNoTracking()
                .Where(p => aiCodes.Count == 0 || aiCodes.Contains((p.product_code ?? "").Trim()))
                .Where(p =>
                    (p.descriptionv6 ?? "").ToLower().Contains("foldback") ||
                    (p.descriptionv6 ?? "").ToLower().Contains("confidence monitor") ||
                    ((p.descriptionv6 ?? "").ToLower().Contains("40") && (p.descriptionv6 ?? "").ToLower().Contains("monitor")))
                .Select(p => new { p.product_code, p.descriptionv6, p.PrintedDesc, p.category, p.PictureFileName })
                .FirstOrDefaultAsync(ct);
        }

        if (byCode == null)
        {
            _logger.LogWarning("Product code {Code} not found in inventory", productCode);
            return items;
        }

        var codes = new List<string> { (byCode.product_code ?? "").Trim() };
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && codes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days })
            .FirstOrDefaultAsync(ct);

        var rateLegacy = pricing?.rate_1st_day ?? 0;
        if (rateLegacy <= 0)
        {
            _logger.LogWarning("Product {Code} has no pricing", productCode);
            return items;
        }

        var componentsLegacy = await GetPackageComponentsAsync(byCode.product_code!.Trim(), ct);
        items.Add(new RecommendedEquipmentItem
        {
            ProductCode = byCode.product_code.Trim(),
            Description = (byCode.descriptionv6 ?? byCode.PrintedDesc ?? "").Trim(),
            Category = byCode.category,
            Quantity = quantity,
            UnitPrice = rateLegacy,
            ExtraDayRate = pricing?.rate_extra_days ?? 0,
            PictureFileName = byCode.PictureFileName,
            IsPackage = componentsLegacy.Count > 0,
            Components = componentsLegacy,
            RecommendationReason = reason
        });
        return items;
    }

    /// <summary>
    /// Recommends lectern (LECT1) with optional microphone (SHURE418).
    /// </summary>
    private async Task<List<RecommendedEquipmentItem>> RecommendLecternAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();
        var lecternItems = await RecommendProductByCodeAsync("LECT1", quantity, "Professional lectern with light and mic points", context, ct);
        items.AddRange(lecternItems);
        // Lectern typically includes gooseneck mic (SHURE418) - add if not in package
        var hasMic = items.Any(i => (i.Components ?? new List<PackageComponent>()).Any(c =>
            (c.Description ?? "").Contains("microphone", StringComparison.OrdinalIgnoreCase) ||
            (c.ProductCode ?? "").Equals("SHURE418", StringComparison.OrdinalIgnoreCase)));
        if (!hasMic)
        {
            var micItems = await RecommendProductByCodeAsync("SHURE418", quantity, "Shure gooseneck microphone for lectern", context, ct);
            items.AddRange(micItems);
        }
        return items;
    }

    /// <summary>
    /// Recommends foldback monitor: LCD40 + NATFLSTD + 2x SDICROSS (per spec).
    /// </summary>
    private async Task<List<RecommendedEquipmentItem>> RecommendFoldbackMonitorAsync(
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        var items = new List<RecommendedEquipmentItem>();
        var lcd = await RecommendProductByCodeAsync("LCD40", quantity, "40\" foldback monitor so presenter can see screen without turning", context, ct);
        var stand = await RecommendProductByCodeAsync("NATFLSTD", quantity, "Floor stand for foldback monitor", context, ct);
        var sdi = await RecommendProductByCodeAsync("SDICROSS", quantity * 2, "HDMI extension for foldback signal", context, ct);
        items.AddRange(lcd);
        items.AddRange(stand);
        items.AddRange(sdi);
        return items;
    }

    #endregion
}
