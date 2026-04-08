using MicrohireAgentChat.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

public sealed partial class SmartEquipmentRecommendationService
{
    // ── Dynamic SubCategory discovery ───────────────────────────────────

    private enum RoomProductType { Audio, Vision, Av, Independent, Unknown }
    private enum RoomSizeVariant { Full, Half, Any }

    private static (RoomProductType Type, RoomSizeVariant Size) ClassifyRoomProduct(string description)
    {
        var d = (description ?? "").ToLowerInvariant();

        if (d.Contains("independent"))
            return (RoomProductType.Independent, RoomSizeVariant.Any);

        bool isSingle = d.Contains("single") || d.Contains("(single)");

        if (d.Contains("av package") || d.Contains("audio visual"))
            return (RoomProductType.Av, isSingle ? RoomSizeVariant.Half : RoomSizeVariant.Full);

        if (d.Contains("speaker") || d.Contains("ceiling speaker") || d.Contains("audio system"))
            return (RoomProductType.Audio, isSingle ? RoomSizeVariant.Half : RoomSizeVariant.Full);

        if (d.Contains("projection") || d.Contains("projector"))
            return (RoomProductType.Vision, RoomSizeVariant.Any);

        return (RoomProductType.Unknown, RoomSizeVariant.Any);
    }

