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
