using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
    /// <summary>English number words for attendee phrases (e.g. "seven people").</summary>
    private static readonly Dictionary<string, int> EnglishAttendeeNumberWords = BuildEnglishAttendeeNumberWords();

    private static Dictionary<string, int> BuildEnglishAttendeeNumberWords()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string[] ones = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
        for (var i = 0; i < ones.Length; i++)
            d[ones[i]] = i;
        string[] tens = ["twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        for (var t = 0; t < tens.Length; t++)
            d[tens[t]] = (t + 2) * 10;
        return d;
    }

    private static bool TryParseEnglishAttendeeWord(string word, out int n)
    {
        n = 0;
        return !string.IsNullOrWhiteSpace(word) && EnglishAttendeeNumberWords.TryGetValue(word.Trim(), out n) && n > 0;
    }

    /// <summary>
    /// Counts from clear user phrasing only (digits or number-words + people/attendees, update patterns).
    /// Does not use assistant-follow-up + bare-number inference (avoids false positives vs CRM prefill).
    /// </summary>
    internal static int ExtractExplicitAttendeesFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages?.ToList() ?? new List<DisplayMessage>();
        var userMessages = ordered
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (userMessages.Count == 0) return 0;

        var latestAttendees = 0;
        var userParts = userMessages.SelectMany(m => m.Parts ?? Enumerable.Empty<string>());
        foreach (var part in userParts)
        {
            if (string.IsNullOrWhiteSpace(part) || IsScheduleOrTimeSelectionMessage(part)) continue;

            var directMatch = Regex.Match(part, @"\b(\d{1,4})\s*(?:people|attendees|pax|participants|guests)\b", RegexOptions.IgnoreCase);
            if (directMatch.Success && int.TryParse(directMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var direct) && direct > 0)
            {
                latestAttendees = direct;
                continue;
            }

            var directWord = Regex.Match(part,
                @"\b(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)\s+(?:people|attendees|pax|participants|guests)\b",
                RegexOptions.IgnoreCase);
            if (directWord.Success && TryParseEnglishAttendeeWord(directWord.Groups[1].Value, out var dw) && dw > 0)
            {
                latestAttendees = dw;
                continue;
            }

            var expectingMatch = Regex.Match(part, @"\b(?:expecting|about|around|approximately|roughly)\s+(\d{1,4})(?:\s*(?:people|attendees|pax|participants|guests))?\b", RegexOptions.IgnoreCase);
            if (expectingMatch.Success && int.TryParse(expectingMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var expecting) && expecting > 0)
            {
                latestAttendees = expecting;
                continue;
            }

            var expectingWord = Regex.Match(part,
                @"\b(?:expecting|about|around|approximately|roughly)\s+(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)(?:\s+(?:people|attendees|pax|participants|guests))?\b",
                RegexOptions.IgnoreCase);
            if (expectingWord.Success && TryParseEnglishAttendeeWord(expectingWord.Groups[1].Value, out var ew) && ew > 0)
            {
                latestAttendees = ew;
                continue;
            }

            var updatedCountMatch = Regex.Match(part, @"\b(?:change|update|set|make)\s+(?:the\s+)?(?:attendee(?:s)?|count)\s+(?:to|as)\s+(\d{1,4})\b", RegexOptions.IgnoreCase);
            if (updatedCountMatch.Success && int.TryParse(updatedCountMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var changed) && changed > 0)
            {
                latestAttendees = changed;
                continue;
            }

            var updatedWord = Regex.Match(part,
                @"\b(?:change|update|set|make)\s+(?:the\s+)?(?:attendee(?:s)?|count)\s+(?:to|as)\s+(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)\b",
                RegexOptions.IgnoreCase);
            if (updatedWord.Success && TryParseEnglishAttendeeWord(updatedWord.Groups[1].Value, out var uw) && uw > 0)
                latestAttendees = uw;
        }

        return latestAttendees;
    }

    /// <summary>
    /// Merges explicit attendee phrases, full transcript inference (incl. contextual bare numbers), and CRM-prefill session.
    /// When a CRM seed exists, contextual inference that disagrees with session is ignored so prefill is not overwritten by ambiguous replies.
    /// </summary>
    internal static int ResolveUserAttendeesFromTranscriptForEquipmentRecommendation(
        int explicitAttendeesFromTranscript,
        int userStatedAttendeesFull,
        int attendeesFromSessionParsed,
        int leadSeededParsed)
    {
        if (explicitAttendeesFromTranscript > 0)
            return explicitAttendeesFromTranscript;
        if (leadSeededParsed > 0
            && userStatedAttendeesFull > 0
            && attendeesFromSessionParsed > 0
            && userStatedAttendeesFull != attendeesFromSessionParsed)
            return 0;
        if (userStatedAttendeesFull > 0)
            return userStatedAttendeesFull;
        return 0;
    }

    internal static int ExtractAttendeesFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages?.ToList() ?? new List<DisplayMessage>();
        var userMessages = ordered
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (userMessages.Count == 0) return 0;

        var latestAttendees = ExtractExplicitAttendeesFromUserMessages(ordered);
        if (latestAttendees > 0)
            return latestAttendees;

        for (int i = 0; i < ordered.Count; i++)
        {
            var msg = ordered[i];
            if (!msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var assistantText = string.Join(" ", msg.Parts ?? Enumerable.Empty<string>());
            if (!Regex.IsMatch(assistantText, @"\b(?:attendees|how many|number of (?:people|guests|attendees|participants))\b", RegexOptions.IgnoreCase))
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null) continue;

            var nextText = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsScheduleOrTimeSelectionMessage(nextText))
                continue;

            if (TryParseAttendeeLikeReply(nextText, out var contextual))
            {
                latestAttendees = contextual;
                break;
            }
        }

        return latestAttendees;
    }

    internal static string? ExtractSetupStyleFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages?.ToList() ?? new List<DisplayMessage>();
        var userMessages = ordered
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (userMessages.Count == 0) return null;

        var explicitPattern = @"\b(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\s+(?:setup|style|layout)\b";
        var reversePattern = @"\b(?:setup|style|layout)\s+(?:is|will\s+be|should\s+be)?\s*(?:a\s+)?(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\b";
        string? latestStyle = null;

        foreach (var message in userMessages)
        {
            var part = GetMessageText(message);
            if (string.IsNullOrWhiteSpace(part))
                continue;

            var explicitMatch = Regex.Match(part, explicitPattern, RegexOptions.IgnoreCase);
            if (explicitMatch.Success)
            {
                latestStyle = NormalizeSetupStyleToken(explicitMatch.Groups[1].Value);
                continue;
            }

            var reverseMatch = Regex.Match(part, reversePattern, RegexOptions.IgnoreCase);
            if (reverseMatch.Success)
            {
                latestStyle = NormalizeSetupStyleToken(reverseMatch.Groups[1].Value);
                continue;
            }

            var standaloneMatch = Regex.Match(
                part,
                @"(?:^|\b)(?:yes|yeah|yep|correct|please|go with|let'?s go with|can we go in|i want|we want)?\s*(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\b",
                RegexOptions.IgnoreCase);
            if (standaloneMatch.Success)
                latestStyle = NormalizeSetupStyleToken(standaloneMatch.Groups[1].Value);
        }

        // Fallback: short direct reply after assistant asks for setup style
        for (int i = 0; i < ordered.Count; i++)
        {
            var msg = ordered[i];
            if (!msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var assistantText = string.Join(" ", msg.Parts ?? Enumerable.Empty<string>());
            if (!Regex.IsMatch(assistantText, @"\b(setup|style|layout|boardroom style|theatre style|banquet style|classroom style)\b", RegexOptions.IgnoreCase))
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null) continue;

            var nextText = GetMessageText(nextUser).Trim();
            var tokenMatch = Regex.Match(
                nextText,
                @"^(?:yes|yeah|yep|correct|please|go with|let'?s go with|can we go in|i want|we want)?\s*(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)(?:\s+(?:setup|style|layout))?$",
                RegexOptions.IgnoreCase);
            if (tokenMatch.Success)
                latestStyle = NormalizeSetupStyleToken(tokenMatch.Groups[1].Value);
        }

        return latestStyle;
    }

    internal static string? ExtractRoomFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var userMessages = (messages ?? Enumerable.Empty<DisplayMessage>())
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        string? latestRoom = null;
        foreach (var message in userMessages)
        {
            var part = GetMessageText(message);
            if (string.IsNullOrWhiteSpace(part)) continue;
            var text = part.ToLowerInvariant();

            if (text.Contains("westin ballroom 1") || Regex.IsMatch(text, @"\bballroom\s*1\b")) latestRoom = "Westin Ballroom 1";
            else if (text.Contains("westin ballroom 2") || Regex.IsMatch(text, @"\bballroom\s*2\b")) latestRoom = "Westin Ballroom 2";
            else if (text.Contains("full westin ballroom") || text.Contains("westin ballroom full") || text.Contains("full ballroom")) latestRoom = "Westin Ballroom";
            else if (text.Contains("westin ballroom")) latestRoom = "Westin Ballroom";
            else if (text.Contains("thrive boardroom") || text.Contains("thrive room")) latestRoom = "Thrive Boardroom";
            else if (Regex.IsMatch(text, @"\bthrive\b")) latestRoom = "Thrive Boardroom";
            else if (Regex.IsMatch(text, @"\belevate\b")) latestRoom = "Elevate";
            // Keep quoting focus on Thrive / Elevate / Westin Ballroom variants.
            // Do not auto-resolve non-primary rooms for quote flow.
            else if (text.Contains("meeting room") && text.Contains("four points")) latestRoom = "Meeting Room";
        }

        return latestRoom;
    }

    private static string GetMessageText(DisplayMessage message)
    {
        var partsText = string.Join(" ", (message.Parts ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p))).Trim();
        if (!string.IsNullOrWhiteSpace(partsText))
            return partsText;

        return (message.FullText ?? string.Empty).Trim();
    }

    private static List<WestinRoom> FilterQuotableWestinRooms(IEnumerable<WestinRoom> rooms)
        => (rooms ?? Enumerable.Empty<WestinRoom>())
            .Where(r => IsQuotableWestinRoomName(r.Name))
            .ToList();

    private static bool IsQuotableWestinRoomName(string? roomName)
    {
        var normalized = (roomName ?? "").Trim().ToLowerInvariant();
        return normalized == "westin ballroom"
            || normalized == "westin ballroom 1"
            || normalized == "westin ballroom 2"
            || normalized == "elevate"
            || normalized == "thrive boardroom";
    }

    private static bool IsScheduleOrTimeSelectionMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.Trim().ToLowerInvariant();

        if (lower.StartsWith("choose schedule:", StringComparison.OrdinalIgnoreCase)
            || lower.StartsWith("choose time:", StringComparison.OrdinalIgnoreCase)
            || lower.StartsWith("i've selected this schedule:", StringComparison.OrdinalIgnoreCase)
            || lower.StartsWith("schedule selected:", StringComparison.OrdinalIgnoreCase))
            return true;

        // Only treat as a schedule message when stage names appear alongside time tokens -
        // a plain "setup, rehearsal and pack up" reply is a valid technician-stage answer.
        return lower.Contains("setup") && lower.Contains("rehearsal") && lower.Contains("pack up")
               && Regex.IsMatch(lower, @"\d{1,2}:\d{2}");
    }

    private static bool TryParseAttendeeLikeReply(string text, out int attendees)
    {
        attendees = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        var replyMatch = Regex.Match(
            normalized,
            @"^(?:about|around|approximately|roughly)?\s*(\d{1,4})\s*(?:people|attendees|pax|participants|guests)?\.?$",
            RegexOptions.IgnoreCase);
        return replyMatch.Success &&
               int.TryParse(replyMatch.Groups[1].Value, out attendees) &&
               attendees > 0;
    }

    internal static bool HasExplicitRehearsalOperatorConfirmation(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0) return false;

        var userMessages = ordered
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var userMsg in userMessages)
        {
            var text = string.Join(" ", userMsg.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (Regex.IsMatch(text,
                @"\b(operator\s+for\s+(the\s+)?rehearsal|rehearsal\s+operator|operator\s+during\s+rehearsal)\b(?!\s*(no|not|none|nah)\b)",
                RegexOptions.IgnoreCase))
                return true;
        }

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            if (!string.Equals(current.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", current.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksRehearsalOperatorQuestion =
                Regex.IsMatch(assistantText, @"\boperator\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\brehearsal\b", RegexOptions.IgnoreCase) &&
                assistantText.Contains("?", StringComparison.Ordinal);
            if (!asksRehearsalOperatorQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsLikelyAffirmativeReply(reply))
                return true;
        }

        return false;
    }

    internal static bool HasExplicitRehearsalOperatorDeclined(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0) return false;

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            if (!string.Equals(current.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", current.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksRehearsalOperatorQuestion =
                Regex.IsMatch(assistantText, @"\boperator\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\brehearsal\b", RegexOptions.IgnoreCase) &&
                assistantText.Contains("?", StringComparison.Ordinal);
            if (!asksRehearsalOperatorQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (Regex.IsMatch(reply, @"\b(no|not needed|none|nah|no thanks|don't need|do not need)\b", RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    internal static bool HasExplicitMicrophoneOperatorConfirmation(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0) return false;

        var userMessages = ordered
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var userMsg in userMessages)
        {
            var text = string.Join(" ", userMsg.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (Regex.IsMatch(text,
                @"\b(operator\s+(to\s+)?(manage|for)\s+(the\s+)?microphone|microphone\s+operator)\b(?!\s*(no|not|none|nah)\b)",
                RegexOptions.IgnoreCase))
                return true;
        }

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            if (!string.Equals(current.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", current.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksMicOperatorQuestion =
                Regex.IsMatch(assistantText, @"\boperator\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bmicrophone", RegexOptions.IgnoreCase) &&
                assistantText.Contains("?", StringComparison.Ordinal);
            if (!asksMicOperatorQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsLikelyAffirmativeReply(reply))
                return true;
        }

        return false;
    }

    internal static bool HasExplicitMicrophoneOperatorDeclined(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0) return false;

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            if (!string.Equals(current.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", current.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksMicOperatorQuestion =
                Regex.IsMatch(assistantText, @"\boperator\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bmicrophone", RegexOptions.IgnoreCase) &&
                assistantText.Contains("?", StringComparison.Ordinal);
            if (!asksMicOperatorQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (Regex.IsMatch(reply, @"\b(no|not needed|none|nah|no thanks|don't need|do not need)\b", RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    internal static bool HasExplicitVideoConferenceConfirmation(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0) return false;

        var userMessages = ordered
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var userMsg in userMessages)
        {
            var text = string.Join(" ", userMsg.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (Regex.IsMatch(text,
                @"\b(camera|webcam|ptz|microphone|mic|speaker|speakers|video\s+conference\s+unit|conference\s+camera|zoom|teams|video\s+call|video\s+conference|hybrid|remote attendees)\b",
                RegexOptions.IgnoreCase))
                return true;
        }

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            if (!string.Equals(current.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", current.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksVideoConferenceQuestion =
                Regex.IsMatch(assistantText, @"\b(video\s+conference|video\s+call|teams|zoom|remote attendees|hybrid)\b", RegexOptions.IgnoreCase) &&
                assistantText.Contains("?", StringComparison.Ordinal);
            if (!asksVideoConferenceQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsLikelyAffirmativeReply(reply))
                return true;
        }

        return false;
    }

    internal static bool IsLikelyAffirmativeReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
        if (Regex.IsMatch(normalized,
            @"\b(yes|yep|yeah|sure|please do|go ahead|sounds good|that works|correct|affirmative|definitely|absolutely|of course|for sure|please|will need|that would be great|that('s| is) (good|great|fine|perfect))\b"))
            return true;
        return normalized is "ok" or "okay";
    }

    internal sealed record TechnicianCoveragePreference(
        bool HasPreference,
        bool NoTechnicianSupport,
        bool Setup,
        bool Rehearsal,
        bool Operate,
        bool Packdown);

    /// <summary>
    /// Tries to deserialize a previously stored technician coverage preference from the session.
    /// Returns null if nothing is stored or the value cannot be parsed.
    /// </summary>
    internal static TechnicianCoveragePreference? TryLoadTechnicianCoverageFromSession(ISession session)
    {
        var stored = session.GetString("Draft:TechnicianCoverage");
        if (string.IsNullOrWhiteSpace(stored)) return null;

        try
        {
            using var doc = JsonDocument.Parse(stored);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var noTech = root.TryGetProperty("NoTechnicianSupport", out var noTechProp) && noTechProp.ValueKind == JsonValueKind.True;
            return new TechnicianCoveragePreference(
                HasPreference: true,
                NoTechnicianSupport: noTech,
                Setup:     root.TryGetProperty("Setup",     out var sp) && sp.ValueKind == JsonValueKind.True,
                // Rehearsal is intentionally always false here – controlled by the rehearsal operator confirmation flow.
                Rehearsal: false,
                Operate:   root.TryGetProperty("Operate",   out var op) && op.ValueKind == JsonValueKind.True,
                Packdown:  root.TryGetProperty("Packdown",  out var pp) && pp.ValueKind == JsonValueKind.True
            );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// When the wizard has already captured technician intent in session (Av extras / base AV)
    /// but the current user line is not in the transcript yet (e.g. FollowUpAv path runs recommend
    /// before AppendUserMessageAsync), infer coverage so <see cref="ExtractTechnicianCoveragePreference"/>
    /// is not blocked on empty history.
    /// </summary>
    internal static TechnicianCoveragePreference? TryInferTechnicianCoverageFromDraftSession(ISession session)
    {
        if (session == null) return null;

        var whole = session.GetString("Draft:TechWholeEvent") ?? "";
        if (string.Equals(whole, "yes", StringComparison.OrdinalIgnoreCase))
            // Rehearsal excluded – controlled by the rehearsal operator confirmation flow.
            return new TechnicianCoveragePreference(true, false, true, false, true, true);

        var ts = session.GetString("Draft:TechStartTime") ?? "";
        var te = session.GetString("Draft:TechEndTime") ?? "";
        if (!string.IsNullOrWhiteSpace(ts) && !string.IsNullOrWhiteSpace(te))
            // Rehearsal excluded – controlled by the rehearsal operator confirmation flow.
            return new TechnicianCoveragePreference(true, false, true, false, true, true);

        if (string.Equals(whole, "no", StringComparison.OrdinalIgnoreCase))
            return null;

        return null;
    }

    internal static TechnicianCoveragePreference ExtractTechnicianCoveragePreference(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0)
            return new TechnicianCoveragePreference(false, false, false, false, false, false);

        bool setup = false;
        bool rehearsal = false;
        bool operate = false;
        bool packdown = false;
        bool hasPreference = false;

        var userMessages = ordered
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var userMessage in userMessages)
        {
            var text = string.Join(" ", userMessage.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(text) || IsScheduleOrTimeSelectionMessage(text))
                continue;

            var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ");
            var mentionsTechnician = Regex.IsMatch(normalized, @"\b(technician|technicians|tech|crew|operator|operate|operation|staffing)\b");

            if (Regex.IsMatch(normalized, @"\b(no tech|no technician|no technicians|without technician|without tech|self[-\s]?operated|self[-\s]?service)\b"))
            {
                return new TechnicianCoveragePreference(true, true, false, false, false, false);
            }

            // Accept "all stages" / "full coverage" without requiring a technician keyword - the user
            // is responding to the stage-selection question and this phrasing is unambiguous enough.
            var allStages = Regex.IsMatch(normalized,
                @"\b(all (stages|day|day long|day-long|of them)|full coverage|full support|everything|whole duration|whole event|whole time|entire duration|entire event|full duration|for the duration|the whole thing)\b");

            if (allStages)
            {
                // Rehearsal is intentionally excluded here – it is controlled solely by the
                // "Would you like an operator for your rehearsal?" confirmation flow.
                return new TechnicianCoveragePreference(true, false, true, false, true, true);
            }

            var foundSetup = Regex.IsMatch(normalized, @"\b(setup|set up|bump in)\b");
            // Rehearsal is intentionally not extracted here – it is controlled solely by the
            // "Would you like an operator for your rehearsal?" confirmation flow.
            // var foundRehearsal = Regex.IsMatch(normalized, @"\b(rehearsal|test\s*&\s*connect|test and connect|soundcheck)\b");
            var foundRehearsal = false;
            // Explicit "operator yes/no" from event-details form submission takes precedence.
            var explicitOperatorNo  = Regex.IsMatch(normalized, @"\boperator\s+no\b");
            var explicitOperatorYes = Regex.IsMatch(normalized, @"\boperator\s+yes\b");
            var foundOperate = !explicitOperatorNo &&
                (explicitOperatorYes || Regex.IsMatch(normalized, @"\b(operate|operation|operator|during the event|during event|live support|show support|run the event)\b"));
            var foundPackdown = Regex.IsMatch(normalized, @"\b(pack\s*down|packdown|pack\s*up|packup|bump out)\b");

            if (mentionsTechnician || foundSetup || foundRehearsal || foundOperate || foundPackdown)
            {
                setup |= foundSetup;
                rehearsal |= foundRehearsal;
                operate |= foundOperate;
                packdown |= foundPackdown;
                hasPreference = setup || rehearsal || operate || packdown;
            }
        }

        if (hasPreference)
            return new TechnicianCoveragePreference(true, false, setup, rehearsal, operate, packdown);

        // Contextual fallback 1: when assistant asks stage question and user gives short affirmative,
        // interpret as full-coverage preference.
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var assistant = ordered[i];
            if (!string.Equals(assistant.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", assistant.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksTechnicianStages =
                Regex.IsMatch(assistantText, @"\btechnician\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bsetup\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\brehearsal\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bpack\s*down|packdown|pack\s*up|packup\b", RegexOptions.IgnoreCase);

            if (!asksTechnicianStages)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsLikelyAffirmativeReply(reply))
                // Rehearsal excluded – controlled by the rehearsal operator confirmation flow.
                return new TechnicianCoveragePreference(true, false, true, false, true, true);

            if (Regex.IsMatch(reply, @"\b(no|not needed|none)\b", RegexOptions.IgnoreCase))
                return new TechnicianCoveragePreference(true, true, false, false, false, false);
        }

        // Contextual fallback 2: when assistant asks "entire event or only setup/rehearsal",
        // treat a short affirmative as full coverage and a short negative as no technician.
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var assistant = ordered[i];
            if (!string.Equals(assistant.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", assistant.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksBinaryCoverageQuestion =
                Regex.IsMatch(assistantText, @"\btechnical operator\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\b(entire event|whole duration|whole event)\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bsetup\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\brehearsal\b", RegexOptions.IgnoreCase);

            if (!asksBinaryCoverageQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (string.IsNullOrWhiteSpace(reply))
                continue;

            if (IsLikelyAffirmativeReply(reply))
                // Rehearsal excluded – controlled by the rehearsal operator confirmation flow.
                return new TechnicianCoveragePreference(true, false, true, false, true, true);

            if (Regex.IsMatch(reply, @"\b(no|not needed|none)\b", RegexOptions.IgnoreCase))
                return new TechnicianCoveragePreference(true, true, false, false, false, false);

            var normalizedReply = Regex.Replace(reply.ToLowerInvariant(), @"\s+", " ");
            if (Regex.IsMatch(normalizedReply,
                @"\b(entire event|whole event|all day|full event|whole duration|whole time|full duration|entire duration|for the duration)\b"))
                // Rehearsal excluded – controlled by the rehearsal operator confirmation flow.
                return new TechnicianCoveragePreference(true, false, true, false, true, true);

            if (Regex.IsMatch(normalizedReply, @"\b(setup|set up|bump in)\b"))
            {
                var includeSetup = Regex.IsMatch(normalizedReply, @"\b(setup|set up|bump in)\b");
                // Rehearsal excluded – controlled by the rehearsal operator confirmation flow.
                return new TechnicianCoveragePreference(true, false, includeSetup, false, false, false);
            }
        }

        return new TechnicianCoveragePreference(false, false, false, false, false, false);
    }

    private static bool ShouldIncludeLaborTaskForCoverage(string? task, TechnicianCoveragePreference coverage, string? roomName = null)
    {
        if (coverage.NoTechnicianSupport)
            return false;
        if (!coverage.HasPreference)
            return true;

        var taskNorm = (task ?? "").Trim().ToLowerInvariant();
        if (taskNorm.Contains("setup"))
            return coverage.Setup;
        if (taskNorm.Contains("rehearsal"))
            return coverage.Rehearsal;
        if (taskNorm.Contains("test"))
            return true; // "Test & Connect" is always included by default as a baseline task
        if (taskNorm.Contains("operate") || taskNorm.Contains("operator") || taskNorm.Contains("support"))
            return coverage.Operate;
        if (taskNorm.Contains("pack"))
            return coverage.Packdown || IsDefaultPackdownRoom(roomName);

        return coverage.Operate;
    }

    private static List<RecommendedLaborItem> ApplyTechnicianCoveragePreference(
        IEnumerable<RecommendedLaborItem> laborItems,
        TechnicianCoveragePreference coverage,
        string? roomName = null)
    {
        var source = (laborItems ?? Enumerable.Empty<RecommendedLaborItem>()).ToList();
        if (source.Count == 0)
            return source;
        if (coverage.NoTechnicianSupport)
            return new List<RecommendedLaborItem>();
        if (!coverage.HasPreference)
            return source;

        var filtered = source
            .Where(l => ShouldIncludeLaborTaskForCoverage(l.Task, coverage, roomName))
            .ToList();

        var operateTemplate = source.FirstOrDefault(l => IsOperateLaborTask(l.Task));

        // "Would you like an operator throughout your event?" = Yes → always use AVTECH for Operate.
        if (coverage.Operate && !filtered.Any(l => IsOperateLaborTask(l.Task)))
        {
            filtered.Add(new RecommendedLaborItem
            {
                ProductCode = "AVTECH",
                Description = "AV Technician",
                Task = "Operate",
                Quantity = 1,
                Hours = 0,
                Minutes = 0,
                RecommendationReason = "Customer requested operator throughout event."
            });
        }

        if (operateTemplate != null)
        {
            if (coverage.Setup && !filtered.Any(l => IsSetupLaborTask(l.Task)))
            {
                filtered.Add(CloneLaborForStage(
                    operateTemplate,
                    task: "Setup",
                    hours: 1,
                    minutes: 0,
                    reasonSuffix: "Added setup coverage because the customer requested technician support for this stage."));
            }

            if (coverage.Rehearsal && !filtered.Any(l => IsRehearsalLaborTask(l.Task)))
            {
                filtered.Add(CloneLaborForStage(
                    operateTemplate,
                    task: "Rehearsal",
                    hours: 0,
                    minutes: 30,
                    reasonSuffix: "Added rehearsal coverage because the customer requested technician support for this stage."));
            }

            var packdownRequired = coverage.Packdown || IsDefaultPackdownRoom(roomName);
            if (packdownRequired && !filtered.Any(l => IsPackdownLaborTask(l.Task)))
            {
                filtered.Add(CloneLaborForStage(
                    operateTemplate,
                    task: "Packdown",
                    hours: 1,
                    minutes: 0,
                    reasonSuffix: "Added pack down coverage because the customer requested technician support for this stage."));
            }
        }

        return filtered
            .OrderBy(GetLaborTaskSortOrder)
            .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RecommendedLaborItem CloneLaborForStage(
        RecommendedLaborItem template,
        string task,
        double hours,
        int minutes,
        string reasonSuffix)
    {
        var baseReason = string.IsNullOrWhiteSpace(template.RecommendationReason)
            ? "Derived from the technician coverage request."
            : template.RecommendationReason.Trim();

        return new RecommendedLaborItem
        {
            ProductCode = string.IsNullOrWhiteSpace(template.ProductCode) ? "AVTECH" : template.ProductCode,
            Description = template.Description ?? "",
            Task = task,
            Quantity = Math.Max(1, template.Quantity),
            Hours = hours,
            Minutes = minutes,
            RecommendationReason = $"{baseReason} {reasonSuffix}".Trim()
        };
    }

    private static bool IsSetupLaborTask(string? task)
        => (task ?? "").Contains("setup", StringComparison.OrdinalIgnoreCase);

    private static bool IsRehearsalLaborTask(string? task)
    {
        var normalized = task ?? "";
        return normalized.Contains("rehearsal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOperateLaborTask(string? task)
    {
        var normalized = task ?? "";
        return normalized.Contains("operate", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("operator", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("support", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackdownLaborTask(string? task)
        => (task ?? "").Contains("pack", StringComparison.OrdinalIgnoreCase);

    private static bool IsDefaultPackdownRoom(string? roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName)) return false;
        var r = roomName.Trim();
        return r.Contains("Elevate", StringComparison.OrdinalIgnoreCase)
            || r.Contains("Thrive", StringComparison.OrdinalIgnoreCase)
            || r.Contains("Ballroom", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestLaborTask(string? task)
        => (task ?? "").Contains("test", StringComparison.OrdinalIgnoreCase);

    private static int GetLaborTaskSortOrder(RecommendedLaborItem labor)
    {
        if (IsSetupLaborTask(labor.Task))
            return 0;
        if (IsRehearsalLaborTask(labor.Task))
            return 1;
        if (IsOperateLaborTask(labor.Task))
            return 2;
        if (IsPackdownLaborTask(labor.Task))
            return 3;
        return 4;
    }

    private static string NormalizeSetupStyleToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        if (normalized == "theater") return "theatre";
        if (normalized == "schoolroom") return "classroom";
        if (normalized == "u shape") return "u-shape";
        return normalized;
    }
}
