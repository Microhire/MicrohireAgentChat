using MicrohireAgentChat.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Extraction;

public sealed partial class ChatExtractionService
{
    private readonly ILogger<ChatExtractionService> _logger;

    public ChatExtractionService(ILogger<ChatExtractionService> logger)
    {
        _logger = logger;
    }

    public (DateTimeOffset? date, string? matched) ExtractEventDate(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.OrderBy(m => m.Timestamp).ToList();
        static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

        var monthNames = "jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec|january|february|march|april|june|july|august|september|october|november|december";

        // Also support common abbreviations and partial matches
        var monthAbbreviations = new Dictionary<string, string>
        {
            ["ja"] = "jan", ["f"] = "feb", ["mar"] = "mar", ["ap"] = "apr", ["may"] = "may", ["jun"] = "jun",
            ["jul"] = "jul", ["au"] = "aug", ["s"] = "sep", ["o"] = "oct", ["n"] = "nov", ["d"] = "dec",
            ["jan"] = "jan", ["feb"] = "feb", ["mar"] = "mar", ["apr"] = "apr", ["may"] = "may", ["jun"] = "jun",
            ["jul"] = "jul", ["aug"] = "aug", ["sep"] = "sep", ["oct"] = "oct", ["nov"] = "nov", ["dec"] = "dec"
        };
        var patterns = new[]
        {
            // "15th of February 2024", "15 of February 2024"
            $@"\b(\d{{1,2}})(st|nd|rd|th)?\s+of\s+({monthNames})\s+(\d{{4}})\b",
            // "15th February 2024", "15 February 2024"
            $@"\b(\d{{1,2}})(st|nd|rd|th)?\s+({monthNames})\s+(\d{{4}})\b",
            // "February 15th, 2024", "February 15 2024"
            $@"\b({monthNames})\s+(\d{{1,2}})(st|nd|rd|th)?(,?\s*(\d{{4}}))?\b",
            // "1st Jan", "15 January", "1 Feb" (day-first without year)
            $@"\b(\d{{1,2}})(st|nd|rd|th)?\s+({monthNames})\b",
            // "15/02/2024", "15/2/24"
            @"\b(\d{1,2})/(\d{1,2})/(\d{2,4})\b",
            // "2024-02-15"
            @"\b(\d{4})-(\d{2})-(\d{2})\b"
        };

        bool TryParseMatch(string text, out DateTimeOffset dto, out string? matched)
        {
            matched = null;

            // First try with original text
            foreach (var pat in patterns)
            {
                foreach (Match m in Regex.Matches(text, pat, RegexOptions.IgnoreCase))
                {
                    var token = m.Value;
                    if (TryParseDateToken(token, out dto))
                    {
                        matched = token;
                        return true;
                    }
                }
            }

            // If no match, try expanding month abbreviations
            var expandedText = text;
            foreach (var abbr in monthAbbreviations)
            {
                // Use word boundaries to avoid partial matches in middle of words
                expandedText = Regex.Replace(expandedText, $@"\b{Regex.Escape(abbr.Key)}\b", abbr.Value, RegexOptions.IgnoreCase);
            }

            // Try again with expanded text
            foreach (var pat in patterns)
            {
                foreach (Match m in Regex.Matches(expandedText, pat, RegexOptions.IgnoreCase))
                {
                    var token = m.Value;
                    if (TryParseDateToken(token, out dto))
                    {
                        matched = token;
                        return true;
                    }
                }
            }

            dto = default;
            return false;
        }

        foreach (var m in ordered.Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m);
            if (TryParseMatch(text, out var dto, out var matched))
                return (dto, matched);
        }

        foreach (var m in ordered.Where(x => !x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m);
            if (TryParseMatch(text, out var dto, out var matched))
                return (dto, matched);
        }

        return (null, null);

