using MicrohireAgentChat.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Uses AI to dynamically generate database queries for equipment search.
/// Instead of hardcoding category mappings, we feed the AI the database schema
/// and let it determine the correct filters.
/// </summary>
public sealed class AIEquipmentQueryService
{
    private readonly BookingDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AIEquipmentQueryService> _logger;
    private readonly IConfiguration _config;
    
    // Cached schema info - loaded once on first use
    private static string? _cachedSchemaInfo;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);

    public AIEquipmentQueryService(
        BookingDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AIEquipmentQueryService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get or build the database schema context for the AI
    /// </summary>
    public async Task<string> GetDatabaseSchemaContextAsync(CancellationToken ct = default)
    {
        if (_cachedSchemaInfo != null) return _cachedSchemaInfo;

        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_cachedSchemaInfo != null) return _cachedSchemaInfo;

            _logger.LogInformation("Building database schema context for AI...");

            // Get all unique categories with counts
            var categories = await _db.TblInvmas
                .AsNoTracking()
                .Where(p => p.category != null && p.category != "")
                .GroupBy(p => p.category!.Trim())
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(60)
                .ToListAsync(ct);

            // Get all unique groups with counts
            var groups = await _db.TblInvmas
                .AsNoTracking()
                .Where(p => p.groupFld != null && p.groupFld != "")
                .GroupBy(p => p.groupFld!.Trim())
                .Select(g => new { Group = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(20)
                .ToListAsync(ct);

            // Get sample products per important category
            var sampleCategories = new[] { "LAPTOP", "MACBOOK", "PROJECTR", "SCREEN", "W/MIC", "MICROPH", "SPEAKER", "LCD-AV", "CAMERAS", "LED", "MIXER" };
            var samples = new Dictionary<string, List<string>>();

            foreach (var cat in sampleCategories)
            {
                var prods = await _db.TblInvmas
                    .AsNoTracking()
                    .Where(p => (p.category ?? "").Trim() == cat)
                    .OrderBy(p => p.product_code)
                    .Take(5)
                    .Select(p => $"{p.product_code}: {p.descriptionv6 ?? p.PrintedDesc}")
                    .ToListAsync(ct);
                
                if (prods.Count > 0)
                    samples[cat] = prods;
            }

            // Build the schema context
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## MICROHIRE EQUIPMENT DATABASE SCHEMA");
            sb.AppendLine();
            sb.AppendLine("### Available Categories (category column in tblInvmas):");
            foreach (var c in categories)
                sb.AppendLine($"- '{c.Category}' ({c.Count} items)");

            sb.AppendLine();
            sb.AppendLine("### Available Groups (groupFld column in tblInvmas):");
            foreach (var g in groups)
                sb.AppendLine($"- '{g.Group}' ({g.Count} items)");

            sb.AppendLine();
            sb.AppendLine("### Sample Products by Category:");
            foreach (var kv in samples)
            {
                sb.AppendLine($"\n**{kv.Key}:**");
                foreach (var p in kv.Value)
                    sb.AppendLine($"  - {p}");
            }

            sb.AppendLine();
            sb.AppendLine("### Key Mappings:");
            sb.AppendLine("- Laptops: category 'LAPTOP' (Windows) or 'MACBOOK' (Mac)");
            sb.AppendLine("- Projectors: category 'PROJECTR' (descriptions contain lumen ratings like '5000', '8000', '10K', '20K')");
            sb.AppendLine("- Screens: category 'SCREEN' (descriptions contain sizes like '10 Metre', '16x9', 'Fastfold', 'Stumpfl')");
            sb.AppendLine("- Wireless Microphones: category 'W/MIC' (handheld, lapel/lavalier, beltpack, headset)");
            sb.AppendLine("- Wired Microphones: category 'MICROPH'");
            sb.AppendLine("- Speakers: category 'SPEAKER'");
            sb.AppendLine("- Monitors/TVs: category 'LCD-AV'");
            sb.AppendLine("- Cameras: category 'CAMERAS'");
            sb.AppendLine("- Lighting: categories 'LED', 'LXMHEAD', 'LXFLOOD', etc. in group 'LIGHTING'");
            sb.AppendLine("- Audio Mixers: category 'MIXER'");

            _cachedSchemaInfo = sb.ToString();
            _logger.LogInformation("Database schema context built: {Length} chars", _cachedSchemaInfo.Length);

            return _cachedSchemaInfo;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    /// <summary>
    /// Ask AI to interpret a user requirement and return query parameters
    /// </summary>
    public async Task<EquipmentQueryParams?> InterpretRequirementAsync(
        string userRequirement,
        CancellationToken ct = default)
    {
        try
        {
            var schemaContext = await GetDatabaseSchemaContextAsync(ct);

            var prompt = $@"You are a database query assistant for Microhire's AV equipment inventory.

{schemaContext}

---

USER REQUIREMENT: ""{userRequirement}""

Based on the database schema above, determine the best query parameters to find this equipment.

RESPOND IN JSON FORMAT ONLY:
{{
  ""category"": ""EXACT_CATEGORY_NAME"",  // Must match exactly from the list above, e.g., ""SCREEN"", ""PROJECTR"", ""W/MIC""
  ""searchTerms"": [""term1"", ""term2""],  // Terms to search in description (e.g., ""lapel"", ""20k"", ""fastfold"")
  ""displayName"": ""Human readable name"",  // e.g., ""Large Projection Screens""
  ""confidence"": 0.9  // Your confidence level 0-1
}}

IMPORTANT:
- Use EXACT category names from the schema (e.g., ""W/MIC"" not ""Wireless Mic"")
- For ""big"" or ""large"" equipment, include size-related search terms
- For screens: sizes are in descriptions like ""10 Metre"", ""16x9"", ""20'"" etc.
- For projectors: brightness in descriptions like ""5000 Lumen"", ""10K"", ""20K""
- For microphones: ""lapel"", ""lavalier"", ""beltpack"" for clip-on; ""handheld"" for handheld

JSON ONLY, no other text:";

            // Use Azure OpenAI via HTTP
            var endpoint = _config["AzureOpenAI:Endpoint"];
            var apiKey = _config["AzureOpenAI:ApiKey"];
            var deployment = _config["AzureOpenAI:Deployment"] ?? "gpt-4o-mini";
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("AzureOpenAI not configured - cannot use AI query interpretation");
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 500
            };

            var httpResponse = await client.PostAsJsonAsync(
                $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview",
                requestBody,
                ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = await httpResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("Azure OpenAI API error: {Status} - {Error}", httpResponse.StatusCode, error);
                return null;
            }

            var responseJson = await httpResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var content = responseJson.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            
            _logger.LogInformation("AI interpreted '{Requirement}' → {Response}", userRequirement, content);

            // Parse JSON response
            // Strip markdown code blocks if present
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var lines = content.Split('\n');
                content = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.StartsWith("```")));
            }

            var result = JsonSerializer.Deserialize<EquipmentQueryParams>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to interpret requirement: {Requirement}", userRequirement);
            return null;
        }
    }

    /// <summary>
    /// Search equipment using AI-generated query parameters
    /// </summary>
    public async Task<List<EquipmentResult>> SearchWithAIAsync(
        string userRequirement,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        var queryParams = await InterpretRequirementAsync(userRequirement, ct);
        
        if (queryParams == null || string.IsNullOrEmpty(queryParams.Category))
        {
            _logger.LogWarning("AI could not interpret requirement: {Requirement}", userRequirement);
            return new List<EquipmentResult>();
        }

        _logger.LogInformation("AI query params: Category={Category}, Terms=[{Terms}], Confidence={Confidence}",
            queryParams.Category, 
            string.Join(", ", queryParams.SearchTerms ?? Array.Empty<string>()),
            queryParams.Confidence);

        // Build and execute query
        var query = _db.TblInvmas.AsNoTracking()
            .Where(p => (p.category ?? "").Trim() == queryParams.Category);

        // Apply search terms if provided
        if (queryParams.SearchTerms?.Length > 0)
        {
            var terms = queryParams.SearchTerms;
            query = query.Where(p =>
                terms.Any(t => (p.descriptionv6 ?? "").ToLower().Contains(t.ToLower())) ||
                terms.Any(t => (p.PrintedDesc ?? "").ToLower().Contains(t.ToLower())));
        }

        // Exclude internal/discontinued items
        query = query.Where(p =>
            !(p.descriptionv6 ?? "").ToLower().Contains("long term hire") &&
            !(p.descriptionv6 ?? "").ToLower().Contains("discontinued"));

        var products = await query
            .Take(100)
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.groupFld,
                p.PictureFileName,
                p.ProductTypeV41
            })
            .ToListAsync(ct);

        if (products.Count == 0)
        {
            _logger.LogWarning("No products found for AI query: {Category}", queryParams.Category);
            return new List<EquipmentResult>();
        }

        // Get pricing
        var productCodes = products.Select(p => (p.product_code ?? "").Trim()).ToList();
        var pricing = await _db.TblRatetbls
            .AsNoTracking()
            .Where(r => r.TableNo == 0 && productCodes.Contains((r.product_code ?? "").Trim()))
            .Select(r => new { Code = (r.product_code ?? "").Trim(), r.rate_1st_day })
            .ToListAsync(ct);

        var priceLookup = pricing.ToDictionary(p => p.Code, p => p.rate_1st_day ?? 0, StringComparer.OrdinalIgnoreCase);

        // Build results with pricing
        var results = products
            .Select(p =>
            {
                var code = (p.product_code ?? "").Trim();
                var price = priceLookup.TryGetValue(code, out var pr) ? pr : 0;

                return new EquipmentResult
                {
                    ProductCode = code,
                    Description = (p.descriptionv6 ?? p.PrintedDesc ?? "").Trim(),
                    Category = p.category,
                    Group = p.groupFld,
                    Picture = p.PictureFileName,
                    DayRate = price,
                    IsPackage = p.ProductTypeV41 == 1
                };
            })
            .Where(r => r.DayRate > 0)
            .OrderByDescending(r => r.DayRate)
            .Take(maxResults)
            .ToList();

        _logger.LogInformation("AI search found {Count} items for '{Requirement}' (category: {Category})",
            results.Count, userRequirement, queryParams.Category);

        return results;
    }

    /// <summary>
    /// Clear cached schema (call after DB changes)
    /// </summary>
    public static void ClearSchemaCache()
    {
        _cachedSchemaInfo = null;
    }
}

public class EquipmentQueryParams
{
    public string? Category { get; set; }
    public string[]? SearchTerms { get; set; }
    public string? DisplayName { get; set; }
    public double Confidence { get; set; }
}

public class EquipmentResult
{
    public string ProductCode { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Category { get; set; }
    public string? Group { get; set; }
    public string? Picture { get; set; }
    public double DayRate { get; set; }
    public bool IsPackage { get; set; }
}

