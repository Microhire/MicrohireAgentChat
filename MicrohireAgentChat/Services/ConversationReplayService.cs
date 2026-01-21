using System.Globalization;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Service for replaying test conversations with varied data for development.
/// Generates highly varied test scenarios to ensure comprehensive coverage of:
/// - Different user personas and communication styles
/// - Various equipment combinations and specifications
/// - Different event types, sizes, and setups
/// - Multiple date/time formats
/// - New vs existing customers
/// - Edge cases and unusual requests
/// </summary>
public class ConversationReplayService
{
    private readonly Random _random = new();

    #region Names and Personas

    private readonly string[] _firstNames = {
        "Alex", "Jordan", "Taylor", "Morgan", "Casey", "Riley", "Avery", "Quinn",
        "Blake", "Cameron", "Dakota", "Ellis", "Finley", "Garrett", "Hayden", "Indigo",
        "Jaden", "Kendall", "Logan", "Madison", "Nolan", "Owen", "Parker", "Reagan",
        "Sarah", "Michael", "Emily", "David", "Jessica", "James", "Amanda", "Christopher",
        "Jennifer", "Matthew", "Ashley", "Daniel", "Samantha", "Andrew", "Nicole", "Joshua",
        "Emma", "William", "Olivia", "Benjamin", "Sophia", "Lucas", "Ava", "Henry"
    };

    private readonly string[] _lastNames = {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson",
        "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White",
        "Harris", "Clark", "Lewis", "Young", "Walker", "Hall", "Allen", "King", "Wright",
        "Scott", "Green", "Baker", "Adams", "Nelson", "Hill", "Mitchell", "Campbell"
    };

    private readonly string[] _companyNames = {
        "TechCorp", "DataSys", "CloudNine", "InnovateLabs", "FutureWorks", "NextGen Solutions",
        "DigitalEdge", "SmartTech", "CodeMasters", "ByteWorks", "LogicFlow", "SysIntegrate",
        "WebForge", "AppCraft", "DevHub", "CodeNest", "TechForge", "DataFlow", "CloudSync",
        "Quantum Computing", "CyberNet", "InfoTech", "GlobalData", "MegaSoft", "Precision Systems",
        "Alpha Dynamics", "Beta Solutions", "Gamma Technologies", "Delta Engineering", "Epsilon Labs",
        "Azure Pacific", "Northern Star", "Southern Cross", "Eastern Horizons", "Western Digital",
        "Brisbane Tech", "Queensland Innovation", "Pacific Ventures", "Coral Sea Computing"
    };

    private readonly string[] _companySuffixes = {
        " Pty Ltd", " Ltd", " Inc", " LLC", " Corp", " Solutions", " Technologies", " Systems",
        "", " Group", " Labs", " Studio", " Co", " Partners", " Enterprises", " Holdings",
        " International", " Australia", " Global", " Consulting"
    };

    private readonly string[] _positions = {
        "CEO", "CTO", "COO", "CFO", "Founder", "Co-Founder", "Director", "Manager", "Coordinator",
        "Lead Developer", "Product Manager", "Event Coordinator", "Operations Manager",
        "Head of Technology", "VP of Engineering", "Chief Technology Officer", "Admin Assistant",
        "Executive Assistant", "Office Manager", "HR Director", "Marketing Director",
        "Sales Manager", "Business Development Manager", "Project Manager", "Team Lead",
        "Senior Developer", "Tech Lead", "Scrum Master", "Agile Coach", "Digital Strategist"
    };

    private readonly string[] _suburbs = {
        "Brisbane", "Brisbane CBD", "South Brisbane", "Fortitude Valley", "West End",
        "New Farm", "Paddington", "Milton", "Toowong", "St Lucia", "Woolloongabba",
        "Kangaroo Point", "Spring Hill", "Herston", "Kelvin Grove", "Red Hill",
        "Hamilton", "Ascot", "Clayfield", "Nundah", "Chermside", "Indooroopilly"
    };

