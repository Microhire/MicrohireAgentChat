namespace MicrohireAgentChat.Services
{
    public sealed record AvailabilityQuery(
       DateTime StartUtc,
       DateTime? EndUtc = null,
       int? VenueId = null,
       string? VenueRoom = null
   );

    public sealed record AvailabilityConflict(
        decimal BookingId,
        string? BookingNo,
        string? OrderNo,
        DateTime Start,
        DateTime End,
        int? VenueId,
        string? VenueRoom
    );

    public sealed record AvailabilityResult(
        bool IsAvailable,
        List<AvailabilityConflict> Conflicts
    );

    public interface IAvailabilityService
    {
        Task<AvailabilityResult> CheckAsync(AvailabilityQuery q, CancellationToken ct = default);
    }

}
