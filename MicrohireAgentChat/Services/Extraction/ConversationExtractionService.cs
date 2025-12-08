using MicrohireAgentChat.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Extraction;

/// <summary>
/// Extracts structured data from conversation messages using heuristic patterns
/// </summary>
public sealed class ConversationExtractionService
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

        // Parse embedded UI/JSON fields if present
        var (jsonName, jsonEmail, jsonPhone) = ParseIslaFields(lines.Select(x => x.line));

        // EMAIL
        var (email, emailMatch) = !string.IsNullOrWhiteSpace(jsonEmail)
            ? (jsonEmail, jsonEmail)
            : FindEmail(user);
        if (email is null) (email, emailMatch) = FindEmail(asst);

        // NAME
        var (name, nameMatch) = !string.IsNullOrWhiteSpace(jsonName)
            ? (jsonName, jsonName)
            : FindName(user);
        if (name is null) (name, nameMatch) = FindName(asst);
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

        // POSITION
        var position = FindPosition(lines.Select(x => x.line));

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
    public Dictionary<string, string> ExtractExpectedFields(IEnumerable<DisplayMessage> messages)
    {
        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var (eventDate, venueName, dateMatched, venueMatched) = ExtractVenueAndEventDate(messages);
        if (eventDate.HasValue)
            facts["event_date"] = eventDate.Value.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(venueName))
            facts["venue_name"] = venueName!;

        var contactInfo = ExtractContactInfo(messages);
        if (!string.IsNullOrWhiteSpace(contactInfo.Name))
            facts["contact_name"] = contactInfo.Name!;
        if (!string.IsNullOrWhiteSpace(contactInfo.Email))
            facts["contact_email"] = contactInfo.Email!;
        if (!string.IsNullOrWhiteSpace(contactInfo.PhoneE164))
            facts["contact_phone"] = contactInfo.PhoneE164!;
        if (!string.IsNullOrWhiteSpace(contactInfo.Position))
            facts["contact_position"] = contactInfo.Position!;

        var (org, addr) = ExtractOrganisationFromTranscript(messages);
        if (!string.IsNullOrWhiteSpace(org))
            facts["organization"] = org!;
        if (!string.IsNullOrWhiteSpace(addr))
            facts["organization_address"] = addr!;

        // Extract selected equipment from "Selected equipment: ..." messages
        var equipment = ExtractSelectedEquipment(messages);
        if (equipment.Any())
        {
            // Store as JSON for the ItemPersistenceService
            facts["selected_equipment"] = System.Text.Json.JsonSerializer.Serialize(equipment);
        }

        // Extract times from "Schedule selected: ..." messages
        var times = ExtractScheduleTimes(messages);
        foreach (var kvp in times)
        {
            facts[kvp.Key] = kvp.Value;
        }

        return facts;
    }

    /// <summary>
    /// Extract schedule times from "Schedule selected: Setup 7:00 AM; Rehearsal 9:30 AM; ..." messages
    /// </summary>
    public Dictionary<string, string> ExtractScheduleTimes(IEnumerable<DisplayMessage> messages)
    {
        var times = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pattern: "Schedule selected: Setup 7:00 AM; Rehearsal 9:30 AM; Start 10:00 AM; End 4:00 PM; Pack Up 6:00 PM"
        var schedulePattern = @"Schedule selected:\s*(.+)";
        
        // Individual time patterns within the schedule
        var timePatterns = new Dictionary<string, string[]>
        {
            ["setup_time"] = new[] { @"Setup\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)", @"Set[- ]?up\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)" },
            ["rehearsal_time"] = new[] { @"Rehearsal\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)" },
            ["show_start_time"] = new[] { @"Start\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)", @"Event\s+Start\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)" },
            ["show_end_time"] = new[] { @"End\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)", @"Event\s+End\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)" },
            ["strike_time"] = new[] { @"Pack[- ]?Up\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)", @"Pack\s+Up\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)" }
        };

        foreach (var msg in messages)
        {
            if (!string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = string.Join("\n", msg.Parts ?? Enumerable.Empty<string>());
            
            var scheduleMatch = Regex.Match(text, schedulePattern, RegexOptions.IgnoreCase);
            if (scheduleMatch.Success)
            {
                var scheduleText = scheduleMatch.Groups[1].Value;
                _logger.LogInformation("Found schedule text: {Schedule}", scheduleText);

                foreach (var kvp in timePatterns)
                {
                    foreach (var pattern in kvp.Value)
                    {
                        var match = Regex.Match(scheduleText, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var timeStr = match.Groups[1].Value.Trim();
                            var normalized = NormalizeTimeToHHmm(timeStr);
                            if (!string.IsNullOrWhiteSpace(normalized))
                            {
                                times[kvp.Key] = normalized;
                                _logger.LogInformation("Extracted {Key}: {Value}", kvp.Key, normalized);
                            }
                            break; // Found a match for this key, move to next
                        }
                    }
                }
            }
        }

        return times;
    }

    /// <summary>
    /// Normalize time string like "7:00 AM" or "4:00 PM" to "HHmm" format
    /// </summary>
    private static string? NormalizeTimeToHHmm(string timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr))
            return null;

        // Try parsing as DateTime to handle AM/PM
        if (DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.ToString("HHmm");
        }

        // Try manual parsing for formats like "7:00 AM" or "7:30AM" or "16:00"
        var match = Regex.Match(timeStr, @"(\d{1,2})(?::(\d{2}))?\s*(AM|PM)?", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var hour = int.Parse(match.Groups[1].Value);
            var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var ampm = match.Groups[3].Success ? match.Groups[3].Value.ToUpperInvariant() : null;

            // Convert to 24-hour format
            if (ampm == "PM" && hour < 12) hour += 12;
            if (ampm == "AM" && hour == 12) hour = 0;

            return $"{hour:D2}{minute:D2}";
        }

        return null;
    }

    /// <summary>
    /// Extract selected equipment from "Selected equipment: ProductName (PRODUCT_CODE)" messages
    /// </summary>
    public List<SelectedEquipmentItem> ExtractSelectedEquipment(IEnumerable<DisplayMessage> messages)
    {
        var items = new List<SelectedEquipmentItem>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pattern: "Selected equipment: Description (PRODUCT_CODE)"
        var pattern = @"Selected equipment:\s*(.+?)\s*\(([A-Z0-9\-_]+)\s*\)";

        foreach (var msg in messages)
        {
            // Only check user messages (equipment selections come from button clicks)
            if (!string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = string.Join("\n", msg.Parts ?? Enumerable.Empty<string>());

            foreach (Match m in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
            {
                var description = m.Groups[1].Value.Trim();
                var productCode = m.Groups[2].Value.Trim();

                // Skip duplicates (same product code)
                if (seenCodes.Contains(productCode))
                    continue;

                seenCodes.Add(productCode);
                items.Add(new SelectedEquipmentItem
                {
                    ProductCode = productCode,
                    Description = description,
                    Quantity = 1 // Default to 1, can be enhanced to parse quantity
                });
            }
        }

        _logger.LogInformation("Extracted {Count} equipment items from conversation", items.Count);
        return items;
    }

    // ==================== PRIVATE HELPERS ====================

    private static bool TryParseDateToken(string token, out DateTimeOffset dto)
    {
        dto = default;

        // Try standard formats
        var formats = new[]
        {
            "d MMMM yyyy", "MMMM d yyyy", "d MMM yyyy", "MMM d yyyy",
            "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"
        };

        foreach (var fmt in formats)
        {
            if (DateTimeOffset.TryParseExact(token, fmt, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dto))
                return true;
        }

        // Try general parse
        if (DateTimeOffset.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
            return true;

        return false;
    }

    private static DateTimeOffset? FindEventDate(IEnumerable<string> lines, int? yearHint, out string? matched)
    {
        matched = null;
        var monthNames = "jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec|january|february|march|april|june|july|august|september|october|november|december";

        var patterns = new[]
        {
            $@"\b(\d{{1,2}})(st|nd|rd|th)?\s+({monthNames})\s+(\d{{4}})\b",
            $@"\b({monthNames})\s+(\d{{1,2}})(st|nd|rd|th)?(,?\s*(\d{{4}}))?\b",
            @"\b(\d{1,2})/(\d{1,2})/(\d{2,4})\b",
            @"\b(\d{4})-(\d{2})-(\d{2})\b"
        };

        foreach (var line in lines)
        {
            foreach (var pat in patterns)
            {
                foreach (Match m in Regex.Matches(line, pat, RegexOptions.IgnoreCase))
                {
                    var token = m.Value;
                    if (TryParseDateToken(token, out var dto))
                    {
                        matched = token;
                        return dto;
                    }
                }
            }
        }

        return null;
    }

    private static string? FindVenue(IEnumerable<string> lines, out string? matched)
    {
        matched = null;

        var venuePatterns = new[]
        {
            @"(?:venue|location|at):\s*([A-Z][^\n,.]{3,60})",
            @"at\s+(?:the\s+)?([A-Z][A-Za-z\s&'-]{3,60})\b"
        };

        foreach (var line in lines)
        {
            foreach (var pat in venuePatterns)
            {
                var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    matched = m.Groups[1].Value.Trim();
                    return matched;
                }
            }
        }

        return null;
    }

    private static (string? name, string? email, string? phone) ParseIslaFields(IEnumerable<string> lines)
    {
        string? name = null, email = null, phone = null;

        foreach (var line in lines)
        {
            var nameMatch = Regex.Match(line, @"Name:\s*(.+)", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
                name = nameMatch.Groups[1].Value.Trim();

            var emailMatch = Regex.Match(line, @"Email:\s*([^\s@]+@[^\s@]+\.[^\s@]+)", RegexOptions.IgnoreCase);
            if (emailMatch.Success)
                email = emailMatch.Groups[1].Value.Trim();

            var phoneMatch = Regex.Match(line, @"(?:Phone|Mobile|Cell):\s*([\d\s\(\)\-\+]+)", RegexOptions.IgnoreCase);
            if (phoneMatch.Success)
                phone = phoneMatch.Groups[1].Value.Trim();
        }

        return (name, email, phone);
    }

    private static (string? value, string? matched) FindEmail(IEnumerable<string> lines)
    {
        var pattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";

        foreach (var line in lines)
        {
            var m = Regex.Match(line, pattern);
            if (m.Success)
                return (m.Value, m.Value);
        }

        return (null, null);
    }

    private static (string? value, string? matched) FindName(IEnumerable<string> lines)
    {
        var patterns = new[]
        {
            @"(?:my name is|i'?m|this is|contact:)\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)",
            @"\bName:\s*([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)"
        };

        foreach (var line in lines)
        {
            foreach (var pat in patterns)
            {
                var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var name = m.Groups[1].Value.Trim();
                    if (name.Split(' ').Length >= 2) // require at least first + last
                        return (name, name);
                }
            }
        }

        return (null, null);
    }

    private static (string? value, string? matched) FindPhone(IEnumerable<string> lines)
    {
        var patterns = new[]
        {
            @"(?:phone|mobile|cell|tel):\s*([\d\s\(\)\-\+]{8,20})",
            @"\b(\+?\d{1,3}[\s\-]?\(?\d{2,4}\)?[\s\-]?\d{3,4}[\s\-]?\d{3,4})\b"
        };

        foreach (var line in lines)
        {
            foreach (var pat in patterns)
            {
                var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var phone = m.Groups[1].Value.Trim();
                    return (phone, phone);
                }
            }
        }

        return (null, null);
    }

    private static string? FindPosition(IEnumerable<string> lines)
    {
        var patterns = new[]
        {
            @"(?:position|title|role):\s*([A-Za-z\s]{3,50})",
            @"i'?m\s+(?:a|an|the)\s+([A-Za-z\s]{3,50})\s+(?:at|for|with)"
        };

        foreach (var line in lines)
        {
            foreach (var pat in patterns)
            {
                var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                if (m.Success)
                    return m.Groups[1].Value.Trim();
            }
        }

        return null;
    }

    private static string? GuessNameFromEmail(string email)
    {
        var local = email.Split('@')[0];
        var parts = Regex.Split(local, @"[._\-]");
        if (parts.Length >= 2)
        {
            return string.Join(" ", parts.Select(p => CapitalizeWords(p)));
        }
        return null;
    }

    private static string? NormalizePhoneAu(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = Regex.Replace(phone, @"[^\d\+]", "");

        if (digits.StartsWith("+61"))
            return digits;

        if (digits.StartsWith("61"))
            return "+" + digits;

        if (digits.StartsWith("0") && digits.Length == 10)
            return "+61" + digits.Substring(1);

        if (digits.Length == 9)
            return "+61" + digits;

        return digits.Length >= 8 ? digits : null;
    }

    private static string NormalizeText(string s)
    {
        s = s.Replace('\'', '\'').Replace('\'', '\'')
             .Replace('"', '"').Replace('"', '"')
             .Replace('–', '-').Replace('—', '-');
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private static (string? org, string? addr) TryParseOrgAddress(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);

        // Normalize
        text = text.Replace('\'', '\'').Replace('"', '"').Replace('"', '"');
        text = Regex.Replace(text, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\b(adress|addess|addres|addrss)\b", "address", RegexOptions.IgnoreCase);

        // Pattern: "Organization, address: 123 Main St"
        var pattern = @"^([^,]+),\s*address:\s*(.+)$";
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = m.Groups[1].Value.Trim();
            var addr = m.Groups[2].Value.Trim();
            return (org, addr);
        }

        // Pattern: "Organization at 123 Main St"
        pattern = @"^(.+?)\s+(?:at|@)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = m.Groups[1].Value.Trim();
            var addr = m.Groups[2].Value.Trim();
            if (addr.Any(char.IsDigit)) // likely an address
                return (org, addr);
        }

        // Pattern: "X is the name, address is Y" or "X is the organization, address is Y"
        pattern = @"^(.+?)\s+is\s+the\s+(?:name|organization|company),\s*address\s+is\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = m.Groups[1].Value.Trim();
            var addr = m.Groups[2].Value.Trim();
            return (org, addr);
        }

        // Pattern: "Organization is X, address is Y"
        pattern = @"^(?:organization|company|org)\s+is\s+([^,]+),\s*address\s+is\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = m.Groups[1].Value.Trim();
            var addr = m.Groups[2].Value.Trim();
            return (org, addr);
        }

        // Pattern: "X is the company. Y is the" pattern
        var re5 = new Regex(@"^(?<org>.+?)\s+is\s+the\s+company\.?\s*(?<addr>.+?)\s+is\s+the",
            RegexOptions.IgnoreCase);
        var m5 = re5.Match(text);
        if (m5.Success) return (m5.Groups["org"].Value.Trim(), m5.Groups["addr"].Value.Trim());

        // Pattern: "company is X. address is Y"
        var re6 = new Regex(@"company\s+is\s+(?<org>.+?)\.?\s+address\s+is\s+(?<addr>.+?)$",
            RegexOptions.IgnoreCase);
        var m6 = re6.Match(text);
        if (m6.Success) return (m6.Groups["org"].Value.Trim(), m6.Groups["addr"].Value.Trim());

        // Fallback: split on common separators
        var separators = new[] { ". ", ", ", " and ", " & " };
        foreach (var sep in separators)
        {
            var idx = text.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                var part1 = text[..idx].Trim();
                var part2 = text[(idx + sep.Length)..].Trim();

                // Check if part1 looks like a company name and part2 like an address
                if (part1.Length > 3 && part1.Length < 50 && part2.Length > 5 && part2.Length < 100)
                {
                    // Simple heuristic: if part2 contains numbers or common address words
                    var addrWords = new[] { "street", "st", "road", "rd", "avenue", "ave", "city", "nyc", "melbourne" };
                    if (part2.Any(char.IsDigit) || addrWords.Any(w => part2.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    {
                        return (part1, part2);
                    }
                }
            }
        }

        return (null, null);
    }

    private static string? CleanAddress(string? addr)
    {
        if (string.IsNullOrWhiteSpace(addr)) return null;
        var result = Regex.Replace(addr!, @"\s+", " ").Trim(',', ';', ':');
        // Truncate to database column length (varchar(200))
        return result.Length > 200 ? result[..200] : result;
    }

    private static string? CapitalizeWords(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var result = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s!.ToLowerInvariant());
        // Truncate to database column length (varchar(50))
        return result.Length > 50 ? result[..50] : result;
    }
}

/// <summary>
/// Contact information extracted from conversation
/// </summary>
public class ContactInfo
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? PhoneE164 { get; set; }
    public string? Position { get; set; }
    public string? NameMatch { get; set; }
    public string? EmailMatch { get; set; }
    public string? PhoneMatch { get; set; }
}

/// <summary>
/// Equipment item selected from gallery picker
/// </summary>
public class SelectedEquipmentItem
{
    public string ProductCode { get; set; } = "";
    public string Description { get; set; } = "";
    public int Quantity { get; set; } = 1;
}
