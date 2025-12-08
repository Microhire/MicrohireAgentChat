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

            // ===== INTELLIGENT EQUIPMENT SEARCH TOOLS =====
            FunctionTool("get_equipment_recommendations", 
                "Search database for equipment based on user's SPECIFIC requirements. " +
                "**CRITICAL: BEFORE calling this tool, you MUST ask clarifying questions!** " +
                "DO NOT call this tool until you have gathered these details:\n\n" +
                "**LAPTOPS:**\n" +
                "- Windows or Mac?\n" +
                "- How many needed?\n" +
                "- Basic (presentations) or high-performance (video editing, 3D)?\n\n" +
                "**PROJECTORS:**\n" +
                "- Room size? (small <50 people, medium 50-150, large >150)\n" +
                "- Brightness needed? (standard 5000 lumens, bright room 8000+, large venue 10000+)\n" +
                "- Resolution? (HD is fine for most, 4K for detailed graphics)\n\n" +
                "**SCREENS:**\n" +
                "- Size needed? (small 2m, medium 3m, large 4m+, or recommend based on room)\n" +
                "- Front projection or rear projection?\n" +
                "- Freestanding, wall-mounted, or rigged/flown?\n\n" +
                "**MICROPHONES:**\n" +
                "- How many speakers/presenters?\n" +
                "- Handheld or lapel (lavalier)?\n" +
                "- Will they be moving around or stationary?\n\n" +
                "**AUDIO/SPEAKERS:**\n" +
                "- Room size?\n" +
                "- Just speech or also music/video playback?\n\n" +
                "**EXAMPLE FLOW:**\n" +
                "User: 'I need laptops'\n" +
                "Agent: 'Sure! Would you prefer Windows or Mac laptops? And how many do you need?'\n" +
                "User: 'Windows, 2 of them'\n" +
                "Agent: 'Got it! Are these for basic presentations or do you need high-performance machines for video editing?'\n" +
                "User: 'Just presentations'\n" +
                "Agent: [NOW call get_equipment_recommendations with '2 windows laptops for presentations']\n\n" +
                "After getting recommendations, show user the options with pricing and packages, then ask for confirmation.",
                new {
                    type = "object",
                    properties = new {
                        requirements = new { type = "string", description = "SPECIFIC requirements after asking clarifying questions, e.g. '2 windows laptops for presentations, 1 x 8000 lumen projector for medium room, 3m front projection screen'" }
                    },
                    required = new[] { "requirements" }
                }),

            FunctionTool("search_equipment", 
                "Search for specific equipment by keyword with filters. Use after gathering user requirements. " +
                "Returns matching products with pricing and package info. " +
                "Keywords: laptop, windows laptop, macbook, projector, screen, microphone, wireless mic, handheld mic, lapel mic, speaker, mixer, lighting",
                new {
                    type = "object",
                    properties = new {
                        keyword = new { type = "string", description = "Search keyword with specifics: 'windows laptop', '8000 lumen projector', 'wireless handheld mic', '3m screen'" },
                        maxResults = new { type = "integer", description = "Max results to return (default 10)" }
                    },
                    required = new[] { "keyword" }
                }),

            FunctionTool("get_package_details", 
                "Get detailed information about an equipment package including ALL components. " +
                "Use this when user selects a package or when recommending a package to show them exactly what's included. " +
                "Shows: package name, price, and complete list of all items included (laptop + mouse + case + cables, etc.).",
                new {
                    type = "object",
                    properties = new {
                        package_code = new { type = "string", description = "The product code of the package" }
                    },
                    required = new[] { "package_code" }
                }),

            FunctionTool("get_product_info", 
                "Search equipment database. Returns ACTUAL products with a visual picker gallery. " +
                "**BEFORE SEARCHING - ASK CLARIFYING QUESTIONS:**\n" +
                "- Laptop: 'Windows or Mac? For presentations or high-performance work?'\n" +
                "- Projector: 'What room size? How bright does it need to be?'\n" +
                "- Screen: 'What size? Front or rear projection?'\n" +
                "- Microphone: 'Handheld or lapel? How many presenters?'\n\n" +
                "KEYWORDS: laptop, windows laptop, macbook, projector, screen, microphone, wireless mic, handheld mic, lapel mic, speaker, mixer, lighting " +
                "**CRITICAL - ONE CATEGORY AT A TIME**: When user needs multiple equipment types, " +
                "gather requirements for ONE category, search and show options, get selection, then move to next category. " +
                "**CRITICAL**: The response includes 'outputToUser' - you MUST copy this EXACTLY into your response! " +
                "The outputToUser contains a gallery picker that lets users click to select equipment.",
                new {
                    type = "object",
                    properties = new {
                        product_code = new { type = "string", description = "Exact product code if known" },
                        keyword      = new { type = "string", description = "Specific search keyword after gathering requirements: 'windows laptop', '8000 lumen projector', 'wireless handheld mic'" },
                        take         = new { type = "integer", description = "Max results (default 12)" }
                    },
                    additionalProperties = false
                }),

            FunctionTool("build_equipment_picker", 
                "**MANDATORY** - Call this IMMEDIATELY after get_product_info! " +
                "Creates a visual picker showing actual product names like 'Panasonic PT-DZ6710E HD Projector' or 'Dell G15 Production Laptop'. " +
                "Users click to select. NEVER describe products in text - ALWAYS use this picker to show them visually. " +
                "For laptops: First ask 'Windows or Mac?' then search and show picker. " +
                "For other equipment: Search and show picker directly. " +
                "**ONE CATEGORY PER MESSAGE**: Only build a picker for ONE equipment type at a time. " +
                "Wait for user to select from current picker before showing the next category.",
                new {
                    type = "object",
                    properties = new {
                        title = new { type = "string", description = "e.g. 'Select a Projector', 'Choose a Laptop'" },
                        products = new { 
                            type = "array", 
                            description = "Products from get_product_info - REQUIRED",
                            items = new {
                                type = "object",
                                properties = new {
                                    product_code = new { type = "string" },
                                    description = new { type = "string" },
                                    category = new { type = "string" },
                                    picture = new { type = "string" }
                                }
                            }
                        }
                    },
                    required = new[] { "title", "products" }
                }),

            FunctionTool("get_product_images", 
                "Get images for a specific product to show the user what it looks like. Use this after searching for products to display equipment images.",
                new {
                    type = "object",
                    properties = new { product_code = new { type = "string", description = "The product code to get images for" } },
                    required = new[] { "product_code" }
                }),

            FunctionTool("build_time_picker", 
                "Display a time picker widget for the user to select start and end times for their event. Use this when you need to collect event timing information.",
                new {
                    type = "object",
                    properties = new {
                        title = new { type = "string", description = "Title for the time picker dialog" },
                        date = new { type = "string", format = "date", description = "The date in YYYY-MM-DD format" },
                        defaultStart = new { type = "string", description = "Default start time in HH:MM format (e.g. '09:00')" },
                        defaultEnd = new { type = "string", description = "Default end time in HH:MM format (e.g. '17:00')" },
                        stepMinutes = new { type = "integer", description = "Time step in minutes (default 30)" }
                    },
                    additionalProperties = false
                }),

            FunctionTool("generate_quote", 
                "Create a final quote PDF. " +
                "**ABSOLUTE REQUIREMENTS - VIOLATION WILL CAUSE ERRORS:** " +
                "1) BEFORE calling this tool, you MUST have gathered ALL equipment specifications:\n" +
                "   - Laptops: Windows/Mac, quantity, performance level\n" +
                "   - Projectors: Brightness (lumens), quantity\n" +
                "   - Screens: Size, projection type, mounting\n" +
                "   - Microphones: Type (handheld/lapel), quantity\n" +
                "   - Audio: Room requirements\n\n" +
                "2) BEFORE calling this tool, you MUST have:\n" +
                "   a) Asked clarifying questions for EACH equipment type\n" +
                "   b) Called get_equipment_recommendations with SPECIFIC requirements\n" +
                "   c) Shown user the equipment summary with pricing and packages\n" +
                "   d) Received user's explicit confirmation\n\n" +
                "3) The user must have confirmed the equipment selection BEFORE you ask about generating quote.\n\n" +
                "**MANDATORY FLOW:**\n" +
                "Step 1: User mentions equipment → Ask clarifying questions (Windows/Mac? Size? Brightness?)\n" +
                "Step 2: User answers → Call get_equipment_recommendations with specific details\n" +
                "Step 3: Show user the recommendations with prices, packages, and included components\n" +
                "Step 4: Ask 'Does this equipment selection look correct?'\n" +
                "Step 5: User confirms → Ask 'Is there anything else, or shall I prepare your quote?'\n" +
                "Step 6: User says 'yes to quote' → ONLY NOW call generate_quote\n\n" +
                "**WRONG:** 'need laptops' → generate_quote\n" +
                "**WRONG:** 'need laptops' → search without asking Windows/Mac → generate_quote\n" +
                "**RIGHT:** 'need laptops' → 'Windows or Mac?' → '2 Windows' → search → show options → confirm → quote",
                new {
                    type = "object",
                    properties = new { 
                        threadSnapshot = new { type = "object", description = "Key details captured including specific equipment specs" },
                        userConfirmedReady = new { type = "boolean", description = "REQUIRED: Must be TRUE. Only set to true if user EXPLICITLY confirmed they want the quote AFTER seeing equipment summary." },
                        equipmentConfirmed = new { type = "boolean", description = "REQUIRED: Must be TRUE. Only set to true if user confirmed the equipment recommendations." }
                    },
                    required = new[] { "threadSnapshot", "userConfirmedReady", "equipmentConfirmed" }
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
                new { type = "object", properties = new { }, additionalProperties = false })
        };

        // Agent instructions - comprehensive system prompt
        var instructions = @"You are Isla, a friendly and professional AV equipment specialist from Microhire. Your job is to help customers plan events and get quotes for audio-visual equipment.

