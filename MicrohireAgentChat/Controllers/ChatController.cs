// Controllers/ChatController.cs
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models; // for DisplayMessage if needed
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

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
            RedactPricesForUiInPlace(messages);
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
            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }
            if (TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack);
                var pretty = $"Schedule selected: Setup {Pretty12(set)}; Rehearsal {Pretty12(reh)}; "
                           + (showStart.HasValue ? $"Start {Pretty12(showStart)}; " : "")
                           + (showEnd.HasValue ? $"End {Pretty12(showEnd)}; " : "")
                           + $"Pack Up {Pretty12(pack)}.";
                text = pretty.Trim();
            }

            // —— rate-limit safe send ——
            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var sem = GetThreadLock(threadId);
            await sem.WaitAsync(ct);
            try
            {
                var messages = await WithRateLimitRetry(
                    () => _chat.SendAsync(HttpContext.Session, text, ct),
                    ct);

                RedactPricesForUiInPlace(messages);
                return View("Index", messages);
            }
            finally { sem.Release(); }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var (_, messages) = _chat.GetTranscript(threadId);
            RedactPricesForUiInPlace(messages);
            return View("Index", messages);
        }
    }

    private static bool WasLastAssistantASummaryAsk(IReadOnlyList<DisplayMessage> messages)
    {
        if (messages == null || messages.Count < 2) return false;

        // Find the last USER turn (the message you just posted)
        int userIdx = -1;
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var m = messages[i];
            if (string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                userIdx = i;
                break;
            }
        }
        if (userIdx <= 0) return false;

        // Find the nearest ASSISTANT turn before that USER turn
        for (int j = userIdx - 1; j >= 0; j--)
        {
            var a = messages[j];
            if (!string.Equals(a.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var raw = string.Join("\n\n", a.Parts ?? Enumerable.Empty<string>());
            var t = Normalize(raw);
            return LooksLikeSummaryAsk(t);
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

        // A "summary ask" = assistant showed the summary and asked to confirm before quoting
        static bool LooksLikeSummaryAsk(string t) =>
               t.Contains("here is your summary")
            || t.Contains("here’s your summary")
            || t.Contains("here's what i have so far")
            || t.Contains("let me summarize")
            || t.Contains("does everything look correct")
            || t.Contains("does this look correct")
            || t.Contains("please confirm")
            || t.Contains("can you confirm")
            || t.Contains("before i create your quote")
            || t.Contains("before creating your quote");
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

            // capture time range (single picker)
            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }

            // capture full schedule (multi-time)
            if (TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack);
                var pretty = $"Schedule selected: Setup {Pretty12(set)}; Rehearsal {Pretty12(reh)}; "
                           + (showStart.HasValue ? $"Start {Pretty12(showStart)}; " : "")
                           + (showEnd.HasValue ? $"End {Pretty12(showEnd)}; " : "")
                           + $"Pack Up {Pretty12(pack)}.";
                text = pretty.Trim();
            }

            // —— rate-limit safe send ——
            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var sem = GetThreadLock(threadId);
            IEnumerable<DisplayMessage> messages;
            await sem.WaitAsync(ct);
            try
            {
                messages = await WithRateLimitRetry(
                    () => _chat.SendAsync(HttpContext.Session, text, ct),
                    ct);
            }
            finally { sem.Release(); }

            var msgList = messages is List<DisplayMessage> ml ? ml : messages.ToList();

            // ---------------- ORGANISATION + CUSTOMER CODE + LINK CONTACT ----------------
            // Extract from transcript
            var (orgName, orgAddr) = _chat.ExtractOrganisationFromTranscript(msgList);

            string? customerCode = null;
            decimal? orgId = null;

            if (!string.IsNullOrWhiteSpace(orgName))
            {
                // Existing?
                var found = await _chat.FindOrganisationAsync(_bookingDb, orgName!, ct);
                if (found is not null)
                {
                    orgId = found.Value.id;
                    customerCode = found.Value.code;
                    // Optional: show back the canonical name for verification
                    // TempData["FoundOrganisationName"] = found.Value.name;
                }
            }

            // Create if not found
            if (orgId is null && !string.IsNullOrWhiteSpace(orgName))
            {
                orgId = await _chat.UpsertOrganisationAsync(_bookingDb, orgName!, orgAddr, ct);
                if (orgId is not null)
                    customerCode = await _chat.GetCustomerCodeByIdAsync(_bookingDb, orgId.Value, ct);
            }

            // Try resolve contact now (independent of consent) so we can link it
            decimal? contactId = await TrySaveContactAsync(msgList, ct);

            // Link contact to organisation when both are known
            if (contactId is not null && !string.IsNullOrWhiteSpace(customerCode))
            {
                await _chat.LinkContactToOrganisationAsync(_bookingDb, customerCode!, contactId.Value, ct);
                // store the code for later booking creation use
                HttpContext.Session.SetString("Draft:CustomerCode", customerCode!);
            }
            // ---------------------------------------------------------------------------

            // build facts once
            var facts = ExtractFactsForPersistence(msgList);

            // persist ONLY when the user's latest message is a positive reply
            // to the immediately preceding assistant summary confirmation
            var isConsentToSummary = LooksLikeConsent(text) && WasLastAssistantASummaryAsk(msgList);

            if (isConsentToSummary)
            {
                // contactId might already be set above; if not, try again quickly
                contactId ??= await TrySaveContactAsync(msgList, ct);

                var summaryKey = ComputeSummaryKey(msgList);
                var lastPersistedKey = HttpContext.Session.GetString("Draft:PersistedSummaryKey");
                if (!string.IsNullOrEmpty(summaryKey) &&
                    string.Equals(summaryKey, lastPersistedKey, StringComparison.Ordinal))
                {
                    RedactPricesForUiInPlace(msgList);
                    // keep CTA state for the view as well
                    ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
                    return PartialView("_Messages", msgList);
                }

                await _chat.TrySaveBookingAsync(_bookingDb, HttpContext.Session, msgList, contactId, ct);

                var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");
                if (!string.IsNullOrWhiteSpace(bookingNo))
                {
                    var hasAnyItems = await _bookingDb.TblItemtrans.AnyAsync(x => x.BookingNoV32 == bookingNo, ct);
                    var hasAnyCrew = await _bookingDb.TblCrews.AnyAsync(c => c.BookingNoV32 == bookingNo, ct);

                    if (!hasAnyItems && !hasAnyCrew)
                    {
                        await _chat.UpsertItemsFromSummaryAsync(_bookingDb, HttpContext.Session, msgList, facts, ct);
                        await _chat.InsertCrewRowsAsync(_bookingDb, bookingNo, facts, ct);
                    }

                    HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey ?? string.Empty);
                    SetConsent(HttpContext.Session, false);
                    HttpContext.Session.SetString("Draft:ShowedBookingNo", "1");
                }
            }

            RedactPricesForUiInPlace(msgList);

            // Tell the view whether to show the Yes/No buttons
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";

            return PartialView("_Messages", msgList);
        }
        catch (Exception ex)
        {
            Response.StatusCode = 500;
            return Content(ex.Message);
        }
    }

    private static string ComputeSummaryKey(IReadOnlyList<DisplayMessage> messages, int lookback = 6)
    {
        if (messages == null || messages.Count == 0) return string.Empty;

        for (int i = messages.Count - 1, seen = 0; i >= 0 && seen < lookback; i--)
        {
            var m = messages[i];
            if (!"assistant".Equals(m.Role, StringComparison.OrdinalIgnoreCase)) continue;
            seen++;

            var raw = string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
            var t = Regex.Replace(raw, @"\s+", " ").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(t))
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var bytes = System.Text.Encoding.UTF8.GetBytes(t);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToHexString(hash); // e.g., "A1B2..."
            }
        }
        return string.Empty;
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

    private static bool LooksLikeConsent(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return false;

        var t = userText.ToLowerInvariant();
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

    private static Dictionary<string, string> ParseSummaryFacts(string raw)
    {
        var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return facts;

        var lines = raw.Replace("\r", "").Split('\n');

        var reLine = new Regex(@"^\s*(?:[\*\u2022\-]\s*)?(?<label>[^:–\-]+?)\s*[:\-–]\s*(?<value>.+?)\s*$",
                               RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    private static bool IsScheduleSelection(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Trim().StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> TranscriptPartial(CancellationToken ct)
    {
        try
        {
            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(threadId);

            // Tell the view whether to show the Yes/No buttons
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(messages.ToList()) ? "1" : "0";

            RedactPricesForUiInPlace(messages);
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

            // Tell the view whether to show the Yes/No buttons
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(messages.ToList()) ? "1" : "0";

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

        var blob = m.Groups[1].Value;
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
            if (messages is null) return null;

            var list = messages.ToList();
            static bool IsUser(DisplayMessage m) =>
                string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase);

            var emailRe = new Regex(@"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var phoneRe = new Regex(@"\+?\d[\d\s\-()]{6,}\d", RegexOptions.Compiled);

            string? userEmail = null, userPhone = null, userPosition = null;

            // Look back through ~20 user turns
            for (int i = list.Count - 1, seen = 0; i >= 0 && seen < 20; i--)
            {
                var m = list[i];
                if (!IsUser(m)) continue;
                seen++;

                var txt = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
                if (string.IsNullOrWhiteSpace(txt)) continue;

                // email
                if (userEmail is null)
                {
                    var em = emailRe.Match(txt);
                    if (em.Success) userEmail = em.Value.Trim();
                }

                // phone
                if (userPhone is null)
                {
                    var pm = phoneRe.Match(txt);
                    if (pm.Success) userPhone = NormalizePhone(pm.Value);
                }

                // position/title
                if (userPosition is null)
                {
                    var p = ExtractPosition(txt);
                    if (!string.IsNullOrWhiteSpace(p)) userPosition = p;
                }

                if (userEmail != null && userPhone != null && userPosition != null) break;
            }

            var userName = GuessUserNameFromTranscript(list);

            if (!LooksLikeValidHumanName(userName) || LooksLikeAssistantName(userName))
                userName = null;

            if (string.IsNullOrWhiteSpace(userName))
                return null;

            // NOTE: now passing position along
            return await _chat.UpsertContactByEmailAsync(_bookingDb, userName, userEmail, userPhone, userPosition, ct);
        }
        catch
        {
            return null;
        }

        static bool LooksLikeAssistantName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim().ToLowerInvariant();
            if (t.Contains("isla") && t.Contains("microhire")) return true;
            if (t.Equals("isla")) return true;
            if (t.StartsWith("my name is ")) return true;
            return false;
        }

        static bool LooksLikeValidHumanName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim();
            if (t.Length < 2 || t.Length > 60) return false;
            if (t.Contains('@')) return false;
            return true;
        }

        static string NormalizePhone(string raw)
        {
            raw = raw.Trim();
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var ch in raw)
            {
                if (char.IsDigit(ch) || (ch == '+' && sb.Length == 0)) sb.Append(ch);
            }
            return sb.ToString();
        }

        // Heuristics to pull a position/title out of free text
        static string? ExtractPosition(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Replace('’', '\'').Replace('“', '"').Replace('”', '"');
            // 1) Labeled: Position/Title/Role: <value>
            var reLabel = new Regex(@"(?:^|\b)(position|title|role)\s*[:\-]\s*(?<v>.+)$",
                RegexOptions.IgnoreCase);
            var m1 = reLabel.Match(t);
            if (m1.Success) return Clean(m1.Groups["v"].Value);

            // 2) “I am the <value>”, “I'm <value> at …”, “I work as <value>”
            var reIam = new Regex(@"\b(i\s*am|i'm|i\s*work\s*as|my\s*role\s*is)\s+(the\s+)?(?<v>[A-Za-z][A-Za-z \-/,&\.]{2,50})",
                RegexOptions.IgnoreCase);
            var m2 = reIam.Match(t);
            if (m2.Success) return Clean(m2.Groups["v"].Value);

            // 3) “<value> at <org>”
            var reAt = new Regex(@"^(?<v>[A-Za-z][A-Za-z \-/,&\.]{2,50})\s+at\s+.+", RegexOptions.IgnoreCase);
            var m3 = reAt.Match(t);
            if (m3.Success) return Clean(m3.Groups["v"].Value);

            return null;

            static string Clean(string s)
            {
                s = s.Trim();
                // strip trailing sentence punctuation
                s = s.TrimEnd('.', ',', ';', ':');
                // keep it short & neat
                if (s.Length > 60) s = s.Substring(0, 60);
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
            }
        }
    }


    private static string? GuessUserNameFromTranscript(IEnumerable<DisplayMessage> messages)
    {
        var list = messages?.ToList() ?? new List<DisplayMessage>();
        if (list.Count == 0) return null;

        // Helper: trim to first line/sentence and clean up
        static string FirstSegment(string s)
        {
            s = (s ?? string.Empty).Trim();
            // split on newline or sentence punctuation
            var cut = s.IndexOfAny(new[] { '\n', '.', '!', '?' });
            if (cut >= 0) s = s[..cut];
            // remove trailing commas etc.
            s = s.Trim().Trim(',', ';', ':');
            return s;
        }

        // Helper: strong name check (letters, space, apostrophe, hyphen, dot)
        static bool LooksLikeName(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return false;
            var t = txt.Trim();

            // reject emails, numbers, and known non-name words
            if (t.Contains('@')) return false;
            if (t.Any(char.IsDigit)) return false;

            var lower = t.ToLowerInvariant();
            if (lower.Contains("address") || lower.Contains("email") || lower.Contains("phone") || lower.Contains("position"))
                return false;

            // allow 1–4 words of letters/.-'
            var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words.Length > 4) return false;

            var nameWord = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z][a-zA-Z\.'\-]{0,59}$");
            if (!words.All(w => nameWord.IsMatch(w))) return false;

            // not absurdly short/long overall
            if (t.Length < 2 || t.Length > 60) return false;

            return true;
        }

        static string ToTitle(string s)
            => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

        // 1) Find the assistant message that asks for the user's name
        var askRegex = new Regex(
            @"(what\s+is\s+your\s+full\s+name\??|your\s+full\s+name\??|may\s+i\s+know\s+your\s+name\??|please\s+share\s+your\s+name)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        int askIdx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (!"assistant".Equals(m.Role, StringComparison.OrdinalIgnoreCase)) continue;

            var text = string.Join("\n", m.Parts ?? Enumerable.Empty<string>());
            if (askRegex.IsMatch(text))
            {
                askIdx = i;
                break;
            }
        }

        // 2) If we found the ask, take the VERY NEXT plausible user reply
        if (askIdx >= 0)
        {
            for (int i = askIdx + 1; i < list.Count; i++)
            {
                var u = list[i];
                if (!"user".Equals(u.Role, StringComparison.OrdinalIgnoreCase)) continue;

                var raw = string.Join("\n", u.Parts ?? Enumerable.Empty<string>()).Trim();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var seg = FirstSegment(raw);
                if (LooksLikeName(seg)) return ToTitle(seg);
            }
        }

        // 3) Fallbacks:
        //   a) explicit "my name is ..." from any user message
        var nameIs = new Regex(@"\b(my\s+name\s+is|i'm|i\s+am)\s+(?<v>[A-Za-z][A-Za-z \.'\-]{1,60})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var u = list[i];
            if (!"user".Equals(u.Role, StringComparison.OrdinalIgnoreCase)) continue;
            var txt = string.Join("\n", u.Parts ?? Enumerable.Empty<string>()).Trim();
            var m = nameIs.Match(txt);
            if (m.Success)
            {
                var v = FirstSegment(m.Groups["v"].Value);
                if (LooksLikeName(v)) return ToTitle(v);
            }
        }

        //   b) last plausible bare user line that looks like a name
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var u = list[i];
            if (!"user".Equals(u.Role, StringComparison.OrdinalIgnoreCase)) continue;
            var seg = FirstSegment(string.Join("\n", u.Parts ?? Enumerable.Empty<string>()));
            if (LooksLikeName(seg)) return ToTitle(seg);
        }

        return null;
    }


    // Price/quote redaction
    private static readonly Regex CurrencyAmountRe =
        new(@"(?ix)
        (?:[$₹]|AUD|USD|INR)\s*            # currency symbol/code
        \d{1,3}(?:,\d{3})*(?:\.\d{1,2})?   # 1,234.56
    ");

    private static readonly Regex PriceOrQuoteLineRe =
        new(@"(?i)\b(
        total\s+(amount|estimated\s*cost)|
        amount\s+comes\s+to|
        (incl(?:uding)?\s*)?gst|
        quote(\s*number)?|
        price|cost|fee|charge
    )\b");

    private static void RedactPricesForUiInPlace(IEnumerable<DisplayMessage> messages)
    {
        if (messages == null) return;

        foreach (var m in messages)
        {
            if (!"assistant".Equals(m.Role, StringComparison.OrdinalIgnoreCase) || m.Parts == null)
                continue;

            var newParts = new List<string>(m.Parts.Count);
            foreach (var part in m.Parts)
            {
                var lines = (part ?? string.Empty).Replace("\r", "").Split('\n');
                var kept = new List<string>();

                foreach (var line in lines)
                {
                    if (CurrencyAmountRe.IsMatch(line)) continue;
                    if (PriceOrQuoteLineRe.IsMatch(line)) continue;
                    kept.Add(line);
                }

                if (kept.Count == 0 && lines.Length > 0)
                    kept.Add("[A quote was prepared. Price is hidden in chat.]");

                newParts.Add(string.Join("\n", kept));
            }

            m.Parts = newParts;
        }
    }

    // One in-flight call per thread prevents parallel runs that trigger 429s.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _threadLocks = new();

    private SemaphoreSlim GetThreadLock(string threadId)
        => _threadLocks.GetOrAdd(threadId, _ => new SemaphoreSlim(1, 1));

    private static async Task<T> WithRateLimitRetry<T>(
        Func<Task<T>> action,
        CancellationToken ct,
        int maxAttempts = 5,
        double baseDelaySeconds = 2.0)
    {
        var rnd = new Random();
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action();
            }
            catch (Exception ex) when (IsRateLimit(ex))
            {
                var retryAfter = ParseRetryAfterSeconds(ex);
                var delay = retryAfter.HasValue
                    ? TimeSpan.FromSeconds(retryAfter.Value)
                    : TimeSpan.FromSeconds(Math.Min(30, baseDelaySeconds * Math.Pow(2, attempt - 1)) + rnd.NextDouble());

                if (attempt == maxAttempts) throw;
                await Task.Delay(delay, ct);
            }
        }
        throw new InvalidOperationException("Unreachable");

        static bool IsRateLimit(Exception ex)
        {
            var msg = ex.Message?.ToLowerInvariant() ?? "";
            return msg.Contains("rate limit") || msg.Contains("429") || msg.Contains("too many requests") || msg.Contains("try again in");
        }

        static double? ParseRetryAfterSeconds(Exception ex)
        {
            var m = Regex.Match(ex.Message ?? "", @"try again in\s+(\d+)\s*second", RegexOptions.IgnoreCase);
            if (m.Success && double.TryParse(m.Groups[1].Value, out var s)) return s;
            return null;
        }
    }
}
