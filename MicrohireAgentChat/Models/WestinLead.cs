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
    /// <summary>Expected attendee count from the sales portal; chat prefill uses this verbatim — wrong DB values need ingestion/CRM mapping fixes.</summary>
    public string Attendees { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    /// <summary>Booking number created in tblbookings during lead sync (e.g. C1441500001).</summary>
    public string? BookingNo { get; set; }
    /// <summary>Set when the client signs/accepts the quote — non-null means the lead link is expired.</summary>
    public DateTime? QuoteSignedUtc { get; set; }
    /// <summary>Name provided at signing for quick audit (full details in the signature JSON file).</summary>
    public string? SignedByName { get; set; }
}
