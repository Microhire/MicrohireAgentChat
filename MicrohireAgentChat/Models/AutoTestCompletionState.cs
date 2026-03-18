namespace MicrohireAgentChat.Models;

/// <summary>
/// Tracks milestones and completion state for the auto-test conversation.
/// </summary>
public sealed class AutoTestCompletionState
{
    public int StepNumber { get; set; }
    public bool IsComplete { get; set; }
    public string? CompletionReason { get; set; }

    public bool NameProvided { get; set; }
    public bool CustomerStatusProvided { get; set; }
    public bool OrganizationProvided { get; set; }
    public bool ContactInfoProvided { get; set; }
    public bool EventDetailsProvided { get; set; }
    public bool EquipmentDiscussed { get; set; }
    public bool ScheduleConfirmed { get; set; }
    public bool QuoteSummaryShown { get; set; }
    public bool QuoteGenerated { get; set; }
    public bool QuoteConfirmed { get; set; }
}
