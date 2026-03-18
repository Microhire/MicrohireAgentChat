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
    #region Alternatives Gallery Tool Handlers

    private async Task<Dictionary<string, string>> BuildAlternativesGalleriesAsync(
        IEnumerable<RecommendedEquipmentItem> recommendations,
        CancellationToken ct)
    {
        var galleries = new Dictionary<string, string>();
        var processedSearchKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var item in recommendations)
        {
            try
            {
                // Get category key (may be empty)
                var categoryKey = (item.Category ?? "").Trim().ToUpperInvariant();
                
                // Try to map category to search keyword
                var searchKeyword = !string.IsNullOrWhiteSpace(categoryKey) 
                    ? MapCategoryToSearchKeyword(categoryKey) 
                    : null;
                
                // FALLBACK: If category mapping fails, try to infer from description
                if (string.IsNullOrWhiteSpace(searchKeyword) && !string.IsNullOrWhiteSpace(item.Description))
                {
                    searchKeyword = InferSearchKeywordFromDescription(item.Description);
                    _logger.LogInformation("[GALLERY] Category '{Category}' not mapped, inferred '{SearchKeyword}' from description: {Description}", 
                        categoryKey, searchKeyword ?? "(none)", item.Description);
                }
                
                if (string.IsNullOrWhiteSpace(searchKeyword))
                {
                    _logger.LogDebug("[GALLERY] Skipping item - no search keyword for category '{Category}', description: {Description}", 
                        categoryKey, item.Description);
                    continue;
                }
                
                // Skip if we already built a gallery for this search keyword
                if (processedSearchKeywords.Contains(searchKeyword))
                    continue;
                processedSearchKeywords.Add(searchKeyword);
                
                _logger.LogInformation("[GALLERY] Searching for alternatives: category='{Category}', keyword='{SearchKeyword}'", 
                    categoryKey, searchKeyword);
                
                // Search for alternatives in this category
                var searchResult = await _equipmentSearch.SearchEquipmentAsync(searchKeyword, 6, ct);
                
                _logger.LogInformation("[GALLERY] Search returned {Count} items for keyword '{SearchKeyword}'", 
                    searchResult.Items.Count, searchKeyword);
                
                if (searchResult.Items.Count <= 1)
                    continue; // No alternatives to show
                
                // Build equipment items for the gallery (description only, no price)
                var equipmentItems = searchResult.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Description))
                    .Select(i => new IslaBlocks.EquipmentItem(
                        i.ProductCode,
                        i.Description,
                        i.Category,
                        !string.IsNullOrWhiteSpace(i.PictureFileName)
                            ? ToAbsoluteUrl($"/images/products/{i.PictureFileName}")
                            : null
                    ))
                    .Take(6)
                    .ToList();
                
                if (equipmentItems.Count > 0)
                {
                    // Use the search keyword as gallery key to avoid duplicates
                    var galleryKey = !string.IsNullOrWhiteSpace(categoryKey) ? categoryKey : searchKeyword.ToUpperInvariant();
                    var title = GetAlternativesTitle(galleryKey);
                    var galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(equipmentItems, title, max: 6);
                    galleries[galleryKey] = galleryHtml;
                    
                    _logger.LogInformation("[GALLERY] Built alternatives gallery for '{GalleryKey}': {Count} items", galleryKey, equipmentItems.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GALLERY] Failed to build alternatives gallery for item: {Description}", item.Description);
            }
        }
        
        _logger.LogInformation("[GALLERY] Total galleries built: {Count}", galleries.Count);
        return galleries;
    }

    /// <summary>
    /// Infers a search keyword from the equipment description when category mapping fails.
    /// </summary>
    private static string? InferSearchKeywordFromDescription(string description)
    {
        var desc = description.ToLowerInvariant();
        
        // Laptops
        if (desc.Contains("laptop") || desc.Contains("notebook") || desc.Contains("dell") || desc.Contains("hp ") || desc.Contains("lenovo"))
            return "laptop";
        
        // MacBooks
        if (desc.Contains("macbook") || desc.Contains("mac book") || desc.Contains("apple mac"))
            return "mac laptop";
        
        // Projectors
        if (desc.Contains("projector") || desc.Contains("epson") || desc.Contains("panasonic") || desc.Contains("barco"))
            return "projector";
        
        // Screens
        if (desc.Contains("screen") || desc.Contains("fastfold") || desc.Contains("stumpfl") || desc.Contains("tripod screen"))
            return "screen";
        
        // Microphones
        if (desc.Contains("microphone") || desc.Contains("mic ") || desc.Contains(" mic") || desc.Contains("wireless mic") || desc.Contains("shure") || desc.Contains("sennheiser"))
            return "microphone";
        
        // Speakers
        if (desc.Contains("speaker") || desc.Contains("pa system") || desc.Contains("jbl") || desc.Contains("qsc") || desc.Contains("bose"))
            return "speaker";
        
        // Cameras
        if (desc.Contains("camera") || desc.Contains("webcam") || desc.Contains("ptz"))
            return "camera";
        
        // Displays/Monitors
        if (desc.Contains("display") || desc.Contains("monitor") || desc.Contains("tv") || desc.Contains("led screen") || desc.Contains("lcd"))
            return "display";
        
        // Lecterns
        if (desc.Contains("lectern") || desc.Contains("podium"))
            return "lectern";
        
        // Clickers / presentation remotes
        if (desc.Contains("clicker") || desc.Contains("presentation remote") || (desc.Contains("wireless") && desc.Contains("presenter") && !desc.Contains("mic")))
            return "clicker";
        
        return null;
    }

    /// <summary>
    /// Maps database category codes to user-friendly search keywords.
    /// Includes main categories, subcategories, and common variations.
    /// </summary>
    private static string? MapCategoryToSearchKeyword(string category)
    {
        return category.ToUpperInvariant().Trim() switch
        {
            // Projectors
            "PROJECTR" => "projector",
            "PROJECTOR" => "projector",
            "EPSON" => "projector",
            "PANASONIC" => "projector",
            "BARCO" => "projector",
            "NEC" => "projector",
            
            // Screens
            "SCREEN" => "screen",
            "SCREENS" => "screen",
            "FASTFOLD" => "screen",
            "STUMPFL" => "screen",
            
            // Microphones
            "W/MIC" => "wireless microphone",
            "MIC" => "microphone",
            "MICROPHONE" => "microphone",
            "SHURE" => "microphone",
            "SENNHEISER" => "microphone",
            "AUDIO" => "microphone",
            
            // Speakers
            "SPEAKER" => "speaker",
            "SPEAKERS" => "speaker",
            "PWRD SPKR" => "speaker",
            "PA" => "speaker",
            "JBL" => "speaker",
            "QSC" => "speaker",
            "BOSE" => "speaker",
            
            // Cameras
            "CAMERA" => "camera",
            "CAMERAS" => "camera",
            "VIDEO" => "camera",
            "WEBCAM" => "camera",
            "PTZ" => "camera",
            
            // Laptops - including package subcategories
            "LAPTOP" => "laptop",
            "LAPTOPS" => "laptop",
            "LAPPACK" => "laptop",
            "PC" => "laptop",
            "DELL" => "laptop",
            "HP" => "laptop",
            "LENOVO" => "laptop",
            
            // MacBooks - including package subcategories
            "MACBOOK" => "mac laptop",
            "MAC" => "mac laptop",
            "MBPPACK" => "mac laptop",
            "APPLE" => "mac laptop",
            
            // Lecterns
            "LECTERN" => "lectern",
            "PODIUM" => "lectern",
            
            // Clickers / presentation remotes
            "CLICKER" => "clicker",
            "WIRPRES" => "clicker",
            "PRESENTER" => "clicker",
            
            // Displays/Monitors
            "DISPLAY" => "display",
            "DISPLAYS" => "display",
            "MONITOR" => "monitor",
            "MONITORS" => "monitor",
            "TV" => "display",
            "LED" => "display",
            "LCD" => "display",
            
            // Lighting
            "LIGHTING" => "lighting",
            "LIGHT" => "lighting",
            "LED LIGHT" => "lighting",
            
            // Staging
            "STAGING" => "staging",
            "STAGE" => "staging",
            "RISER" => "staging",
            
            _ => null
        };
    }

    /// <summary>
    /// Gets a user-friendly title for the alternatives gallery.
    /// Matches the expanded category mappings.
    /// </summary>
    private static string GetAlternativesTitle(string category)
    {
        return category.ToUpperInvariant().Trim() switch
        {
            // Projectors
            "PROJECTR" or "PROJECTOR" or "EPSON" or "PANASONIC" or "BARCO" or "NEC" 
                => "Alternative Projectors",
            
            // Screens
            "SCREEN" or "SCREENS" or "FASTFOLD" or "STUMPFL" 
                => "Alternative Screens",
            
            // Microphones
            "W/MIC" or "MIC" or "MICROPHONE" or "SHURE" or "SENNHEISER" or "AUDIO" 
                => "Alternative Microphones",
            
            // Speakers
            "SPEAKER" or "SPEAKERS" or "PWRD SPKR" or "PA" or "JBL" or "QSC" or "BOSE" 
                => "Alternative Speakers",
            
            // Cameras
            "CAMERA" or "CAMERAS" or "VIDEO" or "WEBCAM" or "PTZ" 
                => "Alternative Cameras",
            
            // Laptops
            "LAPTOP" or "LAPTOPS" or "LAPPACK" or "PC" or "DELL" or "HP" or "LENOVO" 
                => "Alternative Laptops",
            
            // MacBooks
            "MACBOOK" or "MAC" or "MBPPACK" or "APPLE" 
                => "Alternative MacBooks",
            
            // Lecterns
            "LECTERN" or "PODIUM" 
                => "Alternative Lecterns",
            
            // Clickers / presentation remotes
            "CLICKER" or "WIRPRES" or "PRESENTER" 
                => "Alternative Presentation Remotes",
            
            // Displays/Monitors
            "DISPLAY" or "DISPLAYS" or "MONITOR" or "MONITORS" or "TV" or "LED" or "LCD" 
                => "Alternative Displays",
            
            // Lighting
            "LIGHTING" or "LIGHT" or "LED LIGHT" 
                => "Alternative Lighting",
            
            // Staging
            "STAGING" or "STAGE" or "RISER" 
                => "Alternative Staging",
            
            _ => "Alternatives"
        };
    }

    public async Task<string> HandleShowEquipmentAlternativesAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        
        string equipmentType = "";
        string? excludeCode = null;
        int maxResults = 8;
        
        if (doc.RootElement.TryGetProperty("equipment_type", out var etProp))
            equipmentType = etProp.GetString() ?? "";
        if (doc.RootElement.TryGetProperty("exclude_product_code", out var excProp))
            excludeCode = excProp.GetString();
        if (doc.RootElement.TryGetProperty("max_results", out var mrProp) && mrProp.ValueKind == JsonValueKind.Number)
            maxResults = Math.Clamp(mrProp.GetInt32(), 1, 12);
        
        if (string.IsNullOrWhiteSpace(equipmentType))
        {
            return JsonSerializer.Serialize(new { 
                error = "equipment_type is required",
                instruction = "Ask the user what type of equipment they want alternatives for (e.g., screens, projectors, microphones, cameras, lecterns)"
            });
        }
        
        _logger.LogInformation("Showing equipment alternatives for type: {Type}, excluding: {Exclude}", equipmentType, excludeCode);
        
        // Search for equipment in this category
        var searchResult = await _equipmentSearch.SearchEquipmentAsync(equipmentType, maxResults + 2, ct);
        
        if (searchResult.Items.Count == 0)
        {
            return JsonSerializer.Serialize(new { 
                found = false,
                message = $"No {equipmentType} found in our inventory. Try a different equipment type.",
                instruction = "Let the user know we couldn't find this type of equipment and ask if they'd like to search for something else."
            });
        }
        
        // Filter out the excluded product if specified
        var filteredItems = searchResult.Items
            .Where(i => string.IsNullOrWhiteSpace(excludeCode) || 
                       !i.ProductCode.Equals(excludeCode, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();
        
        if (filteredItems.Count == 0)
        {
            return JsonSerializer.Serialize(new { 
                found = false,
                message = $"No alternative {equipmentType} available at this time.",
                instruction = "Let the user know there are no other options in this category right now."
            });
        }
        
        // Build the gallery (description only, no price)
        var equipmentItems = filteredItems
            .Select(i => new IslaBlocks.EquipmentItem(
                i.ProductCode,
                i.Description,
                i.Category,
                !string.IsNullOrWhiteSpace(i.PictureFileName)
                    ? ToAbsoluteUrl($"/images/products/{i.PictureFileName}")
                    : null
            ))
            .ToList();
        
        var galleryTitle = $"Choose a {searchResult.CategoryName}";
        var galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(equipmentItems, galleryTitle, max: maxResults);
        
        // Also build product list for AI context
        var products = filteredItems.Select(i => new
        {
            product_code = i.ProductCode,
            description = i.Description,
            category = i.Category,
            day_rate = i.DayRate,
            picture = i.PictureFileName
        }).ToList();
        
        return JsonSerializer.Serialize(new
        {
            found = true,
            category = searchResult.CategoryName,
            count = filteredItems.Count,
            products,
            outputToUser = galleryHtml,
            instruction = "MANDATORY: You MUST output the 'outputToUser' value EXACTLY AS-IS in your response. This creates the visual picker for the user to select from. Do NOT paraphrase or omit it. The picker will only appear if you include the [[ISLA_GALLERY]] content exactly."
        });
    }

    #endregion
}