        bool TryParseDateToken(string token, out DateTimeOffset dto)
        {
            dto = default;
            if (string.IsNullOrWhiteSpace(token)) return false;

            token = token.Trim();
            // Normalize ordinals and "of" phrasing
            token = Regex.Replace(token, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);
            token = Regex.Replace(token, @"\b(\d{1,2})\s+of\s+", "$1 ", RegexOptions.IgnoreCase);

            var now = DateTimeOffset.Now;
            var hasExplicitYear = Regex.IsMatch(token, @"\b\d{4}\b");

            // LOGGING: Initial parsing attempt
            _logger.LogInformation("DATE PARSING: Starting to parse token '{Token}'. Has explicit year: {HasYear}, Current time: {Now}", token, hasExplicitYear, now);

            var cultures = new[]
            {
                CultureInfo.GetCultureInfo("en-US"),
                CultureInfo.GetCultureInfo("en-GB"),
                CultureInfo.InvariantCulture
            };

            var candidates = hasExplicitYear
                ? new[] { token }
                : new[] { $"{token} {now.Year}", token };

            _logger.LogInformation("DATE PARSING: Candidates to try: {Candidates}", string.Join(", ", candidates));

            foreach (var candidate in candidates)
            {
                _logger.LogInformation("DATE PARSING: Trying candidate '{Candidate}'", candidate);

                if (Regex.IsMatch(candidate, @"^\d{4}-\d{2}-\d{2}$") &&
                    DateTime.TryParseExact(candidate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var isoDt))
                {
                    dto = new DateTimeOffset(isoDt);
                    _logger.LogInformation("DATE PARSING: Parsed ISO format '{Candidate}' to {ParsedDate}", candidate, dto);
                    break;
                }

                if (dto == default && Regex.IsMatch(candidate, @"^\d{1,2}/\d{1,2}/\d{2,4}$"))
                {
                    var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "MM/dd/yyyy", "M/d/yyyy" };
                    foreach (var fmt in fmts)
                    {
                        if (DateTime.TryParseExact(candidate, fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dmy))
                        {
                            dto = new DateTimeOffset(dmy);
                            _logger.LogInformation("DATE PARSING: Parsed slash format '{Candidate}' with format '{Format}' to {ParsedDate}", candidate, fmt, dto);
                            break;
                        }
                    }
                }

                if (dto == default)
                {
                    foreach (var c in cultures)
                    {
                        if (DateTime.TryParse(candidate, c, DateTimeStyles.AssumeLocal, out var dt))
                        {
                            dto = new DateTimeOffset(dt);
                            _logger.LogInformation("DATE PARSING: Parsed with culture '{Culture}' '{Candidate}' to {ParsedDate}", c.Name, candidate, dto);
                            break;
                        }
                    }
                }

                if (dto != default)
                    break;
            }

            if (dto == default)
            {
                _logger.LogWarning("DATE PARSING: Failed to parse token '{Token}' with any method", token);
                return false;
            }

            _logger.LogInformation("DATE PARSING: Initial parse result for '{Token}': {ParsedDate}", token, dto);

            // Always apply smart date detection: roll forward until the date is in the future
            // This handles both cases: explicit years that are wrong, and implicit years
            var originalDate = dto;
            var normalized = new DateTimeOffset(dto.Date, dto.Offset);

            _logger.LogInformation("DATE PARSING: Before roll-forward - Original: {Original}, Normalized: {Normalized}, Current time: {Now}", originalDate, normalized, now);

            int rollForwardCount = 0;
            while (normalized.Date < now.Date)
            {
                var beforeRoll = normalized;
                normalized = normalized.AddYears(1);
                rollForwardCount++;
                _logger.LogInformation("DATE PARSING: Rolled forward from {Before} to {After} (iteration {Count})", beforeRoll.Date, normalized.Date, rollForwardCount);
            }

            dto = normalized;

            _logger.LogInformation("DATE PARSING: Final result for '{Token}': {FinalDate} (rolled forward {Count} times)", token, dto, rollForwardCount);

