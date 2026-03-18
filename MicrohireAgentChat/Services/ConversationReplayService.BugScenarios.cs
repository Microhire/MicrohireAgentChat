using System.Globalization;

namespace MicrohireAgentChat.Services;

public partial class ConversationReplayService
{
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

    /// <summary>
    /// BUG REPRODUCTION: Time Picker Display Order and Inappropriate Triggering
    /// 
    /// This scenario reproduces the bug where:
    /// 1. AI asks for event schedule/times
    /// 2. User asks a follow-up question about room options/layout instead of answering
    /// 3. Time picker incorrectly appears before its loading message
    /// 4. Time picker interrupts the conversation flow
    /// 
    /// Expected behavior after fix:
    /// - Time picker should NOT appear when user asks room-related questions
    /// - When picker does appear, loading text should be above the picker UI
    /// - Conversation flow should remain natural
    /// </summary>
    public IEnumerable<string> GenerateTimePickerDisplayOrderBugConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateFormatted = eventDate.ToString("d MMMM yyyy");
        var attendeeCount = _random.Next(15, 30);

        return new[]
        {
            // Step 1: Initial greeting and contact info
            $"hi, I'm {fullName}",
            "new customer",
            $"{company}, Brisbane CBD",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}@test.com, event coordinator",
            
            // Step 2: Event details
            $"boardroom meeting at Westin Brisbane on {dateFormatted}. We expect {attendeeCount} attendees",
            
            // Step 3: Equipment needs
            "yes there will be 2 presentations",
            "yes we need to show slides",
            "yes please provide a laptop, Windows is fine",
            
            // Step 4: AI asks for schedule/times (this triggers the time picker check)
            // The AI will ask something like "What time would you like your event to start and end?"
            
            // Step 5: USER ASKS ROOM QUESTION INSTEAD OF ANSWERING (THIS IS THE BUG SCENARIO)
            // This should NOT trigger the time picker - it's a follow-up question
            "what are the options for this room?",
            
            // Step 6: User might ask more room-related questions
            "can you explain the room layout?",
            