    #endregion

    #region Event Types and Details

    private readonly string[] _eventTypes = {
        "hackathon", "conference", "workshop", "seminar", "product launch", "team building",
        "training session", "corporate retreat", "networking event", "developer meetup",
        "webinar recording", "board meeting", "annual general meeting", "product demo", "press conference",
        "company all-hands meeting", "investor presentation", "client showcase", "tech talk",
        "panel discussion", "awards ceremony", "graduation ceremony", "gala dinner",
        "charity fundraiser", "industry summit", "trade show", "expo", "job fair",
        "leadership offsite", "strategy planning session", "quarterly review", "town hall meeting",
        "user conference", "customer appreciation event", "partner summit", "sales kickoff"
    };

    private readonly string[] _eventTypeDescriptions = {
        "hackathon where developers will be coding",
        "conference with multiple speakers",
        "hands-on workshop",
        "educational seminar",
        "product launch event",
        "team building day",
        "training session for new employees",
        "corporate retreat for leadership",
        "networking event for professionals",
        "developer meetup with presentations",
        "board meeting requiring video conferencing",
        "AGM with shareholders",
        "product demo for potential clients",
        "press conference for media",
        "company all-hands meeting",
        "investor pitch session",
        "client showcase presentation",
        "tech talk with Q&A"
    };

    private readonly string[] _roomSetups = {
        "classroom style", "theater style", "boardroom style", "U-shape", "hollow square",
        "banquet style", "cocktail style", "cabaret style", "herringbone", "chevron",
        "conference style", "auditorium style", "reception style", "rounds of 10",
        "rounds of 8", "open floor plan", "TED talk style", "panel discussion setup"
    };

    private readonly string[] _roomTypes = {
        "the biggest hall", "the largest ballroom", "the main auditorium", "the executive boardroom",
        "the grand conference room", "the premier event space", "the convention hall",
        "the executive suite", "the presidential ballroom", "the grand pavilion",
        "the Westin Ballroom", "the main conference room", "any available meeting room",
        "your largest space", "a medium-sized room", "an intimate meeting room",
        "a breakout room", "the entire floor", "multiple connected rooms"
    };

    // Attendee counts with realistic ranges for different event types
    private readonly (int min, int max, string description)[] _attendeeCounts = {
        (10, 25, "small group"),
        (25, 50, "medium group"),
        (50, 100, "large group"),
        (100, 200, "very large event"),
        (200, 500, "major conference"),
        (500, 1000, "massive convention")
    };

    #endregion

    #region Equipment Variations

    // Different ways users might request laptops
    private readonly string[] _laptopRequests = {
        "laptops", "laptop computers", "notebooks", "PCs", "computers",
        "windows laptops", "mac laptops", "macbooks", "windows PCs",
        "presentation laptops", "show laptops"
    };

    // Different ways users might request projectors
    private readonly string[] _projectorRequests = {
        "projectors", "projector", "video projectors", "a projector setup",
        "projection equipment", "data projectors", "laser projectors"
    };

    // Different ways users might request screens
    private readonly string[] _screenRequests = {
        "screens", "projection screens", "big screens", "display screens",
        "fastfold screens", "large screens", "screens for projection"
    };

    // Different ways users might request microphones
    private readonly string[] _microphoneRequests = {
        "wireless microphones", "wireless mics", "microphones", "mics",
        "handheld mics", "lapel mics", "clip-on mics", "lavalier mics",
        "radio mics", "cordless microphones", "presenter mics"
    };

    // Different ways users might request speakers/audio
    private readonly string[] _audioRequests = {
        "speakers", "PA system", "sound system", "audio setup",
        "speaker system", "PA speakers", "audio equipment"
    };

