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
    #region Knowledge & Product Tool Handlers

    private async Task<string> HandleGetProductInfoAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        
        string? keyword = null;
        string? productCode = null;
        int take = 12;

        if (doc.RootElement.TryGetProperty("keyword", out var kwProp))
            keyword = kwProp.GetString();
        if (doc.RootElement.TryGetProperty("product_code", out var pc))
            productCode = pc.GetString();
        if (doc.RootElement.TryGetProperty("take", out var t) && t.ValueKind == JsonValueKind.Number)
            take = Math.Clamp(t.GetInt32(), 1, 50);

        // If looking up by specific product code, do direct lookup
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var product = await _db.TblInvmas.AsNoTracking()
                .Where(p => (p.product_code ?? "").Trim() == productCode.Trim())
                .Select(p => new
                {
                    product_code = p.product_code,
                    description = p.descriptionv6 ?? p.PrintedDesc,
                    printed_desc = p.PrintedDesc,
                    category = p.category,
                    group = p.groupFld,
                    picture = p.PictureFileName
                })
                .FirstOrDefaultAsync(ct);

            if (product != null)
            {
                return JsonSerializer.Serialize(new { 
                    products = new[] { product },
                    count = 1,
                    searchInfo = $"Found product: {product.description}"
                });
            }
            
            return JsonSerializer.Serialize(new { 
                products = Array.Empty<object>(), 
                count = 0,
                searchInfo = $"No product found with code '{productCode}'"
            });
        }

        // Use intelligent equipment search for keyword searches
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var result = await _equipmentSearch.SearchEquipmentAsync(keyword, take, ct);

            if (!string.IsNullOrEmpty(result.Error))
            {
                return JsonSerializer.Serialize(new { error = result.Error });
            }

            var products = result.Items.Select(i => new
            {
                product_code = i.ProductCode,
                description = i.Description,
                printed_desc = i.PrintedDescription,
                category = i.Category,
                group = i.Group,
                picture = i.PictureFileName,
                day_rate = i.DayRate,
                is_package = i.IsPackage,
                part_of_packages = i.PartOfPackages?.Select(p => new
                {
                    package_code = p.PackageCode,
                    package_description = p.PackageDescription,
                    package_rate = p.DayRate
                }).ToList()
            }).ToList();

            // Build gallery HTML for visual picker
            string? galleryHtml = null;
            if (result.Items.Count > 0)
            {
                var equipmentItems = result.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Description))
                    .Select(i => new IslaBlocks.EquipmentItem(
                        i.ProductCode,
                        $"{i.Description} - ${i.DayRate:F0}/day",
                        i.Category,
                        !string.IsNullOrWhiteSpace(i.PictureFileName) 
                            ? ToAbsoluteUrl($"/images/products/{i.PictureFileName}") 
                            : null
                    ))
                    .ToList();

                if (equipmentItems.Count > 0)
                {
                    galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(
                        equipmentItems, 
                        $"Select a {result.CategoryName}", 
                        max: take
                    );
                }
            }

            // If we have products with a gallery, tell the agent to output the gallery block
            if (!string.IsNullOrEmpty(galleryHtml))
            {
                return JsonSerializer.Serialize(new { 
                    products,
                    count = products.Count,
                    category = result.CategoryName,
                    searchInfo = $"Found {products.Count} {result.CategoryName.ToLower()} with pricing",
                    outputToUser = galleryHtml,
                    instruction = "OUTPUT THE 'outputToUser' VALUE EXACTLY AS-IS in your response. This creates a visual picker for the user with prices."
                });
            }

            return JsonSerializer.Serialize(new { 
                products, 
                count = products.Count,
                category = result.CategoryName,
                searchInfo = products.Count > 0 
                    ? $"Found {products.Count} items" 
                    : $"No items found for '{keyword}'"
            });
        }

        return JsonSerializer.Serialize(new { 
            products = Array.Empty<object>(), 
            count = 0,
            searchInfo = "No search criteria provided"
        });
    }

    private async Task<string> HandleGetProductImagesAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        
        string? productCode = null;
        if (doc.RootElement.TryGetProperty("product_code", out var pc))
            productCode = pc.GetString();

        if (string.IsNullOrWhiteSpace(productCode))
        {
            return JsonSerializer.Serialize(new { 
                ui = new { images = Array.Empty<object>() }, 
                error = "product_code is required" 
            });
        }

        var product = await _db.TblInvmas.AsNoTracking()
            .Where(x => x.product_code == productCode.Trim())
            .Select(x => new { 
                x.product_code, 
                x.descriptionv6, 
                x.PrintedDesc, 
                x.PictureFileName 
            })
            .FirstOrDefaultAsync(ct);

        if (product == null || string.IsNullOrWhiteSpace(product.PictureFileName))
        {
            return JsonSerializer.Serialize(new { ui = new { images = Array.Empty<object>() } });
        }

        var imageUrl = ToAbsoluteUrl($"/images/products/{product.PictureFileName}");
        var images = new[] { new { url = imageUrl, description = product.descriptionv6 } };

        return JsonSerializer.Serialize(new { ui = new { images } });
    }

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

    private static string FormatOperationModeLabel(string mode)
    {
        return mode.Trim().ToLowerInvariant() switch
        {
            "self_operated" => "Self-operated",
            "operator_recommended" => "Operator recommended",
            "operator_required" => "Operator required",
            _ => mode
        };
    }

    #endregion
}
