namespace MicrohireAgentChat.Models;

/// <summary>Payload for POST /api/leads (sales portal).</summary>
public sealed class CreateLeadRequest
{
    public string? Organisation { get; set; }
    public string? OrganisationAddress { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? EventStartDate { get; set; }
    public string? EventEndDate { get; set; }
    /// <summary>Per-day start/end times. One entry per day between EventStartDate and EventEndDate (inclusive).</summary>
    public List<EventDayInput>? EventDays { get; set; }
    public string? Venue { get; set; }
    public string? Room { get; set; }
    public string? Attendees { get; set; }
    /// <summary>Optional: ID of an existing organisation selected via autocomplete. Skips org creation when set.</summary>
    public decimal? ExistingOrgId { get; set; }
}

public sealed class EventDayInput
{
    /// <summary>YYYY-MM-DD.</summary>
    public string? Date { get; set; }
    /// <summary>HH:mm (24h).</summary>
    public string? StartTime { get; set; }
    /// <summary>HH:mm (24h).</summary>
    public string? EndTime { get; set; }
}