    // Different ways users might request lighting
    private readonly string[] _lightingRequests = {
        "lighting", "stage lighting", "event lighting", "LED lights",
        "uplighting", "wash lighting", "ambient lighting", "mood lighting"
    };

    // Additional equipment types
    private readonly string[] _additionalEquipment = {
        "lectern", "podium", "flipcharts", "whiteboards", "TV monitors",
        "video recording", "live streaming setup", "video conferencing",
        "webcam", "PTZ camera", "mixer", "backdrop", "stage"
    };

    #endregion

    #region Communication Styles

    // Casual greeting responses
    private readonly string[] _nameResponses = {
        "my name is {0}",
        "I'm {0}",
        "hey, {0} here",
        "{0}",
        "its {0}",
        "hi my name is {0}",
        "hello, i'm {0}",
        "{0} is my name",
        "you can call me {0}",
        "name's {0}"
    };

    // Responses for new customer question
    private readonly string[] _newCustomerResponses = {
        "no im a new customer",
        "nope, first time",
        "new customer",
        "no, we haven't used Microhire before",
        "first time using you guys",
        "no we're new",
        "this is our first event with Microhire",
        "haven't used you before",
        "no, brand new",
        "nope, never used your services"
    };

    // Responses for existing customer question
    private readonly string[] _existingCustomerResponses = {
        "yes we've used you before",
        "yes, existing customer",
        "yep, we did an event last year",
        "yes we're repeat customers",
        "yes, {0} has worked with you previously",
        "yeah we've hired equipment before",
        "returning customer actually",
        "yes, multiple times"
    };

    // Ways to provide organization info
    private readonly string[] _orgResponses = {
        "{0} is the name, address is in {1}",
        "the company is {0}, based in {1}",
        "{0}, we're located in {1}",
        "organization name is {0}, {1} address",
        "{0} - {1}",
        "we are {0} from {1}",
        "it's {0}, our office is in {1}",
        "{0} headquartered in {1}"
    };

    // Ways to provide contact info
    private readonly string[] _contactResponses = {
        "{0}, {1}, im the {2}",
        "{0}, {1}, {2}",
        "phone is {0}, email {1}, I'm the {2}",
        "my number is {0}, email is {1}, position is {2}",
        "{0} and {1}, I work as {2}",
        "contact: {0}, {1} - {2}",
        "reach me at {0} or {1}, I'm {2}",
        "{0}, {1}, serving as {2}"
    };

    #endregion

    /// <summary>
    /// Generates a varied test conversation with comprehensive coverage of different scenarios
    /// </summary>
    public IEnumerable<string> GenerateTestConversation()
    {
        // Generate persona
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";

        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";

        var position = _positions[_random.Next(_positions.Length)];
        var suburb = _suburbs[_random.Next(_suburbs.Length)];
        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, companyBase);

        // Generate event details
        var isExistingCustomer = _random.Next(100) < 20; // 20% chance of existing customer
        var eventType = _eventTypes[_random.Next(_eventTypes.Length)];
        var roomSetup = _roomSetups[_random.Next(_roomSetups.Length)];
        var roomType = _roomTypes[_random.Next(_roomTypes.Length)];
        var attendeeInfo = _attendeeCounts[_random.Next(_attendeeCounts.Length)];
        var attendeeCount = _random.Next(attendeeInfo.min, attendeeInfo.max + 1);

        // Generate date in various formats
        var eventDate = GenerateEventDate();
        var dateString = FormatDateRandomly(eventDate);

        // Build conversation
        var messages = new List<string>();

        // 1. Name response (varied style)
        var nameTemplate = _nameResponses[_random.Next(_nameResponses.Length)];
        messages.Add(string.Format(nameTemplate, fullName));

        // 2. New/existing customer response
        if (isExistingCustomer)
        {
            var existingTemplate = _existingCustomerResponses[_random.Next(_existingCustomerResponses.Length)];
            messages.Add(string.Format(existingTemplate, companyName));
        }
        else
        {
            messages.Add(_newCustomerResponses[_random.Next(_newCustomerResponses.Length)]);
        }

