namespace MicrohireAgentChat.Models;

public sealed class LeadExpiredViewModel
{
    public string Organisation { get; set; } = "";
    public string? BookingNo { get; set; }
    public DateTime SignedAtUtc { get; set; }
}
