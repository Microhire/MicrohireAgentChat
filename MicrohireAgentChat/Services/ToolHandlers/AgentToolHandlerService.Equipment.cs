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
    #region Equipment Search & Picker Tool Handlers

    private string HandleBuildEquipmentPicker(string argsJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string title = doc.RootElement.TryGetProperty("title", out var t) 
            ? (t.GetString() ?? "Choose equipment") 
            : "Choose equipment";

        var equipmentItems = new List<IslaBlocks.EquipmentItem>();

        if (doc.RootElement.TryGetProperty("products", out var productsArray) && 
            productsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in productsArray.EnumerateArray())
            {
                var productCode = item.TryGetProperty("product_code", out var pc) ? pc.GetString() : null;
                var description = item.TryGetProperty("description", out var desc) ? desc.GetString() : 
                                  item.TryGetProperty("printed_desc", out var pd) ? pd.GetString() : null;
                var category = item.TryGetProperty("category", out var cat) ? cat.GetString() : null;
                var picture = item.TryGetProperty("picture", out var pic) ? pic.GetString() : null;

                if (!string.IsNullOrWhiteSpace(productCode) && !string.IsNullOrWhiteSpace(description))
                {
                    var imageUrl = !string.IsNullOrWhiteSpace(picture) 
                        ? ToAbsoluteUrl($"/images/products/{picture}") 
                        : null;
                    
                    equipmentItems.Add(new IslaBlocks.EquipmentItem(
                        productCode,
                        description,
                        category,
                        imageUrl
                    ));
                }
            }
        }

        if (equipmentItems.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "No valid products provided" });
        }

        // Build the gallery block
        var galleryBlock = IslaBlocks.BuildEquipmentGalleryBlock(equipmentItems, title, max: 12);

        return JsonSerializer.Serialize(new { 
            ui = new { 
                type = "equipment_gallery",
                galleryHtml = galleryBlock
            },
            message = $"Here are the available options for {title.ToLower()}. Please select one:"
        });
    }

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
            return JsonSerializer.Serialize(new { 
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

    private async Task<string> HandleGetPackageDetailsAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string packageCode = "";
        if (doc.RootElement.TryGetProperty("package_code", out var pc))
            packageCode = pc.GetString() ?? "";

        if (string.IsNullOrWhiteSpace(packageCode))
        {
            return JsonSerializer.Serialize(new { error = "package_code is required" });
        }

        // Get package info
        var package = await _db.TblInvmas.AsNoTracking()
            .Where(p => (p.product_code ?? "").Trim() == packageCode.Trim())
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.groupFld,
                p.ProductTypeV41,
                p.PictureFileName
            })
            .FirstOrDefaultAsync(ct);

        if (package == null)
        {
            return JsonSerializer.Serialize(new { error = $"Package '{packageCode}' not found" });
        }

        // Get pricing
        var pricing = await _db.TblRatetbls.AsNoTracking()
            .Where(r => (r.product_code ?? "").Trim() == packageCode.Trim() && r.TableNo == 0)
            .Select(r => new { r.rate_1st_day, r.rate_extra_days })
            .FirstOrDefaultAsync(ct);

        // Get components
        var components = await _equipmentSearch.GetPackageComponentsAsync(packageCode, ct);

        return JsonSerializer.Serialize(new
        {
            package_code = package.product_code?.Trim(),
            description = (package.descriptionv6 ?? package.PrintedDesc ?? "").Trim(),
            category = package.category,
            group = package.groupFld,
            is_package = package.ProductTypeV41 == 1,
            picture = package.PictureFileName,
            day_rate = pricing?.rate_1st_day ?? 0,
            extra_day_rate = pricing?.rate_extra_days ?? 0,
            components = components.Select(c => new
            {
                product_code = c.ProductCode,
                description = c.Description,
                quantity = c.Quantity,
                is_variable = c.IsVariable
            }).ToList(),
            component_count = components.Count,
            message = $"The {package.descriptionv6?.Trim()} package includes {components.Count} items"
        });
    }

    #endregion

    #region Equipment Context Validation

    /// <summary>
    /// Validates that equipment mentioned in conversation is included in the request.
    /// Returns a list of warnings for potentially missing equipment.
    /// </summary>
    private List<string> ValidateEquipmentContextFromConversation(string argsJson)
    {
        var warnings = new List<string>();
        
        try
        {
            // Get conversation from session (if available)
            var session = _http.HttpContext?.Session;
            var conversationText = session?.GetString("Draft:ConversationSummary") ?? "";
            
            // If no summary, try to get it from other session fields that might contain requirements
            if (string.IsNullOrWhiteSpace(conversationText))
            {
                var eventNotes = session?.GetString("Draft:EventNotes") ?? "";
                var avRequirements = session?.GetString("Draft:AVRequirements") ?? "";
                conversationText = $"{eventNotes} {avRequirements}";
            }
            
            // Parse the equipment requests from the args
            var requestedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(argsJson))
            {
                using var doc = JsonDocument.Parse(argsJson);
                if (doc.RootElement.TryGetProperty("equipment_requests", out var eqArray) && eqArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in eqArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("equipment_type", out var eqt))
                        {
                            var type = eqt.GetString()?.ToLowerInvariant() ?? "";
                            requestedTypes.Add(type);
                            // Also add related types
                            if (type.Contains("projector") || type.Contains("screen"))
                            {
                                requestedTypes.Add("projector");
                                requestedTypes.Add("screen");
                            }
                        }
                    }
                }
            }
            
            // Define keyword to equipment mappings
            var keywordMappings = new Dictionary<string[], string[]>(new ArrayComparer())
            {
                { new[] { "teams", "zoom", "video call", "video conference", "remote", "hybrid", "webcam" }, new[] { "camera", "microphone", "speaker", "display" } },
                { new[] { "presentation", "slides", "powerpoint", "present" }, new[] { "projector", "screen" } },
                { new[] { "video with sound", "play video", "audio playback", "music", "sound system" }, new[] { "speaker" } },
                { new[] { "speech", "presenter", "speaker at event", "speaking" }, new[] { "microphone" } },
                { new[] { "record", "recording", "film", "capture video" }, new[] { "camera" } }
            };
            
            // Check conversation for keywords
            var convLower = conversationText.ToLowerInvariant();
            foreach (var mapping in keywordMappings)
            {
                var keywords = mapping.Key;
                var requiredEquipment = mapping.Value;
                
                // Check if any keyword is mentioned
                var keywordFound = keywords.FirstOrDefault(k => convLower.Contains(k));
                if (keywordFound != null)
                {
                    // Check if all required equipment is in the request
                    foreach (var equipment in requiredEquipment)
                    {
                        if (!requestedTypes.Any(rt => rt.Contains(equipment) || equipment.Contains(rt)))
                        {
                            warnings.Add($"User mentioned '{keywordFound}' but '{equipment}' is not in the equipment request. Please confirm if this is needed.");
                        }
                    }
                }
            }
            
            if (warnings.Count > 0)
            {
                _logger.LogWarning("Context validation found potential missing equipment: {Warnings}", string.Join("; ", warnings));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating equipment context from conversation");
        }
        
        return warnings;
    }

    /// <summary>
    /// Custom comparer for string arrays in dictionary
    /// </summary>
    private class ArrayComparer : IEqualityComparer<string[]>
    {
        public bool Equals(string[]? x, string[]? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.SequenceEqual(y);
        }
        
        public int GetHashCode(string[] obj)
        {
            return obj.Aggregate(0, (a, b) => HashCode.Combine(a, b?.GetHashCode() ?? 0));
        }
    }

    #endregion
}
