using System.Text.Json;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
    /// <summary>
    /// Search for equipment with intelligent category mapping and pricing
    /// </summary>
    private async Task<string> HandleSearchEquipmentAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string keyword = "";
        int maxResults = 6;

        if (doc.RootElement.TryGetProperty("keyword", out var kwProp))
            keyword = kwProp.GetString() ?? "";
        if (doc.RootElement.TryGetProperty("max_results", out var mr) && mr.ValueKind == JsonValueKind.Number)
            maxResults = Math.Clamp(mr.GetInt32(), 1, 12);

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return JsonSerializer.Serialize(new { error = "keyword is required" });
        }

        var result = await _equipmentSearch.SearchEquipmentAsync(keyword, maxResults, ct);

        if (!string.IsNullOrEmpty(result.Error))
        {
            return JsonSerializer.Serialize(new { error = result.Error });
        }

        if (result.Items.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = $"No equipment found matching '{keyword}'. Try a different search term.",
                category = result.CategoryName
            });
        }

        // Build product list with pricing info
        var products = result.Items.Select(i => new
        {
            product_code = i.ProductCode,
            description = i.Description,
            category = i.Category,
            day_rate = i.DayRate,
            extra_day_rate = i.ExtraDayRate,
            is_package = i.IsPackage,
            stock_on_hand = i.StockOnHand,
            picture = i.PictureFileName,
            part_of_packages = i.PartOfPackages?.Select(p => new
            {
                package_code = p.PackageCode,
                package_description = p.PackageDescription,
                package_day_rate = p.DayRate
            }).ToList()
        }).ToList();

        // Build UI gallery for visual selection
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

        string? galleryHtml = null;
        if (equipmentItems.Count > 0)
        {
            galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(
                equipmentItems,
                $"Select a {result.CategoryName}",
                max: maxResults
            );
        }

        return JsonSerializer.Serialize(new
        {
            found = true,
            category = result.CategoryName,
            count = result.Items.Count,
            products,
            outputToUser = galleryHtml,
            instruction = "Present these options to the user. OUTPUT the 'outputToUser' HTML EXACTLY AS-IS for the visual picker to render. Let the user select their preferred option."
        });
    }

    /// <summary>
    /// Parse user requirements and get equipment recommendations
    /// </summary>
    private async Task<string> HandleGetEquipmentRecommendationsAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string requirementText = "";
        if (doc.RootElement.TryGetProperty("requirements", out var req))
            requirementText = req.GetString() ?? "";

        if (string.IsNullOrWhiteSpace(requirementText))
        {
            return JsonSerializer.Serialize(new { error = "requirements text is required" });
        }

        // Parse user requirements
        var parsedRequirements = _equipmentSearch.ParseUserRequirements(requirementText);

        if (parsedRequirements.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = "Could not identify specific equipment requirements. Please specify what equipment you need (e.g., '2 laptops, 1 projector, 2 wireless microphones').",
                parsed_items = Array.Empty<object>()
            });
        }

        // Get recommendations for each category
        var recommendations = await _equipmentSearch.GetRecommendationsAsync(parsedRequirements, ct);

        // Build response with recommendations
        var categoryRecommendations = recommendations.Categories.Select(c => new
        {
            category = c.CategoryName,
            requested_quantity = c.RequestedQuantity,
            original_request = c.OriginalRequest,
            not_found = c.NotFound,
            not_found_message = c.NotFoundMessage,
            top_recommendation = c.TopRecommendation == null ? null : new
            {
                product_code = c.TopRecommendation.ProductCode,
                description = c.TopRecommendation.Description,
                day_rate = c.TopRecommendation.DayRate,
                total_for_qty = c.TopRecommendation.DayRate * c.RequestedQuantity,
                is_package = c.TopRecommendation.IsPackage,
                picture = c.TopRecommendation.PictureFileName
            },
            alternative_options = c.AlternativeOptions.Select(a => new
            {
                product_code = a.ProductCode,
                description = a.Description,
                day_rate = a.DayRate,
                is_package = a.IsPackage
            }).ToList(),
            package_option = c.PackageOption == null ? null : new
            {
                package_code = c.PackageOption.PackageCode,
                package_description = c.PackageOption.PackageDescription,
                day_rate = c.PackageOption.DayRate,
                reason = c.PackageOption.ReasonToRecommend,
                components = c.PackageOption.Components.Select(comp => new
                {
                    product_code = comp.ProductCode,
                    description = comp.Description,
                    quantity = comp.Quantity
                }).ToList()
            }
        }).ToList();

        // Build summary message for the agent
        var summaryParts = new List<string>();
        foreach (var rec in recommendations.Categories.Where(c => !c.NotFound && c.TopRecommendation != null))
        {
            var item = rec.TopRecommendation!;
            summaryParts.Add($"- {rec.RequestedQuantity}x {item.Description} ({item.ProductCode}) at ${item.DayRate:F2}/day = ${item.DayRate * rec.RequestedQuantity:F2}");

            if (rec.PackageOption != null)
            {
                summaryParts.Add($"  💡 Package available: {rec.PackageOption.PackageDescription} at ${rec.PackageOption.DayRate:F2}/day includes accessories");
            }
        }

        var responseMessage = summaryParts.Count > 0
            ? $"Based on your requirements, here are my recommendations:\n\n{string.Join("\n", summaryParts)}\n\nEstimated daily total: ${recommendations.EstimatedDayTotal:F2}"
            : "I couldn't find matching equipment for your requirements. Please specify the equipment types you need.";

        return JsonSerializer.Serialize(new
        {
            found = summaryParts.Count > 0,
            parsed_requirements = parsedRequirements.Select(r => new
            {
                original = r.OriginalText,
                normalized = r.NormalizedType,
                quantity = r.Quantity
            }).ToList(),
            recommendations = categoryRecommendations,
            estimated_daily_total = recommendations.EstimatedDayTotal,
            summary_message = responseMessage,
            instruction = "Present these recommendations to the user. Ask them to confirm if they want these items or if they'd like to see alternatives. If a package option is available, mention it as a value-add option."
        });
    }
}
