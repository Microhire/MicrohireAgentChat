using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Tracks what information has been provided vs missing in the conversation
/// </summary>
public sealed class ConversationStateService
{
    private readonly ConversationExtractionService _extraction;

    public ConversationStateService(ConversationExtractionService extraction)
    {
        _extraction = extraction;
    }

    /// <summary>
    /// Analyze conversation and determine what information is available vs missing
    /// </summary>
    public ConversationState GetConversationState(IEnumerable<DisplayMessage> messages)
    {
        var state = new ConversationState();

        // Extract event type
        var (eventType, _) = _extraction.ExtractEventType(messages);
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            state.EventType = new InformationState { Status = InfoStatus.Extracted, Value = eventType };
        }

        // Extract contact information
        var contactInfo = _extraction.ExtractContactInfo(messages);
        if (!string.IsNullOrWhiteSpace(contactInfo.Name))
        {
            state.ContactInfo = new InformationState { Status = InfoStatus.Extracted, Value = contactInfo.Name };
        }

        // Extract venue/date information
        var (eventDate, venueName, _, _) = _extraction.ExtractVenueAndEventDate(messages);
        if (eventDate.HasValue)
        {
            state.Dates = new InformationState { Status = InfoStatus.Extracted, Value = eventDate.Value.ToString("yyyy-MM-dd") };
        }
        if (!string.IsNullOrWhiteSpace(venueName))
        {
            state.VenueInfo = new InformationState { Status = InfoStatus.Extracted, Value = venueName };
        }

        // Extract comprehensive information
        var eventInfo = _extraction.ExtractAllEventInformation(messages);
        if (eventInfo.Attendees.HasValue)
        {
            state.Attendees = new InformationState { Status = InfoStatus.Extracted, Value = eventInfo.Attendees.Value.ToString() };
        }
        if (eventInfo.Budget.HasValue)
        {
            state.Budget = new InformationState { Status = InfoStatus.Extracted, Value = eventInfo.Budget.Value.ToString("C") };
        }

        return state;
    }

    /// <summary>
    /// Get list of missing information fields
    /// </summary>
    public List<string> GetMissingFields(ConversationState state)
    {
        var missing = new List<string>();

        if (state.EventType?.Status != InfoStatus.Extracted)
            missing.Add("event_type");
        if (state.ContactInfo?.Status != InfoStatus.Extracted)
            missing.Add("contact_info");
        if (state.VenueInfo?.Status != InfoStatus.Extracted)
            missing.Add("venue");
        if (state.Dates?.Status != InfoStatus.Extracted)
            missing.Add("dates");
        if (state.Attendees?.Status != InfoStatus.Extracted)
            missing.Add("attendees");

        return missing;
    }

    /// <summary>
    /// Check if event type has been provided
    /// </summary>
    public bool HasEventType(ConversationState state) =>
        state.EventType?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if contact info has been provided
    /// </summary>
    public bool HasContactInfo(ConversationState state) =>
        state.ContactInfo?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if venue has been provided
    /// </summary>
    public bool HasVenueInfo(ConversationState state) =>
        state.VenueInfo?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if dates have been provided
    /// </summary>
    public bool HasDates(ConversationState state) =>
        state.Dates?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if attendee count has been provided
    /// </summary>
    public bool HasAttendees(ConversationState state) =>
        state.Attendees?.Status == InfoStatus.Extracted;
}

/// <summary>
/// Current state of information extracted from conversation
/// </summary>
public class ConversationState
{
    public InformationState? EventType { get; set; }
    public InformationState? ContactInfo { get; set; }
    public InformationState? VenueInfo { get; set; }
    public InformationState? Dates { get; set; }
    public InformationState? Attendees { get; set; }
    public InformationState? Budget { get; set; }
}

/// <summary>
/// Status of a piece of information
/// </summary>
public enum InfoStatus
{
    Missing,
    Extracted,
    Confirmed
}

/// <summary>
/// State of a specific piece of information
/// </summary>
public class InformationState
{
    public InfoStatus Status { get; set; }
    public string? Value { get; set; }
}