// Controllers/ChatController.cs
using System.Text.RegularExpressions;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models; // for DisplayMessage if needed
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace MicrohireAgentChat.Controllers;

public sealed class ChatController : Controller
{
    private readonly AzureAgentChatService _chat;
    private readonly BookingDbContext _bookingDb;

    // Detect: "Choose time: 09:00–10:30" (supports hyphen or en dash)
    private static readonly Regex ChooseTimeRe =
        new(@"^Choose\s*time:\s*(\d{1,2}:\d{2})\s*[–-]\s*(\d{1,2}:\d{2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Detect: "Choose schedule: key=HH:mm; key=HH:mm; ..."
    private static readonly Regex ChooseScheduleRe =
        new(@"^\s*Choose\s+schedule\s*:\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Exact scripted greeting per your “Final Script for AI”
    private const string GreetingText =
        "Hello, my name is Isla from Microhire. What is your full name?";

    public ChatController(AzureAgentChatService chat, BookingDbContext bookingDb)
    {
        _chat = chat;
        _bookingDb = bookingDb;
    }

    // Small helper to pick a stable user key for persistence.
    private string GetUserKey()
    {
        if (User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity!.Name))
            return User.Identity!.Name!;
        // fallback to a sticky session-based key
        var sid = HttpContext.Session.GetString("PersistUserKey");
        if (string.IsNullOrWhiteSpace(sid))
        {
            sid = HttpContext.Session.Id;
            HttpContext.Session.SetString("PersistUserKey", sid);
        }
        return sid!;
    }

    // Full page
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(threadId);
            return View(messages);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(Enumerable.Empty<DisplayMessage>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return RedirectToAction(nameof(Index));

        try
        {
            var userKey = GetUserKey();
            await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);

            text = text.Trim();

            // 1) Capture single time range "Choose time: HH:mm–HH:mm"
            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }

            // 2) Capture multi-time schedule "Choose schedule: setup=...; rehearsal=...; start=...; end=...; packup=..."
            if (TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack);

                // Rewrite the outgoing text into a friendly normalized message
                var pretty = $"Schedule selected: " +
                             $"Setup {Pretty12(set)}; " +
                             $"Rehearsal {Pretty12(reh)}; " +
                             (showStart.HasValue ? $"Start {Pretty12(showStart)}; " : "") +
                             (showEnd.HasValue ? $"End {Pretty12(showEnd)}; " : "") +
                             $"Pack Up {Pretty12(pack)}.";
                text = pretty.Trim();
            }

            var messages = await _chat.SendAsync(HttpContext.Session, text, ct);
            return View("Index", messages);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var (_, messages) = _chat.GetTranscript(threadId);
            return View("Index", messages);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        try
        {
            HttpContext.Session.Remove("AgentThreadId");

            var newThreadId = _chat.EnsureThreadId(HttpContext.Session);

            var userKey = GetUserKey();
            await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(newThreadId);
            return View("Index", messages);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", Enumerable.Empty<DisplayMessage>());
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPartial(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return BadRequest("empty");

        try
        {
            var userKey = GetUserKey();
            await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);

            text = text.Trim();

            // --- capture time range (single picker) ---
            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }

            // --- capture full schedule (multi-time) ---
            if (TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack);
                var pretty = $"Schedule selected: Setup {Pretty12(set)}; Rehearsal {Pretty12(reh)}; "
                           + (showStart.HasValue ? $"Start {Pretty12(showStart)}; " : "")
                           + (showEnd.HasValue ? $"End {Pretty12(showEnd)}; " : "")
                           + $"Pack Up {Pretty12(pack)}.";
                text = pretty.Trim();
            }

            // -------------- Send to the agent --------------
            var messages = await _chat.SendAsync(HttpContext.Session, text, ct);
            var msgList = messages is List<DisplayMessage> ml ? ml : messages.ToList();

            // -------------- Contact capture --------------
            var contactId = await TrySaveContactAsync(msgList, ct);

            // -------------- Consent (from *user* text) --------------
            if (LooksLikeConsent(text))
            {
                // No prereq check anymore — a clear "yes/proceed" sets consent.
                SetConsent(HttpContext.Session, true);
            }


