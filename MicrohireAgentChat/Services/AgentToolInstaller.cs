using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using MicrohireAgentChat.Config;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed class AgentToolInstaller : IHostedService
{
    private readonly AIProjectClient _project;
    private readonly ILogger<AgentToolInstaller> _log;
    private readonly AzureAgentOptions _opts;

    public AgentToolInstaller(
        AIProjectClient project,
        IOptions<AzureAgentOptions> opts,
        ILogger<AgentToolInstaller> log)
    {
        _project = project;
        _opts = opts.Value;
        _log = log;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_opts.AgentId))
            throw new InvalidOperationException("AzureAgent:AgentId is not configured.");

        var admin = _project.GetPersistentAgentsClient().Administration;

        // Make sure the agent exists (throws if not found)
        var agent = admin.GetAgent(_opts.AgentId).Value;
        _log.LogInformation("Installing tools on agent {AgentId} ({Name})", agent.Id, agent.Name);

        // Build function tools JSON payload:
        // { "tools": [ { "type":"function", "function":{ "name": "...", "description":"...", "parameters":{...} } }, ... ] }
        var tools = new object[]
        {            // NEW — returns current Brisbane time for guest-facing “today/current time” answers
            FunctionTool("get_now_aest", "Return the current date and time in Australia/Brisbane timezone (AEST/AEDT).",
                new { type = "object", properties = new { }, additionalProperties = false }),

            FunctionTool("check_date_availability", "Check Rental Point availability by date or range",
                new {
                    type = "object",
                    properties = new {
                        date    = new { type = "string", format = "date",  description = "YYYY-MM-DD" },
                        endDate = new { type = "string", format = "date",  description = "YYYY-MM-DD (optional)" },
                        venueId = new { type = "integer",                 description = "Optional venue ID filter" },
                        room    = new { type = "string",                  description = "Optional room name filter" }
                    },
                    required = new[] { "date" }
                }),

            FunctionTool("list_westin_rooms", "List rooms at The Westin Brisbane",
                new { type = "object", properties = new { }, additionalProperties = false }),

            FunctionTool("get_room_images", "Image URLs for the selected Westin room",
                new {
                    type = "object",
                    properties = new { room = new { type = "string" } },
                    required = new[] { "room" }
                }),

            FunctionTool("get_product_info", "Product details from DB",
                new {
                    type = "object",
                    properties = new {
                        product_code = new { type = "string" },
                        keyword      = new { type = "string" }
                    },
                    additionalProperties = false
                }),

            FunctionTool("get_product_images", "Images for a product",
                new {
                    type = "object",
                    properties = new { product_code = new { type = "string" } },
                    required = new[] { "product_code" }
                }),

            FunctionTool("generate_quote", "Create a quote from details gathered so far; returns UI payload",
                new {
                    type = "object",
                    properties = new { threadSnapshot = new { type = "object", description = "Key details captured so far" } },
                    required = new[] { "threadSnapshot" }
                }),

            FunctionTool("update_quote", "Overwrite selections, mark LIGHT PENCIL, regenerate quote",
                new {
                    type = "object",
                    properties = new { changes = new { type = "object" } },
                    required = new[] { "changes" }
                }),

            FunctionTool("save_contact", "Save contact details",
                new {
                    type = "object",
                    properties = new {
                        name  = new { type = "string" },
                        email = new { type = "string", format = "email" },
                        phone = new { type = "string" }
                    },
                    required = new[] { "name","email","phone" }
                }),

            FunctionTool("send_internal_followup", "Notify sales to follow up",
                new { type = "object", properties = new { }, additionalProperties = false }),

            // NEW: Inventory/Equipment tools
            FunctionTool("get_laptops", "Get available laptop inventory (Windows or Mac)",
                new {
                    type = "object",
                    properties = new {
                        type = new { type = "string", description = "Laptop type to retrieve (windows, mac, or all)" }
                    },
                    additionalProperties = false
                }),

            FunctionTool("search_equipment", "Search for equipment/products by name or description",
                new {
                    type = "object",
                    properties = new {
                        query = new { type = "string", description = "Search term (e.g., 'microphone', 'projector', 'laptop')" },
                        category = new { type = "string", description = "Optional category filter (e.g., 'LAPTOP', 'MACBOOK', 'NETWORK')" }
                    },
                    required = new[] { "query" }
                }),

            FunctionTool("get_equipment_by_category", "Get all equipment in a specific category",
                new {
                    type = "object",
                    properties = new {
                        category = new { type = "string", description = "Category code (e.g., 'LAPTOP', 'MACBOOK', 'IPAD', 'NETWORK', 'PRINTER')" }
                    },
                    required = new[] { "category" }
                }),

            FunctionTool("get_product_details", "Get detailed information about a specific product including pricing",
                new {
                    type = "object",
                    properties = new {
                        product_code = new { type = "string", description = "Product code to look up" }
                    },
                    required = new[] { "product_code" }
                }),

            FunctionTool("check_package_components", "Check if a product is a package and get its components",
                new {
                    type = "object",
                    properties = new {
                        product_code = new { type = "string", description = "Package product code" }
                    },
                    required = new[] { "product_code" }
                })
        };

        var body = new { tools };
        var content = RequestContent.Create(
            BinaryData.FromObjectAsJson(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Your SDK exposes: UpdateAgent(string agentId, RequestContent content)
        var response = admin.UpdateAgent(_opts.AgentId, content);

        // Log status; try to read Value.Id if present
        int status = 0;
        try { status = response.Status; } catch { /* ignore */ }

        string updatedId = _opts.AgentId;
        try { dynamic d = response; updatedId = d.Value.Id; } catch { /* ignore */ }

        _log.LogInformation("Tools installed. Agent: {AgentId}. Status: {Status}", updatedId, status);

        await Task.CompletedTask;
    }

    private static object FunctionTool(string name, string description, object parametersSchema) =>
        new
        {
            type = "function",
            function = new
            {
                name,
                description,
                parameters = parametersSchema
            }
        };
}
