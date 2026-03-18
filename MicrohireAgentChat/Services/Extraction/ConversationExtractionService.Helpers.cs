using MicrohireAgentChat.Models;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MicrohireAgentChat.Services.Extraction;

public sealed partial class ConversationExtractionService
{
    private bool TryParseDateToken(string token, out DateTimeOffset dto)
    {
        dto = default;
        if (string.IsNullOrWhiteSpace(token)) return false;

        token = token.Trim();
        // Remove ordinal suffixes and "of" to normalize tokens like "15th of February"
        token = Regex.Replace(token, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);
        token = Regex.Replace(token, @"\b(\d{1,2})\s+of\s+", "$1 ", RegexOptions.IgnoreCase);

        var now = DateTimeOffset.Now;
        // Consider the token missing a year if no 4-digit year is present
        var hasExplicitYear = Regex.IsMatch(token, @"\b\d{4}\b");

        // LOGGING: Initial parsing attempt
        _logger.LogInformation("CONV EXTRACTION DATE PARSING: Starting to parse token '{Token}'. Has explicit year: {HasYear}, Current time: {Now}", token, hasExplicitYear, now);

        var cultures = new[]
        {
            CultureInfo.GetCultureInfo("en-US"),
            CultureInfo.GetCultureInfo("en-GB"),
            CultureInfo.InvariantCulture
        };

        // If the year is missing, try parsing with the current year appended first
        var candidates = hasExplicitYear
            ? new[] { token }
            : new[] { $"{token} {now.Year}", token };

        _logger.LogInformation("CONV EXTRACTION DATE PARSING: Candidates to try: {Candidates}", string.Join(", ", candidates));

        foreach (var candidate in candidates)
        {
            _logger.LogInformation("CONV EXTRACTION DATE PARSING: Trying candidate '{Candidate}'", candidate);

            // ISO (yyyy-MM-dd)
            if (Regex.IsMatch(candidate, @"^\d{4}-\d{2}-\d{2}$") &&
                DateTime.TryParseExact(candidate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var isoDt))
            {
                dto = new DateTimeOffset(isoDt);
                _logger.LogInformation("CONV EXTRACTION DATE PARSING: Parsed ISO format '{Candidate}' to {ParsedDate}", candidate, dto);
                break;
            }

            // Slash formats (dd/MM/yyyy, etc.)
            if (Regex.IsMatch(candidate, @"^\d{1,2}/\d{1,2}/\d{2,4}$"))
            {
                var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "MM/dd/yyyy", "M/d/yyyy" };
                foreach (var fmt in fmts)
                {
                    if (DateTime.TryParseExact(candidate, fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dmy))
                    {
                        dto = new DateTimeOffset(dmy);
                        _logger.LogInformation("CONV EXTRACTION DATE PARSING: Parsed slash format '{Candidate}' with format '{Format}' to {ParsedDate}", candidate, fmt, dto);
                        break;
                    }
                }
                if (dto != default) break;
            }

            // General parsing with culture fallbacks
            foreach (var c in cultures)
            {
                if (DateTime.TryParse(candidate, c, DateTimeStyles.AssumeLocal, out var dt))
                {
                    dto = new DateTimeOffset(dt);
                    _logger.LogInformation("CONV EXTRACTION DATE PARSING: Parsed with culture '{Culture}' '{Candidate}' to {ParsedDate}", c.Name, candidate, dto);
                    break;
                }
            }

            if (dto != default) break;
        }

        if (dto == default)
        {
            _logger.LogWarning("CONV EXTRACTION DATE PARSING: Failed to parse token '{Token}' with any method", token);
            return false;
        }

        _logger.LogInformation("CONV EXTRACTION DATE PARSING: Initial parse result for '{Token}': {ParsedDate}", token, dto);

        // Always apply smart date detection: roll forward until the date is in the future
        // This handles both cases: explicit years that are wrong, and implicit years
        var originalDate = dto;
        var normalized = new DateTimeOffset(dto.Date, dto.Offset);

        _logger.LogInformation("CONV EXTRACTION DATE PARSING: Before roll-forward - Original: {Original}, Normalized: {Normalized}, Current time: {Now}", originalDate, normalized, now);

        int rollForwardCount = 0;
        while (normalized.Date < now.Date)
        {
            var beforeRoll = normalized;
            normalized = normalized.AddYears(1);
            rollForwardCount++;
            _logger.LogInformation("CONV EXTRACTION DATE PARSING: Rolled forward from {Before} to {After} (iteration {Count})", beforeRoll.Date, normalized.Date, rollForwardCount);
        }

