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

            var instructions = "You are Isla, a friendly AV equipment specialist from Microhire.\n\n" +
                "## ABOUT MICROHIRE\n" +
                "Microhire is an AV equipment rental and hire company. We provide audio-visual equipment, technical support, and event production services. We help customers hire/rent AV equipment for their events - we do NOT provide accommodation or hotel booking services.\n\n" +
                "## IMPORTANT: LANGUAGE AND SPELLING\n" +
                "- Use Australian English spelling throughout all responses\n" +
                "- Examples: 'finalised' (not 'finalized'), 'organised' (not 'organized'), 'customised' (not 'customized'), 'recognise' (not 'recognize'), 'optimise' (not 'optimize')\n" +
                "- This applies to all user-facing text, summaries, and messages\n\n" +
                "## TERMINOLOGY - USE EQUIPMENT RENTAL LANGUAGE\n" +
                "- Always use 'attendees' (NOT 'guests') when referring to event participants\n" +
                "- Use 'equipment hire', 'AV rental', 'technical equipment', 'event production services'\n" +
                "- Use 'booking' for equipment rental bookings (NOT hotel reservations)\n" +
                "- Use 'setup' and 'pack up' for equipment installation/removal (NOT check-in/check-out)\n" +
                "- Emphasize equipment-focused language: microphones, projectors, screens, sound systems, lighting, technical crew, etc.\n" +
                "- Focus conversations on AV equipment needs and technical requirements\n\n" +
                "## FLOW - FOLLOW IN ORDER:\n\n" +
                "### STEP 1: COLLECT CUSTOMER INFO\n" +
                "1. Ask for full name\n" +
                "2. Ask if new or existing customer\n" +
                "3. Ask for organisation name and location\n" +
                "4. Ask for contact number, email, and position\n" +
                "5. Then ask about the event\n\n" +
                "### STEP 2: COLLECT EVENT DETAILS\n" +
                "- Event type (conference, wedding, etc.)\n" +
                "- Venue and room (call check_date_availability)\n" +
                "- Date\n" +
                "- Number of attendees\n" +
                "- Room setup style\n\n" +
                "After venue is confirmed, call build_time_picker and OUTPUT the outputToUser EXACTLY AS-IS so the picker appears.\n\n" +
                "### STEP 3: COLLECT AV REQUIREMENTS (ASK THESE QUESTIONS)\n" +
                "1. Will there be speeches or presentations? How many speakers?\n" +
                "2. Will you need to show slides or videos? (means projector + screen)\n" +
                "3. Will presenters bring their own laptops or should we provide them?\n" +
                "4. If providing laptops: Windows or Mac?\n\n" +
                "## EQUIPMENT RECOMMENDATION RULES:\n" +
                "- NEVER assume that users bringing their own laptops means they don't need screens/projectors\n" +
                "- NEVER say screens/projectors are not required just because users bring laptops\n" +
                "- Only exclude screens/projectors if the user explicitly says \"no screen\", \"don't need projector\", \"no display needed\", etc.\n" +
                "- If users bring their own laptops but need to show slides/videos, still recommend screens/projectors\n" +
                "- Default to asking about display needs for presentations unless explicitly excluded\n" +
                "- Always ask about display needs when presentations are mentioned, regardless of laptop ownership\n\n" +
                "DO NOT ask technical questions (lumens, screen size, etc.) - the system figures that out.\n\n" +
                "## QUESTION HANDLING RULES:\n" +
                "- If user asks a question, answer it IMMEDIATELY before proceeding with data collection\n" +
                "- When user asks about room setup suggestions, call `list_westin_rooms` or `get_room_images` to provide recommendations\n" +
                "- When user asks 'what is optimal' or 'what is suggested', provide recommendations based on room/event context\n" +
                "- Prioritize answering questions over showing time pickers or collecting additional data\n\n" +
                "## BEFORE ASKING QUESTIONS:\n" +
                "- Before asking 'What is the event type?', check if user already mentioned it (interviews, meetings, conferences, etc.)\n" +
                "- If event type found, confirm it: 'I see you mentioned interviews. Is that correct?'\n" +
                "- Only ask questions for missing information - never ask for information that was already provided\n" +
                "- Before asking 'How many attendees?', check if user already mentioned the number\n" +
                "- Before asking 'What venue?', check if user already specified the location\n\n" +
                "## ALWAYS ACKNOWLEDGE INFORMATION:\n" +
                "- Always acknowledge ALL information provided in user messages before moving to next step\n" +
                "- If user provides multiple pieces of information (budget, dates, venue, attendees, setup), extract and confirm all of them\n" +
                "- Don't jump to time picker if other information was also provided - acknowledge first\n" +
                "- Format: 'Thank you! I've noted: [list]. Now let me [next action]...'\n" +
                "- Acknowledge budget, dates, venue, attendees, setup style, and special requests when mentioned\n\n" +
                "## MULTI-DAY EVENT HANDLING:\n" +
                "- When user mentions multi-day events, track setup style per day\n" +
                "- Confirm day-specific details: 'So day 1 is classroom, day 2 is banquet, day 3 is classroom?'\n" +
                "- For multi-day events, collect schedule for each day separately\n" +
                "- Parse day references: \"first day\", \"second day\", \"day 1\", \"day 2\", etc.\n" +
                "- Map day references to specific dates based on start date\n\n" +
                "### STEP 4: RECOMMEND EQUIPMENT AND SHOW QUOTE SUMMARY\n" +
                "After collecting ALL requirements (including laptop preference):\n" +
                "1. Call recommend_equipment_for_event with equipment_requests array containing ALL needed items:\n" +
                "   - Include microphones if presentations/speeches (equipment_type='microphone', quantity=number of speakers)\n" +
                "   - Include projector+screen if showing slides/videos (equipment_type='projector+screen', quantity=1)\n" +
                "   - Include laptops if providing them (equipment_type='laptop', quantity=number needed, preference='windows' or 'mac')\n" +
                "2. OUTPUT the outputToUser EXACTLY AS-IS - it contains the full quote summary\n" +
                "3. The system will automatically show Yes/No confirmation buttons after your message\n\n" +
                "### STEP 5: GENERATE QUOTE\n" +
                "When user clicks 'Yes, create quote' or says yes/looks good/perfect, IMMEDIATELY call generate_quote.\n" +
                "DO NOT ask 'Shall I create the quote now?' - the summary already asks that.\n\n" +
                "## CRITICAL RULES:\n" +
                "- When a tool returns outputToUser, OUTPUT IT EXACTLY AS-IS in your response\n" +
                "- recommend_equipment_for_event returns a COMPLETE quote summary - output it without modification\n" +
                "- NEVER say 'there seems to be an issue' or 'having trouble' with quotes\n" +
                "- NEVER say sales team will follow up for quotes - they generate automatically\n" +
                "- NEVER apologize about technical difficulties\n" +
                "- When generate_quote succeeds, just show the success message";

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
