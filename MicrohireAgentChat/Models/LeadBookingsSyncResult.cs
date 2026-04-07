namespace MicrohireAgentChat.Models;

/// <summary>Outcome of syncing a sales lead to BookingsDb (RetailOps).</summary>
public sealed class LeadBookingsSyncResult
{
    /// <summary>created, updated, skipped, or error</summary>
    public string ContactAction { get; init; } = "skipped";

    /// <summary>created, updated, skipped, or error</summary>
    public string OrgAction { get; init; } = "skipped";

    /// <summary>Generated booking number (e.g. C1441500001), or null if booking creation failed.</summary>
    public string? BookingNo { get; init; }

    /// <summary>created, skipped, or error</summary>
    public string BookingAction { get; init; } = "skipped";
}