            // Step 7: Finally user provides schedule info (this SHOULD trigger picker)
            "the event starts at 9am and ends at 5pm"
        };
    }

    /// <summary>
    /// BUG REPRODUCTION: Skip Contact Info Before Quote
    /// 
    /// This scenario tests that the AI blocks quote generation when contact info is missing.
    /// The user deliberately skips providing name, email/phone, and organisation,
    /// but proceeds through venue, schedule, and equipment selection.
    /// The AI should block the quote summary until contact info is collected.
    /// </summary>
    public IEnumerable<string> GenerateSkipContactInfoConversation()
    {
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateFormatted = eventDate.ToString("d MMMM yyyy");
        var attendeeCount = _random.Next(8, 20);

        // This conversation deliberately skips contact info
        // User tries to jump straight to event details
        return new[]
        {
            // Skip name/contact, jump to event request
            "i want to have an event board room style please with 8 people, what tech can I have with this?",
            
            // Skip providing contact info, ask for details instead
            "can i have details please?",
            
            // Ask about venue options
            "what are the rooms available in westin brisbane?",
            
            // Select a room
            "thrive boardroom please",
            
            // Agree to proceed
            "yes please",
            
            // Confirm speeches and presentations
            "yes there will be speeches and presentations",
            
            // Provide event date
            $"event is on {dateFormatted}",
            
            // Confirm video with audio needs
            "yes there will be videos with audio. Please provide laptop",
            
            // Select Windows laptop
            "windows please",
            
            // Try to create the quote - this is where validation should kick in
            "yes create the quote please"
        };
    }

    /// <summary>
    /// BUG REPRODUCTION: Quote Generation Issues
    /// 
    /// This scenario tests multiple quote generation edge cases that were reported:
    /// 1. Multiple "yes create quote" attempts with retry loops
    /// 2. Context memory issues - Teams/video call mentioned but not included in equipment
    /// 3. AI going back and forth about success/failure
    /// 4. "A quote was prepared" when nothing happened
    /// 5. Missing equipment from earlier conversation
    /// 
    /// The conversation deliberately includes:
    /// - Teams/Zoom call requirements early in conversation
    /// - Camera/recording requests
    /// - Full flow through to quote confirmation
    /// - Multiple confirmation attempts to test idempotency
    /// </summary>
    public IEnumerable<string> GenerateQuoteGenerationIssuesConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        
        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";
        
        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, companyBase);
        var position = _positions[_random.Next(_positions.Length)].ToLower();
        var suburb = _suburbs[_random.Next(_suburbs.Length)];
        
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateFormatted = eventDate.ToString("d MMMM yyyy");
        var attendeeCount = _random.Next(8, 25);
        
        // Variation in how they mention Teams/video call
        var videoCallMentions = new[]
        {
            "we'll have a Teams call with remote participants",
            "there will be people joining via Zoom",
            "we need video conferencing with remote attendees",
            "some attendees will join remotely via Teams",
            "it's a hybrid meeting with Teams participants"
        };
        var videoCallMention = videoCallMentions[_random.Next(videoCallMentions.Length)];
        
        // Variation in camera/recording requests
        var cameraRequests = new[]
        {
            "and we want to record the session",
            "we'd like to record the meeting",
            "we need a camera for the video call",
            "please include a webcam for the call",
            "we need to record and broadcast"
        };
        var cameraRequest = cameraRequests[_random.Next(cameraRequests.Length)];

        return new[]
        {
            // Step 1: Introduction WITH video call mention (context memory test)
            $"Hi, I'm {fullName}. We're planning a boardroom meeting and {videoCallMention} {cameraRequest}",
            
            // Step 2: New/existing customer
            "new customer",
            
            // Step 3: Organisation info
            $"{companyName}, {suburb}",
            
            // Step 4: Contact info
            $"{phone}, {email}, {position}",
            
            // Step 5: Event details (should already have video call context from step 1)
            $"boardroom meeting at Westin Brisbane, {attendeeCount} attendees, {dateFormatted}",
            
            // Step 6: Room selection
            "thrive boardroom please",
            
            // Step 7: Confirm schedule
            "boardroom style setup",
            
            // Step 8: Provide times
            "event starts at 9am, ends at 4pm",
            
            // Step 9: Confirm presentations - remind about video call again
            "yes there will be 2 presenters. Remember we have remote attendees joining via video call",
            
            // Step 10: Confirm slides needed
            "yes we need to show slides and share screen with remote participants",
            
            // Step 11: Laptop preference
            "yes please provide a Mac laptop",
            
            // Step 12: First quote confirmation (should trigger quote generation)
            "yes create quote",
            
            // Step 13: Second confirmation (tests idempotency - should return same quote)
            "yes create the quote please",
            
            // Step 14: Third confirmation (edge case - repeated requests)
            "yes create quote now",
            
            // Step 15: Ask about what was included (tests if camera/video equipment included)
            "can you confirm the quote includes the camera and video conferencing equipment?"
        };
    }

    /// <summary>
    /// BUG REPRODUCTION: Thrive Boardroom + Zoom/Teams should ask, never assume
    ///
    /// Deterministic flow based on reported production transcript where the assistant:
    /// - Assumes camera/mic/speakers after "Zoom/Teams" mention
    /// - Re-asks fixed Thrive boardroom setup repeatedly
    /// - Can end in "temporary issue" fallback loop
    ///
    /// Expected behavior:
    /// 1) Keep Thrive setup fixed as boardroom without re-confirm loops
    /// 2) Ask explicit clarifying AV questions for video call gear; do not auto-add
    /// 3) Continue to quote summary flow without hanging
    /// </summary>
    public IEnumerable<string> GenerateThriveZoomNoAssumptionConversation()
    {
        return new[]
        {
            // Contact onboarding
            "Daniel Morel",
            "new",
            "Morel Enterprises",
            "Brisbane",
            "daniel@morel.co",

            // Event context
            "meeting",
            "thrive",
            "7 April 2026",
            "8",

            // Time picker submit in server-parsed format (avoids formatting drift)
            "Choose schedule: date=2026-04-07; setup=07:00; rehearsal=09:30; start=10:00; end=16:00; packup=17:00",

            // Key bug trigger: user mentions Zoom/Teams but does NOT request camera/mic/speakers explicitly
            "Yes a couple of laptops and we will be sharing presentations on screen with attendees in the room and others globally via zoom and teams",

            // User follows up with additional optional equipment while bot should still clarify laptop + VC specifics
            "Yes a clicker would be good",
            "A flipchart would be good too",
            "2 laptops",
            "what are my options",
            "mac please",
            "yes"
        };
    }

    /// <summary>
    /// FIX VERIFICATION: Schedule not recognised after time picker submit
    ///
    /// Verifies the fix where the agent was asking for the schedule again after the user
    /// had already submitted it via the time picker ("It seems there might be an issue with
    /// the system recognising your schedule"). The replay sends the raw "Choose schedule: ..."
    /// message so the server parses it, saves to session, and adds the reformatted
    /// "I've selected this schedule: ..." to the thread. Then we continue with AV and quote;
    /// the agent must not ask for schedule again or block quote with "I need your event schedule".
    /// </summary>
    public IEnumerable<string> GenerateScheduleRecognitionFixConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";
        var company = $"{_companyNames[_random.Next(_companyNames.Length)]}{_companySuffixes[_random.Next(_companySuffixes.Length)]}";
        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateStr = eventDate.ToString("yyyy-MM-dd");
        var attendeeCount = _random.Next(6, 12);

        return new[]
        {
            $"Hi I'm {fullName}. We need a boardroom meeting at Westin Brisbane, {attendeeCount} people, {eventDate:d MMMM yyyy}",
            "new customer",
            $"{company}, Brisbane",
            $"04{_random.Next(10000000, 99999999)}, {firstName.ToLower()}.{lastName.ToLower()}@test.com, founder",
            // Raw time picker submit - server will parse and replace with "I've selected this schedule: ..."
            $"Choose schedule: date={dateStr}; setup=07:00; rehearsal=09:30; start=10:00; end=16:00; packup=18:00",
            "boardroom style",
            "yes there will be 2 presenters with slides",
            "Mac laptop please",
            "yes need audio",
            "yep all good",
            "Yes, create quote"
        };
    }

    /// <summary>
    /// BUG REPRODUCTION / FIX VERIFICATION: Speaker Style Question Order
    ///
    /// Verifies the AV follow-up sequence for Ballroom/Elevate rooms:
    /// 1) User confirms presentation audio playback is needed
    /// 2) Assistant must ask speaker style (inbuilt vs external/portable PA)
    /// 3) Assistant asks laptop question only after speaker style is captured
    /// </summary>
    public IEnumerable<string> GenerateSpeakerStyleOrderConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";

        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";

        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, companyBase);
        var position = _positions[_random.Next(_positions.Length)].ToLower();

        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateStr = eventDate.ToString("yyyy-MM-dd");
        var dateFormatted = eventDate.ToString("d MMMM yyyy");
        var attendeeCount = _random.Next(20, 61);

        return new[]
        {
            $"Hi I'm {fullName}. We're planning a presentation event at Westin Brisbane for {attendeeCount} attendees on {dateFormatted}",
            "new customer",
            $"{companyName}, Brisbane",
            $"{phone}, {email}, {position}",
            "Elevate 1",
            "theatre style",
            $"Choose schedule: date={dateStr}; setup=08:00; rehearsal=08:30; start=09:00; end=16:00; packup=17:00",
            "yes there will be 3 presenters",
            "yes we need to show slides and videos",
            // This user reply should trigger speaker style follow-up before laptop questions.
            "yes the presentation includes audio playback",
            "external portable PA speakers please",
            "we'll bring our own laptop",
            "yes we need a USBC adaptor",
            "yes create quote"
        };
    }

    /// <summary>
    /// BUG REPRODUCTION: Westin Brisbane Room-Specific AV Packages
    ///
    /// Verifies the fix for AI not recommending built-in venue-installed AV packages.
    /// Previously the AI recommended generic portable projectors/speakers instead of
    /// room-specific packages (e.g. Westin Ballroom Dual Projector Package, Elevate
    /// Ceiling Speaker System, Thrive AV Package).
    /// 
    /// This scenario tests that when venue is Westin Brisbane and room is specified,
    /// the AI recommends the correct WSB packages from venue-room-packages.json.
    /// </summary>
    public IEnumerable<string> GenerateWestinBrisbaneRoomPackagesConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";

        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";

        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, companyBase);
        var position = _positions[_random.Next(_positions.Length)].ToLower();

        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateFormatted = eventDate.ToString("d MMMM yyyy");

        // Rotate through rooms to test: Westin Ballroom, Elevate, Thrive Boardroom
        var rooms = new[] { "Westin Ballroom", "Elevate", "Thrive Boardroom" };
        var room = rooms[_random.Next(rooms.Length)];

        return new[]
        {
            // Step 1: Introduction with event and venue
            $"Hi I'm {fullName}. We need to book a conference at Westin Brisbane",
            "new customer",
            $"{companyName}, Brisbane",
            $"{phone}, {email}, {position}",
            // Event type
            "conference with 80 attendees",
            // Venue
            "Westin Brisbane",
            // Room - critical for room-specific package recommendation
            room.ToLower(),
            // Schedule
            $"event date is {dateFormatted}",
            "setup at 8am, start 9am, finish 5pm",
            // Presentations and equipment
            "yes there will be 3 speakers with presentations",
            "yes we need projectors and screens to show slides",
            "yes we need speakers for the presentations",
            "Windows laptop please",
            // Should trigger recommend_equipment_for_event with venue_name + room_name
            // Expected: AI recommends room-specific WSB packages (e.g. Westin Ballroom Dual Projector Package,
            // Westin Full Ballroom Ceiling Speaker System) NOT generic portable equipment
            "yes create the quote"
        };
    }

    /// <summary>
    /// BUG REPRODUCTION / FIX VERIFICATION: Quote Modification
    ///
    /// Covers the fix for "Allow modification of the generated quote":
    /// 1. Edit before first quote: User asks to remove projector/screen and add mic; then confirms create quote.
    ///    Verifies update_equipment is used and generated quote reflects changes.
    /// 2. Optional: After quote is created, user asks to add another mic – verifies regenerate_quote flow.
    /// </summary>
    public IEnumerable<string> GenerateQuoteModificationConversation()
    {
        var firstName = _firstNames[_random.Next(_firstNames.Length)];
        var lastName = _lastNames[_random.Next(_lastNames.Length)];
        var fullName = $"{firstName} {lastName}";

        var companyBase = _companyNames[_random.Next(_companyNames.Length)];
        var companySuffix = _companySuffixes[_random.Next(_companySuffixes.Length)];
        var companyName = $"{companyBase}{companySuffix}";

        var phone = GeneratePhoneNumber();
        var email = GenerateEmail(firstName, lastName, companyBase);
        var position = _positions[_random.Next(_positions.Length)].ToLower();

        var eventDate = DateTime.Now.AddDays(_random.Next(14, 60));
        var dateFormatted = eventDate.ToString("d MMMM yyyy");
        var attendeeCount = _random.Next(6, 12);

        return new[]
        {
            // Step 1: Introduction – boardroom meeting
            $"Hi I'm {fullName}. We need a boardroom meeting at Westin Brisbane, {attendeeCount} people, {dateFormatted}",
            "new customer",
            $"{companyName}, Brisbane",
            $"{phone}, {email}, {position}",
            // Event and room
            "boardroom meeting at Westin Brisbane",
            "Thrive Boardroom",
            // Schedule
            "event starts at 9am, ends at 4pm",
            "boardroom style",
            // Equipment – presentations so AI recommends projector/screen etc.
            "yes there will be 2 presenters with slides",
            "yes we need to show slides",
            "Mac laptop please",
            // User corrects equipment before creating quote (tests update_equipment)
            "The quote seems to be incorrect – can you remove the projector screen and add a mic",
            // Confirm and create quote (quote should reflect mic, no projector/screen)
            "yes create quote",
            // Optional: modify existing quote (tests regenerate_quote)
            "I want to add a second microphone to the quote"
        };
    }

    /// <summary>
    /// FIX VERIFICATION: Image Gallery Preview Modal
    /// 
    /// This scenario triggers an image gallery (rooms/layouts) to test the preview modal.
    /// The user provides all details and asks for room recommendations.
    /// </summary>
    public IEnumerable<string> GenerateImageGalleryPreviewConversation()
    {
        return new[]
        {
            "John Doe",
            "new customer",
            "Test Corp, Brisbane",
            "0400000000, john@testcorp.com, Event Manager",
            "I'm planning a corporate meeting on March 20th 2026 for 25 people at the Westin Brisbane. What rooms do you have available?"
        };
    }

    /// <summary>
    /// FIX VERIFICATION: AI Delay and Memory Issue
    /// 
    /// This scenario tests the fix for the "brief delay" message and memory loss.
    /// It follows a standard flow but is designed to be used for verifying that
    /// the AI doesn't lose context even if a run is interrupted or retried.
    /// </summary>
    public IEnumerable<string> GenerateAiDelayMemoryFixConversation()
    {
        var eventDate = DateTime.Now.AddDays(30);
        var dateStr = eventDate.ToString("yyyy-MM-dd");
        
        return new[]
        {
            "Hi, I'm Alex Johnson from TechCorp. We're a new customer.",
            "0412345678, alex@techcorp.com, Event Manager",
            $"We need a boardroom meeting at Westin Brisbane for 15 people on {eventDate:d MMMM yyyy}.",
            "Thrive Boardroom please.",
            "Boardroom style setup.",
            // This message is often where the delay/timeout might happen during schedule processing
            $"Choose schedule: date={dateStr}; setup=08:00; rehearsal=08:30; start=09:00; end=17:00; packup=18:00",
            "Yes, we'll have 2 presenters with slides.",
            "Windows laptop please.",
            "Yes, create the quote."
        };
    }

    /// <summary>
    /// FIX VERIFICATION: Allow creation of another quote from the same conversation
    ///
    /// Hardcoded flow that reproduces the bug fix: first complete one quote (Westin Ballroom 2, 10 Mar 2026),
    /// then ask for "a new quote for another event", provide a second event (Elevate, team dinner, 4 Apr 2026),
    /// complete schedule and equipment, then "yes create quote". The second quote must get a NEW booking number,
    /// not reuse the first (C1374900124). Verifies LooksLikeNewQuoteIntent + ClearBookingAndQuoteDraftState.
    /// </summary>
    public IEnumerable<string> GenerateAnotherQuoteFromSameConversation()
    {
        // First event: 10 March 2026, Westin Ballroom 2, 100 attendees, presentation
        const string firstDateStr = "2026-03-10";
        return new[]
        {
            // --- First quote: contact and event ---
            "Shanks Sake",
            "new",
            "OP corp, brisbane",
            "shanks@testing.com, 0412659873",
            "CEO",
            "westin brisbane, elevate room",
            "march 10, 2026",
            "100",
            "yes, i'll go with westin ballroom 2",
            // Schedule (raw time picker format – server parses and saves to session)
            $"Choose schedule: date={firstDateStr}; setup=07:00; rehearsal=08:00; start=09:00; end=17:00; packup=18:00",
            "yes, there will be 3 speakers and video presentations too",
            "wireless mic for the speakers and yes, audio is needed",
            "yes please",
            "please provide 1 mac laptop",
            "yes create quote",
            // --- Trigger new-quote intent (must clear Draft:BookingNo so second quote creates new booking) ---
            "Can I ask for a new quote for another event?",
            // --- Second event: different venue/room/date/type ---
            "this time it's a team dinner elevate room on april 4, 2026 with 50 attendees",
            $"Choose schedule: date=2026-04-04; setup=07:00; rehearsal=08:00; start=09:00; end=17:00; packup=18:00",
            "2 speakers with video presentation",
            "yes please",
            "yes create quote"
        };
    }

}
