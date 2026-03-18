using System.Globalization;
using System.Text.Json;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Config;
using Microsoft.Extensions.Options;

namespace MicrohireAgentChat.Services;

/// <summary>
/// AI-powered auto-test customer simulator. Uses Azure OpenAI GPT-4o to generate
/// realistic customer messages based on conversation transcript and a generated persona.
/// </summary>
public sealed class AutoTestCustomerService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AutoTestCustomerService> _logger;
    private readonly AutoTestOptions _options;
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly Random _random = new();

    private static readonly string[] FirstNames = {
        "Alex", "Jordan", "Taylor", "Morgan", "Casey", "Riley", "Sarah", "Michael",
        "Emily", "David", "Jessica", "James", "Emma", "William", "Olivia", "Benjamin"
    };

    private static readonly string[] LastNames = {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee"
    };

    private static readonly string[] CompanyNames = {
        "TechCorp", "DataSys", "CloudNine", "InnovateLabs", "FutureWorks", "NextGen Solutions",
        "Brisbane Tech", "Queensland Innovation", "Pacific Ventures", "Coral Sea Computing"
    };

    private static readonly string[] CompanySuffixes = { " Pty Ltd", " Ltd", " Inc", "", " Group", " Australia" };

    private static readonly string[] Positions = {
        "Event Coordinator", "Manager", "Director", "Operations Manager", "Executive Assistant",
        "Project Manager", "Marketing Director", "CEO", "Founder"
    };

    private static readonly string[] Suburbs = {
        "Brisbane", "Brisbane CBD", "South Brisbane", "Fortitude Valley", "West End", "Milton"
    };

    private static readonly string[] EventTypes = {
        "conference", "workshop", "board meeting", "training session", "product launch",
        "team building", "seminar", "gala dinner", "hackathon", "networking event"
    };

    private static readonly string[] RoomSetups = {
        "classroom style", "theater style", "boardroom style", "U-shape", "banquet style", "cabaret style"
    };

    private static readonly string[] RoomTypes = {
        "the main conference room", "the executive boardroom", "the Westin Ballroom",
        "the largest ballroom", "any available meeting room", "the grand conference room"
    };

    public AutoTestCustomerService(
        IHttpClientFactory httpClientFactory,
        IOptions<AutoTestOptions> options,
        IOptions<AzureOpenAIOptions> openAIOptions,
        ILogger<AutoTestCustomerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _openAIOptions = openAIOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a random customer persona for the given scenario.
    /// </summary>
    public AutoTestPersona InitializePersona(string? scenario)
    {
        var firstName = FirstNames[_random.Next(FirstNames.Length)];
        var lastName = LastNames[_random.Next(LastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var companyBase = CompanyNames[_random.Next(CompanyNames.Length)];
        var companySuffix = CompanySuffixes[_random.Next(CompanySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";
        var suburb = Suburbs[_random.Next(Suburbs.Length)];
        var position = Positions[_random.Next(Positions.Length)];
        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, companyBase);
        var isExistingCustomer = _random.Next(100) < 20;
        var eventType = EventTypes[_random.Next(EventTypes.Length)];
        var roomSetup = RoomSetups[_random.Next(RoomSetups.Length)];
        var roomType = RoomTypes[_random.Next(RoomTypes.Length)];
        var (minAttendees, maxAttendees) = scenario switch
        {
            "small_meeting" => (8, 25),
            "large_conference" => (200, 500),
            "social_event" => (80, 200),
            "thrive_boardroom" => (4, 10),
            "ballroom_projector" => (30, 150),
            _ => (15, 120)
        };
        var attendeeCount = _random.Next(minAttendees, maxAttendees + 1);
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 365));
        var dateFormatted = FormatDateRandomly(eventDate);

        var (scenarioKey, scenarioDescription) = scenario switch
        {
            "small_meeting" => ("small_meeting", "Small meeting (8-25 people)"),
            "large_conference" => ("large_conference", "Large conference (200-500 people)"),
            "social_event" => ("social_event", "Social/Gala event"),
            "thrive_boardroom" => ("thrive_boardroom", "Thrive Boardroom (fixed layout, 4-10 people)"),
            "ballroom_projector" => ("ballroom_projector", "Ballroom with projector area selection"),
            "random" => ("random", "Random scenario"),
            _ => ("random", "Random scenario")
        };

        if (scenario == "thrive_boardroom")
        {
            roomType = "the Thrive Boardroom";
            roomSetup = "boardroom style";
            var thriveEvents = new[] { "board meeting", "training session", "workshop" };
            eventType = thriveEvents[_random.Next(thriveEvents.Length)];
        }
        else if (scenario == "ballroom_projector")
        {
            roomType = _random.Next(2) == 0 ? "Westin Ballroom 1" : "Westin Ballroom 2";
            var ballroomSetups = new[] { "theatre style", "classroom style", "boardroom style" };
            roomSetup = ballroomSetups[_random.Next(ballroomSetups.Length)];
            var ballroomEvents = new[] { "conference", "seminar", "presentation" };
            eventType = ballroomEvents[_random.Next(ballroomEvents.Length)];
        }

        return new AutoTestPersona
        {
            FullName = fullName,
            FirstName = firstName,
            LastName = lastName,
            CompanyName = companyName,
            Suburb = suburb,
            Phone = phone,
            Email = email,
            Position = position,
            IsExistingCustomer = isExistingCustomer,
            EventType = eventType,
            RoomSetup = roomSetup,
            RoomType = roomType,
            AttendeeCount = attendeeCount,
            EventDate = eventDate,
            EventDateFormatted = dateFormatted,
            ScenarioKey = scenarioKey,
            ScenarioDescription = scenarioDescription
        };
    }

    /// <summary>
    /// Generates the next customer message using GPT-4o based on transcript and persona.
    /// Returns null if Azure OpenAI is not configured or the model returns empty.
    /// </summary>
    public async Task<string?> GenerateNextMessageAsync(
        IEnumerable<DisplayMessage> transcript,
        AutoTestPersona persona,
        CancellationToken ct = default)
    {
        var endpoint = _openAIOptions.Endpoint;
        var apiKey = _openAIOptions.ApiKey;
        var deployment = _openAIOptions.Deployment ?? "gpt-4o";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("AzureOpenAI not configured - cannot generate auto-test customer message");
            return null;
        }

        var transcriptText = BuildTranscriptText(transcript);
        var personaText = BuildPersonaText(persona);
        var systemPrompt = BuildSystemPrompt(personaText);

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = $"Current conversation:\n\n{transcriptText}\n\nGenerate ONLY the next single message the customer would send. One message only, no explanation, no JSON. Keep it natural and concise (1-3 sentences)." }
        };

        var requestBody = new
        {
            messages,
            temperature = 0.7,
            max_tokens = 300
        };

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Remove("api-key");
        client.DefaultRequestHeaders.Add("api-key", apiKey);

        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview";
        var response = await client.PostAsJsonAsync(url, requestBody, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Azure OpenAI auto-test API error: {Status} - {Error}", response.StatusCode, error);
            return null;
        }

        var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = responseJson.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return null;

        _logger.LogInformation("Auto-test customer generated message: {Preview}", content.Length > 60 ? content[..60] + "..." : content);
        return content;
    }

    /// <summary>
    /// Detects completion state and milestones from the conversation transcript.
    /// </summary>
    public AutoTestCompletionState DetectCompletionState(IEnumerable<DisplayMessage> transcript, int stepNumber)
    {
        var list = transcript.ToList();
        var state = new AutoTestCompletionState { StepNumber = stepNumber };

        var userTexts = list.Where(m => m.Role == "user").SelectMany(m => m.Parts ?? new List<string>()).Select(p => p?.Trim() ?? "").Where(s => s.Length > 0).ToList();
        var assistantTexts = list.Where(m => m.Role == "assistant").SelectMany(m => m.Parts ?? new List<string>()).Select(p => p?.Trim() ?? "").Where(s => s.Length > 0).ToList();
        var allAssistant = string.Join(" ", assistantTexts);
        var allUser = string.Join(" ", userTexts);

        state.NameProvided = userTexts.Any(t => t.Length > 2 && t.Length < 80 && !t.Contains("@") && !t.Contains("04"));
        state.CustomerStatusProvided = userTexts.Any(t => 
            t.Contains("new", StringComparison.OrdinalIgnoreCase) || 
            t.Contains("existing", StringComparison.OrdinalIgnoreCase) || 
            t.Contains("first time", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("returning", StringComparison.OrdinalIgnoreCase));
        state.OrganizationProvided = userTexts.Any(t => t.Length > 10 && (t.Contains("Pty") || t.Contains("Ltd") || t.Contains("Inc") || t.Contains("Brisbane") || t.Split(' ').Length >= 3));
        state.ContactInfoProvided = userTexts.Any(t => t.Contains("@") || t.Contains("04") || t.Contains("phone", StringComparison.OrdinalIgnoreCase) || t.Contains("email", StringComparison.OrdinalIgnoreCase));
        state.EventDetailsProvided = userTexts.Any(t => 
            t.Contains("Westin", StringComparison.OrdinalIgnoreCase) || 
            t.Contains("conference", StringComparison.OrdinalIgnoreCase) || 
            t.Contains("meeting", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("attendees", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("people", StringComparison.OrdinalIgnoreCase));
        state.EquipmentDiscussed = userTexts.Any(t => 
            t.Contains("laptop", StringComparison.OrdinalIgnoreCase) || 
            t.Contains("projector", StringComparison.OrdinalIgnoreCase) || 
            t.Contains("slides", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("presentation", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("yes", StringComparison.OrdinalIgnoreCase)) && userTexts.Count >= 5;
        state.ScheduleConfirmed = userTexts.Any(t => 
            t.Contains("Choose schedule:", StringComparison.OrdinalIgnoreCase) || 
            t.Contains("I've selected this schedule", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("yes that looks good", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("schedule", StringComparison.OrdinalIgnoreCase) && t.Contains("confirm", StringComparison.OrdinalIgnoreCase));
        state.QuoteSummaryShown = assistantTexts.Any(t => 
            t.Contains("summary", StringComparison.OrdinalIgnoreCase) && t.Contains("quote", StringComparison.OrdinalIgnoreCase)) ||
            allAssistant.Contains("create your quote", StringComparison.OrdinalIgnoreCase) ||
            allAssistant.Contains("confirm", StringComparison.OrdinalIgnoreCase) && allAssistant.Contains("proceed", StringComparison.OrdinalIgnoreCase);
        state.QuoteGenerated = allAssistant.Contains("Quote Ready", StringComparison.OrdinalIgnoreCase) || 
            allAssistant.Contains("Download PDF", StringComparison.OrdinalIgnoreCase) ||
            allAssistant.Contains("quote has been generated", StringComparison.OrdinalIgnoreCase) ||
            allAssistant.Contains("/files/quotes/", StringComparison.OrdinalIgnoreCase);
        state.QuoteConfirmed = allAssistant.Contains("Heavy Pencil", StringComparison.OrdinalIgnoreCase) || 
            allAssistant.Contains("quote to 'Heavy Pencil'", StringComparison.OrdinalIgnoreCase) ||
            allAssistant.Contains("notified our team", StringComparison.OrdinalIgnoreCase);

        if (state.QuoteConfirmed)
        {
            state.IsComplete = true;
            state.CompletionReason = "Quote confirmed";
        }
        else if (state.QuoteGenerated && userTexts.Any(t => t.Trim().Contains("yes", StringComparison.OrdinalIgnoreCase) && t.Length < 50))
        {
            state.IsComplete = true;
            state.CompletionReason = "Quote accepted by customer";
        }
        else if (stepNumber >= _options.MaxMessages)
        {
            state.IsComplete = true;
            state.CompletionReason = "Max steps reached";
        }

        return state;
    }

    private static string BuildTranscriptText(IEnumerable<DisplayMessage> transcript)
    {
        var lines = new List<string>();
        foreach (var m in transcript)
        {
            var role = m.Role == "assistant" ? "Isla (Assistant)" : "Customer";
            var text = m.FullText ?? string.Join(" ", m.Parts ?? new List<string>());
            if (string.IsNullOrWhiteSpace(text)) continue;
            lines.Add($"{role}: {text.Trim()}");
        }
        return string.Join("\n", lines);
    }

    private static string BuildPersonaText(AutoTestPersona p)
    {
        return $@"You are the customer in this chat. Your details (use naturally when the assistant asks):
- Full name: {p.FullName}
- Company: {p.CompanyName}, based in {p.Suburb}
- Phone: {p.Phone}, Email: {p.Email}, Position: {p.Position}
- New or existing customer: {(p.IsExistingCustomer ? "Existing" : "New")}
- Event: {p.EventType} at Westin Brisbane, {p.RoomType}, {p.RoomSetup}, {p.AttendeeCount} attendees, date: {p.EventDateFormatted}";
    }

    private static string BuildSystemPrompt(string personaText)
    {
        return $@"You are simulating a customer in a live chat with Isla, a venue and equipment booking assistant from Microhire. Your goal is to have a natural conversation that leads to a quote.

{personaText}

Rules:
- Reply as the customer only. Output ONLY the next message the customer would send — no labels, no explanations, no JSON.
- Be natural and concise (usually 1-3 sentences). Match how real customers type (casual, sometimes brief).
- If Isla asks for your name, give it. If she asks new/existing customer, answer. If she asks organisation, contact, event details, equipment needs, or schedule — provide the info from your persona when relevant.
- If Isla shows room options, respond with your persona's room type (e.g. ""Thrive Boardroom please"" or ""Westin Ballroom 1"").
- ROOM LAYOUT: When Isla shows room layout options and asks for setup style, respond with your persona's room setup (e.g. ""theatre style please"" or ""boardroom style"").
- CRITICAL - TIME PICKER / SCHEDULE: When Isla shows a time picker, asks you to select schedule times, or presents schedule options, you MUST respond with the EXACT format below (nothing else):
  Choose schedule: date=YYYY-MM-DD; setup=HH:mm; rehearsal=HH:mm; start=HH:mm; end=HH:mm; packup=HH:mm
  Example: ""Choose schedule: date=2026-04-28; setup=08:00; rehearsal=08:30; start=09:00; end=17:00; packup=19:00""
  Use your event date in YYYY-MM-DD format. Make times realistic (setup 30-60min before rehearsal, rehearsal 30min before start, packup 1-3hrs after end). Do NOT say ""yes that looks good"" or any natural language — output the Choose schedule line only.
- PROJECTOR AREA: When Isla shows a floor plan and asks you to choose a projector placement area, respond with the area letter (e.g. ""Area B please"" or ""I'd like area E""). For dual projector, say ""B and C"" or ""E and F"".
- If Isla presents a quote summary and asks to create the quote, say yes (e.g. ""yes create the quote"", ""yes please"").
- If you see ""Quote Ready"" or a download link and she asks you to accept, say yes (e.g. ""yes I accept"", ""looks good, we'll take it"").
- Do not repeat yourself unnecessarily. Do not break character. Do not output anything except the single next customer message.";
    }

    private string GeneratePhoneNumber()
    {
        var formats = new[]
        {
            $"04{_random.Next(10000000, 99999999)}",
            $"04 {_random.Next(1000, 9999)} {_random.Next(1000, 9999)}"
        };
        return formats[_random.Next(formats.Length)];
    }

    private string GenerateEmail(string firstName, string lastName, string company)
    {
        var clean = company.ToLower().Replace(" ", "").Replace("'", "");
        return $"{firstName.ToLower()}.{lastName.ToLower()}@{clean}.com";
    }

    private string FormatDateRandomly(DateTime date)
    {
        var formats = new[]
        {
            date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture),
            date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture),
            date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        };
        return formats[_random.Next(formats.Length)];
    }
}
