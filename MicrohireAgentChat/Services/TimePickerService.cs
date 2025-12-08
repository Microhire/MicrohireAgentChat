using MicrohireAgentChat.Models;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Handles time picker UI and schedule selection logic - extracted from AzureAgentChatService
/// </summary>
public sealed class TimePickerService
{
    private readonly IBookingDraftStore? _drafts;
    private readonly ILogger<TimePickerService> _logger;

    private static readonly Regex ChooseTimeRegex = new(
        @"^Choose\s*time:\s*(\d{1,2}:\d{2})\s*[–-]\s*(\d{1,2}:\d{2})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public TimePickerService(IBookingDraftStore? drafts, ILogger<TimePickerService> logger)
    {
        _drafts = drafts;
        _logger = logger;
    }

    /// <summary>
    /// Try to parse a schedule selection message like "Choose time: 09:00 - 17:00"
    /// </summary>
    public bool TryParseScheduleSelection(string message, out (TimeSpan Start, TimeSpan End) schedule)
    {
        schedule = default;

        var match = ChooseTimeRegex.Match(message.Trim());
        if (!match.Success)
            return false;

        if (!TimeSpan.TryParse(match.Groups[1].Value, out var start))
            return false;
        
        if (!TimeSpan.TryParse(match.Groups[2].Value, out var end))
            return false;

        schedule = (start, end);
        return true;
    }

    /// <summary>
    /// Save selected schedule to draft store
    /// </summary>
    public void SaveScheduleToDraft(string threadId, (TimeSpan Start, TimeSpan End) schedule)
    {
        if (_drafts == null) return;

        var draft = _drafts.GetOrCreate(threadId);
        draft.Start = schedule.Start;
        draft.End = schedule.End;
        
        _logger.LogInformation("Saved schedule to draft for thread {ThreadId}: {Start} - {End}", 
            threadId, schedule.Start, schedule.End);
    }

    /// <summary>
    /// Build a confirmation message for selected schedule
    /// </summary>
    public string BuildScheduleConfirmation((TimeSpan Start, TimeSpan End) schedule, DateTimeOffset? date)
    {
        var startStr = FormatTime(schedule.Start);
        var endStr = FormatTime(schedule.End);
        
        var dateStr = date.HasValue 
            ? $" on **{date.Value:dddd, MMMM d, yyyy}**" 
            : "";

        return $"✅ Great! I've noted your event will run from **{startStr}** to **{endStr}**{dateStr}.";
    }

    /// <summary>
    /// Check if a message should trigger time picker display
    /// </summary>
    public bool ShouldShowTimePicker(IEnumerable<DisplayMessage> messages)
    {
        // Check if last assistant message asks for time
        var lastAssistant = messages
            .Where(m => m.Role == "assistant")
            .LastOrDefault();

        if (lastAssistant == null) return false;

        var text = lastAssistant.FullText?.ToLower() ?? "";
        return text.Contains("what time") || 
               text.Contains("start time") || 
               text.Contains("end time") ||
               text.Contains("when does") ||
               text.Contains("schedule");
    }

    private static string FormatTime(TimeSpan time)
    {
        var hours = (int)time.TotalHours;
        var minutes = time.Minutes;
        
        if (hours == 0 && minutes == 0)
            return "midnight";
        if (hours == 12 && minutes == 0)
            return "noon";
        
        var period = hours >= 12 ? "PM" : "AM";
        var displayHours = hours % 12;
        if (displayHours == 0) displayHours = 12;
        
        return minutes == 0 
            ? $"{displayHours}{period}"
            : $"{displayHours}:{minutes:D2}{period}";
    }
}

