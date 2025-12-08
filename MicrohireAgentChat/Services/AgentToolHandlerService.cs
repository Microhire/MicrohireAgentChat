using Azure.AI.Agents.Persistent;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Handles all Azure Agent tool calls - extracted from AzureAgentChatService
/// </summary>
public sealed class AgentToolHandlerService
{
    private readonly BookingDbContext _db;
    private readonly IWestinRoomCatalog _roomCatalog;
    private readonly IBookingDraftStore? _drafts;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AgentToolHandlerService> _logger;
    private readonly EquipmentSearchService _equipmentSearch;

    public AgentToolHandlerService(
        BookingDbContext db,
        IWestinRoomCatalog roomCatalog,
        IBookingDraftStore? drafts,
        IHttpContextAccessor http,
        ILogger<AgentToolHandlerService> logger,
        EquipmentSearchService equipmentSearch)
    {
        _db = db;
        _roomCatalog = roomCatalog;
        _drafts = drafts;
        _http = http;
        _logger = logger;
        _equipmentSearch = equipmentSearch;
    }

    /// <summary>
    /// Process a single tool call and return the result
    /// </summary>
    public async Task<string> HandleToolCallAsync(
        string toolName,
        string argsJson,
        string threadId,
        CancellationToken ct)
    {
        try
        {
            return toolName switch
            {
                "check_date_availability" => await HandleCheckAvailabilityAsync(argsJson, threadId, ct),
                "get_now_aest" => HandleGetNowAest(),
                "list_westin_rooms" => await HandleListRoomsAsync(ct),
                "build_time_picker" => HandleBuildTimePicker(argsJson),
                "get_room_images" => await HandleGetRoomImagesAsync(argsJson, ct),
                "get_product_info" => await HandleGetProductInfoAsync(argsJson, ct),
                "get_product_images" => await HandleGetProductImagesAsync(argsJson, ct),
                "build_equipment_picker" => HandleBuildEquipmentPicker(argsJson),
                "search_equipment" => await HandleSearchEquipmentAsync(argsJson, ct),
                "get_equipment_recommendations" => await HandleGetEquipmentRecommendationsAsync(argsJson, ct),
                "get_package_details" => await HandleGetPackageDetailsAsync(argsJson, ct),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool call failed: {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> HandleCheckAvailabilityAsync(string argsJson, string threadId, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var map = new Dictionary<string, string>();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
                map[prop.Name] = prop.Value.GetString() ?? "";
        }

        // Merge from draft if available
        var draft = _drafts?.TryGet(threadId);
        if (draft != null)
        {
            if (!map.ContainsKey("startTime") && draft.Start is TimeSpan s)
                map["startTime"] = $"{(int)s.TotalHours:00}{s.Minutes:00}";
            if (!map.ContainsKey("endTime") && draft.End is TimeSpan e)
                map["endTime"] = $"{(int)e.TotalHours:00}{e.Minutes:00}";
            if (!map.ContainsKey("date") && draft.Date is DateOnly d)
                map["date"] = d.ToString("yyyy-MM-dd");
        }

        var mergedJson = JsonSerializer.Serialize(map);
        var args = JsonSerializer.Deserialize<CheckDateArgs>(mergedJson)
                   ?? throw new InvalidOperationException("check_date_availability: missing/invalid args");

        var result = await CheckAvailabilityInternalAsync(args, ct);
        return JsonSerializer.Serialize(result);
    }

    private string HandleGetNowAest()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        
        return JsonSerializer.Serialize(new
        {
            currentDateTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
            timezone = "AEST",
            dayOfWeek = now.DayOfWeek.ToString()
        });
    }

    private async Task<string> HandleListRoomsAsync(CancellationToken ct)
    {
        var rooms = (await _roomCatalog.GetRoomsAsync(ct))
            .Select(r => new
            {
                id = r.Id,
                name = r.Name,
                slug = r.Slug,
                level = r.Level,
                cover = ToAbsoluteUrl(r.Cover)
            });
        
        return JsonSerializer.Serialize(new { rooms });
    }

    private string HandleBuildTimePicker(string argsJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string title = doc.RootElement.TryGetProperty("title", out var t) 
            ? (t.GetString() ?? "Select start and end time") 
            : "Select start and end time";
        string? dateIso = doc.RootElement.TryGetProperty("date", out var d) ? d.GetString() : null;
        string? defStart = doc.RootElement.TryGetProperty("defaultStart", out var ds) ? ds.GetString() : "09:00";
        string? defEnd = doc.RootElement.TryGetProperty("defaultEnd", out var de) ? de.GetString() : "10:00";
        int stepMinutes = doc.RootElement.TryGetProperty("stepMinutes", out var sm) && sm.ValueKind == JsonValueKind.Number
                            ? sm.GetInt32() : 30;

        var payload = new
        {
            ui = new
            {
                type = "timepicker",
                title = title,
                date = dateIso,
                defaultStart = defStart,
                defaultEnd = defEnd,
                stepMinutes = stepMinutes
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task<string> HandleGetRoomImagesAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string roomKey = "";
        if (doc.RootElement.TryGetProperty("room", out var rProp) && rProp.ValueKind == JsonValueKind.String)
            roomKey = rProp.GetString() ?? "";

        var room = (await _roomCatalog.GetRoomsAsync(ct))
            .FirstOrDefault(r =>
                r.Name.Equals(roomKey, StringComparison.OrdinalIgnoreCase) ||
                r.Slug.Equals(roomKey, StringComparison.OrdinalIgnoreCase));

        if (room is null)
        {
            return JsonSerializer.Serialize(new { error = "room not found" });
        }

        var payload = new
        {
            room = room.Name,
            cover = ToAbsoluteUrl(room.Cover),
            layouts = room.Layouts.Select(l => new
            {
                type = l.Type,
                capacity = l.Capacity,
                image = ToAbsoluteUrl(l.Image)
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task<object> CheckAvailabilityInternalAsync(CheckDateArgs args, CancellationToken ct)
    {
        // TODO: Implement actual availability checking logic
        // This is placeholder - extract from AzureAgentChatService
        return new
        {
            Available = true,
            Message = "Room is available"
        };
    }

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

    #region Intelligent Equipment Search Tools

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

    /// <summary>
    /// Get detailed information about a package including all components
    /// </summary>
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

    private string ToAbsoluteUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return "";
        
        var request = _http.HttpContext?.Request;
        if (request == null) return relativePath;

        var scheme = request.Scheme;
        var host = request.Host.ToUriComponent();
        return $"{scheme}://{host}{relativePath}";
    }
}

// Supporting types
public class CheckDateArgs
{
    public string? Date { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Room { get; set; }
}

