using System.Text.Json;

namespace MicrohireAgentChat.Services;
public sealed class EventFlowState
{
    public enum Step
    {
        Start,
        AskEventType,
        AskOrganizedBefore,
        AskHappyLastTime,
        AskDate,
        AskVenue,
        ClarifyWestin,      // user said just “Brisbane”
        AskBallroom,        // Westin Brisbane confirmed
        AskGuestCount,
        AskTheme,
        AskSpeakersCount,
        AskOneFileOrMultiple,
        AskTimes,
        AskItinerary,
        SummaryAndNext
    }

    public Step Current { get; set; } = Step.Start;

    static bool LooksWestinBrisbane(string s) =>
        s.Contains("westin", StringComparison.OrdinalIgnoreCase) &&
        s.Contains("brisbane", StringComparison.OrdinalIgnoreCase);

    static bool LooksAmbiguousCity(string s)
    {
        s = s.Trim();
        if (s.Equals("brisbane", StringComparison.OrdinalIgnoreCase)) return true;
        return s.Contains("brisbane", StringComparison.OrdinalIgnoreCase) &&
               !s.Contains("westin", StringComparison.OrdinalIgnoreCase);
    }

    public void Advance(string userText)
    {
        userText ??= "";

        switch (Current)
        {
            case Step.Start: Current = Step.AskEventType; break;
            case Step.AskEventType: Current = Step.AskOrganizedBefore; break;
            case Step.AskOrganizedBefore: Current = Step.AskHappyLastTime; break;
            case Step.AskHappyLastTime: Current = Step.AskDate; break;
            case Step.AskDate: Current = Step.AskVenue; break;

            case Step.AskVenue:
                if (LooksWestinBrisbane(userText)) Current = Step.AskBallroom;
                else if (LooksAmbiguousCity(userText)) Current = Step.ClarifyWestin;
                else Current = Step.AskTheme;
                break;

            case Step.ClarifyWestin:
                Current = LooksWestinBrisbane(userText) ? Step.AskBallroom : Step.AskTheme;
                break;

            case Step.AskBallroom: Current = Step.AskGuestCount; break;
            case Step.AskGuestCount: Current = Step.AskTheme; break;
            case Step.AskTheme: Current = Step.AskSpeakersCount; break;
            case Step.AskSpeakersCount: Current = Step.AskOneFileOrMultiple; break;
            case Step.AskOneFileOrMultiple: Current = Step.AskTimes; break;
            case Step.AskTimes: Current = Step.AskItinerary; break;
            case Step.AskItinerary: Current = Step.SummaryAndNext; break;
            case Step.SummaryAndNext: Current = Step.SummaryAndNext; break;
        }
    }

    public string NextQuestion() => Current switch
    {
        Step.AskEventType => "What type of event are you planning?",
        Step.AskOrganizedBefore => "Have you organized this type of event before?",
        Step.AskHappyLastTime => "Were you happy with how it turned out last time?",
        Step.AskDate => "What date would you like to hold the event?",
        Step.AskVenue => "What venue do you have in mind for the event?",
        Step.ClarifyWestin => "Is the venue The Westin Brisbane?",
        Step.AskBallroom => "Will you be hosting it in the Westin Ballroom?",
        Step.AskGuestCount => "How many people will be attending?",
        Step.AskTheme => "Do you have a theme or objective for the night?",
        Step.AskSpeakersCount => "How many people will speak in addition to the CEO?",
        Step.AskOneFileOrMultiple => "Will presentations be in one file or multiple files?",
        Step.AskTimes => "Do you have the setup, rehearsal, start, end and pack-up times?",
        Step.AskItinerary => "Do you have an itinerary to upload for the team?",
        Step.SummaryAndNext => "Would you like a technician on site for the whole evening, or just setup and handover?",
        _ => "What type of event are you planning?"
    };

    // ---- session helpers ----
    private const string SessionKey = "EventFlowState";
    public static EventFlowState Load(ISession session)
    {
        var json = session.GetString(SessionKey);
        return string.IsNullOrEmpty(json)
            ? new EventFlowState()
            : (JsonSerializer.Deserialize<EventFlowState>(json) ?? new EventFlowState());
    }
    public void Save(ISession session) =>
        session.SetString(SessionKey, JsonSerializer.Serialize(this));
}
