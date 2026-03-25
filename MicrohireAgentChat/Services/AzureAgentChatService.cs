// AzureAgentChatService.cs
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using Markdig;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using MicrohireAgentChat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services
{
    /// <summary>
    /// Result of SendAsync; when ContinueNeeded is true, caller should invoke SendAsyncContinue.
    /// </summary>
    public sealed record SendAsyncResult(IEnumerable<DisplayMessage> Messages, bool ContinueNeeded);

    public sealed partial class AzureAgentChatService
    {
        private const string SessionKeyThreadId = "AgentThreadId";
        private const string SessionKeyGreeted = "IslaGreeted";
        private const string SessionKeyActiveRunId = "AgentActiveRunId";
        private const string SessionKeyContactSavePending = "ContactSavePending";
        private const string SessionKeyContactSaveCompleted = "ContactSaveCompleted";
        private const string SessionKeyCurrentTurnId = "GuardFollowUpCurrentTurnId";
        private const string SessionKeyGuardFollowUpTurnId = "GuardFollowUpTurnId";
        private const string SessionKeyGuardFollowUpFingerprint = "GuardFollowUpFingerprint";
        private const string SessionKeyGuardFollowUpText = "GuardFollowUpText";
        
        private readonly AIProjectClient _projectClient;
        private readonly string _agentId;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AzureAgentChatService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly BookingDbContext _bookings;
        private readonly AppDbContext _appDb;  // For thread persistence (separate from client's DB)
        
        // Modular services (extracted from this monolith)
        private readonly BookingOrchestrationService _orchestration;
        private readonly AgentToolHandlerService _toolHandler;
        private readonly TimePickerService _timePicker;
        private readonly QuoteGenerationService _quoteGen;
        private readonly HtmlQuoteGenerationService _htmlQuoteGen;
        private readonly ItemPersistenceService _itemPersistence;
        private readonly ConversationStateService _conversationState;
        private readonly AcknowledgmentService _acknowledgment;
        private readonly QuestionDetectionService _questionDetection;
        private readonly ConversationExtractionService _extraction;
        private readonly ChatExtractionService _chatExtraction;
        private readonly BookingPersistenceService _bookingPersistence;
        
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _threadLocks = new();
        private static SemaphoreSlim GetThreadGate(string threadId)
            => _threadLocks.GetOrAdd(threadId, _ => new SemaphoreSlim(1, 1));

        public AzureAgentChatService(
            AIProjectClient projectClient,
            IOptions<AzureAgentOptions> options,
            BookingDbContext bookings,
            AppDbContext appDb,  // Thread persistence DB (separate from client's BookingsDb)
            IHttpContextAccessor http,
            IWebHostEnvironment env,
            ILogger<AzureAgentChatService> logger,
            BookingOrchestrationService orchestration,
            AgentToolHandlerService toolHandler,
            TimePickerService timePicker,
            QuoteGenerationService quoteGen,
            HtmlQuoteGenerationService htmlQuoteGen,
            ItemPersistenceService itemPersistence,
            ConversationStateService conversationState,
            AcknowledgmentService acknowledgment,
            QuestionDetectionService questionDetection,
            ConversationExtractionService extraction,
            ChatExtractionService chatExtraction,
            BookingPersistenceService bookingPersistence)
        {
            _projectClient = projectClient;
            _agentId = options.Value.AgentId ?? throw new ArgumentNullException(nameof(options.Value.AgentId));
            _bookings = bookings;
            _appDb = appDb;
            _http = http;
            _env = env;
            _logger = logger;
            _orchestration = orchestration;
            _toolHandler = toolHandler;
            _timePicker = timePicker;
            _quoteGen = quoteGen;
            _htmlQuoteGen = htmlQuoteGen;
            _itemPersistence = itemPersistence;
            _conversationState = conversationState;
            _acknowledgment = acknowledgment;
            _questionDetection = questionDetection;
            _extraction = extraction;
            _chatExtraction = chatExtraction;
            _bookingPersistence = bookingPersistence;
        }

        private PersistentAgentsClient AgentsClient => _projectClient.GetPersistentAgentsClient();

        #region Controller-facing API

        public async Task SendInternalFollowupAsync(string bookingNo, string reason, CancellationToken ct)
        {
            _logger.LogWarning("[INTERNAL_FOLLOWUP] HIGH PRIORITY: Booking {BookingNo} requires follow-up. Reason: {Reason}", bookingNo, reason);
            // Future: Integrate with email or Slack/Teams webhook here
            await Task.CompletedTask;
        }

        public async Task AppendAssistantMessageAsync(ISession session, string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var threadId = EnsureThreadId(session);
            await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(10));
            AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, text.Trim());
        }

        /// <summary>
        /// Appends a user message to the Azure thread without starting an agent run (e.g. structured wizard submit
        /// handled server-side). Mirrors the user-message step in <see cref="SendAsync"/>.
        /// </summary>
        public async Task AppendUserMessageAsync(ISession session, string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var threadId = EnsureThreadId(session);
            await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(10));
            var activeRuns = AgentsClient.Runs.GetRuns(threadId).Where(r => IsActive(r.Status)).ToList();
            if (activeRuns.Count > 0)
            {
                CancelActiveRuns(threadId);
                await Task.Delay(500, ct);
                await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(5));
            }

            try
            {
                AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.User, text.Trim());
                _logger.LogInformation("AppendUserMessageAsync: user message added to thread {ThreadId}", threadId);
                await Task.Delay(300, ct);
            }
            catch (RequestFailedException ex) when (ex.Status == 400 && ex.Message.Contains("run", StringComparison.OrdinalIgnoreCase) && ex.Message.Contains("active", StringComparison.OrdinalIgnoreCase))
            {
                CancelActiveRuns(threadId);
                await Task.Delay(2000, ct);
                await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(5));
                AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.User, text.Trim());
                await Task.Delay(300, ct);
            }
        }

        private static bool IsActive(Azure.AI.Agents.Persistent.RunStatus s)
            => s == Azure.AI.Agents.Persistent.RunStatus.Queued || s == Azure.AI.Agents.Persistent.RunStatus.InProgress || s == Azure.AI.Agents.Persistent.RunStatus.RequiresAction;

        private static readonly Regex _retryAfterRegex =
            new Regex(@"\b(\d+)\s*seconds?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Parse "retry after N seconds" from Azure rate-limit error message. Returns null if not found.</summary>
        private static TimeSpan? TryParseRetryAfterFromMessage(string? message)
        {
            if (string.IsNullOrEmpty(message)) return null;
            var m = _retryAfterRegex.Match(message);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out var sec)) return null;
            var wait = TimeSpan.FromSeconds(Math.Clamp(sec, 2, 60));
            return wait;
        }

        private static bool IsRateLimitError(string? message)
            => !string.IsNullOrEmpty(message) && (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) || message.Contains("token rate limit", StringComparison.OrdinalIgnoreCase));

        private static TimeSpan PickRetryDelay(RequestFailedException ex, int attempt)
        {
            var wait = TimeSpan.FromSeconds(Math.Min(30, 2 + attempt * 3));
            var m = _retryAfterRegex.Match(ex.Message ?? "");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var sec))
                wait = TimeSpan.FromSeconds(Math.Max(sec, (int)wait.TotalSeconds));
            return wait;
        }

        private async Task WaitForNoActiveRunAsync(string threadId, CancellationToken ct, TimeSpan? maxWait = null)
        {
            var deadline = DateTime.UtcNow + (maxWait ?? TimeSpan.FromMinutes(2));
            while (true)
            {
                var runs = AgentsClient.Runs.GetRuns(threadId);
                var active = runs.Any(r => IsActive(r.Status));
                if (!active) return;
                if (DateTime.UtcNow >= deadline) return;
                await Task.Delay(350, ct);
            }
        }

        /// <summary>
        /// Cancel any active (non-terminal) runs on the thread so messages can be added.
        /// Uses CancellationToken.None so it runs even when the request was canceled.
        /// </summary>
        private void CancelActiveRuns(string threadId)
        {
            try
            {
                var runs = AgentsClient.Runs.GetRuns(threadId);
                foreach (var r in runs)
                {
                    if (!IsActive(r.Status)) continue;
                    try
                    {
                        AgentsClient.Runs.CancelRun(threadId, r.Id);
                        _logger.LogInformation("Cancelled active run {RunId} for thread {ThreadId} so fallback message can be added", r.Id, threadId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cancel run {RunId} for thread {ThreadId}", r.Id, threadId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get or cancel runs for thread {ThreadId}", threadId);
            }
        }

        public string EnsureThreadId(ISession session)
        {
            var id = session.GetString(SessionKeyThreadId);
            if (!string.IsNullOrWhiteSpace(id)) return id;

            var threadResp = AgentsClient.Threads.CreateThread();
            var thread = threadResp.Value;
            session.SetString(SessionKeyThreadId, thread.Id);
            return thread.Id;
        }

        public async Task EnsureGreetingAsync(ISession session, string greeting, CancellationToken ct)
        {
            var threadId = EnsureThreadId(session);
            var hasAny = AgentsClient.Messages.GetMessages(threadId).Any();
            if (!hasAny)
            {
                AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, greeting);
            }

            session.SetInt32(SessionKeyGreeted, 1);
            await Task.CompletedTask;
        }

        public (string ThreadId, IEnumerable<DisplayMessage> Messages) GetTranscript(string threadId)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();

            var list = new List<DisplayMessage>();
            var messages = AgentsClient.Messages.GetMessages(threadId);

            foreach (var m in messages.OrderBy(m => m.CreatedAt))
            {
                var parts = m.ContentItems
                    .OfType<Azure.AI.Agents.Persistent.MessageTextContent>()
                    .Select(t => t.Text ?? string.Empty)
                    .ToList();

                // IMPORTANT: keep paragraph boundaries so lists work
                var full = string.Join("\n\n", parts);

                // Convert to HTML for the UI
                var html = Markdown.ToHtml(full, pipeline);

                list.Add(new DisplayMessage
                {
                    Role = m.Role.ToString(),
                    Timestamp = m.CreatedAt,
                    Parts = parts,
                    FullText = full,
                    Html = html
                });
            }

            var sanitized = SuppressDuplicateLaptopQuestions(list, pipeline);
            return (threadId, sanitized);
        }

        private IEnumerable<DisplayMessage> SuppressDuplicateLaptopQuestions(IEnumerable<DisplayMessage> messages, MarkdownPipeline pipeline)
        {
            var filtered = new List<DisplayMessage>();
            var state = new LaptopAnswerState();

            foreach (var message in messages)
            {
                if (string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    var signals = _extraction.DetectLaptopAnswerSignals(message.FullText);
                    state.Apply(signals);
                    filtered.Add(message);
                    continue;
                }

                var text = message.FullText ?? string.Empty;
                var looksLikeOwnershipQuestion = LooksLikeLaptopOwnershipQuestion(text);
                var looksLikePreferenceQuestion = LooksLikeLaptopPreferenceQuestion(text);
                var standaloneLaptopPrompt = IsStandaloneLaptopPrompt(text);

                if (looksLikeOwnershipQuestion && standaloneLaptopPrompt && state.OwnershipAnswered)
                {
                    _logger.LogInformation("[LAPTOP_DEDUPE] Suppressing duplicate ownership question: {Text}", text);
                    // #region agent log
                    EmitToolLoopDebugLog(
                        "transcript-sanitize",
                        "H7",
                        "AzureAgentChatService.cs:laptop-dedupe",
                        "Suppressed duplicate laptop ownership prompt",
                        new
                        {
                            standaloneLaptopPrompt,
                            state.OwnershipAnswered,
                            state.NeedsProvidedLaptop,
                            state.PreferenceAnswered,
                            preview = TruncateForDebug(text, 240)
                        });
                    // #endregion
                    continue;
                }

                // Preference dedupe only applies AFTER ownership is explicitly answered.
                if (looksLikePreferenceQuestion
                    && standaloneLaptopPrompt
                    && state.OwnershipAnswered
                    && (!state.NeedsProvidedLaptop || state.PreferenceAnswered))
                {
                    _logger.LogInformation("[LAPTOP_DEDUPE] Suppressing duplicate/inapplicable preference question: {Text}", text);
                    // #region agent log
                    EmitToolLoopDebugLog(
                        "transcript-sanitize",
                        "H7",
                        "AzureAgentChatService.cs:laptop-dedupe",
                        "Suppressed duplicate/inapplicable laptop preference prompt",
                        new
                        {
                            standaloneLaptopPrompt,
                            state.OwnershipAnswered,
                            state.NeedsProvidedLaptop,
                            state.PreferenceAnswered,
                            preview = TruncateForDebug(text, 240)
                        });
                    // #endregion
                    continue;
                }

                if (looksLikePreferenceQuestion && !standaloneLaptopPrompt)
                {
                    // #region agent log
                    EmitToolLoopDebugLog(
                        "transcript-sanitize",
                        "H7",
                        "AzureAgentChatService.cs:laptop-dedupe",
                        "Kept assistant message because laptop preference prompt is part of broader AV response",
                        new
                        {
                            standaloneLaptopPrompt,
                            state.OwnershipAnswered,
                            state.NeedsProvidedLaptop,
                            state.PreferenceAnswered,
                            preview = TruncateForDebug(text, 240)
                        });
                    // #endregion
                }

                filtered.Add(message);
            }

            // Ensure HTML remains correct if we touched text-only fields in future edits.
            foreach (var msg in filtered.Where(m => string.IsNullOrWhiteSpace(m.Html) && !string.IsNullOrWhiteSpace(m.FullText)))
            {
                msg.Html = Markdown.ToHtml(msg.FullText, pipeline);
            }

            return filtered;
        }

        private static bool LooksLikeLaptopOwnershipQuestion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.ToLowerInvariant();
            return normalized.Contains("bringing your own laptop")
                   || normalized.Contains("bring your own laptop")
                   || normalized.Contains("do you need one")
                   || normalized.Contains("own laptop or do you need one");
        }

        private static bool LooksLikeLaptopPreferenceQuestion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.ToLowerInvariant();
            return normalized.Contains("windows or mac")
                   || normalized.Contains("windows or a mac")
                   || normalized.Contains("would you prefer windows or mac");
        }

        private static bool IsStandaloneLaptopPrompt(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();
            if (!normalized.Contains('?')) return false;

            // If the prompt also discusses broader AV topics, do not suppress it.
            var hasOtherAvContent = Regex.IsMatch(
                normalized,
                @"\b(video conferencing|video conference|zoom|teams|projector|screen|display|camera|microphone|speaker|flipchart|clicker|av equipment)\b",
                RegexOptions.IgnoreCase);
            if (hasOtherAvContent) return false;

            var nonEmptyLines = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;
            if (nonEmptyLines > 2) return false;

            return normalized.Length <= 220;
        }

        private static string TruncateForDebug(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength] + "...";
        }

        private int CountAssistantMessagesRaw(string threadId)
        {
            var messages = AgentsClient.Messages.GetMessages(threadId);
            return messages.Count(m => IsAssistantRole(m.Role.ToString()));
        }


        public async Task<SendAsyncResult> SendAsync(ISession session, string userText, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userText))
                return new SendAsyncResult(Enumerable.Empty<DisplayMessage>(), false);

            var threadId = EnsureThreadId(session);
            var gate = GetThreadGate(threadId);
            await gate.WaitAsync(ct);
            try
            {
                // Clear pending galleries from previous request to ensure fresh tracking
                session.Remove(SessionKeyPendingGalleries);
                StartGuardFollowUpTracking(session);
                
                // Handle schedule selection using TimePickerService - reformat for AI agent to continue the conversation
                if (_timePicker.TryParseScheduleSelection(userText.Trim(), out var schedule))
                {
                    _timePicker.SaveScheduleToDraft(threadId, schedule);
                    
                    // Find the date from current transcript
                    var (_, current) = GetTranscript(threadId);
                    var (dateDto, _) = _chatExtraction.ExtractEventDate(current);

                    // Save date and mark as confirmed when schedule is submitted via time picker
                    if (dateDto.HasValue)
                    {
                        session.SetString("Draft:EventDate", dateDto.Value.ToString("yyyy-MM-dd"));
                        session.SetString("Draft:DateConfirmed", "1");
                        _logger.LogInformation("[DATE CONFIRM] Date confirmed via schedule selection: {EventDate}", dateDto.Value.ToString("yyyy-MM-dd"));
                    }

                    // Reformat the message for AI agent to continue the conversation
                    var confirmation = _timePicker.BuildScheduleConfirmation(schedule, dateDto);
                    userText = $"I've selected this schedule: {confirmation.Replace("✅ Perfect! I've confirmed your schedule", "").Replace(".", "")}. Please confirm this schedule.";
                    _logger.LogInformation("Schedule selection reformatted for AI agent: {UserText}", userText);
                }
                // Handle multi-part schedule selection - parse it and reformat for AI agent
                else if (_timePicker.TryParseMultiScheduleSelection(userText.Trim(), out var multiSchedule))
                {
                    // Save the schedule times to session so the time picker can display them
                    SaveMultiScheduleToSession(multiSchedule);
                    
                    // Reformat the message to be more natural for the AI agent
                    var readableSchedule = _timePicker.BuildMultiScheduleConfirmation(multiSchedule);
                    userText = $"I've selected this schedule: {readableSchedule.Replace("✅ Perfect! I've confirmed your schedule", "").Replace(".", "")}. Please confirm this schedule.";
                    _logger.LogInformation("Multi-schedule selection reformatted for AI agent: {UserText}", userText);
                }

                // Normal flow for free-text / other UI messages
                var priorRunId = session.GetString(SessionKeyActiveRunId);
                if (!string.IsNullOrWhiteSpace(priorRunId))
                {
                    _logger.LogInformation("New message received while run {RunId} was marked active in session. Waiting briefly then cancelling if needed.", priorRunId);
                    await WaitForRunToCompleteAsync(threadId, priorRunId, ct, TimeSpan.FromSeconds(5));
                    
                    // If still active after wait, cancel it
                    var checkRun = await With429RetryAsync(() => Task.FromResult(AgentsClient.Runs.GetRun(threadId, priorRunId).Value), "CheckPriorRun", ct);
                    if (IsActive(checkRun.Status))
                    {
                        _logger.LogWarning("Prior run {RunId} still active after wait, cancelling.", priorRunId);
                        AgentsClient.Runs.CancelRun(threadId, priorRunId);
                        await Task.Delay(1000, ct);
                    }
                }

                // Ensure no other runs are active on this thread before adding a new message
                await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(5));
                
                // If still active, cancel them so the new message can be processed
                var activeRuns = AgentsClient.Runs.GetRuns(threadId).Where(r => IsActive(r.Status)).ToList();
                if (activeRuns.Any())
                {
                    _logger.LogWarning("Thread {ThreadId} still has {Count} active runs. Cancelling them to process new message.", threadId, activeRuns.Count);
                    CancelActiveRuns(threadId);
                    await Task.Delay(2000, ct); // Increased wait for cancellation to propagate
                    await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(5)); // Final check
                }

                // CRITICAL: Always add user message FIRST before any processing
                // This ensures the user's message appears in the conversation
                try
                {
                    AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.User, userText.Trim());
                    _logger.LogInformation("User message added to thread {ThreadId}: {UserText}", threadId, userText.Trim().Length > 100 ? userText.Trim().Substring(0, 100) + "..." : userText.Trim());
                    
                    // Small delay to allow Azure to process the message before we potentially start a run or get transcript
                    await Task.Delay(500, ct);
                }
                catch (RequestFailedException ex) when (ex.Status == 400 && ex.Message.Contains("run") && ex.Message.Contains("active"))
                {
                    _logger.LogWarning("Active run detected on server despite cancellation attempts, retrying one last time after wait");
                    CancelActiveRuns(threadId);
                    await Task.Delay(2000, ct);
                    await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(5));
                    AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.User, userText.Trim());
                    await Task.Delay(500, ct);
                }

                // Two-phase contact save: show "One moment please" immediately, then run agent in Continue
                // Only trigger once per conversation — skip if contact save already completed
                var (_, messagesAfterUser) = GetTranscript(threadId);
                var messagesAfterUserList = messagesAfterUser as IList<DisplayMessage> ?? messagesAfterUser.ToList();
                var assistantMessagesBeforeRun = CountAssistantMessagesRaw(threadId);
                // Lead portal: contact already exists on WestinLeads — never use two-phase save (avoids extra
                // Continue round that often fails with "temporary issue" and confused ordering vs venue form).
                var isLeadEntry = string.Equals(session.GetString("Draft:EntrySource"), "lead", StringComparison.OrdinalIgnoreCase);
                if (session.GetString(SessionKeyContactSaveCompleted) != "1"
                    && !isLeadEntry
                    && !IsStructuredWizardUserText(userText)
                    && ShouldShowContactSavePending(messagesAfterUserList, session))
                {
                    AddContactSavePendingMessage(threadId);
                    session.SetString(SessionKeyContactSavePending, "1");
                    var (_, partialMessages) = GetTranscript(threadId);
                    _logger.LogInformation("Contact save detected for thread {ThreadId}; returning partial, Continue needed", threadId);
                    return new SendAsyncResult(partialMessages, true);
                }

                // ALWAYS RUN THE AI AGENT - No more early returns that skip the agent!
                // The AI agent should ALWAYS respond to user messages
                _logger.LogInformation("Running AI agent for thread {ThreadId}", threadId);
                var agent = AgentsClient.Administration.GetAgent(_agentId).Value;
                
                Azure.AI.Agents.Persistent.ThreadRun? run = null;
                bool agentResponded = false;
                int maxRetries = 5; // Allow more retries so rate-limit waits can succeed
                int currentRetry = 0;

                while (currentRetry <= maxRetries && !agentResponded)
                {
                    if (currentRetry > 0)
                    {
                        var errMsg = run?.LastError?.Message;
                        var waitSpan = TimeSpan.FromSeconds(1);
                        if (IsRateLimitError(errMsg) && TryParseRetryAfterFromMessage(errMsg) is { } suggested)
                        {
                            waitSpan = suggested;
                            _logger.LogInformation("Rate limit hit. Waiting {Seconds}s (Azure suggested) before retry (Attempt {Attempt}/{MaxRetries}) for thread {ThreadId}", (int)waitSpan.TotalSeconds, currentRetry + 1, maxRetries + 1, threadId);
                        }
                        else
                        {
                            _logger.LogInformation("Retrying AI agent run (Attempt {Attempt}/{MaxRetries}) for thread {ThreadId}", currentRetry + 1, maxRetries + 1, threadId);
                        }
                        await Task.Delay(waitSpan, ct);
                        CancelActiveRuns(threadId);
                        await Task.Delay(1000, ct);
                    }

                    try
                    {
                        run = await RunAgentAndHandleToolsAsync(threadId, agent.Id, ct);
                        agentResponded = run.Status == Azure.AI.Agents.Persistent.RunStatus.Completed;
                        
                        if (!agentResponded)
                        {
                            var err = run.LastError?.Message ?? run.Status.ToString();
                            _logger.LogWarning("AI agent run did not complete normally (Attempt {Attempt}): {Status} - {Error} for thread {ThreadId}", currentRetry + 1, run.Status, err, threadId);
                        }
                    }
                    catch (Exception agentEx)
                    {
                        _logger.LogError(agentEx, "AI agent run failed (Attempt {Attempt}) for thread {ThreadId}", currentRetry + 1, threadId);
                    }
                    
                    currentRetry++;
                }
                
                if (!agentResponded)
                {
                    _logger.LogWarning("Agent failed to respond for thread {ThreadId}. Ensuring fallback reply if no assistant delta exists.", threadId);
                    await EnsureAssistantFallbackIfNoDeltaAsync(
                        session,
                        threadId,
                        assistantMessagesBeforeRun,
                        "send_async_non_completed",
                        ct);
                }
                else
                {
                    // Run completed but may have only processed tool calls without
                    // producing a visible assistant message.  Re-run the agent so the
                    // user always sees a reply.
                    var (_, postRunMessages) = GetTranscript(threadId);
                    var postRunAssistantCountSanitized = CountAssistantMessages(postRunMessages);
                    var postRunAssistantCountRaw = CountAssistantMessagesRaw(threadId);
                    // #region agent log
                    EmitToolLoopDebugLog(
                        run?.Id ?? $"thread:{threadId}",
                        "H8",
                        "AzureAgentChatService.cs:no-delta-counts",
                        "Assistant count snapshot before no-delta retries",
                        new
                        {
                            assistantMessagesBeforeRun,
                            postRunAssistantCountRaw,
                            postRunAssistantCountSanitized
                        });
                    // #endregion
                    const int maxNoDeltaRetries = 2;

                    for (int noDeltaRetry = 0;
                         noDeltaRetry < maxNoDeltaRetries && postRunAssistantCountRaw <= assistantMessagesBeforeRun;
                         noDeltaRetry++)
                    {
                        if (HasGuardFollowUpPendingForCurrentTurn(session))
                        {
                            _logger.LogInformation(
                                "[CHAT_FLOW] Skipping no-delta rerun for thread {ThreadId} because a guard follow-up prompt is already pending for this turn.",
                                threadId);
                            break;
                        }

                        _logger.LogWarning(
                            "[CHAT_FLOW] Run completed but no assistant delta on thread {ThreadId}. Re-running agent (attempt {Attempt}/{Max}).",
                            threadId, noDeltaRetry + 1, maxNoDeltaRetries);

                        try
                        {
                            CancelActiveRuns(threadId);
                            await Task.Delay(500, ct);
                            run = await RunAgentAndHandleToolsAsync(threadId, agent.Id, ct);

                            (_, postRunMessages) = GetTranscript(threadId);
                            postRunAssistantCountRaw = CountAssistantMessagesRaw(threadId);
                            postRunAssistantCountSanitized = CountAssistantMessages(postRunMessages);
                            // #region agent log
                            EmitToolLoopDebugLog(
                                run?.Id ?? $"thread:{threadId}",
                                "H8",
                                "AzureAgentChatService.cs:no-delta-retry-counts",
                                "Assistant count snapshot after no-delta retry",
                                new
                                {
                                    attempt = noDeltaRetry + 1,
                                    assistantMessagesBeforeRun,
                                    postRunAssistantCountRaw,
                                    postRunAssistantCountSanitized
                                });
                            // #endregion
                        }
                        catch (Exception rerunEx)
                        {
                            _logger.LogError(rerunEx,
                                "[CHAT_FLOW] Re-run attempt {Attempt} failed for thread {ThreadId}.",
                                noDeltaRetry + 1, threadId);
                            break;
                        }
                    }

                    if (postRunAssistantCountRaw <= assistantMessagesBeforeRun)
                    {
                        _logger.LogError(
                            "[CHAT_FLOW] Agent completed but still no assistant delta after {MaxRetries} re-runs on thread {ThreadId}. Injecting fallback.",
                            maxNoDeltaRetries, threadId);
                        await EnsureAssistantFallbackIfNoDeltaAsync(
                            session,
                            threadId,
                            assistantMessagesBeforeRun,
                            "send_async_completed_no_delta",
                            ct);
                    }
                }

                var (_, messages) = GetTranscript(threadId);

                // Inject any missing gallery content that the AI didn't output
                try { await InjectMissingGalleriesAsync(threadId, messages, ct); } catch (Exception ex) { _logger.LogWarning(ex, "Gallery injection failed (non-fatal)"); }

                // If room+date known but time missing AND Isla just asked for time, append a timepicker
                try { await MaybeAppendTimePickerAsync(threadId, messages, ct); } catch { /* non-fatal */ }

                // Optional post-processing - NOW USING ORCHESTRATION SERVICE
                try { await PostProcessAfterRunAsync(messages, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Post-processing (contact/booking) failed."); }

                var (_, finalMessages) = GetTranscript(threadId);
                _logger.LogInformation("Returning {Count} messages from SendAsync for thread {ThreadId}", finalMessages.Count(), threadId);
                return new SendAsyncResult(finalMessages, false);
            }
            catch (OperationCanceledException)
            {
                // User refreshed or request was cancelled. Cancel active runs so the next request can add messages.
                _logger.LogWarning("Request cancelled for thread {ThreadId}. Cancelling active runs so next request can proceed.", threadId);
                CancelActiveRuns(threadId);
                throw;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Continues after a partial contact-save response. User message and "One moment please" are already in thread.
        /// Runs the agent and PostProcess, then returns the full transcript.
        /// </summary>
        public async Task<IEnumerable<DisplayMessage>> SendAsyncContinue(ISession session, CancellationToken ct)
        {
            if (session.GetString(SessionKeyContactSavePending) != "1")
            {
                _logger.LogWarning("SendAsyncContinue called but ContactSavePending not set");
                var threadId = EnsureThreadId(session);
                var (_, messages) = GetTranscript(threadId);
                return messages;
            }

            var threadId2 = EnsureThreadId(session);
            var gate = GetThreadGate(threadId2);
            await gate.WaitAsync(ct);
            try
            {
                session.Remove(SessionKeyContactSavePending);
                StartGuardFollowUpTracking(session);
                var (_, messagesBeforeContinue) = GetTranscript(threadId2);
                var assistantMessagesBeforeContinue = CountAssistantMessagesRaw(threadId2);

                var agent = AgentsClient.Administration.GetAgent(_agentId).Value;
                var run = await RunAgentAndHandleToolsAsync(threadId2, agent.Id, ct);
                var runCompleted = run.Status == Azure.AI.Agents.Persistent.RunStatus.Completed;
                var fallbackAdded = false;

                if (!runCompleted)
                {
                    _logger.LogWarning("Agent run did not complete in SendAsyncContinue: {Status}", run.Status);
                    fallbackAdded = await EnsureAssistantFallbackIfNoDeltaAsync(
                        session,
                        threadId2,
                        assistantMessagesBeforeContinue,
                        "send_async_continue_non_completed",
                        ct);
                }

                var (_, messages) = GetTranscript(threadId2);

                try { await InjectMissingGalleriesAsync(threadId2, messages, ct); } catch (Exception ex) { _logger.LogWarning(ex, "Gallery injection failed (non-fatal)"); }
                try { await MaybeAppendTimePickerAsync(threadId2, messages, ct); } catch { /* non-fatal */ }

                try { await PostProcessAfterRunAsync(messages, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Post-processing (contact/booking) failed."); }

                if (runCompleted || fallbackAdded)
                {
                    session.SetString(SessionKeyContactSaveCompleted, "1");
                }

                var (_, finalMessages) = GetTranscript(threadId2);
                _logger.LogInformation("SendAsyncContinue returning {Count} messages for thread {ThreadId}", finalMessages.Count(), threadId2);
                return finalMessages;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Handle user questions before proceeding with normal flow
        /// </summary>
        private async Task<string?> HandleQuestionAsync(string threadId, QuestionInfo questionInfo, CancellationToken ct)
        {
            switch (questionInfo.QuestionType)
            {
                case QuestionType.RoomSetup:
                    if (!string.IsNullOrWhiteSpace(questionInfo.Context.RoomName))
                    {
                        // Get room information and provide recommendation
                        try
                        {
                            var roomInfo = await _toolHandler.HandleToolCallAsync("list_westin_rooms", "{}", threadId, ct);
                            if (!string.IsNullOrEmpty(roomInfo))
                            {
                                var rooms = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(roomInfo);
                                if (rooms != null && rooms.ContainsKey("rooms"))
                                {
                                    var roomList = rooms["rooms"] as IEnumerable<object>;
                                    var room = roomList?.FirstOrDefault(r =>
                                    {
                                        if (r is System.Text.Json.JsonElement elem &&
                                            elem.TryGetProperty("name", out var name) &&
                                            name.GetString()?.Contains(questionInfo.Context.RoomName, StringComparison.OrdinalIgnoreCase) == true)
                                            return true;
                                        return false;
                                    });

                                    if (room != null)
                                    {
                                        return _acknowledgment.GenerateRoomSetupAcknowledgment(
                                            questionInfo.Context.RoomName,
                                            "classroom setup with capacity for presentations");
                                    }
                                }
                            }
                        }
                        catch { /* Fall through to default */ }
                    }
                    return $"For room setup suggestions, I can help you choose the optimal configuration. What room will you be using?";

                case QuestionType.Equipment:
                    return $"For equipment recommendations, I can suggest the best setup for your event. Let me ask a few questions about your needs first.";

                default:
                    return $"I'd be happy to help answer your question. Let me gather some information about your event first.";
            }
        }

        private static int CountAssistantMessages(IEnumerable<DisplayMessage> messages)
            => messages.Count(m => IsAssistantRole(m.Role));

        private static bool IsAssistantRole(string? role)
            => string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "agent", StringComparison.OrdinalIgnoreCase);

        private void StartGuardFollowUpTracking(ISession session)
        {
            session.SetString(SessionKeyCurrentTurnId, Guid.NewGuid().ToString("N"));
            session.Remove(SessionKeyGuardFollowUpTurnId);
            session.Remove(SessionKeyGuardFollowUpFingerprint);
            session.Remove(SessionKeyGuardFollowUpText);
        }

        private bool HasGuardFollowUpPendingForCurrentTurn(ISession session)
        {
            var currentTurnId = session.GetString(SessionKeyCurrentTurnId);
            if (string.IsNullOrWhiteSpace(currentTurnId))
                return false;

            return string.Equals(session.GetString(SessionKeyGuardFollowUpTurnId), currentTurnId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(session.GetString(SessionKeyGuardFollowUpText));
        }

        private async Task<bool> EnsureAssistantFallbackIfNoDeltaAsync(
            ISession session,
            string threadId,
            int assistantMessagesBeforeTurn,
            string context,
            CancellationToken ct)
        {
            var (_, currentMessages) = GetTranscript(threadId);
            var currentAssistantCount = CountAssistantMessages(currentMessages);
            if (currentAssistantCount > assistantMessagesBeforeTurn)
            {
                return false;
            }

            var fallbackText = session.GetString(SessionKeyGuardFollowUpText);
            if (string.IsNullOrWhiteSpace(fallbackText)
                || !string.Equals(session.GetString(SessionKeyGuardFollowUpTurnId), session.GetString(SessionKeyCurrentTurnId), StringComparison.Ordinal))
            {
                fallbackText = "Sorry, I hit a temporary issue and could not complete that step. Please send your last message again and I will continue.";
            }

            try
            {
                CancelActiveRuns(threadId);
                await AppendAssistantMessageAsync(session, fallbackText, ct);
                _logger.LogWarning("Appended fallback assistant message for thread {ThreadId}. Context={Context}", threadId, context);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append fallback assistant message for thread {ThreadId}. Context={Context}", threadId, context);
                return false;
            }
        }

        /// <summary>
        /// Check if user message is asking a question (not providing new information)
        /// </summary>
        private bool IsUserAskingQuestion(string userText)
        {
            if (string.IsNullOrWhiteSpace(userText)) return false;
            
            var text = userText.Trim().ToLowerInvariant();
            
            // Check for question words and patterns
            return text.Contains("?") ||
                   text.StartsWith("what ") || text.StartsWith("how ") || text.StartsWith("when ") ||
                   text.StartsWith("where ") || text.StartsWith("why ") || text.StartsWith("which ") ||
                   text.StartsWith("who ") || text.StartsWith("can you ") || text.StartsWith("could you ") ||
                   text.StartsWith("have you ") || text.StartsWith("did you ") || text.StartsWith("do you ") ||
                   text.StartsWith("will you ") || text.StartsWith("would you ") || text.StartsWith("should ") ||
                   text.Contains("confirm") && text.Contains("?");
        }

        /// <summary>
        /// Get previously acknowledged event information from session
        /// </summary>
        private EventInformation GetPreviouslyAcknowledgedInfo(ISession session)
        {
            var info = new EventInformation();
            
            // Retrieve previously acknowledged values from session
            var budgetStr = session.GetString("Ack:Budget");
            if (!string.IsNullOrWhiteSpace(budgetStr) && decimal.TryParse(budgetStr, out var budget))
                info.Budget = budget;
            
            var attendeesStr = session.GetString("Ack:Attendees");
            if (!string.IsNullOrWhiteSpace(attendeesStr) && int.TryParse(attendeesStr, out var attendees))
                info.Attendees = attendees;
            
            info.SetupStyle = session.GetString("Ack:SetupStyle");
            info.Venue = session.GetString("Ack:Venue");
            info.SpecialRequests = session.GetString("Ack:SpecialRequests");
            
            var datesJson = session.GetString("Ack:Dates");
            if (!string.IsNullOrWhiteSpace(datesJson))
            {
                try
                {
                    info.Dates = System.Text.Json.JsonSerializer.Deserialize<List<string>>(datesJson);
                }
                catch { /* ignore */ }
            }
            
            return info;
        }

        /// <summary>
        /// Check if current event information contains NEW information compared to previously acknowledged
        /// </summary>
        private bool HasNewInformation(EventInformation current, EventInformation previous)
        {
            // Check each field - if current has a value that previous doesn't, it's new
            if (current.Budget.HasValue && !previous.Budget.HasValue)
                return true;
            
            if (current.Attendees.HasValue && !previous.Attendees.HasValue)
                return true;
            
            if (!string.IsNullOrWhiteSpace(current.SetupStyle) && string.IsNullOrWhiteSpace(previous.SetupStyle))
                return true;
            
            if (!string.IsNullOrWhiteSpace(current.Venue) && string.IsNullOrWhiteSpace(previous.Venue))
                return true;
            
            if (!string.IsNullOrWhiteSpace(current.SpecialRequests) && string.IsNullOrWhiteSpace(previous.SpecialRequests))
                return true;
            
            // Check for new dates
            if (current.Dates != null && current.Dates.Count > 0)
            {
                if (previous.Dates == null || previous.Dates.Count == 0)
                    return true;
                
                // Check if any date in current is not in previous
                foreach (var date in current.Dates)
                {
                    if (!previous.Dates.Contains(date))
                        return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Save acknowledged event information to session to prevent duplicate acknowledgments
        /// </summary>
        private void SaveAcknowledgedInfo(ISession session, EventInformation info)
        {
            if (info.Budget.HasValue)
                session.SetString("Ack:Budget", info.Budget.Value.ToString());
            
            if (info.Attendees.HasValue)
                session.SetString("Ack:Attendees", info.Attendees.Value.ToString());
            
            if (!string.IsNullOrWhiteSpace(info.SetupStyle))
                session.SetString("Ack:SetupStyle", info.SetupStyle);
            
            if (!string.IsNullOrWhiteSpace(info.Venue))
                session.SetString("Ack:Venue", info.Venue);
            
            if (!string.IsNullOrWhiteSpace(info.SpecialRequests))
                session.SetString("Ack:SpecialRequests", info.SpecialRequests);
            
            if (info.Dates != null && info.Dates.Count > 0)
            {
                var datesJson = System.Text.Json.JsonSerializer.Serialize(info.Dates);
                session.SetString("Ack:Dates", datesJson);
            }
        }


        #endregion


        #region Core agent run loop + tools
        // Turn a relative path (/images/...) into an absolute URL for the current host (incl. PathBase)
        // If it's already absolute, return as-is.
        private string ToAbsoluteLocalUrl(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path ?? string.Empty;
            if (Uri.IsWellFormedUriString(path, UriKind.Absolute)) return path;

            var req = _http.HttpContext?.Request;
            if (req == null) return path; // fallback: leave relative

            var pathBase = req.PathBase.HasValue ? req.PathBase.ToString() : string.Empty;
            var rel = path.StartsWith("/") ? path : "/" + path;
            return $"{req.Scheme}://{req.Host}{pathBase}{rel}";
        }

        private static bool IsTerminal(Azure.AI.Agents.Persistent.RunStatus s)
            => s == Azure.AI.Agents.Persistent.RunStatus.Completed || s == Azure.AI.Agents.Persistent.RunStatus.Failed || s == Azure.AI.Agents.Persistent.RunStatus.Cancelled || s == Azure.AI.Agents.Persistent.RunStatus.Expired;

        #endregion
    }
}
