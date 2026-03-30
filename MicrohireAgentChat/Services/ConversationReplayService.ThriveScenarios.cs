namespace MicrohireAgentChat.Services;

/// <summary>
/// Thrive Boardroom-specific conversation replay scenarios for comprehensive flow testing.
/// Covers: happy path, capacity limits, presentations, video conferencing, equipment modification, room aliases.
/// </summary>
public partial class ConversationReplayService
{
    /// <summary>
    /// Minimal Thrive flow — happy path end-to-end.
    /// Name, customer status, org, contact, event type, "thrive", date, attendees (<=10), schedule, confirm equipment, create quote.
    /// </summary>
    public IEnumerable<string> GenerateThriveSimpleMeetingConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, _companyNames[_random.Next(_companyNames.Length)]);
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateStr = eventDate.ToString("yyyy-MM-dd");
        var attendeeCount = _random.Next(4, 11); // Thrive max 10

        return new[]
        {
            $"Hi I'm {fullName}",
            "new customer",
            $"{company}, Brisbane",
            $"{phone}, {email}, event coordinator",
            "boardroom meeting at Westin Brisbane",
            "thrive boardroom please",
            $"event on {eventDate:d MMMM yyyy}, {attendeeCount} attendees",
            $"Choose schedule: date={dateStr}; setup=08:00; rehearsal=08:30; start=09:00; end=16:00; packup=17:00",
            "yes there will be 1 presenter with slides",
            "yes we need to show slides",
            "Mac laptop please",
            "yes create quote"
        };
    }

    /// <summary>
    /// User requests Thrive with 15+ attendees. Agent should NOT suggest Thrive and should redirect to larger rooms (Elevate, Podium).
    /// </summary>
    public IEnumerable<string> GenerateThriveTooManyAttendeesConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));

        return new[]
        {
            $"Hi I'm {fullName}. We need a meeting at Westin Brisbane for 15 people on {eventDate:d MMMM yyyy}",
            "new customer",
            $"{company}, Brisbane",
            $"{GeneratePhoneNumber()}, {GenerateEmail(firstName, lastName, _companyNames[_random.Next(_companyNames.Length)])}, manager",
            "boardroom style meeting",
            "I'd like the thrive boardroom",
            // Agent should warn that Thrive holds max 10 and suggest Elevate/Podium
            "ok, elevate then",
            $"Choose schedule: date={eventDate:yyyy-MM-dd}; setup=08:00; rehearsal=08:30; start=09:00; end=16:00; packup=17:00",
            "yes 2 presenters with slides",
            "Windows laptop please",
            "yes create quote"
        };
    }

    /// <summary>
    /// Thrive with 2 presenters, slides, Mac laptops, clicker, flipchart.
    /// Verifies THRVAVP is included but no duplicate projector/screen/speaker, and laptop + clicker + flipchart are added.
    /// </summary>
    public IEnumerable<string> GenerateThrivePresentationsFullAvConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateStr = eventDate.ToString("yyyy-MM-dd");

        return new[]
        {
            $"Hi I'm {fullName}. Boardroom meeting at Westin Brisbane, 8 people, {eventDate:d MMMM yyyy}",
            "new customer",
            $"{company}, Brisbane",
            $"{GeneratePhoneNumber()}, {GenerateEmail(firstName, lastName, _companyNames[_random.Next(_companyNames.Length)])}, director",
            "thrive boardroom please",
            $"Choose schedule: date={dateStr}; setup=08:00; rehearsal=08:30; start=09:00; end=17:00; packup=18:00",
            "yes there will be 2 presenters with slides",
            "yes we need to show slides",
            "yes a clicker would be good",
            "yes flipchart please",
            "Mac laptop please",
            "yes create quote"
        };
    }

    /// <summary>
    /// User mentions Zoom/Teams call. Agent must ask explicit clarifying questions about video conferencing gear, never auto-add.
    /// Should include video_conference_unit after user confirms.
    /// </summary>
    public IEnumerable<string> GenerateThriveVideoConferenceConversation()
    {
        return new[]
        {
            "Daniel Morel",
            "new",
            "Morel Enterprises",
            "Brisbane",
            "daniel@morel.co",
            "meeting",
            "thrive",
            "7 April 2026",
            "8",
            "Choose schedule: date=2026-04-07; setup=07:00; rehearsal=09:30; start=10:00; end=16:00; packup=17:00",
            "Yes a couple of laptops and we will be sharing presentations on screen with attendees in the room and others globally via zoom and teams",
            "Yes a clicker would be good",
            "A flipchart would be good too",
            "2 laptops",
            "what are my options",
            "mac please",
            "yes we need video conferencing equipment",
            "yes"
        };
    }

    /// <summary>
    /// User brings own laptop, needs HDMI adaptor. Tests the HDMI adaptor flow for Thrive.
    /// </summary>
    public IEnumerable<string> GenerateThriveOwnLaptopAdaptorConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateStr = eventDate.ToString("yyyy-MM-dd");

        return new[]
        {
            $"Hi I'm {fullName}. Meeting at Westin Brisbane Thrive Boardroom, 6 people, {eventDate:d MMMM yyyy}",
            "new customer",
            $"{company}, Brisbane",
            $"{GeneratePhoneNumber()}, {GenerateEmail(firstName, lastName, _companyNames[_random.Next(_companyNames.Length)])}, coordinator",
            "thrive please",
            $"Choose schedule: date={dateStr}; setup=08:00; rehearsal=08:30; start=09:00; end=16:00; packup=17:00",
            "yes 1 presenter with slides",
            "we're bringing our own laptop",
            "yes we need a USBC adaptor",
            "yes create quote"
        };
    }

    /// <summary>
    /// Thrive flow, gets quote summary, then asks to remove an item and add flipchart. Tests update_equipment and regenerate_quote.
    /// </summary>
    public IEnumerable<string> GenerateThriveEquipmentModificationConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateStr = eventDate.ToString("yyyy-MM-dd");

        return new[]
        {
            $"Hi I'm {fullName}. Boardroom meeting at Westin Brisbane, 8 people, {eventDate:d MMMM yyyy}",
            "new customer",
            $"{company}, Brisbane",
            $"{GeneratePhoneNumber()}, {GenerateEmail(firstName, lastName, _companyNames[_random.Next(_companyNames.Length)])}, manager",
            "Thrive Boardroom",
            $"Choose schedule: date={dateStr}; setup=08:00; rehearsal=08:30; start=09:00; end=16:00; packup=17:00",
            "yes there will be 2 presenters with slides",
            "yes we need to show slides",
            "Mac laptop please",
            "yes create quote",
            "Actually can you remove the clicker and add a flipchart instead",
            "yes create quote"
        };
    }

    /// <summary>
    /// User says only "thrive" (not "thrive boardroom") to select the room. Tests standalone token matching.
    /// </summary>
    public IEnumerable<string> GenerateThriveRoomAliasBareConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateStr = eventDate.ToString("yyyy-MM-dd");

        return new[]
        {
            $"Hi I'm {fullName}. Meeting at Westin Brisbane, 6 people, {eventDate:d MMMM yyyy}",
            "new customer",
            $"{company}, Brisbane",
            $"{GeneratePhoneNumber()}, {GenerateEmail(firstName, lastName, _companyNames[_random.Next(_companyNames.Length)])}, coordinator",
            "what rooms do you have?",
            "thrive",
            $"Choose schedule: date={dateStr}; setup=08:00; rehearsal=08:30; start=09:00; end=16:00; packup=17:00",
            "yes 1 presenter with slides",
            "we'll bring our own laptop",
            "yes create quote"
        };
    }
}
