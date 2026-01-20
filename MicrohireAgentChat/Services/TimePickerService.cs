using MicrohireAgentChat.Models;
using System.Globalization;
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
    /// Try to parse a multi-part schedule selection message like "Choose schedule: date=2026-03-09; setup=09:00; rehearsal=09:30; start=10:00; end=16:00; packup=20:00"
    /// </summary>
    public bool TryParseMultiScheduleSelection(string message, out ScheduleSelection schedule)
    {
        schedule = new ScheduleSelection();

        var match = Regex.Match(message.Trim(), @"^\s*Choose\s+schedule\s*:\s*(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var blob = match.Groups[1].Value;
        var kvs = blob.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        TimeSpan ts;
        DateTime? eventDate = null;

        foreach (var kv in kvs)
        {
            var parts = kv.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            var key = parts[0].ToLowerInvariant();
            var val = parts[1];

            // Handle date separately (ISO format like 2026-04-28)
            if (key == "date")
            {
                if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    eventDate = dt;
                continue;
            }

            // Parse time value - HTML5 time inputs send "HH:mm" format (e.g., "10:00", "16:00")
            if (!TimeSpan.TryParse(val, CultureInfo.InvariantCulture, out ts))
            {
                // Fallback: try parsing as "HH:mm" explicitly if standard parse fails
                if (val.Contains(':') && val.Length >= 4)
                {
                    var timeParts = val.Split(':');
                    if (timeParts.Length == 2 &&
                        int.TryParse(timeParts[0], out var hours) &&
                        int.TryParse(timeParts[1], out var minutes))
                    {
                        ts = new TimeSpan(hours, minutes, 0);
                    }
                    else
                    {
                        continue; // Skip invalid time format
                    }
                }
                else
                {
                    continue; // Skip invalid time format
                }
            }

            switch (key)
            {
                case "setup":
                    schedule.Setup = ts; break;
                case "rehearsal":
                    schedule.Rehearsal = ts; break;
                case "start":
                case "showstart":
                case "eventstart":
                    schedule.Start = ts; break;
                case "end":
                case "showend":
                case "eventend":
                    schedule.End = ts; break;
                case "packup":
                case "pack_down":
                case "packdown":
                    schedule.PackUp = ts; break;
            }
        }

        schedule.EventDate = eventDate;

        // Validate chronological order before returning
        if (!ValidateScheduleOrder(schedule.Setup, schedule.Rehearsal, schedule.Start, schedule.End, schedule.PackUp))
        {
            return false;
        }

        return schedule.Setup != default && schedule.Rehearsal != default && schedule.PackUp != default;
    }

    /// <summary>
    /// Validates that schedule times are in chronological order: Setup < Rehearsal < Start < End < Pack Up
    /// </summary>
    private static bool ValidateScheduleOrder(
        TimeSpan setup,
        TimeSpan rehearsal,
        TimeSpan? showStart,
        TimeSpan? showEnd,
        TimeSpan packup)
    {
        // Validate: setup < rehearsal
        if (rehearsal <= setup)
            return false;

        // Validate: rehearsal < start (CRITICAL - main issue from screenshot)
        if (showStart.HasValue && showStart.Value <= rehearsal)
            return false;

        // Validate: start < end
        if (showStart.HasValue && showEnd.HasValue && showEnd.Value <= showStart.Value)
            return false;

        // Validate: end < packup
        if (showEnd.HasValue && packup <= showEnd.Value)
            return false;

        // Validate: setup < start (if rehearsal not provided, but this shouldn't happen per business logic)
        // Note: This case is handled by the rehearsal check above since rehearsal is required

        // Validate: rehearsal < packup (if end not provided)
        if (!showEnd.HasValue && packup <= rehearsal)
            return false;

        return true;
    }

    /// <summary>
    /// Build a confirmation message for multi-part schedule selection
    /// </summary>
    public string BuildMultiScheduleConfirmation(ScheduleSelection schedule)
    {
        var parts = new List<string>
        {
            $"Setup {FormatTime(schedule.Setup)}",
            $"Rehearsal {FormatTime(schedule.Rehearsal)}"
        };

        if (schedule.Start.HasValue)
        {
            parts.Add($"Event Start {FormatTime(schedule.Start.Value)}");
        }

        if (schedule.End.HasValue)
        {
            parts.Add($"Event End {FormatTime(schedule.End.Value)}");
        }

        parts.Add($"Pack Up {FormatTime(schedule.PackUp)}");

        var dateStr = schedule.EventDate.HasValue
            ? $" on {schedule.EventDate.Value:dddd, MMMM d, yyyy}"
            : "";

        return $"✅ Perfect! I've confirmed your schedule{dateStr}: {string.Join("; ", parts)}.";
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

/// <summary>
/// Represents a complete schedule selection with multiple time components
/// </summary>
public class ScheduleSelection
{
    public TimeSpan Setup { get; set; }
    public TimeSpan Rehearsal { get; set; }
    public TimeSpan? Start { get; set; }
    public TimeSpan? End { get; set; }
    public TimeSpan PackUp { get; set; }
    public DateTime? EventDate { get; set; }
}