        dto = normalized;

        _logger.LogInformation("CONV EXTRACTION DATE PARSING: Final result for '{Token}': {FinalDate} (rolled forward {Count} times)", token, dto, rollForwardCount);

        return true;
    }

    private DateTimeOffset? FindEventDate(IEnumerable<string> lines, int? yearHint, out string? matched)
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
        
        // Known venue names/keywords to look for
        var knownVenues = new[] {
            "westin", "hilton", "marriott", "sheraton", "hyatt", "intercontinental", "sofitel",
            "novotel", "mercure", "ibis", "pullman", "crowne plaza", "rydges", "stamford",
            "convention centre", "convention center", "expo", "exhibition", "conference centre",
            "ballroom", "grand ballroom", "function room", "banquet hall"
        };

        // First, look for known venue names in the text
        foreach (var line in lines)
        {
            var lineLower = line.ToLower();
            
            // Skip lines that look like questions (from assistant)
            if (lineLower.Contains("what is your") || lineLower.Contains("could you") || 
                lineLower.Contains("please provide") || lineLower.Contains("?"))
                continue;
                
            foreach (var venue in knownVenues)
            {
                if (lineLower.Contains(venue))
                {
                    // Try to extract the full venue name with context
                    // Pattern: "Westin Brisbane" or "The Westin Brisbane" or "at Westin Brisbane"
                    var pattern = $@"(?:at\s+)?(?:the\s+)?({Regex.Escape(venue)}[A-Za-z\s&'-]{{0,40}})";
                    var m = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        matched = m.Groups[1].Value.Trim();
                        // Clean up trailing words that aren't part of venue
                        matched = Regex.Replace(matched, @"\s+(?:in|for|with|style|would|be|perfect|please).*$", "", RegexOptions.IgnoreCase).Trim();
                        if (matched.Length > 3) return matched;
                    }
                }
            }
        }

        // Explicit patterns
        var venuePatterns = new[]
        {
            @"(?:venue|location):\s*([A-Z][^\n,.]{3,60})",
            @"(?:venue|location)\s+(?:is|will be|would be)\s+(?:the\s+)?([A-Z][A-Za-z\s&'-]{3,60})",
            @"at\s+(?:the\s+)?([A-Z][A-Za-z\s&'-]{3,60})(?:\s+(?:hotel|centre|center|ballroom|room))",
            @"(?:the\s+)?([A-Z][A-Za-z\s&'-]{3,40})\s+(?:hotel|convention|conference|ballroom)"
        };

        foreach (var line in lines)
        {
            // Skip question lines
            if (line.Contains("?")) continue;
            
            foreach (var pat in venuePatterns)
            {
                var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var candidate = m.Groups[1].Value.Trim();
                    // Validate it's not a common word/phrase
                    if (!IsCommonPhrase(candidate) && candidate.Length > 3)
                    {
                        matched = candidate;
                        return matched;
                    }
                }
            }
        }

        return null;
    }
    
    /// <summary>
    /// Check if a string is a common phrase that shouldn't be extracted as venue/org
    /// </summary>
    private static bool IsCommonPhrase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        
        var commonPhrases = new[] {
            "what is your", "is your full", "full name", "your name", "your email", "your phone",
            "could you", "please provide", "let me", "thank you", "hi there", "hello",
            "how can", "i can", "we can", "i will", "we will"
        };
        
        var lower = text.ToLower().Trim();
        return commonPhrases.Any(p => lower.Contains(p)) || lower.Length < 4;
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

    /// <summary>True if the name is the assistant's (Isla, Microhire, or both). Allows e.g. "Isla Smith".</summary>
    private static bool LooksLikeAssistantName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().ToLowerInvariant();
        return t == "isla" || t == "microhire" || (t.Contains("isla") && t.Contains("microhire"));
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
            @"(?:my name is|i am|i'?m|this is|contact:)\s+([A-Za-z][A-Za-z'’-]+(?:\s+[A-Za-z][A-Za-z'’-]+){1,3})",
            @"\bname\s*(?:is|:)\s*([A-Za-z][A-Za-z'’-]+(?:\s+[A-Za-z][A-Za-z'’-]+){1,3})"
        };

        foreach (var line in lines)
        {
            foreach (var pat in patterns)
            {
                var m = Regex.Match(line, pat, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var name = CleanExtractedName(m.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(name))
                        return (name, m.Value.Trim());
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
                    return NormalizeExtractedPosition(m.Groups[1].Value.Trim());
            }
        }

        return null;
    }

    /// <summary>
    /// Extract position from short user reply after assistant asks for position/role.
    /// Handles cases like: "Could you let me know your position or role?" -> "Owner"
    /// </summary>
    private static string? FindPositionFromShortReplyAfterPrompt(List<(string role, string line)> convo)
    {
        var prompt = new Regex(
            @"\b(?:what'?s\s+your\s+(?:position|title|role)|what\s+is\s+your\s+(?:position|title|role)|"
          + @"your\s+(?:position|title|role)\??|"
          + @"(?:could you|could I)\s+(?:let me know|tell me)\s+your\s+(?:position|role|title)|"
          + @"(?:position|role|title)\s+(?:or\s+role|or\s+position)|"
          + @"lastly.*(?:position|role|title))\b",
            RegexOptions.IgnoreCase);

        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "yes", "yeah", "yep", "no", "nope", "skip", "hi", "hello", "thanks", "thank you",
            "ok", "okay", "sure", "fine", "good", "optional", "none", "n/a", "na"
        };

        for (int i = 0; i < convo.Count; i++)
        {
            var (role, line) = convo[i];
            if (!role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;
            if (!prompt.IsMatch(line)) continue;

            for (int j = i + 1; j < Math.Min(i + 4, convo.Count); j++)
            {
                var (r2, l2) = convo[j];
                if (!r2.Equals("user", StringComparison.OrdinalIgnoreCase)) continue;

                var candidate = l2.Trim().Trim(',', '.', ';', '!', '?', ':', '"', '\'');
                var normalized = NormalizeExtractedPosition(candidate);
                if (string.IsNullOrWhiteSpace(normalized)) continue;
                if (stop.Contains(normalized)) continue;
                if (candidate.Contains("@") || Regex.IsMatch(candidate, @"\+?\d[\d\s\-()]{6,}\d")) continue;

                return normalized;
            }
        }

        return null;
    }

    private static string? NormalizeExtractedPosition(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        var t = Regex.Replace(candidate, @"\s+", " ").Trim();
        if (t.Length < 2 || t.Length > 50) return null;

        var lower = t.ToLowerInvariant();
        if (lower is "no" or "none" or "n/a" or "na" or "nil" or "unknown" or "tbc" or "skip" or "optional") return null;
        if (LooksLikeAssistantName(t)) return null;

        return CapitalizeWords(t);
    }

    private static (string? Name, string? Matched) FindNameFromShortReplyAfterPrompt(
        List<(string role, string line)> convo)
    {
        var prompt = new Regex(
            @"\b(what('?s| is)\s+your\s+(full\s+)?name|may\s+i\s+have\s+your\s+name|your\s+(full\s+)?name\??|"
          + @"pardon\s+my\s+manners.*name|can\s+i\s+grab\s+your\s+name)\b",
            RegexOptions.IgnoreCase);

        for (int i = 0; i < convo.Count; i++)
        {
            var (role, line) = convo[i];
            if (!role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;
            if (!prompt.IsMatch(line)) continue;

            for (int j = i + 1; j < Math.Min(i + 4, convo.Count); j++)
            {
                var (r2, l2) = convo[j];
                if (!r2.Equals("user", StringComparison.OrdinalIgnoreCase)) continue;

                var candidate = l2.Trim().Trim(',', '.', ';', '!', '?', ':', '"', '\'');
                var cleaned = CleanExtractedName(candidate);
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return (cleaned, l2);
            }
        }

        return (null, null);
    }

    private static string? GuessNameFromEmail(string email)
    {
        var local = email.Split('@')[0];
        var parts = Regex.Split(local, @"[._\-]");
        if (parts.Length >= 2)
        {
            var filtered = parts
                .Select(p => p.Trim())
                .Where(p => p.Length >= 2 && p.All(char.IsLetter))
                .Where(p => !IsGenericEmailToken(p))
                .Take(3)
                .ToArray();

            if (filtered.Length >= 2)
            {
                var guess = string.Join(" ", filtered.Select(p => CapitalizeWords(p)));
                return CleanExtractedName(guess);
            }
        }
        return null;
    }

    private static bool IsGenericEmailToken(string token)
    {
        var t = token.Trim().ToLowerInvariant();
        return t is "info" or "admin" or "support" or "sales" or "hello" or "team" or "contact" or "bookings" or "events" or "accounts" or "noreply" or "no-reply";
    }

    private static string? CleanExtractedName(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        var normalized = Regex.Replace(candidate, @"\s+", " ").Trim();

        // Strip label-style prefixes if they leaked into captured content.
        normalized = Regex.Replace(normalized, @"^(?:name|contact)\s*(?:is|:)\s*", "", RegexOptions.IgnoreCase).Trim();

        if (LooksLikeAssistantName(normalized))
            return null;

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length > 4)
            return null;

        foreach (var word in words)
        {
            var alpha = word.Replace("'", "").Replace("-", "").Replace("’", "");
            if (alpha.Length < 2 || !alpha.All(char.IsLetter))
                return null;
            if (LooksLikeAssistantNamePart(alpha))
                return null;
        }

        return CapitalizeWords(normalized);
    }

    /// <summary>True if the part (first/middle/last) is the assistant's name token.</summary>
    private static bool LooksLikeAssistantNamePart(string? part)
    {
        if (string.IsNullOrWhiteSpace(part)) return false;
        var t = part.Trim().ToLowerInvariant();
        return t == "isla" || t == "microhire";
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

    /// <summary>
    /// Parses a single projector area identifier from a line of text.
    /// When <paramref name="inProjectorContext"/> is true, bare single-letter replies are also accepted.
    /// </summary>
    private static string? ParseProjectorAreaFromText(string text, bool inProjectorContext = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Trim();

        // Bare single-letter reply (e.g. "A") — only safe when the projector prompt was already shown
        if (inProjectorContext && normalized.Length == 1 && char.IsLetter(normalized[0]))
        {
            var letter = char.ToUpperInvariant(normalized[0]);
            if (letter is >= 'A' and <= 'F')
                return letter.ToString();
        }

        // "Area A", "projector area C" — unambiguous regardless of context
        var patterns = new[]
        {
            @"\bprojector\s+area\s*[:\-]?\s*([A-F])\b",
            @"\barea\s*[:\-]?\s*([A-F])\b",
        };

        foreach (var pat in patterns)
        {
            var m = Regex.Match(normalized, pat, RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value.ToUpperInvariant();
        }

        return null;
    }

    /// <summary>
    /// Parses all projector area identifiers from a line of text.
    /// When <paramref name="inProjectorContext"/> is true, bare A-F letters are also matched.
    /// </summary>
    private static List<string> ParseProjectorAreasFromText(string text, bool inProjectorContext = false)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var normalized = text.Trim();

        if (inProjectorContext)
        {
            // User is responding to the projector prompt — accept bare A-F letters
            foreach (Match m in Regex.Matches(normalized, @"\b([A-F])\b", RegexOptions.IgnoreCase))
            {
                var area = m.Groups[1].Value.ToUpperInvariant();
                if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                    result.Add(area);
            }
        }
        else
        {
            // Require explicit "area" or "projector area" keyword to avoid false positives
            foreach (Match m in Regex.Matches(normalized, @"\b(?:projector\s+area|area)\s*[:\-]?\s*([A-F])\b", RegexOptions.IgnoreCase))
            {
                var area = m.Groups[1].Value.ToUpperInvariant();
                if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                    result.Add(area);
            }
        }

        return result;
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

        // Normalize quotes to standard ASCII
        text = text.Replace('\u2018', '\'').Replace('\u2019', '\'').Replace('\u201C', '"').Replace('\u201D', '"');
        text = Regex.Replace(text, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\b(adress|addess|addres|addrss|adreess|addrees|adrress)\b", "address", RegexOptions.IgnoreCase);
        
        // Preprocess: Strip leading verbs like "Called", "Named", "Is", etc. from the entire text
        // This handles cases like "Called THE gully, located in winston glades"
        var verbPrefixPattern = new Regex(@"^(?:called|named|is|are|company\s+is|business\s+is|organisation\s+is|organization\s+is|org\s+is|the\s+company\s+is|the\s+business\s+is)\s+(.+)$", RegexOptions.IgnoreCase);
        var verbMatch = verbPrefixPattern.Match(text);
        if (verbMatch.Success)
            text = verbMatch.Groups[1].Value.Trim();
        
        // Common Australian suburbs for location matching
        var ausSuburbs = new[] { 
            "ascot", "kelvin grove", "fortitude valley", "south bank", "southbank", "brisbane", 
            "sydney", "melbourne", "perth", "adelaide", "hobart", "darwin", "canberra",
            "parramatta", "chatswood", "bondi", "surry hills", "newtown", "redfern",
            "st kilda", "richmond", "carlton", "fitzroy", "south yarra", "toorak"
        };
        
        Match m;
        string pattern;

        // ============= NATURAL LANGUAGE PATTERNS (highest priority) =============
        
        // Pattern: "it's X, our office is in Y" / "it's X, we're in Y" / "it's X, we are in Y"
        pattern = @"^(?:it'?s|its)\s+(.+?),\s*(?:our\s+)?(?:office|we(?:'re|'re| are)?)\s+(?:is\s+)?(?:in|at)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }
        
        // Pattern: "it's X, located in Y" / "it's X, based in Y"
        pattern = @"^(?:it'?s|its)\s+(.+?),\s*(?:located|based|situated)\s+(?:in|at)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }
        
        // Pattern: "X, our office is in Y" (without "it's")
        pattern = @"^(.+?),\s*(?:our\s+)?office\s+is\s+(?:in|at)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }
        
        // Pattern: "X, we're in Y" / "X, we are in Y" / "X, we're based in Y"
        pattern = @"^(.+?),\s*we(?:'re|'re| are)\s+(?:based\s+|located\s+)?(?:in|at)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }
        
        // Pattern: "we're X, in Y" / "we are X, in Y"
        pattern = @"^we(?:'re|'re| are)\s+(.+?),\s*(?:in|at|based in|located in)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }

        // ============= EXPLICIT PATTERNS =============
        
        // Pattern: "Organization, address: 123 Main St"
        pattern = @"^([^,]+),\s*address:\s*(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }

        // Pattern: "Organization at 123 Main St" (requires digit or suburb in addr)
        pattern = @"^(.+?)\s+(?:at|@)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim().ToLower();
            if (IsValidOrgCandidate(org) && (addr.Any(char.IsDigit) || ausSuburbs.Any(s => addr.Contains(s))))
                return (org, m.Groups[2].Value.Trim());
        }

        // Pattern: "Organization headquartered in Location" or "Organization based in Location"
        pattern = @"^(.+?)\s+(?:headquartered|based|located|situated)\s+(?:in|at)\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }

        // Pattern: "X is the name, address is Y" or "X is the organization, address is Y"
        pattern = @"^(.+?)\s+is\s+the\s+(?:name|organization|organisation|company),\s*(?:address|location)\s+is\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }

        // Pattern: "Organization is X, address is Y"
        pattern = @"^(?:organization|organisation|company|org)\s+(?:name\s+)?is\s+([^,]+),\s*(?:address|location)\s+is\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }

        // Pattern: "Organization is X address is Y" (no comma)
        pattern = @"^(?:organization|organisation|company|org)\s+(?:name\s+)?is\s+(.+?)\s+(?:address|location)\s+is\s+(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }

        // Pattern: "X (organization), Y (address)" - flexible comma separation
        pattern = @"^(?:organization|organisation|company|org|name)\s*(?:is|:)?\s*(.+?),\s*(?:address|location|based\s+(?:in|at))\s*(?:is|:)?\s*(.+)$";
        m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var org = CleanOrgName(m.Groups[1].Value);
            var addr = m.Groups[2].Value.Trim();
            if (IsValidOrgCandidate(org)) return (org, addr);
        }

        // Pattern: "X is the company. Y is the" pattern
        var re5 = new Regex(@"^(?<org>.+?)\s+is\s+the\s+company\.?\s*(?<addr>.+?)\s+is\s+the",
            RegexOptions.IgnoreCase);
        var m5 = re5.Match(text);
        if (m5.Success)
        {
            var org = CleanOrgName(m5.Groups["org"].Value);
            if (IsValidOrgCandidate(org)) return (org, m5.Groups["addr"].Value.Trim());
        }

        // Pattern: "company is X. address is Y"
        var re6 = new Regex(@"company\s+is\s+(?<org>.+?)\.?\s+address\s+is\s+(?<addr>.+?)$",
            RegexOptions.IgnoreCase);
        var m6 = re6.Match(text);
        if (m6.Success)
        {
            var org = CleanOrgName(m6.Groups["org"].Value);
            if (IsValidOrgCandidate(org)) return (org, m6.Groups["addr"].Value.Trim());
        }

        // ============= SMART FALLBACK: comma-separated with location hint =============
        // Pattern: "X, Y" where Y contains location words or is a known suburb
        var commaIdx = text.IndexOf(',');
        if (commaIdx > 3)
        {
            var part1 = text[..commaIdx].Trim();
            var part2 = text[(commaIdx + 1)..].Trim();
            
            // Clean up part1 (remove "it's", "we're", etc.)
            part1 = CleanOrgName(part1);
            
            // Check if part2 looks like a location
            var part2Lower = part2.ToLower();
            var locationWords = new[] { "in ", "at ", "office", "based", "located", "street", "st ", "road", "rd ", "avenue", "ave " };
            var hasLocationWord = locationWords.Any(w => part2Lower.Contains(w));
            var hasSuburb = ausSuburbs.Any(s => part2Lower.Contains(s));
            
            if (IsValidOrgCandidate(part1) && (hasLocationWord || hasSuburb))
            {
                // Extract just the location from part2
                var addrMatch = Regex.Match(part2, @"(?:in|at)\s+(.+)$", RegexOptions.IgnoreCase);
                var addr = addrMatch.Success ? addrMatch.Groups[1].Value.Trim() : part2;
                return (part1, addr);
            }
        }

        return (null, null);
    }
    
    /// <summary>
    /// Clean organization name by removing common prefixes
    /// </summary>
    private static string CleanOrgName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        
        name = name.Trim().TrimEnd(',', '.', ';');
        
        // Remove common prefixes - including verbs like "called", "named", "is", etc.
        var prefixes = new[] { 
            "it's ", "its ", "it is ", "we're ", "we are ", "i work for ", "i work at ", "from ",
            "called ", "named ", "is ", "are ", "company is ", "business is ", "organisation is ", 
            "organization is ", "org is ", "the company is ", "the business is "
        };
        foreach (var prefix in prefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                name = name[prefix.Length..].Trim();
        }
        
        // Also handle patterns like "called THE gully" -> "THE gully"
        var verbPattern = new Regex(@"^(?:called|named|is|are|company\s+is|business\s+is|organisation\s+is|organization\s+is|org\s+is|the\s+company\s+is|the\s+business\s+is)\s+(.+)$", RegexOptions.IgnoreCase);
        var match = verbPattern.Match(name);
        if (match.Success)
            name = match.Groups[1].Value.Trim();
        
        return name;
    }
    
    /// <summary>
    /// Check if a string looks like a valid organization name candidate
    /// </summary>
    private static bool IsValidOrgCandidate(string? org)
    {
        if (string.IsNullOrWhiteSpace(org)) return false;
        if (org.Length < 3 || org.Length > 60) return false;
        
        // Must not be common phrases
        var invalidPhrases = new[] { "yes", "no", "ok", "okay", "sure", "thanks", "thank you", "please", "hello", "hi" };
        if (invalidPhrases.Contains(org.ToLower())) return false;
        
        return true;
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
    public bool? IsPackage { get; set; }
    public string? ParentPackageCode { get; set; }
    /// <summary>Job comment for operator (e.g. "Client requested: Handheld" for QLXD2SK mic kit).</summary>
    public string? Comment { get; set; }
}

public class SelectedLaborItem
{
    public string ProductCode { get; set; } = "AVTECH";
    public string Description { get; set; } = "";
    public string Task { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public double Hours { get; set; }
    public int Minutes { get; set; }
}

/// <summary>
/// Comprehensive event information extracted from conversation
/// </summary>
public class EventInformation
{
    public decimal? Budget { get; set; }
    public string? BudgetMatch { get; set; }
    public int? Attendees { get; set; }
    public string? AttendeesMatch { get; set; }
    public string? SetupStyle { get; set; }
    public string? SetupMatch { get; set; }
    public string? Venue { get; set; }
    public string? VenueMatch { get; set; }
    public string? SpecialRequests { get; set; }
    public string? SpecialRequestsMatch { get; set; }
    public List<string>? Dates { get; set; }

    /// <summary>
    /// Check if any information was extracted
    /// </summary>
    public bool HasInformation()
    {
        return Budget.HasValue || Attendees.HasValue || !string.IsNullOrWhiteSpace(SetupStyle) ||
               !string.IsNullOrWhiteSpace(Venue) || !string.IsNullOrWhiteSpace(SpecialRequests) ||
               (Dates != null && Dates.Count > 0);
    }
}
