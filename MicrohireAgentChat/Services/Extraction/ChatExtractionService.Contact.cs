using MicrohireAgentChat.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MicrohireAgentChat.Services.Extraction;

public sealed partial class ChatExtractionService
{
    public sealed record ContactInfo(
        string? Name,
        string? Email,
        string? PhoneE164,
        string? NameMatched,
        string? EmailMatched,
        string? PhoneMatched,
        string? Position
    );

    private static (string? Name, string? Matched) FindNameNearEmailOrPhone(
        IEnumerable<string> userLines, string? email, string? phone)
    {
        string? phoneTail = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length >= 6) phoneTail = digits[^6..];
        }

        foreach (var line in userLines.Reverse())
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var l = line.Trim();

            bool containsEmailToken = l.Contains("@", StringComparison.Ordinal);
            bool containsExactEmail = !string.IsNullOrWhiteSpace(email) &&
                                      l.IndexOf(email, StringComparison.OrdinalIgnoreCase) >= 0;

            bool containsPhone = false;
            if (phoneTail is not null)
            {
                var ld = new string(l.Where(char.IsDigit).ToArray());
                containsPhone = ld.Contains(phoneTail, StringComparison.Ordinal);
            }

            if (!(containsExactEmail || containsEmailToken || containsPhone)) continue;

            var chunks = l.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .ToList();

            foreach (var raw in chunks)
            {
                var s = Regex.Replace(raw, @"\b(my name is|i am|i'm|this is)\b", "", RegexOptions.IgnoreCase).Trim();
                if (s.Contains("@")) continue;
                if (Regex.IsMatch(s, @"\d")) continue;
                if (Regex.IsMatch(s, @"\bisla\b", RegexOptions.IgnoreCase)) continue;

                if (LooksLikeHumanName(s))
                    return (ToTitle(s), line);
            }
        }

        return (null, null);

        static bool LooksLikeHumanName(string s)
        {
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 3) return false;
            if (!parts.All(p => Regex.IsMatch(p, @"^[A-Za-z][a-z]+$"))) return false;
            
            // Reject names starting with articles
            var first = parts[0].ToLowerInvariant();
            if (first is "the" or "a" or "an") return false;
            
            // Reject common job titles/roles that aren't names
            var lower = s.ToLowerInvariant();
            var jobTitles = new[] { "director", "manager", "engineer", "developer", "officer", "assistant", 
                "coordinator", "supervisor", "executive", "president", "owner", "founder", "ceo", "cto", "cfo" };
            if (jobTitles.Any(t => lower.Contains(t))) return false;
            
            return true;
        }

        static string ToTitle(string s) =>
            CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

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
        var all = user.Concat(asst).ToList();

        // ---- parse from any embedded UI/JSON fields if present (existing helper) ----
        var (jsonName, jsonEmail, jsonPhone) = ParseIslaFields(all);

        // ---- EMAIL ----
        var (email, emailMatch) = !string.IsNullOrWhiteSpace(jsonEmail)
            ? (jsonEmail, jsonEmail)
            : FindEmail(user);
        if (email is null) (email, emailMatch) = FindEmail(asst);

        // ---- NAME - filter out assistant names from jsonName before using it ----
        var (name, nameMatch) = !string.IsNullOrWhiteSpace(jsonName) && !LooksLikeAssistantName(jsonName)
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

        // ---- PHONE ----
        string? phoneRaw = !string.IsNullOrWhiteSpace(jsonPhone) ? jsonPhone : null;
        string? phoneMatch = !string.IsNullOrWhiteSpace(jsonPhone) ? jsonPhone : null;

        if (phoneRaw is null)
        {
            (phoneRaw, phoneMatch) = FindPhone(user);
            if (phoneRaw is null) (phoneRaw, phoneMatch) = FindPhone(asst);
        }
        var phoneE164 = NormalizePhoneAu(phoneRaw);

        // Full convo (role + line) for short-reply name prompt detection
        var convo = lines.Select(x => (role: x.role, line: x.line)).ToList();

        if (string.IsNullOrWhiteSpace(jsonName))
        {
            var nr = FindNameFromShortReplyAfterPrompt(convo);
            if (!string.IsNullOrWhiteSpace(nr.Name))
            {
                name = nr.Name;
                nameMatch = nr.Matched;
            }
        }

        if (name is null)
        {
            (name, nameMatch) = FindNameNearEmailOrPhone(user, email, phoneRaw ?? phoneE164);
        }
        if (name is null)
        {
            (name, nameMatch) = FindName(user);
        }
        if (name is null && !string.IsNullOrWhiteSpace(email))
        {
            var guess = GuessNameFromEmail(email!);
            if (!string.IsNullOrWhiteSpace(guess)) { name = guess; nameMatch = email; }
        }
        if (name is null)
        {
            (name, nameMatch) = FindName(asst);
            if (!string.IsNullOrWhiteSpace(name) &&
                (Regex.IsMatch(name, @"\bisla\b", RegexOptions.IgnoreCase) ||
                 Regex.IsMatch(name, @"\bmicrohire\b", RegexOptions.IgnoreCase)))
            { name = null; nameMatch = null; }
        }

        // ---- POSITION / ROLE / TITLE ----
        // 1) From JSON-like lines if present: "position": "...", "role": "...", "title": "..."
        string? position = FindPositionFromJsonLike(all);

        // 2) From free text (prefer user lines, then assistant)
        if (string.IsNullOrWhiteSpace(position))
        {
            (position, _) = FindPositionFromText(user);
            if (string.IsNullOrWhiteSpace(position))
                (position, _) = FindPositionFromText(asst);
        }

        // Final tidy/cap
        position = CleanPosition(position);

        return new ContactInfo(
            Name: name,
            Email: email,
            PhoneE164: phoneE164,
            NameMatched: nameMatch,
            EmailMatched: emailMatch,
            PhoneMatched: phoneMatch,
            Position: position
        );

        // ---------------- helpers (local) ----------------

        static string? CleanPosition(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();

            // stop at trailing org clause
            // e.g. "Head of Events at Microhire" -> "Head of Events"
            t = Regex.Replace(t, @"\s+(?:at|with)\s+.+$", "", RegexOptions.IgnoreCase).Trim();

            // strip trailing punctuation
            t = t.Trim().TrimEnd('.', ',', ';', ':').Trim();

            // discard obviously bad captures
            if (t.Length < 2 || t.Length > 80) return null;
            if (Regex.IsMatch(t, @"\b(isla|microhire)\b", RegexOptions.IgnoreCase)) return null;

            // Title-case but preserve obvious acronyms (CEO, CTO, VP)
            string Titleish(string x)
            {
                var ti = CultureInfo.CurrentCulture.TextInfo;
                var words = x.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < words.Length; i++)
                {
                    var w = words[i];
                    if (w.Length <= 4 && w.ToUpperInvariant() == w) continue; // keep acronyms
                    words[i] = ti.ToTitleCase(w.ToLowerInvariant());
                }
                return string.Join(' ', words);
            }

            return Titleish(t);
        }

        static string? FindPositionFromJsonLike(IEnumerable<string> lines)
        {
            // look for "position": "...", "role": "...", "title": "..."
            foreach (var ln in lines)
            {
                // quick reject if no colon
                if (!ln.Contains(":")) continue;

                var m = Regex.Match(ln,
                    @"(?:""position""|""role""|""title"")\s*:\s*""(?<v>[^""]+)""",
                    RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups["v"].Value;
            }
            return null;
        }

        static (string? pos, string? matched) FindPositionFromText(IEnumerable<string> lines)
        {
            // Pattern A: "Position: X", "Role - X", "Title: X"
            var reLabel = new Regex(
                @"(?:^|\b)(position|role|title)\s*[:\-]\s*(?<v>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Pattern B: "My position is X", "I work as a/an X", "I'm the X at ..."
            var rePhrase = new Regex(
                @"\b(?:my\s+position\s+is|i\s+work\s+as|i'?m\s+(?:a|an|the)|i\s+am\s+(?:a|an|the))\s+(?<v>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var ln in lines)
            {
                var line = ln.Trim();
                if (line.Length == 0) continue;

                var m1 = reLabel.Match(line);
                if (m1.Success)
                {
                    var v = m1.Groups["v"].Value.Trim();
                    return (v, line);
                }

                var m2 = rePhrase.Match(line);
                if (m2.Success)
                {
                    var v = m2.Groups["v"].Value.Trim();
                    return (v, line);
                }
            }
            return (null, null);
        }
    }


    private static (string? name, string? email, string? phone) ParseIslaFields(IEnumerable<string> lines)
    {
        foreach (var line in lines.Reverse())
        {
            var m = Regex.Match(line, @"\{.*""type""\s*:\s*""isla\.fields"".*\}", RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            try
            {
                using var doc = JsonDocument.Parse(m.Value);
                var root = doc.RootElement;

                string? get(string key) =>
                    root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
                        ? el.GetString()
                        : null;

                var name = get("name");
                var email = get("email");
                var phone = get("phone");
                if (!string.IsNullOrWhiteSpace(name) ||
                    !string.IsNullOrWhiteSpace(email) ||
                    !string.IsNullOrWhiteSpace(phone))
                {
                    return (name, email, phone);
                }
            }
            catch { }
        }
        return (null, null, null);
    }

    /// <summary>True if the name is the assistant's (Isla, Microhire, or both). Allows e.g. "Isla Smith".</summary>
    private static bool LooksLikeAssistantName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().ToLowerInvariant();
        if (t.StartsWith("my name is ")) return true;   // safety
        return t == "isla" || t == "microhire" || (t.Contains("isla") && t.Contains("microhire"));
    }

    private static (string?, string?) FindEmail(IEnumerable<string> src)
    {
        var re = new Regex(@"\b([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})\b", RegexOptions.IgnoreCase);
        foreach (var line in src.Reverse())
        {
            var m = re.Match(line);
            if (m.Success) return (m.Groups[1].Value.Trim(), m.Groups[1].Value.Trim());
        }
        foreach (var line in src)
        {
            var m = Regex.Match(line, @"\b(email|e-mail)\s*[:\-]?\s*([^\s,;]+)", RegexOptions.IgnoreCase);
            if (m.Success) return (m.Groups[2].Value.Trim(), m.Groups[2].Value.Trim());
        }
        return (null, null);
    }

    private static (string?, string?) FindName(IEnumerable<string> src)
    {
        foreach (var line in src)
        {
            var m = Regex.Match(line, @"^\s*(contact\s*name|name)\s*(?:[:\-]|is)\s*(.+?)\s*$",
                                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var val = CleanTail(m.Groups[2].Value);
                if (LooksLikeHumanName(val)) return (ToTitle(val), m.Value.Trim());
            }
        }
        foreach (var line in src)
        {
            var m = Regex.Match(line,
                @"\b(my name is|i am|i'm|this is)\s+([A-Za-z][a-z]+(?:\s+[A-Za-z][a-z]+){0,2})\b",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var val = CleanTail(m.Groups[2].Value);
                if (LooksLikeHumanName(val)) return (ToTitle(val), m.Value.Trim());
            }
        }
        return (null, null);

        static string CleanTail(string s) => s.Trim().TrimEnd('.', ',', ';', '!', '?', ':');
        static bool LooksLikeHumanName(string s)
        {
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 3) return false;
            if (!parts.All(p => Regex.IsMatch(p, @"^[A-Za-z][a-z]+$"))) return false;
            
            // Reject names starting with articles
            var first = parts[0].ToLowerInvariant();
            if (first is "the" or "a" or "an") return false;
            
            // Reject common job titles/roles that aren't names
            var lower = s.ToLowerInvariant();
            var jobTitles = new[] { "director", "manager", "engineer", "developer", "officer", "assistant", 
                "coordinator", "supervisor", "executive", "president", "owner", "founder", "ceo", "cto", "cfo" };
            if (jobTitles.Any(t => lower.Contains(t))) return false;
            
            return true;
        }
        static string ToTitle(string s)
            => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

    private static (string?, string?) FindPhone(IEnumerable<string> src)
    {
        var re = new Regex(@"\b(\+?\s?61[\s\-\(\)]?\d(?:[\s\-\(\)]?\d){8}|\(?0\d\)?(?:[\s\-]?\d){8,9}|\+?\d[\d\-\s\(\)]{7,}\d)\b");
        foreach (var line in src.Reverse())
        {
            var m = re.Match(line);
            if (m.Success)
            {
                var raw = m.Groups[1].Value.Trim();
                return (raw, raw);
            }
        }
        foreach (var line in src)
        {
            var m = Regex.Match(line, @"\b(phone|mobile|contact|contact number)\s*[:\-]?\s*([+\d\(\)\s\-]{6,})",
                                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var raw = m.Groups[2].Value.Trim();
                return (raw, m.Value.Trim());
            }
        }
        return (null, null);
    }

    private static string? NormalizePhoneAu(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var hasPlus = raw.Contains('+');
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits)) return null;

        if (hasPlus && digits.StartsWith("61")) return "+" + digits;
        if (digits.StartsWith("61")) return "+" + digits;

        if (digits.StartsWith("0"))
        {
            if (digits.StartsWith("04"))
                return "+61" + digits.Substring(1);
            return "+61" + digits.Substring(1);
        }

        if (digits.Length == 9 || digits.Length == 10)
            return "+61" + digits;

        return hasPlus ? "+" + digits : digits;
    }

    private static (string? Name, string? Matched) FindNameFromShortReplyAfterPrompt(
        List<(string role, string line)> convo)
    {
        var prompt = new Regex(
            @"\b(what('?s| is)\s+your\s+name|may\s+i\s+have\s+your\s+name|your\s+name\??|"
          + @"pardon\s+my\s+manners.*name|can\s+i\s+grab\s+your\s+name)\b",
            RegexOptions.IgnoreCase);

        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "yes","yeah","yep","no","nope","hi","hello","thanks","thank you","ok","okay","sure","fine","good" };

        bool LooksLikeName(string s)
        {
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 3) return false;
            if (stop.Contains(s)) return false;
            return parts.All(p => Regex.IsMatch(p, @"^[A-Za-z][a-z]+$"));
        }

        string ToTitle(string s) =>
            System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

        for (int i = 0; i < convo.Count; i++)
        {
            var (role, line) = convo[i];
            if (!role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;
            if (!prompt.IsMatch(line)) continue;

            for (int j = i + 1; j < Math.Min(i + 4, convo.Count); j++)
            {
                var (r2, l2) = convo[j];
                if (!r2.Equals("user", StringComparison.OrdinalIgnoreCase)) continue;

                var cand = l2.Trim().Trim(',', '.', ';', '!', '?', ':', '"', '\'');
                if (LooksLikeName(cand))
                    return (ToTitle(cand), l2);
            }
        }

        return (null, null);
    }

    private static string? GuessNameFromEmail(string email)
    {
        try
        {
            var local = email.Split('@')[0];
            local = Regex.Replace(local, @"\d+", "");
            var tokens = Regex.Split(local, @"[._+\-]+")
                              .Where(t => t.Length >= 2)
                              .Take(3)
                              .ToArray();
            if (tokens.Length == 0) return null;
            var guess = string.Join(" ", tokens);
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(guess.ToLowerInvariant());
        }
        catch { return null; }
    }

    public static string? ExtractLastChoice(IEnumerable<DisplayMessage> messages, string prefix)
    {
        var pfx = prefix.ToLowerInvariant();
        foreach (var m in messages.Reverse())
        {
            foreach (var part in m.Parts ?? Enumerable.Empty<string>())
            {
                var s = part?.Trim() ?? string.Empty;
                if (s.Length == 0) continue;
                var lower = s.ToLowerInvariant();
                if (lower.StartsWith(pfx))
                    return s.Substring(prefix.Length).Trim();
            }
        }
        return null;
    }

    // “Number of Guests: 20” or “20 guests”
    public static int? ExtractAttendees(IEnumerable<DisplayMessage> messages)
    {
        var re = new Regex(@"\b(\d{1,4})\s*(guests|attendees|people)\b", RegexOptions.IgnoreCase);
        foreach (var m in messages.Reverse())
        {
            foreach (var part in (m.Parts ?? Enumerable.Empty<string>()))
            {
                var s = part ?? "";
                // explicit “Number of Guests: 20”
                var colon = Regex.Match(s, @"Number of Guests:\s*(\d{1,4})", RegexOptions.IgnoreCase);
                if (colon.Success && int.TryParse(colon.Groups[1].Value, out var n1)) return n1;

                // free text “… 20 guests”
                var free = re.Match(s);
                if (free.Success && int.TryParse(free.Groups[1].Value, out var n2)) return n2;
            }
        }
        return null;
    }

    // “Event: Meeting” (from your summary) or last user mention of type (“meeting”, “conference”, etc.)
    public static string? ExtractEventType(IEnumerable<DisplayMessage> messages)
    {
        foreach (var m in messages.Reverse())
        {
            foreach (var part in (m.Parts ?? Enumerable.Empty<string>()))
            {
                var s = part ?? "";
                var m1 = Regex.Match(s, @"\bEvent:\s*([A-Za-z][\w\s\-]{1,60})", RegexOptions.IgnoreCase);
                if (m1.Success) return m1.Groups[1].Value.Trim();
            }
        }
        // very soft fallback: pick last user short noun
        foreach (var m in messages.Reverse().Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
        {
            var s = string.Join(" ", m.Parts ?? Enumerable.Empty<string>());
            var m2 = Regex.Match(s, @"\b(meeting|conference|seminar|gala|dinner|workshop|presentation|wedding|party)\b", RegexOptions.IgnoreCase);
            if (m2.Success) return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(m2.Value.ToLowerInvariant());
        }
        return null;
    }

    public (string? Organisation, string? Address) ExtractOrganisationFromTranscript(IEnumerable<DisplayMessage> messages)
    {
        var list = messages?.ToList() ?? new();
        if (list.Count == 0) return (null, null);

        // scan newest-to-oldest ~12 USER turns
        int seen = 0;
        for (int i = list.Count - 1; i >= 0 && seen < 12; i--)
        {
            var m = list[i];
            if (!string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)) continue;
            seen++;

            var text = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            text = Normalize(text);

            var (org, addr) = TryParseOrgAddress(text);
            if (!string.IsNullOrWhiteSpace(org) || !string.IsNullOrWhiteSpace(addr))
                return (Cap(org), CleanAddr(addr));
        }
        return (null, null);

        // ---------- helpers ----------
        static string Normalize(string s)
        {
            s = s.Replace('’', '\'').Replace('‘', '\'')
                 .Replace('“', '"').Replace('”', '"')
                 .Replace('–', '-').Replace('—', '-');
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        // Normalizes and extracts "<org>, address: <addr>" from a free text line.
        static (string? org, string? addr) TryParseOrgAddress(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return (null, null);

            // --- normalize text & common typos ---
            t = t.Replace('’', '\'').Replace('“', '"').Replace('”', '"');
            t = Regex.Replace(t, @"\s+", " ").Trim();
            // address typos
            t = Regex.Replace(t, @"\b(adress|addess|addres|addrss)\b", "address", RegexOptions.IgnoreCase);
            // optional punctuation normalization
            t = t.Replace(" ,", ",").Replace(" ;", ";");
            
            // Preprocess: Strip leading verbs like "Called", "Named", "Is", etc. from the entire text
            // This handles cases like "Called THE gully, located in winston glades"
            var verbPrefixPattern = new Regex(@"^(?:called|named|is|are|company\s+is|business\s+is|organisation\s+is|organization\s+is|org\s+is|the\s+company\s+is|the\s+business\s+is)\s+(.+)$", RegexOptions.IgnoreCase);
            var verbMatch = verbPrefixPattern.Match(t);
            if (verbMatch.Success)
                t = verbMatch.Groups[1].Value.Trim();

            // --- helpers ---
            static string Clean(string s) => s.Trim().Trim(',', ';', ':');
            static string CleanupOrgLeft(string s)
            {
                s = Clean(s);
                // Drop leading labels if user wrote "Organization / Company / Name :"
                s = Regex.Replace(s, @"^(organisation|organization|company|business|name)\s*(name)?\s*$",
                                  "", RegexOptions.IgnoreCase).Trim();
                return s;
            }

            // 0) Very direct shape: "<org> and address is/at/: <addr>"
            var re0 = new Regex(@"^(?<org>.+?)\s+(?:&|and)\s+address\s*(?:is|at|=|:)?\s*(?<addr>.+)$",
                RegexOptions.IgnoreCase);
            var m0 = re0.Match(t);
            if (m0.Success) return (Clean(m0.Groups["org"].Value), Clean(m0.Groups["addr"].Value));

            // 1) "Organization/Company/Business/Name is/:\s<org> , address is/:\s<addr>"
            var re1 = new Regex(
                @"(?:organisation|organization|company|business|name)\s*(?:name)?\s*(?:is|=|:)?\s*(?<org>[^,;]+?)\s*[,;]\s*address\s*(?:is|=|:|at)?\s*(?<addr>.+)$",
                RegexOptions.IgnoreCase);
            var m1 = re1.Match(t);
            if (m1.Success) return (Clean(m1.Groups["org"].Value), Clean(m1.Groups["addr"].Value));

            // 2) Unlabeled: "<org> , address <addr>"
            var re2 = new Regex(
                @"^(?<org>[^,;]+?)\s*[,;]\s*address\s*(?:is|=|:|at)?\s*(?<addr>.+)$",
                RegexOptions.IgnoreCase);
            var m2 = re2.Match(t);
            if (m2.Success) return (Clean(m2.Groups["org"].Value), Clean(m2.Groups["addr"].Value));

            // 3) Labeled in reverse: "address: <addr> , organization: <org>"
            var re3 = new Regex(
                @"address\s*(?:is|=|:|at)?\s*(?<addr>[^,;]+?)\s*[,;]\s*(?:organisation|organization|company|business|name)\s*(?:name)?\s*(?:is|=|:)?\s*(?<org>.+)$",
                RegexOptions.IgnoreCase);
            var m3 = re3.Match(t);
            if (m3.Success) return (Clean(m3.Groups["org"].Value), Clean(m3.Groups["addr"].Value));

            // 4) Loose fallback: split at first "address"
            var idx = t.IndexOf("address", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                var left = t[..idx];
                var right = t[(idx + "address".Length)..];
                right = Regex.Replace(right, @"^(?:\s*(is|=|:|at))?\s*", "", RegexOptions.IgnoreCase);
                left = CleanupOrgLeft(left);
                if (!string.IsNullOrWhiteSpace(left) || !string.IsNullOrWhiteSpace(right))
                    return (Clean(left), Clean(right));
            }

            // 5) Handle "X is the company. Y is the" pattern
            var re5 = new Regex(@"^(?<org>.+?)\s+is\s+the\s+company\.?\s*(?<addr>.+?)\s+is\s+the",
                RegexOptions.IgnoreCase);
            var m5 = re5.Match(t);
            if (m5.Success) return (Clean(m5.Groups["org"].Value), Clean(m5.Groups["addr"].Value));

            // 6) Handle "company is X. address is Y" pattern
            var re6 = new Regex(@"company\s+is\s+(?<org>.+?)\.?\s+address\s+is\s+(?<addr>.+?)$",
                RegexOptions.IgnoreCase);
            var m6 = re6.Match(t);
            if (m6.Success) return (Clean(m6.Groups["org"].Value), Clean(m6.Groups["addr"].Value));

            // 7) Very loose fallback: split on common separators
            var separators = new[] { ". ", ", ", " and ", " & " };
            foreach (var sep in separators)
            {
                var sepIndex = t.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (sepIndex > 0)
                {
                    var part1 = t[..sepIndex].Trim();
                    var part2 = t[(sepIndex + sep.Length)..].Trim();

                    // Check if part1 looks like a company name and part2 like an address
                    if (part1.Length > 3 && part1.Length < 50 && part2.Length > 5 && part2.Length < 100)
                    {
                        // Simple heuristic: if part2 contains numbers or common address words
                        var addrWords = new[] { "street", "st", "road", "rd", "avenue", "ave", "city", "nyc", "melbourne" };
                        if (part2.Any(char.IsDigit) || addrWords.Any(w => part2.Contains(w, StringComparison.OrdinalIgnoreCase)))
                        {
                            return (Clean(part1), Clean(part2));
                        }
                    }
                }
            }

            return (null, null);
        }

        static string CleanupOrgLeft(string left)
        {
            // Strip common leading/trailing helper words from the organisation side.
            // First, remove leading verbs like "called", "named", "is", "are", etc.
            left = Regex.Replace(left, @"^\s*(?:called|named|is|are|company\s+is|business\s+is|organisation\s+is|organization\s+is|org\s+is|the\s+company\s+is|the\s+business\s+is)\s+", "", RegexOptions.IgnoreCase);
            left = Regex.Replace(left, @"^\s*(my|the)\s+", "", RegexOptions.IgnoreCase); // leading
            left = Regex.Replace(left,
                @"\s+(?:and|,)?\s*(?:company|organisation|organization|name)?\s*$",
                "", RegexOptions.IgnoreCase); // trailing noise like "... and"
            left = Regex.Replace(left, @"\s+\bis\b\s*$", "", RegexOptions.IgnoreCase); // trailing "is"
            return left.Trim();
        }

        static string? Cap(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var ti = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
            var result = ti.ToTitleCase(s.ToLowerInvariant());
            // Truncate to database column length (varchar(50))
            return result.Length > 50 ? result[..50] : result;
        }

        static string? CleanAddr(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            // trim trailing sentence punctuation that users often add
            var result = s.Trim().TrimEnd('.', '!', ';');
            // Truncate to database column length (varchar(200))
            return result.Length > 200 ? result[..200] : result;
        }
    }
}
