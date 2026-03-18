// Controllers/ChatController.cs
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models; // for DisplayMessage if needed
using MicrohireAgentChat.Services;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace MicrohireAgentChat.Controllers;

public sealed class ChatController : Controller
{
    private readonly AzureAgentChatService _chat;
    private readonly BookingDbContext _bookingDb;
    private readonly BookingOrchestrationService _orchestration;
    private readonly HtmlQuoteGenerationService _htmlQuoteGen;
    private readonly ItemPersistenceService _itemPersistence;
    private readonly ContactPersistenceService _contactPersistence;
    private readonly BookingPersistenceService _bookingPersistence;
    private readonly ConversationReplayService _replayService;
    private readonly AutoTestCustomerService _autoTestService;
    private readonly DevModeOptions _devOptions;
    private readonly AutoTestOptions _autoTestOptions;
    private readonly IWebHostEnvironment _env;
    private readonly IRazorViewEngine _razorViewEngine;
    private readonly ILogger<ChatController> _logger;

    // Detect: "Choose time: 09:00–10:30" (supports hyphen or en dash)
    private static readonly Regex ChooseTimeRe =
        new(@"^Choose\s*time:\s*(\d{1,2}:\d{2})\s*[–-]\s*(\d{1,2}:\d{2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Detect: "Choose schedule: key=HH:mm; key=HH:mm; ..."
    private static readonly Regex ChooseScheduleRe =
        new(@"^\s*Choose\s+schedule\s*:\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>True if the name is the assistant's (Isla, Microhire, or both). Used to avoid saving assistant name as contact.</summary>
    private static bool LooksLikeAssistantName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().ToLowerInvariant();
        return t == "isla" || t == "microhire" || (t.Contains("isla") && t.Contains("microhire"));
    }

    // Exact scripted greeting per your "Final Script for AI"
    private const string GreetingText =
        "Hello, my name is Isla from Microhire. What is your full name?";
    private const string AwaitingQuoteReviewPromptKey = "Draft:AwaitingQuoteReviewPrompt";
    private const string QuoteReviewPromptShownKey = "Draft:QuoteReviewPromptShown";
    private const string ProjectorPromptShownKey = "Draft:ProjectorPromptShown";
    private const string ProjectorPromptThreadIdKey = "Draft:ProjectorPromptThreadId";
    private const string ProjectorAreaCapturedKey = "Draft:ProjectorAreaCaptured";
    private const string ProjectorAreaThreadIdKey = "Draft:ProjectorAreaThreadId";

    public ChatController(
        AzureAgentChatService chat,
        BookingDbContext bookingDb,
        BookingOrchestrationService orchestration,
        HtmlQuoteGenerationService htmlQuoteGen,
        ItemPersistenceService itemPersistence,
        ContactPersistenceService contactPersistence,
        BookingPersistenceService bookingPersistence,
        ConversationReplayService replayService,
        AutoTestCustomerService autoTestService,
        IOptions<DevModeOptions> devOptions,
        IOptions<AutoTestOptions> autoTestOptions,
        IWebHostEnvironment env,
        IRazorViewEngine razorViewEngine,
        ILogger<ChatController> logger)
    {
        _chat = chat;
        _bookingDb = bookingDb;
        _orchestration = orchestration;
        _htmlQuoteGen = htmlQuoteGen;
        _itemPersistence = itemPersistence;
        _contactPersistence = contactPersistence;
        _bookingPersistence = bookingPersistence;
        _replayService = replayService;
        _autoTestService = autoTestService;
        _devOptions = devOptions.Value;
        _autoTestOptions = autoTestOptions.Value;
        _env = env;
        _razorViewEngine = razorViewEngine;
        _logger = logger;
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

    // Check if user has completed quote
    private bool IsQuoteComplete()
    {
        return string.Equals(HttpContext.Session.GetString("Draft:QuoteComplete"), "1", StringComparison.Ordinal);
    }

    private static bool LooksLikeExplicitCreateQuoteConsent(string? userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return false;
        var t = userText.Trim().ToLowerInvariant();
        string[] explicitCreateQuotePhrases =
        {
            "yes create quote",
            "yes create the quote",
            "yes, create quote",
            "yes, create the quote",
            "create quote",
            "create the quote",
            "generate quote",
            "generate the quote",
            "yes generate quote",
            "yes generate the quote",
            "go ahead and create the quote",
            "go ahead create quote",
            "i'm ready for the quote",
            "im ready for the quote",
            "ready for the quote"
        };

        return explicitCreateQuotePhrases.Any(p => t.Contains(p, StringComparison.Ordinal));
    }

    /// <summary>
    /// Clears session keys that bind quote generation to an existing booking and carry-over quote payload.
    /// Use when user expresses "new quote" / "another quote" intent so the next quote creates a new booking.
    /// For full conversation reset (new thread), call this then also remove AgentThreadId, Draft:ContactId, Draft:CustomerCode, and Ack:*.
    /// </summary>
    private void ClearBookingAndQuoteDraftState()
    {
        HttpContext.Session.Remove("Draft:BookingNo");
        HttpContext.Session.Remove("Draft:QuoteComplete");
        HttpContext.Session.Remove("Draft:QuoteUrl");
        HttpContext.Session.Remove("Draft:QuoteAccepted");
        HttpContext.Session.Remove(AwaitingQuoteReviewPromptKey);
        HttpContext.Session.Remove(QuoteReviewPromptShownKey);
        HttpContext.Session.Remove("Draft:PersistedSummaryKey");
        HttpContext.Session.Remove("Draft:ShowedBookingNo");
        HttpContext.Session.Remove("Draft:SelectedEquipment");
        HttpContext.Session.Remove("Draft:SelectedLabor");
        HttpContext.Session.Remove("Draft:TotalDayRate");
        HttpContext.Session.Remove("Draft:ProjectorArea");
        HttpContext.Session.Remove("Draft:ProjectorAreas");
        HttpContext.Session.Remove(ProjectorPromptShownKey);
        HttpContext.Session.Remove(ProjectorPromptThreadIdKey);
        HttpContext.Session.Remove(ProjectorAreaCapturedKey);
        HttpContext.Session.Remove(ProjectorAreaThreadIdKey);
        HttpContext.Session.Remove("Draft:LaptopOwnershipAnswered");
        HttpContext.Session.Remove("Draft:NeedsProvidedLaptop");
        HttpContext.Session.Remove("Draft:LaptopPreference");
        HttpContext.Session.Remove("ContactSavePending");
        HttpContext.Session.Remove("ContactSaveCompleted");
    }

    private void PersistProjectorAreaSelection(string threadId, IReadOnlyList<string> projectorAreas)
    {
        if (projectorAreas == null || projectorAreas.Count == 0)
            return;

        HttpContext.Session.SetString("Draft:ProjectorAreas", string.Join(",", projectorAreas));
        HttpContext.Session.SetString("Draft:ProjectorArea", projectorAreas[0]);
        HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
        HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
        HttpContext.Session.SetString(ProjectorAreaCapturedKey, "1");
        HttpContext.Session.SetString(ProjectorAreaThreadIdKey, threadId);
    }

    private void SyncProjectorPromptMarkers(IEnumerable<DisplayMessage> messages, string threadId)
    {
        if (!messages.Any(IsProjectorPromptAssistantMessage))
            return;

        HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
        HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
    }

    private static bool IsProjectorPromptAssistantMessage(DisplayMessage message) =>
        string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
        && ((message.FullText?.Contains("/images/westin/westin-ballroom/floor-plan.png", StringComparison.OrdinalIgnoreCase) == true)
            || (message.Parts ?? Enumerable.Empty<string>()).Any(part =>
                part.Contains("/images/westin/westin-ballroom/floor-plan.png", StringComparison.OrdinalIgnoreCase)
                || part.Contains("projector placement area", StringComparison.OrdinalIgnoreCase)));


    // Get quote URL if available
    private string? GetQuoteUrl()
    {
        return HttpContext.Session.GetString("Draft:QuoteUrl");
    }

    private string? TryExtractAmountFromQuoteHtml(string bookingNo)
    {
        try
        {
            var quoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            if (string.IsNullOrWhiteSpace(quoteUrl)) return null;

            var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var filePath = Path.Combine(webRoot, quoteUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(filePath)) return null;

            var html = System.IO.File.ReadAllText(filePath);
            var match = Regex.Match(html, @"Total Quotation</div>\s*<div class=""confirmation-value"">(.*?)</div>", RegexOptions.Singleline);
            return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    // Full page
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            // Check for existing quote based on booking number in session
            var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");
            if (!string.IsNullOrWhiteSpace(bookingNo))
            {
                // Check for existing HTML quotes
                var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var quotesDir = Path.Combine(webRoot, "files", "quotes");
                var existingQuoteFile = Directory.Exists(quotesDir) 
                    ? Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                        .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                        .FirstOrDefault()
                    : null;
                if (!string.IsNullOrEmpty(existingQuoteFile))
                {
                    var existingQuoteUrl = $"/files/quotes/{Path.GetFileName(existingQuoteFile)}";
                    // Mark as complete but don't redirect - allow continued chat
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1");
                    HttpContext.Session.SetString("Draft:QuoteUrl", existingQuoteUrl);
                }
            }

            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(threadId);
            RedactPricesForUiInPlace(messages);
            ViewData["DevModeEnabled"] = _devOptions.Enabled;
            ViewData["IsDevelopment"] = _env.IsDevelopment();
            _logger.LogInformation("DevMode enabled: {_devOptions.Enabled}", _devOptions.Enabled);
            ViewData["QuoteComplete"] = false;
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(messages);
            return View(messages);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(Enumerable.Empty<DisplayMessage>());
        }
    }

    // Quote Complete Overlay
    [HttpGet]
    public IActionResult QuoteComplete(string quoteUrl)
    {
        // Redirect to chat interface - no longer using overlay
        // This handles any direct links or legacy redirects
        return RedirectToAction("Index");
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
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);

            text = text.Trim();
            if (LooksLikeNewQuoteIntent(text))
            {
                ClearBookingAndQuoteDraftState();
                _logger.LogInformation("[QUOTE FLOW] New quote intent detected in Send, cleared booking/quote draft state. UserText={UserText}", text.Length > 100 ? text.Substring(0, 100) + "..." : text);
            }
            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }

            var isInProjectorContextSend = HttpContext.Session.GetString(ProjectorPromptShownKey) == "1";
            if (TryCaptureProjectorAreaSelections(text, out var projectorAreas, inProjectorContext: isInProjectorContextSend))
            {
                PersistProjectorAreaSelection(threadId, projectorAreas);
            }
            if (TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack, out var eventDate))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack, eventDate);

                // DEBUG: Log the raw parsed times before formatting
                _logger.LogInformation("SEND METHOD - RAW PARSED TIMES: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    set, reh, showStart, showEnd, pack);
                _logger.LogInformation("SEND METHOD - RAW TimeSpan values: Setup.TotalHours={SetupHours}, Rehearsal.TotalHours={RehearsalHours}, Start.TotalHours={StartHours}, End.TotalHours={EndHours}, PackUp.TotalHours={PackUpHours}",
                    set.TotalHours, reh.TotalHours, showStart?.TotalHours, showEnd?.TotalHours, pack.TotalHours);

                // Build summary with all times - always include Start/End if they were provided
                _logger.LogInformation("FORMATTING TIMES: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    set, reh, showStart, showEnd, pack);

                var summaryParts = new List<string>
                {
                    $"Setup {Pretty12(set)}",
                    $"Rehearsal {Pretty12(reh)}"
                };

                if (showStart.HasValue)
                {
                    summaryParts.Add($"Start {Pretty12(showStart)}");
                }

                if (showEnd.HasValue)
                {
                    summaryParts.Add($"End {Pretty12(showEnd)}");
                }

                summaryParts.Add($"Pack Up {Pretty12(pack)}");

                _logger.LogInformation("FORMATTED TIMES: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    Pretty12(set), Pretty12(reh), showStart.HasValue ? Pretty12(showStart) : "null",
                    showEnd.HasValue ? Pretty12(showEnd) : "null", Pretty12(pack));

                var pretty = $"Schedule selected: {string.Join("; ", summaryParts)}.";
                text = pretty.Trim();

                _logger.LogInformation("Schedule summary generated: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    set, reh, showStart, showEnd, pack);
            }

            // —— rate-limit safe send ——
            threadId = _chat.EnsureThreadId(HttpContext.Session);
            var sem = GetThreadLock(threadId);
            await sem.WaitAsync(ct);
            try
            {
                var result = await WithChatOperationTimeoutAsync(
                    "Send.SendAsync",
                    opCt => WithRateLimitRetry(
                        () => _chat.SendAsync(HttpContext.Session, text, opCt),
                        opCt),
                    ct);

                SyncProjectorPromptMarkers(result.Messages, threadId);
                RedactPricesForUiInPlace(result.Messages);
                SetScheduleTimesInViewData();
                ViewData["ProgressStep"] = DetermineProgressStep(result.Messages);
                return View("Index", result.Messages);
            }
            finally { sem.Release(); }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var (_, messages) = _chat.GetTranscript(threadId);
            RedactPricesForUiInPlace(messages);
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(messages);
            return View("Index", messages);
        }
    }

    /// <summary>
    /// Second phase of two-phase contact save. Called by frontend when SendPartial returns continueNeeded.
    /// Runs the agent and PostProcess, returns full transcript as HTML.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContinuePartial(CancellationToken ct)
    {
        try
        {
            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var (_, beforeMessages) = _chat.GetTranscript(threadId);
            var assistantCountBeforeContinue = CountAssistantMessages(beforeMessages);
            var sem = GetThreadLock(threadId);
            await sem.WaitAsync(ct);
            IEnumerable<DisplayMessage> messages;
            try
            {
                messages = await WithChatOperationTimeoutAsync(
                    "ContinuePartial.SendAsyncContinue",
                    opCt => WithRateLimitRetry(() => _chat.SendAsyncContinue(HttpContext.Session, opCt), opCt),
                    ct);
            }
            finally { sem.Release(); }

            var msgList = messages.ToList();
            SyncProjectorPromptMarkers(msgList, threadId);
            if (!HasAssistantDelta(assistantCountBeforeContinue, msgList))
            {
                _logger.LogWarning("[CHAT_FLOW] ContinuePartial completed without assistant delta for thread {ThreadId}. Skipping fallback injection to avoid error-loop message.", threadId);
            }
            EnsureImmediateQuoteReviewPromptAfterQuoteSuccess(msgList);
            RedactPricesForUiInPlace(msgList);
            SetScheduleTimesInViewData();
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);

            var html = await RenderPartialViewToStringAsync("_Messages", msgList);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContinuePartial failed");
            try
            {
                var threadId = _chat.EnsureThreadId(HttpContext.Session);
                var (_, messages) = _chat.GetTranscript(threadId);
                var msgList = messages.ToList();
                await AddAssistantMessageAndPersistAsync(msgList, BuildTransientFailureFallbackMessage(), ct);
                RedactPricesForUiInPlace(msgList);
                SetScheduleTimesInViewData();
                ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
                ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
                ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                var html = await RenderPartialViewToStringAsync("_Messages", msgList);
                return Content(html, "text/html");
            }
            catch
            {
                Response.StatusCode = 500;
                return Content("Could not complete. Please try again.");
            }
        }
    }

    private static bool ContainsConfusingQuoteLanguage(IReadOnlyList<DisplayMessage> messages)
    {
        if (messages == null || messages.Count == 0) return false;

        // Check the last assistant message for confusing quote language
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msg = messages[i];
            if (!string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var text = (msg.FullText ?? string.Join("\n\n", msg.Parts ?? Enumerable.Empty<string>())).ToLowerInvariant();

            // Normalise some punctuation so phrase detection is more robust
            text = text
                .Replace("’", "'")
                .Replace("“", "\"")
                .Replace("”", "\"");

            // Pre-emptive "I'll create your booking / generate quote PDF" message (remove so we replace with actual success message)
            if (text.Contains("create your booking right away") ||
                text.Contains("i'll create your booking") ||
                (text.Contains("automatically generate") && (text.Contains("quote") || text.Contains("pdf"))) ||
                text.Contains("download link here once") ||
                (text.Contains("download link") && text.Contains("once it's ready")) ||
                text.Contains("could you please try sending your message again") ||
                text.Contains("our team will follow up with you shortly to resolve this") ||
                text.Contains("our team will follow up with you to help complete your booking"))
            {
                return true;
            }

            if (text.Contains("technical hiccup") ||
                text.Contains("slight hitch") ||
                text.Contains("unable to generate") ||
                text.Contains("can't generate") ||
                text.Contains("cannot generate") ||
                text.Contains("having trouble") ||
                text.Contains("refine this manually") ||
                text.Contains("our team will follow up") ||
                text.Contains("there's an issue") ||
                text.Contains("there is an issue") ||
                text.Contains("there was an issue") ||
                text.Contains("there was an issue generating your quote") ||
                text.Contains("issue with generating") ||
                text.Contains("issue generating") ||
                text.Contains("issue generating your quote") ||
                text.Contains("problem generating") ||
                text.Contains("experiencing an issue") ||
                text.Contains("experiencing a problem") ||
                text.Contains("quote generation process") ||
                text.Contains("issue with the quote") ||
                text.Contains("couldn't generate") ||
                text.Contains("could not generate") ||
                text.Contains("couldn't create the quote") ||
                text.Contains("couldn't create the quote automatically") ||
                text.Contains("team will follow up with you") ||
                text.Contains("follow up with you soon") ||
                text.Contains("failed to generate") ||
                text.Contains("notify our team") ||
                text.Contains("sales team immediately"))
            {
                return true;
            }

            // Only check the most recent assistant message
            break;
        }

        return false;
    }

    private static bool WasLastAssistantASummaryAsk(IReadOnlyList<DisplayMessage> messages)
    {
        if (messages == null || messages.Count < 2) return false;

        // Only consider the last assistant message (immediately before the user's reply).
        // This prevents "yes" to an unrelated question (e.g. laptops) from being treated as quote consent
        // when an older message in the thread had asked to create the quote.
        var lastAssistant = messages.LastOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        if (lastAssistant == null) return false;

        var raw = string.Join("\n\n", lastAssistant.Parts ?? Enumerable.Empty<string>());
        var t = Normalize(raw);
        return LooksLikeSummaryAsk(t);

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
            || t.Contains("here's your summary")
            || t.Contains("here's what i have so far")
            || t.Contains("here's a summary")
            || t.Contains("let me summarise") || t.Contains("let me summarize")  // Support both AUS and US spelling
            || t.Contains("does everything look correct")
            || t.Contains("does this look correct")
            || t.Contains("please confirm")
            || t.Contains("can you confirm")
            || t.Contains("could you confirm")
            || t.Contains("before i create your quote")
            || t.Contains("before creating your quote")
            || t.Contains("before generating your quote")
            || t.Contains("before proceeding to your quote")
            || t.Contains("before i proceed")
            || t.Contains("before i generate your quote")
            || t.Contains("before i proceed to generate the quote")
            || t.Contains("are there any other details")
            || t.Contains("anything else you'd like to add")
            || t.Contains("would you like to add anything else")
            || t.Contains("is there anything else")
            || t.Contains("anything else to add")
            || t.Contains("ready to create the quote")
            || t.Contains("shall i create the quote")
            || t.Contains("shall i generate the quote")
            || t.Contains("shall we move ahead")
            || t.Contains("shall we proceed")
            || t.Contains("would you like me to create")
            || t.Contains("would you like me to generate")
            || t.Contains("finalise the quote") || t.Contains("finalize the quote")  // Support both AUS and US spelling
            || t.Contains("finalised equipment") || t.Contains("finalized equipment")  // Support both AUS and US spelling
            || t.Contains("equipment lineup")
            || t.Contains("equipment selection")
            || t.Contains("here's your finalised") || t.Contains("here's your finalized")  // Support both AUS and US spelling
            || t.Contains("here's the equipment")
            // New summary format from smart equipment recommendation
            || t.Contains("quote summary")
            || t.Contains("recommended equipment")
            || t.Contains("equipment looks correct")
            || t.Contains("i can create your quote")
            || t.Contains("create your quote now")
            || t.Contains("estimated total");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult StartConversationReplay(string scenario, CancellationToken ct)
    {
        if (!_devOptions.Enabled)
        {
            return BadRequest("Dev mode is not enabled");
        }

        try
        {
            IEnumerable<string> messages;
            string scenarioName;
            
            // Select scenario based on user selection or default to random
            switch (scenario?.ToLowerInvariant())
            {
                // === BUG REPRODUCTION SCENARIOS ===
                case "ai_stops_responding":
                    messages = _replayService.GenerateAiStopsRespondingBugConversation();
                    scenarioName = "🐛 AI Stops Responding Bug";
                    break;
                    
                case "time_picker_validation":
                    messages = _replayService.GenerateTimePickerValidationConversation();
                    scenarioName = "🐛 Time Picker Validation";
                    break;
                    
                case "time_picker_display_order":
                    messages = _replayService.GenerateTimePickerDisplayOrderBugConversation();
                    scenarioName = "🐛 Time Picker Display Order Bug";
                    break;
                    
                case "skip_contact_info":
                    messages = _replayService.GenerateSkipContactInfoConversation();
                    scenarioName = "🐛 Skip Contact Info";
                    break;
                    
                case "quote_generation_issues":
                    messages = _replayService.GenerateQuoteGenerationIssuesConversation();
                    scenarioName = "🐛 Quote Generation Issues";
                    break;

                case "thrive_zoom_no_assumption":
                    messages = _replayService.GenerateThriveZoomNoAssumptionConversation();
                    scenarioName = "🐛 Thrive Zoom/Teams Ask-Only";
                    break;

                case "schedule_recognition_fix":
                    messages = _replayService.GenerateScheduleRecognitionFixConversation();
                    scenarioName = "🐛 Schedule Recognition Fix";
                    break;

                case "westin_brisbane_room_packages":
                    messages = _replayService.GenerateWestinBrisbaneRoomPackagesConversation();
                    scenarioName = "🛠️ Westin Brisbane Room Packages";
                    break;

                case "quote_modification":
                    messages = _replayService.GenerateQuoteModificationConversation();
                    scenarioName = "📝 Quote Modification";
                    break;

                case "image_gallery_preview":
                    messages = _replayService.GenerateImageGalleryPreviewConversation();
                    scenarioName = "🖼️ Image Gallery Preview";
                    break;

                case "ai_delay_memory_fix":
                    messages = _replayService.GenerateAiDelayMemoryFixConversation();
                    scenarioName = "🛡️ AI Delay & Memory Fix";
                    break;

                case "speaker_style_order":
                    messages = _replayService.GenerateSpeakerStyleOrderConversation();
                    scenarioName = "🔊 Speaker Style Order";
                    break;

                case "another_quote_from_same_conversation":
                    messages = _replayService.GenerateAnotherQuoteFromSameConversation();
                    scenarioName = "📋 Another Quote From Same Conversation";
                    break;

                // === THRIVE BOARDROOM SCENARIOS ===
                case "thrive_simple_meeting":
                    messages = _replayService.GenerateThriveSimpleMeetingConversation();
                    scenarioName = "🏢 Thrive Simple Meeting";
                    break;
                case "thrive_too_many_attendees":
                    messages = _replayService.GenerateThriveTooManyAttendeesConversation();
                    scenarioName = "🏢 Thrive Too Many Attendees";
                    break;
                case "thrive_presentations_full_av":
                    messages = _replayService.GenerateThrivePresentationsFullAvConversation();
                    scenarioName = "🏢 Thrive Presentations Full AV";
                    break;
                case "thrive_video_conference":
                    messages = _replayService.GenerateThriveVideoConferenceConversation();
                    scenarioName = "🏢 Thrive Video Conference";
                    break;
                case "thrive_own_laptop_adaptor":
                    messages = _replayService.GenerateThriveOwnLaptopAdaptorConversation();
                    scenarioName = "🏢 Thrive Own Laptop Adaptor";
                    break;
                case "thrive_equipment_modification":
                    messages = _replayService.GenerateThriveEquipmentModificationConversation();
                    scenarioName = "🏢 Thrive Equipment Modification";
                    break;
                case "thrive_room_alias_bare":
                    messages = _replayService.GenerateThriveRoomAliasBareConversation();
                    scenarioName = "🏢 Thrive Room Alias Bare";
                    break;

                // === STANDARD TEST SCENARIOS ===
                case "small_event":
                    messages = _replayService.GenerateSmallEventConversation();
                    scenarioName = "Small Event (15-25 people)";
                    break;
                    
                case "large_conference":
                    messages = _replayService.GenerateLargeConferenceConversation();
                    scenarioName = "Large Conference (300-500 people)";
                    break;
                    
                case "social_event":
                    messages = _replayService.GenerateSocialEventConversation();
                    scenarioName = "Social/Gala Event";
                    break;
                    
                case "random":
                default:
                    // Random selection from standard scenarios (includes quote modification fix verification)
                    var random = new Random();
                    var randomScenario = random.Next(11);
                    (messages, scenarioName) = randomScenario switch
                    {
                        0 => (_replayService.GenerateSmallEventConversation(), "Small Event"),
                        1 => (_replayService.GenerateLargeConferenceConversation(), "Large Conference"),
                        2 => (_replayService.GenerateHackathonConversation(), "Hackathon Event"),
                        3 => (_replayService.GenerateSocialEventConversation(), "Social/Gala Event"),
                        4 => (_replayService.GenerateMinimalInfoConversation(), "Minimal Info User"),
                        5 => (_replayService.GenerateDetailedConversation(), "Detailed/Verbose User"),
                        6 => (_replayService.GenerateUrgentBookingConversation(), "Urgent/Rush Booking"),
                        7 => (_replayService.GenerateMultiDayEventConversation(), "Multi-Day Event"),
                        8 => (_replayService.GenerateTrainingWorkshopConversation(), "Training Workshop"),
                        9 => (_replayService.GenerateQuoteModificationConversation(), "Quote Modification"),
                        _ => (_replayService.GenerateTestConversation(), "Standard Random Event")
                    };
                    break;
            }

            var messageList = messages.ToList();
            
            _logger.LogInformation("Starting test replay: {Scenario} with {Count} messages", 
                scenarioName, messageList.Count);

            // Store the replay messages in session
            HttpContext.Session.SetString("Dev:ReplayMessages", System.Text.Json.JsonSerializer.Serialize(messageList));
            HttpContext.Session.SetInt32("Dev:CurrentMessageIndex", 0);
            HttpContext.Session.SetString("Dev:ScenarioName", scenarioName);

            return Json(new { 
                success = true, 
                messageCount = messageList.Count,
                scenarioName = scenarioName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start conversation replay");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GetNextReplayMessage(CancellationToken ct)
    {
        if (!_devOptions.Enabled)
        {
            return BadRequest("Dev mode is not enabled");
        }

        try
        {
            var messagesJson = HttpContext.Session.GetString("Dev:ReplayMessages");
            var currentIndex = HttpContext.Session.GetInt32("Dev:CurrentMessageIndex") ?? 0;

            if (string.IsNullOrEmpty(messagesJson))
            {
                return Json(new { success = false, error = "No replay session active" });
            }

            var messages = System.Text.Json.JsonSerializer.Deserialize<List<string>>(messagesJson);
            if (messages == null || currentIndex >= messages.Count)
            {
                return Json(new { success = false, error = "Replay completed" });
            }

            var nextMessage = messages[currentIndex];
            HttpContext.Session.SetInt32("Dev:CurrentMessageIndex", currentIndex + 1);

            return Json(new
            {
                success = true,
                message = nextMessage,
                remaining = messages.Count - currentIndex - 1,
                completed = currentIndex + 1 >= messages.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next replay message");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartAutoTest(string? scenario, CancellationToken ct)
    {
        if (!_devOptions.Enabled || !_autoTestOptions.Enabled)
            return BadRequest("Dev mode or AutoTest is not enabled");

        try
        {
            var userKey = GetUserKey();
            await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);

            ClearBookingAndQuoteDraftState();
            HttpContext.Session.Remove("AgentThreadId");
            HttpContext.Session.Remove("Draft:ContactId");
            HttpContext.Session.Remove("Draft:CustomerCode");
            HttpContext.Session.Remove("Ack:Budget");
            HttpContext.Session.Remove("Ack:Attendees");
            HttpContext.Session.Remove("Ack:SetupStyle");
            HttpContext.Session.Remove("Ack:Venue");
            HttpContext.Session.Remove("Ack:SpecialRequests");
            HttpContext.Session.Remove("Ack:Dates");

            var newThreadId = _chat.EnsureThreadId(HttpContext.Session);
            await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);
            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var persona = _autoTestService.InitializePersona(scenario);
            HttpContext.Session.SetString("Dev:AutoTestPersona", JsonSerializer.Serialize(persona));
            HttpContext.Session.SetInt32("Dev:AutoTestStepNumber", 0);
            HttpContext.Session.SetInt32("Dev:AutoTestRunning", 1);

            return Json(new
            {
                success = true,
                personaName = persona.FullName,
                scenarioDescription = persona.ScenarioDescription
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartAutoTest failed");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAutoTestStep(CancellationToken ct)
    {
        if (!_devOptions.Enabled || !_autoTestOptions.Enabled)
            return BadRequest("Dev mode or AutoTest is not enabled");

        if (HttpContext.Session.GetInt32("Dev:AutoTestRunning") != 1)
            return Json(new { success = true, isComplete = true, completionReason = "Stopped" });

        var personaJson = HttpContext.Session.GetString("Dev:AutoTestPersona");
        var stepNumber = HttpContext.Session.GetInt32("Dev:AutoTestStepNumber") ?? 0;

        if (string.IsNullOrEmpty(personaJson))
            return Json(new { success = false, error = "No auto-test session. Start Auto Test first." });

        var persona = JsonSerializer.Deserialize<AutoTestPersona>(personaJson);
        if (persona == null)
            return Json(new { success = false, error = "Invalid persona in session." });

        if (stepNumber >= _autoTestOptions.MaxMessages)
            return Json(new { success = true, isComplete = true, completionReason = "Max steps reached", stepNumber, html = "" });

        try
        {
            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var (_, transcript) = _chat.GetTranscript(threadId);

            var generatedMessage = await _autoTestService.GenerateNextMessageAsync(transcript, persona, ct);
            if (string.IsNullOrWhiteSpace(generatedMessage))
                return Json(new { success = false, error = "Failed to generate customer message." });

            var text = generatedMessage.Trim();
            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }

            var isInProjectorContextAutoTest = HttpContext.Session.GetString(ProjectorPromptShownKey) == "1";
            if (TryCaptureProjectorAreaSelections(text, out var projectorAreas, inProjectorContext: isInProjectorContextAutoTest))
            {
                PersistProjectorAreaSelection(threadId, projectorAreas);
            }
            var gotSchedule = false;
            if (!text.Trim().StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase) &&
                TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack, out var eventDate))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack, eventDate);
                var summaryParts = new List<string> { $"Setup {Pretty12(set)}", $"Rehearsal {Pretty12(reh)}" };
                if (showStart.HasValue) summaryParts.Add($"Start {Pretty12(showStart)}");
                if (showEnd.HasValue) summaryParts.Add($"End {Pretty12(showEnd)}");
                summaryParts.Add($"Pack Up {Pretty12(pack)}");
                text = $"Schedule selected: {string.Join("; ", summaryParts)}.";
                gotSchedule = true;
            }

            var scheduleAlreadySubmitted = !string.IsNullOrWhiteSpace(
                HttpContext.Session.GetString("Draft:SetupTime"));
            if (!gotSchedule
                && !scheduleAlreadySubmitted
                && !text.StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase)
                && LooksLikeScheduleConfirmation(text, transcript))
            {
                text = GenerateAutoTestScheduleFallback(persona);
                _logger.LogInformation("Auto-test schedule fallback applied: {Schedule}", text);
            }

            var sem = GetThreadLock(threadId);
            await sem.WaitAsync(ct);
            SendAsyncResult sendResult;
            try
            {
                sendResult = await WithChatOperationTimeoutAsync(
                    "RunAutoTestStep.SendAsync",
                    opCt => WithRateLimitRetry(() => _chat.SendAsync(HttpContext.Session, text, opCt), opCt),
                    ct);
            }
            finally
            {
                sem.Release();
            }

            IEnumerable<DisplayMessage> messages = sendResult.Messages;
            if (sendResult.ContinueNeeded)
            {
                messages = await WithChatOperationTimeoutAsync(
                    "RunAutoTestStep.SendAsyncContinue",
                    opCt => WithRateLimitRetry(() => _chat.SendAsyncContinue(HttpContext.Session, opCt), opCt),
                    ct);
            }

            var msgList = messages is List<DisplayMessage> ml ? ml : messages.ToList();
            SyncProjectorPromptMarkers(msgList, threadId);
            EnsureImmediateQuoteReviewPromptAfterQuoteSuccess(msgList);
            RedactPricesForUiInPlace(msgList);
            SetScheduleTimesInViewData();
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);

            var html = await RenderPartialViewToStringAsync("_Messages", msgList);
            HttpContext.Session.SetInt32("Dev:AutoTestStepNumber", stepNumber + 1);

            var completionState = _autoTestService.DetectCompletionState(msgList, stepNumber + 1);

            return Json(new
            {
                success = true,
                html,
                generatedMessage,
                stepNumber = stepNumber + 1,
                completionState = new
                {
                    completionState.IsComplete,
                    completionState.CompletionReason,
                    completionState.NameProvided,
                    completionState.CustomerStatusProvided,
                    completionState.OrganizationProvided,
                    completionState.ContactInfoProvided,
                    completionState.EventDetailsProvided,
                    completionState.EquipmentDiscussed,
                    completionState.ScheduleConfirmed,
                    completionState.QuoteSummaryShown,
                    completionState.QuoteGenerated,
                    completionState.QuoteConfirmed
                },
                isComplete = completionState.IsComplete,
                milestones = new
                {
                    nameProvided = completionState.NameProvided,
                    customerStatusProvided = completionState.CustomerStatusProvided,
                    organizationProvided = completionState.OrganizationProvided,
                    contactInfoProvided = completionState.ContactInfoProvided,
                    eventDetailsProvided = completionState.EventDetailsProvided,
                    equipmentDiscussed = completionState.EquipmentDiscussed,
                    scheduleConfirmed = completionState.ScheduleConfirmed,
                    quoteSummaryShown = completionState.QuoteSummaryShown,
                    quoteGenerated = completionState.QuoteGenerated,
                    quoteConfirmed = completionState.QuoteConfirmed
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunAutoTestStep failed");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult StopAutoTest()
    {
        HttpContext.Session.Remove("Dev:AutoTestPersona");
        HttpContext.Session.Remove("Dev:AutoTestStepNumber");
        HttpContext.Session.Remove("Dev:AutoTestRunning");
        return Json(new { success = true });
    }

    private static bool LooksLikeScheduleConfirmation(string text, IEnumerable<DisplayMessage> transcript)
    {
        var lower = text.ToLowerInvariant();
        var isAffirmative = lower.Contains("looks good") || lower.Contains("that works")
            || lower.Contains("sounds great") || lower.Contains("yes please")
            || lower.Contains("confirm") || lower.Contains("perfect")
            || (lower.StartsWith("yes") && lower.Length < 40);
        if (!isAffirmative) return false;

        var lastAssistant = transcript
            .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();
        if (lastAssistant == null) return false;

        var assistantText = lastAssistant.FullText
            ?? string.Join(" ", lastAssistant.Parts ?? new List<string>());
        return assistantText.Contains("timePicker", StringComparison.OrdinalIgnoreCase)
            || assistantText.Contains("multitime", StringComparison.OrdinalIgnoreCase)
            || assistantText.Contains("\"type\":\"multitime\"", StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateAutoTestScheduleFallback(AutoTestPersona persona)
    {
        var dateIso = persona.EventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"Choose schedule: date={dateIso}; setup=08:00; rehearsal=08:30; start=09:00; end=17:00; packup=19:00";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        try
        {
            // Clear quote completion state when resetting
            ClearBookingAndQuoteDraftState();
            HttpContext.Session.Remove("AgentThreadId");
            HttpContext.Session.Remove("Draft:ContactId");
            HttpContext.Session.Remove("Draft:CustomerCode");
            HttpContext.Session.Remove("Ack:Budget");
            HttpContext.Session.Remove("Ack:Attendees");
            HttpContext.Session.Remove("Ack:SetupStyle");
            HttpContext.Session.Remove("Ack:Venue");
            HttpContext.Session.Remove("Ack:SpecialRequests");
            HttpContext.Session.Remove("Ack:Dates");

            var newThreadId = _chat.EnsureThreadId(HttpContext.Session);

            var userKey = GetUserKey();
            await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(newThreadId);
            ViewData["QuoteComplete"] = false;
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(messages);
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
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            var (_, beforeMessages) = _chat.GetTranscript(threadId);
            var assistantCountBeforeTurn = CountAssistantMessages(beforeMessages);

            text = text.Trim();
            var rawUserTextForContact = text;   // keep original text for contact parsing

            if (LooksLikeNewQuoteIntent(text))
            {
                ClearBookingAndQuoteDraftState();
                _logger.LogInformation("[QUOTE FLOW] New quote intent detected in SendPartial, cleared booking/quote draft state. UserText={UserText}", text.Length > 100 ? text.Substring(0, 100) + "..." : text);
            }

            // Only unlock generate_quote when user explicitly confirms quote creation
            // in direct response to a summary/CTA ask.
            HttpContext.Session.Remove(GenerateQuoteFlag);
            var explicitCreateConsent = LooksLikeExplicitCreateQuoteConsent(text);
            var conversationalConsent = LooksLikeConsent(text);
            if (explicitCreateConsent || conversationalConsent)
            {
                var (_, preMessages) = _chat.GetTranscript(threadId);
                if (WasLastAssistantASummaryAsk(preMessages.ToList()))
                {
                    HttpContext.Session.SetString(GenerateQuoteFlag, "1");
                }
            }

            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }

            var isInProjectorContext = HttpContext.Session.GetString(ProjectorPromptShownKey) == "1";
            if (TryCaptureProjectorAreaSelections(text, out var projectorAreas, inProjectorContext: isInProjectorContext))
            {
                PersistProjectorAreaSelection(threadId, projectorAreas);
            }

            // SKIP schedule parsing for "Choose schedule:" messages from time picker - let AzureAgentChatService handle them
            if (!text.Trim().StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase) &&
                TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack, out var eventDate))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack, eventDate);

                // DEBUG: Log the raw parsed times before formatting
                _logger.LogInformation("SENDPARTIAL METHOD - RAW PARSED TIMES: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    set, reh, showStart, showEnd, pack);
                _logger.LogInformation("SENDPARTIAL METHOD - RAW TimeSpan values: Setup.TotalHours={SetupHours}, Rehearsal.TotalHours={RehearsalHours}, Start.TotalHours={StartHours}, End.TotalHours={EndHours}, PackUp.TotalHours={PackUpHours}",
                    set.TotalHours, reh.TotalHours, showStart?.TotalHours, showEnd?.TotalHours, pack.TotalHours);

                // Build summary with all times - always include Start/End if they were provided
                _logger.LogInformation("FORMATTING TIMES: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    set, reh, showStart, showEnd, pack);

                var summaryParts = new List<string>
                {
                    $"Setup {Pretty12(set)}",
                    $"Rehearsal {Pretty12(reh)}"
                };

                if (showStart.HasValue)
                {
                    summaryParts.Add($"Start {Pretty12(showStart)}");
                }

                if (showEnd.HasValue)
                {
                    summaryParts.Add($"End {Pretty12(showEnd)}");
                }

                summaryParts.Add($"Pack Up {Pretty12(pack)}");

                _logger.LogInformation("FORMATTED TIMES: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    Pretty12(set), Pretty12(reh), showStart.HasValue ? Pretty12(showStart) : "null",
                    showEnd.HasValue ? Pretty12(showEnd) : "null", Pretty12(pack));

                var pretty = $"Schedule selected: {string.Join("; ", summaryParts)}.";
                text = pretty.Trim();

                _logger.LogInformation("Schedule summary generated: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}",
                    set, reh, showStart, showEnd, pack);
            }

            // —— safe send ——
            threadId = _chat.EnsureThreadId(HttpContext.Session);
            var sem = GetThreadLock(threadId);
            SendAsyncResult sendResult;
            var sendFailed = false;
            await sem.WaitAsync(ct);
            
            var sendStart = DateTime.UtcNow;
            try
            {
                _logger.LogInformation("[CHAT_FLOW] Starting SendAsync for text: {Text}", text.Length > 50 ? text.Substring(0, 50) + "..." : text);
                sendResult = await WithChatOperationTimeoutAsync(
                    "SendPartial.SendAsync",
                    opCt => WithRateLimitRetry(
                        () => _chat.SendAsync(HttpContext.Session, text, opCt),
                        opCt),
                    ct);
                _logger.LogInformation("[CHAT_FLOW] SendAsync completed in {Duration}s", (DateTime.UtcNow - sendStart).TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CHAT_FLOW] SendAsync failed after {Duration}s", (DateTime.UtcNow - sendStart).TotalSeconds);
                sendFailed = true;
                // Fallback: Get whatever transcript we have
                var (_, currentTranscript) = _chat.GetTranscript(threadId);
                sendResult = new SendAsyncResult(currentTranscript, false);
            }
            finally { sem.Release(); }

            // Two-phase contact save: return partial immediately so frontend can show "One moment please", then call ContinuePartial
            if (sendResult.ContinueNeeded)
            {
                var partialList = sendResult.Messages.ToList();
                SyncProjectorPromptMarkers(partialList, threadId);
                EnsureImmediateQuoteReviewPromptAfterQuoteSuccess(partialList);
                RedactPricesForUiInPlace(partialList);
                SetScheduleTimesInViewData();
                ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
                ViewData["ProgressStep"] = DetermineProgressStep(partialList);
                var html = await RenderPartialViewToStringAsync("_Messages", partialList);
                return Json(new { success = true, html, continueNeeded = true });
            }

            var msgList = sendResult.Messages is List<DisplayMessage> ml ? ml : sendResult.Messages.ToList();
            SyncProjectorPromptMarkers(msgList, threadId);
            if (!HasAssistantDelta(assistantCountBeforeTurn, msgList))
            {
                _logger.LogWarning("[CHAT_FLOW] SendPartial produced no assistant delta for thread {ThreadId} (sendFailed={SendFailed}). Appending fallback reply.", threadId, sendFailed);
                await AddAssistantMessageAndPersistAsync(msgList, BuildTransientFailureFallbackMessage(), ct);
            }
            EnsureImmediateQuoteReviewPromptAfterQuoteSuccess(msgList);

            // If the last AI message contains confusing quote language, we'll handle it in the consent logic

            // Check if user confirmed a booking summary
            // IMPORTANT: Trigger when EITHER:
            // 1. User's message looks like consent AND the assistant was asking for confirmation (summary ask), OR
            // 2. User explicitly said "yes create quote" (or similar) - bypass wasSummaryAsk because after AI responds,
            //    the "last assistant" is the AI's error message, not the original summary. Explicit phrase is unambiguous.
            var looksLikeConsent = LooksLikeConsent(text);
            var wasSummaryAsk = WasLastAssistantASummaryAsk(msgList);
            var isExplicitCreateQuote = LooksLikeExplicitCreateQuoteConsent(text);
            var isConsentToSummary = (looksLikeConsent && wasSummaryAsk) || isExplicitCreateQuote;

            // Detect quote acceptance.
            // IMPORTANT: "yes create quote" should generate the quote, not auto-accept it to Heavy Pencil.
            var isQuoteAccepted = looksLikeConsent
                && !LooksLikeExplicitCreateQuoteConsent(text)
                && WasLastAssistantAQuoteAcceptanceAsk(msgList)
                && IsQuoteComplete();

            // ========== COMPREHENSIVE LOGGING FOR QUOTE FLOW ==========
            var logQuoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";
            var logQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            var logBookingNo = HttpContext.Session.GetString("Draft:BookingNo");
            
            _logger.LogInformation(
                "[QUOTE FLOW] Decision point: " +
                "UserText='{Text}', " +
                "LooksLikeConsent={LooksLikeConsent}, " +
                "WasSummaryAsk={WasSummaryAsk}, " +
                "IsExplicitCreateQuote={IsExplicitCreateQuote}, " +
                "IsConsentToSummary={IsConsentToSummary}, " +
                "IsQuoteAccepted={IsQuoteAccepted}, " +
                "ExistingBookingNo={ExistingBookingNo}, " +
                "QuoteComplete={QuoteComplete}, " +
                "ExistingQuoteUrl={ExistingQuoteUrl}",
                text?.Substring(0, Math.Min(50, text?.Length ?? 0)),
                looksLikeConsent,
                wasSummaryAsk,
                isExplicitCreateQuote,
                isConsentToSummary,
                isQuoteAccepted,
                logBookingNo ?? "null",
                logQuoteComplete,
                logQuoteUrl ?? "null");

            // Handle quote acceptance (fallback text-based path; primary path is AcceptQuoteWithSignaturePartial)
            if (isQuoteAccepted)
            {
                var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");
                if (!string.IsNullOrWhiteSpace(bookingNo))
                {
                    var booking = await _bookingDb.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
                    if (booking != null)
                    {
                        booking.BookingProgressStatus = 2; // Heavy Pencil
                        await _bookingDb.SaveChangesAsync(ct);
                        _logger.LogInformation("Updated booking {BookingNo} status to Heavy Pencil (text fallback)", bookingNo);
                    }

                    try 
                    {
                        await _chat.SendInternalFollowupAsync(bookingNo, "Quote accepted by user (text consent) - status updated to Heavy Pencil.", ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send internal followup for booking {BookingNo}", bookingNo);
                    }

                    var bookingDate = booking?.dDate ?? booking?.SDate;
                    var bookingDateStr = bookingDate?.ToString("dd MMMM yyyy") ?? "the scheduled date";

                    string fallbackAmountStr;
                    if (booking?.price_quoted.HasValue == true && booking.price_quoted.Value > 0)
                    {
                        fallbackAmountStr = booking.price_quoted.Value.ToString("C2", CultureInfo.GetCultureInfo("en-AU")) + " inc GST";
                    }
                    else
                    {
                        fallbackAmountStr = TryExtractAmountFromQuoteHtml(bookingNo) ?? "the quoted amount";
                    }

                    var confirmText = $"Your quote {bookingNo}, for a booking on {bookingDateStr} is confirmed. " +
                                      $"Kindly attend to the booking payment of {fallbackAmountStr}, payment details are noted on the Quote document. " +
                                      "One of our team members will be in contact with you shortly.";

                    var fallbackQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
                    var fallbackSignedActionsHtml = "";
                    if (!string.IsNullOrWhiteSpace(fallbackQuoteUrl))
                    {
                        confirmText += "\n\nYou can view or download your signed quote using the buttons below.";
                        fallbackSignedActionsHtml = BuildSignedQuoteActionsHtml(fallbackQuoteUrl, bookingNo);
                    }

                    // Redact BEFORE adding the confirmation so the amount isn't stripped
                    RedactPricesForUiInPlace(msgList);

                    var fallbackParts = new List<string> { confirmText };
                    if (!string.IsNullOrWhiteSpace(fallbackSignedActionsHtml))
                    {
                        fallbackParts.Add(fallbackSignedActionsHtml);
                    }

                    var confirmationMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = fallbackParts,
                        FullText = confirmText,
                        Html = $"<p>{System.Net.WebUtility.HtmlEncode(confirmText.Split("\n\n")[0])}</p>{fallbackSignedActionsHtml}"
                    };
                    await AddAssistantMessageAndPersistAsync(msgList, confirmationMessage, ct);
                    HttpContext.Session.SetString("Draft:QuoteAccepted", "1");
                    HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "0");
                    HttpContext.Session.SetString(QuoteReviewPromptShownKey, "1");

                    ViewData["QuoteAccepted"] = "1";
                    ViewData["ShowQuoteCta"] = "0";
                    SetScheduleTimesInViewData();
                    ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                    return PartialView("_Messages", msgList);
                }
            }

            // NOTE: Removed aggressive "isClearConsent" logic that was causing false triggers
            // Quote generation should ONLY happen when assistant explicitly asked for confirmation

            // if booking already exists, persist FULL transcript now
            var bookingNoAtStart = HttpContext.Session.GetString("Draft:BookingNo");
            if (!string.IsNullOrWhiteSpace(bookingNoAtStart))
            {
                await _bookingPersistence.SaveFullTranscriptToBooknoteAsync(bookingNoAtStart!, msgList, ct);
            }

            if (isConsentToSummary && LooksLikeExplicitCreateQuoteConsent(text))
            {
                var summaryKey = ComputeSummaryKey(msgList);
                var lastPersistedKey = HttpContext.Session.GetString("Draft:PersistedSummaryKey");
                var quoteCompleteNow = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";
                var quoteUrlNow = HttpContext.Session.GetString("Draft:QuoteUrl");
                var quoteSuccessRecently = msgList
                    .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                    .TakeLast(3)
                    .Any(m =>
                    {
                        var raw = (m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>())).ToLowerInvariant();
                        return raw.Contains("successfully generated your quote") && raw.Contains("booking");
                    });

                // If the tool loop already generated the quote in this request, don't run fallback generation again.
                if (quoteCompleteNow && !string.IsNullOrWhiteSpace(quoteUrlNow) && quoteSuccessRecently)
                {
                    _logger.LogInformation("[QUOTE FLOW] Skipping fallback quote generation because quote was already generated in this turn.");
                    if (!string.IsNullOrEmpty(summaryKey))
                        HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey);

                    RedactPricesForUiInPlace(msgList);
                    ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
                    ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
                    SetScheduleTimesInViewData();
                    ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                    return PartialView("_Messages", msgList);
                }

                // Skip duplicate processing if we already handled this exact summary
                if (isConsentToSummary && !string.IsNullOrEmpty(summaryKey) &&
                    string.Equals(summaryKey, lastPersistedKey, StringComparison.Ordinal))
                {
            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
                }

                // ========== VALIDATION: Check required contact fields before allowing quote generation ==========
                var contactName = HttpContext.Session.GetString("Draft:ContactName");
                var contactEmail = HttpContext.Session.GetString("Draft:ContactEmail");
                var contactPhone = HttpContext.Session.GetString("Draft:ContactPhone");
                var organisation = HttpContext.Session.GetString("Draft:Organisation");

                var missingContactFields = new List<string>();
                if (string.IsNullOrWhiteSpace(contactName))
                    missingContactFields.Add("customer name");
                if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
                    missingContactFields.Add("contact email or phone number");
                if (string.IsNullOrWhiteSpace(organisation))
                    missingContactFields.Add("organisation name");

                if (missingContactFields.Count > 0)
                {
                    _logger.LogWarning("Quote generation via consent blocked - missing contact fields: {Fields}", 
                        string.Join(", ", missingContactFields));
                    
                    // Add message asking for contact info
                    var requestInfoMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> { $"Before I can create your quote, I need a few quick details: {string.Join(", ", missingContactFields)}. Could you please provide this information?" },
                        FullText = $"Before I can create your quote, I need: {string.Join(", ", missingContactFields)}.",
                        Html = $"<p>Before I can create your quote, I need a few quick details: <strong>{string.Join(", ", missingContactFields)}</strong>. Could you please provide this information?</p>"
                    };
                    msgList.Add(requestInfoMessage);
                    
                    RedactPricesForUiInPlace(msgList);
                    ViewData["ShowQuoteCta"] = "0";
                    SetScheduleTimesInViewData();
                    ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                    return PartialView("_Messages", msgList);
                }

                // ========== VALIDATION: Schedule must be submitted before allowing quote generation ==========
                bool hasSchedule = msgList.Any(m => (m.Parts ?? Enumerable.Empty<string>()).Any(p => p != null && (
                    p.TrimStart().StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase) ||
                    p.TrimStart().StartsWith("I've selected this schedule:", StringComparison.OrdinalIgnoreCase))));
                if (!hasSchedule)
                {
                    var sessionStart = HttpContext.Session.GetString("Draft:StartTime");
                    var sessionDateConfirmed = HttpContext.Session.GetString("Draft:DateConfirmed");
                    if (!string.IsNullOrWhiteSpace(sessionStart) || sessionDateConfirmed == "1")
                        hasSchedule = true;
                }
                if (!hasSchedule)
                {
                    _logger.LogWarning("Quote generation via consent blocked - schedule not yet submitted");
                    var scheduleRequestMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> { "I need your event schedule before I can create the quote. Please confirm your setup, start, and end times using the time picker." },
                        FullText = "I need your event schedule before I can create the quote. Please confirm your setup, start, and end times using the time picker.",
                        Html = "<p>I need your event schedule before I can create the quote. Please confirm your setup, start, and end times using the time picker.</p>"
                    };
                    msgList.Add(scheduleRequestMessage);
                    RedactPricesForUiInPlace(msgList);
                    ViewData["ShowQuoteCta"] = "0";
                    SetScheduleTimesInViewData();
                    ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                    return PartialView("_Messages", msgList);
                }

                // ========== VALIDATION: Projector area for Westin Ballroom family when projection is required ==========
                var draftVenueName = HttpContext.Session.GetString("Draft:VenueName");
                var draftRoomName = HttpContext.Session.GetString("Draft:RoomName");
                var draftProjectorAreas = ParseProjectorAreas(HttpContext.Session.GetString("Draft:ProjectorAreas"));
                if (draftProjectorAreas.Count == 0)
                    draftProjectorAreas = ParseProjectorAreas(HttpContext.Session.GetString("Draft:ProjectorArea"));
                var summaryReqJson = HttpContext.Session.GetString("Draft:SummaryEquipmentRequests") ?? string.Empty;
                var selectedEquipmentJsonForArea = HttpContext.Session.GetString("Draft:SelectedEquipment") ?? string.Empty;

                var isWestinBallroomFamily =
                    !string.IsNullOrWhiteSpace(draftVenueName) &&
                    draftVenueName.Contains("westin", StringComparison.OrdinalIgnoreCase) &&
                    draftVenueName.Contains("brisbane", StringComparison.OrdinalIgnoreCase) &&
                    (
                        string.Equals(draftRoomName, "Westin Ballroom", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(draftRoomName, "Westin Ballroom 1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(draftRoomName, "Westin Ballroom 2", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(draftRoomName, "Ballroom", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(draftRoomName, "Ballroom 1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(draftRoomName, "Ballroom 2", StringComparison.OrdinalIgnoreCase)
                    );

                var projectionNeeded =
                    summaryReqJson.Contains("projector", StringComparison.OrdinalIgnoreCase) ||
                    summaryReqJson.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
                    summaryReqJson.Contains("display", StringComparison.OrdinalIgnoreCase) ||
                    selectedEquipmentJsonForArea.Contains("projector", StringComparison.OrdinalIgnoreCase) ||
                    selectedEquipmentJsonForArea.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
                    selectedEquipmentJsonForArea.Contains("display", StringComparison.OrdinalIgnoreCase);

                if (!projectionNeeded)
                {
                    // Prevent stale carry-over into booking facts when no projection is requested.
                    HttpContext.Session.Remove("Draft:ProjectorArea");
                    HttpContext.Session.Remove("Draft:ProjectorAreas");
                }

                var projectorCount = GetRequestedProjectorCount(summaryReqJson);
                if (projectorCount <= 0 && selectedEquipmentJsonForArea.Contains("projector", StringComparison.OrdinalIgnoreCase))
                    projectorCount = 1;
                // Westin Ballroom always requires 2 projector placement areas (any valid pair for the chosen room variant).
                var requiredAreaCount = projectionNeeded
                    ? (isWestinBallroomFamily ? 2 : (projectorCount > 1 ? Math.Min(projectorCount, 3) : 1))
                    : 0;
                var allowedAreas = GetAllowedProjectorAreasForRoom(draftRoomName);
                var validSelectedAreas = draftProjectorAreas
                    .Where(a => allowedAreas.Contains(a, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Trust session markers first — the prompt may have been emitted by the AI agent
                // and not yet reflected in msgList, so transcript scanning alone is unreliable.
                var promptAlreadyShown =
                    HttpContext.Session.GetString(ProjectorPromptShownKey) == "1" &&
                    HttpContext.Session.GetString(ProjectorPromptThreadIdKey) == threadId;
                var areaAlreadyCapturedInSession =
                    HttpContext.Session.GetString(ProjectorAreaCapturedKey) == "1" &&
                    HttpContext.Session.GetString(ProjectorAreaThreadIdKey) == threadId;

                if (!promptAlreadyShown && !areaAlreadyCapturedInSession)
                {
                    // Sync from transcript as fallback before deciding to clear
                    SyncProjectorPromptMarkers(msgList, threadId);
                    promptAlreadyShown = HttpContext.Session.GetString(ProjectorPromptShownKey) == "1" &&
                                        HttpContext.Session.GetString(ProjectorPromptThreadIdKey) == threadId;
                }

                // Only clear the stored areas if we have no evidence the user was ever asked
                if (!promptAlreadyShown && !areaAlreadyCapturedInSession)
                    validSelectedAreas.Clear();

                if (isWestinBallroomFamily && projectionNeeded && validSelectedAreas.Count < requiredAreaCount)
                {
                    _logger.LogWarning("Quote generation via consent blocked - missing projector area (Westin Ballroom family with projection)");
                    var allowedText = string.Join(", ", allowedAreas);
                    var areaRequestMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string>
                        {
                            requiredAreaCount == 1
                                ? $"Before I create the quote, please choose the **projector placement area** for Westin Ballroom.\n\nValid areas for this room: **{allowedText}**.\n\nReply with one area.\n\n![Westin Ballroom projector placement areas](/images/westin/westin-ballroom/floor-plan.png)"
                                : $"Before I create the quote, please choose **{requiredAreaCount} projector placement areas** for Westin Ballroom.\n\nValid areas for this room: **{allowedText}**.\n\nReply with any two areas (e.g. `{allowedAreas[0]} & {allowedAreas[1]}`).\n\n![Westin Ballroom projector placement areas](/images/westin/westin-ballroom/floor-plan.png)"
                        },
                        FullText = "Before I create the quote, please choose projector placement area A-F for Westin Ballroom.",
                        Html = "<p>Before I create the quote, please choose the <strong>projector placement area</strong> for Westin Ballroom (<strong>A-F</strong>).</p>"
                    };
                    // Persist to thread and set session marker so subsequent turns know the prompt was shown
                    HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
                    HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
                    await AddAssistantMessageAndPersistAsync(msgList, areaRequestMessage, ct);
                    RedactPricesForUiInPlace(msgList);
                    ViewData["ShowQuoteCta"] = "0";
                    SetScheduleTimesInViewData();
                    ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                    return PartialView("_Messages", msgList);
                }

                // ========== FORCE QUOTE GENERATION FOR ANY CONSENT ==========
                var existingBookingNo = HttpContext.Session.GetString("Draft:BookingNo");

                // If we have a booking number, try to generate quote immediately
                if (!string.IsNullOrWhiteSpace(existingBookingNo))
                {
                    _logger.LogInformation("User consented - attempting quote generation for existing booking {BookingNo}", existingBookingNo);
                    await GenerateQuoteForBookingAsync(existingBookingNo, msgList, ct);

                    // Mark as processed to avoid duplicates
                    if (isConsentToSummary)
                    {
                        HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey ?? string.Empty);
                    }
                }
                else
                {
                    // No existing booking, try to create one first
                    _logger.LogInformation("User consented but no existing booking - attempting to create booking first");

                    // Get equipment from session if available (stored by smart equipment recommendation tool)
                    var additionalFacts = new Dictionary<string, string>();
                    var selectedEquipment = HttpContext.Session.GetString("Draft:SelectedEquipment");
                    if (!string.IsNullOrWhiteSpace(selectedEquipment))
                    {
                        additionalFacts["selected_equipment"] = selectedEquipment;
                        _logger.LogInformation("Adding stored equipment to booking: {Equipment}", selectedEquipment);
                    }
                    var selectedLabor = HttpContext.Session.GetString("Draft:SelectedLabor");
                    if (!string.IsNullOrWhiteSpace(selectedLabor))
                    {
                        additionalFacts["selected_labor"] = selectedLabor;
                        var laborSummary = TryBuildLaborSummaryFromSelectedLabor(selectedLabor);
                        if (!string.IsNullOrWhiteSpace(laborSummary))
                        {
                            additionalFacts["labor_summary"] = laborSummary;
                        }
                        _logger.LogInformation("Adding stored labor selection to booking orchestration flow.");
                    }
                    var totalDayRate = HttpContext.Session.GetString("Draft:TotalDayRate");
                    if (!string.IsNullOrWhiteSpace(totalDayRate))
                    {
                        additionalFacts["price_quoted"] = totalDayRate;
                    }
                    var eventType = HttpContext.Session.GetString("Draft:EventType");
                    if (!string.IsNullOrWhiteSpace(eventType))
                    {
                        additionalFacts["event_type"] = eventType;
                    }
                    if (projectionNeeded)
                    {
                        var projectorAreaValue = HttpContext.Session.GetString("Draft:ProjectorArea");
                        if (!string.IsNullOrWhiteSpace(projectorAreaValue))
                        {
                            additionalFacts["projector_area"] = projectorAreaValue;
                        }
                        var projectorAreasValue = HttpContext.Session.GetString("Draft:ProjectorAreas");
                        if (!string.IsNullOrWhiteSpace(projectorAreasValue))
                        {
                            additionalFacts["projector_areas"] = projectorAreasValue;
                        }
                    }
                    var venueName = HttpContext.Session.GetString("Draft:VenueName");
                    if (!string.IsNullOrWhiteSpace(venueName))
                    {
                        additionalFacts["venue_name"] = venueName;
                    }
                    var roomName = HttpContext.Session.GetString("Draft:RoomName");
                    if (!string.IsNullOrWhiteSpace(roomName))
                    {
                        additionalFacts["venue_room"] = roomName;
                    }

                    var result = await _orchestration.ProcessConversationAsync(msgList, existingBookingNo, ct, additionalFacts);

                    if (result.Success && !string.IsNullOrWhiteSpace(result.BookingNo))
                    {
                        // Store results in session
                        HttpContext.Session.SetString("Draft:BookingNo", result.BookingNo!);
                        if (result.ContactId.HasValue)
                            HttpContext.Session.SetString("Draft:ContactId", result.ContactId.Value.ToString());
                        if (!string.IsNullOrWhiteSpace(result.CustomerCode))
                            HttpContext.Session.SetString("Draft:CustomerCode", result.CustomerCode!);

                        HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey ?? string.Empty);
                        SetConsent(HttpContext.Session, false);
                        HttpContext.Session.SetString("Draft:ShowedBookingNo", "1");

                        _logger.LogInformation("Booking created successfully: {BookingNo}", result.BookingNo);

                        // Now generate the quote
                        await GenerateQuoteForBookingAsync(result.BookingNo!, msgList, ct);
                    }
                    else if (result.Errors.Any())
                    {
                        _logger.LogError("Booking creation failed: {Errors}", string.Join("; ", result.Errors));

                        // Even if booking creation failed, try to generate quote if we have a booking number
                        if (!string.IsNullOrWhiteSpace(existingBookingNo))
                        {
                            _logger.LogInformation("Booking creation failed, but attempting quote generation for existing booking {BookingNo}", existingBookingNo);
                            await GenerateQuoteForBookingAsync(existingBookingNo, msgList, ct);
                        }
                        else
                        {
                            // Add error message to conversation
                            var errorMessage = new DisplayMessage
                            {
                                Role = "assistant",
                                Timestamp = DateTimeOffset.UtcNow,
                                Parts = new List<string> {
                                    "Our team will follow up with you to help complete your booking."
                                },
                                FullText = "Our team will follow up with you to help complete your booking.",
                                Html = "<p>Our team will follow up with you to help complete your booking.</p>"
                            };
                            msgList = msgList.Concat(new[] { errorMessage }).ToList();
                        }
                    }
                }
            }

            var finalBookingNo = HttpContext.Session.GetString("Draft:BookingNo");
            if (!string.IsNullOrWhiteSpace(finalBookingNo))
            {
                try
                {
                    await _bookingPersistence.SyncBookingFromSessionAsync(finalBookingNo, HttpContext.Session, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PROACTIVE] Failed to sync session data to booking {BookingNo}", finalBookingNo);
                }

                try
                {
                    await _bookingPersistence.SaveFullTranscriptToBooknoteAsync(finalBookingNo, msgList, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save final transcript for booking {BookingNo}", finalBookingNo);
                }
            }

            try
            {
                await EnsureAcceptedQuoteStaysHeavyPencilAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed ensuring Heavy Pencil status after message processing");
            }

            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            
            // Log final message count for debugging
            _logger.LogInformation("Returning {Count} messages to view. Last message role: {LastRole}", 
                msgList.Count, 
                msgList.LastOrDefault()?.Role ?? "none");
            
            return PartialView("_Messages", msgList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendPartial for text: {Text}", text?.Substring(0, Math.Min(100, text?.Length ?? 0)));
            
            // Even on error, try to return the current transcript so user's message is preserved
            try
            {
                var threadId = _chat.EnsureThreadId(HttpContext.Session);
                var (_, messages) = _chat.GetTranscript(threadId);
                var msgList = messages.ToList();
                
                // Only add error message if we haven't already added a success/quote message in this request
                // and if the last message isn't already a quote/success message
                var hasQuote = msgList.Any(m => m.Role == "assistant" && (m.FullText?.Contains("successfully generated your quote") == true || m.Html?.Contains("btn-primary") == true));
                
                if (!hasQuote)
                {
                    // Add an error message to the conversation
                    var errorMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> { "Could you please try sending your message again?" },
                        FullText = "Could you please try sending your message again?",
                        Html = "<p>Could you please try sending your message again?</p>"
                    };
                    msgList.Add(errorMessage);
                }
                else
                {
                    _logger.LogInformation("Skipping redundant error message in SendPartial catch because quote was already generated");
                }
                
                SetScheduleTimesInViewData();
                ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                return PartialView("_Messages", msgList);
            }
            catch
            {
                // Last resort fallback
                Response.StatusCode = 500;
                return Content("Something went wrong. Please refresh the page and try again.");
            }
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
            || t.Contains("here's your summary")
            || t.Contains("let me summarise") || t.Contains("let me summarize")  // Support both AUS and US spelling
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

    private async Task GenerateQuoteForBookingAsync(string bookingNo, List<DisplayMessage> msgList, CancellationToken ct)
    {
        try
        {
            var venueOrRoomChanged = await _bookingPersistence.SyncVenueAndRoomForBookingAsync(bookingNo, HttpContext.Session, ct);
            if (venueOrRoomChanged)
            {
                HttpContext.Session.Remove("Draft:QuoteUrl");
                HttpContext.Session.Remove("Draft:QuoteComplete");
                HttpContext.Session.Remove("Draft:QuoteTimestamp");
            }

            try
            {
                var contactOrOrgChanged = await _bookingPersistence.SyncContactAndOrganisationForBookingAsync(bookingNo, HttpContext.Session, ct);
                if (contactOrOrgChanged)
                {
                    HttpContext.Session.Remove("Draft:QuoteUrl");
                    HttpContext.Session.Remove("Draft:QuoteComplete");
                    HttpContext.Session.Remove("Draft:QuoteTimestamp");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync session contact/organisation to booking {BookingNo} before quote generation", bookingNo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync session venue/room to booking {BookingNo} before quote generation", bookingNo);
        }

        // ========== IDEMPOTENCY CHECK - Return existing quote if already generated ==========
        var existingQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
        var quoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";
        
        if (quoteComplete && !string.IsNullOrWhiteSpace(existingQuoteUrl))
        {
            _logger.LogInformation("Quote already exists for booking {BookingNo}, returning existing quote: {QuoteUrl}", bookingNo, existingQuoteUrl);
            
            // Check if any assistant message already announced quote success for this booking.
            var quoteMessageAlreadyExists = msgList
                .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                .Any(m =>
                {
                    var raw = m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
                    if (string.IsNullOrWhiteSpace(raw)) return false;
                    return raw.Contains("successfully generated your quote", StringComparison.OrdinalIgnoreCase)
                        && raw.Contains(bookingNo, StringComparison.OrdinalIgnoreCase);
                });
            if (quoteMessageAlreadyExists)
            {
                _logger.LogInformation("Quote link already in last message, skipping duplicate");
                MarkQuoteReviewPromptPending();
                return;
            }
            
            // Add success message with existing quote
            var existingQuoteMessage = BuildQuoteReadyMessage(bookingNo, existingQuoteUrl);
            await AddAssistantMessageAndPersistAsync(msgList, existingQuoteMessage, ct);
            AppendQuoteReviewPromptImmediately(msgList);
            return;
        }
        
        // Also check for existing quote files on disk (in case session was lost)
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var quotesDir = Path.Combine(webRoot, "files", "quotes");
        if (Directory.Exists(quotesDir))
        {
            var existingQuoteFiles = Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                .ToList();
            
            if (existingQuoteFiles.Any())
            {
                var mostRecentQuote = existingQuoteFiles.First();
                var quoteAge = DateTime.UtcNow - System.IO.File.GetCreationTimeUtc(mostRecentQuote);
                
                // If quote is less than 1 hour old, reuse it
                if (quoteAge.TotalHours < 1)
                {
                    var reusedQuoteUrl = $"/files/quotes/{Path.GetFileName(mostRecentQuote)}";
                    
                    // Update session with existing quote
                    HttpContext.Session.SetString("Draft:QuoteUrl", reusedQuoteUrl);
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1");
                    HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
                    
                    _logger.LogInformation("Found existing quote file for booking {BookingNo} (age: {Age}), reusing: {QuoteUrl}", 
                        bookingNo, quoteAge, reusedQuoteUrl);
                    
                    // Add success message with existing quote
                    var reusedQuoteMessage = BuildQuoteReadyMessage(bookingNo, reusedQuoteUrl);
                    await AddAssistantMessageAndPersistAsync(msgList, reusedQuoteMessage, ct);
                    AppendQuoteReviewPromptImmediately(msgList);
                    return;
                }
            }
        }
        
        // ========== SYNC SESSION EQUIPMENT TO BOOKING BEFORE QUOTE ==========
        var selectedEquipmentJson = HttpContext.Session.GetString("Draft:SelectedEquipment");
        if (!string.IsNullOrWhiteSpace(selectedEquipmentJson))
        {
            try
            {
                await _itemPersistence.UpsertSelectedEquipmentAsync(bookingNo, selectedEquipmentJson, ct);
                _logger.LogInformation("Synced session equipment to booking {BookingNo} before quote generation", bookingNo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync session equipment to booking {BookingNo} before quote; continuing with existing booking items", bookingNo);
            }
        }

        try
        {
            await _bookingPersistence.SyncLaborFromSessionForBookingAsync(bookingNo, HttpContext.Session, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync session labor to booking {BookingNo} before quote generation", bookingNo);
        }
        
        // ========== GENERATE QUOTE HTML ==========
        try
        {
            _logger.LogInformation("Starting HTML quote generation for booking {BookingNo}", bookingNo);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            try
            {
                var (quoteSuccess, quoteUrl, quoteError) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(bookingNo, linkedCts.Token, HttpContext.Session);
                _logger.LogInformation("HTML quote generation completed for booking {BookingNo}, success: {Success}", bookingNo, quoteSuccess);

                if (quoteSuccess && !string.IsNullOrWhiteSpace(quoteUrl))
                {
                    // ========== SYNC QUOTE STATE ==========
                    HttpContext.Session.SetString("Draft:QuoteUrl", quoteUrl);
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1"); // Mark quote as complete
                    HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
                    _logger.LogInformation("HTML quote generated for booking {BookingNo}: {QuoteUrl}. State synchronized.", bookingNo, quoteUrl);

                    // Check if the messages list contains confusing quote language - if so, remove them
                    var confusingMessages = msgList.Where(m => m.Role == "assistant" && ContainsConfusingQuoteLanguage(new[] { m })).ToList();
                    foreach (var cm in confusingMessages)
                    {
                        _logger.LogInformation("Removing confusing AI message: {Text}", cm.FullText);
                        msgList.Remove(cm);
                    }

                    // Add success message directly to the conversation (bypass AI agent)
                    var quoteMessage = BuildQuoteReadyMessage(bookingNo, quoteUrl);
                    await AddAssistantMessageAndPersistAsync(msgList, quoteMessage, ct);
                    AppendQuoteReviewPromptImmediately(msgList);
                    _logger.LogInformation("Success message added to msgList. Total messages: {Count}. Quote URL: {QuoteUrl}", msgList.Count, quoteUrl);
                }
                else
                {
                    _logger.LogWarning("HTML quote generation failed for booking {BookingNo}: {Error}", bookingNo, quoteError);

                    // Before we show an error, double-check whether a quote actually exists
                    var verifyQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
                    var verifyQuoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";

                    if (verifyQuoteComplete && !string.IsNullOrWhiteSpace(verifyQuoteUrl))
                    {
                        _logger.LogWarning("Quote generation reported failure for booking {BookingNo}, but quote state indicates success. Using existing quote instead of showing error.", bookingNo);

                        // Avoid duplicating an existing success message with the same URL
                        var lastAssistant = msgList.LastOrDefault(m => m.Role == "assistant");
                        if (lastAssistant == null || lastAssistant.FullText == null || !lastAssistant.FullText.Contains(verifyQuoteUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            await AddAssistantMessageAndPersistAsync(msgList, BuildQuoteReadyMessage(bookingNo, verifyQuoteUrl), ct);
                            AppendQuoteReviewPromptImmediately(msgList);
                        }
                    }
                    else
                    {
                        // As a secondary safety net, check for a recently-created quote file on disk
                        var verifyQuoteFile = Directory.Exists(quotesDir)
                            ? Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                                .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                                .FirstOrDefault()
                            : null;

                        if (!string.IsNullOrEmpty(verifyQuoteFile))
                        {
                            var recoveredUrl = $"/files/quotes/{Path.GetFileName(verifyQuoteFile)}";

                            HttpContext.Session.SetString("Draft:QuoteUrl", recoveredUrl);
                            HttpContext.Session.SetString("Draft:QuoteComplete", "1");
                            HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));

                            _logger.LogInformation("Recovered quote for booking {BookingNo} from disk despite reported failure: {QuoteUrl}", bookingNo, recoveredUrl);

                            await AddAssistantMessageAndPersistAsync(msgList, BuildQuoteReadyMessage(bookingNo, recoveredUrl), ct);
                            AppendQuoteReviewPromptImmediately(msgList);
                        }
                        else
                        {
                            // Never surface an internal quote-generation error to the user.
                            // Keep the experience forward-moving while backend retries/recovery can happen.
                            var failureMessage = new DisplayMessage
                            {
                                Role = "assistant",
                                Timestamp = DateTimeOffset.UtcNow,
                                Parts = new List<string> {
                                    $"Your quote for booking {bookingNo} is being finalized now. Please wait a moment and refresh, and I will share the live quote link as soon as it is ready."
                                },
                                FullText = $"Your quote for booking {bookingNo} is being finalized now. Please wait a moment and refresh, and I will share the live quote link as soon as it is ready.",
                                Html = $"<p>Your quote for booking <strong>{bookingNo}</strong> is being finalized now. Please wait a moment and refresh, and I will share the live quote link as soon as it is ready.</p>"
                            };
                            await AddAssistantMessageAndPersistAsync(msgList, failureMessage, ct);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("HTML quote generation timed out for booking {BookingNo}", bookingNo);

                // Check if quote was actually generated despite the timeout by looking for existing HTML files
                // Use different variable names to avoid scope conflicts
                var timeoutQuoteFile = Directory.Exists(quotesDir) 
                    ? Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                        .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                        .FirstOrDefault()
                    : null;

                if (!string.IsNullOrEmpty(timeoutQuoteFile))
                {
                    var timeoutQuoteUrl = $"/files/quotes/{Path.GetFileName(timeoutQuoteFile)}";
                    // Quote was generated successfully despite timeout - sync state
                    HttpContext.Session.SetString("Draft:QuoteUrl", timeoutQuoteUrl);
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1"); // Mark quote as complete
                    HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
                    _logger.LogInformation("HTML quote was actually generated despite timeout for booking {BookingNo}: {QuoteUrl}. State synchronized.", bookingNo, timeoutQuoteUrl);

                    await AddAssistantMessageAndPersistAsync(msgList, BuildQuoteReadyMessage(bookingNo, timeoutQuoteUrl), ct);
                    AppendQuoteReviewPromptImmediately(msgList);
                }
                else
                {
                    // Add timeout message
                    var timeoutMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> {
                            $"Your quote for booking {bookingNo} is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance."
                        },
                        FullText = $"Your quote for booking {bookingNo} is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance.",
                        Html = $"<p>Your quote for booking <strong>{bookingNo}</strong> is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance.</p>"
                    };
                    await AddAssistantMessageAndPersistAsync(msgList, timeoutMessage, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during quote generation for booking {BookingNo}", bookingNo);
        }

        // Final verification: if a quote exists, strip out any lingering error messages
        try
        {
            var finalQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            var finalQuoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";

            if (finalQuoteComplete && !string.IsNullOrWhiteSpace(finalQuoteUrl))
            {
                // Remove any assistant messages that look like quote-generation error apologies
                msgList.RemoveAll(m =>
                {
                    if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                        return false;

                    var text = (m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>())).ToLowerInvariant();

                    // Keep clear success messages
                    if (text.Contains("great news!") && text.Contains("generated your quote"))
                        return false;

                    return text.Contains("issue generating your quote") ||
                           text.Contains("issue with generating your quote") ||
                           (text.Contains("there was an issue") && text.Contains("your quote")) ||
                           (text.Contains("our team will follow up") && text.Contains("quote")) ||
                           text.Contains("couldn't create the quote") ||
                           text.Contains("couldn't create the quote automatically") ||
                           text.Contains("team will follow up with you") ||
                           text.Contains("follow up with you soon");
                });
            }
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx, "Failed to clean up confusing quote error messages after quote generation for booking {BookingNo}", bookingNo);
        }
    }

    private static bool LooksLikeConsent(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return false;

        var t = userText.Trim().ToLowerInvariant();
        
        // Explicit quote acceptance phrases
        string[] explicitAcceptance =
        {
            "yes proceed", "yes, proceed", "proceed with quote", "proceed with the quote",
            "accept quote", "accept the quote", "approve quote", "approve the quote",
            "looks good proceed", "looks good, proceed", "yes i want to proceed",
            "yes, i want to proceed", "go ahead with quote", "go ahead with the quote"
        };
        
        if (explicitAcceptance.Any(p => t.Contains(p)))
            return true;

        // Explicit quote/booking consent phrases (high confidence) - check these FIRST
        // These are unambiguous and should match regardless of message length
        string[] explicitConsent =
        {
            "generate the quote", "generate quote", "create the quote", "create quote",
            "prepare the quote", "make the quote", "send the quote",
            "yes create quote", "yes, create the quote", "yes create the quote",
            "make the booking", "create the booking", "place the booking",
            "give me quote", "give me the quote", "give me a quote",
            "get the quote", "get a quote", "get my quote",
            "can i get the quote", "can i get a quote", "can i get my quote",
            "can i have the quote", "can i have a quote", "can i have my quote",
            "i want the quote", "want the quote", "i need the quote",
            "finalise the booking", "finalise booking", "finalize the booking", "finalize booking",  // Support both AUS and US spelling
            "yes please create", "yes generate", "go ahead and create",
            "i'm ready for the quote", "ready for the quote", 
            "create my quote", "finalise the quote", "finalize the quote",  // Support both AUS and US spelling
            "just create", "just make", "proceed with the quote", "proceed with quote"
        };
        
        if (explicitConsent.Any(p => t.Contains(p)))
            return true;
        
        // For short messages only - check for simple affirmatives
        // This prevents false positives like "yes there will be speeches"
        if (t.Length > 50) return false;
        
        // Short affirmative responses (only if message is very short - likely just a "yes")
        string[] shortAffirmatives =
        {
            "yes", "yep", "yeah", "yes please", "please proceed", "proceed", 
            "go ahead", "confirmed", "confirm", "ok", "okay", "sure"
        };
        
        // For short affirmatives, the message should be VERY short (just the affirmative itself)
        // This prevents "yes there will be speeches" from matching
        if (t.Length <= 20 && shortAffirmatives.Any(p => t == p || t == p + "!" || t == p + "."))
            return true;
            
        return false;
    }

    /// <summary>
    /// Detects user intent to start a new quote/booking in the same thread (e.g. "another quote", "new quote for another event").
    /// Used to clear booking-scoped session state so the next quote creates a new booking instead of updating the previous one.
    /// </summary>
    private static bool LooksLikeNewQuoteIntent(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return false;
        var t = userText.Trim().ToLowerInvariant();

        string[] restartPhrases =
        {
            "new quote for another",
            "new quote for a different",
            "another quote for",
            "another quote for a",
            "can i ask for a new quote",
            "can i get a new quote",
            "i'd like another quote",
            "i'd like a new quote",
            "i want another quote",
            "i want a new quote",
            "need another quote",
            "need a new quote",
            "get another quote",
            "create another quote",
            "new quote for another event",
            "quote for another event",
            "quote for a different event",
            "new booking",
            "start over",
            "start a new quote",
            "start another quote"
        };

        return restartPhrases.Any(p => t.Contains(p));
    }

    private static bool WasLastAssistantAQuoteAcceptanceAsk(IReadOnlyList<DisplayMessage> messages)
    {
        if (messages == null || messages.Count < 1) return false;
        var lastAssistant = messages.LastOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        if (lastAssistant == null) return false;

        var text = (lastAssistant.FullText ?? string.Join("\n\n", lastAssistant.Parts ?? Enumerable.Empty<string>())).ToLowerInvariant();
        return text.Contains("would you like to proceed with this quote") ||
               text.Contains("would you like to confirm this quote") ||
               text.Contains("would you like me to confirm this quote") ||
               text.Contains("would you like to proceed and accept this quote") ||
               text.Contains("selecting yes will notify microhire") ||
               text.Contains("notify microhire to finalize");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuoteReviewedPartial(CancellationToken ct)
    {
        try
        {
            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            var (_, messages) = _chat.GetTranscript(threadId);
            var msgList = messages is List<DisplayMessage> list ? list : messages.ToList();

            var quoteAccepted = HttpContext.Session.GetString("Draft:QuoteAccepted") == "1";
            var awaitingPrompt = HttpContext.Session.GetString(AwaitingQuoteReviewPromptKey) == "1";
            var promptAlreadyShown = HttpContext.Session.GetString(QuoteReviewPromptShownKey) == "1";

            if (!quoteAccepted && awaitingPrompt && !promptAlreadyShown)
            {
                // Don't add duplicate prompt if last message already asks for confirmation
                if (!WasLastAssistantAQuoteAcceptanceAsk(msgList))
                {
                    await AddAssistantMessageAndPersistAsync(msgList, BuildPostQuoteReviewPromptMessage(), ct);
                    _logger.LogInformation("Quote review prompt appended after quote open event.");
                }
                HttpContext.Session.SetString(QuoteReviewPromptShownKey, "1");
                HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "0");
            }

            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed handling quote reviewed event");
            Response.StatusCode = 500;
            return Content("Unable to update quote review state.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptQuoteWithSignaturePartial(
        string fullName,
        string signDate,
        string titlePosition,
        string purchaseOrder,
        string signatureData,
        CancellationToken ct)
    {
        try
        {
            if (!IsQuoteComplete() || string.Equals(HttpContext.Session.GetString("Draft:QuoteAccepted"), "1"))
            {
                return await ReturnQuoteAcceptanceUnavailablePartialAsync(
                    "Your quote acceptance session is no longer active. Please refresh the page and open the latest quote, then try signing again.",
                    ct);
            }

            var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");
            if (string.IsNullOrWhiteSpace(bookingNo))
            {
                return await ReturnQuoteAcceptanceUnavailablePartialAsync(
                    "I could not find the booking linked to this quote in your current session. Please refresh the page and open your latest quote before signing.",
                    ct);
            }

            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            var (_, messages) = _chat.GetTranscript(threadId);
            var msgList = messages is List<DisplayMessage> list ? list : messages.ToList();

            var booking = await _bookingDb.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
            if (booking == null)
            {
                return await ReturnQuoteAcceptanceUnavailablePartialAsync(
                    $"I could not find booking {bookingNo} right now. Please refresh the page and try again. If this keeps happening, contact Microhire support.",
                    ct);
            }

            booking.BookingProgressStatus = 2; // Heavy Pencil
            await _bookingDb.SaveChangesAsync(ct);
            _logger.LogInformation("[SIGNING] Updated booking {BookingNo} status to Heavy Pencil via digital signature", bookingNo);

            // ---- Resolve quote HTML file path ----
            var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var quoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            string? quoteFilePath = null;
            if (!string.IsNullOrWhiteSpace(quoteUrl))
            {
                var relativePath = quoteUrl.TrimStart('/');
                quoteFilePath = Path.Combine(webRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            // ---- Overlay signature onto quote HTML ----
            string? amountFromHtml = null;
            if (quoteFilePath != null && System.IO.File.Exists(quoteFilePath))
            {
                try
                {
                    var html = await System.IO.File.ReadAllTextAsync(quoteFilePath, ct);

                    // Extract amount from the quote HTML as a fallback
                    var amountMatch = Regex.Match(html, @"Total Quotation</div>\s*<div class=""confirmation-value"">(.*?)</div>", RegexOptions.Singleline);
                    if (amountMatch.Success)
                        amountFromHtml = System.Net.WebUtility.HtmlDecode(amountMatch.Groups[1].Value).Trim();

                    var safeName = System.Net.WebUtility.HtmlEncode(fullName ?? "");
                    var safeDate = System.Net.WebUtility.HtmlEncode(signDate ?? "");
                    var safeTitle = System.Net.WebUtility.HtmlEncode(titlePosition ?? "");
                    var safePo = System.Net.WebUtility.HtmlEncode(purchaseOrder ?? "");
                    var filledLineStyle = "style=\"border-bottom:1px solid #333;height:30px;line-height:30px;font-size:13px;color:#222;padding-left:4px\"";

                    html = Regex.Replace(html,
                        @"(<label>Full Name</label>\s*)<div class=""signature-line""></div>",
                        $"$1<div class=\"signature-line\" {filledLineStyle}>{safeName}</div>");
                    html = Regex.Replace(html,
                        @"(<label>Date</label>\s*)<div class=""signature-line""></div>",
                        $"$1<div class=\"signature-line\" {filledLineStyle}>{safeDate}</div>");
                    html = Regex.Replace(html,
                        @"(<label>Title / Position</label>\s*)<div class=""signature-line""></div>",
                        $"$1<div class=\"signature-line\" {filledLineStyle}>{safeTitle}</div>");
                    html = Regex.Replace(html,
                        @"(<label>Purchase Order \(if applicable\)</label>\s*)<div class=""signature-line""></div>",
                        $"$1<div class=\"signature-line\" {filledLineStyle}>{safePo}</div>");

                    if (!string.IsNullOrWhiteSpace(signatureData))
                    {
                        html = Regex.Replace(html,
                            @"(<label>Signature</label>\s*)<div class=""signature-line signature-box""></div>",
                            $"$1<div class=\"signature-line signature-box\" style=\"border:1px solid #333;border-radius:4px;height:80px;padding:4px;display:flex;align-items:center\"><img src=\"{signatureData}\" style=\"max-width:100%;max-height:72px\" alt=\"Signature\" /></div>");
                    }

                    await System.IO.File.WriteAllTextAsync(quoteFilePath, html, ct);
                    _logger.LogInformation("[SIGNING] Quote HTML updated with signature data: {Path}", quoteFilePath);

                    var pdfPath = Path.ChangeExtension(quoteFilePath, ".pdf");
                    await HtmlQuoteGenerationService.GeneratePdfFromHtmlAsync(html, pdfPath, _logger);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SIGNING] Failed to overlay signature onto quote HTML for booking {BookingNo}", bookingNo);
                }
            }

            // ---- Persist signature metadata as JSON ----
            try
            {
                var sigDir = Path.Combine(webRoot, "files", "quotes");
                Directory.CreateDirectory(sigDir);
                var sigPayload = new
                {
                    BookingNo = bookingNo,
                    FullName = fullName ?? "",
                    SignDate = signDate ?? "",
                    TitlePosition = titlePosition ?? "",
                    PurchaseOrder = purchaseOrder ?? "",
                    SignedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
                    HasSignatureImage = !string.IsNullOrWhiteSpace(signatureData)
                };
                var sigPath = Path.Combine(sigDir, $"Quote-{bookingNo}-signature.json");
                await System.IO.File.WriteAllTextAsync(sigPath, JsonSerializer.Serialize(sigPayload, new JsonSerializerOptions { WriteIndented = true }), ct);
                _logger.LogInformation("[SIGNING] Signature metadata saved to {Path}", sigPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SIGNING] Failed to persist signature metadata for booking {BookingNo}", bookingNo);
            }

            try
            {
                await _chat.SendInternalFollowupAsync(bookingNo, $"Quote digitally signed and accepted by {fullName ?? "customer"} - status updated to Heavy Pencil.", ct);
                _logger.LogInformation("[SIGNING] Internal followup triggered for booking {BookingNo}", bookingNo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SIGNING] Failed to send internal followup for booking {BookingNo}", bookingNo);
            }

            // ---- Build confirmation message ----
            var bookingDate = booking.dDate ?? booking.SDate;
            var bookingDateStr = bookingDate?.ToString("dd MMMM yyyy") ?? "the scheduled date";

            string amountStr;
            if (booking.price_quoted.HasValue && booking.price_quoted.Value > 0)
                amountStr = booking.price_quoted.Value.ToString("C2", CultureInfo.GetCultureInfo("en-AU")) + " inc GST";
            else if (!string.IsNullOrWhiteSpace(amountFromHtml))
                amountStr = amountFromHtml;
            else
                amountStr = "the quoted amount";

            var confirmText = $"Your quote {bookingNo}, for a booking on {bookingDateStr} is confirmed. " +
                              $"Kindly attend to the booking payment of {amountStr}, payment details are noted on the Quote document. " +
                              "One of our team members will be in contact with you shortly.";

            var signedActionsHtml = "";
            if (!string.IsNullOrWhiteSpace(quoteUrl))
            {
                confirmText += "\n\nYou can view or download your signed quote using the buttons below.";
                signedActionsHtml = BuildSignedQuoteActionsHtml(quoteUrl, bookingNo);
            }

            // Redact BEFORE adding the confirmation so the amount isn't stripped
            RedactPricesForUiInPlace(msgList);

            var confirmationParts = new List<string> { confirmText };
            if (!string.IsNullOrWhiteSpace(signedActionsHtml))
            {
                confirmationParts.Add(signedActionsHtml);
            }

            var confirmationMessage = new DisplayMessage
            {
                Role = "assistant",
                Timestamp = DateTimeOffset.UtcNow,
                Parts = confirmationParts,
                FullText = confirmText,
                Html = $"<p>{System.Net.WebUtility.HtmlEncode($"Your quote {bookingNo}, for a booking on {bookingDateStr} is confirmed. Kindly attend to the booking payment of {amountStr}, payment details are noted on the Quote document. One of our team members will be in contact with you shortly.")}</p>{signedActionsHtml}"
            };
            await AddAssistantMessageAndPersistAsync(msgList, confirmationMessage, ct);

            HttpContext.Session.SetString("Draft:QuoteAccepted", "1");
            HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "0");
            HttpContext.Session.SetString(QuoteReviewPromptShownKey, "1");

            ViewData["QuoteAccepted"] = "1";
            ViewData["ShowQuoteCta"] = "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIGNING] Failed to process quote acceptance with signature for booking");
            Response.StatusCode = 500;
            return Content("Unable to process quote acceptance.");
        }
    }

    private async Task<IActionResult> ReturnQuoteAcceptanceUnavailablePartialAsync(string assistantMessage, CancellationToken ct)
    {
        _logger.LogInformation("[SIGNING] Returning graceful quote acceptance fallback: {Message}", assistantMessage);
        try
        {
            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            var (_, messages) = _chat.GetTranscript(threadId);
            var msgList = messages is List<DisplayMessage> list ? list : messages.ToList();

            var last = msgList.LastOrDefault();
            var duplicateAlreadyPresent =
                last != null
                && string.Equals(last.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                && string.Equals(last.FullText, assistantMessage, StringComparison.Ordinal);

            if (!duplicateAlreadyPresent)
            {
                var fallbackMessage = new DisplayMessage
                {
                    Role = "assistant",
                    Timestamp = DateTimeOffset.UtcNow,
                    Parts = new List<string> { assistantMessage },
                    FullText = assistantMessage,
                    Html = $"<p>{System.Net.WebUtility.HtmlEncode(assistantMessage)}</p>"
                };
                await AddAssistantMessageAndPersistAsync(msgList, fallbackMessage, ct);
            }

            RedactPricesForUiInPlace(msgList);
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            ViewData["ShowQuoteCta"] = "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SIGNING] Failed to return graceful quote acceptance fallback");
            return Content(assistantMessage);
        }
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
            || t.Contains("let me summarise") || t.Contains("let me summarize")  // Support both AUS and US spelling
            || t.Contains("here's your summary")
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
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(messages);

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
            // Clear quote completion state when resetting
            ClearBookingAndQuoteDraftState();
            HttpContext.Session.Remove("AgentThreadId");
            HttpContext.Session.Remove("Draft:ContactId");
            HttpContext.Session.Remove("Draft:CustomerCode");
            HttpContext.Session.Remove("Ack:Budget");
            HttpContext.Session.Remove("Ack:Attendees");
            HttpContext.Session.Remove("Ack:SetupStyle");
            HttpContext.Session.Remove("Ack:Venue");
            HttpContext.Session.Remove("Ack:SpecialRequests");
            HttpContext.Session.Remove("Ack:Dates");

            var newThreadId = _chat.EnsureThreadId(HttpContext.Session);

            var userKey = GetUserKey();
            await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(newThreadId);

            // Tell the view whether to show the Yes/No buttons
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(messages.ToList()) ? "1" : "0";
            ViewData["QuoteComplete"] = false;
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(messages);

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

    private static bool TryCaptureProjectorAreaSelections(string text, out List<string> areas, bool inProjectorContext = false)
    {
        areas = ParseProjectorAreasFromUserText(text, inProjectorContext);
        return areas.Count > 0;
    }

    /// <summary>
    /// Parses projector area identifiers from stored session values (e.g. "A" or "A,D").
    /// Session values are written by us, so a simple split on known delimiters is safe.
    /// </summary>
    private static List<string> ParseProjectorAreas(string? sessionValue)
    {
        if (string.IsNullOrWhiteSpace(sessionValue)) return new List<string>();
        return sessionValue
            .Split(new[] { ',', ';', '&', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToUpperInvariant())
            .Where(p => p.Length == 1 && p[0] >= 'A' && p[0] <= 'F')
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Parses projector area identifiers from free-form user text.
    /// When <paramref name="inProjectorContext"/> is true (prompt was already shown), bare
    /// single-letter and short multi-letter replies are accepted. Otherwise only explicit
    /// "area X" / "projector area X" tokens are matched to avoid false positives.
    /// </summary>
    private static List<string> ParseProjectorAreasFromUserText(string? text, bool inProjectorContext = false)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var trimmed = text.Trim();

        if (inProjectorContext)
        {
            // User is responding to the projector prompt — accept bare A-F letters
            foreach (Match m in Regex.Matches(trimmed, @"\b([A-F])\b", RegexOptions.IgnoreCase))
            {
                var area = m.Groups[1].Value.ToUpperInvariant();
                if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                    result.Add(area);
            }
        }
        else
        {
            // No prompt context — require explicit "area" or "projector area" prefix to avoid
            // capturing incidental A-F letters from unrelated messages.
            foreach (Match m in Regex.Matches(trimmed, @"\b(?:projector\s+area|area)\s*[:\-]?\s*([A-F])\b", RegexOptions.IgnoreCase))
            {
                var area = m.Groups[1].Value.ToUpperInvariant();
                if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                    result.Add(area);
            }
        }

        return result;
    }

    private static int GetRequestedProjectorCount(string summaryRequestsJson)
    {
        if (string.IsNullOrWhiteSpace(summaryRequestsJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(summaryRequestsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;
            var total = 0;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.TryGetProperty("EquipmentType", out var t1) ? t1.GetString()
                    : item.TryGetProperty("equipment_type", out var t2) ? t2.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(type) || !type.Contains("projector", StringComparison.OrdinalIgnoreCase))
                    continue;
                var qty = item.TryGetProperty("Quantity", out var q1) && q1.ValueKind == JsonValueKind.Number ? q1.GetInt32()
                    : item.TryGetProperty("quantity", out var q2) && q2.ValueKind == JsonValueKind.Number ? q2.GetInt32()
                    : 1;
                total += Math.Max(1, qty);
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static List<string> GetAllowedProjectorAreasForRoom(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        if (room is "westin ballroom 1" or "ballroom 1") return new List<string> { "E", "D", "C" };
        if (room is "westin ballroom 2" or "ballroom 2") return new List<string> { "A", "F", "B" };
        return new List<string> { "A", "B", "C", "D", "E", "F" };
    }

    private async Task AddAssistantMessageAndPersistAsync(List<DisplayMessage> msgList, DisplayMessage message, CancellationToken ct)
    {
        msgList.Add(message);

        try
        {
            var persistedText = (message.Parts != null && message.Parts.Count > 0)
                ? string.Join("\n\n", message.Parts.Where(p => !string.IsNullOrWhiteSpace(p)))
                : (message.FullText ?? string.Empty);
            await _chat.AppendAssistantMessageAsync(HttpContext.Session, persistedText, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist assistant message to thread.");
        }
    }

    private static int CountAssistantMessages(IEnumerable<DisplayMessage> messages)
        => messages.Count(m =>
            string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Role, "agent", StringComparison.OrdinalIgnoreCase));

    private static bool HasAssistantDelta(int assistantCountBefore, IEnumerable<DisplayMessage> afterMessages)
        => CountAssistantMessages(afterMessages) > assistantCountBefore;

    private static DisplayMessage BuildTransientFailureFallbackMessage()
    {
        const string fallbackText = "Sorry, I hit a temporary issue and could not complete that step. Please send your last message again and I will continue.";
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { fallbackText },
            FullText = fallbackText,
            Html = $"<p>{fallbackText}</p>"
        };
    }

    private static string? TryBuildLaborSummaryFromSelectedLabor(string selectedLaborJson)
    {
        if (string.IsNullOrWhiteSpace(selectedLaborJson))
        {
            return null;
        }

        try
        {
            var laborItems = JsonSerializer.Deserialize<List<SelectedLaborItem>>(
                selectedLaborJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (laborItems == null || laborItems.Count == 0)
            {
                return null;
            }

            return string.Join(
                "\n",
                laborItems.Select(i =>
                {
                    var durationHours = i.Hours + (i.Minutes / 60.0);
                    if (durationHours <= 0) durationHours = 1;
                    var task = string.IsNullOrWhiteSpace(i.Task) ? "Operate" : i.Task;
                    var quantity = i.Quantity <= 0 ? 1 : i.Quantity;
                    return $"{quantity}x {task} @ {durationHours:0.##} hours";
                }));
        }
        catch
        {
            return null;
        }
    }

    private void MarkQuoteReviewPromptPending()
    {
        HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "1");
        HttpContext.Session.SetString(QuoteReviewPromptShownKey, "0");
        HttpContext.Session.SetString("Draft:QuoteAccepted", "0");
    }

    private void AppendQuoteReviewPromptImmediately(List<DisplayMessage> msgList)
    {
        if (msgList.Count > 0 && WasLastAssistantAQuoteAcceptanceAsk(msgList))
        {
            return;
        }

        msgList.Add(BuildPostQuoteReviewPromptMessage());
        HttpContext.Session.SetString("Draft:QuoteAccepted", "0");
        HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "0");
        HttpContext.Session.SetString(QuoteReviewPromptShownKey, "1");
    }

    private void EnsureImmediateQuoteReviewPromptAfterQuoteSuccess(List<DisplayMessage> msgList)
    {
        if (HttpContext.Session.GetString("Draft:QuoteAccepted") == "1")
        {
            return;
        }

        if (msgList.Count == 0 || WasLastAssistantAQuoteAcceptanceAsk(msgList))
        {
            return;
        }

        var lastAssistant = msgList.LastOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        if (lastAssistant == null)
        {
            return;
        }

        var text = (lastAssistant.FullText ?? string.Join("\n\n", lastAssistant.Parts ?? Enumerable.Empty<string>()))
            .ToLowerInvariant();

        if (text.Contains("generated your quote") && text.Contains("booking"))
        {
            AppendQuoteReviewPromptImmediately(msgList);
        }
    }

    private static DisplayMessage BuildPostQuoteReviewPromptMessage()
    {
        const string promptText = "Would you like to proceed and accept this quote?\n\nSelecting Yes will notify Microhire to finalize the details.";
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { promptText },
            FullText = promptText,
            Html = $"<p>{promptText}</p>"
        };
    }

    private static DisplayMessage BuildQuoteReadyMessage(string bookingNo, string quoteUrl)
    {
        const string confirmationPrompt = "\n\nWould you like to confirm this quote?";
        var downloadHref = BuildQuoteDownloadHref(quoteUrl, bookingNo);
        var successPart = $"Great news! I have successfully generated your quote for booking {bookingNo}. You can view it <a href=\"{quoteUrl}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"isla-quote-open\" data-quote-open=\"1\">here</a> or <a href=\"{downloadHref}\" rel=\"noopener noreferrer\" class=\"isla-quote-download\" data-quote-download=\"1\" data-download-name=\"Quote-{bookingNo}.pdf\"><i class=\"ph ph-download\"></i> download it</a>.";
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { successPart + confirmationPrompt },
            FullText = $"Great news! I have successfully generated your quote for booking {bookingNo}. You can view it here: {quoteUrl}{confirmationPrompt}",
            Html = successPart
        };
    }

    private static string BuildSignedQuoteActionsHtml(string quoteUrl, string bookingNo)
    {
        var downloadHref = BuildQuoteDownloadHref(quoteUrl, bookingNo);
        return
            "<div style=\"margin-top:1rem;display:flex;gap:.75rem;flex-wrap:wrap;justify-content:center\">" +
            $"<a href=\"{quoteUrl}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"isla-quote-download-btn isla-quote-open\" data-quote-open=\"1\">View Signed Quote</a>" +
            $"<a href=\"{downloadHref}\" rel=\"noopener noreferrer\" class=\"isla-quote-download-btn isla-quote-download\" data-quote-download=\"1\" data-download-name=\"Quote-{bookingNo}.pdf\">Download Signed Quote</a>" +
            "</div>";
    }

    private static string BuildQuoteDownloadHref(string quoteUrl, string bookingNo)
    {
        if (string.IsNullOrWhiteSpace(quoteUrl))
        {
            return "#";
        }

        var srcPath = quoteUrl;
        if (Uri.TryCreate(quoteUrl, UriKind.Absolute, out var absoluteQuoteUri))
        {
            srcPath = absoluteQuoteUri.AbsolutePath;
        }

        if (!srcPath.StartsWith("/", StringComparison.Ordinal))
        {
            srcPath = "/" + srcPath.TrimStart('/');
        }

        var fileName = $"Quote-{bookingNo}.pdf";
        return $"/quotes/render?src={Uri.EscapeDataString(srcPath)}&outFile={Uri.EscapeDataString(fileName)}";
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
        out TimeSpan packup,
        out DateTime? eventDate)
    {
        setup = default;
        rehearsal = default;
        showStart = null;
        showEnd = null;
        packup = default;
        eventDate = null;

        var m = ChooseScheduleRe.Match(text);
        if (!m.Success) return false;

        var blob = m.Groups[1].Value;
        // Note: Cannot log here as this is a static method
        var kvs = blob.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        TimeSpan ts;
        bool gotSetup = false, gotReh = false, gotPack = false;

        foreach (var kv in kvs)
        {
            var parts = kv.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            var key = parts[0].ToLowerInvariant();
            var val = parts[1];
            // Note: Cannot log here as this is a static method

            // Handle date separately (ISO format like 2026-04-28)
            if (key == "date")
            {
                if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    eventDate = dt;
                continue;
            }

            // Parse time value - HTML5 time inputs send "HH:mm" format (e.g., "10:00", "16:00")
            // TimeSpan.TryParse can handle "HH:mm" format directly
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
                        // Note: Cannot log here as this is a static method
                    }
                    else
                    {
                        // Note: Cannot log here as this is a static method
                        continue; // Skip invalid time format
                    }
                }
                else
                {
                    // Note: Cannot log here as this is a static method
                    continue; // Skip invalid time format
                }
            }
            else
            {
                // Note: Cannot log here as this is a static method
            }

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

        // Validate chronological order before returning
        if (!ValidateScheduleOrder(setup, rehearsal, showStart, showEnd, packup))
        {
            return false;
        }

        return gotSetup && gotReh && gotPack;
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

    private void SaveScheduleToSession(
        TimeSpan setup,
        TimeSpan rehearsal,
        TimeSpan? showStart,
        TimeSpan? showEnd,
        TimeSpan packup,
        DateTime? eventDate)
    {
        HttpContext.Session.SetString("Draft:SetupTime", setup.ToString(@"hh\:mm"));
        HttpContext.Session.SetString("Draft:RehearsalTime", rehearsal.ToString(@"hh\:mm"));
        if (showStart.HasValue) HttpContext.Session.SetString("Draft:StartTime", showStart.Value.ToString(@"hh\:mm"));
        if (showEnd.HasValue) HttpContext.Session.SetString("Draft:EndTime", showEnd.Value.ToString(@"hh\:mm"));
        HttpContext.Session.SetString("Draft:PackupTime", packup.ToString(@"hh\:mm"));
        if (eventDate.HasValue) 
        {
            HttpContext.Session.SetString("Draft:EventDate", eventDate.Value.ToString("yyyy-MM-dd"));
            // Mark date as confirmed when schedule is submitted by user
            HttpContext.Session.SetString("Draft:DateConfirmed", "1");
            _logger.LogInformation("[DATE CONFIRM] Date confirmed via ChatController schedule submission: {EventDate}", eventDate.Value.ToString("yyyy-MM-dd"));
        }
    }

    /// <summary>
    /// Reads schedule times from session and returns them as a dictionary with default fallbacks.
    /// Returns null values for keys that don't exist in session.
    /// </summary>
    private Dictionary<string, string?> GetScheduleTimesFromSession()
    {
        return new Dictionary<string, string?>
        {
            { "setup", HttpContext.Session.GetString("Draft:SetupTime") },
            { "rehearsal", HttpContext.Session.GetString("Draft:RehearsalTime") },
            { "start", HttpContext.Session.GetString("Draft:StartTime") },
            { "end", HttpContext.Session.GetString("Draft:EndTime") },
            { "packup", HttpContext.Session.GetString("Draft:PackupTime") }
        };
    }

    /// <summary>
    /// Sets schedule times from session into ViewData for use by the view.
    /// </summary>
    private void SetScheduleTimesInViewData()
    {
        var scheduleTimes = GetScheduleTimesFromSession();
        ViewData["ScheduleSetupTime"] = scheduleTimes["setup"];
        ViewData["ScheduleRehearsalTime"] = scheduleTimes["rehearsal"];
        ViewData["ScheduleStartTime"] = scheduleTimes["start"];
        ViewData["ScheduleEndTime"] = scheduleTimes["end"];
        ViewData["SchedulePackupTime"] = scheduleTimes["packup"];
    }

    private string DetermineProgressStep(IEnumerable<DisplayMessage>? messages)
    {
        if (HttpContext.Session.GetString("Draft:QuoteComplete") == "1")
            return "fourth-step-active";

        var list = messages as IReadOnlyList<DisplayMessage> ?? messages?.ToList();

        if (list != null && WasLastAssistantASummaryAsk(list))
            return "third-step-active";

        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:SetupTime")))
            return "second-step-active";

        if (list != null && list.Count > 1)
            return "first-step-active";

        return "";
    }

    private static string Pretty12(TimeSpan? ts)
    {
        if (!ts.HasValue) return "";

        var time = ts.Value;
        var hours = time.Hours;
        var minutes = time.Minutes;

        // Convert to 12-hour format
        var period = hours >= 12 ? "PM" : "AM";
        var hour12 = hours == 0 ? 12 : (hours > 12 ? hours - 12 : hours);

        return $"{hour12}:{minutes:D2} {period}";
    }

    private async Task<string> RenderPartialViewToStringAsync(string viewName, object model)
    {
        var viewResult = _razorViewEngine.FindView(ControllerContext, viewName, isMainPage: false);
        if (viewResult?.View == null)
        {
            viewResult = _razorViewEngine.GetView(null, $"~/Views/Chat/{viewName}.cshtml", isMainPage: false);
            if (viewResult?.View == null)
                return string.Empty;
        }
        var tempDataProvider = HttpContext.RequestServices.GetRequiredService<ITempDataProvider>();
        await using var sw = new StringWriter();
        var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        foreach (var kv in ViewData)
            viewDictionary[kv.Key] = kv.Value;
        var viewContext = new ViewContext(ControllerContext, viewResult.View, viewDictionary, new TempDataDictionary(HttpContext, tempDataProvider), sw, new HtmlHelperOptions());
        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }

    private async Task<decimal?> TrySaveContactAsync(IEnumerable<DisplayMessage> messages, CancellationToken ct)
    {
        try
        {
            if (messages is null) return null;
            var list = messages.ToList();
            if (list.Count == 0) return null;

            // ---------- helpers ----------
            static bool IsUser(DisplayMessage m) =>
                string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase);

            static bool LooksLikeMissing(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return true;
                var v = value.Trim().ToLowerInvariant();
                return v is "no" or "none" or "n/a" or "na" or "nil"
                         or "unknown" or "tbc" or "not sure"
                         or "dont know" or "don't know";
            }

            static bool LooksLikeValidHumanName(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                var t = s.Trim();
                if (t.Contains('@')) return false;
                if (Regex.IsMatch(t, @"\d")) return false;
                if (t.Length < 2 || t.Length > 80) return false;

                var lower = t.ToLowerInvariant();
                if (LooksLikeMissing(lower)) return false;
                if (lower is "yes" or "yeah" or "yep" or "nope" or "ok" or "okay")
                    return false;
                if (LooksLikeAssistantName(t)) return false;

                return true;
            }

            static string NormalizePhone(string raw)
            {
                raw = raw.Trim();
                var sb = new StringBuilder(raw.Length);
                foreach (var ch in raw)
                    if (char.IsDigit(ch) || (ch == '+' && sb.Length == 0))
                        sb.Append(ch);
                return sb.ToString();
            }

            var emailRe = new Regex(@"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var phoneRe = new Regex(@"\+?\d[\d\s\-()]{6,}\d", RegexOptions.Compiled);

            string? name = null;
            string? email = null;
            string? phone = null;
            string? position = null;

            // -------- NAME (first valid user answer without comma) --------
            foreach (var m in list.Where(IsUser))
            {
                var txt = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
                if (string.IsNullOrWhiteSpace(txt)) continue;

                // ignore the combined contact line for name
                if (txt.Contains(",")) continue;

                if (LooksLikeValidHumanName(txt))
                {
                    name = txt;
                    break;
                }
            }

            // optional: fallback to summary-based guess
            if (string.IsNullOrWhiteSpace(name))
            {
                var guessed = GuessUserNameFromTranscript(list);
                if (LooksLikeValidHumanName(guessed))
                    name = guessed;
            }

            // -------- EMAIL / PHONE / POSITION (scan backwards) --------
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var m = list[i];
                if (!IsUser(m)) continue;

                var txt = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
                if (string.IsNullOrWhiteSpace(txt)) continue;

                if (email == null)
                {
                    var em = emailRe.Match(txt);
                    if (em.Success) email = em.Value.Trim();
                }

                if (phone == null)
                {
                    var pm = phoneRe.Match(txt);
                    if (pm.Success) phone = NormalizePhone(pm.Value);
                }

                if (position == null)
                {
                    var p = ExtractPositionForContact(txt);
                    if (!string.IsNullOrWhiteSpace(p))
                        position = p;
                }

                if (email != null && phone != null && position != null)
                    break;
            }

            // ---- IMPORTANT GUARD ----
            // if we still don't have any real contact details, don't create/update
            if (string.IsNullOrWhiteSpace(email) &&
                string.IsNullOrWhiteSpace(phone) &&
                string.IsNullOrWhiteSpace(position))
            {
                return null;
            }

            // At this point we have at least one of: email / phone / position
            return await _contactPersistence.UpsertContactByEmailAsync(
                name,
                email,
                phone,
                position,
                ct);
        }
        catch
        {
            return null;
        }
    }


    private static string? ExtractPositionForContact(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Replace('’', '\'').Replace('“', '"').Replace('”', '"');

        // "..., position : leader" OR "position :-leader"
        var reLabel = new Regex(
            @"(?:^|[,;])\s*(position|title|role)\s*[:\-]\s*(?<v>[^,;]+)",
            RegexOptions.IgnoreCase);

        var m1 = reLabel.Match(t);
        if (m1.Success) return Clean(m1.Groups["v"].Value);

        // "I am the event manager", "I work as a coordinator"
        var reIam = new Regex(
            @"\b(i\s*am|i'm|i\s*work\s*as|my\s*role\s*is)\s+(the\s+)?(?<v>[A-Za-z][A-Za-z \-/&\.]{2,60})",
            RegexOptions.IgnoreCase);

        var m2 = reIam.Match(t);
        if (m2.Success) return Clean(m2.Groups["v"].Value);

        return null;

        static string? Clean(string s)
        {
            s = s.Trim().Trim(' ', '.', ',', ';', ':', '-', '–', '—');
            if (s.Length == 0) return null;
            if (s.Length > 60) s = s[..60];

            // reject numeric garbage (like 7894561230)
            var digitCount = s.Count(char.IsDigit);
            if (digitCount >= s.Length / 2) return null;

            var lower = s.ToLowerInvariant();
            if (lower is "no" or "none" or "n/a" or "na" or "nil" or "unknown")
                return null;

            return CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(s.ToLowerInvariant());
        }
    }






    private static string JoinParts(DisplayMessage m) =>
    string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();

    private async Task EnsureAcceptedQuoteStaysHeavyPencilAsync(CancellationToken ct)
    {
        if (!string.Equals(HttpContext.Session.GetString("Draft:QuoteAccepted"), "1", StringComparison.Ordinal))
            return;

        var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");
        if (string.IsNullOrWhiteSpace(bookingNo))
            return;

        var booking = await _bookingDb.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
        if (booking == null)
            return;

        var current = booking.BookingProgressStatus ?? 0;
        if (current >= 2)
            return;

        booking.BookingProgressStatus = 2; // Heavy Pencil
        await _bookingDb.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Auto-corrected BookingProgressStatus to Heavy Pencil for accepted quote {BookingNo}. Previous={Previous}",
            bookingNo, current);
    }

    private static byte NoteTypeFor(string role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)2;
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

            // reject assistant name (Isla, Microhire, or both)
            if (LooksLikeAssistantName(t)) return false;

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

    // Regex to match gallery blocks that should be preserved during price redaction
    private static readonly Regex GalleryBlockRe =
        new(@"\[\[ISLA_GALLERY\]\].*?\[\[/ISLA_GALLERY\]\]", RegexOptions.Singleline);

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
                // Extract and preserve gallery blocks before filtering
                var galleries = new List<string>();
                var partWithPlaceholders = GalleryBlockRe.Replace(part ?? string.Empty, match =>
                {
                    galleries.Add(match.Value);
                    return $"__ISLA_GALLERY_PLACEHOLDER_{galleries.Count - 1}__";
                });

                var lines = partWithPlaceholders.Replace("\r", "").Split('\n');
                var kept = new List<string>();

                foreach (var line in lines)
                {
                    // Always keep lines that contain gallery placeholders
                    if (line.Contains("__ISLA_GALLERY_PLACEHOLDER_"))
                    {
                        kept.Add(line);
                        continue;
                    }

                    // Special handling for equipment and summary lines: redact the price but keep the text
                    // These lines typically start with a dash/bullet or contain bold headers
                    if (line.TrimStart().StartsWith("-") || line.Contains("**"))
                    {
                        var redacted = CurrencyAmountRe.Replace(line, "[Price in quote]");
                        kept.Add(redacted);
                        continue;
                    }

                    if (CurrencyAmountRe.IsMatch(line)) continue;
                    if (PriceOrQuoteLineRe.IsMatch(line)) continue;
                    kept.Add(line);
                }

                // Only show fallback message if no galleries were preserved and all lines were filtered
                if (kept.Count == 0 && lines.Length > 0 && galleries.Count == 0)
                    kept.Add("[A quote was prepared. Price is hidden in chat.]");

                // Restore gallery blocks in place of placeholders
                var result = string.Join("\n", kept);
                for (int i = 0; i < galleries.Count; i++)
                {
                    result = result.Replace($"__ISLA_GALLERY_PLACEHOLDER_{i}__", galleries[i]);
                }

                newParts.Add(result);
            }

            m.Parts = newParts;
        }
    }

    // One in-flight call per thread prevents parallel runs that trigger 429s.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _threadLocks = new();

    private SemaphoreSlim GetThreadLock(string threadId)
        => _threadLocks.GetOrAdd(threadId, _ => new SemaphoreSlim(1, 1));

    private static async Task<T> WithChatOperationTimeoutAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken requestCt,
        int timeoutSeconds = 75)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(requestCt, timeoutCts.Token);

        try
        {
            return await action(linkedCts.Token);
        }
        catch (OperationCanceledException ex) when (!requestCt.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"{operation} timed out after {timeoutSeconds} seconds.", ex);
        }
    }

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
