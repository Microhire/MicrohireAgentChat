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
    public string? Venue { get; set; }
    public string? Room { get; set; }
    public string? Attendees { get; set; }
}
