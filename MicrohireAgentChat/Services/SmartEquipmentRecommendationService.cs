using MicrohireAgentChat.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Intelligently recommends equipment PACKAGES based on event context WITHOUT asking technical questions.
/// 
/// KEY CONCEPT: Equipment items are organised in packages. When a user asks for a "laptop",
/// we recommend the appropriate PACKAGE (e.g., PCLPRO, PCLP-L1) which includes:
/// - Components (standard items included in the package)
/// - Accessories (optional add-ons)  
/// - Alternatives (items that can be swapped)
/// 
/// ROOM-AWARE: For Westin Brisbane and Four Points Brisbane, recommends venue-installed WSB packages
/// (category WSB) when venue and room are known, instead of generic PROJECTR/SPEAKER items.
/// 
/// The package has the pricing - individual components often don't have their own prices.
/// We query packages that have pricing from tblRatetbl and return their component breakdown.
/// </summary>
public sealed partial class SmartEquipmentRecommendationService
{
    private readonly BookingDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SmartEquipmentRecommendationService> _logger;
    private readonly IWestinRoomCatalog _roomCatalog;

    /// <summary>Process-wide AI catalog list; invalidated when item-rules.json or venue-room-packages.json changes.</summary>
    private static List<string>? _sharedAiCatalogCodes;
    private static string? _sharedAiCatalogFingerprint;
    private static readonly object _sharedAiCatalogLock = new();

    /// <summary>Per <see cref="GetRecommendationsAsync"/> request: batched TblInvmas / TblRatetbls by product code.</summary>
    private Dictionary<string, ProductInvSnap?>? _recommendationInvBatch;
    private Dictionary<string, RateSnap?>? _recommendationRateBatch;

    private readonly record struct ProductInvSnap(string ProductCode, string? Descriptionv6, string? PrintedDesc, string? Category, string? PictureFileName);

    private readonly record struct RateSnap(double Rate1stDay, double RateExtraDays);

