using System.Globalization;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Service for replaying test conversations with varied data for development
/// </summary>
public class ConversationReplayService
{
    private readonly Random _random = new();

    private readonly string[] _firstNames = {
        "Alex", "Jordan", "Taylor", "Morgan", "Casey", "Riley", "Avery", "Quinn",
        "Blake", "Cameron", "Dakota", "Ellis", "Finley", "Garrett", "Hayden", "Indigo",
        "Jaden", "Kendall", "Logan", "Madison", "Nolan", "Owen", "Parker", "Reagan"
    };

    private readonly string[] _lastNames = {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
        "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White"
    };

    private readonly string[] _companyNames = {
        "TechCorp", "DataSys", "CloudNine", "InnovateLabs", "FutureWorks", "NextGen Solutions",
        "DigitalEdge", "SmartTech", "CodeMasters", "ByteWorks", "LogicFlow", "SysIntegrate",
        "WebForge", "AppCraft", "DevHub", "CodeNest", "TechForge", "DataFlow", "CloudSync"
    };

    private readonly string[] _companySuffixes = {
        " Pty Ltd", " Ltd", " Inc", " LLC", " Corp", " Solutions", " Technologies", " Systems",
        "", " Group", " Labs", " Studio", " Co", " Partners"
    };

    private readonly string[] _positions = {
        "CEO", "CTO", "COO", "CFO", "Founder", "Director", "Manager", "Coordinator",
        "Lead Developer", "Product Manager", "Event Coordinator", "Operations Manager",
        "Head of Technology", "VP of Engineering", "Chief Technology Officer"
    };

    private readonly string[] _eventTypes = {
        "hackathon", "conference", "workshop", "seminar", "product launch", "team building",
        "training session", "corporate retreat", "networking event", "developer meetup",
        "webinar", "board meeting", "annual general meeting", "product demo", "press conference"
    };

    private readonly string[] _venues = {
        "Westin Brisbane", "Brisbane Convention Centre", "Riverside Hotel", "Grand Plaza Hotel",
        "Metropolitan Conference Center", "Harbour View Ballroom", "City Central Hotel",
        "Executive Suites Brisbane", "Convention Centre Plaza", "Riverfront Conference Hall"
    };

    private readonly string[] _roomTypes = {
        "the biggest hall", "the largest ballroom", "the main auditorium", "the executive boardroom",
        "the grand conference room", "the premier event space", "the convention hall",
        "the executive suite", "the presidential ballroom", "the grand pavilion"
    };

    /// <summary>
    /// Generates a varied test conversation based on the original hackathon booking flow
    /// </summary>
    public IEnumerable<string> GenerateTestConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";

        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";

        var position = _positions[_random.Next(_positions.Length)];
        var phone = $"04{_random.Next(10000000, 99999999)}";
        var email = $"{firstName.ToLower()}.{lastName.ToLower()}@{companyBase.ToLower().Replace(" ", "")}.com";

        var eventType = _eventTypes[_random.Next(_eventTypes.Length)];

        // Fixed venue and room for testing against actual inventory system
        var venue = "Westin Brisbane";
        var roomType = "the biggest hall";

        // Generate a future date (next 1-12 months from now)
        var monthsAhead = _random.Next(1, 13);
        var eventDate = DateTime.Now.AddMonths(monthsAhead);
        var dateString = eventDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

        return new[]
        {
            $"my name is {fullName}",
            "no im a new customer",
            $"{companyName} is the name, address is in Brisbane",
            $"{phone}, {email}, im the {position.ToLower()}",
            $"{eventType}, im gonna need the venue from you guys, {venue} and {roomType} in classroom style for 100 people, on the {dateString}",
            "im gonna need 2 laptops 2 projectors and 2 screens and 2 wireless microphones"
        };
    }
}
