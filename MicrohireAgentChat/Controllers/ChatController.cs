// Controllers/ChatController.cs
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models; // for DisplayMessage if needed
using MicrohireAgentChat.Services;
using MicrohireAgentChat.Services.Orchestration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Controllers;

public sealed class ChatController : Controller
{
    private readonly AzureAgentChatService _chat;
    private readonly BookingDbContext _bookingDb;
    private readonly BookingOrchestrationService _orchestration;
    private readonly HtmlQuoteGenerationService _htmlQuoteGen;
    private readonly ConversationReplayService _replayService;
    private readonly DevModeOptions _devOptions;
    private readonly ILogger<ChatController> _logger;

    // Detect: "Choose time: 09:00–10:30" (supports hyphen or en dash)
    private static readonly Regex ChooseTimeRe =
        new(@"^Choose\s*time:\s*(\d{1,2}:\d{2})\s*[–-]\s*(\d{1,2}:\d{2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Detect: "Choose schedule: key=HH:mm; key=HH:mm; ..."
    private static readonly Regex ChooseScheduleRe =
        new(@"^\s*Choose\s+schedule\s*:\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Exact scripted greeting per your "Final Script for AI"
    private const string GreetingText =
        "Hello, my name is Isla from Microhire. What is your full name?";

    public ChatController(
        AzureAgentChatService chat,
        BookingDbContext bookingDb,
        BookingOrchestrationService orchestration,
        HtmlQuoteGenerationService htmlQuoteGen,
        ConversationReplayService replayService,
        IOptions<DevModeOptions> devOptions,
        ILogger<ChatController> logger)
    {
        _chat = chat;
        _bookingDb = bookingDb;
        _orchestration = orchestration;
        _htmlQuoteGen = htmlQuoteGen;
        _replayService = replayService;
        _devOptions = devOptions.Value;
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

    // Get quote URL if available
    private string? GetQuoteUrl()
    {
        return HttpContext.Session.GetString("Draft:QuoteUrl");
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
                    ViewData["ExistingQuoteUrl"] = existingQuoteUrl;
                }
            }

            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(threadId);
            RedactPricesForUiInPlace(messages);
            ViewData["DevModeEnabled"] = _devOptions.Enabled;
            _logger.LogInformation("DevMode enabled: {_devOptions.Enabled}", _devOptions.Enabled);
            ViewData["QuoteComplete"] = false;
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

        // Clear quote completion state when user sends new message - allows continued conversation
        if (IsQuoteComplete())
        {
            HttpContext.Session.Remove("Draft:QuoteComplete");
            HttpContext.Session.Remove("Draft:QuoteUrl");
        }

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
            if (TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack, out var eventDate))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack, eventDate);
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

