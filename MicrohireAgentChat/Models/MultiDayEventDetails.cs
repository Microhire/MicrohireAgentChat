namespace MicrohireAgentChat.Models;

/// <summary>
/// Details for multi-day events with day-specific information
/// </summary>
public class MultiDayEventDetails
{
    public Dictionary<int, DayEventDetails> Days { get; set; } = new();
    public DateTime StartDate { get; set; }
    public int DurationDays { get; set; }

    /// <summary>
    /// Add or update details for a specific day
    /// </summary>
    public void SetDayDetails(int dayNumber, DayEventDetails details)
    {
        Days[dayNumber] = details;
    }

    /// <summary>
    /// Get details for a specific day
    /// </summary>
    public DayEventDetails? GetDayDetails(int dayNumber)
    {
        return Days.TryGetValue(dayNumber, out var details) ? details : null;
    }
}

/// <summary>
/// Event details specific to a single day
/// </summary>
public class DayEventDetails
{
    public int DayNumber { get; set; }
    public DateTime Date { get; set; }
    public string? SetupStyle { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public List<string> EquipmentNeeds { get; set; } = new();
    public string? SpecialNotes { get; set; }
}