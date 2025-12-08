using MicrohireAgentChat.Models;

namespace MicrohireAgentChat.Services.Shared;

/// <summary>
/// Interface for extracting structured data from conversation transcripts
/// </summary>
public interface IConversationExtractor
{
    /// <summary>
    /// Extracts event date from conversation messages
    /// </summary>
    (DateTimeOffset? Date, string? Matched) ExtractEventDate(IEnumerable<DisplayMessage> messages);

    /// <summary>
    /// Extracts contact information (name, email, phone, position)
    /// </summary>
    ContactInfo ExtractContactInfo(IEnumerable<DisplayMessage> messages);

    /// <summary>
    /// Extracts organization name and address
    /// </summary>
    (string? Organisation, string? Address) ExtractOrganization(IEnumerable<DisplayMessage> messages);

    /// <summary>
    /// Extracts venue name and event date together
    /// </summary>
    (DateTimeOffset? EventDate, string? VenueName, string? DateMatched, string? VenueMatched) ExtractVenueAndEventDate(IEnumerable<DisplayMessage> messages);
}

/// <summary>
/// Contact information extracted from conversation
/// </summary>
public sealed record ContactInfo(
    string? Name,
    string? Email,
    string? PhoneE164,
    string? NameMatched,
    string? EmailMatched,
    string? PhoneMatched,
    string? Position
);

