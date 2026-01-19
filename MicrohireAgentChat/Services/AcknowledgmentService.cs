using MicrohireAgentChat.Services.Extraction;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Generates acknowledgment messages from extracted event information
/// </summary>
public sealed class AcknowledgmentService
{
    /// <summary>
    /// Generate acknowledgment message from extracted information
    /// </summary>
    public string? GenerateAcknowledgment(EventInformation info)
    {
        if (!info.HasInformation())
            return null;

        var parts = new List<string>();

        if (info.Budget.HasValue)
            parts.Add($"budget of {info.Budget.Value.ToString("C")}");

        if (info.Attendees.HasValue)
            parts.Add($"{info.Attendees.Value} attendees");

        if (!string.IsNullOrWhiteSpace(info.SetupStyle))
            parts.Add($"{info.SetupStyle} setup");

        if (!string.IsNullOrWhiteSpace(info.Venue))
            parts.Add($"venue in {info.Venue}");

        if (info.Dates != null && info.Dates.Count > 0)
        {
            if (info.Dates.Count == 1)
                parts.Add($"date: {info.Dates[0]}");
            else
                parts.Add($"dates: {string.Join(", ", info.Dates)}");
        }

        if (!string.IsNullOrWhiteSpace(info.SpecialRequests))
            parts.Add($"special request: {info.SpecialRequests}");

        if (parts.Count == 0)
            return null;

        var acknowledgment = $"Thank you! I've noted: {string.Join(", ", parts)}.";

        // Add next action based on what information is missing
        acknowledgment += " Now let me confirm the schedule for you.";

        return acknowledgment;
    }

    /// <summary>
    /// Generate acknowledgment for event type confirmation
    /// </summary>
    public string GenerateEventTypeAcknowledgment(string eventType)
    {
        return $"I see you mentioned {eventType}. Is that correct?";
    }

    /// <summary>
    /// Generate acknowledgment for room setup questions
    /// </summary>
    public string GenerateRoomSetupAcknowledgment(string roomName, string setupSuggestion)
    {
        return $"For the {roomName}, I'd recommend a {setupSuggestion} setup based on the room configuration and capacity.";
    }
}