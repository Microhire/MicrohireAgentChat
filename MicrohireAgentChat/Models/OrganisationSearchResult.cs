namespace MicrohireAgentChat.Models;

/// <summary>Result item for organisation autocomplete search.</summary>
public sealed class OrganisationSearchResult
{
    public decimal Id { get; init; }
    public string CustomerCode { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Address { get; init; }
}