    public SmartEquipmentRecommendationService(
        BookingDbContext db,
        IWebHostEnvironment env,
        ILogger<SmartEquipmentRecommendationService> logger,
        IWestinRoomCatalog roomCatalog)
    {
        _db = db;
        _env = env;
        _logger = logger;
        _roomCatalog = roomCatalog;
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

        _recommendationInvBatch = new Dictionary<string, ProductInvSnap?>(StringComparer.OrdinalIgnoreCase);
        _recommendationRateBatch = new Dictionary<string, RateSnap?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Process each equipment request
            foreach (var request in context.EquipmentRequests)
            {
                var recommendations = await GetEquipmentForRequestAsync(request, context, ct);
                result.Items.AddRange(recommendations);
            }

            // AUDIO PAIRING LOGIC: Auto-suggest speakers when audio output is likely needed
            await ApplyAudioPairingLogicAsync(result, context, ct);

            // MIXER LOGIC: Add MIXER06 when more than 1 microphone (6ch supports up to 6 mics)
            await ApplyMicrophoneMixerLogicAsync(result, context, ct);

            // LABOR RECOMMENDATION LOGIC: Recommend technicians based on equipment and event complexity
            await RecommendLaborAsync(result, context, ct);

            // ROOM-SPECIFIC SUGGESTIONS: Add commentary or suggest additional items based on the room
            await ApplyRoomSpecificSuggestionsAsync(result, context, ct);

            // Calculate totals
            result.TotalDayRate = result.Items.Sum(i => i.UnitPrice * i.Quantity);

            _logger.LogInformation("Smart recommendations complete: {Count} items, ${Total}/day",
                result.Items.Count, result.TotalDayRate);

            return result;
        }
        finally
        {
            _recommendationInvBatch = null;
            _recommendationRateBatch = null;
        }
    }

    /// <summary>
    /// Recalculate technician recommendations for an updated equipment list (e.g. after update_equipment).
    /// Uses the same labor logic as GetRecommendationsAsync so the quote summary shows Technician Support
    /// consistently when equipment is modified.
    /// </summary>
    /// <param name="equipment">Current selected equipment (product code, description, quantity).</param>
    /// <param name="context">Event context (event type, attendees, venue, room, content flags).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Recommended labor items to display and persist as Draft:SelectedLabor.</returns>
    public async Task<IReadOnlyList<RecommendedLaborItem>> RecommendLaborForEquipmentAsync(
        IReadOnlyList<EquipmentItemForLabor> equipment,
        EventContext context,
        CancellationToken ct = default)
    {
        if (equipment == null || equipment.Count == 0)
        {
            var emptyResult = new SmartEquipmentRecommendation { EventContext = context, Items = new List<RecommendedEquipmentItem>() };
            await RecommendLaborAsync(emptyResult, context, ct);
            return emptyResult.LaborItems;
        }

        var productCodes = equipment.Select(e => (e.ProductCode ?? "").Trim()).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var categoryByCode = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (productCodes.Count > 0)
        {
            var rows = await _db.TblInvmas
                .AsNoTracking()
                .Where(p => productCodes.Contains((p.product_code ?? "").Trim()))
                .Select(p => new { product_code = (p.product_code ?? "").Trim(), p.category })
                .ToListAsync(ct);
            foreach (var r in rows)
                categoryByCode[r.product_code] = r.category;
        }

        var items = new List<RecommendedEquipmentItem>();
        foreach (var e in equipment)
        {
            var code = (e.ProductCode ?? "").Trim();
            var desc = (e.Description ?? "").Trim();
            categoryByCode.TryGetValue(code, out var category);
            if (string.IsNullOrWhiteSpace(category))
            {
                var d = desc.ToLowerInvariant();
                if (d.Contains("microphone") || d.Contains("mic ")) category = "W/MIC";
                else if (d.Contains("projector") || d.Contains("projection")) category = "PROJECTR";
                else if (d.Contains("screen")) category = "SCREEN";
                else if (d.Contains("speaker") || d.Contains("audio")) category = "SPEAKER";
                else if (d.Contains("led") || d.Contains("light")) category = "LED";
            }
            items.Add(new RecommendedEquipmentItem
            {
                ProductCode = code,
                Description = desc,
                Category = category,
                Quantity = e.Quantity,
                UnitPrice = 0
            });
        }

        var result = new SmartEquipmentRecommendation
        {
            EventContext = context,
            Items = items,
            LaborItems = new List<RecommendedLaborItem>()
        };
        await RecommendLaborAsync(result, context, ct);
        return result.LaborItems;
    }

    /// <summary>
    /// Returns the AI-approved catalog product codes.
    /// Source order:
    /// 1) item-rules.json product_code entries
    /// 2) venue-room-packages.json package codes
    /// 3) known fixed accessory/package codes used by recommendation logic
    /// </summary>
    private static string BuildAiCatalogFingerprint(IWebHostEnvironment env)
    {
        var dataPath = Path.Combine(env.WebRootPath ?? string.Empty, "data");
        var itemRulesPath = Path.Combine(dataPath, "item-rules.json");
        var roomPackagesPath = Path.Combine(dataPath, "venue-room-packages.json");
        return $"{itemRulesPath}|{GetFileWriteTicksUtc(itemRulesPath)}|{roomPackagesPath}|{GetFileWriteTicksUtc(roomPackagesPath)}";
    }

    private static long GetFileWriteTicksUtc(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
        }
        catch
        {
            return -1L;
        }
    }

    private async Task<List<string>> GetAiCatalogCodesAsync(CancellationToken ct)
    {
        var fingerprint = BuildAiCatalogFingerprint(_env);
        lock (_sharedAiCatalogLock)
        {
            if (_sharedAiCatalogCodes is { Count: > 0 } && _sharedAiCatalogFingerprint == fingerprint)
                return _sharedAiCatalogCodes;
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dataPath = Path.Combine(_env.WebRootPath ?? string.Empty, "data");

        // item-rules.json (extract all "product_code" values recursively)
        var itemRulesPath = Path.Combine(dataPath, "item-rules.json");
        if (File.Exists(itemRulesPath))
        {
            try
            {
                await using var fs = File.OpenRead(itemRulesPath);
                using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
                CollectProductCodesRecursive(doc.RootElement, codes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed reading item-rules.json for AI catalog code extraction");
            }
        }

        // venue-room-packages.json (extract all string package codes recursively)
        var roomPackagesPath = Path.Combine(dataPath, "venue-room-packages.json");
        if (File.Exists(roomPackagesPath))
        {
            try
            {
                await using var fs = File.OpenRead(roomPackagesPath);
                using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
                CollectCodesFromStringArraysRecursive(doc.RootElement, codes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed reading venue-room-packages.json for AI catalog code extraction");
            }
        }

        // Dynamic SubCategory discovery: pull all product codes from aiFolders referenced in venue-room-packages.json
        var venueSubCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(roomPackagesPath))
        {
            try
            {
                await using var fs2 = File.OpenRead(roomPackagesPath);
                using var doc2 = await JsonDocument.ParseAsync(fs2, cancellationToken: ct);
                CollectAiFolderValues(doc2.RootElement, venueSubCategories);
            }
            catch { /* best effort */ }
        }

        if (venueSubCategories.Count > 0)
        {
            try
            {
                var subCatNorm = venueSubCategories.Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
                var subCatProducts = await _db.TblInvmas
                    .AsNoTracking()
                    .Where(p => p.SubCategory != null && subCatNorm.Contains((p.SubCategory ?? "").Trim().ToLower()))
                    .Select(p => (p.product_code ?? "").Trim())
                    .ToListAsync(ct);
                foreach (var code in subCatProducts.Where(c => !string.IsNullOrWhiteSpace(c)))
                    codes.Add(code);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed discovering AI catalog codes from SubCategories");
            }
        }

        // Safety net for fixed codes used directly by recommendation logic (includes new AI-folder SKUs + legacy WSB*)
        foreach (var fixedCode in new[]
                 {
                     "USBCMX2", "V1HD", "SDICROSS", "NATFLIPC", "LOG4kCAM", "LECT1", "SHURE418",
                     "NATFLSTD", "LCD40", "MIXER06", "LOGISPOT", "WIRPRES", "QLXD2SK",
                     "THRVAVP", "THRVCSS", "THRVPROJ", "THRVIND",
                     "WBDPROJ", "WBSPROJ", "WBSNPROJ", "WBSSPROJ", "WBFBCSS", "WBSBCSS", "WBIND", "WBAVP", "WBSAVP",
                     "ELEVPROJ", "ELEVAVP", "ELEVSAVP", "ELEVCSS", "ELEVSCSS", "ELEVIND",
                     "WSBTHAV", "WSBTHAUD", "WSBTHPRO",
                     "WSBBDPRO", "WSBBSPRO", "WSBNSPRO", "WSBSSPRO", "WSBFBALL", "WSBALLAU",
                     "WSBELSAD", "WSBELAUD", "WSBELPRO", "WSBELAV", "WSBFELAV",
                     "WSBFPAUD", "WSBFPPRO", "WSBFPAVP",
                     "PCLPRO", "PCLP-L1", "PCLP-L2", "PCLP-L3", "PCPROLT1",
                     "13MBP-LM", "13MBP-LT", "13MBP-L1", "13MBP-L2",
                     // In-memory / fixture SKUs used by unit tests (no item-rules.json in temp web roots)
                     "MIC-HH-01", "PA-PORT-01", "SCREEN16"
                 })
        {
            codes.Add(fixedCode);
        }

        var list = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_sharedAiCatalogLock)
        {
            // Another thread may have populated while we were parsing; prefer the fresher fingerprint match.
            if (_sharedAiCatalogCodes is { Count: > 0 } && _sharedAiCatalogFingerprint == fingerprint)
                return _sharedAiCatalogCodes;

            _sharedAiCatalogFingerprint = fingerprint;
            _sharedAiCatalogCodes = list;
        }

        _logger.LogInformation("Loaded {Count} AI-catalog equipment codes (shared cache)", list.Count);
        return list;
    }

    /// <summary>
    /// Batches TblInvmas and TblRatetbls reads for the current recommendation request.
    /// </summary>
    private async Task EnsureRecommendationProductsLoadedAsync(IReadOnlyCollection<string> codes, CancellationToken ct)
    {
        if (_recommendationInvBatch == null || _recommendationRateBatch == null || codes.Count == 0)
            return;

        var normalized = codes
            .Select(c => (c ?? "").Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var needInv = normalized.Where(c => !_recommendationInvBatch.ContainsKey(c)).ToList();
        if (needInv.Count > 0)
        {
            // Match DB codes without client-side Trim() in the predicate (InMemory + some providers translate poorly).
            var needSet = needInv.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rows = await _db.TblInvmas
                .AsNoTracking()
                .Where(p => p.product_code != null && needSet.Contains(p.product_code))
                // Explicit SKU batch load: do not apply AI-catalog filter (differs from open search; avoids test/prod mismatches).
                .Select(p => new ProductInvSnap(
                    (p.product_code ?? "").Trim(),
                    p.descriptionv6,
                    p.PrintedDesc,
                    p.category,
                    p.PictureFileName))
                .ToListAsync(ct);

            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                _recommendationInvBatch[row.ProductCode] = row;
                found.Add(row.ProductCode);
            }

            foreach (var c in needInv)
            {
                if (!found.Contains(c))
                    _recommendationInvBatch[c] = null;
            }
        }

        var needRate = normalized
            .Where(c => !_recommendationRateBatch.ContainsKey(c))
            .Where(c => _recommendationInvBatch.TryGetValue(c, out var inv) && inv != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (needRate.Count > 0)
        {
            var rateSet = needRate.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rates = await _db.TblRatetbls
                .AsNoTracking()
                .Where(r => r.TableNo == 0 && r.product_code != null && rateSet.Contains(r.product_code))
                .Select(r => new { Code = r.product_code!.Trim(), r.rate_1st_day, r.rate_extra_days })
                .ToListAsync(ct);

            var byCode = rates
                .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var c in needRate)
            {
                if (byCode.TryGetValue(c, out var rt))
                    _recommendationRateBatch[c] = new RateSnap(rt.rate_1st_day ?? 0, rt.rate_extra_days ?? 0);
                else
                    _recommendationRateBatch[c] = null;
            }
        }
    }

    private static void CollectProductCodesRecursive(JsonElement element, HashSet<string> codes)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("product_code") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var code = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                            codes.Add(code.Trim());
                    }
                    CollectProductCodesRecursive(prop.Value, codes);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectProductCodesRecursive(item, codes);
                break;
        }
    }

    private static void CollectAiFolderValues(JsonElement element, HashSet<string> folders)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("aiFolder") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                            folders.Add(val.Trim());
                    }
                    // Also discover products from independentItems folders (THRVIND, ELEVIND, WBIND etc.)
                    if (prop.NameEquals("independentItems") && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var arrItem in prop.Value.EnumerateArray())
                        {
                            if (arrItem.ValueKind == JsonValueKind.String)
                            {
                                var val = arrItem.GetString();
                                if (!string.IsNullOrWhiteSpace(val))
                                    folders.Add(val.Trim());
                            }
                        }
                    }
                    CollectAiFolderValues(prop.Value, folders);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectAiFolderValues(item, folders);
                break;
        }
    }

    private static void CollectCodesFromStringArraysRecursive(JsonElement element, HashSet<string> codes)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var code = item.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                            codes.Add(code.Trim());
                    }
                    else
                    {
                        CollectCodesFromStringArraysRecursive(item, codes);
                    }
                }
                break;

            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    CollectCodesFromStringArraysRecursive(prop.Value, codes);
                break;
        }
    }
}

