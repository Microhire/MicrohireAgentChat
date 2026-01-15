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
        {
            _log.LogWarning("AzureAgent:AgentId is not configured - skipping tool installation");
            return;
        }

        try
        {
            var admin = _project.GetPersistentAgentsClient().Administration;
            var agent = admin.GetAgent(_opts.AgentId).Value;
            _log.LogInformation("Installing tools on agent {AgentId} ({Name})", agent.Id, agent.Name);

            var tools = new object[]
            {
                FunctionTool("get_now_aest", "Return the current date and time in Australia/Brisbane timezone.",
                    new { type = "object", properties = new { }, additionalProperties = false }),

                FunctionTool("check_date_availability", "Check venue availability by date",
                    new {
                        type = "object",
                        properties = new {
                            date = new { type = "string", format = "date", description = "YYYY-MM-DD" },
                            endDate = new { type = "string", format = "date", description = "YYYY-MM-DD (optional)" },
                            venueId = new { type = "integer", description = "Optional venue ID filter" },
                            room = new { type = "string", description = "Optional room name filter" }
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

                FunctionTool("recommend_equipment_for_event", 
                    "Smart equipment recommendation based on event context. Returns outputToUser with recommendations - output it EXACTLY.",
                    new {
                        type = "object",
                        properties = new {
                            event_type = new { type = "string", description = "Event type: conference, wedding, seminar, etc." },
                            expected_attendees = new { type = "integer", description = "Number of attendees" },
                            venue_name = new { type = "string", description = "Venue name" },
                            room_name = new { type = "string", description = "Room name" },
                            duration_days = new { type = "integer", description = "Duration in days (default 1)" },
                            equipment_requests = new { 
                                type = "array", 
                                items = new {
                                    type = "object",
                                    properties = new {
                                        equipment_type = new { type = "string" },
                                        quantity = new { type = "integer" },
                                        preference = new { type = "string" },
                                        microphone_type = new { type = "string" }
                                    }
                                }
                            }
                        },
                        required = new[] { "event_type", "expected_attendees", "equipment_requests" }
                    }),

                FunctionTool("search_equipment", "Search for equipment by keyword",
                    new {
                        type = "object",
                        properties = new {
                            keyword = new { type = "string", description = "Search keyword" },
                            maxResults = new { type = "integer", description = "Max results (default 10)" }
                        },
                        required = new[] { "keyword" }
                    }),

                FunctionTool("get_package_details", "Get package details including components",
                    new {
                        type = "object",
                        properties = new {
                            package_code = new { type = "string", description = "Package product code" }
                        },
                        required = new[] { "package_code" }
                    }),

                FunctionTool("get_product_info", "Search equipment database. Output the outputToUser EXACTLY.",
                    new {
                        type = "object",
                        properties = new {
                            product_code = new { type = "string" },
                            keyword = new { type = "string" },
                            take = new { type = "integer" }
                        },
                        additionalProperties = false
                    }),

                FunctionTool("build_equipment_picker", "Creates a visual picker for equipment selection",
                    new {
                        type = "object",
                        properties = new {
                            title = new { type = "string" },
                            products = new { type = "array", items = new { type = "object" } }
                        },
                        required = new[] { "title", "products" }
                    }),

                FunctionTool("get_product_images", "Get images for a product",
                    new {
                        type = "object",
                        properties = new { product_code = new { type = "string" } },
                        required = new[] { "product_code" }
                    }),

                FunctionTool("build_time_picker", 
                    "Display time picker widget. CRITICAL: Output the outputToUser value EXACTLY AS-IS in your response!",
                    new {
                        type = "object",
                        properties = new {
                            title = new { type = "string" },
                            date = new { type = "string", description = "Event date" },
                            defaultStart = new { type = "string" },
                            defaultEnd = new { type = "string" },
                            stepMinutes = new { type = "integer" }
                        },
                        additionalProperties = false
                    }),

                FunctionTool("generate_quote", 
                    "Generate quote. Call IMMEDIATELY when user confirms equipment - do NOT ask for additional confirmation!",
                    new {
                        type = "object",
                        properties = new { 
                            threadSnapshot = new { type = "object" }
                        },
                        required = Array.Empty<string>()
                    }),

                FunctionTool("update_quote", "Update quote with changes",
                    new {
                        type = "object",
                        properties = new { changes = new { type = "object" } },
                        required = new[] { "changes" }
                    }),

                FunctionTool("save_contact", "Save contact details",
                    new {
                        type = "object",
                        properties = new {
                            name = new { type = "string" },
                            email = new { type = "string", format = "email" },
                            phone = new { type = "string" }
                        },
                        required = new[] { "name", "email", "phone" }
                    }),

                FunctionTool("send_internal_followup", "Notify sales to follow up",
                    new { type = "object", properties = new { }, additionalProperties = false })
            };

            var instructions = @"You are Isla, a friendly AV equipment specialist from Microhire.

## FLOW - FOLLOW IN ORDER:

### STEP 1: COLLECT CUSTOMER INFO
1. Ask for full name
2. Ask if new or existing customer
3. Ask for organization name and location
4. Ask for contact number, email, and position
5. Then ask about the event

### STEP 2: COLLECT EVENT DETAILS
- Event type (conference, wedding, etc.)
- Venue and room (call check_date_availability)
- Date
- Number of attendees
- Room setup style

After venue is confirmed, call build_time_picker and OUTPUT the outputToUser EXACTLY AS-IS so the picker appears.

### STEP 3: COLLECT AV REQUIREMENTS (ASK THESE QUESTIONS)
1. Will there be speeches or presentations? How many speakers?
2. Will you need to show slides or videos? (means projector + screen)
3. Will presenters bring laptops or should we provide them?
4. If providing laptops: Windows or Mac?

DO NOT ask technical questions (lumens, screen size, etc.) - the system figures that out.

### STEP 4: RECOMMEND EQUIPMENT AND SHOW QUOTE SUMMARY
After collecting ALL requirements (including laptop preference):
1. Call recommend_equipment_for_event with equipment_requests array containing ALL needed items:
   - Include microphones if presentations/speeches (equipment_type='microphone', quantity=number of speakers)
   - Include projector+screen if showing slides/videos (equipment_type='projector+screen', quantity=1)
   - Include laptops if providing them (equipment_type='laptop', quantity=number needed, preference='windows' or 'mac')
2. OUTPUT the outputToUser EXACTLY AS-IS - it contains the full quote summary
3. The system will automatically show Yes/No confirmation buttons after your message

### STEP 5: GENERATE QUOTE
When user clicks 'Yes, create quote' or says yes/looks good/perfect, IMMEDIATELY call generate_quote.
DO NOT ask 'Shall I create the quote now?' - the summary already asks that.

## CRITICAL RULES:
- When a tool returns outputToUser, OUTPUT IT EXACTLY AS-IS in your response
- recommend_equipment_for_event returns a COMPLETE quote summary - output it without modification
- NEVER say 'there seems to be an issue' or 'having trouble' with quotes
- NEVER say sales team will follow up for quotes - they generate automatically
- NEVER apologize about technical difficulties
- When generate_quote succeeds, just show the success message";

            var body = new { tools, instructions };
            var content = RequestContent.Create(
                BinaryData.FromObjectAsJson(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            var response = admin.UpdateAgent(_opts.AgentId, content);

            int status = 0;
            try { status = response.Status; } catch { }

            string updatedId = _opts.AgentId;
            try { dynamic d = response; updatedId = d.Value.Id; } catch { }

            _log.LogInformation("Tools installed. Agent: {AgentId}. Status: {Status}", updatedId, status);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to install tools on agent. The app will continue but tools may not be updated.");
        }

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
