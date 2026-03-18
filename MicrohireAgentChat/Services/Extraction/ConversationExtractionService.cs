using MicrohireAgentChat.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Extraction;

/// <summary>
/// Extracts structured data from conversation messages using heuristic patterns
/// </summary>
public sealed partial class ConversationExtractionService
{
    private readonly ILogger<ConversationExtractionService> _logger;

    public ConversationExtractionService(ILogger<ConversationExtractionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extract event date from conversation messages
    /// Looks for patterns like: "15 January 2025", "Jan 15 2025", "15/01/2025", "2025-01-15"
    /// </summary>
    public (DateTimeOffset? date, string? matched) ExtractEventDate(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.OrderBy(m => m.Timestamp).ToList();
        static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

        var monthNames = "jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec|january|february|march|april|june|july|august|september|october|november|december";
        var patterns = new[]
        {
            $@"\b(\d{{1,2}})(st|nd|rd|th)?\s+({monthNames})\s+(\d{{4}})\b",
            $@"\b({monthNames})\s+(\d{{1,2}})(st|nd|rd|th)?(,?\s*(\d{{4}}))?\b",
            // "1st Jan", "15 January", "1 Feb" (day-first without year)
            $@"\b(\d{{1,2}})(st|nd|rd|th)?\s+({monthNames})\b",
            @"\b(\d{1,2})/(\d{1,2})/(\d{2,4})\b",
            @"\b(\d{4})-(\d{2})-(\d{2})\b"
        };

        bool TryParseMatch(string text, out DateTimeOffset dto, out string? matched)
        {
            matched = null;
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
            dto = default;
            return false;
        }

        // Check user messages first
        foreach (var m in ordered.Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m);
            if (TryParseMatch(text, out var dto, out var matched))
                return (dto, matched);
        }

        // Then check assistant messages
        foreach (var m in ordered.Where(x => !x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var text = JoinParts(m);
            if (TryParseMatch(text, out var dto, out var matched))
                return (dto, matched);
        }

        return (null, null);
    }

    /// <summary>
    /// Extract venue name and event date from conversation
    /// </summary>
    public (DateTimeOffset? EventDate, string? VenueName, string? DateMatched, string? VenueMatched) 
        ExtractVenueAndEventDate(IEnumerable<DisplayMessage> messages)
    {
        var items = messages
            .OrderBy(m => m.Timestamp)
            .SelectMany(m => (m.Parts ?? Enumerable.Empty<string>())
                .SelectMany(p => p.Replace("\r\n", "\n").Split('\n'))
                .Select(line => new { line = line.Trim(), role = m.Role }))
            .Where(x => !string.IsNullOrWhiteSpace(x.line))
            .ToList();

        var userLines = items.Where(x => x.role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.line).ToList();
        var asstLines = items.Where(x => !x.role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.line).ToList();
        var fullText = string.Join("\n", items.Select(x => x.line));

        var yearInContext = Regex.Matches(fullText, @"\b(20\d{2})\b")
                                 .OfType<Match>()
                                 .Select(m => int.Parse(m.Groups[1].Value))
                                 .Cast<int?>()
                                 .FirstOrDefault();

        var date = FindEventDate(userLines, yearInContext, out var dateMatched)
                   ?? FindEventDate(asstLines, yearInContext, out dateMatched);

        var venue = FindVenue(userLines, out var venueMatched)
                    ?? FindVenue(asstLines, out venueMatched);

        return (date, venue, dateMatched, venueMatched);
    }

    /// <summary>
    /// Extract room name from conversation. Used for Westin Brisbane / Four Points Brisbane
    /// to recommend room-specific AV packages.
    /// </summary>
    public string? ExtractRoom(IEnumerable<DisplayMessage> messages)
    {
        var items = messages
            ?.OrderBy(m => m.Timestamp)
            .SelectMany(m => (m.Parts ?? Enumerable.Empty<string>())
                .SelectMany(p => p.Replace("\r\n", "\n").Split('\n'))
                .Select(line => (line: line.Trim(), role: m.Role ?? "")))
            .Where(x => !string.IsNullOrWhiteSpace(x.line))
            .ToList() ?? new List<(string line, string role)>();

        var asstLines = items.Where(x => !string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.line).ToList();
        var userLines = items.Where(x => string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.line).ToList();

        // 1. "Room: X" or "• Room: X" in assistant summaries
        foreach (var line in asstLines)
        {
            var m = Regex.Match(line, @"^(?:•\s*)?room\s*:\s*(.+)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var val = m.Groups[1].Value.Trim();
                if (val.Length > 2) return val;
            }
        }

        // 2. Room names in user messages
        var roomPatterns = new[]
        {
            @"(?:in\s+)?(?:the\s+)?(Westin\s+Ballroom(?:\s+[IV\d]+)?)",
            @"(?:in\s+)?(?:the\s+)?(Ballroom)",
            @"(?:in\s+)?(Elevate)",
            @"(?:in\s+)?(?:the\s+)?(Thrive(?:\s+Boardroom)?)",
            @"(?:in\s+)?(?:the\s+)?(Four\s+Points\s+Meeting\s+Room)",
            @"(?:in\s+)?(?:the\s+)?(Meeting\s+Room)"
        };

        foreach (var line in userLines.Concat(asstLines))
        {
            var lineLower = line.ToLower();
            if (lineLower.Contains("?") && (lineLower.Contains("which") || lineLower.Contains("what")))
                continue;

            foreach (var pat in roomPatterns)
            {
                var match = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var val = match.Groups[1].Value.Trim();
                    if (val.Equals("Ballroom", StringComparison.OrdinalIgnoreCase))
                        return "Westin Ballroom";
                    if (val.Equals("Meeting Room", StringComparison.OrdinalIgnoreCase) && lineLower.Contains("four points"))
                        return "Meeting Room";
                    if (val.Length > 2) return val;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extract projector placement area (A-F) from conversation.
    /// Accepts phrases like "Area C", "projector area F". Also accepts bare single-letter replies
    /// (e.g. "A") when the conversation contains a projector-area prompt from the assistant.
    /// </summary>
    public string? ExtractProjectorArea(IEnumerable<DisplayMessage> messages)
    {
        var items = messages
            ?.OrderBy(m => m.Timestamp)
            .SelectMany(m => (m.Parts ?? Enumerable.Empty<string>())
                .SelectMany(p => p.Replace("\r\n", "\n").Split('\n'))
                .Select(line => (line: line.Trim(), role: m.Role ?? "")))
            .Where(x => !string.IsNullOrWhiteSpace(x.line))
            .ToList() ?? new List<(string line, string role)>();

        // Context flag: did the assistant ever ask the user to choose a projector area?
        var inProjectorContext = items.Any(x =>
            !string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase) &&
            (x.line.Contains("projector placement area", StringComparison.OrdinalIgnoreCase) ||
             x.line.Contains("floor-plan.png", StringComparison.OrdinalIgnoreCase)));

        // Prefer user lines first, then assistant summaries.
        var lines = items
            .Where(x => string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.line)
            .Concat(items.Where(x => !string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase)).Select(x => x.line))
            .ToList();

        foreach (var line in lines)
        {
            var parsed = ParseProjectorAreaFromText(line, inProjectorContext);
            if (!string.IsNullOrWhiteSpace(parsed))
                return parsed;
        }

        return null;
    }

    /// <summary>
    /// Extract projector placement areas (A-F), preserving first-seen order and uniqueness.
    /// Also accepts bare single-letter replies when the conversation contains a projector-area prompt.
    /// </summary>
    public List<string> ExtractProjectorAreas(IEnumerable<DisplayMessage> messages)
    {
        var items = messages
            ?.OrderBy(m => m.Timestamp)
            .SelectMany(m => (m.Parts ?? Enumerable.Empty<string>())
                .SelectMany(p => p.Replace("\r\n", "\n").Split('\n'))
                .Select(line => (line: line.Trim(), role: m.Role ?? "")))
            .Where(x => !string.IsNullOrWhiteSpace(x.line))
            .ToList() ?? new List<(string line, string role)>();

        // Context flag: did the assistant ever ask the user to choose a projector area?
        var inProjectorContext = items.Any(x =>
            !string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase) &&
            (x.line.Contains("projector placement area", StringComparison.OrdinalIgnoreCase) ||
             x.line.Contains("floor-plan.png", StringComparison.OrdinalIgnoreCase)));

        var lines = items
            .Where(x => string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.line)
            .Concat(items.Where(x => !string.Equals(x.role, "user", StringComparison.OrdinalIgnoreCase)).Select(x => x.line));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var line in lines)
        {
            foreach (var area in ParseProjectorAreasFromText(line, inProjectorContext))
            {
                if (seen.Add(area))
                    result.Add(area);
            }
        }
        return result;
    }