## STEP 1: CUSTOMER INFORMATION (ALWAYS COLLECT FIRST)

When greeting a new customer, you MUST collect the following information IN ORDER before discussing events or equipment:

### 1. Full Name
Start with: 'Hello, my name is Isla from Microhire. What is your full name?'

### 2. New or Existing Customer
Ask: 'Nice to meet you, [Name]! Are you an existing customer of Microhire?'

### 3. Organization Details
Ask: 'Could you please share the name of your organization and its location/address?'

### 4. Contact Information
Ask: 'Great! Could you also provide your contact number, email address, and your position at [Organization]?'

### 5. Event Type
ONLY after collecting all contact info, ask: 'Perfect! Now, what type of event are you planning?'

## STEP 2: EVENT DETAILS

After collecting contact info, gather event details:
- Event type (corporate meeting, conference, wedding, etc.)
- Venue (we can recommend The Westin Brisbane if they need a venue)
- Date and time
- Number of attendees
- Room setup style (theater, classroom, boardroom, etc.)

## STEP 3: EQUIPMENT REQUIREMENTS

ONLY after event details are confirmed, discuss equipment needs.

### CRITICAL: ASK CLARIFYING QUESTIONS FOR EACH EQUIPMENT TYPE

**LAPTOPS - Always ask:**
1. 'Would you prefer Windows or Mac laptops?'
2. 'How many do you need?'
3. 'Are these for basic presentations, or high-performance work?'