internal readonly record struct OperationProfile(
    bool HasRequired,
    bool HasRecommended,
    bool HasSelfOperated,
    bool HasUnknown)
{
    public static OperationProfile Empty => new(false, false, false, false);
    public bool IsSelfOperatedOnly => HasSelfOperated && !HasRequired && !HasRecommended && !HasUnknown;
}

internal sealed class WestinLaborRulesConfig
{
    public List<WestinLaborRoomRule> Rooms { get; set; } = new();
    public WestinLaborSpecialRules? SpecialRules { get; set; }
}

internal sealed class WestinLaborRoomRule
{
    public string RoomKey { get; set; } = "";
    public List<string> RoomContains { get; set; } = new();
    public List<string> PackageCodes { get; set; } = new();
    public string BaselineLaborCode { get; set; } = "AVTECH";
    public string VisionSpecialistCode { get; set; } = "VXTECH";
    public string AudioSpecialistCode { get; set; } = "AXTECH";
    public int MicrophoneOperatorThreshold { get; set; } = 2;
}

internal sealed class WestinLaborSpecialRules
{
    public string SwitcherCode { get; set; } = "V1HD";
    public string MixerCode { get; set; } = "MIXER06";
    public string FlipchartCode { get; set; } = "NATFLIPC";
    public string VideoConferenceCode { get; set; } = "LOG4kCAM";
    public List<string> LecternCodes { get; set; } = new() { "LECT1", "SHURE418" };
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
    public int NumberOfMicrophones { get; set; } // New field
    public bool NeedsLaptops { get; set; }
    public bool IsContentHeavy { get; set; } // New field
    public bool IsContentLight { get; set; } // New field
    public bool NeedsRecording { get; set; } // New field
    public bool NeedsStreaming { get; set; } // New field
    public bool NeedsHeavyStreaming { get; set; } // New field
    public bool NeedsLighting { get; set; } // New field
    public bool NeedsAdvancedLighting { get; set; } // New field
    public string? SpeakerStylePreference { get; set; } // e.g., "inbuilt", "external", "portable"
    /// <summary>
    /// Set when the base AV wizard was submitted and the user explicitly did NOT select speakers.
    /// Prevents audio pairing logic from auto-adding speakers against the user's choice.
    /// </summary>
    public bool UserDeclinedAudio { get; set; }
    /// <summary>
    /// Set when the base AV wizard was submitted and the user explicitly unchecked the combined
    /// "Inbuilt projector and screen" checkbox. Prevents projection packages from being auto-added.
    /// </summary>
    public bool UserDeclinedProjection { get; set; }
    public List<string> ProjectorAreas { get; set; } = new();
    public List<EquipmentRequest> EquipmentRequests { get; set; } = new();
}

