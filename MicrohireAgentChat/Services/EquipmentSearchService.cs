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
public sealed partial class EquipmentSearchService
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

        // === CLICKERS / PRESENTATION REMOTES (description-based; category may vary by DB) ===
        ["clicker"] = new("Presentation Remotes", null, null, new[] { "clicker", "presentation remote", "logitech presenter", "wireless presenter" }, false),
        ["clickers"] = new("Presentation Remotes", null, null, new[] { "clicker", "presentation remote", "logitech presenter", "wireless presenter" }, false),
        ["presentation remote"] = new("Presentation Remotes", null, null, new[] { "clicker", "presentation remote", "logitech presenter", "wireless presenter" }, false),
        ["presentation remotes"] = new("Presentation Remotes", null, null, new[] { "clicker", "presentation remote", "logitech presenter", "wireless presenter" }, false),
        ["wireless presenter"] = new("Presentation Remotes", null, null, new[] { "clicker", "presentation remote", "logitech presenter", "wireless presenter" }, false),

        // === ACCESSORIES (Westin package rules) ===
        ["flipchart"] = new("Flipcharts", new[] { "FLIPCHART", "STATIONERY" }, "VISION", new[] { "flipchart", "flip chart", "easel", "whiteboard" }, false),
        ["flipcharts"] = new("Flipcharts", new[] { "FLIPCHART", "STATIONERY" }, "VISION", new[] { "flipchart", "flip chart" }, false),
        ["hdmi_adaptor"] = new("HDMI Adaptors", new[] { "CABLE", "ADAPTOR", "CONVERTER" }, "VISION", new[] { "usbc", "hdmi", "adaptor", "adapter" }, false),
        ["hdmi adaptor"] = new("HDMI Adaptors", new[] { "CABLE", "ADAPTOR", "CONVERTER" }, "VISION", new[] { "usbc", "hdmi", "adaptor" }, false),
        ["switcher"] = new("Vision Switchers", new[] { "SWITCHER", "SCALER", "VISION" }, "VISION", new[] { "switcher", "hdmi switcher", "seamless" }, false),
        ["foldback_monitor"] = new("Foldback Monitors", new[] { "LCD-AV", "MONITOR" }, "VISION", new[] { "foldback", "confidence", "monitor" }, false),
        ["foldback monitor"] = new("Foldback Monitors", new[] { "LCD-AV", "MONITOR" }, "VISION", new[] { "foldback", "confidence" }, false),
        ["video_conference_unit"] = new("Video Conference", new[] { "CAMERAS", "CONFERENCE" }, "VISION", new[] { "video conference", "teams", "zoom", "logitech" }, false),
        ["video conference unit"] = new("Video Conference", new[] { "CAMERAS", "CONFERENCE" }, "VISION", new[] { "video conference", "teams", "zoom" }, false),
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
            @"(\d+)\s*(clickers?|presentation\s+remotes?)",
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
            "speaker", "camera", "ipad", "tablet", "lectern", "podium", "clicker", "presentation remote"
        };

        // Special handling for Microsoft Teams and video conferencing
        var teamsPatterns = new[] { "teams", "microsoft teams", "ms teams", "video conference", "video conferencing" };
        foreach (var pattern in teamsPatterns)
        {
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // Microsoft Teams requires: laptop (if not provided), webcam, microphone/speaker, internet
                if (!requirements.Any(r => r.NormalizedType.Contains("laptop")))
                {
                    requirements.Add(new EquipmentRequirement
                    {
                        OriginalText = "Microsoft Teams",
                        NormalizedType = "laptop",
                        Quantity = 1
                    });
                }
                if (!requirements.Any(r => r.NormalizedType.Contains("webcam") || r.NormalizedType.Contains("camera")))
                {
                    requirements.Add(new EquipmentRequirement
                    {
                        OriginalText = "Microsoft Teams",
                        NormalizedType = "webcam",
                        Quantity = 1
                    });
                }
                if (!requirements.Any(r => r.NormalizedType.Contains("microphone") || r.NormalizedType.Contains("mic")))
                {
                    requirements.Add(new EquipmentRequirement
                    {
                        OriginalText = "Microsoft Teams",
                        NormalizedType = "microphone",
                        Quantity = 1
                    });
                }
                if (!requirements.Any(r => r.NormalizedType.Contains("speaker")))
                {
                    requirements.Add(new EquipmentRequirement
                    {
                        OriginalText = "Microsoft Teams",
                        NormalizedType = "speaker",
                        Quantity = 1
                    });
                }
                // Note: Internet connection is typically assumed to be available
                break; // Only add once even if multiple patterns match
            }
        }

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
            .Replace("clickers", "clicker")
            .Replace("presentation remotes", "presentation remote")
            .Replace("mixers", "mixer")
            .Trim();

        return normalized;
    }

    #endregion

}