            // -------------- Gate all writes --------------

            // -------------- Gate all writes --------------
            var isSummary = HasFinalSummary(msgList);
            var hasConsent = HasConsent(HttpContext.Session);

            // Build facts ONCE in the controller from the best assistant "summary" message
            var facts = ExtractFactsForPersistence(msgList);   // <— new helper below

            if (isSummary && hasConsent && contactId != null)
            {
                // 1) Save header
                await _chat.TrySaveBookingAsync(_bookingDb, HttpContext.Session, msgList, contactId, ct);

                // 2) Booking no
                var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");
                if (!string.IsNullOrWhiteSpace(bookingNo))
                {
                    // 3) Items & crew (pass facts)
                    await _chat.UpsertItemsFromSummaryAsync(_bookingDb, HttpContext.Session, msgList, facts, ct);
                    await _chat.InsertCrewRowsAsync(_bookingDb, bookingNo, facts, ct);

                    // 4) Confirm with booking no.
                    //var body = $"Your booking has been created 🎉\n\n" +
                    //           $"• **Booking No:** {bookingNo}\n" +
                    //           $"Would you like me to email the quote or make any adjustments?";
                    //var html = $"<p><strong>Your booking has been created 🎉</strong></p>" +
                    //           $"<p>Booking No: <code>{System.Net.WebUtility.HtmlEncode(bookingNo)}</code></p>" +
                    //           $"<p>Would you like me to email the quote or make any adjustments?</p>";
                    //msgList.Add(new DisplayMessage("assistant", DateTimeOffset.UtcNow, new[] { body })
                    //{ FullText = body, Html = html });

                    SetConsent(HttpContext.Session, false);     // avoid duplicates
                    HttpContext.Session.SetString("Draft:ShowedBookingNo", "1");
                }
            }

            //else
            //{
            //    // If a summary just showed and we don't have consent yet, ask *once*.
            //    if (isSummary && !hasConsent && !LooksLikeConsent(text))
            //    {
            //        const string prompt = "Would you like me to generate your quote now?";
            //        msgList.Add(new DisplayMessage("assistant", DateTimeOffset.UtcNow, new[] { prompt })
            //        { FullText = prompt, Html = $"<p>{System.Net.WebUtility.HtmlEncode(prompt)}</p>" });
            //    }
            //}