            return true;
        }
    }

    /// <summary>
    /// Extract event time (start/end) from conversation messages
    /// Looks for patterns like: "9:00 AM", "14:30", "2pm", "10am-4pm"
    /// </summary>
    public (TimeSpan? startTime, TimeSpan? endTime, string? matched) ExtractEventTime(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.OrderBy(m => m.Timestamp).ToList();
        static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

        // Special-case: phrases like "starts at 9am and ends at 5 pm"
        bool TryParseStartEndPhrase(string text, out TimeSpan? start, out TimeSpan? end, out string? matched)
        {
            start = null;
            end = null;
            matched = null;

            var pattern = @"(?:event\s+)?(?:start|starts|begin|begins|beginning)(?:\s+at)?\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm))(?:(?!\d).)*?(?:event\s+)?(?:end|ends|finish|finishes)(?:\s+at)?\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm))";
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!m.Success)
                return false;

            string Clean(string s) => Regex.Replace(s, @"\s+", "");

            var startToken = Clean(m.Groups[1].Value);
            var endToken = Clean(m.Groups[2].Value);

            if (TryParseSingleTime(startToken, out var s) && TryParseSingleTime(endToken, out var e))
            {
                start = s;
                end = e;
                matched = m.Value;
                return true;
            }

            return false;
        }

        var timePatterns = new[]
        {
            // 9:00 AM - 5:00 PM, 9am-5pm, etc.
            @"\b(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)\s*(?:to|-|–|—)\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)\b",
            // 14:30 - 18:00, 2:00pm - 6:00pm, etc.
            @"\b(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)?\s*(?:to|-|–|—)\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)?\b",
            // Single time: 9:00 AM, 2pm, 14:30
            @"\b(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)\b",
            // 24-hour format: 14:30, 09:00
            @"\b(\d{1,2}):(\d{2})\b"
        };

        bool TryParseTimeRange(string text, out TimeSpan? start, out TimeSpan? end, out string? matched)
        {
            start = null;
            end = null;
            matched = null;

            foreach (var pat in timePatterns)
            {
                foreach (Match m in Regex.Matches(text, pat, RegexOptions.IgnoreCase))
                {
                    var token = m.Value;
                    if (TryParseTimeToken(token, out var s, out var e))
                    {
                        start = s;
                        end = e;
                        matched = token;
                        return true;
                    }
                }
            }
            return false;
        }

        // Check user messages first
        foreach (var m in ordered.Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m);
            if (TryParseStartEndPhrase(text, out var start, out var end, out var matchedFromPhrase))
                return (start, end, matchedFromPhrase);

            if (TryParseTimeRange(text, out var rangeStart, out var rangeEnd, out var matched))
                return (rangeStart, rangeEnd, matched);
        }

        // Then check assistant messages
        foreach (var m in ordered.Where(x => !x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m);
            if (TryParseStartEndPhrase(text, out var start, out var end, out var matchedFromPhrase))
                return (start, end, matchedFromPhrase);

            if (TryParseTimeRange(text, out var rangeStart, out var rangeEnd, out var matched))
                return (rangeStart, rangeEnd, matched);
        }

        return (null, null, null);
    }

    /// <summary>
    /// Extract event times with semantic context - distinguishes between event start, rehearsal, and finish times
    /// </summary>
    public (TimeSpan? eventStart, TimeSpan? eventEnd, TimeSpan? rehearsal, string context) ExtractEventTimesWithContext(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.OrderBy(m => m.Timestamp).ToList();
        static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

        TimeSpan? eventStart = null;
        TimeSpan? eventEnd = null;
        TimeSpan? rehearsal = null;
        var context = "";

        // Check user messages for semantic time references
        foreach (var m in ordered.Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m).ToLowerInvariant();

            // Look for event start time: "start 9am", "event starts at 9am", "starts 9am"
            var startMatch = Regex.Match(text, @"(?:event\s+)?(?:start|starts|beginning|begins)(?:\s+at)?\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm))", RegexOptions.IgnoreCase);
            if (startMatch.Success && TimeSpan.TryParse(startMatch.Groups[1].Value.Trim(), out var startTime))
            {
                eventStart = startTime;
                context += $"Event starts at {startTime:hh\\:mm}; ";
            }

            // Look for event end time: "finish 5pm", "end at 5pm", "ends 5pm"
            var endMatch = Regex.Match(text, @"(?:event\s+)?(?:finish|end|ends|finishes)(?:\s+at)?\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm))", RegexOptions.IgnoreCase);
            if (endMatch.Success && TimeSpan.TryParse(endMatch.Groups[1].Value.Trim(), out var endTime))
            {
                eventEnd = endTime;
                context += $"Event ends at {endTime:hh\\:mm}; ";
            }

            // Look for rehearsal time: "rehearsal at 8am", "rehearsal 8am"
            var rehearsalMatch = Regex.Match(text, @"rehearsal(?:\s+at)?\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm))", RegexOptions.IgnoreCase);
            if (rehearsalMatch.Success && TimeSpan.TryParse(rehearsalMatch.Groups[1].Value.Trim(), out var rehearsalTime))
            {
                rehearsal = rehearsalTime;
                context += $"Rehearsal at {rehearsalTime:hh\\:mm}; ";
            }

            // Look for relative rehearsal: "1 hour before", "2 hours before rehearsal"
            var relativeMatch = Regex.Match(text, @"(\d+)\s+hour(?:s)?\s+before(?:\s+(?:for\s+)?rehearsal)?", RegexOptions.IgnoreCase);
            if (relativeMatch.Success && int.TryParse(relativeMatch.Groups[1].Value, out var hoursBefore))
            {
                if (eventStart.HasValue && !rehearsal.HasValue)
                {
                    rehearsal = eventStart.Value.Subtract(TimeSpan.FromHours(hoursBefore));
                    context += $"Rehearsal {hoursBefore} hour(s) before event start; ";
                }
            }
        }

        // If we have event start but no rehearsal, default to 1 hour before
        if (eventStart.HasValue && !rehearsal.HasValue)
        {
            rehearsal = eventStart.Value.Subtract(TimeSpan.FromHours(1));
            context += "Default rehearsal 1 hour before event start; ";
        }

        return (eventStart, eventEnd, rehearsal, context.Trim());
    }

    private static bool TryParseTimeToken(string token, out TimeSpan? start, out TimeSpan? end)
    {
        start = null;
        end = null;

        // Clean the token
        token = token.ToLowerInvariant().Replace(" ", "");

        // Pattern 1: time1-time2 (e.g., "9am-5pm", "14:30-18:00")
        var rangeMatch = Regex.Match(token, @"^(\d{1,2}(?::\d{2})?(?:am|pm)?)-(?:(\d{1,2}(?::\d{2})?(?:am|pm)?))$");
        if (rangeMatch.Success)
        {
            if (TryParseSingleTime(rangeMatch.Groups[1].Value, out var s) &&
                TryParseSingleTime(rangeMatch.Groups[2].Value, out var e))
            {
                start = s;
                end = e;
                return true;
            }
        }

    // Pattern 2: single time (e.g., "9am", "14:30")
    if (TryParseSingleTime(token, out var singleTime))
    {
        start = singleTime;
        // For single times, assume a default duration (e.g., 2 hours)
        end = singleTime.Add(TimeSpan.FromHours(2));
        return true;
    }

        return false;
    }

    private static bool TryParseSingleTime(string timeStr, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        // Handle AM/PM formats
        if (Regex.IsMatch(timeStr, @"^\d{1,2}(?::\d{2})?(?:am|pm)$"))
        {
            if (DateTime.TryParse(timeStr, out var dt))
            {
                result = dt.TimeOfDay;
                return true;
            }
        }

        // Handle 24-hour format
        if (Regex.IsMatch(timeStr, @"^\d{1,2}:\d{2}$"))
        {
            if (TimeSpan.TryParse(timeStr, out result))
            {
                return true;
            }
        }

        return false;
    }

}