    private static bool ContainsConfusingQuoteLanguage(IReadOnlyList<DisplayMessage> messages)
    {
        if (messages == null || messages.Count == 0) return false;

        // Check the last assistant message for confusing quote language
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msg = messages[i];
            if (!string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var text = string.Join("\n\n", msg.Parts ?? Enumerable.Empty<string>()).ToLowerInvariant();
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
                text.Contains("issue with generating") ||
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

        // Look through the last 6 messages for any assistant summary confirmation
        int messagesToCheck = Math.Min(6, messages.Count);
        for (int i = messages.Count - 1; i >= messages.Count - messagesToCheck; i--)
        {
            var m = messages[i];
            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var raw = string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
            var t = Normalize(raw);
            if (LooksLikeSummaryAsk(t))
            {
                return true;
            }
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
            || t.Contains("here's your summary")
            || t.Contains("here's what i have so far")
            || t.Contains("here's a summary")
            || t.Contains("let me summarize")
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
            || t.Contains("finalize the quote")
            || t.Contains("finalized equipment")
            || t.Contains("equipment lineup")
            || t.Contains("equipment selection")
            || t.Contains("here's your finalized")
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
    public async Task<IActionResult> StartConversationReplay(CancellationToken ct)
    {
        if (!_devOptions.Enabled)
        {
            return BadRequest("Dev mode is not enabled");
        }

        try
        {
            // Randomly select a test scenario for comprehensive coverage
            var random = new Random();
            var scenarioType = random.Next(20); // 20 different scenario weights for better distribution
            
            IEnumerable<string> messages;
            string scenarioName;
            
            switch (scenarioType)
            {
                case 0: // Small intimate event
                    messages = _replayService.GenerateSmallEventConversation();
                    scenarioName = "Small Event (15-25 people)";
                    break;
                    
                case 1: // Large conference
                    messages = _replayService.GenerateLargeConferenceConversation();
                    scenarioName = "Large Conference (300-500 people)";
                    break;
                    
                case 2: // Hackathon
                    messages = _replayService.GenerateHackathonConversation();
                    scenarioName = "Hackathon Event";
                    break;
                    
                case 3: // Social/Gala event
                    messages = _replayService.GenerateSocialEventConversation();
                    scenarioName = "Social/Gala Event";
                    break;
                    
                case 4: // Minimal info user
                    messages = _replayService.GenerateMinimalInfoConversation();
                    scenarioName = "Minimal Info User";
                    break;
                    
                case 5: // Very detailed user
                    messages = _replayService.GenerateDetailedConversation();
                    scenarioName = "Detailed/Verbose User";
                    break;
                    
                case 6: // Complex with changes
                    messages = _replayService.GenerateComplexTestConversation();
                    scenarioName = "Complex with Follow-ups";
                    break;
                    
                case 7: // Urgent/rush booking
                    messages = _replayService.GenerateUrgentBookingConversation();
                    scenarioName = "Urgent/Rush Booking";
                    break;
                    
                case 8: // Multi-day event
                    messages = _replayService.GenerateMultiDayEventConversation();
                    scenarioName = "Multi-Day Event";
                    break;
                    
                case 9: // Panel discussion at Westin Brisbane
                    messages = _replayService.GenerateEquipmentOnlyConversation();
                    scenarioName = "Panel Discussion";
                    break;
                    
                case 10: // All info at once
                    messages = _replayService.GenerateAllAtOnceConversation();
                    scenarioName = "All Info At Once";
                    break;
                    
                case 11: // Training workshop
                    messages = _replayService.GenerateTrainingWorkshopConversation();
                    scenarioName = "Training Workshop";
                    break;
                    
                case 12: // Mac-focused tech company
                    messages = _replayService.GenerateMacTechConversation();
                    scenarioName = "Mac/Creative Tech Company";
                    break;
                    
                case 13: // Boardroom meeting
                    messages = _replayService.GenerateBoardroomMeetingConversation();
                    scenarioName = "Corporate Boardroom Meeting";
                    break;
                    
                case 14: // Product launch
                    messages = _replayService.GenerateProductLaunchConversation();
                    scenarioName = "Product Launch Event";
                    break;
                    
                default: // 25% - Standard random (most common case)
                    messages = _replayService.GenerateTestConversation();
                    scenarioName = "Standard Random Event";
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
                scenario = scenarioName
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
    public async Task<IActionResult> GetNextReplayMessage(CancellationToken ct)
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
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        try
        {
            // Clear quote completion state when resetting
            HttpContext.Session.Remove("AgentThreadId");
            HttpContext.Session.Remove("Draft:QuoteComplete");
            HttpContext.Session.Remove("Draft:QuoteUrl");
            HttpContext.Session.Remove("Draft:BookingNo");
            HttpContext.Session.Remove("Draft:ContactId");
            HttpContext.Session.Remove("Draft:CustomerCode");
            HttpContext.Session.Remove("Draft:PersistedSummaryKey");
            HttpContext.Session.Remove("Draft:ShowedBookingNo");

            var newThreadId = _chat.EnsureThreadId(HttpContext.Session);

            var userKey = GetUserKey();
            await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(newThreadId);
            ViewData["QuoteComplete"] = false;
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

        // Clear quote completion state when user sends new message - allows continued conversation
        if (IsQuoteComplete())
        {
            HttpContext.Session.Remove("Draft:QuoteComplete");
            HttpContext.Session.Remove("Draft:QuoteUrl");
        }

        try
        {
            var userKey = GetUserKey();
            await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);

            text = text.Trim();
            var rawUserTextForContact = text;   // keep original text for contact parsing

            if (TryCaptureTimeSelection(text, out var start, out var end))
            {
                SaveTimeSelectionToSession(start, end);
                text = $"Choose time: {start:hh\\:mm}–{end:hh\\:mm}";
            }

            if (TryCaptureScheduleSelection(text, out var set, out var reh, out var showStart, out var showEnd, out var pack, out var eventDate))
            {
                SaveScheduleToSession(set, reh, showStart, showEnd, pack, eventDate);
                var pretty = $"Schedule selected: Setup {Pretty12(set)}; Rehearsal {Pretty12(reh)}; "
                           + (showStart.HasValue ? $"Start {Pretty12(showStart)}; " : "")
                           + (showEnd.HasValue ? $"End {Pretty12(showEnd)}; " : "")
                           + $"Pack Up {Pretty12(pack)}.";
                text = pretty.Trim();
            }

            // —— safe send ——
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

            // If the last AI message contains confusing quote language, we'll handle it in the consent logic

            // Check if user confirmed a booking summary
            // IMPORTANT: Only trigger if BOTH conditions are met:
            // 1. User's message looks like explicit consent to create quote
            // 2. The assistant was actually asking for confirmation (summary ask)
            var isConsentToSummary = LooksLikeConsent(text) && WasLastAssistantASummaryAsk(msgList);

            // Log consent detection for debugging
            _logger.LogInformation("Consent detection: text='{Text}', isConsentToSummary={IsConsentToSummary}, wasSummaryAsk={WasSummaryAsk}",
                text, isConsentToSummary, WasLastAssistantASummaryAsk(msgList));

            // NOTE: Removed aggressive "isClearConsent" logic that was causing false triggers
            // Quote generation should ONLY happen when assistant explicitly asked for confirmation

            // If user confirmed booking and AI gave a confusing quote response, add clarification
            if (isConsentToSummary && ContainsConfusingQuoteLanguage(msgList))
            {
                var clarificationMessage = new DisplayMessage
                {
                    Role = "assistant",
                    Timestamp = DateTimeOffset.UtcNow,
                    Parts = new List<string> {
                        "Perfect! I'll create your booking right away and automatically generate a professional quote PDF for you. You'll see a download link here once it's ready."
                    },
                    FullText = "Perfect! I'll create your booking right away and automatically generate a professional quote PDF for you. You'll see a download link here once it's ready.",
                    Html = "<p>Perfect! I'll create your booking right away and automatically generate a professional quote PDF for you. You'll see a download link here once it's ready.</p>"
                };
                msgList = msgList.Concat(new[] { clarificationMessage }).ToList();
            }

            // if booking already exists, persist FULL transcript now
            var bookingNoAtStart = HttpContext.Session.GetString("Draft:BookingNo");
            if (!string.IsNullOrWhiteSpace(bookingNoAtStart))
            {
                await _chat.SaveFullTranscriptToBooknoteAsync(_bookingDb, bookingNoAtStart!, msgList, ct);
            }

            if (isConsentToSummary)
            {
                var summaryKey = ComputeSummaryKey(msgList);
                var lastPersistedKey = HttpContext.Session.GetString("Draft:PersistedSummaryKey");

                // Skip duplicate processing if we already handled this exact summary
                if (isConsentToSummary && !string.IsNullOrEmpty(summaryKey) &&
                    string.Equals(summaryKey, lastPersistedKey, StringComparison.Ordinal))
                {
                    RedactPricesForUiInPlace(msgList);
                    ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
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
                    var totalDayRate = HttpContext.Session.GetString("Draft:TotalDayRate");
                    if (!string.IsNullOrWhiteSpace(totalDayRate))
                    {
                        additionalFacts["price_quoted"] = totalDayRate;
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
                                    "I apologize, but there was an issue creating your booking. Our team will follow up with you to complete this process."
                                },
                                FullText = "I apologize, but there was an issue creating your booking. Our team will follow up with you to complete this process.",
                                Html = "<p>I apologize, but there was an issue creating your booking. Our team will follow up with you to complete this process.</p>"
                            };
                            msgList = msgList.Concat(new[] { errorMessage }).ToList();
                        }
                    }
                }
            }

            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            
            // Log final message count for debugging
            _logger.LogInformation("Returning {Count} messages to view. Last message role: {LastRole}", 
                msgList.Count, 
                msgList.LastOrDefault()?.Role ?? "none");
            
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

    private async Task GenerateQuoteForBookingAsync(string bookingNo, List<DisplayMessage> msgList, CancellationToken ct)
    {
        // ========== GENERATE QUOTE HTML ==========
        try
        {
            _logger.LogInformation("Starting HTML quote generation for booking {BookingNo}", bookingNo);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            try
            {
                var (quoteSuccess, quoteUrl, quoteError) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(bookingNo, linkedCts.Token);
                _logger.LogInformation("HTML quote generation completed for booking {BookingNo}, success: {Success}", bookingNo, quoteSuccess);

                if (quoteSuccess && !string.IsNullOrWhiteSpace(quoteUrl))
                {
                    HttpContext.Session.SetString("Draft:QuoteUrl", quoteUrl);
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1"); // Mark quote as complete
                    _logger.LogInformation("HTML quote generated for booking {BookingNo}: {QuoteUrl}", bookingNo, quoteUrl);

                    // Check if the last AI message was confusing about quotes - if so, replace it
                    var lastAssistantMessage = msgList.LastOrDefault(m => m.Role == "assistant");
                    if (lastAssistantMessage != null && ContainsConfusingQuoteLanguage(new[] { lastAssistantMessage }))
                    {
                        _logger.LogInformation("Replacing confusing AI quote message with success message");
                        // Remove the confusing message and add our success message
                        msgList.Remove(lastAssistantMessage);
                    }

                    // Add success message directly to the conversation (bypass AI agent)
                    var quoteMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> {
                            $"Great news! I've successfully generated your quote for booking {bookingNo}. <a href=\"{quoteUrl}\" target=\"_blank\" class=\"btn btn-primary\">View Quote</a>"
                        },
                        FullText = $"Great news! I've successfully generated your quote for booking {bookingNo}. You can view it here: {quoteUrl}",
                        Html = $"Great news! I've successfully generated your quote for booking {bookingNo}. <a href=\"{quoteUrl}\" target=\"_blank\" class=\"btn btn-primary\">View Quote</a>"
                    };
                    msgList.Add(quoteMessage);
                    _logger.LogInformation("Success message added to msgList. Total messages: {Count}. Quote URL: {QuoteUrl}", msgList.Count, quoteUrl);
                }
                else
                {
                    _logger.LogWarning("HTML quote generation failed for booking {BookingNo}: {Error}", bookingNo, quoteError);

                    // Add failure message directly to the conversation
                    var failureMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> {
                            "I apologize, but there was an issue generating your quote. Our team will follow up with you shortly to resolve this."
                        },
                        FullText = "I apologize, but there was an issue generating your quote. Our team will follow up with you shortly to resolve this.",
                        Html = "<p>I apologize, but there was an issue generating your quote. Our team will follow up with you shortly to resolve this.</p>"
                    };
                    msgList.Add(failureMessage);
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("HTML quote generation timed out for booking {BookingNo}", bookingNo);

                // Check if quote was actually generated despite the timeout by looking for existing HTML files
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
                    // Quote was generated successfully despite timeout
                    HttpContext.Session.SetString("Draft:QuoteUrl", existingQuoteUrl);
                    HttpContext.Session.SetString("Draft:QuoteComplete", "1"); // Mark quote as complete
                    _logger.LogInformation("HTML quote was actually generated despite timeout for booking {BookingNo}: {QuoteUrl}", bookingNo, existingQuoteUrl);

                    var successMessage = new DisplayMessage
                    {
                        Role = "assistant",
                        Timestamp = DateTimeOffset.UtcNow,
                        Parts = new List<string> {
                            $"Great news! I've successfully generated your quote for booking {bookingNo}. <a href=\"{existingQuoteUrl}\" target=\"_blank\" class=\"btn btn-primary\">View Quote</a>"
                        },
                        FullText = $"Great news! I've successfully generated your quote for booking {bookingNo}. You can view it here: {existingQuoteUrl}",
                        Html = $"Great news! I've successfully generated your quote for booking {bookingNo}. <a href=\"{existingQuoteUrl}\" target=\"_blank\" class=\"btn btn-primary\">View Quote</a>"
                    };
                    msgList.Add(successMessage);
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
                    msgList.Add(timeoutMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during quote generation for booking {BookingNo}", bookingNo);
        }
    }

    private static bool LooksLikeConsent(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return false;

        var t = userText.Trim().ToLowerInvariant();
        
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
            "finalize the booking", "finalize booking",
            "yes please create", "yes generate", "go ahead and create",
            "i'm ready for the quote", "ready for the quote", 
            "create my quote", "finalize the quote",
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
            // Clear quote completion state when resetting
            HttpContext.Session.Remove("AgentThreadId");
            HttpContext.Session.Remove("Draft:QuoteComplete");
            HttpContext.Session.Remove("Draft:QuoteUrl");
            HttpContext.Session.Remove("Draft:BookingNo");
            HttpContext.Session.Remove("Draft:ContactId");
            HttpContext.Session.Remove("Draft:CustomerCode");
            HttpContext.Session.Remove("Draft:PersistedSummaryKey");
            HttpContext.Session.Remove("Draft:ShowedBookingNo");

            var newThreadId = _chat.EnsureThreadId(HttpContext.Session);

            var userKey = GetUserKey();
            await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);

            await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);

            var (_, messages) = _chat.GetTranscript(newThreadId);

            // Tell the view whether to show the Yes/No buttons
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(messages.ToList()) ? "1" : "0";
            ViewData["QuoteComplete"] = false;

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
        var kvs = blob.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        TimeSpan ts;
        bool gotSetup = false, gotReh = false, gotPack = false;

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
        TimeSpan packup,
        DateTime? eventDate)
    {
        HttpContext.Session.SetString("Draft:SetupTime", setup.ToString(@"hh\:mm"));
        HttpContext.Session.SetString("Draft:RehearsalTime", rehearsal.ToString(@"hh\:mm"));
        if (showStart.HasValue) HttpContext.Session.SetString("Draft:StartTime", showStart.Value.ToString(@"hh\:mm"));
        if (showEnd.HasValue) HttpContext.Session.SetString("Draft:EndTime", showEnd.Value.ToString(@"hh\:mm"));
        HttpContext.Session.SetString("Draft:PackupTime", packup.ToString(@"hh\:mm"));
        if (eventDate.HasValue) HttpContext.Session.SetString("Draft:EventDate", eventDate.Value.ToString("yyyy-MM-dd"));
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
            return await _chat.UpsertContactByEmailAsync(
                _bookingDb,
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