    /// <summary>
    /// Discovers all products in the given SubCategory and classifies them by type and room size.
    /// </summary>
    private async Task<List<(string ProductCode, string Description, RoomProductType Type, RoomSizeVariant Size)>>
        DiscoverProductsBySubCategoryAsync(string subCategory, CancellationToken ct)
    {
        var subCatLower = subCategory.Trim().ToLowerInvariant();

        var rows = await _db.TblInvmas
            .AsNoTracking()
            .Where(p => p.SubCategory != null &&
                        (p.SubCategory ?? "").Trim().ToLower() == subCatLower)
            .Select(p => new { Code = (p.product_code ?? "").Trim(), Desc = (p.descriptionv6 ?? p.PrintedDesc ?? "").Trim() })
            .ToListAsync(ct);

        var results = new List<(string, string, RoomProductType, RoomSizeVariant)>();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Code)) continue;
            var (type, size) = ClassifyRoomProduct(r.Desc);
            results.Add((r.Code, r.Desc, type, size));
        }

        _logger.LogInformation("Discovered {Count} products in SubCategory {SubCat}: {Codes}",
            results.Count, subCategory, string.Join(", ", results.Select(r => $"{r.Item1}({r.Item3}/{r.Item4})")));

        return results;
    }

    // ── Venue-room-packages.json cache ──────────────────────────────────

    private static readonly object _venueRoomPackagesLock = new();
    private static string? _venueRoomPackagesPathCached;
    private static DateTime _venueRoomPackagesWriteUtc;
    private static Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>? _venueRoomPackagesMapping;

    /// <summary>
    /// Parsed venue-room-packages.json, cached until the file changes (mtime).
    /// </summary>
    private async Task<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>?> GetVenueRoomPackagesMappingAsync(CancellationToken ct)
    {
        var jsonPath = Path.Combine(_env.WebRootPath ?? "", "data", "venue-room-packages.json");
        if (!File.Exists(jsonPath))
            return null;

        DateTime writeUtc;
        try
        {
            writeUtc = File.GetLastWriteTimeUtc(jsonPath);
        }
        catch
        {
            writeUtc = DateTime.MinValue;
        }

        lock (_venueRoomPackagesLock)
        {
            if (_venueRoomPackagesMapping != null &&
                _venueRoomPackagesPathCached == jsonPath &&
                _venueRoomPackagesWriteUtc == writeUtc)
            {
                return _venueRoomPackagesMapping;
            }
        }

        await using var stream = File.OpenRead(jsonPath);
        var mapping = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>(stream, cancellationToken: ct);

        lock (_venueRoomPackagesLock)
        {
            _venueRoomPackagesMapping = mapping;
            _venueRoomPackagesPathCached = jsonPath;
            _venueRoomPackagesWriteUtc = writeUtc;
        }

        return mapping;
    }

    private async Task ApplyRoomSpecificSuggestionsAsync(
        SmartEquipmentRecommendation result,
        EventContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.VenueName) || !context.VenueName.Contains("Westin", StringComparison.OrdinalIgnoreCase))
            return;

        var roomNorm = (context.RoomName ?? "").ToLowerInvariant();
        var eventType = (context.EventType ?? "").ToLower();

        // Suggestions for Thrive and Elevate
        if (roomNorm.Contains("thrive") || roomNorm.Contains("elevate"))
        {
            var presentationMentioned = eventType.Contains("presentation") || context.NumberOfPresentations > 0;
            if (presentationMentioned)
            {
                var hasClicker = result.Items.Any(i =>
                    i.Description.ToLower().Contains("clicker") ||
                    i.Description.ToLower().Contains("presenter") ||
                    string.Equals(i.ProductCode, "WIRPRES", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.ProductCode, "LOGISPOT", StringComparison.OrdinalIgnoreCase));
                if (!hasClicker && result.Items.Any(i => i.Category == "PROJECTR" || i.Category == "SCREEN"))
                {
                    var visionItem = result.Items.FirstOrDefault(i => i.Category == "PROJECTR" || i.Category == "SCREEN");
                    if (visionItem != null)
                    {
                        visionItem.RecommendationReason += ". Suggestion: A wireless clicker is recommended for your presentations.";
                    }
                }

                var hasRecording = result.Items.Any(i => i.Description.ToLower().Contains("recording"));
                if (!hasRecording)
                {
                    var mainItem = result.Items.FirstOrDefault();
                    if (mainItem != null)
                    {
                        mainItem.RecommendationReason += ". Note: Audio/video recording is available to capture your session.";
                    }
                }
            }
        }

        // Suggestions for Elevate specifically
        if (roomNorm.Contains("elevate"))
        {
            var mainItem = result.Items.FirstOrDefault();
            if (mainItem != null)
            {
                if (!result.Items.Any(i => i.Description.ToLower().Contains("lectern")))
                    mainItem.RecommendationReason += ". Tip: A lectern with microphone is highly recommended for Elevate.";

                if (!result.Items.Any(i => i.Description.ToLower().Contains("uplight")))
                    mainItem.RecommendationReason += ". Tip: Ambient uplighting is often used in this room to create a desired atmosphere.";

                if (!result.Items.Any(i => i.Description.ToLower().Contains("signage") || i.Description.ToLower().Contains("bip")))
                    mainItem.RecommendationReason += ". Tip: The BIP (Digital Signage 'Big iPhone') is a great addition for this space.";

                if (!result.LaborItems.Any(l => l.Description.Contains("Operator")))
                    mainItem.RecommendationReason += ". Note: Technical operator assistance is recommended for a seamless experience in Elevate.";
            }
        }

        // Background music for Ballroom, Elevate, PFA
        if (roomNorm.Contains("ballroom") || roomNorm.Contains("elevate") || roomNorm.Contains("pre-function") || roomNorm.Contains("pfa"))
        {
            var audioItem = result.Items.FirstOrDefault(i => i.Description.ToLower().Contains("audio") || i.Description.ToLower().Contains("speaker"));
            if (audioItem != null)
            {
                audioItem.RecommendationReason += ". Suggestion: Background music can help keep the evening flowing.";
            }
        }

        // Photography for meaningful events
        bool isMeaningful = eventType.Contains("wedding") || eventType.Contains("party") || eventType.Contains("gala") || eventType.Contains("award") || eventType.Contains("birthday") || eventType.Contains("celebration");
        if (isMeaningful && (roomNorm.Contains("ballroom") || roomNorm.Contains("elevate") || roomNorm.Contains("pre-function") || roomNorm.Contains("pfa")))
        {
            var mainItem = result.Items.FirstOrDefault();
            if (mainItem != null)
            {
                mainItem.RecommendationReason += ". Pro tip: Professional photography is recommended for this type of event.";
            }
        }

        await Task.CompletedTask;
    }

    private async Task<OperationProfile> BuildOperationProfileAsync(
        IReadOnlyList<RecommendedEquipmentItem> items,
        EventContext context,
        CancellationToken ct)
    {
        if (items.Count == 0) return OperationProfile.Empty;

        var modeByCategory = await LoadOperationModesByCategoryAsync(context, ct);
        if (modeByCategory.Count == 0) return OperationProfile.Empty;

        bool hasRequired = false;
        bool hasRecommended = false;
        bool hasSelfOperated = false;
        bool hasUnknown = false;

        foreach (var item in items)
        {
            var categoryId = MapItemToKnowledgeCategory(item);
            if (string.IsNullOrWhiteSpace(categoryId) || !modeByCategory.TryGetValue(categoryId, out var mode))
            {
                hasUnknown = true;
                continue;
            }

            switch (mode)
            {
                case "operator_required":
                    hasRequired = true;
                    break;
                case "operator_recommended":
                    hasRecommended = true;
                    break;
                case "self_operated":
                    hasSelfOperated = true;
                    break;
                default:
                    hasUnknown = true;
                    break;
            }
        }

        return new OperationProfile(hasRequired, hasRecommended, hasSelfOperated, hasUnknown);
    }

    private async Task<Dictionary<string, string>> LoadOperationModesByCategoryAsync(
        EventContext context,
        CancellationToken ct)
    {
        var modes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dataPath = Path.Combine(_env.WebRootPath ?? string.Empty, "data");
        var isWestinEvent = (context.VenueName ?? string.Empty).Contains("Westin", StringComparison.OrdinalIgnoreCase);

        var fileNames = new List<string> { "product-knowledge-master.json" };
        if (isWestinEvent)
        {
            fileNames.Add("product-knowledge-westin.json");
        }

        foreach (var fileName in fileNames)
        {
            var path = Path.Combine(dataPath, fileName);
            if (!File.Exists(path)) continue;

            string json;
            try
            {
                json = await File.ReadAllTextAsync(path, ct);
            }
            catch
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var category in categories.EnumerateArray())
                {
                    if (!category.TryGetProperty("id", out var idProp))
                        continue;

                    var categoryId = idProp.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(categoryId))
                        continue;

                    if (!category.TryGetProperty("operationMode", out var modeProp))
                        continue;

                    var mode = modeProp.GetString()?.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(mode))
                        continue;

                    modes[categoryId] = mode;
                }
            }
            catch
            {
                // Ignore malformed optional metadata files and fall back to existing heuristics.
            }
        }

        return modes;
    }

    private static string? MapItemToKnowledgeCategory(RecommendedEquipmentItem item)
    {
        var category = (item.Category ?? string.Empty).Trim().ToUpperInvariant();
        var description = (item.Description ?? string.Empty).ToLowerInvariant();

        if (category is "W/MIC" or "SPEAKER" || description.Contains("microphone") || description.Contains("speaker") || description.Contains("mixing desk"))
            return "audio";

        if (description.Contains("led wall"))
            return "visual";

        if (category is "PROJECTR" or "SCREEN" || description.Contains("projector") || description.Contains("projection screen") || description.Contains("confidence monitor"))
            return "visual";

        if (category is "LED" || description.Contains("uplight") || description.Contains("moving light") || description.Contains("spotlight") || description.Contains("lighting"))
            return "lighting";

        if (description.Contains("stage") || description.Contains("lectern") || description.Contains("truss") || description.Contains("riser"))
            return "staging";

        if (description.Contains("kiosk") || description.Contains("digital signage") || description.Contains("rfid") || description.Contains("interpretation") || description.Contains("streaming"))
            return "event-technology";

        if (description.Contains("laptop") || description.Contains("macbook") || description.Contains("playback") || description.Contains("media server") || description.Contains("presenter") || description.Contains("clicker"))
            return "computers-playback";

        if (description.Contains("cable") || description.Contains("power distribution") || description.Contains("pdu") || description.Contains("ups") || description.Contains("rigging"))
            return "cables-power-rigging";

        if (description.Contains("haze") || description.Contains("confetti") || description.Contains("co2") || description.Contains("pyro") || description.Contains("projection mapping"))
            return "special-effects";

        return null;
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

    /// <summary>
    /// Try to get room-specific WSB packages for Westin Brisbane / Four Points Brisbane.
    /// Returns empty list if venue/room not matched or no packages found.
    /// </summary>
    private async Task<List<RecommendedEquipmentItem>> TryGetRoomSpecificPackagesAsync(
        string? venueName,
        string? roomName,
        string equipmentType,
        int quantity,
        EventContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(venueName) || string.IsNullOrWhiteSpace(roomName))
            return new List<RecommendedEquipmentItem>();

        var venueNorm = venueName.Trim().ToLowerInvariant();
        var roomNorm = roomName.Trim().ToLowerInvariant();

        bool isWestin = venueNorm.Contains("westin") && venueNorm.Contains("brisbane");
        bool isFourPoints = venueNorm.Contains("four points") && venueNorm.Contains("brisbane");
        if (!isWestin && !isFourPoints)
            return new List<RecommendedEquipmentItem>();

        string? mappingKey = isWestin ? "Westin Brisbane" : "Four Points Brisbane";
        var roomKey = ResolveRoomKey(roomNorm, mappingKey);
        if (roomKey == null)
            return new List<RecommendedEquipmentItem>();

        var mapping = await GetVenueRoomPackagesMappingAsync(ct);
        if (mapping == null)
        {
            var jsonPath = Path.Combine(_env.WebRootPath ?? "", "data", "venue-room-packages.json");
            _logger.LogWarning("venue-room-packages.json not found or unreadable at {Path}", jsonPath);
            return new List<RecommendedEquipmentItem>();
        }

        if (!mapping.TryGetValue(mappingKey, out var venueRooms) || !venueRooms.TryGetValue(roomKey, out var roomPkgs))
        {
            _logger.LogDebug("No room mapping for {Venue} / {Room}", mappingKey, roomKey);
            return new List<RecommendedEquipmentItem>();
        }

        // Read aiFolder early — drives dynamic SubCategory discovery
        var aiFolder = "WSB";
        if (roomPkgs.TryGetValue("aiFolder", out var aiFolderEl) && aiFolderEl.ValueKind == JsonValueKind.String)
        {
            var folderStr = aiFolderEl.GetString();
            if (!string.IsNullOrWhiteSpace(folderStr))
                aiFolder = folderStr.Trim();
        }
        bool hasSpecificAiFolder = !string.Equals(aiFolder, "WSB", StringComparison.OrdinalIgnoreCase);

        // Try reading explicit package codes from JSON arrays (audio/vision/av)
        List<string>? packageCodes = null;
        string[]? keysToTry = equipmentType.ToLowerInvariant() switch
        {
            "av" => new[] { "av", "vision", "audio" },
            "vision" => new[] { "vision", "av" },
            "projector" or "projectors" or "screen" or "screens" => new[] { "vision", "av" },
            "speaker" or "speakers" or "audio" => new[] { "audio", "av" },
            _ => null
        };

        if (keysToTry != null)
        {
            foreach (var key in keysToTry)
            {
                if (roomPkgs.TryGetValue(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    packageCodes = new List<string>();
                    foreach (var e in arr.EnumerateArray())
                    {
                        var s = e.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            packageCodes.Add(s.Trim());
                    }
                    if (packageCodes.Count > 0)
                        break;
                }
            }
        }

        // ── Dynamic SubCategory discovery ────────────────────────────────
        // When JSON arrays are absent but aiFolder points to a specific SubCategory,
        // discover all products in that SubCategory and classify dynamically.
        if ((packageCodes == null || packageCodes.Count == 0) && hasSpecificAiFolder)
        {
            var discovered = await DiscoverProductsBySubCategoryAsync(aiFolder, ct);
            if (discovered.Count == 0)
                return new List<RecommendedEquipmentItem>();

            var targetType = equipmentType.ToLowerInvariant() switch
            {
                "av" => RoomProductType.Av,
                "vision" or "projector" or "projectors" or "screen" or "screens" => RoomProductType.Vision,
                "speaker" or "speakers" or "audio" => RoomProductType.Audio,
                _ => (RoomProductType?)null
            };

            if (!targetType.HasValue)
                return new List<RecommendedEquipmentItem>();

            // Safety net: if the user explicitly declined projection via the combined checkbox,
            // do not return vision packages even if a vision request somehow slipped through.
            if (context.UserDeclinedProjection && targetType == RoomProductType.Vision)
                return new List<RecommendedEquipmentItem>();

            var isHalfRoom = roomNorm.Contains("elevate 1") || roomNorm.Contains("elevate-1") ||
                             roomNorm.Contains("elevate 2") || roomNorm.Contains("elevate-2") ||
                             roomNorm.Contains("thrive 1") || roomNorm.Contains("thrive-1") ||
                             roomNorm.Contains("ballroom 1") || roomNorm.Contains("ballroom 2");
            var targetSize = isHalfRoom ? RoomSizeVariant.Half : RoomSizeVariant.Full;

            var candidates = discovered.Where(d => d.Type == targetType.Value).ToList();
            var sizeMatched = candidates.Where(d => d.Size == targetSize).ToList();
            if (sizeMatched.Count == 0)
                sizeMatched = candidates.Where(d => d.Size == RoomSizeVariant.Any).ToList();
            if (sizeMatched.Count == 0 && targetSize == RoomSizeVariant.Full)
                sizeMatched = candidates; // full room can use any variant

            packageCodes = sizeMatched.Select(d => d.ProductCode).ToList();
            if (packageCodes.Count == 0)
                return new List<RecommendedEquipmentItem>();
        }
        else if (packageCodes == null || packageCodes.Count == 0)
        {
            return new List<RecommendedEquipmentItem>();
        }

        // ── Ballroom-specific routing (explicit JSON codes) ──────────────

        // Westin Ballroom audio package routing:
        // - Full Ballroom -> WBFBCSS
        // - Ballroom 1/2 -> WBSBCSS
        if ((equipmentType.Contains("speaker", StringComparison.OrdinalIgnoreCase) || equipmentType.Contains("audio", StringComparison.OrdinalIgnoreCase)) &&
            roomNorm.Contains("ballroom"))
        {
            var isBallroomOneOrTwo = roomNorm.Contains("ballroom 1") || roomNorm.Contains("ballroom 2");
            packageCodes = isBallroomOneOrTwo
                ? packageCodes.Where(c => string.Equals(c, "WBSBCSS", StringComparison.OrdinalIgnoreCase)).ToList()
                : packageCodes.Where(c => string.Equals(c, "WBFBCSS", StringComparison.OrdinalIgnoreCase)).ToList();

            if (packageCodes.Count == 0)
                return new List<RecommendedEquipmentItem>();
        }

        // Westin Ballroom AV package routing:
        // - Full Ballroom -> WBAVP
        // - Ballroom 1/2 -> WBSAVP
        if (equipmentType.Equals("av", StringComparison.OrdinalIgnoreCase) && roomNorm.Contains("ballroom"))
        {
            var isBallroomOneOrTwo = roomNorm.Contains("ballroom 1") || roomNorm.Contains("ballroom 2");
            packageCodes = isBallroomOneOrTwo
                ? packageCodes.Where(c => string.Equals(c, "WBSAVP", StringComparison.OrdinalIgnoreCase)).ToList()
                : packageCodes.Where(c => string.Equals(c, "WBAVP", StringComparison.OrdinalIgnoreCase)).ToList();

            if (packageCodes.Count == 0)
                return new List<RecommendedEquipmentItem>();
        }

        // Westin Ballroom vision package routing based on projector placement area:
        // - Area A -> WBSSPROJ (south, 10k lumen)
        // - Area D -> WBSNPROJ (north, 12k lumen)
        // - Area B, C, E, or F -> WBSPROJ (standard single, 6k lumen)
        // - Dual (2+ areas) -> WBDPROJ (dual, 2x 6k lumen)
        // - "none" -> no vision package
        if ((equipmentType.Contains("projector", StringComparison.OrdinalIgnoreCase) ||
             equipmentType.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
             equipmentType.Contains("screen", StringComparison.OrdinalIgnoreCase)) &&
            roomNorm.Contains("ballroom"))
        {
            var areas = context.ProjectorAreas?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? new List<string>();

            // "none" means user declined projection
            if (areas.Any(a => string.Equals(a.Trim(), "none", StringComparison.OrdinalIgnoreCase)))
                return new List<RecommendedEquipmentItem>();

            if (areas.Count >= 2)
            {
                packageCodes = packageCodes.Where(c => string.Equals(c, "WBDPROJ", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else if (areas.Count == 1)
            {
                var area = areas[0].Trim().ToUpperInvariant();
                var targetCode = area switch
                {
                    "A" => "WBSSPROJ",
                    "D" => "WBSNPROJ",
                    "B" or "C" or "E" or "F" => "WBSPROJ",
                    _ => null
                };
                if (targetCode != null)
                    packageCodes = packageCodes.Where(c => string.Equals(c, targetCode, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            // If no areas specified, fall through to the generic heuristic below (line ~740)

            if (packageCodes.Count == 0)
                return new List<RecommendedEquipmentItem>();
        }

        // Elevate audio package routing:
        // - Full Elevate -> ELEVCSS (8 speakers)
        // - Elevate 1/2 -> ELEVSCSS (4 speakers)
        if ((equipmentType.Contains("speaker", StringComparison.OrdinalIgnoreCase) || equipmentType.Contains("audio", StringComparison.OrdinalIgnoreCase)) &&
            roomNorm.Contains("elevate"))
        {
            var isHalfElevate = roomNorm.Contains("elevate 1") || roomNorm.Contains("elevate-1") ||
                                roomNorm.Contains("elevate 2") || roomNorm.Contains("elevate-2");
            packageCodes = isHalfElevate
                ? packageCodes.Where(c => string.Equals(c, "ELEVSCSS", StringComparison.OrdinalIgnoreCase)).ToList()
                : packageCodes.Where(c => string.Equals(c, "ELEVCSS", StringComparison.OrdinalIgnoreCase)).ToList();

            if (packageCodes.Count == 0)
                return new List<RecommendedEquipmentItem>();
        }

        // Elevate AV package routing:
        // - Full Elevate -> ELEVAVP (projector + screen + 8 speakers)
        // - Elevate 1/2 -> ELEVSAVP (projector + screen + 4 speakers)
        if (equipmentType.Equals("av", StringComparison.OrdinalIgnoreCase) && roomNorm.Contains("elevate"))
        {
            var isHalfElevate = roomNorm.Contains("elevate 1") || roomNorm.Contains("elevate-1") ||
                                roomNorm.Contains("elevate 2") || roomNorm.Contains("elevate-2");
            packageCodes = isHalfElevate
                ? packageCodes.Where(c => string.Equals(c, "ELEVSAVP", StringComparison.OrdinalIgnoreCase)).ToList()
                : packageCodes.Where(c => string.Equals(c, "ELEVAVP", StringComparison.OrdinalIgnoreCase)).ToList();

            if (packageCodes.Count == 0)
                return new List<RecommendedEquipmentItem>();
        }

        // ── Fetch products from DB ───────────────────────────────────────

        var folderNormLower = aiFolder.Trim().ToLowerInvariant();
        var products = await _db.TblInvmas
            .AsNoTracking()
            .Where(p =>
                packageCodes.Contains((p.product_code ?? "").Trim()) &&
                ((p.SubCategory ?? "").Trim().ToLower() == folderNormLower ||
                 (p.category ?? "").Trim().ToLower() == folderNormLower ||
                 (p.category ?? "").Trim().ToLower() == "wsb"))
            .Select(p => new { p.product_code, p.descriptionv6, p.PrintedDesc, p.category, p.PictureFileName })
            .ToListAsync(ct);

        var codes = products.Select(p => (p.product_code ?? "").Trim()).ToList();
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && codes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day, r.rate_extra_days, r.rate_week })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);
        var items = new List<RecommendedEquipmentItem>();

        foreach (var p in products)
        {
            var code = (p.product_code ?? "").Trim();
            if (!priceLookup.TryGetValue(code, out var priceInfo) || (priceInfo.rate_1st_day ?? 0) <= 0)
                continue;

            var components = await GetPackageComponentsAsync(code, ct);
            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = code,
                Description = (p.descriptionv6 ?? p.PrintedDesc ?? "").Trim(),
                Category = p.category,
                Quantity = quantity,
                UnitPrice = priceInfo.rate_1st_day ?? 0,
                ExtraDayRate = priceInfo.rate_extra_days ?? 0,
                WeeklyRate = priceInfo.rate_week ?? 0,
                PictureFileName = p.PictureFileName,
                IsPackage = components.Count > 0,
                Components = components,
                RecommendationReason = $"Built-in AV package for {roomKey} at {mappingKey} - ideal for your event"
            });
        }

        // For projector/speaker/av, return single best match to avoid recommending redundant packages
        if (items.Count > 1 && (equipmentType.Contains("projector") || equipmentType.Contains("speaker") || equipmentType.Contains("audio") || equipmentType.Contains("av") || equipmentType.Contains("vision")))
        {
            var qty = quantity;
            var attendees = context.ExpectedAttendees;
            var best = (equipmentType.Contains("projector") || equipmentType.Contains("vision") || equipmentType.Contains("av"))
                ? (qty >= 2 || (context.ProjectorAreas?.Count >= 2) ? items.FirstOrDefault(i => i.Description.ToLowerInvariant().Contains("dual")) ?? items.First()
                  : items.OrderBy(i => i.UnitPrice).First())
                : (attendees > 100 ? items.FirstOrDefault(i => i.Description.ToLowerInvariant().Contains("full")) ?? items.OrderByDescending(i => i.UnitPrice).First()
                  : items.OrderBy(i => i.UnitPrice).First());
            return best != null ? new List<RecommendedEquipmentItem> { best } : items.Take(1).ToList();
        }
        return items.OrderBy(i => i.UnitPrice).ToList();
    }

    private static string? ResolveRoomKey(string roomNorm, string venueKey) =>
        VenueRoomPackagesCache.ResolveMappingRoomKey(roomNorm, venueKey);

    /// <summary>
    /// Projector placement dropdown options from <c>venue-room-packages.json</c> (cached).
    /// Used by chat UI and kept in sync with ballroom package routing via <see cref="ResolveRoomKey"/>.
    /// </summary>
    public object[]? GetProjectorPlacementOptionsForUi(string? venueName, string? roomName) =>
        VenueRoomPackagesCache.TryGetProjectorPlacementOptions(_env, venueName, roomName);

    /// <summary>
    /// True when venue is Westin Brisbane and room has max capacity <= 15 (e.g. Thrive Boardroom).
    /// For these rooms we skip room-specific vision/AV packages and use attendee-based small equipment.
    /// </summary>
    private async Task<bool> IsSmallRoomAsync(string? venueName, string? roomName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(venueName) || string.IsNullOrWhiteSpace(roomName)) return false;
        var venueNorm = venueName.Trim().ToLowerInvariant();
        if (!venueNorm.Contains("westin") || !venueNorm.Contains("brisbane")) return false;
        var roomNorm = roomName.Trim().ToLowerInvariant();
        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var roomNormSlug = roomNorm.Replace(" ", "-");
        var room = rooms.FirstOrDefault(r =>
            r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase) ||
            r.Slug.Equals(roomName, StringComparison.OrdinalIgnoreCase) ||
            r.Slug.Equals(roomNormSlug, StringComparison.OrdinalIgnoreCase) ||
            (roomNorm.Contains("thrive") && !roomNorm.Contains("elevate") && (r.Name.Contains("Thrive", StringComparison.OrdinalIgnoreCase) || r.Slug.Contains("thrive"))) ||
            (roomNorm == "elevate 1" && r.Slug.Equals("elevate-1", StringComparison.OrdinalIgnoreCase)) ||
            (roomNorm == "elevate 2" && r.Slug.Equals("elevate-2", StringComparison.OrdinalIgnoreCase)) ||
            (roomNorm.Contains("elevate") && (r.Name.Contains("Elevate", StringComparison.OrdinalIgnoreCase) || r.Slug.Contains("elevate"))) ||
            (roomNorm.Contains("ballroom") && r.Name.Contains("Ballroom", StringComparison.OrdinalIgnoreCase)));
        if (room is null) return false;
        var maxCap = room.Layouts.Count > 0 ? room.Layouts.Max(l => l.Capacity) : 0;
        return maxCap > 0 && maxCap <= 15;
    }


}
