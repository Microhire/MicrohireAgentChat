using Azure.AI.Agents.Persistent;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
    #region Room Tool Handlers

    private async Task<string> HandleListRoomsAsync(CancellationToken ct)
    {
        var allRooms = await _roomCatalog.GetRoomsAsync(ct);
        var rooms = allRooms
            .Select(r =>
            {
                var capacities = r.Layouts
                    .Where(l => l.Capacity > 0)
                    .ToDictionary(l => l.Type, l => l.Capacity, StringComparer.OrdinalIgnoreCase);
                return new
                {
                    id = r.Id,
                    name = r.Name,
                    slug = r.Slug,
                    level = r.Level,
                    cover = ToAbsoluteUrl(r.Cover),
                    capacities = capacities.Count > 0 ? capacities : null
                };
            })
            .ToList();

        var roomCards = allRooms
            .Select(r =>
            {
                var maxCap = r.Layouts.Count > 0 ? r.Layouts.Max(l => l.Capacity) : (int?)null;
                return new IslaBlocks.RoomCard(
                    r.Id,
                    r.Name,
                    r.Slug,
                    ToAbsoluteUrl(r.Cover),
                    r.Level,
                    maxCap);
            })
            .ToList();
        var baseUrl = GetBaseUrl();
        var galleryBlock = IslaBlocks.BuildRoomsGalleryBlock(roomCards, baseUrl, max: 12, headerRoomList: "The Westin Brisbane");
        var outputToUser = "Here are the rooms at The Westin Brisbane:\n\n" + galleryBlock;
        var payload = new
        {
            rooms,
            outputToUser,
            instruction = "OUTPUT the 'outputToUser' value EXACTLY AS-IS so room images appear. Do not paraphrase."
        };
        return JsonSerializer.Serialize(payload);
    }

    private async Task<string> HandleGetRoomImagesAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string roomKey = "";
        if (doc.RootElement.TryGetProperty("room", out var rProp) && rProp.ValueKind == JsonValueKind.String)
            roomKey = rProp.GetString() ?? "";

        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var roomNorm = roomKey.Trim().ToLowerInvariant();
        var roomNormSlug = roomNorm.Replace(" ", "-");
        var room = rooms.FirstOrDefault(r =>
                r.Name.Equals(roomKey, StringComparison.OrdinalIgnoreCase) ||
                r.Slug.Equals(roomKey, StringComparison.OrdinalIgnoreCase))
            ?? rooms.FirstOrDefault(r => r.Slug.Equals(roomNormSlug, StringComparison.OrdinalIgnoreCase))
            ?? (roomNorm.Contains("thrive") ? rooms.FirstOrDefault(r => r.Slug == "thrive-boardroom") : null)
            ?? (roomNorm == "elevate 1" ? rooms.FirstOrDefault(r => r.Slug == "elevate-1") : null)
            ?? (roomNorm == "elevate 2" ? rooms.FirstOrDefault(r => r.Slug == "elevate-2") : null)
            ?? (roomNorm == "elevate" ? rooms.FirstOrDefault(r => r.Slug == "elevate") : null)
            ?? (roomNorm == "ballroom 1" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-1") : null)
            ?? (roomNorm == "ballroom 2" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-2") : null)
            ?? (roomNorm == "ballroom" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom") : null)
            ?? rooms.FirstOrDefault(r => r.Name.Contains(roomKey, StringComparison.OrdinalIgnoreCase) || r.Slug.Contains(roomNormSlug, StringComparison.OrdinalIgnoreCase));

        if (room is null)
        {
            return JsonSerializer.Serialize(new { error = "room not found" });
        }

        var coverUrl = ToAbsoluteUrl(room.Cover);

        if (room.Slug == "thrive-boardroom")
        {
            var coverOnly = IslaBlocks.BuildCoverOnlyBlock(room.Name, coverUrl, GetBaseUrl());
            var fixedOutput = $"Here is the Thrive Boardroom:\n\n{coverOnly}";
            return JsonSerializer.Serialize(new
            {
                room = room.Name,
                cover = coverUrl,
                fixedLayout = "boardroom",
                outputToUser = fixedOutput,
                instruction = "OUTPUT the 'outputToUser' value EXACTLY AS-IS. Do NOT ask the user for room setup style -- automatically use 'boardroom' for Thrive Boardroom."
            });
        }

        var layouts = room.Layouts.Select(l => new
        {
            type = l.Type,
            capacity = l.Capacity,
            image = ToAbsoluteUrl(l.Image)
        }).ToList();

        var roomImagesDto = new IslaBlocks.RoomImagesDto(
            room.Name,
            coverUrl,
            room.Layouts.Select(l => new IslaBlocks.LayoutDto(l.Type, l.Capacity, ToAbsoluteUrl(l.Image))).ToList());
        var baseUrl = GetBaseUrl();
        var galleryBlock = IslaBlocks.BuildLayoutsGalleryBlock(roomImagesDto, baseUrl, includeCover: true);
        var outputToUser = $"Here are the room and setup options for {room.Name}:\n\n" + galleryBlock;

        var payload = new
        {
            room = room.Name,
            cover = coverUrl,
            layouts,
            outputToUser,
            instruction = "OUTPUT the 'outputToUser' value EXACTLY AS-IS so room and setup images appear."
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task<(string? capacityWarning, string? capacityOkLine)> TryGetCapacityCheckAsync(
        string venueName, string roomName, int expectedAttendees, string? setupStyle, CancellationToken ct)
    {
        if (expectedAttendees <= 0 || string.IsNullOrWhiteSpace(setupStyle)) return (null, null);
        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var roomNorm = roomName.Trim().ToLowerInvariant();
        var roomNormSlug = roomNorm.Replace(" ", "-");

        // Robust room resolution
        var room = rooms.FirstOrDefault(r => r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Equals(roomName, StringComparison.OrdinalIgnoreCase))
                ?? rooms.FirstOrDefault(r => r.Slug.Equals(roomNormSlug, StringComparison.OrdinalIgnoreCase))
                ?? (roomNorm.Contains("thrive") ? rooms.FirstOrDefault(r => r.Slug == "thrive-boardroom") : null)
                ?? (roomNorm == "elevate 1" ? rooms.FirstOrDefault(r => r.Slug == "elevate-1") : null)
                ?? (roomNorm == "elevate 2" ? rooms.FirstOrDefault(r => r.Slug == "elevate-2") : null)
                ?? (roomNorm == "elevate" ? rooms.FirstOrDefault(r => r.Slug == "elevate") : null)
                ?? (roomNorm == "ballroom 1" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-1") : null)
                ?? (roomNorm == "ballroom 2" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-2") : null)
                ?? (roomNorm == "ballroom" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom") : null)
                ?? rooms.FirstOrDefault(r => r.Name.Contains(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Contains(roomNormSlug, StringComparison.OrdinalIgnoreCase));

        if (room is null) return (null, null);

        // Use explicit setup_style only. Do not infer from event type.
        var style = (setupStyle ?? "").Trim().ToLowerInvariant();
        string layoutType = style switch
        {
            "theatre" or "theater" => "Theatre",
            "banquet" => "Banquet",
            "classroom" or "schoolroom" => "Classroom",
            "boardroom" or "conference" => "Boardroom",
            "ushape" or "u-shape" or "u shape" => "U-Shape",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(layoutType))
            return (null, null);

        var layout = room.Layouts.FirstOrDefault(l => l.Type.Equals(layoutType, StringComparison.OrdinalIgnoreCase));
        var capacity = layout?.Capacity ?? 0;
        if (capacity <= 0) return (null, null);

        if (expectedAttendees > capacity)
        {
            // Suggest larger rooms that have this layout and fit the attendees
            var larger = rooms
                .Select(r2 =>
                {
                    var l2 = r2.Layouts.FirstOrDefault(l => l.Type.Equals(layoutType, StringComparison.OrdinalIgnoreCase));
                    return (room: r2, cap: l2?.Capacity ?? 0);
                })
                .Where(x => x.cap >= expectedAttendees)
                .OrderBy(x => x.cap)
                .Take(3)
                .Select(x => $"{x.room.Name} ({x.cap} {layoutType.ToLowerInvariant()})")
                .ToList();
            var suggest = larger.Count > 0 ? " Consider " + string.Join(", or ", larger) + "." : "";
            return ($"{room.Name} {layoutType} capacity is {capacity}; your event has {expectedAttendees} attendees.{suggest}", null);
        }

        return (null, $"**Room capacity ({layoutType}):** {capacity} — your event fits.");
    }

    private async Task<string> HandleGetRoomCapacityAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var roomName = doc.RootElement.TryGetProperty("room_name", out var rn) ? rn.GetString()?.Trim() : null;
        var setupType = doc.RootElement.TryGetProperty("setup_type", out var st) ? st.GetString()?.Trim() : null;

        if (string.IsNullOrWhiteSpace(roomName))
        {
            return JsonSerializer.Serialize(new { outputToUser = "Please specify a room name (e.g. Elevate, Thrive Boardroom, Westin Ballroom)." });
        }

        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var roomNorm = roomName.Trim().ToLowerInvariant();
        var roomNormSlug = roomNorm.Replace(" ", "-");

        // Robust room resolution
        var room = rooms.FirstOrDefault(r => r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Equals(roomName, StringComparison.OrdinalIgnoreCase))
                ?? rooms.FirstOrDefault(r => r.Slug.Equals(roomNormSlug, StringComparison.OrdinalIgnoreCase))
                ?? (roomNorm.Contains("thrive") ? rooms.FirstOrDefault(r => r.Slug == "thrive-boardroom") : null)
                ?? (roomNorm == "elevate 1" ? rooms.FirstOrDefault(r => r.Slug == "elevate-1") : null)
                ?? (roomNorm == "elevate 2" ? rooms.FirstOrDefault(r => r.Slug == "elevate-2") : null)
                ?? (roomNorm == "elevate" ? rooms.FirstOrDefault(r => r.Slug == "elevate") : null)
                ?? (roomNorm == "ballroom 1" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-1") : null)
                ?? (roomNorm == "ballroom 2" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-2") : null)
                ?? (roomNorm == "ballroom" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom") : null)
                ?? rooms.FirstOrDefault(r => r.Name.Contains(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Contains(roomNormSlug, StringComparison.OrdinalIgnoreCase));

        if (room is null)
        {
            return JsonSerializer.Serialize(new { outputToUser = $"Room '{roomName}' was not found at The Westin Brisbane. Use list_westin_rooms to see available rooms." });
        }

        var capacities = room.Layouts.Where(l => l.Capacity > 0).ToList();
        if (capacities.Count == 0)
        {
            return JsonSerializer.Serialize(new { outputToUser = $"{room.Name}: capacity by setup is not specified in the catalog. Contact the venue for details." });
        }

        // Map optional setup_type (tool param) to layout type (catalog uses Theatre, Boardroom, U-Shape, etc.)
        string? layoutType = null;
        if (!string.IsNullOrWhiteSpace(setupType))
        {
            var setupNorm = setupType.Trim().ToLowerInvariant();
            layoutType = setupNorm switch
            {
                "theatre" or "theater" => "Theatre",
                "banquet" => "Banquet",
                "classroom" or "schoolroom" => "Classroom",
                "boardroom" or "conference" => "Boardroom",
                "ushape" or "u-shape" or "u shape" => "U-Shape",
                "reception" or "cocktail" => "Reception", // catalog may not have Reception; we use Cocktail where applicable
                _ => capacities.FirstOrDefault(l => l.Type.Equals(setupType, StringComparison.OrdinalIgnoreCase))?.Type
            };
            if (string.IsNullOrEmpty(layoutType) && capacities.All(c => !c.Type.Equals(setupType, StringComparison.OrdinalIgnoreCase)))
                layoutType = setupNorm switch
                {
                    "reception" => capacities.FirstOrDefault(c => c.Type.Equals("Reception", StringComparison.OrdinalIgnoreCase))?.Type ?? "Reception",
                    "cocktail" => "Banquet", // fallback for cocktail
                    _ => null
                };
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(layoutType))
        {
            var layout = room.Layouts.FirstOrDefault(l => l.Type.Equals(layoutType, StringComparison.OrdinalIgnoreCase));
            if (layout != null && layout.Capacity > 0)
            {
                sb.AppendLine($"{room.Name} — **{layout.Type}** capacity: **{layout.Capacity}**.");
                return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
            }
            sb.AppendLine($"{room.Name}: no capacity listed for '{setupType}'. Available setups: " +
                string.Join(", ", capacities.Select(c => $"{c.Type} ({c.Capacity})")) + ".");
            return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
        }

        sb.AppendLine($"**{room.Name}** — capacities by setup:");
        foreach (var c in capacities.OrderByDescending(c => c.Capacity))
            sb.AppendLine($"- **{c.Type}:** {c.Capacity}");
        return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
    }

    private async Task<string> HandleGetCapacityTableAsync(CancellationToken ct)
    {
        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("## Westin Brisbane - Room Capacities (Theatre Style)");
        sb.AppendLine();
        sb.AppendLine("| Room Name | Theatre Capacity | Reception | Area (sqm) |");
        sb.AppendLine("| :--- | :---: | :---: | :---: |");

        var sortedRooms = rooms
            .Select(r => new
            {
                r.Name,
                Theatre = r.Layouts.FirstOrDefault(l => l.Type == "Theatre")?.Capacity ?? 0,
                Reception = r.Layouts.FirstOrDefault(l => l.Type == "Reception")?.Capacity ?? 0,
                Area = 0 // Will fetch from guide if needed, but for sorting we use Theatre
            })
            .OrderByDescending(x => x.Theatre)
            .ToList();

        // Get area from guide for completeness
        var dataPath = Path.Combine(_env.WebRootPath ?? "", "data");
        var guidePath = Path.Combine(dataPath, "westin-venue-guide.json");
        var areas = new Dictionary<string, int>();
        if (File.Exists(guidePath))
        {
            var guideJson = await File.ReadAllTextAsync(guidePath, ct);
            using var doc = JsonDocument.Parse(guideJson);
            foreach (var v in doc.RootElement.GetProperty("venues").EnumerateArray())
            {
                var name = v.GetProperty("name").GetString() ?? "";
                if (v.TryGetProperty("sizeSqm", out var sqm) && sqm.ValueKind == JsonValueKind.Number)
                    areas[name] = sqm.GetInt32();
            }
        }

        foreach (var r in sortedRooms)
        {
            var area = areas.TryGetValue(r.Name, out var a) ? a.ToString() : "-";
            var theatre = r.Theatre > 0 ? r.Theatre.ToString() : "-";
            var reception = r.Reception > 0 ? r.Reception.ToString() : "-";
            sb.AppendLine($"| {r.Name} | {theatre} | {reception} | {area} |");
        }

        return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
    }

    #endregion
}