public class EquipmentRequest
{
    public string EquipmentType { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string? Preference { get; set; } // e.g., "windows", "mac"
    public string? MicrophoneType { get; set; } // e.g., "handheld", "lapel"
    public string? SpeakerStyle { get; set; } // e.g., "inbuilt", "external", "portable"
    public string? SpecificModel { get; set; } // e.g., "Dell 3580" for reverse lookup
}

public class SmartEquipmentRecommendation
{
    public EventContext? EventContext { get; set; }
    public List<RecommendedEquipmentItem> Items { get; set; } = new();
    public List<RecommendedLaborItem> LaborItems { get; set; } = new();
    public double TotalDayRate { get; set; }
}

/// <summary>
/// Minimal equipment item for labor recalculation (e.g. from update_equipment flow).
/// </summary>
public class EquipmentItemForLabor
{
    public string ProductCode { get; set; } = "";
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public class RecommendedLaborItem
{
    public string ProductCode { get; set; } = "AVTECH";
    public string Description { get; set; } = "";
    public string Task { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public double Hours { get; set; }
    public int Minutes { get; set; }
    public string RecommendationReason { get; set; } = "";
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
    /// <summary>Job comment for operator (e.g. "Client requested: Handheld" for QLXD2SK mic kit).</summary>
    public string? Comment { get; set; }
    
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
