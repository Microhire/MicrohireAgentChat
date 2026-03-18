using System.Text;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
    private async Task<string> HandleGetProductKnowledgeAsync(string argsJson, CancellationToken ct)
    {
        string? category = null;
        string? warehouseScope = null;
        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("category", out var catProp))
                category = catProp.GetString()?.Trim();
            if (root.TryGetProperty("warehouse_scope", out var scopeProp))
                warehouseScope = scopeProp.GetString()?.Trim();
        }

        var dataPath = Path.Combine(_env.WebRootPath ?? "", "data");
        var sb = new StringBuilder();

        bool loadMaster = string.IsNullOrEmpty(warehouseScope) || string.Equals(warehouseScope, "master", StringComparison.OrdinalIgnoreCase);
        bool loadWestin = string.IsNullOrEmpty(warehouseScope) || string.Equals(warehouseScope, "westin", StringComparison.OrdinalIgnoreCase);

        if (!loadMaster && !loadWestin)
        {
            sb.AppendLine("**Product knowledge**\n\nUse warehouse_scope: `master` (Brisbane, Sydney, Melbourne) or `westin` (Westin Brisbane on-site).");
            return JsonSerializer.Serialize(new { outputToUser = sb.ToString() });
        }

        if (string.IsNullOrEmpty(category) && loadMaster && loadWestin)
        {
            sb.AppendLine("**MicroHire product knowledge**\n");
            sb.AppendLine("Products are recorded in RentalPoint. Inventory is held at:");
            sb.AppendLine("- **Master warehouses:** Brisbane (Main), Sydney (WH2), Melbourne (WH3)");
            sb.AppendLine("- **On-site:** Westin Brisbane (dedicated AV warehouse inside The Westin Brisbane)\n");
            sb.AppendLine("**Categories:** Audio Equipment, Visual Equipment, Lighting Equipment, Staging & Structures, Event Technology Solutions, Computers & Playback Systems, Cables/Power/Rigging Essentials, Special Effects & Theming.");
            sb.AppendLine("\nCall `get_product_knowledge` with `category` (e.g. \"Audio\", \"Lighting\") and/or `warehouse_scope` (\"master\" or \"westin\") for full details.");
            return JsonSerializer.Serialize(new { outputToUser = sb.ToString() });
        }

        if (loadMaster)
        {
            var masterPath = Path.Combine(dataPath, "product-knowledge-master.json");
            if (File.Exists(masterPath))
            {
                var masterJson = await File.ReadAllTextAsync(masterPath, ct);
                sb.AppendLine(FormatProductKnowledgeJson(masterJson, category, "Master (Brisbane, Sydney, Melbourne)"));
            }
        }

        if (loadWestin)
        {
            var westinPath = Path.Combine(dataPath, "product-knowledge-westin.json");
            if (File.Exists(westinPath))
            {
                var westinJson = await File.ReadAllTextAsync(westinPath, ct);
                if (sb.Length > 0) sb.AppendLine("\n---\n");
                sb.AppendLine(FormatProductKnowledgeJson(westinJson, category, "Westin Brisbane on-site"));
            }
        }

        var output = sb.ToString();
        if (string.IsNullOrWhiteSpace(output))
            output = "No product knowledge found for the requested category or warehouse scope.";

        return JsonSerializer.Serialize(new { outputToUser = output });
    }

    private async Task<string> HandleGetWestinVenueGuideAsync(CancellationToken ct)
    {
        var dataPath = Path.Combine(_env.WebRootPath ?? "", "data");
        var guidePath = Path.Combine(dataPath, "westin-venue-guide.json");
        if (!File.Exists(guidePath))
        {
            var msg = "Westin Brisbane venue guide is not available.";
            return JsonSerializer.Serialize(new { outputToUser = msg });
        }

        var json = await File.ReadAllTextAsync(guidePath, ct);
        var sb = new StringBuilder();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("description", out var descProp))
        {
            sb.AppendLine(descProp.GetString() ?? "");
            sb.AppendLine();
        }

        if (root.TryGetProperty("venues", out var venues) && venues.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("## Venues at The Westin Brisbane");
            sb.AppendLine();
            foreach (var v in venues.EnumerateArray())
            {
                var name = v.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!IsQuotableWestinRoomName(name))
                    continue;
                sb.AppendLine($"### {name}");
                sb.AppendLine();

                if (v.TryGetProperty("configuration", out var config) && config.ValueKind != JsonValueKind.Null)
                {
                    var configStr = config.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(configStr))
                        sb.AppendLine("**Configuration:** " + configStr + "  ");
                }
                if (v.TryGetProperty("sizeSqm", out var sqm) && sqm.ValueKind != JsonValueKind.Null)
                {
                    int? sqmVal = null;
                    if (sqm.ValueKind == JsonValueKind.Number && sqm.TryGetInt32(out var sV)) sqmVal = sV;
                    else if (sqm.ValueKind == JsonValueKind.String && int.TryParse(sqm.GetString(), out var sV2)) sqmVal = sV2;

                    if (sqmVal.HasValue)
                    {
                        var sqftLine = "";
                        if (v.TryGetProperty("sizeSqft", out var sqft) && sqft.ValueKind != JsonValueKind.Null)
                        {
                            int? sqftVal = null;
                            if (sqft.ValueKind == JsonValueKind.Number && sqft.TryGetInt32(out var sfV)) sqftVal = sfV;
                            else if (sqft.ValueKind == JsonValueKind.String && int.TryParse(sqft.GetString(), out var sfV2)) sqftVal = sfV2;
                            if (sqftVal.HasValue) sqftLine = $" ({sqftVal} sq.ft)";
                        }
                        sb.AppendLine($"**Area/Size:** {sqmVal} sqm{sqftLine}  ");
                    }
                }
                if (v.TryGetProperty("capacities", out var cap) && cap.ValueKind == JsonValueKind.Object)
                {
                    var capParts = new List<string>();
                    foreach (var p in cap.EnumerateObject())
                    {
                        string? val = p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString()
                            : (p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetRawText() : p.Value.GetString() ?? p.Value.GetRawText());
                        if (!string.IsNullOrEmpty(val) && val != "null")
                            capParts.Add($"{p.Name}: {val}");
                    }
                    if (capParts.Count > 0)
                        sb.AppendLine("**Capacities:** " + string.Join("; ", capParts) + "  ");
                }
                if (v.TryGetProperty("builtInAV", out var builtIn) && builtIn.ValueKind != JsonValueKind.Null)
                {
                    var s = builtIn.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                        sb.AppendLine("**Built-in AV:** " + s + "  ");
                }
                if (v.TryGetProperty("microhireAddOns", out var addOns) && addOns.ValueKind != JsonValueKind.Null)
                {
                    var s = addOns.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                        sb.AppendLine("**MicroHire add-ons:** " + s + "  ");
                }
                if (v.TryGetProperty("bestFor", out var bestFor) && bestFor.ValueKind != JsonValueKind.Null)
                {
                    var s = bestFor.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                        sb.AppendLine("**Best for:** " + s + "  ");
                }
                sb.AppendLine();
            }
        }

        if (root.TryGetProperty("avInfrastructureAcrossAllSpaces", out var avInfra))
        {
            var s = avInfra.GetString()?.Trim();
            if (!string.IsNullOrEmpty(s))
            {
                sb.AppendLine("## AV Infrastructure Across All Westin Spaces");
                sb.AppendLine();
                sb.AppendLine(s);
                sb.AppendLine();
            }
        }

        if (root.TryGetProperty("roomSetupTypes", out var setupTypes) && setupTypes.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("## Room Setup Types");
            sb.AppendLine();
            foreach (var st in setupTypes.EnumerateArray())
            {
                var stName = st.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "";
                var stDesc = st.TryGetProperty("description", out var sd) ? sd.GetString() ?? "" : "";
                sb.AppendLine($"- **{stName}:** {stDesc}");
            }
        }

        var output = sb.ToString();
        if (string.IsNullOrWhiteSpace(output))
            output = "No venue guide data found.";

        return JsonSerializer.Serialize(new { outputToUser = output });
    }

    /// <summary>
    /// For Westin Brisbane: resolve room, use explicit setup_style, and check expected_attendees vs capacity.
    /// Returns (capacityWarning, capacityOkLine). If over capacity, warning includes suggested larger rooms.
    /// </summary>
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

    private static string FormatProductKnowledgeJson(string json, string? categoryFilter, string scopeLabel)
    {
        var sb = new StringBuilder();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("description", out var descProp))
        {
            sb.AppendLine($"**{scopeLabel}**");
            sb.AppendLine(descProp.GetString() ?? "");
            sb.AppendLine();
        }

        if (root.TryGetProperty("seasonalAvailability", out var seasonal) && seasonal.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("**Seasonal availability**");
            foreach (var prop in seasonal.EnumerateObject())
                sb.AppendLine($"- {prop.Name}: {prop.Value.GetString() ?? ""}");
            sb.AppendLine();
        }

        if (!root.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
            return sb.ToString();

        foreach (var cat in categories.EnumerateArray())
        {
            var id = cat.TryGetProperty("id", out var idP) ? idP.GetString()?.Trim() : null;
            var name = cat.TryGetProperty("name", out var nameP) ? nameP.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(name)) continue;

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                var filter = categoryFilter.Trim();
                var match = string.Equals(id, filter, StringComparison.OrdinalIgnoreCase) ||
                            (name != null && name.Contains(filter, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;
            }

            sb.AppendLine($"## {name}");
            if (cat.TryGetProperty("description", out var d)) sb.AppendLine(d.GetString() ?? "").AppendLine();

            if (cat.TryGetProperty("equipmentTypes", out var et) && et.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Equipment types**");
                foreach (var item in et.EnumerateArray())
                    sb.Append("- ").AppendLine(item.GetString() ?? "");
                sb.AppendLine();
            }

            if (cat.TryGetProperty("eventRecommendations", out var er) && er.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Event recommendations**");
                foreach (var rec in er.EnumerateArray())
                {
                    var ev = rec.TryGetProperty("eventType", out var ep) ? ep.GetString() : null;
                    var recText = rec.TryGetProperty("recommendation", out var rp) ? rp.GetString() : null;
                    sb.AppendLine($"- **{ev}**: {recText}");
                }
                sb.AppendLine();
            }

            if (cat.TryGetProperty("integrationAndScalability", out var ias))
            { sb.AppendLine("**Integration & scalability**"); sb.AppendLine(ias.GetString() ?? ""); sb.AppendLine(); }
            if (cat.TryGetProperty("operationalNotes", out var on))
            { sb.AppendLine("**Operational notes**"); sb.AppendLine(on.GetString() ?? ""); sb.AppendLine(); }
            var hasOperationMode = cat.TryGetProperty("operationMode", out var operationModeProp);
            var hasOperationGuidance = cat.TryGetProperty("operationGuidance", out var operationGuidanceProp);
            if (hasOperationMode || hasOperationGuidance)
            {
                sb.AppendLine("**Operator requirement**");
                if (hasOperationMode)
                {
                    var mode = operationModeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(mode))
                        sb.AppendLine("- Mode: " + FormatOperationModeLabel(mode));
                }

                if (hasOperationGuidance)
                {
                    var guidance = operationGuidanceProp.GetString();
                    if (!string.IsNullOrWhiteSpace(guidance))
                        sb.AppendLine("- Guidance: " + guidance);
                }

                sb.AppendLine();
            }
            if (cat.TryGetProperty("externalSupport", out var es))
            { sb.AppendLine("**External support**"); sb.AppendLine(es.GetString() ?? ""); sb.AppendLine(); }

            if (cat.TryGetProperty("availabilitySeasons", out var av) && av.ValueKind == JsonValueKind.Object)
            {
                sb.AppendLine("**Availability & seasons**");
                foreach (var prop in av.EnumerateObject())
                    sb.AppendLine($"- {prop.Name}: {prop.Value.GetString() ?? ""}");
                sb.AppendLine();
            }

            if (cat.TryGetProperty("warehouseStock", out var ws) && ws.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Warehouse stock**");
                foreach (var wh in ws.EnumerateArray())
                {
                    var whName = wh.TryGetProperty("warehouseName", out var wn) ? wn.GetString() : null;
                    sb.AppendLine($"- **{whName}**");
                    if (wh.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                        foreach (var it in items.EnumerateArray())
                        {
                            var iname = it.TryGetProperty("name", out var n) ? n.GetString() : null;
                            var qty = it.TryGetProperty("quantity", out var q) ? q.GetInt32() : (int?)null;
                            sb.AppendLine($"  - {iname}" + (qty.HasValue ? $": {qty}" : ""));
                        }
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
