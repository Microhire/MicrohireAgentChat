using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Reads <c>wwwroot/data/venue-room-packages.json</c> with mtime-based caching.
/// Used for optional per-room UI metadata such as projector placement options.
/// </summary>
public static class VenueRoomPackagesCache
{
    private static readonly object LockObj = new();
    private static string? _pathCached;
    private static DateTime _writeUtc;
    private static Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>? _mapping;

    /// <summary>
    /// Returns projector placement options as anonymous { id, label } for the venue/room if defined in JSON.
    /// </summary>
    public static object[]? TryGetProjectorPlacementOptions(IWebHostEnvironment env, string? venueName, string? roomName)
    {
        var mapping = GetMapping(env);
        if (mapping == null || string.IsNullOrWhiteSpace(venueName) || string.IsNullOrWhiteSpace(roomName))
            return null;

        if (!TryGetVenueRooms(mapping, venueName, out var rooms))
            return null;

        if (!rooms.TryGetValue(roomName.Trim(), out var roomEl))
            return null;

        if (!roomEl.TryGetValue("projectorPlacementOptions", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<object>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var label = item.TryGetProperty("label", out var lbEl) ? lbEl.GetString() : id;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            list.Add(new { id, label = label ?? id });
        }

        return list.Count > 0 ? list.ToArray() : null;
    }

    private static bool TryGetVenueRooms(
        Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>> mapping,
        string? venueName,
        out Dictionary<string, Dictionary<string, JsonElement>> rooms)
    {
        if (string.IsNullOrWhiteSpace(venueName))
        {
            rooms = null!;
            return false;
        }

        var trimmed = venueName.Trim();
        var resolved = ResolveMappingVenueKey(trimmed);
        if (!string.IsNullOrWhiteSpace(resolved) && mapping.TryGetValue(resolved, out var byResolved))
        {
            rooms = byResolved;
            return true;
        }

        if (mapping.TryGetValue(trimmed, out var byTrim))
        {
            rooms = byTrim;
            return true;
        }

        rooms = null!;
        return false;
    }

    /// <summary>
    /// Maps draft/session room text to the key used in <c>venue-room-packages.json</c> (same rules as smart equipment).
    /// </summary>
    public static string? ResolveMappingRoomKey(string? roomName, string mappingVenueKey)
    {
        var roomNorm = (roomName ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(roomNorm))
            return null;

        if (mappingVenueKey == "Westin Brisbane")
        {
            if (roomNorm.Contains("ballroom 1") || roomNorm.Contains("ballroom-1")) return "Westin Ballroom 1";
            if (roomNorm.Contains("ballroom 2") || roomNorm.Contains("ballroom-2")) return "Westin Ballroom 2";
            if (roomNorm.Contains("ballroom")) return "Westin Ballroom";
            if (roomNorm.Contains("elevate 1") || roomNorm.Contains("elevate-1")) return "Elevate";
            if (roomNorm.Contains("elevate 2") || roomNorm.Contains("elevate-2")) return "Elevate";
            if (roomNorm.Contains("elevate")) return "Elevate";
            if (roomNorm.Contains("thrive")) return "Thrive Boardroom";
        }

        if (mappingVenueKey == "Four Points Brisbane")
        {
            if (roomNorm.Contains("meeting") || roomNorm.Contains("four points")) return "Meeting Room";
        }

        return null;
    }

    /// <summary>
    /// Resolves venue display name for JSON lookup (Westin Brisbane / Four Points Brisbane).
    /// </summary>
    public static string? ResolveMappingVenueKey(string? venueName)
    {
        var venueNorm = (venueName ?? "").Trim().ToLowerInvariant();
        if (venueNorm.Contains("westin") && venueNorm.Contains("brisbane"))
            return "Westin Brisbane";
        if (venueNorm.Contains("four points") && venueNorm.Contains("brisbane"))
            return "Four Points Brisbane";
        return null;
    }

    /// <summary>
    /// Labels shown as checked items on the Base AV form for the room (from <c>baseEquipment</c> array).
    /// </summary>
    public static IReadOnlyList<string> TryGetBaseEquipmentLabels(IWebHostEnvironment env, string? venueName, string? roomDraftName)
    {
        var labels = TryGetStringArrayProperty(env, venueName, roomDraftName, "baseEquipment");
        return labels ?? Array.Empty<string>();
    }

    /// <summary>
    /// Reads a string array property for the mapped venue/room in <c>venue-room-packages.json</c>.
    /// </summary>
    public static string[]? TryGetStringArrayProperty(IWebHostEnvironment env, string? venueName, string? roomDraftName, string propertyName)
    {
        var mappingVenue = ResolveMappingVenueKey(venueName);
        if (mappingVenue == null || string.IsNullOrWhiteSpace(roomDraftName))
            return null;

        var roomKey = ResolveMappingRoomKey(roomDraftName, mappingVenue);
        if (string.IsNullOrWhiteSpace(roomKey))
            return null;

        var mapping = GetMapping(env);
        if (mapping == null)
            return null;

        if (!TryGetVenueRooms(mapping, venueName, out var rooms))
            return null;

        if (!rooms.TryGetValue(roomKey.Trim(), out var roomEl))
            return null;

        if (!roomEl.TryGetValue(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;
            var s = item.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                list.Add(s.Trim());
        }

        return list.Count > 0 ? list.ToArray() : null;
    }

    private static Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>? GetMapping(IWebHostEnvironment env)
    {
        var jsonPath = Path.Combine(env.WebRootPath ?? "", "data", "venue-room-packages.json");
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

        lock (LockObj)
        {
            if (_mapping != null && _pathCached == jsonPath && _writeUtc == writeUtc)
                return _mapping;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            var mapping = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>(json);
            lock (LockObj)
            {
                _mapping = mapping;
                _pathCached = jsonPath;
                _writeUtc = writeUtc;
            }

            return mapping;
        }
        catch
        {
            return null;
        }
    }
}