            return PartialView("_Messages", msgList);
        }
        catch (Exception ex)
        {
            Response.StatusCode = 500;
            return Content(ex.Message);
        }
    }
    private static IDictionary<string, string> ExtractFactsForPersistence(IReadOnlyList<DisplayMessage> messages)
    {
        if (messages == null || messages.Count == 0) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static string Norm(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant()
                 .Replace('’', '\'').Replace('‘', '\'')
                 .Replace('“', '"').Replace('”', '"')
                 .Replace('–', '-').Replace('—', '-');
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        static bool LooksLikeSummaryText(string t) =>
               t.Contains("final summary")
            || t.Contains("here is your summary")
            || t.Contains("here’s your summary")
            || t.Contains("let me summarize")
            || t.Contains("does this look correct")
            || t.Contains("before i create your quote")
            || t.Contains("before creating your quote")
            || t.Contains("can you please confirm")
            || t.Contains("please confirm if all these details are correct");

        static bool LooksLikeQuoteText(string t) =>
               t.Contains("your quote is ready")
            || t.Contains("total estimated cost")
            || t.Contains("total amount")
            || t.Contains("inc gst") || t.Contains("including gst")
            || t.Contains("quote number") || t.Contains("quotation");

        static bool HasSummaryLabels(string raw) =>
               raw.IndexOf("number of speakers", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("number of presenters", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("number of guests", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("date and time", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("room:", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("layout:", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("equipment:", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("rehearsal", StringComparison.OrdinalIgnoreCase) >= 0
            || raw.IndexOf("technical support", StringComparison.OrdinalIgnoreCase) >= 0;

        DisplayMessage? summary = null;

        for (int i = messages.Count - 1, seen = 0; i >= 0 && seen < 10; i--)
        {
            var m = messages[i];
            if (!"assistant".Equals(m.Role, StringComparison.OrdinalIgnoreCase)) continue;
            seen++;

            var raw = string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
            var t = Norm(raw);

            if (LooksLikeQuoteText(t)) continue;                   // skip quote blocks
            if (LooksLikeSummaryText(t) || HasSummaryLabels(raw))  // pick a summary
            { summary = m; break; }
        }

        summary ??= messages.LastOrDefault(m => "assistant".Equals(m.Role, StringComparison.OrdinalIgnoreCase));
        var rawSummary = string.Join("\n", summary?.Parts ?? Enumerable.Empty<string>());
        return ParseSummaryFacts(rawSummary);
    }


    private const string GenerateQuoteFlag = "Draft:GenerateQuote";
    // --- Quote gating helpers ----------------------------------------------------

    // -------------------- Consent + Summary helpers --------------------
    // -------------------- Consent + Summary helpers --------------------
    private static bool LooksLikeConsent(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return false;

        var t = userText.ToLowerInvariant();
        // very common patterns
        string[] yesPhrases =
        {
        "yes", "yep", "yeah", "please proceed", "proceed", "go ahead",
        "generate the quote", "generate quote", "create the quote",
        "prepare the quote", "make the quote", "send the quote",
        "yes create quote", "yes please", "that’s ok", "thats ok", "this ok", "looks good"
    };

        return yesPhrases.Any(p => t.Contains(p));
    }


    private static bool HasFinalSummary(IReadOnlyList<DisplayMessage> messages, int lookback = 6)
    {
        if (messages == null || messages.Count == 0) return false;

        // Search the last few assistant turns for a “summary checkpoint”
        for (int i = messages.Count - 1, seen = 0; i >= 0 && seen < lookback; i--)
        {
            var m = messages[i];
            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;
            seen++;

            var raw = string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
            var t = Normalize(raw);

            if (LooksLikeSummaryText(t)) return true;
        }
        return false;

        static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant();
            s = s.Replace('’', '\'').Replace('‘', '\'')
                 .Replace('“', '"').Replace('”', '"')
                 .Replace('–', '-').Replace('—', '-');
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        static bool LooksLikeSummaryText(string t) =>
               t.Contains("final summary")
            || t.Contains("here is your final summary")
             || t.Contains("quote")
            || t.Contains("here's your final summary")
            || t.Contains("let me summarize")
            || t.Contains("here’s your summary")
            || t.Contains("here is your summary")
            || t.Contains("does this look correct")
            || t.Contains("before creating your quote")
            || t.Contains("before i create your quote");
    }


   private const string ConsentKey = "Draft:Consent";

    private static bool HasConsent(ISession s)
        => string.Equals(s.GetString(ConsentKey), "1", StringComparison.Ordinal);

    private static void SetConsent(ISession s, bool on)
        => s.SetString(ConsentKey, on ? "1" : "0");


    // --- Simple "summary facts" parser for the last assistant message ---
    // Returns a case-insensitive dictionary: Label -> Value
    private static Dictionary<string, string> ParseSummaryFacts(string raw)
    {
        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return facts;

        // split lines and try a few patterns
        var lines = raw.Replace("\r", "").Split('\n');

        // Pattern A: bullet/normal lines:  • Label: value    or    - Label – value
        var reLine = new Regex(@"^\s*(?:[\*\u2022\-]\s*)?(?<label>[^:–\-]+?)\s*[:\-–]\s*(?<value>.+?)\s*$",
                               RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern B: markdown-y bracketed:  [**Label**, **Value**]
        var reBracket = new Regex(@"^\s*\[\s*\*\*(?<label>[^*]+?)\*\*\s*,\s*\*\*(?<value>.+?)\*\*\s*\]\s*$",
                                  RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            string? label = null, value = null;

            var m1 = reLine.Match(line);
            if (m1.Success)
            {
                label = m1.Groups["label"].Value.Trim();
                value = m1.Groups["value"].Value.Trim();
            }
            else
            {
                var m2 = reBracket.Match(line);
                if (m2.Success)
                {
                    label = m2.Groups["label"].Value.Trim('*', ' ', '\t');
                    value = m2.Groups["value"].Value.Trim('*', ' ', '\t');
                }
            }

            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
            {
                // prefer the latest non-empty value for a label
                facts[label] = value;
            }
        }

        return facts;
    }

    private static Dictionary<string, string> ExtractFactsFromLastAssistant(IEnumerable<DisplayMessage> messages)
    {
        var lastAssistant = messages.LastOrDefault(m =>
            string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));

        var raw = string.Join("\n", lastAssistant?.Parts ?? Enumerable.Empty<string>());
        return ParseSummaryFacts(raw);
    }


    // Detects the assistant's final confirmation/summary so we only persist then.
   

    // True if the POSTed text was a schedule selection command.
    private static bool IsScheduleSelection(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Trim().StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase);
    }

    // Checks whether we have enough to persist: room (from transcript),
    // date (from your extractor), and start/end (already in Session from pickers).

    [HttpGet]
    public async Task<IActionResult> TranscriptPartial(CancellationToken ct)
    {
        try
        {
            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(threadId);
            return PartialView("_Messages", messages);
        }
        catch (Exception ex)
        {
            Response.StatusCode = 500;
            return Content(ex.Message);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPartial(CancellationToken ct)
    {
        try
        {
            HttpContext.Session.Remove("AgentThreadId");
            var newThreadId = _chat.EnsureThreadId(HttpContext.Session);

            var userKey = GetUserKey();
            await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(newThreadId);
            return PartialView("_Messages", messages);
        }
        catch (Exception ex)
        {
            Response.StatusCode = 500;
            return Content(ex.Message);
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static bool TryCaptureTimeSelection(string text, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;

        var m = ChooseTimeRe.Match(text);
        if (!m.Success) return false;

        if (TimeSpan.TryParse(m.Groups[1].Value, out start) &&
            TimeSpan.TryParse(m.Groups[2].Value, out end))
        {
            return true;
        }
        return false;
    }

    private void SaveTimeSelectionToSession(TimeSpan start, TimeSpan end)
    {
        HttpContext.Session.SetString("Draft:StartTime", start.ToString(@"hh\:mm"));
        HttpContext.Session.SetString("Draft:EndTime", end.ToString(@"hh\:mm"));
    }

    // Schedule: parse & stash
    private static bool TryCaptureScheduleSelection(
        string text,
        out TimeSpan setup,
        out TimeSpan rehearsal,
        out TimeSpan? showStart,
        out TimeSpan? showEnd,
        out TimeSpan packup)
    {
        setup = default;
        rehearsal = default;
        showStart = null;
        showEnd = null;
        packup = default;

        var m = ChooseScheduleRe.Match(text);
        if (!m.Success) return false;

        var blob = m.Groups[1].Value; // "setup=07:00; rehearsal=09:30; start=10:00; end=16:00; packup=18:00"
        var kvs = blob.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        TimeSpan ts;
        bool gotSetup = false, gotReh = false, gotPack = false;

        foreach (var kv in kvs)
        {
            var parts = kv.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            var key = parts[0].ToLowerInvariant();
            var val = parts[1];

            if (!TimeSpan.TryParse(val, CultureInfo.InvariantCulture, out ts)) continue;

            switch (key)
            {
                case "setup":
                    setup = ts; gotSetup = true; break;
                case "rehearsal":
                    rehearsal = ts; gotReh = true; break;
                case "start":
                case "showstart":
                case "eventstart":
                    showStart = ts; break;
                case "end":
                case "showend":
                case "eventend":
                    showEnd = ts; break;
                case "packup":
                case "pack_down":
                case "packdown":
                    packup = ts; gotPack = true; break;
            }
        }

        // Require at least setup/rehearsal/packup to consider it a valid schedule capture.
        return gotSetup && gotReh && gotPack;
    }

    private void SaveScheduleToSession(
        TimeSpan setup,
        TimeSpan rehearsal,
        TimeSpan? showStart,
        TimeSpan? showEnd,
        TimeSpan packup)
    {
        HttpContext.Session.SetString("Draft:SetupTime", setup.ToString(@"hh\:mm"));
        HttpContext.Session.SetString("Draft:RehearsalTime", rehearsal.ToString(@"hh\:mm"));
        if (showStart.HasValue) HttpContext.Session.SetString("Draft:StartTime", showStart.Value.ToString(@"hh\:mm"));
        if (showEnd.HasValue) HttpContext.Session.SetString("Draft:EndTime", showEnd.Value.ToString(@"hh\:mm"));
        HttpContext.Session.SetString("Draft:PackupTime", packup.ToString(@"hh\:mm"));
    }

    private static string Pretty12(TimeSpan? ts)
    {
        if (!ts.HasValue) return "";
        var dummy = DateTime.Today.Add(ts.Value);
        return dummy.ToString("h:mm tt", CultureInfo.InvariantCulture);
    }

    private async Task<decimal?> TrySaveContactAsync(IEnumerable<DisplayMessage> messages, CancellationToken ct)
    {
        try
        {
            var ci = _chat.ExtractContactInfo(messages);

            var inferredName = GuessUserNameFromTranscript(messages);

            string? name = ci?.Name?.Trim();
            string? email = ci?.Email?.Trim();
            string? phone = ci?.PhoneE164?.Trim();

            // If we inferred a name from the user's last reply and it doesn't look like the assistant,
            // ALWAYS prefer it (this overrides "Isla from Microhire").
            if (!string.IsNullOrWhiteSpace(inferredName) && !LooksLikeAssistantName(inferredName))
            {
                name = inferredName;
            }

            // If name is still missing or still looks like the assistant, stop here.
            if (!LooksLikeValidHumanName(name))
                return null;
            // Allow provisional save with name only; email/phone may come later
            return await _chat.UpsertContactByEmailAsync(_bookingDb, name!, email, phone, ct);
        }
        catch
        {
            // Don’t break the chat flow because of contact-capture issues
            return null;
        }

        static bool LooksLikeAssistantName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim().ToLowerInvariant();

            // catches: "isla", "isla from microhire", "isla - microhire", etc.
            if (t.Contains("isla") && t.Contains("microhire")) return true;
            if (t.Equals("isla")) return true;

            // also treat very bot-ish phrases as non-user names
            if (t.StartsWith("my name is ")) return true;

            return false;
        }


        static bool LooksLikeValidHumanName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim();
            if (t.Length < 2) return false;
            if (t.Contains('@')) return false;            // not an email
            if (t.Length > 60) return false;              // too long for a name
            return true;
        }
    }

    /// <summary>
    /// If the last assistant asked for the guest’s name, take the next user message
    /// (short, no '@', <= 4 words) as the name. Fallback: latest short user message.
    /// </summary>
    private static string? GuessUserNameFromTranscript(IEnumerable<DisplayMessage> messages)
    {
        var list = messages?.ToList() ?? new List<DisplayMessage>();
        if (list.Count == 0) return null;

        // Find last assistant turn that asked for the name
        int askIdx = -1;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var m = list[i];
            if (!"assistant".Equals(m.Role, StringComparison.OrdinalIgnoreCase)) continue;

            var text = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).ToLowerInvariant();
            if (text.Contains("what is your full name") || text.Contains("your full name"))
            {
                askIdx = i;
                break;
            }
        }

        // If we found the ask, the next user message after that is likely the name
        if (askIdx >= 0)
        {
            for (int i = askIdx + 1; i < list.Count; i++)
            {
                var u = list[i];
                if (!"user".Equals(u.Role, StringComparison.OrdinalIgnoreCase)) continue;
                var txt = (string.Join("\n", u.Parts ?? Enumerable.Empty<string>())).Trim();
                if (LooksLikeName(txt)) return ToTitle(txt);
            }
        }

        // Fallback: latest plausible user message that looks like a name
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var u = list[i];
            if (!"user".Equals(u.Role, StringComparison.OrdinalIgnoreCase)) continue;
            var txt = (string.Join("\n", u.Parts ?? Enumerable.Empty<string>())).Trim();
            if (LooksLikeName(txt)) return ToTitle(txt);
        }

        return null;

        static bool LooksLikeName(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return false;
            if (txt.Contains('@')) return false;
            if (txt.StartsWith("choose ", StringComparison.OrdinalIgnoreCase)) return false; // UI command
            var words = txt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words.Length > 4) return false;
            return txt.Length <= 60;
        }

        static string ToTitle(string s)
            => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }

}
