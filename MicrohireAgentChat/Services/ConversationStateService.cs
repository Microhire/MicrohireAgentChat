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

        // Extract contact information (name, email, phone, position)
        var contactInfo = _extraction.ExtractContactInfo(messages);
        if (!string.IsNullOrWhiteSpace(contactInfo.Name))
        {
            state.ContactName = new InformationState { Status = InfoStatus.Extracted, Value = contactInfo.Name };
        }
        if (!string.IsNullOrWhiteSpace(contactInfo.Email))
        {
            state.ContactEmail = new InformationState { Status = InfoStatus.Extracted, Value = contactInfo.Email };
        }
        if (!string.IsNullOrWhiteSpace(contactInfo.PhoneE164))
        {
            state.ContactPhone = new InformationState { Status = InfoStatus.Extracted, Value = contactInfo.PhoneE164 };
        }
        if (!string.IsNullOrWhiteSpace(contactInfo.Position))
        {
            state.ContactPosition = new InformationState { Status = InfoStatus.Extracted, Value = contactInfo.Position };
        }

        // Extract organisation information
        var (org, orgAddr) = _extraction.ExtractOrganisationFromTranscript(messages);
        if (!string.IsNullOrWhiteSpace(org))
        {
            state.Organisation = new InformationState { Status = InfoStatus.Extracted, Value = org };
        }
        if (!string.IsNullOrWhiteSpace(orgAddr))
        {
            state.OrganisationAddress = new InformationState { Status = InfoStatus.Extracted, Value = orgAddr };
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

        // Extract room (for Westin Brisbane / Four Points Brisbane - needed for room-specific AV packages)
        var roomName = _extraction.ExtractRoom(messages);
        if (!string.IsNullOrWhiteSpace(roomName))
        {
            state.RoomInfo = new InformationState { Status = InfoStatus.Extracted, Value = roomName };
        }

        // Extract schedule times
        var scheduleTimes = _extraction.ExtractScheduleTimes(messages);
        if (scheduleTimes.TryGetValue("show_start_time", out var startTime) && !string.IsNullOrWhiteSpace(startTime))
        {
            state.ScheduleStartTime = new InformationState { Status = InfoStatus.Extracted, Value = startTime };
        }
        if (scheduleTimes.TryGetValue("show_end_time", out var endTime) && !string.IsNullOrWhiteSpace(endTime))
        {
            state.ScheduleEndTime = new InformationState { Status = InfoStatus.Extracted, Value = endTime };
        }
        if (scheduleTimes.TryGetValue("setup_time", out var setupTime) && !string.IsNullOrWhiteSpace(setupTime))
        {
            state.ScheduleSetupTime = new InformationState { Status = InfoStatus.Extracted, Value = setupTime };
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
        if (state.ContactName?.Status != InfoStatus.Extracted)
            missing.Add("contact_name");
        if (state.VenueInfo?.Status != InfoStatus.Extracted)
            missing.Add("venue");
        if (state.Dates?.Status != InfoStatus.Extracted)
            missing.Add("dates");
        if (state.Attendees?.Status != InfoStatus.Extracted)
            missing.Add("attendees");

        return missing;
    }

    /// <summary>
    /// Get list of required fields that must be collected before generating a quote
    /// </summary>
    public List<string> GetRequiredMissingFieldsForQuote(ConversationState state)
    {
        var missing = new List<string>();

        // Contact name is required
        if (state.ContactName?.Status != InfoStatus.Extracted)
            missing.Add("contact_name");

        // Either email or phone is required
        if (state.ContactEmail?.Status != InfoStatus.Extracted && 
            state.ContactPhone?.Status != InfoStatus.Extracted)
            missing.Add("contact_email_or_phone");

        // Organisation is required
        if (state.Organisation?.Status != InfoStatus.Extracted)
            missing.Add("organisation");

        // Event dates are required
        if (state.Dates?.Status != InfoStatus.Extracted)
            missing.Add("event_dates");

        // Schedule times are required (at least start time)
        if (state.ScheduleStartTime?.Status != InfoStatus.Extracted)
            missing.Add("schedule_times");

        return missing;
    }

    /// <summary>
    /// Check if all required fields for quote generation have been collected
    /// </summary>
    public bool IsReadyForQuote(ConversationState state) =>
        GetRequiredMissingFieldsForQuote(state).Count == 0;

    /// <summary>
    /// Check if event type has been provided
    /// </summary>
    public bool HasEventType(ConversationState state) =>
        state.EventType?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if contact name has been provided
    /// </summary>
    public bool HasContactName(ConversationState state) =>
        state.ContactName?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if contact email or phone has been provided
    /// </summary>
    public bool HasContactEmailOrPhone(ConversationState state) =>
        state.ContactEmail?.Status == InfoStatus.Extracted || 
        state.ContactPhone?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if organisation has been provided
    /// </summary>
    public bool HasOrganisation(ConversationState state) =>
        state.Organisation?.Status == InfoStatus.Extracted;

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
    /// Check if schedule times have been provided
    /// </summary>
    public bool HasScheduleTimes(ConversationState state) =>
        state.ScheduleStartTime?.Status == InfoStatus.Extracted;

    /// <summary>
    /// Check if attendee count has been provided
    /// </summary>
    public bool HasAttendees(ConversationState state) =>
        state.Attendees?.Status == InfoStatus.Extracted;

    // Legacy method for backward compatibility
    public bool HasContactInfo(ConversationState state) =>
        state.ContactName?.Status == InfoStatus.Extracted;
}

/// <summary>
/// Current state of information extracted from conversation
/// </summary>
public class ConversationState
{
    // Event information
    public InformationState? EventType { get; set; }
    public InformationState? VenueInfo { get; set; }
    public InformationState? RoomInfo { get; set; }
    public InformationState? Dates { get; set; }
    public InformationState? Attendees { get; set; }
    public InformationState? Budget { get; set; }

    // Contact information (expanded)
    public InformationState? ContactName { get; set; }
    public InformationState? ContactEmail { get; set; }
    public InformationState? ContactPhone { get; set; }
    public InformationState? ContactPosition { get; set; }

    // Organisation information
    public InformationState? Organisation { get; set; }
    public InformationState? OrganisationAddress { get; set; }

    // Schedule times
    public InformationState? ScheduleStartTime { get; set; }
    public InformationState? ScheduleEndTime { get; set; }
    public InformationState? ScheduleSetupTime { get; set; }

    // Legacy property for backward compatibility
    public InformationState? ContactInfo
    {
        get => ContactName;
        set => ContactName = value;
    }
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