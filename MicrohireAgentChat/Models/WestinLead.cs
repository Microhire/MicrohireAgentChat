namespace MicrohireAgentChat.Models;

public sealed class WestinLead
{
    public int Id { get; set; }
    public Guid Token { get; set; }
    public string Organisation { get; set; } = null!;
    public string OrganisationAddress { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string EventStartDate { get; set; } = null!;
    public string EventEndDate { get; set; } = null!;
    public string Venue { get; set; } = null!;
    public string Room { get; set; } = null!;
    public string Attendees { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
}