        // 3. Organization info (varied style)
        var orgTemplate = _orgResponses[_random.Next(_orgResponses.Length)];
        messages.Add(string.Format(orgTemplate, companyName, suburb));

        // 4. Contact info (varied style)
        var contactTemplate = _contactResponses[_random.Next(_contactResponses.Length)];
        messages.Add(string.Format(contactTemplate, phone, email, position.ToLower()));

        // 5. Event details (comprehensive or split)
        messages.Add(GenerateEventMessage(eventType, roomType, roomSetup, attendeeCount, dateString));

        // 6. Event-focused equipment responses (multiple messages answering the bot's smart questions)
        var equipmentResponses = GenerateEventFocusedEquipmentResponses(eventType, attendeeCount);
        foreach (var response in equipmentResponses)
        {
            messages.Add(response);
        }

        return messages;
    }

    /// <summary>
    /// Generates a comprehensive test conversation with edge cases and complex scenarios
    /// </summary>
    public IEnumerable<string> GenerateComplexTestConversation()
    {
        var baseMessages = GenerateTestConversation().ToList();
        
        // Add some follow-up complexity randomly
        if (_random.Next(100) < 50)
        {
            // Add a change request
            baseMessages.Add(GenerateChangeRequest());
        }

        return baseMessages;
    }

    #region Helper Methods

    private string GeneratePhoneNumber()
    {
        var formats = new[]
        {
            $"04{_random.Next(10000000, 99999999)}",
            $"04 {_random.Next(1000, 9999)} {_random.Next(1000, 9999)}",
            $"+61 4{_random.Next(10000000, 99999999)}",
            $"0{_random.Next(2, 8)} {_random.Next(1000, 9999)} {_random.Next(1000, 9999)}"
        };
        return formats[_random.Next(formats.Length)];
    }

    private string GenerateEmail(string firstName, string lastName, string company)
    {
        var cleanCompany = company.ToLower().Replace(" ", "").Replace("'", "");
        var formats = new[]
        {
            $"{firstName.ToLower()}.{lastName.ToLower()}@{cleanCompany}.com",
            $"{firstName.ToLower()}@{cleanCompany}.com",
            $"{firstName.ToLower()[0]}{lastName.ToLower()}@{cleanCompany}.com",
            $"{firstName.ToLower()}.{lastName.ToLower()}@{cleanCompany}.com.au",
            $"{firstName.ToLower()}_{lastName.ToLower()}@{cleanCompany}.com"
        };
        return formats[_random.Next(formats.Length)];
    }

    private DateTime GenerateEventDate()
    {
        // Generate dates 1-12 months ahead, with some variation
        var daysAhead = _random.Next(14, 365); // 2 weeks to 1 year ahead
        return DateTime.Now.AddDays(daysAhead);
    }

    private string FormatDateRandomly(DateTime date)
    {
        var formats = new[]
        {
            date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), // 8 January 2026
            date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture), // January 8, 2026
            date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), // 08/01/2026
            date.ToString("d MMM yyyy", CultureInfo.InvariantCulture), // 8 Jan 2026
            $"the {date.Day}{GetDaySuffix(date.Day)} of {date.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}", // the 8th of January 2026
            $"{date.ToString("dddd", CultureInfo.InvariantCulture)} {date.ToString("d MMMM", CultureInfo.InvariantCulture)}", // Wednesday 8 January
            $"next {date.ToString("MMMM", CultureInfo.InvariantCulture)} on the {date.Day}{GetDaySuffix(date.Day)}" // next January on the 8th
        };
        return formats[_random.Next(formats.Length)];
    }

    private static string GetDaySuffix(int day)
    {
        if (day >= 11 && day <= 13) return "th";
        return (day % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }

    private string GenerateEventMessage(string eventType, string roomType, string roomSetup, int attendeeCount, string dateString)
    {
        // Generate various event message formats
        var templates = new[]
        {
            // Full details in one message
            $"{eventType}, im gonna need the venue from you guys, Westin Brisbane and {roomType} in {roomSetup} for {attendeeCount} people, on the {dateString}",
            $"planning a {eventType} at Westin Brisbane, {roomType}, {roomSetup}, {attendeeCount} attendees, {dateString}",
            $"we're hosting a {eventType} on {dateString}. Need Westin Brisbane {roomType}, {roomSetup} setup for about {attendeeCount} people",
            $"{eventType} for {attendeeCount} people. Date is {dateString}. Venue: Westin Brisbane, {roomType}, {roomSetup}",
            $"it's a {eventType} with around {attendeeCount} attendees. {dateString} at Westin Brisbane {roomType}. {roomSetup} please",
            
            // More casual/brief
            $"{eventType}, {attendeeCount} people, {dateString}, Westin Brisbane {roomType}",
            $"doing a {eventType} on {dateString} for {attendeeCount} pax at the Westin",
            
            // More formal/detailed
            $"We are organizing a {eventType} scheduled for {dateString}. We expect approximately {attendeeCount} attendees and require {roomType} at the Westin Brisbane in a {roomSetup} arrangement.",
            $"I need to book for a {eventType}. The event date is {dateString}, we're expecting {attendeeCount} attendees, and we'd like {roomType} at the Westin Brisbane configured in {roomSetup}.",
            
            // With additional context
            $"{eventType} on {dateString}. It's for our team of {attendeeCount} people. Westin Brisbane {roomType} in {roomSetup} would be perfect",
            $"planning our annual {eventType}. {dateString}, {attendeeCount} people expected. Need the Westin Brisbane {roomType}, {roomSetup}"
        };

        return templates[_random.Next(templates.Length)];
    }

    /// <summary>
    /// Generates event-focused responses that answer the bot's smart questions.
    /// Instead of listing equipment, users answer questions about their event activities.
    /// </summary>
    private List<string> GenerateEventFocusedEquipmentResponses(string eventType, int attendeeCount)
    {
        var responses = new List<string>();
        
        // Determine event characteristics
        bool isSocialEvent = eventType.Contains("dinner") || eventType.Contains("gala") || 
                           eventType.Contains("wedding") || eventType.Contains("reception") ||
                           eventType.Contains("party") || eventType.Contains("fundraiser");
        
        bool isConference = eventType.Contains("conference") || eventType.Contains("seminar") ||
                           eventType.Contains("summit") || eventType.Contains("meeting") ||
                           eventType.Contains("presentation") || eventType.Contains("training") ||
                           eventType.Contains("workshop");
        
        bool isHackathon = eventType.Contains("hackathon") || eventType.Contains("coding");
        
        // Generate speaker count based on event type and size
        int speakerCount = isHackathon ? _random.Next(3, 8) :
                          isConference ? _random.Next(2, 6) :
                          isSocialEvent ? _random.Next(1, 3) :
                          _random.Next(1, 5);

        // Response to "Will there be presentations/speeches?"
        var presentationResponses = isSocialEvent 
            ? new[] {
                $"yes, we'll have {speakerCount} speeches",
                $"just {speakerCount} short speeches",
                $"yes, {speakerCount} people will be giving toasts/speeches",
                $"yeah about {speakerCount} speakers for speeches"
            }
            : new[] {
                $"yes, we have {speakerCount} presenters",
                $"yes about {speakerCount} different speakers presenting",
                $"definitely, {speakerCount} presentations throughout the day",
                $"yes we'll have multiple presentations, maybe {speakerCount} speakers",
                $"yeah {speakerCount} people will be presenting"
            };
        
        responses.Add(presentationResponses[_random.Next(presentationResponses.Length)]);

        // Response to "Will you need slides/videos displayed?"
        if (!isSocialEvent)
        {
            var needsVisuals = _random.Next(100) < 85; // 85% need visuals for non-social events
            var visualResponses = needsVisuals 
                ? new[] {
                    "yes definitely",
                    "yes they'll be showing slides",
                    "yes we need to display presentations",
                    "yes, there will be slideshows and videos",
                    "yep, powerpoint presentations"
                }
                : new[] {
                    "no, just talking",
                    "no slides needed",
                    "no visuals, just speakers"
                };
            responses.Add(visualResponses[_random.Next(visualResponses.Length)]);
        }
        else
        {
            // Social events usually don't need slides
            if (_random.Next(100) < 20) // 20% chance they want visuals at social event
            {
                responses.Add(new[] { 
                    "yes we'd like to show a slideshow/video", 
                    "yes, we have a photo montage to display" 
                }[_random.Next(2)]);
            }
            else
            {
                responses.Add(new[] { 
                    "no slides, just speeches", 
                    "no", 
                    "no visual presentations needed" 
                }[_random.Next(3)]);
            }
        }

        // Response about laptops (only for conferences/hackathons/training)
        if (isConference || isHackathon)
        {
            var needsLaptops = _random.Next(100) < 70; // 70% need laptops provided
            if (needsLaptops)
            {
                var laptopResponses = new[] {
                    "yes please provide laptops",
                    "yes we need presentation laptops",
                    "yes, speakers won't have their own",
                    "please provide them, Windows preferred",
                    "yes please, Windows laptops",
                    "yes, Mac if you have them",
                    "yes please - Windows ones"
                };
                responses.Add(laptopResponses[_random.Next(laptopResponses.Length)]);
            }
            else
            {
                responses.Add(new[] {
                    "no, presenters will bring their own",
                    "they'll have their own laptops",
                    "no, speakers are bringing their own"
                }[_random.Next(3)]);
            }
        }

        // Response about sound system (especially for social events)
        if (isSocialEvent)
        {
            var needsMusic = _random.Next(100) < 90; // 90% want music at social events
            if (needsMusic)
            {
                var audioResponses = new[] {
                    "yes please, we need background music",
                    "yes we'll need a sound system for music and speeches",
                    "definitely need audio for music",
                    "yes, need good speakers for music"
                };
                responses.Add(audioResponses[_random.Next(audioResponses.Length)]);
            }
        }

        // Response about lighting (more common for social events)
        if (isSocialEvent && _random.Next(100) < 60) // 60% want lighting at social events
        {
            var lightingResponses = new[] {
                "yes that would be great",
                "yes please, some nice ambient lighting",
                "yes we'd love mood lighting",
                "yes, uplighting would be nice"
            };
            responses.Add(lightingResponses[_random.Next(lightingResponses.Length)]);
        }
        else if (!isSocialEvent && _random.Next(100) < 20) // 20% want lighting at conferences
        {
            responses.Add("yes, some stage lighting would be good");
        }

        return responses;
    }

    private string GenerateChangeRequest()
    {
        var changes = new[]
        {
            "actually can we add one more laptop?",
            "wait, make that 3 projectors instead",
            "also need a lectern if you have one",
            "can we also get a PA system?",
            "I forgot to mention we need stage lighting too",
            "one more thing - we need a backup laptop",
            "actually we might need more mics, make it 5 please",
            "can you add video recording equipment?",
            "we'll also need webcams for video conferencing",
            "sorry, can we change to Mac laptops instead?"
        };
        return changes[_random.Next(changes.Length)];
    }

    #endregion

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
    public IEnumerable<string> GenerateAiStopsRespondingBugConversation()
    {
        // Generate randomized but realistic data
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        
        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";
        
        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, companyBase);
        var position = _positions[_random.Next(_positions.Length)].ToLower();
        
        // Generate random Australian state
        var states = new[] { "nsw", "vic", "qld", "sa", "wa", "tas", "nt", "act" };
        var state = states[_random.Next(states.Length)];
        
        // Generate event type (year-end meetings are common)
        var meetingTypes = new[] { 
            "a year end meeting", 
            "an end of year meeting", 
            "our annual meeting", 
            "a quarterly review meeting",
            "a board meeting",
            "a team planning session"
        };
        var meetingType = meetingTypes[_random.Next(meetingTypes.Length)];
        
        // Generate random attendee count (10-50 for boardroom)
        var attendeeCount = _random.Next(10, 51);
        
        // Generate random event date in the future
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 90));
        var monthName = eventDate.ToString("MMMM").ToLower();
        var day = eventDate.Day;
        
        // Generate schedule times
        var setupHour = _random.Next(7, 10);
        var rehearsalHour = setupHour + 1;
        var startHour = rehearsalHour + 1;
        var endHour = _random.Next(15, 18);
        var packupHour = endHour + 1;
        
        var dayOfWeek = eventDate.ToString("dddd");
        var dateFormatted = eventDate.ToString("MMMM d, yyyy");
        
        // Return the exact conversation flow that triggers the bug
        return new[]
        {
            // Step 1: User provides name (various formats)
            fullName,
            
            // Step 2: User indicates new customer (various ways to say it)
            new[] { "new one", "new", "new customer", "first time", "nope, new" }[_random.Next(5)],
            
            // Step 3: User provides organization and location
            $"{companyName.ToLower()}, {state}",
            
            // Step 4: User provides contact details (phone, email, position)
            $"{phone},{email},{position}",
            
            // Step 5: User describes event type
            meetingType,
            
            // Step 6: User asks for venue recommendation
            new[] { 
                "can i have your recommendation", 
                "what do you recommend?", 
                "any suggestions?",
                "what venues do you have?",
                "show me the options"
            }[_random.Next(5)],
            
            // Step 7: User selects Thrive Boardroom
            new[] {
                "I like the thrive boardroom",
                "thrive boardroom please",
                "the thrive boardroom looks good",
                "I'll go with thrive boardroom"
            }[_random.Next(4)],
            
            // Step 8: User selects boardroom layout
            "boardroom",
            
            // Step 9: User provides date and attendee count
            $"it's on {monthName} {day} and have around {attendeeCount} attendees",
            
            // Step 10: User confirms schedule selection (THIS IS WHERE THE BUG OCCURS)
            // The AI shows the time picker, user confirms, and then AI stops responding
            $"I've selected this schedule: on {dayOfWeek}, {dateFormatted}: Setup {setupHour}AM; Rehearsal {rehearsalHour}AM; Event Start {startHour}AM; Event End {endHour - 12}PM; Pack Up {packupHour - 12}PM. Please confirm this schedule.",
            
            // Step 11: Additional messages to test if AI continues responding
            // These messages should get responses, but the bug causes the AI to stop
            "yes that looks good",
            "we'll need a projector and microphones",
            "hello? are you there?"
        };
    }

    #endregion

    /// <summary>
    /// Generates a test case specifically for time picker validation testing
    /// This includes scenarios that will trigger the time picker and test validation
    /// </summary>
    public IEnumerable<string> GenerateTimePickerValidationConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));

        // This conversation will trigger the time picker, and we can test validation
        return new[]
        {
            $"hi, I'm {fullName}",
            "new customer",
            $"{company}, Brisbane CBD",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@test.com, event coordinator",
            $"boardroom meeting at Westin Brisbane on {eventDate:d MMMM yyyy}. We expect 20 attendees",
            // Event-focused responses:
            "yes there will be 2 presentations",
            "yes we need to show slides",
            "yes please provide a laptop, Windows is fine",
            // This will trigger the time picker - user provides start/end times
            "start time at 9am and finish time at 5pm"
        };
    }

    #endregion
}