    /// <summary>
    /// Extract contact information (name, email, phone, position) from conversation
    /// </summary>
    public ContactInfo ExtractContactInfo(IEnumerable<DisplayMessage> messages)
    {
        var lines = messages
            .OrderBy(m => m.Timestamp)
            .SelectMany(m => (m.Parts ?? Enumerable.Empty<string>())
                .SelectMany(p => p.Replace("\r\n", "\n").Split('\n'))
                .Select(line => new { line = line.Trim(), role = m.Role }))
            .Where(x => x.line.Length > 0)
            .ToList();

        var user = lines.Where(x => x.role.Equals("user", StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.line).ToList();
        var asst = lines.Where(x => !x.role.Equals("user", StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.line).ToList();
        var convo = lines.Select(x => (role: x.role, line: x.line)).ToList();

        // Parse embedded UI/JSON fields if present
        var (jsonName, jsonEmail, jsonPhone) = ParseIslaFields(lines.Select(x => x.line));
        var cleanedJsonName = CleanExtractedName(jsonName);

        // EMAIL
        var (email, emailMatch) = !string.IsNullOrWhiteSpace(jsonEmail)
            ? (jsonEmail, jsonEmail)
            : FindEmail(user);
        if (email is null) (email, emailMatch) = FindEmail(asst);

        // NAME - filter out assistant names from jsonName before using it. Do NOT use assistant lines for contact name.
        var (name, nameMatch) = !string.IsNullOrWhiteSpace(cleanedJsonName)
            ? (cleanedJsonName, cleanedJsonName)
            : FindName(user);
        if (name is null)
        {
            var shortReplyName = FindNameFromShortReplyAfterPrompt(convo);
            if (!string.IsNullOrWhiteSpace(shortReplyName.Name))
            {
                name = shortReplyName.Name;
                nameMatch = shortReplyName.Matched;
            }
        }
        if (name is null && !string.IsNullOrWhiteSpace(email))
        {
            var guess = GuessNameFromEmail(email!);
            if (!string.IsNullOrWhiteSpace(guess))
            {
                name = guess;
                nameMatch = email;
            }
        }

        // PHONE
        string? phoneRaw = !string.IsNullOrWhiteSpace(jsonPhone) ? jsonPhone : null;
        string? phoneMatch = !string.IsNullOrWhiteSpace(jsonPhone) ? jsonPhone : null;

        if (phoneRaw is null)
        {
            (phoneRaw, phoneMatch) = FindPhone(user);
            if (phoneRaw is null) (phoneRaw, phoneMatch) = FindPhone(asst);
        }
        var phoneE164 = NormalizePhoneAu(phoneRaw);

        // POSITION - labelled/phrase first, then short-reply fallback
        var position = FindPosition(lines.Select(x => x.line));
        if (string.IsNullOrWhiteSpace(position))
        {
            var shortReplyPos = FindPositionFromShortReplyAfterPrompt(convo);
            if (!string.IsNullOrWhiteSpace(shortReplyPos))
                position = shortReplyPos;
        }

        return new ContactInfo
        {
            Name = name,
            Email = email,
            PhoneE164 = phoneE164,
            Position = position,
            NameMatch = nameMatch,
            EmailMatch = emailMatch,
            PhoneMatch = phoneMatch
        };
    }

    /// <summary>
    /// Extract organization name and address from conversation
    /// </summary>
    public (string? Organisation, string? Address) ExtractOrganisationFromTranscript(IEnumerable<DisplayMessage> messages)
    {
        var list = messages?.ToList() ?? new();
        if (list.Count == 0) return (null, null);

        // Scan oldest-to-newest (organization info usually comes early in conversation)
        // Only look at early messages (first 12 user messages) where org info is typically provided
        int seen = 0;
        foreach (var m in list)
        {
            if (!string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)) continue;
            seen++;
            if (seen > 12) break; // Only check first 12 user messages

            var text = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            
            // Skip messages that are clearly NOT organization info
            if (IsEquipmentOrEventMessage(text)) continue;

            text = NormalizeText(text);

            var (org, addr) = TryParseOrgAddress(text);
            if (!string.IsNullOrWhiteSpace(org) && IsValidOrganizationName(org!))
                return (CapitalizeWords(org), CleanAddress(addr));
        }
        return (null, null);
    }

    /// <summary>
    /// Check if message is about equipment, events, or other non-org content
    /// </summary>
    private static bool IsEquipmentOrEventMessage(string text)
    {
        var lower = text.ToLowerInvariant();
        
        // Equipment-related keywords
        var equipmentKeywords = new[] {
            "projector", "laptop", "screen", "mic", "microphone", "speaker", "camera",
            "lighting", "stage", "audio", "video", "led", "cable", "tripod", "podium",
            "selected equipment", "i need", "we need", "i'll need", "we'll need",
            "i want", "we want"
        };

        // Event/action keywords that indicate this is not an org message
        var eventKeywords = new[] {
            "team building", "conference", "meeting", "presentation", "seminar", "webinar",
            "workshop", "training", "event", "ceremony", "celebration", "party",
            "schedule", "rehearsal", "setup", "pack up", "attendees", "people attending",
            "yes create", "generate quote", "make the booking", "finalize"
        };

        return equipmentKeywords.Any(kw => lower.Contains(kw)) || 
               eventKeywords.Any(kw => lower.Contains(kw));
    }

    /// <summary>
    /// Validate that a string looks like a proper organization name
    /// </summary>
    private static bool IsValidOrganizationName(string org)
    {
        if (string.IsNullOrWhiteSpace(org)) return false;
        if (org.Length < 3 || org.Length > 60) return false;

        // Must not start with common words that indicate it's not an org
        var invalidStarts = new[] {
            "i ", "we ", "yes ", "no ", "ok ", "team ", "the ", "my ", "our ",
            "selected ", "schedule ", "i'm ", "i'll ", "we're ", "we'll "
        };
        var lower = org.ToLowerInvariant();
        if (invalidStarts.Any(s => lower.StartsWith(s))) return false;

        // Must not contain action verbs
        var actionVerbs = new[] {
            " is attending", " need ", " want ", " have ", " will ", " would ",
            " can ", " should ", " could ", "projector", "laptop", "screen"
        };
        if (actionVerbs.Any(v => lower.Contains(v))) return false;

        // Should contain company-like suffixes or be a proper noun format
        var companySuffixes = new[] {
            "ltd", "limited", "pty", "inc", "corp", "corporation", "company", "co",
            "group", "holdings", "enterprises", "solutions", "services", "technologies",
            "tech", "systems", "consulting", "partners", "associates", "agency"
        };
        
        // Either has company suffix OR is 2-4 proper words
        var hasCompanySuffix = companySuffixes.Any(s => lower.Contains(s));
        var wordCount = org.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var looksLikeProperName = wordCount >= 1 && wordCount <= 5 && 
                                   char.IsUpper(org[0]) && 
                                   !lower.StartsWith("i ");

        return hasCompanySuffix || looksLikeProperName;
    }

    /// <summary>
    /// Extract structured fields from conversation (venue, date, equipment, etc.)
    /// </summary>

    /// <summary>
    /// Detect whether a user message answered laptop ownership/preference prompts.
    /// </summary>
    public LaptopAnswerSignals DetectLaptopAnswerSignals(string? userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return new LaptopAnswerSignals();

        var text = userText.Trim().ToLowerInvariant();
        var signals = new LaptopAnswerSignals();

        var ownLaptopPatterns = new[]
        {
            @"\b(bring|bringing|using|use|have|got)\s+(my|our|own)\s+(laptop|macbook|notebook)\b",
            @"\b(my|our|own)\s+(laptop|macbook|notebook)\b",
            @"\bwe(?:'ll| will)\s+bring\s+(our\s+)?(laptop|laptops|own laptop)\b",
            @"\bown\s+laptop\b"
        };

        var providedLaptopPatterns = new[]
        {
            @"\bneed\s+(a\s+)?(laptop|macbook)\b",
            @"\bprovide\s+(a\s+)?(laptop|macbook)\b",
            @"\b(please\s+)?(add|include)\s+(a\s+)?(laptop|macbook)\b",
            @"\b(laptop|macbook)\s+please\b",
            @"\brent\s+(a\s+)?(laptop|macbook)\b",
            @"\bhire\s+(a\s+)?(laptop|macbook)\b"
        };

        if (ownLaptopPatterns.Any(p => Regex.IsMatch(text, p, RegexOptions.IgnoreCase)))
        {
            signals.OwnershipAnswered = true;
            signals.NeedsProvidedLaptop = false;
        }

        if (providedLaptopPatterns.Any(p => Regex.IsMatch(text, p, RegexOptions.IgnoreCase)))
        {
            signals.OwnershipAnswered = true;
            signals.NeedsProvidedLaptop = true;
        }

        if (Regex.IsMatch(text, @"\bwindows\b|\bpc\b", RegexOptions.IgnoreCase))
        {
            signals.PreferenceAnswered = true;
            signals.Preference = "windows";
            signals.OwnershipAnswered = true;
            signals.NeedsProvidedLaptop = true;
        }
        else if (Regex.IsMatch(text, @"\bmac\b|\bmacbook\b|\bapple\b", RegexOptions.IgnoreCase))
        {
            signals.PreferenceAnswered = true;
            signals.Preference = "mac";
            signals.OwnershipAnswered = true;
            signals.NeedsProvidedLaptop = true;
        }

        return signals;
    }

    /// <summary>
    /// Extract cumulative laptop ownership/preference state from transcript.
    /// </summary>
    public LaptopAnswerState ExtractLaptopAnswerState(IEnumerable<DisplayMessage> messages)
    {
        var state = new LaptopAnswerState();
        var ordered = messages.OrderBy(m => m.Timestamp);

        foreach (var message in ordered)
        {
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = string.IsNullOrWhiteSpace(message.FullText)
                ? string.Join(" ", message.Parts ?? Enumerable.Empty<string>())
                : message.FullText;
            var signals = DetectLaptopAnswerSignals(text);
            state.Apply(signals);
        }

        return state;
    }
}

public sealed class LaptopAnswerSignals
{
    public bool OwnershipAnswered { get; set; }
    public bool NeedsProvidedLaptop { get; set; }
    public bool PreferenceAnswered { get; set; }
    public string? Preference { get; set; }
}

public sealed class LaptopAnswerState
{
    public bool OwnershipAnswered { get; private set; }
    public bool NeedsProvidedLaptop { get; private set; }
    public bool PreferenceAnswered { get; private set; }
    public string? Preference { get; private set; }

    public void Apply(LaptopAnswerSignals signals)
    {
        if (signals.OwnershipAnswered)
        {
            OwnershipAnswered = true;
            NeedsProvidedLaptop = signals.NeedsProvidedLaptop;
            if (!NeedsProvidedLaptop)
            {
                // Own laptop path means preference is not required anymore.
                PreferenceAnswered = false;
                Preference = null;
            }
        }

        if (signals.PreferenceAnswered)
        {
            OwnershipAnswered = true;
            NeedsProvidedLaptop = true;
            PreferenceAnswered = true;
            Preference = signals.Preference;
        }
    }
}