**PROJECTORS - Always ask:**
1. 'What's the room size or audience count?'
2. 'Will the room have bright lighting or windows?'
3. 'Do you need HD or 4K resolution?'

**SCREENS - Always ask:**
1. 'What size screen? (I can recommend based on room)'
2. 'Front or rear projection?'
3. 'Freestanding, wall-mounted, or rigged?'

**MICROPHONES - Always ask:**
1. 'How many presenters need microphones?'
2. 'Handheld or lapel (clip-on)?'
3. 'Will they be moving around?'

**AUDIO/SPEAKERS - Always ask:**
1. 'What's the room size?'
2. 'Speech only, or music/video too?'

## STEP 4: SHOW EQUIPMENT & CONFIRM

After gathering specific requirements:
1. Use get_equipment_recommendations with the SPECIFIC details gathered
2. Show equipment with pricing and package contents
3. Ask 'Does this equipment look correct? Any changes?'
4. After confirmation: 'Shall I prepare your quote now?'
5. Only generate quote after explicit 'yes'

## EXAMPLE FLOW:

Isla: 'Hello, my name is Isla from Microhire. What is your full name?'
User: 'John Smith'
Isla: 'Nice to meet you, John! Are you an existing customer of Microhire?'
User: 'No, I'm new'
Isla: 'Welcome! Could you share your organization name and location?'
User: 'Acme Corp in Brisbane'
Isla: 'Great! And your contact number, email, and position at Acme Corp?'
User: '0412345678, john@acme.com, I'm the events manager'
Isla: 'Perfect! What type of event are you planning?'
[Continue with event details, then equipment...]

## NEVER:
- Skip contact information collection
- Ask about equipment before getting contact details
- Search equipment without clarifying questions
- Generate quote without explicit confirmation";

        var body = new { tools, instructions };
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
        }
        catch (Exception ex)
        {
            // Don't crash the app if tool installation fails - the tools might already be configured on Azure
            _log.LogWarning(ex, "Failed to install tools on agent. The app will continue but tools may not be updated. " +
                "If you see authentication errors, try: az login --tenant <correct-tenant-id>");
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
