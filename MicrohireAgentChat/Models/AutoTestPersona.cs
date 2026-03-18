namespace MicrohireAgentChat.Models;

/// <summary>
/// Persona for the AI-powered auto-test customer. Used as context for GPT-4o to generate realistic messages.
/// </summary>
public sealed class AutoTestPersona
{
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Suburb { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public bool IsExistingCustomer { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RoomSetup { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public int AttendeeCount { get; set; }
    public DateTime EventDate { get; set; }
    public string EventDateFormatted { get; set; } = string.Empty;
    public string ScenarioKey { get; set; } = string.Empty;
    public string ScenarioDescription { get; set; } = string.Empty;
}
