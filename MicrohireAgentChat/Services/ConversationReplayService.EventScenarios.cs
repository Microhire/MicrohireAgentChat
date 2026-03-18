using System.Globalization;

namespace MicrohireAgentChat.Services;

public partial class ConversationReplayService
{
    #region Specific Scenario Generators

    /// <summary>
    /// Generates a small intimate event test case - Answers event-focused questions
    /// </summary>
    public IEnumerable<string> GenerateSmallEventConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 90));

        return new[]
        {
            $"hi, I'm {fullName}",
            "new customer",
            $"{company}, Brisbane office",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}.{lastName.ToLower()}@example.com, manager",
            $"small board meeting at Westin Brisbane, just 15 people, boardroom style, {eventDate:d MMMM yyyy}",
            // Event-focused responses:
            "yes there will be 2 presentations",
            "yes we need to show slides",
            "please provide a laptop, Windows is fine"
        };
    }

    /// <summary>
    /// Generates a large conference test case - Answers event-focused questions
    /// </summary>
    public IEnumerable<string> GenerateLargeConferenceConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(60, 180));

        return new[]
        {
            $"my name is {fullName}",
            "we're a returning customer",
            $"{company} based in Brisbane CBD",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@bigcorp.com, event director",
            $"annual tech conference, Westin Brisbane main ballroom, theater style for 400 attendees, {eventDate:d MMMM yyyy}",
            // Event-focused responses:
            "yes we have about 8 speakers presenting throughout the day",
            "yes definitely, lots of slides and videos to display",
            "yes please provide Windows laptops for the presenters",
            "we'll also need stage lighting to highlight speakers"
        };
    }

    /// <summary>
    /// Generates a hackathon specific test case - Answers event-focused questions
    /// </summary>
    public IEnumerable<string> GenerateHackathonConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]} Labs";
        var eventDate = DateTime.Now.AddDays(_random.Next(30, 120));

        return new[]
        {
            $"{fullName}",
            "nope first time",
            $"{company}, Fortitude Valley",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@{company.ToLower().Replace(" ", "")}.io, developer advocate",
            $"hackathon event at Westin Brisbane, need the biggest space, open floor plan for 150 developers, {eventDate:d MMMM}",
            // Event-focused responses:
            "yes there will be about 6 demo presentations at the end",
            "yes we need screens for the demos and presentations",
            "yes please provide 5 laptops for the demos - Windows for sure"
        };
    }

    /// <summary>
    /// Generates a wedding/social event test case - Answers event-focused questions
    /// </summary>
    public IEnumerable<string> GenerateSocialEventConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var eventDate = DateTime.Now.AddDays(_random.Next(90, 365));
        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";
        var suburb = _suburbs[_random.Next(_suburbs.Length)];

        return new[]
        {
            $"Hello, my name is {fullName}",
            "no, this is my first time using Microhire",
            $"{companyName}, based in {suburb}",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}{lastName.ToLower()}@gmail.com, I'm the event coordinator",
            $"gala dinner at Westin Brisbane ballroom, banquet style for 200 attendees, {eventDate:MMMM d, yyyy}",
            // Event-focused responses for social event:
            "yes we'll have 2 speeches",
            "no, just speeches no slides",
            "yes we definitely need music throughout the evening",
            "yes please, some nice ambient lighting would be perfect"
        };
    }

    /// <summary>
    /// Generates a minimal info test case (user provides brief answers)
    /// </summary>
    public IEnumerable<string> GenerateMinimalInfoConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));

        return new[]
        {
            $"{firstName} {lastName}",
            "new",
            $"ABC Corp, Brisbane",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@abc.com, manager",
            $"meeting, Westin Brisbane, 50 people, {eventDate:dd/MM/yyyy}",
            // Brief event-focused responses:
            "yes 3 speakers",
            "yes slides",
            "yes need laptop, Windows"
        };
    }

    /// <summary>
    /// Generates a very detailed/verbose test case - Detailed event-focused answers
    /// </summary>
    public IEnumerable<string> GenerateDetailedConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(45, 150));
        var speakerCount = _random.Next(3, 6);

        return new[]
        {
            $"Good morning! My full name is {fullName}, pleased to meet you.",
            "We haven't worked with Microhire before, so we're new customers looking forward to a great partnership.",
            $"Our organization is {company}. We're headquartered in {_suburbs[_random.Next(_suburbs.Length)]}, Brisbane, Queensland, Australia.",
            $"You can reach me on 04{_random.Next(10000000, 99999999)}. My email address is {firstName.ToLower()}.{lastName.ToLower()}@{company.ToLower().Replace(" ", "").Replace("'", "")}.com. I serve as the {_positions[_random.Next(_positions.Length)]} at our company.",
            $"We're planning a comprehensive conference event. The date is {eventDate:dddd, MMMM d, yyyy}. We're expecting approximately {_random.Next(80, 150)} attendees. We'd like to use the Westin Brisbane, specifically their main ballroom if available, set up in {_roomSetups[_random.Next(_roomSetups.Length)]} configuration.",
            // Detailed event-focused responses:
            $"Yes absolutely, we have {speakerCount} different speakers who will be presenting throughout the day. Each presentation will be roughly 30-45 minutes with Q&A afterward.",
            $"Definitely, every speaker will be showing PowerPoint slides and some will have video content as well. The audience really needs to see the screen clearly.",
            $"Yes please, we'd like you to provide {_random.Next(2, 4)} Windows laptops for the presenters. They won't be bringing their own equipment.",
            "If possible, we'd also appreciate some basic stage lighting to highlight the speakers during their presentations."
        };
    }

    /// <summary>
    /// Generates a rush/urgent booking test case - Quick event-focused answers
    /// </summary>
    public IEnumerable<string> GenerateUrgentBookingConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        // Urgent = within next 2 weeks
        var eventDate = DateTime.Now.AddDays(_random.Next(3, 14));

        return new[]
        {
            $"Hi, {fullName} here, I need help urgently!",
            "new customer but we need this sorted quickly",
            $"{company}, Brisbane CBD",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@urgent.com, events coordinator",
            $"URGENT - last minute training session at Westin Brisbane, {eventDate:d MMMM} - that's only {(eventDate - DateTime.Now).Days} days away! 75 people, classroom setup",
            // Quick event-focused answers:
            "yes 4 trainers presenting",
            "yes need to show presentations",
            "yes provide laptops please - Windows"
        };
    }

    /// <summary>
    /// Generates a multi-day event test case - Event-focused answers
    /// </summary>
    public IEnumerable<string> GenerateMultiDayEventConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(60, 180));
        var days = _random.Next(2, 5);

        return new[]
        {
            $"my name is {fullName}",
            "returning customer",
            $"{company}, {_suburbs[_random.Next(_suburbs.Length)]}",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@{company.ToLower().Replace(" ", "")}.com, {_positions[_random.Next(_positions.Length)].ToLower()}",
            $"{days}-day conference at Westin Brisbane, starting {eventDate:d MMMM yyyy}. Main ballroom, theater style for 250 attendees",
            // Event-focused answers for multi-day:
            $"yes we have about 6 speakers each day over the {days} days",
            "yes definitely need to show presentations and videos",
            "yes please provide laptops - Windows preferred",
            "and we'd like stage lighting for the speakers"
        };
    }

    /// <summary>
    /// Generates a panel discussion test case at Westin Brisbane - Event-focused
    /// </summary>
    public IEnumerable<string> GenerateEquipmentOnlyConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 90));
        var attendees = _random.Next(30, 120);
        var roomType = new[] { 
            "main ballroom", "conference room", "executive boardroom", 
            "meeting room", "grand hall"
        }[_random.Next(5)];

        return new[]
        {
            $"hi I'm {fullName}",
            "first time customer",
            $"{company}, based in Brisbane",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@example.com, project manager",
            $"panel discussion at Westin Brisbane {roomType} on {eventDate:d MMMM}. About {attendees} people attending",
            // Event-focused answers:
            "yes we have 3 panelists who will be speaking",
            "yes we need to show some slides during the discussion",
            "no, panelists will bring their own laptops"
        };
    }

    /// <summary>
    /// Generates an all-info-at-once test case (user provides lots of context upfront)
    /// This tests how the bot handles users who give event details but still need smart equipment questions
    /// </summary>
    public IEnumerable<string> GenerateAllAtOnceConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var position = _positions[_random.Next(_positions.Length)];
        var eventDate = DateTime.Now.AddDays(_random.Next(30, 120));
        var attendees = _random.Next(50, 200);
        var phone = $"04{_random.Next(10000000, 99999999)}";
        var email = $"{firstName.ToLower()}.{lastName.ToLower()}@company.com";
        var speakerCount = _random.Next(3, 8);

        // User gives everything at once including event activity details
        return new[]
        {
            $"Hi! I'm {fullName} from {company} ({phone}, {email}), we're a new customer. I'm the {position.ToLower()} and I need to book a conference at Westin Brisbane on {eventDate:d MMMM yyyy}. We're expecting {attendees} people in {_roomSetups[_random.Next(_roomSetups.Length)]} setup. We'll have {speakerCount} speakers presenting slides throughout the day. They'll need laptops - Windows preferred. Can you help?"
        };
    }

    /// <summary>
    /// Generates a training/workshop specific test case - Event-focused responses
    /// </summary>
    public IEnumerable<string> GenerateTrainingWorkshopConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(21, 90));
        var trainees = _random.Next(20, 60);

        return new[]
        {
            $"hey, {fullName} here",
            "new to Microhire",
            $"{company}, South Brisbane",
            $"04{_random.Next(10000000, 99999999)}, training@{company.ToLower().Replace(" ", "")}.com, training manager",
            $"running a hands-on training workshop at Westin Brisbane on {eventDate:d MMMM}. Need classroom style for {trainees} trainees",
            // Event-focused responses for training:
            "yes there will be 2 trainers presenting",
            "yes we need to show training materials on screen",
            $"yes this is hands-on training so every trainee needs a laptop - that's {trainees} Windows laptops"
        };
    }

    /// <summary>
    /// Generates a Mac-focused tech company test case - Event-focused with Mac preference
    /// </summary>
    public IEnumerable<string> GenerateMacTechConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = new[] { "Apple Partners", "Design Studio", "Creative Labs", "UX Agency", "Digital Creatives", "Media House" }[_random.Next(6)];
        var eventDate = DateTime.Now.AddDays(_random.Next(30, 90));

        return new[]
        {
            $"hi I'm {fullName}",
            "first time using Microhire",
            $"{company}, Fortitude Valley",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@{company.ToLower().Replace(" ", "")}.io, creative director",
            $"creative showcase event at Westin Brisbane, {eventDate:d MMMM}, expecting 80 designers and developers, theater style",
            // Event-focused responses with Mac preference:
            "yes we have 4 creative directors presenting their portfolio work",
            "absolutely, lots of visual content and videos to showcase",
            "yes please provide laptops - but we NEED Macs, MacBooks specifically - it's a creative event"
        };
    }

    /// <summary>
    /// Generates a corporate boardroom meeting test case - Event-focused for video conferencing
    /// </summary>
    public IEnumerable<string> GenerateBoardroomMeetingConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(7, 45));

        return new[]
        {
            $"Good afternoon, {fullName}",
            "existing customer, we've hired from you before",
            $"{company}, Brisbane CBD",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@corporate.com.au, executive assistant",
            $"board meeting at Westin Brisbane executive boardroom on {eventDate:d MMMM yyyy}. Just 12 board members, boardroom style",
            // Event-focused responses for boardroom:
            "yes 3 executives will present reports",
            "yes we need to show financial presentations",
            "please provide 1 laptop, Windows is fine",
            "we also have remote attendees joining via video conference"
        };
    }

    /// <summary>
    /// Generates a product launch/demo test case - Event-focused for impressive event
    /// </summary>
    public IEnumerable<string> GenerateProductLaunchConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(45, 120));

        return new[]
        {
            $"hello, my name is {fullName}",
            "new customer for this event",
            $"{company}, we're headquartered in Brisbane",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@startup.io, head of marketing",
            $"product launch event at Westin Brisbane grand ballroom, {eventDate:d MMMM yyyy}. Expecting 300 attendees plus media. Theater style with a stage",
            // Event-focused responses for product launch:
            "yes we have 4 speakers including our CEO doing the main presentation",
            "absolutely, we need impressive visuals - product videos and live demos",
            "yes please provide laptops for the demos, high-end Windows machines",
            "we want professional stage lighting to make this look impressive for the media"
        };
    }

    #region Bug Reproduction Scenarios

    /// <summary>
    /// BUG REPRODUCTION: AI Stops Responding After Time Picker Confirmation
    /// 
    /// This scenario reproduces the reported bug where:
    /// 1. User goes through the full conversation flow
    /// 2. User selects venue (Thrive Boardroom)
    /// 3. User confirms schedule via time picker
    /// 4. AI stops responding and just repeats "Thank you! I've noted X attendees..."
    /// 
    /// Uses randomized data but follows the exact conversation pattern that triggers the bug.
    /// </summary>

    #endregion

    #endregion
}
