// AzureAgentChatService.cs
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using Markdig;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services
{
    public sealed class AzureAgentChatService
    {
        private const string SessionKeyThreadId = "AgentThreadId";
        private const string SessionKeyGreeted = "IslaGreeted";
        private const string SessionKeyActiveRunId = "AgentActiveRunId";
        
        private readonly AIProjectClient _projectClient;
        private readonly string _agentId;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AzureAgentChatService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly BookingDbContext _bookings;
        
        // Modular services (extracted from this monolith)
        private readonly BookingOrchestrationService _orchestration;
        private readonly AgentToolHandlerService _toolHandler;
        private readonly TimePickerService _timePicker;
        private readonly QuoteGenerationService _quoteGen;
        
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _threadLocks = new();
        private static SemaphoreSlim GetThreadGate(string threadId)
            => _threadLocks.GetOrAdd(threadId, _ => new SemaphoreSlim(1, 1));

        public AzureAgentChatService(
            AIProjectClient projectClient,
            IOptions<AzureAgentOptions> options,
            BookingDbContext bookings,
            IHttpContextAccessor http,
            IWebHostEnvironment env,
            ILogger<AzureAgentChatService> logger,
            BookingOrchestrationService orchestration,
            AgentToolHandlerService toolHandler,
            TimePickerService timePicker,
            QuoteGenerationService quoteGen)
        {
            _projectClient = projectClient;
            _agentId = options.Value.AgentId ?? throw new ArgumentNullException(nameof(options.Value.AgentId));
            _bookings = bookings;
            _http = http;
            _env = env;
            _logger = logger;
            _orchestration = orchestration;
            _toolHandler = toolHandler;
            _timePicker = timePicker;
            _quoteGen = quoteGen;
        }

        private PersistentAgentsClient AgentsClient => _projectClient.GetPersistentAgentsClient();

        #region Controller-facing API

        private static bool IsActive(Azure.AI.Agents.Persistent.RunStatus s)
            => s == Azure.AI.Agents.Persistent.RunStatus.Queued || s == Azure.AI.Agents.Persistent.RunStatus.InProgress || s == Azure.AI.Agents.Persistent.RunStatus.RequiresAction;

        private static readonly Regex _retryAfterRegex =
            new Regex(@"\b(\d+)\s*seconds?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            if (session.GetInt32(SessionKeyGreeted) == 1) return;

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
                    Parts = parts,
                    FullText = full,
                    Html = html
                });
            }

            return (threadId, list);
        }


        public async Task<IEnumerable<DisplayMessage>> SendAsync(ISession session, string userText, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userText)) return Enumerable.Empty<DisplayMessage>();

            var threadId = EnsureThreadId(session);
            var gate = GetThreadGate(threadId);
            await gate.WaitAsync(ct);
            try
            {
                // Handle schedule selection using TimePickerService
                if (_timePicker.TryParseScheduleSelection(userText.Trim(), out var schedule))
                {
                    _timePicker.SaveScheduleToDraft(threadId, schedule);

                    // Find the date from current transcript
                    var (_, current) = GetTranscript(threadId);
                    var (dateDto, _) = ExtractEventDate(current);

                    // Post confirmation
                    var confirmation = _timePicker.BuildScheduleConfirmation(schedule, dateDto);
                    AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, confirmation);

                    var (_, finalNow) = GetTranscript(threadId);
                    return finalNow;
                }

                // Normal flow for free-text / other UI messages
                var priorRunId = session.GetString(SessionKeyActiveRunId);
                if (!string.IsNullOrWhiteSpace(priorRunId))
                    await WaitForRunToCompleteAsync(threadId, priorRunId, ct);

                await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(15));

                try
                {
                    AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.User, userText.Trim());
                }
                catch (RequestFailedException ex) when (ex.Status == 400 && ex.Message.Contains("run") && ex.Message.Contains("active"))
                {
                    await WaitForNoActiveRunAsync(threadId, ct, TimeSpan.FromSeconds(15));
                    AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.User, userText.Trim());
                }

                var agent = AgentsClient.Administration.GetAgent(_agentId).Value;
                var run = await RunAgentAndHandleToolsAsync(threadId, agent.Id, ct);

                if (run.Status != Azure.AI.Agents.Persistent.RunStatus.Completed)
                {
                    var err = run.LastError?.Message ?? run.Status.ToString();
                    throw new InvalidOperationException($"Run did not complete: {err}");
                }

                var (_, messages) = GetTranscript(threadId);

                // If room+date known but time missing AND Isla just asked for time, append a timepicker
                try { await MaybeAppendTimePickerAsync(threadId, messages, ct); } catch { /* non-fatal */ }

                // Optional post-processing - NOW USING ORCHESTRATION SERVICE
                try { await PostProcessAfterRunAsync(messages, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Post-processing (contact/booking) failed."); }

                var (_, finalMessages) = GetTranscript(threadId);
                return finalMessages;
            }
            finally
            {
                gate.Release();
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
        private async Task MaybeAppendTimePickerAsync(string threadId, IEnumerable<DisplayMessage> messages, CancellationToken ct)
        {
            // 1) Already shown?
            bool uiAlreadyShown = messages.Any(m =>
                (m.Parts ?? Enumerable.Empty<string>())
                    .Any(p => p.IndexOf("\"type\":\"timepicker\"", StringComparison.OrdinalIgnoreCase) >= 0
                           || p.IndexOf("\"type\":\"multitime\"", StringComparison.OrdinalIgnoreCase) >= 0));
            if (uiAlreadyShown) return;

            // 2) A single range already chosen?
            bool timeChosen = messages.Any(m =>
                (m.Parts ?? Enumerable.Empty<string>())
                    .Any(p => p.StartsWith("Choose time:", StringComparison.OrdinalIgnoreCase)));
            if (timeChosen) return;

            // >>> NEW: a multi-schedule already chosen?
            bool scheduleChosen = messages.Any(m =>
                (m.Parts ?? Enumerable.Empty<string>())
                    .Any(p => p.StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase)));
            if (scheduleChosen) return;

            // 3) Do we know the date?
            var (dateDto, _) = ExtractEventDate(messages);
            if (dateDto is null) return;
            var dateIso = dateDto.Value.ToString("yyyy-MM-dd");
            var prettyDate = dateDto.Value.ToString("d MMMM yyyy");

            // 4) Did Isla just ask for the time?
            var lastAssistant = messages.LastOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
            string lastAssistantText = string.Join("\n\n", lastAssistant?.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(lastAssistantText))
                return;

            // Check for various ways the AI might ask for time information
            var textLower = lastAssistantText.ToLowerInvariant();
            bool isAskingForTime = textLower.Contains("what time") ||
                                   textLower.Contains("start time") ||
                                   textLower.Contains("end time") ||
                                   textLower.Contains("when does") ||
                                   textLower.Contains("schedule") ||
                                   textLower.Contains("timing") ||
                                   textLower.Contains("setup time") ||
                                   textLower.Contains("pack");

            if (!isAskingForTime)
                return;

            // 5) Build the inline MULTI timepicker JSON (Setup, Rehearsal, Pack Up)
            var uiPayload = new
            {
                ui = new
                {
                    type = "multitime",
                    title = $"Confirm your schedule for {prettyDate}",
                    date = dateIso,
                    pickers = new[]
                    {
                new { name = "setup",     label = "Setup Time",     @default = "07:00" },
                new { name = "rehearsal", label = "Rehearsal Time", @default = "09:30" },
                new { name = "start",     label = "Event Start Time", @default = "10:00" },
                new { name = "end",       label = "Event End Time",   @default = "16:00" },
                new { name = "packup",    label = "Pack Up Time",   @default = "18:00" },
            },
                    submitLabel = "Submit"
                }
            };
            var uiJson = JsonSerializer.Serialize(uiPayload);

            var text = $"Here’s a time picker for you. Please confirm your schedule for **{prettyDate}**:\n\n" + uiJson;

            AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, text);
            await Task.CompletedTask;
        }


        private async Task<Azure.AI.Agents.Persistent.ThreadRun> RunAgentAndHandleToolsAsync(string threadId, string agentId, CancellationToken ct)
        {
            // Create the run with retry
            var run = await With429RetryAsync(
                () => Task.FromResult(AgentsClient.Runs.CreateRun(threadId, agentId).Value),
                "CreateRun", ct);

            try
            {
                _http.HttpContext?.Session.SetString(SessionKeyActiveRunId, run.Id);

                var startUtc = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(5);
                var delayMs = 500;          // start slightly higher
                var maxDelayMs = 7000;      // ramp polling up to ~7s

                while (true)
                {
                    // Poll with retry
                    run = await With429RetryAsync(
                        () => Task.FromResult(AgentsClient.Runs.GetRun(threadId, run.Id).Value),
                        "GetRun", ct);

                    if (IsTerminal(run.Status)) return run;

                    if (run.Status == Azure.AI.Agents.Persistent.RunStatus.RequiresAction && run.RequiredAction != null)
                    {
                        dynamic action = run.RequiredAction!;
                        IEnumerable<dynamic>? toolCalls = null;

                        try { toolCalls = (IEnumerable<dynamic>)action.ToolCalls; } catch { }
                        if (toolCalls == null) { try { toolCalls = (IEnumerable<dynamic>)action.SubmitToolOutputs.ToolCalls; } catch { } }
                        if (toolCalls == null) { try { toolCalls = (IEnumerable<dynamic>)action.SubmitToolOutputsAction.ToolCalls; } catch { } }

                        if (toolCalls == null)
                        {
                            _logger.LogWarning($"RequiredAction present but ToolCalls not found. Type={action.GetType().FullName}");
                        }
                        else
                        {
                            var outputs = new List<(string Id, string Output)>();

                            foreach (var call in toolCalls)
                            {
                                var toolCallId = (string)call.Id;
                                string name;
                                string argsJson;

                                try { name = (string)call.Function.Name; } catch { name = (string)call.Name; }
                                try { argsJson = (string)(call.Function.Arguments ?? "{}"); } catch { argsJson = (string)(call.Arguments ?? "{}"); }

                                try
                                {
                                    // Delegate tool handling to AgentToolHandlerService
                                    var handled = await _toolHandler.HandleToolCallAsync(name, argsJson, threadId, ct);
                                    if (handled != null)
                                    {
                                        outputs.Add((toolCallId, handled));
                                        continue;
                                    }

                                    // Handle remaining tools not yet extracted
                                    switch (name)
                                    {

                                        case "generate_quote":
                                            {
                                                // SAFEGUARD: Check if user explicitly confirmed they're ready AND equipment is confirmed
                                                bool userConfirmedReady = false;
                                                bool equipmentConfirmed = false;
                                                try
                                                {
                                                    using var doc = JsonDocument.Parse(argsJson ?? "{}");
                                                    if (doc.RootElement.TryGetProperty("userConfirmedReady", out var ucr))
                                                        userConfirmedReady = ucr.ValueKind == JsonValueKind.True;
                                                    if (doc.RootElement.TryGetProperty("equipmentConfirmed", out var ec))
                                                        equipmentConfirmed = ec.ValueKind == JsonValueKind.True;
                                                }
                                                catch { }

                                                // If equipment hasn't been confirmed, reject the quote generation
                                                if (!equipmentConfirmed)
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "Cannot generate quote - equipment has not been confirmed by the user.",
                                                        instruction = "STOP! You must follow this flow BEFORE generating a quote:\n" +
                                                            "1) Call 'get_equipment_recommendations' with the user's equipment requirements text.\n" +
                                                            "2) Show the user the equipment recommendations with pricing. Mention any packages available.\n" +
                                                            "3) Ask 'Does this equipment look correct? Any changes needed?'\n" +
                                                            "4) Wait for user to confirm (e.g., 'yes', 'looks good', 'that's right').\n" +
                                                            "5) Ask 'Shall I prepare your quote now?'\n" +
                                                            "6) Only after user explicitly confirms, call generate_quote with equipmentConfirmed=true and userConfirmedReady=true.\n\n" +
                                                            "You CANNOT skip showing equipment recommendations to the user!"
                                                    })));
                                                    break;
                                                }

                                                // If user hasn't confirmed ready, reject the quote generation
                                                if (!userConfirmedReady)
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "Cannot generate quote yet - user has not confirmed they are ready.",
                                                        instruction = "You showed equipment to the user but haven't asked if they're ready for the quote.\n" +
                                                            "Ask: 'Is there anything else you need, or shall I create your quote now?'\n" +
                                                            "Only call generate_quote with userConfirmedReady=true after user explicitly says yes."
                                                    })));
                                                    break;
                                                }

                                                // Get the booking number from session
                                                var session = _http.HttpContext?.Session;
                                                var bookingNo = session?.GetString("Draft:BookingNo");
                                                
                                                if (string.IsNullOrWhiteSpace(bookingNo))
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = "No booking found. Please ensure the booking has been created first.",
                                                        instruction = "A booking must be created before generating a quote. Make sure all booking details (date, time, venue, equipment) have been confirmed."
                                                    })));
                                                    break;
                                                }

                                                // Use QuoteGenerationService to generate PDF from booking data
                                                var (success, pdfUrl, error) = await _quoteGen.GenerateQuoteForBookingAsync(bookingNo, ct);
                                                
                                                if (!success || string.IsNullOrEmpty(pdfUrl))
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                    {
                                                        error = error ?? "Failed to generate quote PDF.",
                                                        bookingNo
                                                    })));
                                                    break;
                                                }

                                                // Build full URL
                                                var req = _http.HttpContext?.Request;
                                                var baseUrl = (req == null) ? "" : $"{req.Scheme}://{req.Host}";
                                                var fullQuoteUrl = $"{baseUrl}{pdfUrl}";

                                                outputs.Add((toolCallId, JsonSerializer.Serialize(new
                                                {
                                                    ui = new
                                                    {
                                                        quoteUrl = fullQuoteUrl,
                                                        bookingNo
                                                    },
                                                    message = $"Quote PDF generated successfully for booking {bookingNo}."
                                                })));
                                                break;
                                            }

                                        case "update_quote":
                                        case "save_contact":
                                        case "send_internal_followup":
                                            {
                                                outputs.Add((toolCallId, JsonSerializer.Serialize(new { ok = true })));
                                                break;
                                            }

                                        case "get_product_info":
                                            {
                                                var args = JsonSerializer.Deserialize<GetProductInfoArgs>(argsJson) ?? new GetProductInfoArgs();
                                                var take = Math.Clamp(args.take ?? 12, 1, 50);

                                                IQueryable<TblInvmas> q = _bookings.TblInvmas.AsNoTracking();

                                                if (!string.IsNullOrWhiteSpace(args.product_code))
                                                {
                                                    var code = args.product_code.Trim();
                                                    q = q.Where(p => p.product_code == code);
                                                }

                                                if (!string.IsNullOrWhiteSpace(args.keyword))
                                                {
                                                    var kw = args.keyword.Trim();
                                                    q = q.Where(p =>
                                                        (p.product_code ?? "").Contains(kw) ||
                                                        (p.category ?? "").Contains(kw) ||
                                                        (p.descriptionv6 ?? "").Contains(kw) ||
                                                        (p.PrintedDesc ?? "").Contains(kw));
                                                }

                                                var rows = await q
                                                    .OrderByDescending(p => p.PictureFileName)
                                                    .Take(take)
                                                    .Select(p => new
                                                    {
                                                        p.product_code,
                                                        p.category,
                                                        description = p.descriptionv6 ?? p.PrintedDesc,
                                                        printedDesc = p.PrintedDesc,
                                                        p.PictureFileName
                                                    })
                                                    .ToListAsync(ct);

                                                var items = rows.Select(p => new
                                                {
                                                    p.product_code,
                                                    p.category,
                                                    p.description,
                                                    p.printedDesc,
                                                    imageUrl = ProductImageUrl(p.PictureFileName)
                                                }).ToList();

                                                outputs.Add((toolCallId, JsonSerializer.Serialize(new { items })));
                                                break;
                                            }

                                        case "get_product_images":
                                            {
                                                var args = JsonSerializer.Deserialize<GetProductImagesArgs>(argsJson) ?? new GetProductImagesArgs();
                                                var code = (args.product_code ?? "").Trim();

                                                if (string.IsNullOrEmpty(code))
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new { ui = new { images = Array.Empty<object>() }, error = "product_code is required" })));
                                                    break;
                                                }

                                                var p = await _bookings.TblInvmas.AsNoTracking()
                                                    .Where(x => x.product_code == code)
                                                    .Select(x => new { x.product_code, x.descriptionv6, x.PrintedDesc, x.PictureFileName })
                                                    .FirstOrDefaultAsync(ct);

                                                if (p == null || string.IsNullOrWhiteSpace(p.PictureFileName))
                                                {
                                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new { ui = new { images = Array.Empty<object>() } })));
                                                    break;
                                                }

                                                var img = new
                                                {
                                                    url = ProductImageUrl(p.PictureFileName),
                                                    caption = $"{p.product_code} — {(p.descriptionv6 ?? p.PrintedDesc ?? "Image")}"
                                                };

                                                outputs.Add((toolCallId, JsonSerializer.Serialize(new { ui = new { images = new[] { img } } })));
                                                break;
                                            }

                                        default:
                                            {
                                                outputs.Add((toolCallId, JsonSerializer.Serialize(new { error = $"Unknown tool '{name}'" })));
                                                break;
                                            }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Tool {ToolName} failed", name);
                                    outputs.Add((toolCallId, JsonSerializer.Serialize(new { error = ex.Message })));
                                }
                            }

                            var payloadObj = new
                            {
                                tool_outputs = outputs.Select(o => new { tool_call_id = o.Id, output = o.Output })
                            };

                            var json = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { PropertyNamingPolicy = null });
                            var content = RequestContent.Create(BinaryData.FromString(json));

                            dynamic runsDyn = AgentsClient.Runs;

                            // Submit tool outputs with retry (handles both method shapes)
                            await With429RetryAsync(async () =>
                            {
                                try
                                {
                                    var resp = runsDyn.SubmitToolOutputsToRun(threadId, run.Id, content);
                                    try { run = resp.Value; } catch { run = AgentsClient.Runs.GetRun(threadId, run.Id).Value; }
                                }
                                catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                                {
                                    var resp = runsDyn.SubmitToolOutputs(threadId, run.Id, content);
                                    try { run = resp.Value; } catch { run = AgentsClient.Runs.GetRun(threadId, run.Id).Value; }
                                }
                                return run;
                            }, "SubmitToolOutputs", ct);

                            continue;
                        }
                    }

                    if (DateTime.UtcNow - startUtc > timeout)
                    {
                        _logger.LogWarning($"Run {run.Id} timed out in status {run.Status}.");
                        return run;
                    }

                    await Task.Delay(delayMs, ct);
                    if (delayMs < maxDelayMs) delayMs = Math.Min(delayMs + 400, maxDelayMs);
                }
            }
            finally
            {
                _http.HttpContext?.Session.Remove(SessionKeyActiveRunId);
            }
        }


        private static object BuildNowAestResponse()
        {
            DateTimeOffset nowLocal;
            try
            {
                var tzWin = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
                nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzWin);
            }
            catch
            {
                var tzIana = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
                nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzIana);
            }

            return new
            {
                iso = nowLocal.ToString("yyyy-MM-dd'T'HH:mm:ssK"),
                date = nowLocal.ToString("yyyy-MM-dd"),
                time24 = nowLocal.ToString("HH:mm"),
                weekday = nowLocal.ToString("dddd"),
                tz = "AEST"
            };
        }

        private sealed class GetProductInfoArgs
        {
            public string? product_code { get; set; }
            public string? keyword { get; set; }
            public int? take { get; set; }
        }

        private sealed class GetProductImagesArgs
        {
            public string? product_code { get; set; }
        }
        private string ToAbsoluteUrl(string pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return pathOrUrl;
            if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return pathOrUrl;

            var req = _http.HttpContext?.Request;
            if (req == null) return pathOrUrl;

            return $"{req.Scheme}://{req.Host}{pathOrUrl}";
        }

        private static string ExtractFileName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            if (s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(s);
                    var seg = uri.Segments.LastOrDefault()?.Trim('/');
                    return seg ?? "";
                }
                catch { }
            }

            s = s.Replace('\\', '/');
            var name = Path.GetFileName(s);
            return name ?? "";
        }

        private string ProductImageUrl(string? pictureFileName)
        {
            if (string.IsNullOrWhiteSpace(pictureFileName)) return "";
            if (pictureFileName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return pictureFileName;

            var file = ExtractFileName(pictureFileName);
            if (string.IsNullOrEmpty(file)) return "";
            var encoded = Uri.EscapeDataString(file);
            var rel = $"/images/products/{encoded}";
            var req = _http.HttpContext?.Request;
            if (req == null) return rel;
            return $"{req.Scheme}://{req.Host}{rel}";
        }

        #endregion

        #region Tool: check_date_availability

        private sealed class CheckDateArgs
        {
            public DateTime date { get; set; }
            public DateTime? endDate { get; set; }
            public int? venueId { get; set; }
            public string? room { get; set; }
        }

        private sealed record AvailabilityConflict(
            decimal BookingId,
            string? BookingNo,
            string? OrderNo,
            DateTime Start,
            DateTime End,
            DateTime OrderDate,
            int? VenueId,
            string? VenueRoom
        );

        private sealed record AvailabilityResult(
            bool IsAvailable,
            List<AvailabilityConflict> Conflicts
        );

        private async Task<AvailabilityResult> CheckAvailabilityAsync(CheckDateArgs args, CancellationToken ct)
        {
            var startDateInclusive = args.date.Date;
            var endDateExclusive = (args.endDate ?? args.date).Date.AddDays(1);

            var q = _bookings.TblBookings.AsNoTracking()
                .Where(b => (bool)!b.bBookingIsComplete)
                .Where(b => b.status != 255);

            if (args.venueId is not null)
                q = q.Where(b => b.VenueID == args.venueId);

            if (!string.IsNullOrWhiteSpace(args.room))
                q = q.Where(b => b.VenueRoom == args.room);

            var rows = await q
                .Where(b => b.ShowSDate != null)
                .Where(b => b.ShowSDate == startDateInclusive)
                .OrderBy(b => b.SDate)
                .Select(b => new
                {
                    b.ID,
                    b.booking_no,
                    b.order_no,
                    b.SDate,
                    b.rDate,
                    b.order_date,
                    b.VenueID,
                    b.VenueRoom
                })
                .ToListAsync(ct);

            var conflicts = rows.Select(b => new AvailabilityConflict(
                b.ID,
                b.booking_no,
                b.order_no,
                b.SDate!.Value.Date,
                b.rDate!.Value.Date,
                b.order_date!.Value.Date,
                b.VenueID,
                b.VenueRoom
            )).ToList();

            return new AvailabilityResult(conflicts.Count == 0, conflicts);
        }

        private async Task WaitForRunToCompleteAsync(string threadId, string? runId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(runId)) return;

            while (true)
            {
                var r = AgentsClient.Runs.GetRun(threadId, runId).Value;
                if (r.Status == Azure.AI.Agents.Persistent.RunStatus.Completed ||
                    r.Status == Azure.AI.Agents.Persistent.RunStatus.Failed ||
                    r.Status == Azure.AI.Agents.Persistent.RunStatus.Cancelled ||
                    r.Status == Azure.AI.Agents.Persistent.RunStatus.Expired)
                {
                    return;
                }

                await Task.Delay(350, ct);
            }
        }

        private static DateTime? ComposeDateTime(DateTime? date, string? hhmm)
        {
            if (date is null) return null;
            if (string.IsNullOrWhiteSpace(hhmm)) return date.Value;

            if (hhmm.Length == 4 &&
                int.TryParse(hhmm.AsSpan(0, 2), out var hh) &&
                int.TryParse(hhmm.AsSpan(2, 2), out var mm))
            {
                return new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, hh, mm, 0, DateTimeKind.Unspecified);
            }

            return date.Value;
        }

        #endregion

        #region Persisted thread mapping

        public async Task ReplacePersistedThreadAsync(string userKey, string newThreadId, CancellationToken ct)
        {
            //var row = await _appDb.AgentThreads.FirstOrDefaultAsync(x => x.UserKey == userKey, ct);
            //var now = DateTime.UtcNow;

            //if (row is null)
            //{
            //    _appDb.AgentThreads.Add(new AgentThread
            //    {
            //        UserKey = userKey,
            //        ThreadId = newThreadId,
            //        CreatedUtc = now,
            //        LastSeenUtc = now
            //    });
            //}
            //else
            //{
            //    row.ThreadId = newThreadId;
            //    row.LastSeenUtc = now;
            //    _appDb.AgentThreads.Update(row);
            //}

            //await _appDb.SaveChangesAsync(ct);
        }

        public async Task<string> EnsureThreadIdPersistedAsync(
            ISession session,
            string userKey,
            CancellationToken ct)
        {
            //var saved = await _appDb.AgentThreads
            //    .AsNoTracking()
            //    .Where(t => t.UserKey == userKey)
            //    .Select(t => t.ThreadId)
            //    .FirstOrDefaultAsync(ct);

            //if (!string.IsNullOrWhiteSpace(saved))
            //{
            //    session.SetString(SessionKeyThreadId, saved);
            //    await TouchLastSeenAsync(userKey, ct);
            //    return saved;
            //}

            var threadId = EnsureThreadId(session);

            var now = DateTime.UtcNow;
            //var existing = await _appDb.AgentThreads
            //    .Where(t => t.UserKey == userKey || t.ThreadId == threadId)
            //    .FirstOrDefaultAsync(ct);

            //if (existing is null)
            //{
            //    _appDb.AgentThreads.Add(new AgentThread
            //    {
            //        UserKey = userKey,
            //        ThreadId = threadId,
            //        CreatedUtc = now,
            //        LastSeenUtc = now
            //    });
            //}
            //else
            //{
            //    existing.UserKey = userKey;
            //    existing.ThreadId = threadId;
            //    existing.LastSeenUtc = now;
            //    _appDb.AgentThreads.Update(existing);
            //}

            //await _appDb.SaveChangesAsync(ct);
            return threadId;
        }

        private async Task TouchLastSeenAsync(string userKey, CancellationToken ct)
        {
            //var row = await _appDb.AgentThreads
            //    .Where(t => t.UserKey == userKey)
            //    .FirstOrDefaultAsync(ct);

            //if (row is not null)
            //{
            //    row.LastSeenUtc = DateTime.UtcNow;
            //    _appDb.AgentThreads.Update(row);
            //    await _appDb.SaveChangesAsync(ct);
            //}
        }

        public async Task<string?> GetSavedThreadIdAsync(string userKey, CancellationToken ct)
        {
            //var t = await _appDb.AgentThreads
            //    .AsNoTracking()
            //    .Where(x => x.UserKey == userKey)
            //    .Select(x => x.ThreadId)
            //    .FirstOrDefaultAsync(ct);

            //if (!string.IsNullOrWhiteSpace(t))
            //    await TouchLastSeenAsync(userKey, ct);

            //return t;
            return string.Empty;
        }

        public async Task<(string ThreadId, IEnumerable<DisplayMessage> Messages)?>
            GetTranscriptForUserAsync(string userKey, CancellationToken ct)
        {
            var threadId = await GetSavedThreadIdAsync(userKey, ct);
            if (string.IsNullOrWhiteSpace(threadId))
                return null;
            var result = GetTranscript(threadId);
            return result;
        }

        #endregion

        #region Extraction helpers (dates, venue, fields, contacts, event type)

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

            foreach (var m in ordered.Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
            {
                var text = JoinParts(m);
                if (TryParseMatch(text, out var dto, out var matched))
                    return (dto, matched);
            }

            foreach (var m in ordered.Where(x => !x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
            {
                var text = JoinParts(m);
                if (TryParseMatch(text, out var dto, out var matched))
                    return (dto, matched);
            }

            return (null, null);

            static bool TryParseDateToken(string token, out DateTimeOffset dto)
            {
                token = token.Trim();
                token = Regex.Replace(token, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);

                var cultures = new[]
                {
                    CultureInfo.GetCultureInfo("en-US"),
                    CultureInfo.GetCultureInfo("en-GB"),
                    CultureInfo.InvariantCulture
                };

                if (Regex.IsMatch(token, @"^\d{4}-\d{2}-\d{2}$") &&
                    DateTime.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var isoDt))
                {
                    dto = new DateTimeOffset(isoDt);
                    return true;
                }

                if (Regex.IsMatch(token, @"^\d{1,2}/\d{1,2}/\d{2,4}$"))
                {
                    var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "MM/dd/yyyy", "M/d/yyyy" };
                    foreach (var fmt in fmts)
                    {
                        if (DateTime.TryParseExact(token, fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dmy))
                        {
                            dto = new DateTimeOffset(dmy);
                            return true;
                        }
                    }
                }

                foreach (var c in cultures)
                {
                    if (DateTime.TryParse(token, c, DateTimeStyles.AssumeLocal, out var dt))
                    {
                        dto = new DateTimeOffset(dt);
                        return true;
                    }
                }

                dto = default;
                return false;
            }
        }

        /// <summary>
        /// Extract event time (start/end) from conversation messages
        /// Looks for patterns like: "9:00 AM", "14:30", "2pm", "10am-4pm"
        /// </summary>
        public (TimeSpan? startTime, TimeSpan? endTime, string? matched) ExtractEventTime(IEnumerable<DisplayMessage> messages)
        {
            var ordered = messages.OrderBy(m => m.Timestamp).ToList();
            static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

            var timePatterns = new[]
            {
                // 9:00 AM - 5:00 PM, 9am-5pm, etc.
                @"\b(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)\s*(?:to|-|–|—)\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)\b",
                // 14:30 - 18:00, 2:00pm - 6:00pm, etc.
                @"\b(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)?\s*(?:to|-|–|—)\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)?\b",
                // Single time: 9:00 AM, 2pm, 14:30
                @"\b(\d{1,2})(?::(\d{2}))?\s*(am|pm|AM|PM)\b",
                // 24-hour format: 14:30, 09:00
                @"\b(\d{1,2}):(\d{2})\b"
            };

            bool TryParseTimeRange(string text, out TimeSpan? start, out TimeSpan? end, out string? matched)
            {
                start = null;
                end = null;
                matched = null;

                foreach (var pat in timePatterns)
                {
                    foreach (Match m in Regex.Matches(text, pat, RegexOptions.IgnoreCase))
                    {
                        var token = m.Value;
                        if (TryParseTimeToken(token, out var s, out var e))
                        {
                            start = s;
                            end = e;
                            matched = token;
                            return true;
                        }
                    }
                }
                return false;
            }

            // Check user messages first
            foreach (var m in ordered.Where(x => x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
            {
                var text = JoinParts(m);
                if (TryParseTimeRange(text, out var start, out var end, out var matched))
                    return (start, end, matched);
            }

            // Then check assistant messages
            foreach (var m in ordered.Where(x => !x.Role.Equals("user", StringComparison.OrdinalIgnoreCase)))
            {
                var text = JoinParts(m);
                if (TryParseTimeRange(text, out var start, out var end, out var matched))
                    return (start, end, matched);
            }

            return (null, null, null);
        }

        private static bool TryParseTimeToken(string token, out TimeSpan? start, out TimeSpan? end)
        {
            start = null;
            end = null;

            // Clean the token
            token = token.ToLowerInvariant().Replace(" ", "");

            // Pattern 1: time1-time2 (e.g., "9am-5pm", "14:30-18:00")
            var rangeMatch = Regex.Match(token, @"^(\d{1,2}(?::\d{2})?(?:am|pm)?)-(?:(\d{1,2}(?::\d{2})?(?:am|pm)?))$");
            if (rangeMatch.Success)
            {
                if (TryParseSingleTime(rangeMatch.Groups[1].Value, out var s) &&
                    TryParseSingleTime(rangeMatch.Groups[2].Value, out var e))
                {
                    start = s;
                    end = e;
                    return true;
                }
            }

        // Pattern 2: single time (e.g., "9am", "14:30")
        if (TryParseSingleTime(token, out var singleTime))
        {
            start = singleTime;
            // For single times, assume a default duration (e.g., 2 hours)
            end = singleTime.Add(TimeSpan.FromHours(2));
            return true;
        }

            return false;
        }

        private static bool TryParseSingleTime(string timeStr, out TimeSpan result)
        {
            result = TimeSpan.Zero;

            // Handle AM/PM formats
            if (Regex.IsMatch(timeStr, @"^\d{1,2}(?::\d{2})?(?:am|pm)$"))
            {
                if (DateTime.TryParse(timeStr, out var dt))
                {
                    result = dt.TimeOfDay;
                    return true;
                }
            }

            // Handle 24-hour format
            if (Regex.IsMatch(timeStr, @"^\d{1,2}:\d{2}$"))
            {
                if (TimeSpan.TryParse(timeStr, out result))
                {
                    return true;
                }
            }

            return false;
        }

        public Dictionary<string, string> ExtractExpectedFields(IEnumerable<DisplayMessage> messages)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Booking ID"] = null!,
                ["Booking #"] = null!,
                ["Quote Total Amount inc GST"] = null!,
                ["Equipment Cost"] = null!,
                ["Booking Type"] = null!,
                ["Labor Cost"] = null!,
                ["Service Charge"] = null!,
                ["Contact Name"] = null!,
                ["Show Start Time"] = null!,
                ["Show End Time"] = null!,
                ["Booking Status"] = null!,
                ["Room"] = null!,
                ["Show Name"] = null!,
                ["Organization"] = null!,
                ["Sales Person Code"] = null!,
                ["Show Start Date"] = null!,
                ["Show Finishes"] = null!,
                ["Setup Date"] = null!,
                ["Rehearsal Date"] = null!,
                ["Bill To"] = null!,
                ["Event Type"] = null!,
                ["Customer ID"] = null!,
                ["Venue Name"] = null!
            };

            var lines = messages
                .OrderBy(m => m.Timestamp)
                .Select(m => string.Join(" ", m.Parts ?? Enumerable.Empty<string>()))
                .ToList();
            var full = string.Join("\n", lines);

            string? Set(string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    result[key] = value!.Trim();
                return value;
            }

            static string? FirstOrNull(params string?[] arr) => arr.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            string? FindText(string text, params string[] labels)
            {
                foreach (var label in labels)
                {
                    var pattern = $@"(?:(?:^|\b){Regex.Escape(label)}\s*[:\-]?\s*)([^\r\n,;|]+)";
                    var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value.Trim();
                }
                return null;
            }

            string? FindId(string text, params string[] labels)
            {
                foreach (var label in labels)
                {
                    var m = Regex.Match(text, $@"{Regex.Escape(label)}\s*[:\-]?\s*([A-Za-z0-9\-_/]+)", RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value.Trim();
                }
                return null;
            }

            string? FindMoney(string text, params string[] labels)
            {
                foreach (var label in labels)
                {
                    var p = $@"{Regex.Escape(label)}\s*[:\-]?\s*(?:AUD\s*)?(\$?\s*[0-9][0-9,]*\.?[0-9]{{0,2}})(?:\s*(?:AUD|inc\s*GST|GST\s*incl\.?)\b)?";
                    var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
                    if (m.Success) return NormalizeMoney(m.Groups[1].Value);
                }
                var near = string.Join("|", labels.Select(Regex.Escape));
                var nearPat = $@"(?:{near}).{{0,40}}(\$?\s*[0-9][0-9,]*\.?[0-9]{{0,2}})";
                var nearM = Regex.Match(text, nearPat, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (nearM.Success) return NormalizeMoney(nearM.Groups[1].Value);
                return null;

                static string NormalizeMoney(string raw)
                {
                    var v = raw.Replace(" ", "");
                    if (!v.StartsWith("$")) v = "$" + v.TrimStart('$');
                    return v;
                }
            }

            string? FindDate(string text, params string[] labels)
            {
                foreach (var label in labels)
                {
                    var pat = $@"{Regex.Escape(label)}\s*[:\-]?\s*([A-Za-z]{{3,9}}\s+\d{{1,2}}(?:st|nd|rd|th)?(?:,?\s+\d{{4}})?|\d{{1,2}}/\d{{1,2}}/\d{{2,4}}|\d{{4}}-\d{{2}}-\d{{2}}|\d{{1,2}}\s+[A-Za-z]{{3,9}}\s+\d{{4}})";
                    foreach (Match m in Regex.Matches(text, pat, RegexOptions.IgnoreCase))
                    {
                        var token = m.Groups[1].Value;
                        if (TryParseDateToken(token, out var dto))
                            return dto.ToString("dd MMM yyyy");
                    }
                }
                return null;
            }

            string? FindTime(string text, params string[] labels)
            {
                foreach (var label in labels)
                {
                    var pat = $@"{Regex.Escape(label)}\s*[:\-]?\s*([01]?\d|2[0-3]):([0-5]\d)\s*(am|pm)?";
                    var m = Regex.Match(text, pat, RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var token = $"{m.Groups[1].Value}:{m.Groups[2].Value} {m.Groups[3].Value}".Trim();
                        if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                            return dt.ToString("HH:mm");
                        return token;
                    }
                }
                foreach (var label in labels)
                {
                    var pat2 = $@"{Regex.Escape(label)}\s*[:\-]?\s*([1-9]|1[0-2])\s*(am|pm)";
                    var m2 = Regex.Match(text, pat2, RegexOptions.IgnoreCase);
                    if (m2.Success)
                    {
                        var token = $"{m2.Groups[1].Value}:00 {m2.Groups[2].Value}";
                        if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                            return dt.ToString("HH:mm");
                        return token;
                    }
                }
                return null;
            }

            static bool TryParseDateToken(string token, out DateTimeOffset dto)
            {
                token = token.Trim();
                token = Regex.Replace(token, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);

                var cultures = new[]
                {
                    CultureInfo.GetCultureInfo("en-AU"),
                    CultureInfo.GetCultureInfo("en-GB"),
                    CultureInfo.GetCultureInfo("en-US"),
                    CultureInfo.InvariantCulture
                };

                if (Regex.IsMatch(token, @"^\d{4}-\d{2}-\d{2}$") &&
                    DateTime.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var iso))
                { dto = new DateTimeOffset(iso); return true; }

                if (Regex.IsMatch(token, @"^\d{1,2}/\d{1,2}/\d{2,4}$"))
                {
                    var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yy", "d/M/yy" };
                    foreach (var f in fmts)
                        if (DateTime.TryParseExact(token, f, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
                        { dto = new DateTimeOffset(d); return true; }
                }

                foreach (var c in cultures)
                    if (DateTime.TryParse(token, c, DateTimeStyles.AssumeLocal, out var dt))
                    { dto = new DateTimeOffset(dt); return true; }

                dto = default;
                return false;
            }

            Set("Booking ID", FirstOrNull(FindId(full, "booking id", "id")));
            Set("Booking #", FirstOrNull(FindId(full, "booking #", "booking no", "booking number", "booking ref", "reference")));
            Set("Quote Total Amount inc GST", FindMoney(full, "total inc gst", "quote total", "grand total", "total amount", "total incl gst", "total including gst"));
            Set("Equipment Cost", FindMoney(full, "equipment cost", "equipment total", "gear total", "av total"));
            Set("Booking Type", FindText(full, "booking type", "type", "event type"));
            Set("Labor Cost", FindMoney(full, "labour cost", "labor cost", "crew total", "labour total", "labor total"));
            Set("Service Charge", FindMoney(full, "service charge", "surcharge", "fees", "handling"));
            Set("Contact Name", FirstOrNull(FindText(full, "contact name", "client name", "customer name", "name")));
            Set("Show Start Time", FirstOrNull(FindTime(full, "show start time", "start time", "event start", "doors at")));
            Set("Show End Time", FirstOrNull(FindTime(full, "show end time", "end time", "event end", "finish time", "finishes at")));
            Set("Booking Status", FirstOrNull(FindText(full, "booking status", "status")));
            Set("Room", FirstOrNull(FindText(full, "room", "venue room", "ballroom", "space")));
            Set("Show Name", FirstOrNull(FindText(full, "show name", "event name", "name of event", "show")));
            Set("Organization", FirstOrNull(FindText(full, "organization", "organisation", "company")));
            Set("Sales Person Code", FirstOrNull(FindText(full, "sales person code", "sales code", "rep code")));
            Set("Show Start Date", FirstOrNull(FindDate(full, "show start date", "event date", "start date", "on")));
            Set("Show Finishes", FirstOrNull(FindDate(full, "show finishes", "finish date", "end date", "to", "until")));
            Set("Setup Date", FirstOrNull(FindDate(full, "setup date", "set date", "bump in date", "move-in date")));
            Set("Rehearsal Date", FirstOrNull(FindDate(full, "rehearsal date", "reh date")));
            Set("Bill To", FirstOrNull(FindText(full, "bill to", "billing name", "invoice to")));
            Set("Event Type", FirstOrNull(FindText(full, "event type", "type of event")));
            Set("Customer ID", FirstOrNull(FindId(full, "customer id", "client id", "cust id")));
            Set("Venue Name", FirstOrNull(FindText(full, "venue name", "venue", "hotel", "location")));

            return result;
        }

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

            var userLines = items.Where(x => x.role.Equals("user", StringComparison.OrdinalIgnoreCase)).Select(x => x.line).ToList();
            var asstLines = items.Where(x => !x.role.Equals("user", StringComparison.OrdinalIgnoreCase)).Select(x => x.line).ToList();
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

            static DateTimeOffset? FindEventDate(IEnumerable<string> src, int? yearHint, out string? matched)
            {
                matched = null;

                foreach (var line in src)
                {
                    var m = Regex.Match(line, @"\b(event\s*date|date|on)\s*[:\-]?\s*(.+)$", RegexOptions.IgnoreCase);
                    if (m.Success && TryParseDateToken(m.Groups[2].Value.Trim(), yearHint, out var dto))
                    {
                        matched = m.Groups[2].Value.Trim();
                        return dto;
                    }
                }

                foreach (var line in src)
                {
                    foreach (Match mm in Regex.Matches(line,
                        @"(\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{2,4}|[A-Za-z]{3,9}\s+\d{1,2}(?:st|nd|rd|th)?(?:,?\s+\d{4})?|\d{1,2}\s+[A-Za-z]{3,9}(?:\s+\d{4})?)",
                        RegexOptions.IgnoreCase))
                    {
                        var token = mm.Value.Trim();
                        if (TryParseDateToken(token, yearHint, out var dto))
                        {
                            matched = token;
                            return dto;
                        }
                    }
                }

                return null;
            }

            static bool TryParseDateToken(string token, int? yearHint, out DateTimeOffset dto)
            {
                token = token.Trim();
                token = Regex.Replace(token, @"\b(\d{1,2})(st|nd|rd|th)\b", "$1", RegexOptions.IgnoreCase);

                if (Regex.IsMatch(token, @"^\d{4}-\d{2}-\d{2}$") &&
                    DateTime.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var iso))
                { dto = new DateTimeOffset(iso); return true; }

                var fmts = new[] { "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy", "dd/MM/yy", "M/d/yy" };
                foreach (var f in fmts)
                    if (DateTime.TryParseExact(token, f, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d1))
                    { dto = new DateTimeOffset(d1); return true; }

                foreach (var c in new[] { "en-AU", "en-GB", "en-US" })
                    if (DateTime.TryParse(token, CultureInfo.GetCultureInfo(c), DateTimeStyles.AssumeLocal, out var d2))
                    { dto = new DateTimeOffset(d2); return true; }

                var m = Regex.Match(token, @"^(?<day>\d{1,2})\s+(?<mon>[A-Za-z]{3,9})(?:\s+(?<yr>\d{4}))?$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var day = int.Parse(m.Groups["day"].Value);
                    var mon = DateTime.ParseExact(m.Groups["mon"].Value.Substring(0, 3), "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None).Month;
                    var yr = m.Groups["yr"].Success ? int.Parse(m.Groups["yr"].Value) : InferYearFor(day, mon, yearHint);
                    if (yr > 0 && day >= 1 && day <= DateTime.DaysInMonth(yr, mon))
                    {
                        dto = new DateTimeOffset(new DateTime(yr, mon, day));
                        return true;
                    }
                }

                dto = default;
                return false;
            }

            static int InferYearFor(int day, int month, int? yearHint)
            {
                if (yearHint.HasValue) return yearHint.Value;
                var now = DateTime.Now;
                var yr = now.Year;
                var candidate = new DateTime(yr, month, Math.Min(day, DateTime.DaysInMonth(yr, month)));
                if (candidate.Date < now.Date) yr++;
                return yr;
            }

            static string? FindVenue(IEnumerable<string> src, out string? matched)
            {
                matched = null;

                var venueKeywords = new[] {
                    "westin","brisbane","hotel","resort","ballroom","hall","centre","center",
                    "convention","banquet","club","theatre","theater","auditorium","room","suite",
                    "terrace","lawn"
                };

                var genericEvent = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "gala","dinner","gala dinner","conference","meeting","event","reception",
                    "party","wedding","concert","seminar","workshop","banquet"
                };

                static string Clean(string s) => s.Trim().TrimEnd('.', ',', ';', '?', '!');
                bool HasVenueKeyword(string s) => venueKeywords.Any(k => s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

                bool LooksGeneric(string s)
                {
                    if (genericEvent.Contains(s)) return true;
                    var words = s.Split(new[] { ' ', '-', '/', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    return words.Length > 0 && words.All(w => genericEvent.Contains(w));
                }

                foreach (var line in src)
                {
                    var m = Regex.Match(line, @"\b(venue\s*name|venue|location|hotel)\s*[:\-]?\s*(.+)$",
                                        RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var cand = Clean(m.Groups[2].Value);
                        if (!LooksGeneric(cand))
                        {
                            matched = cand;
                            return cand;
                        }
                    }
                }

                foreach (var line in src)
                {
                    var m = Regex.Match(line, @"\bat\s+([A-Z][A-Za-z0-9&\-\.\s']{2,})",
                                        RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        var cand = Clean(m.Groups[1].Value);
                        if (!LooksGeneric(cand) && HasVenueKeyword(cand))
                        {
                            matched = cand;
                            return cand;
                        }
                    }
                }

                foreach (var line in src)
                {
                    if (line.Length <= 80)
                    {
                        var cand = Clean(line);
                        if (!LooksGeneric(cand) && HasVenueKeyword(cand))
                        {
                            matched = cand;
                            return cand;
                        }
                    }
                }

                return null;
            }
        }


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
                return parts.All(p => Regex.IsMatch(p, @"^[A-Za-z][a-z]+$"));
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

            // ---- NAME ----
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
                return parts.All(p => Regex.IsMatch(p, @"^[A-Za-z][a-z]+$"));
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

        #endregion

        #region Booking JSON parsing + persistence (limited field set)

        // REPLACE the record with this version so we can carry id/booking_no too.
        public sealed record ParsedBookingLimited(
            decimal? PriceQuoted,         // price_quoted
            decimal? HirePrice,           // hire_price
            string? BookingTypeV32,      // booking_type_v32
            decimal? Labour,              // labour
            decimal? SundryTotal,         // insurance_v5 (service charge)
            string? ContactNameV6,       // contact_nameV6
            string? ShowStartTimeHHmm,   // showStartTime (HHmm)
            string? ShowEndTimeHHmm,     // ShowEndTime (HHmm)
            string? BookingProgressStatus,// BookingProgressStatus
            string? VenueRoom,           // VenueRoom
            string? ShowName,            // showName
            string? OrganizationV6,      // OrganizationV6
            string? Salesperson,         // Salesperson
            DateTime? SDate,             // SDate (Show Start Date)
            DateTime? RDate,             // rDate  (Show Finishes / End Date)
            DateTime? SetDate,           // SetDate
            DateTime? RehDate,           // RehDate
            string? CustID,              // CustID (bill-to code text in your schema)
            int? VenueID,                // VenueID

            // NEW: to enable proper upsert without changing your call sites:
            decimal? BookingId,          // ID (nullable)
            string? BookingNo            // booking_no (nullable)
        );

        // Keep this helper as-is (already in your file), used below:
        // private static string? SanitizeHHmm(string? hhmm) { ... }

        // ADD: turn transcript -> ParsedBookingLimited (uses your ExtractExpectedFields)
        private ParsedBookingLimited? TryParseBookingFromTranscript(IEnumerable<DisplayMessage> messages)
        {
            if (messages is null) return null;

            // Prefer the explicit isla.booking blob
            var blob = FindLastIslaBookingBlob(messages);
            if (blob != null)
            {
                return MapBookingBlobToParsed(blob);
            }

            // Fallback: synthesize from transcript (only used if no blob is present)
            var m = ExtractExpectedFields(messages); // your existing extractor

            // Map + normalize (same helpers you already have)
            static decimal? Money(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var v = s.Replace("AUD", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("$", "")
                         .Replace(",", "")
                         .Trim();
                return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
            }

            static DateTime? D(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var fmts = new[] { "dd MMM yyyy", "d MMM yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy" };
                foreach (var f in fmts)
                    if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var dt))
                        return dt;
                return DateTime.TryParse(s, out var any) ? any : null;
            }

            static decimal? DecId(string? s)
                => string.IsNullOrWhiteSpace(s) ? null
                   : decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;

            static int? IntId(string? s)
                => string.IsNullOrWhiteSpace(s) ? null
                   : int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : (int?)null;

            string? Get(string key) => m.TryGetValue(key, out var v) ? v?.Trim() : null;

            var parsed = new ParsedBookingLimited(
                PriceQuoted: Money(Get("Quote Total Amount inc GST")),
                HirePrice: Money(Get("Equipment Cost")),
                BookingTypeV32: Get("Booking Type"),
                Labour: Money(Get("Labor Cost")),
                SundryTotal: Money(Get("Service Charge")),
                ContactNameV6: Get("Contact Name"),
                ShowStartTimeHHmm: SanitizeHHmm(Get("Show Start Time")),
                ShowEndTimeHHmm: SanitizeHHmm(Get("Show End Time")),
                BookingProgressStatus: Get("Booking Status"),
                VenueRoom: Get("Room"),
                ShowName: Get("Show Name"),
                OrganizationV6: Get("Organization"),
                Salesperson: Get("Sales Person Code"),
                SDate: D(Get("Show Start Date")),
                RDate: D(Get("Show Finishes")),
                SetDate: D(Get("Setup Date")),
                RehDate: D(Get("Rehearsal Date")),
                CustID: Get("Customer ID"),
                VenueID: IntId(Get("Venue Name")),
                BookingId: DecId(Get("Booking ID")),
                BookingNo: Get("Booking #")
            );

            // If nothing meaningful, return null
            var anySignal = parsed.SDate.HasValue
                            || !string.IsNullOrWhiteSpace(parsed.VenueRoom)
                            || parsed.VenueID.HasValue
                            || !string.IsNullOrWhiteSpace(parsed.ContactNameV6)
                            || !string.IsNullOrWhiteSpace(parsed.ShowName)
                            || !string.IsNullOrWhiteSpace(parsed.OrganizationV6)
                            || !string.IsNullOrWhiteSpace(parsed.BookingTypeV32);

            return anySignal ? parsed : null;
        }

        // Map a parsed JSON blob -> ParsedBookingLimited
        private ParsedBookingLimited MapBookingBlobToParsed(Dictionary<string, string> blob)
        {
            static decimal? Money(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var v = s.Replace("AUD", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("$", "")
                         .Replace(",", "")
                         .Trim();
                return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;
            }
            static DateTime? D(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
                var fmts = new[] { "dd MMM yyyy", "d MMM yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy" };
                foreach (var f in fmts)
                    if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var dt))
                        return dt;
                return DateTime.TryParse(s, out var any) ? any : null;
            }
            static decimal? DecId(string? s)
                => string.IsNullOrWhiteSpace(s) ? null
                   : decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
            static int? IntId(string? s)
                => string.IsNullOrWhiteSpace(s) ? null
                   : int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : (int?)null;

            string? Get(string key) => blob.TryGetValue(key, out var v) ? v?.Trim() : null;

            return new ParsedBookingLimited(
                PriceQuoted: Money(Get("Quote Total Amount inc GST")),
                HirePrice: Money(Get("Equipment Cost")),
                BookingTypeV32: Get("Booking Type"),
                Labour: Money(Get("Labor Cost")),
                SundryTotal: Money(Get("Service Charge")),
                ContactNameV6: Get("Contact Name"),
                ShowStartTimeHHmm: SanitizeHHmm(Get("Show Start Time")),
                ShowEndTimeHHmm: SanitizeHHmm(Get("Show End Time")),
                BookingProgressStatus: Get("Booking Status"),
                VenueRoom: Get("Room"),
                ShowName: Get("Show Name"),
                OrganizationV6: Get("Organization"),
                Salesperson: Get("Sales Person Code"),
                SDate: D(Get("Show Start Date")),
                RDate: D(Get("Show Finishes")),
                SetDate: D(Get("Setup Date")),
                RehDate: D(Get("Rehearsal Date")),
                CustID: Get("Customer ID"),
                VenueID: IntId(Get("Venue Name")),
                BookingId: DecId(Get("Booking ID")),
                BookingNo: Get("Booking #")
            );
        }

        // Save only when we have enough to make a meaningful TblBookings row
        private static bool IsBookingComplete(ParsedBookingLimited p, decimal? contactId)
        {
            // Required minimums (tune as needed, but keep strict to avoid noisy inserts):
            // - Event date (SDate)
            // - Venue info (either VenueID or VenueRoom)
            // - Some identity of organiser (ContactID via upsert OR ContactNameV6)
            // - A human label of the event (ShowName OR OrganizationV6 OR EventType)
            var hasDate = p.SDate.HasValue;
            var hasVenue = (p.VenueID.HasValue) || !string.IsNullOrWhiteSpace(p.VenueRoom);
            var hasContact = (contactId.HasValue) || !string.IsNullOrWhiteSpace(p.ContactNameV6);
            var hasLabel = !string.IsNullOrWhiteSpace(p.ShowName)
                               || !string.IsNullOrWhiteSpace(p.OrganizationV6)
                               || !string.IsNullOrWhiteSpace(p.BookingTypeV32)
                               || !string.IsNullOrWhiteSpace(p.CustID);

            // Optional but nice-to-have (do NOT block save if missing):
            // end date/time, quote numbers, etc.

            return hasDate && hasVenue && hasContact && hasLabel;
        }


        private static byte? TryByte(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (byte.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var b)) return b;
            return null; // ignore text like "Rental + Production" if your DB needs a code
        }

        private static string? SanitizeHHmm(string? hhmm)
        {
            if (string.IsNullOrWhiteSpace(hhmm)) return null;
            var s = Regex.Replace(hhmm, @"\D", "");
            if (s.Length == 4) return s;
            if (DateTime.TryParse(hhmm, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                return t.ToString("HHmm");
            return null;
        }

        private async Task PostProcessAfterRunAsync(IEnumerable<DisplayMessage> messages, CancellationToken ct)
        {
            // Delegate all contact/organization saving logic to the orchestration service
            // It handles deduplication, proper linking, and follows the guide's patterns
            try
            {
                // Simple check: only save if we have meaningful contact info (email or phone)
                var transcript = string.Join(" ", messages.Select(m => m.FullText ?? ""));
                if (!transcript.Contains('@') && !Regex.IsMatch(transcript, @"\+?\d{10,}"))
                {
                    return; // Wait for email or phone before saving
                }
                
                await _orchestration.SaveContactAndOrganizationAsync(messages, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Post-processing failed");
            }
        }

        private static string StripZeroWidth(string s)
        {
            // Remove ZWSP/ZWJ/ZWNJ/BOM etc.
            return Regex.Replace(s, @"[\u200B-\u200D\uFEFF]", "");
        }

        private static string NormalizeQuotes(string s)
        {
            // Convert curly quotes to straight quotes for JSON parsing
            return s.Replace('“', '"')
                    .Replace('”', '"')
                    .Replace('‘', '\'')
                    .Replace('’', '\'');
        }

        private static string StripCodeFencesPreserveContent(string s)
        {
            var lines = s.Split('\n');
            var result = new List<string>(lines.Length);
            bool inFence = false;

            foreach (var line in lines)
            {
                var t = line.TrimEnd();
                if (t.StartsWith("```"))
                {
                    inFence = !inFence;
                    continue; // drop fence markers, keep content
                }
                // drop stray backticks but keep content
                result.Add(line.Replace("```", ""));
            }
            return string.Join("\n", result);
        }


        #endregion
        // Finds the latest JSON object (single- or multi-line, possibly html-escaped or inside code fences)
        // that has "type":"isla.booking". Returns a string dictionary or null.
        private static Dictionary<string, string>? FindLastIslaBookingBlob(IEnumerable<DisplayMessage> messages)
        {
            // Merge newest → oldest so “last confirmed” booking appears first in time-sorted scan
            var merged = string.Join("\n",
                messages.OrderByDescending(x => x.Timestamp)
                        .SelectMany(m => (m.Parts ?? Enumerable.Empty<string>()).Reverse()));

            if (string.IsNullOrWhiteSpace(merged)) return null;

            // Normalize: HTML decode, strip zero-width, normalize quotes, strip fences (keeping content)
            merged = WebUtility.HtmlDecode(merged);
            merged = StripZeroWidth(merged);
            merged = NormalizeQuotes(merged);
            merged = StripCodeFencesPreserveContent(merged);

            // 1) Primary: structured brace-walk to pull ALL JSON objects
            var jsonObjects = ExtractJsonObjects(merged);
            for (int i = jsonObjects.Count - 1; i >= 0; i--)
            {
                var dict = TryParseIfIslaBooking(jsonObjects[i]);
                if (dict != null) return dict;
            }

            // 2) Regex fallback: match last {...} that contains "type":"isla.booking" even with curly quotes
            // Singleline to allow newlines; tolerant quotes “” and &quot; already normalized above
            var rx = new Regex(@"\{[^{}]*""type""\s*:\s*""isla\.booking""[^{}]*\}",
                               RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var matches = rx.Matches(merged);
            if (matches.Count > 0)
            {
                var json = matches[^1].Value;
                var dict = TryParseIfIslaBooking(json);
                if (dict != null) return dict;
            }

            // 3) Last-ditch: locate "type":"isla.booking" and expand to braces
            var idx = LastIndexOfIslaBooking(merged);
            if (idx >= 0)
            {
                var expanded = ExpandToJsonObject(merged, idx);
                if (!string.IsNullOrWhiteSpace(expanded))
                {
                    var dict = TryParseIfIslaBooking(expanded!);
                    if (dict != null) return dict;
                }
            }

            // Optional: add a small diagnostic to confirm what we scanned
            // _logger.LogInformation("No isla.booking JSON found. Tail:\n{0}",
            //     merged.Length <= 600 ? merged : merged[^600..]);

            return null;
        }


        private static List<string> ExtractJsonObjects(string s)
        {
            var list = new List<string>();
            int depth = 0, start = -1;
            bool inStr = false;
            char prev = '\0';

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && prev != '\\') inStr = !inStr;

                if (!inStr)
                {
                    if (c == '{')
                    {
                        if (depth == 0) start = i;
                        depth++;
                    }
                    else if (c == '}')
                    {
                        if (depth > 0 && --depth == 0 && start >= 0)
                        {
                            list.Add(s.Substring(start, i - start + 1));
                            start = -1;
                        }
                    }
                }
                prev = c;
            }
            return list;
        }

        private static int LastIndexOfIslaBooking(string s)
        {
            var pos = s.LastIndexOf("\"type\"", StringComparison.OrdinalIgnoreCase);
            while (pos >= 0)
            {
                var tail = s.AsSpan(pos, Math.Min(300, s.Length - pos)).ToString();
                if (tail.IndexOf("isla.booking", StringComparison.OrdinalIgnoreCase) >= 0)
                    return pos;
                pos = s.LastIndexOf("\"type\"", pos - 1, StringComparison.OrdinalIgnoreCase);
            }
            return -1;
        }

        private static string? ExpandToJsonObject(string s, int hintIndex)
        {
            int left = hintIndex;
            while (left >= 0 && s[left] != '{') left--;
            if (left < 0) return null;

            int depth = 0;
            bool inStr = false;
            char prev = '\0';
            for (int i = left; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && prev != '\\') inStr = !inStr;
                if (!inStr)
                {
                    if (c == '{') depth++;
                    else if (c == '}' && --depth == 0)
                        return s.Substring(left, i - left + 1);
                }
                prev = c;
            }
            return null;
        }

        private static Dictionary<string, string>? TryParseIfIslaBooking(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                if (!root.TryGetProperty("type", out var el) ||
                    el.ValueKind != JsonValueKind.String ||
                    !string.Equals(el.GetString(), "isla.booking", StringComparison.OrdinalIgnoreCase))
                    return null;

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in root.EnumerateObject())
                {
                    switch (p.Value.ValueKind)
                    {
                        case JsonValueKind.String: dict[p.Name] = p.Value.GetString()!; break;
                        case JsonValueKind.Number: dict[p.Name] = p.Value.ToString(); break;
                        case JsonValueKind.True:
                        case JsonValueKind.False: dict[p.Name] = p.Value.GetBoolean().ToString(); break;
                    }
                }
                return dict;
            }
            catch { return null; }
        }
        private static string StaticQuoteHtml()
        {
            var css = """
    <style>
      body { font-family: Arial, Helvetica, sans-serif; color:#111; margin:24px; }
      h1,h2,h3 { margin: 0 0 12px; }
      h1 { font-size: 22px; letter-spacing: .3px; }
      h2 { font-size: 16px; margin-top: 24px; border-bottom: 1px solid #e5e5e5; padding-bottom: 6px; }
      p  { margin: 6px 0 10px; }
      .muted { color:#555; }
      .grid { display:grid; grid-template-columns: 180px 1fr; gap:8px 16px; }
      .card { border:1px solid #e5e5e5; border-radius:10px; padding:14px; margin:10px 0; }
      table { width:100%; border-collapse:collapse; margin: 6px 0 14px; }
      th, td { border-bottom:1px solid #eee; padding:8px 6px; text-align:left; vertical-align:top; }
      th { font-weight:600; font-size: 13px; }
      tfoot td { border-top:1px solid #ddd; font-weight:600; }
      .right { text-align:right; }
      .small { font-size:12px; }
      .totals td { border:none; padding:4px 0; }
      .totals .line { border-bottom:1px dashed #ddd; }
      a { color:#0a58ca; text-decoration:none; }
    </style>
    """;

            return $"""
    <!doctype html><html><head><meta charset="utf-8"><title>Microhire | ARCSOPT Meeting</title>{css}</head><body>
      <h1>ARCSOPT MEETING — Proposal</h1>
      <div class="grid">
        <div>Client</div><div><strong>ARCSOPT</strong> — Megan Suurenbroek, admin@arcsopt.org</div>
        <div>Venue</div><div><strong>The Westin Brisbane</strong>, 111 Mary Street Brisbane City QLD 4000 — Room: <strong>Westin Ballroom I</strong></div>
        <div>Dates</div><div>Fri 17 Oct 2025 — Setup 07:30, Rehearsal 07:30, Event 08:00 to 17:00</div>
        <div>Account Manager</div><div>Nishal Kumar · +61 04 84814633 · nishal.kumar@microhire.com.au</div>
        <div>Reference</div><div>C1374000002 - 001</div>
      </div>

      <div class="card">
        <p>Dear Megan,</p>
        <p class="muted small">
          Thank you for the opportunity to present our audio-visual production services for your upcoming event at The Westin Brisbane.
          Based on the information received, we recommend the following for a smooth event.
        </p>
      </div>

      <h2>Equipment & Services</h2>

      <h3>Vision</h3>
      <table>
        <thead><tr><th>Description</th><th class="right">Qty</th><th class="right">Line Total</th></tr></thead>
        <tbody>
          <tr><td>Westin Ballroom Single Projector Package<br><span class="small muted">Full HD Digital Projector, 120" motorised 16:9 screen, HDMI, client laptop at lectern, wireless presenter</span></td><td class="right">1</td><td class="right">$619.10</td></tr>
        </tbody>
        <tfoot><tr><td colspan="2" class="right">Vision Total</td><td class="right">$619.10</td></tr></tfoot>
      </table>

      <h3>Audio</h3>
      <table>
        <thead><tr><th>Description</th><th class="right">Qty</th><th class="right">Line Total</th></tr></thead>
        <tbody>
          <tr><td>Ceiling Speaker System</td><td class="right">1</td><td class="right">$0.00</td></tr>
          <tr><td>6 Channel Audio Mixer</td><td class="right">1</td><td class="right">$0.00</td></tr>
          <tr><td>2× Wireless Handheld (Shure QLXD4 K52)</td><td class="right">2</td><td class="right">$584.42</td></tr>
        </tbody>
        <tfoot><tr><td colspan="2" class="right">Audio Total</td><td class="right">$584.42</td></tr></tfoot>
      </table>

      <h2>Technical Services</h2>
      <table>
        <thead><tr><th>Task</th><th>Start</th><th>Finish</th><th class="right">Hrs</th><th class="right">Total ($)</th></tr></thead>
        <tbody>
          <tr><td>AV Technician Setup</td><td>17/10/25 07:30</td><td>17/10/25 09:00</td><td class="right">1.5</td><td class="right">$165.00</td></tr>
          <tr><td>AV Technician Test & Connect</td><td>17/10/25 08:30</td><td>17/10/25 09:30</td><td class="right">1.0</td><td class="right">$110.00</td></tr>
          <tr><td>AV Technician Pack Down</td><td>17/10/25 18:00</td><td>17/10/25 19:00</td><td class="right">1.0</td><td class="right">$110.00</td></tr>
        </tbody>
        <tfoot><tr><td colspan="4" class="right">Labour Total</td><td class="right">$385.00</td></tr></tfoot>
      </table>

      <h2>Budget Summary</h2>
      <table class="totals">
        <tbody>
          <tr><td class="right" style="width:80%;">Rental Equipment</td><td class="right">$1,203.52</td></tr>
          <tr><td class="right">Labour</td><td class="right">$385.00</td></tr>
          <tr class="line"><td class="right">Service Charge</td><td class="right">$120.35</td></tr>
          <tr><td class="right">Sub Total (ex GST)</td><td class="right">$1,708.87</td></tr>
          <tr class="line"><td class="right">GST</td><td class="right">$170.89</td></tr>
          <tr><td class="right"><strong>Total</strong></td><td class="right"><strong>$1,879.76</strong></td></tr>
        </tbody>
      </table>
      <p class="small muted">Pricing valid until Wed 7 May 2025. Resources subject to availability at booking.</p>

      <h2>Confirmation of Services</h2>
      <div class="card small">
        <p>On behalf of ARCSOPT, I accept this proposal and wish to proceed. We understand equipment and personnel are not allocated until this is signed and returned. This proposal is subject to Microhire’s terms & conditions.</p>
        <p><a href="https://www.microhire.com.au/terms-conditions/">microhire.com.au/terms-conditions/</a></p>
        <p><strong>Total Quotation Amount:</strong> $1,879.76 inc GST · <strong>Reference:</strong> C1374000002 - 001</p>
      </div>
    </body></html>
    """;
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
        public async Task<decimal?> UpsertContactByEmailAsync(
            BookingDbContext db,
            string? fullName,
            string? email,
            string? phoneE164,
            string? position,
            CancellationToken ct)
        {
            try
            {
                static DateTime NowAest()
                {
#if WINDOWS
            var tz = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
#else
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
#endif
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                }

                var now = NowAest();

                static string? Trunc(string? s, int len)
                    => string.IsNullOrEmpty(s) ? s : (s.Length <= len ? s : s[..len]);

                static bool LooksLikeAssistantName(string? s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return false;
                    var t = s.Trim().ToLowerInvariant();
                    return t.Contains("isla") || t.Contains("microhire");
                }

                static bool LooksLikeMissing(string? value)
                {
                    if (string.IsNullOrWhiteSpace(value)) return true;
                    var v = value.Trim().ToLowerInvariant();
                    return v is "no" or "none" or "n/a" or "na" or "nil"
                             or "unknown" or "tbc" or "not sure"
                             or "dont know" or "don't know";
                }

                static string? NormalizePosition(string? p)
                {
                    if (LooksLikeMissing(p)) return null;
                    if (string.IsNullOrWhiteSpace(p)) return null;
                    p = Regex.Replace(p, @"\s+", " ").Trim();
                    return p.Length < 2 ? null : p;
                }

                // NAME SPLIT
                (string? first, string? middle, string? last, string? displayRaw) SplitName(string? name)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        return (null, null, null, null);

                    static string Cap(string s) =>
                        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

                    var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 1)
                        return (Cap(parts[0]), null, null, Cap(parts[0]));

                    if (parts.Length == 2)
                        return (Cap(parts[0]), null, Cap(parts[1]), Cap(name));

                    return (
                        Cap(parts[0]),
                        Cap(string.Join(' ', parts.Skip(1).Take(parts.Length - 2))),
                        Cap(parts[^1]),
                        Cap(name)
                    );
                }

                var (first, middle, last, displayRaw) = SplitName(fullName);

                if (string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase))
                    middle = null;

                string? display = LooksLikeAssistantName(displayRaw) ? null : displayRaw;

                // --------------------------
                // EMAIL-ONLY LOOKUP
                // --------------------------
                TblContact? existing = null;

                if (!string.IsNullOrWhiteSpace(email))
                {
                    var e = email.Trim().ToLowerInvariant();
                    existing = await db.Contacts
                        .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == e, ct);

                    if (existing != null &&
                        LooksLikeAssistantName(existing.Contactname) &&
                        !string.IsNullOrWhiteSpace(display))
                    {
                        existing.Contactname = Trunc(display, 35);
                    }
                }

                // NORMALIZED POSITION
                string? pos = Trunc(NormalizePosition(position), 50);

                // CREATE NEW CONTACT
                if (existing is null)
                {
                    if (string.IsNullOrWhiteSpace(display) &&
                        string.IsNullOrWhiteSpace(email) &&
                        string.IsNullOrWhiteSpace(phoneE164) &&
                        string.IsNullOrWhiteSpace(pos))
                        return null;

                    var row = new TblContact
                    {
                        Contactname = Trunc(display, 35),
                        Firstname = Trunc(first, 25),
                        MidName = string.IsNullOrWhiteSpace(middle) ? null : Trunc(middle, 35),
                        Surname = Trunc(last, 35),
                        Email = Trunc(email, 80),
                        Cell = Trunc(phoneE164, 16),
                        Active = "Y",
                        CreateDate = now,
                        LastContact = now,
                        LastAttempt = now,
                        LastUpdate = now,
                        Position = pos
                    };

                    db.Contacts.Add(row);
                    await db.SaveChangesAsync(ct);
                    return row.Id;
                }

                // UPDATE EXISTING CONTACT
                if (!string.IsNullOrWhiteSpace(display))
                    existing.Contactname = Trunc(display, 35);

                if (!string.IsNullOrWhiteSpace(first))
                    existing.Firstname = Trunc(first, 25);

                if (!string.IsNullOrWhiteSpace(middle) &&
                    !string.Equals(middle, "from", StringComparison.OrdinalIgnoreCase))
                    existing.MidName = Trunc(middle, 35);

                if (!string.IsNullOrWhiteSpace(last))
                    existing.Surname = Trunc(last, 35);

                if (!string.IsNullOrWhiteSpace(email))
                    existing.Email = Trunc(email, 80);

                if (!string.IsNullOrWhiteSpace(phoneE164))
                    existing.Cell = Trunc(phoneE164, 16);

                if (!string.IsNullOrWhiteSpace(pos))
                    existing.Position = pos;

                existing.Active = existing.Active ?? "Y";
                existing.LastContact = now;
                existing.LastAttempt = now;
                existing.LastUpdate = now;

                await db.SaveChangesAsync(ct);
                return existing.Id;
            }
            catch (OperationCanceledException) { throw; }
            catch (DbUpdateException ex)
            {
                Exception root = ex;
                while (root.InnerException != null)
                    root = root.InnerException;

                throw new InvalidOperationException($"tblContact upsert failed: {root.Message}", ex);
            }
        }

        static bool LooksLikeAssistantName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim().ToLowerInvariant();
            if (t.Contains("isla") && t.Contains("microhire")) return true;
            if (t.Equals("isla")) return true;
            if (t.StartsWith("my name is ")) return true;   // safety
            return false;

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

        // Robust 429 retry with exponential backoff + jitter.
        // Use for ALL Azure Agents SDK calls that can rate-limit.
        private static async Task<T> With429RetryAsync<T>(Func<Task<T>> action, string label, CancellationToken ct)
        {
            var rand = new Random();
            int attempt = 0;

            // Start at ~2s; we’ll back off up to ~20s.
            var delay = TimeSpan.FromSeconds(2);

            // Helper: try to extract Retry-After seconds from exception
            static int? TryGetRetryAfterSeconds(Azure.RequestFailedException ex)
            {
                // 1) Some SDKs stash it in ex.Data
                try
                {
                    if (ex.Data != null)
                    {
                        if (ex.Data["Retry-After"] is string raStr && int.TryParse(raStr, out var secs1))
                            return secs1;
                        if (ex.Data["retry-after"] is string raStr2 && int.TryParse(raStr2, out var secs2))
                            return secs2;
                    }
                }
                catch { /* ignore */ }

                // 2) Sometimes the message includes it (best-effort)
                try
                {
                    var m = Regex.Match(ex.Message ?? string.Empty, @"Retry-After[:=\s]+(?<s>\d+)", RegexOptions.IgnoreCase);
                    if (m.Success && int.TryParse(m.Groups["s"].Value, out var secs))
                        return secs;
                }
                catch { /* ignore */ }

                return null;
            }

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    return await action();
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 429)
                {
                    attempt++;

                    // Prefer server hint if we can find one; otherwise use our current delay.
                    var hinted = TryGetRetryAfterSeconds(ex);
                    var wait = hinted.HasValue ? TimeSpan.FromSeconds(hinted.Value) : delay;

                    // Add +/-30% jitter
                    var ms = (int)wait.TotalMilliseconds;
                    var withJitter = rand.Next((int)(ms * 0.7), (int)(ms * 1.3));
                    await Task.Delay(withJitter, ct);

                    // Exponential backoff up to ~20s
                    var nextMs = Math.Min(ms * 2, 20_000);
                    delay = TimeSpan.FromMilliseconds(nextMs);
                }
                catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.Message.Contains("429"))
                {
                    // Some layers may surface 429 as HttpRequestException
                    attempt++;
                    var baseMs = Math.Min(2000 * Math.Pow(2, attempt - 1), 20000); // 2s -> 4 -> 8 -> 16 -> 20
                    var jitter = rand.Next((int)(baseMs * 0.7), (int)(baseMs * 1.3));
                    await Task.Delay(jitter, ct);
                }
            }
        }
        private static readonly Regex ChooseScheduleRe =
    new(@"^Choose\s*schedule:\s*(?<pairs>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private sealed class ChosenSchedule
        {
            public TimeSpan? Setup { get; set; }
            public TimeSpan? Rehearsal { get; set; }
            public TimeSpan? Start { get; set; }
            public TimeSpan? End { get; set; }
            public TimeSpan? PackUp { get; set; }
        }


        public async Task SaveConversationToBookNoteAsync(
            BookingDbContext db,
            string bookingNo,
            string userText,
            string assistantText,
            CancellationToken ct,
            decimal? operatorId = null)
        {
            // Get next line number for this booking
            var maxLine = await db.TblBooknotes
                .Where(n => n.BookingNo == bookingNo)
                .Select(n => (int?)n.LineNo)
                .MaxAsync(ct) ?? 0;

            var rowUser = new TblBooknote
            {
                BookingNo = bookingNo,
                LineNo = (byte)(maxLine + 1),
                TextLine = userText,
                NoteType = 0,            // 1 = user
                OperatorId = operatorId
            };

            var rowAi = new TblBooknote
            {
                BookingNo = bookingNo,
                LineNo = (byte)(maxLine + 2),
                TextLine = assistantText,
                NoteType = 0,            // 2 = assistant
                OperatorId = operatorId
            };

            db.TblBooknotes.Add(rowUser);
            db.TblBooknotes.Add(rowAi);
            await db.SaveChangesAsync(ct);
        }
        private static string JoinParts(DisplayMessage m) =>
    string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
        private static string BuildTranscriptForBooknote(IEnumerable<DisplayMessage> messages)
        {
            var sb = new StringBuilder();

            foreach (var m in messages)
            {
                var role = (m.Role ?? "").Trim().ToLowerInvariant();
                string label = role switch
                {
                    "assistant" => "Agent",
                    "user" => "User",
                    _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(role)
                };

                var body = string.Join("\n", m.Parts ?? Enumerable.Empty<string>()).Trim();
                if (string.IsNullOrWhiteSpace(body))
                    continue;

                // blank line between messages
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                // "Agent: ..." / "User: ..."
                sb.Append(label);
                sb.Append(": ");
                sb.Append(body);
            }

            return sb.ToString();
        }

        private static byte NoteTypeFor(string role) =>
            string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)2;

        /*
         * Rebuilds tblbooknote for the given booking:
         * - deletes existing rows for that booking
         * - writes one row per message in order (user=1, assistant=2)
         */
        public async Task SaveFullTranscriptToBooknoteAsync(
     BookingDbContext db,
     string bookingNo,
     IEnumerable<DisplayMessage> messages,
     CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(bookingNo) || messages == null)
                return;

            // ---------- build "Agent: ..." / "User: ..." transcript ----------
            var transcript = BuildTranscriptForBooknote(messages);
            if (string.IsNullOrWhiteSpace(transcript))
                return;

            static DateTime NowAest()
            {
#if WINDOWS
        var tz = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
#else
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
#endif
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }

            var now = NowAest();

            // ---------- save into your booknote table ----------
            // Adjust this part to match your actual TblBooknote entity/columns.

            var existing = await db.TblBooknotes
                .FirstOrDefaultAsync(b =>
                    b.BookingNo == bookingNo &&
                    b.NoteType == 1,  // or whatever flag you use
                    ct);

            if (existing == null)
            {
                var row = new TblBooknote
                {
                    BookingNo = bookingNo,
                    NoteType = 1,
                    TextLine = transcript
                };

                db.TblBooknotes.Add(row);
            }
            else
            {
                existing.TextLine = transcript;
                
            }

            await db.SaveChangesAsync(ct);
        }


        private static bool TryCaptureScheduleSelection(string text, out ChosenSchedule schedule)
        {
            schedule = new();
            if (string.IsNullOrWhiteSpace(text)) return false;

            var m = ChooseScheduleRe.Match(text.Trim());
            if (!m.Success) return false;

            var pairs = (m.Groups["pairs"].Value ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in pairs)
            {
                var kv = raw.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].Trim().ToLowerInvariant();
                var val = kv[1].Trim();

                if (!TimeSpan.TryParse(val, out var ts)) continue;

                switch (key)
                {
                    case "setup": schedule.Setup = ts; break;
                    case "rehearsal": schedule.Rehearsal = ts; break;
                    case "start": schedule.Start = ts; break;
                    case "end": schedule.End = ts; break;
                    case "packup":
                    case "pack_down":
                    case "packdown": schedule.PackUp = ts; break;
                }
            }

            return schedule.Setup.HasValue || schedule.Rehearsal.HasValue ||
                   schedule.Start.HasValue || schedule.End.HasValue || schedule.PackUp.HasValue;
        }

        private static string ToAmPm(TimeSpan t)
        {
            var dt = DateTime.Today + t;
            return dt.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-AU"));
        }

        // NOTE: your ExtractEventDate returns DateTimeOffset? → format that
        private static string FormatPrettyDate(DateTimeOffset? d)
        {
            if (d is null) return "your date";
            return d.Value.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU"));
        }

        private static string BuildScheduleConfirmation(ChosenSchedule s, DateTimeOffset? date)
        {
            var lines = new List<string>
    {
        $"Your schedule for {FormatPrettyDate(date)} is confirmed:",
        ""
    };

            if (s.Setup.HasValue) lines.Add($"Setup: {ToAmPm(s.Setup.Value)}");
            if (s.Rehearsal.HasValue) lines.Add($"Rehearsal: {ToAmPm(s.Rehearsal.Value)}");
            if (s.Start.HasValue) lines.Add($"Event Start: {ToAmPm(s.Start.Value)}");
            if (s.End.HasValue) lines.Add($"Event End: {ToAmPm(s.End.Value)}");
            if (s.PackUp.HasValue) lines.Add($"Pack Up: {ToAmPm(s.PackUp.Value)}");

            return string.Join("\n", lines);
        }

        // Persist the chosen points in your draft store/session (adjust to your store type)
        private void SaveScheduleToDraft(string threadId, ChosenSchedule s)
        {
            var session = _http.HttpContext?.Session;
            if (session == null) return;

            if (s.Start.HasValue) session.SetString("Draft:StartTime", s.Start.Value.ToString(@"hh\:mm"));
            if (s.End.HasValue) session.SetString("Draft:EndTime", s.End.Value.ToString(@"hh\:mm"));
            if (s.Setup.HasValue) session.SetString("Draft:SetupTime", s.Setup.Value.ToString(@"hh\:mm"));
            if (s.Rehearsal.HasValue) session.SetString("Draft:RehearsalTime", s.Rehearsal.Value.ToString(@"hh\:mm"));
            if (s.PackUp.HasValue) session.SetString("Draft:PackUpTime", s.PackUp.Value.ToString(@"hh\:mm"));
        }
        // ----------------------------- BOOKING SAVE HELPERS -----------------------------

        private static string? Trunc(string? s, int len)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= len ? s : s.Substring(0, len));

        private static TimeSpan? ParseTime(string? s)
            => !string.IsNullOrWhiteSpace(s) && TimeSpan.TryParse(s, out var t) ? t : (TimeSpan?)null;

        private static int? ToHHmm(TimeSpan? ts)
            => ts.HasValue ? (ts.Value.Hours * 100 + ts.Value.Minutes) : (int?)null;

        private static DateTime NowAest()
        {
#if WINDOWS
    var tz = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
#else
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
#endif
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        // “Choose room: Westin Ballroom …” / “Choose layout: Theatre”
        private static string? ExtractLastChoice(IEnumerable<DisplayMessage> messages, string prefix)
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
        private static int? ExtractAttendees(IEnumerable<DisplayMessage> messages)
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
        private static string? ExtractEventType(IEnumerable<DisplayMessage> messages)
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

        private async Task<string> GenerateNextBookingNoAsync(BookingDbContext db, CancellationToken ct)
        {
            const string prefix = "C1374";
            const int suffixWidth = 6; // zero-padded digits after the prefix, e.g. 000000

            // Pull existing booking numbers with our prefix
            var existing = await db.TblBookings
                .AsNoTracking()
                .Where(b => b.booking_no != null && b.booking_no.StartsWith(prefix))
                .Select(b => b.booking_no!)
                .ToListAsync(ct);

            // Find the max numeric suffix
            int maxSeq = -1;
            foreach (var bk in existing)
            {
                var suffix = bk.Length > prefix.Length ? bk.Substring(prefix.Length) : "";
                // Keep only digits in case there’s any noise
                var digits = new string(suffix.Where(char.IsDigit).ToArray());

                if (int.TryParse(digits, out var n))
                    if (n > maxSeq) maxSeq = n;
            }

            var nextSeq = maxSeq + 1; // start at 0 if none found

            // Compose: prefix + zero-padded 6-digit suffix
            return $"{prefix}{nextSeq.ToString().PadLeft(suffixWidth, '0')}";
        }

        private async Task<TblBooking> GetOrCreateBookingAsync(
            BookingDbContext db, string bookingNo, CancellationToken ct)
        {
            // 0) Reuse an Added (unsaved) instance for this booking_no if it exists
            var added = db.ChangeTracker.Entries<TblBooking>()
                          .FirstOrDefault(e => e.State == EntityState.Added &&
                                               e.Entity.booking_no == bookingNo)
                          ?.Entity;
            if (added != null) return added;

            // 1) Already tracked (any state)
            var tracked = db.ChangeTracker.Entries<TblBooking>()
                            .FirstOrDefault(e => e.Entity.booking_no == bookingNo)
                            ?.Entity;
            if (tracked != null) return tracked;

            // 2) Exists in DB (tracked by default)
            var existing = await db.TblBookings
                                   .FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
            if (existing != null) return existing;

            // 3) Create brand new (EF will give it a temporary key until SaveChanges)
            var row = new TblBooking
            {
                booking_no = bookingNo,
                BookingProgressStatus = 1
            };
            db.TblBookings.Add(row);
            return row;
        }
        static string NormalizeBullets(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            raw = raw.Replace("\r", "");
            raw = raw.Replace('–', '-').Replace('—', '-')
                     .Replace('’', '\'').Replace('“', '"').Replace('”', '"');
            return raw;
        }

        static Dictionary<string, string> ParseSummaryFacts(string raw)
        {
            var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            raw = NormalizeBullets(raw);

            foreach (var line in raw.Split('\n')
                                    .Select(l => l.Trim('•', '-', ' ', '\t'))
                                    .Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var label = line.Substring(0, idx).Trim();
                var value = line[(idx + 1)..].Trim();
                if (!facts.ContainsKey(label)) facts[label] = value;
            }
            return facts;
        }

        private string GetDataFilePath(string fileName)
        {
            // wwwroot/data first
            if (!string.IsNullOrWhiteSpace(_env.WebRootPath))
            {
                var p = Path.Combine(_env.WebRootPath, "data", fileName);
                if (System.IO.File.Exists(p)) return p;
            }
            // content root + wwwroot/data
            if (!string.IsNullOrWhiteSpace(_env.ContentRootPath))
            {
                var p = Path.Combine(_env.ContentRootPath, "wwwroot", "data", fileName);
                if (System.IO.File.Exists(p)) return p;
            }
            // fallback
            var f = Path.Combine(AppContext.BaseDirectory, "wwwroot", "data", fileName);
            return System.IO.File.Exists(f) ? f : string.Empty;
        }

        private static DateTime? ToDateTime(object? value)
        {
            // Works for DateTime?, DateOnly?, or null
            if (value is DateTime dt) return dt;
            if (value is DateTime ndt) return ndt;
            if (value is DateOnly d) return d.ToDateTime(TimeOnly.MinValue);
            if (value is DateOnly nd) return nd.ToDateTime(TimeOnly.MinValue);
            return null;
        }


        public async Task<decimal?> TrySaveOrganisationAsync(
            BookingDbContext db,
            IEnumerable<DisplayMessage> messages,
            CancellationToken ct)
        {
            if (messages == null) return null;

            var (org, addr) = ExtractOrganisationFromTranscript(messages);
            if (string.IsNullOrWhiteSpace(org) && string.IsNullOrWhiteSpace(addr))
                return null;

            // If only address is present, we still upsert using a placeholder org if needed.
            // You can tighten this if you require org.
            if (string.IsNullOrWhiteSpace(org))
                org = "Unknown Organisation";

            return await UpsertOrganisationAsync(db, org!, addr, ct);
        }
        private static string MakeCustomerCodeFromId(decimal id) => "C" + Convert.ToInt32(id);

public async Task<decimal?> UpsertOrganisationAsync(
    BookingDbContext db,
    string organisation,
    string? address,
    CancellationToken ct)
    {
        if (db == null) return null;

        var org = Normalize(organisation);
        var addr = Normalize(address);

        if (string.IsNullOrWhiteSpace(org) && string.IsNullOrWhiteSpace(addr))
            return null;

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // try existing by name (case-insensitive)
            var existing = await db.TblCusts
                .AsTracking()
                .FirstOrDefaultAsync(c =>
                    c.OrganisationV6 != null &&
                    c.OrganisationV6.ToLower() == org.ToLower(), ct);

            if (existing is null)
            {
                // 1) Insert with a UNIQUE placeholder to satisfy the UNIQUE KEY
                var row = new TblCust
                {
                    OrganisationV6 = Trunc(org, 50),
                    Address_l1V6 = Trunc(addr, 50),
                    Customer_code = MakeTempCustomerCode() // <— unique placeholder
                };

                await db.TblCusts.AddAsync(row, ct);
                await db.SaveChangesAsync(ct); // ID is now generated

                // 2) Replace placeholder with final ID-based code (C#####)
                row.Customer_code = MakeCustomerCodeFromId(row.ID);
                await db.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);
                return row.ID;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(addr))
                    existing.Address_l1V6 = Trunc(addr, 50);

                if (string.IsNullOrWhiteSpace(existing.Customer_code))
                    existing.Customer_code = MakeCustomerCodeFromId(existing.ID);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return existing.ID;
            }
        }
        catch (DbUpdateException ex)
        {
            // bubble the exact SQL cause to logs/UI
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException($"Failed to save organisation record: {detail}", ex);
        }

        // ---- helpers ----
        static string? Trunc(string? s, int len)
            => string.IsNullOrWhiteSpace(s) ? s : (s!.Length <= len ? s : s[..len]);

        static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = Regex.Replace(s, @"\s+", " ").Trim();
            return t.Trim(';', ':', '.', ',');
        }

        static string MakeCustomerCodeFromId(decimal id)
        {
            // e.g. ID=14415 → "C14415"
            var n = (long)id;
            return $"C{n:D5}";
        }

        static string MakeTempCustomerCode()
        {
            // ultra-low collision temporary value under 35 chars
            // e.g. "C_TMP_638353004123456789_ab12"
            var ticks = DateTime.UtcNow.Ticks;
            var rand = Guid.NewGuid().ToString("N")[..4];
            return $"C_TMP_{ticks}_{rand}";
        }
    }

    public async Task<(decimal id, string code, string name)?> FindOrganisationAsync(
    BookingDbContext db, string organisation, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(organisation)) return null;
            var norm = organisation.Trim().ToLower();

            var org = await db.TblCusts
                .Where(c => c.OrganisationV6 != null && c.OrganisationV6.ToLower() == norm)
                .Select(c => new { c.ID, c.Customer_code, c.OrganisationV6 })
                .FirstOrDefaultAsync(ct);

            if (org == null) return null;

            var code = string.IsNullOrWhiteSpace(org.Customer_code) ? "C" + Convert.ToInt32(org.ID) : org.Customer_code!;
            return (org.ID, code, org.OrganisationV6!);
        }
        public async Task<string?> GetCustomerCodeByIdAsync(BookingDbContext db, decimal orgId, CancellationToken ct)
        {
            var c = await db.TblCusts.Where(x => x.ID == orgId)
                .Select(x => new { x.ID, x.Customer_code })
                .FirstOrDefaultAsync(ct);
            if (c == null) return null;
            return string.IsNullOrWhiteSpace(c.Customer_code) ? "C" + Convert.ToInt32(c.ID) : c.Customer_code!;
        }
        public async Task LinkContactToOrganisationAsync(
            BookingDbContext db, string customerCode, decimal contactId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(customerCode) || contactId <= 0) return;

            var exists = await db.Set<TblLinkCustContact>()
                .AnyAsync(x => x.Customer_Code == customerCode && x.ContactID == contactId, ct);
            if (exists) return;

            db.Set<TblLinkCustContact>().Add(new TblLinkCustContact
            {
                Customer_Code = customerCode,
                ContactID = contactId
            });

            await db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Pulls organisation + address from the latest USER messages.
        /// Handles variants like:
        ///  - "Dhan Corp and address is 123 Fiesta St, Melbourne"
        ///  - "Organisation: Dhan Corp, Address: 123 Fiesta St, Melbourne"
        ///  - "Company is Dhan Corp; address 123 Fiesta St, Melbourne"
        /// </summary>
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


        static int? FirstInt(string s)
        {
            var m = Regex.Match(s ?? "", @"\d+");
            return m.Success && int.TryParse(m.Value, out var n) ? n : (int?)null;
        }
        // Models for rules ------------------------------------------------------------
        public sealed class ItemRule
        {
            [JsonPropertyName("type")] public string? type { get; set; } // "driver" | "bundle"

            // driver fields
            [JsonPropertyName("key")] public string? key { get; set; }
            [JsonPropertyName("product_code")] public string? product_code { get; set; }
            [JsonPropertyName("sourceLabel")] public string? sourceLabel { get; set; }
            [JsonPropertyName("qtyFrom")] public string? qtyFrom { get; set; }         // "regex" | "labelNumber"
            [JsonPropertyName("regex")] public string? regex { get; set; }
            [JsonPropertyName("multiplier")] public int? multiplier { get; set; }

            // bundle fields
            [JsonPropertyName("requirePresentation")] public bool? requirePresentation { get; set; }
            [JsonPropertyName("roomContains")] public string[]? roomContains { get; set; }
            [JsonPropertyName("bundleItems")] public List<BundleItem>? bundleItems { get; set; }
        }

        public sealed class BundleItem
        {
            [JsonPropertyName("product_code")] public string product_code { get; set; } = "";
            [JsonPropertyName("comment")] public string? comment { get; set; }

            // quantity strategies
            [JsonPropertyName("qtyFrom")] public string qtyFrom { get; set; } = "literal"; // "literal" | "optionalFixed" | "useDriverQty" | "maxOf"

            // literal / optionalFixed
            [JsonPropertyName("literalQty")] public int? literalQty { get; set; }

            // useDriverQty
            [JsonPropertyName("driverKey")] public string? driverKey { get; set; }

            // maxOf
            [JsonPropertyName("driverKeys")] public string[]? driverKeys { get; set; }
            [JsonPropertyName("defaultQty")] public int? defaultQty { get; set; }
        }
        static bool LooksLikeBotPerson(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim().ToLowerInvariant();
            // be aggressive here – anything containing "isla" we treat as bot
            return t.Contains("isla") || t.Contains("microhire");
        }

        public async Task UpsertItemsFromSummaryAsync(
           BookingDbContext db, ISession session,
           IEnumerable<DisplayMessage> messages, IDictionary<string, string> facts, CancellationToken ct)
        {
            var lastAssistant = messages.LastOrDefault(m =>
                string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
            if (lastAssistant == null) return;

            var raw = string.Join("\n", lastAssistant.Parts ?? Enumerable.Empty<string>());

            // ---------- NORMALIZATION HELPERS ----------
            static string Norm(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var sb = new StringBuilder(s.Length);
                foreach (var ch in s)
                {
                    if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                        sb.Append(char.ToLowerInvariant(ch));
                }
                return Regex.Replace(sb.ToString().Trim(), @"\s+", " ");
            }

            string? FindValue(params string[] labelHints)
            {
                var hints = labelHints.Select(Norm).ToArray();
                foreach (var kv in facts)
                {
                    var k = Norm(kv.Key);
                    if (hints.Any(h => k.Contains(h)))
                        return kv.Value;
                }
                return null;
            }

            int ParseCountFromValue(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return 0;
                var m = Regex.Match(value, "(\\d+)");
                return (m.Success && int.TryParse(m.Groups[1].Value, out var n)) ? n : 0;
            }

            int ParseCount(params string[] labelHints) => ParseCountFromValue(FindValue(labelHints));

            // ---------- COUNTS ----------
            var speakersCountLabel = Math.Max(ParseCount("number of speakers", "speakers", "speaker"), 0);
            var projectorsCountLabel = Math.Max(ParseCount("number of projectors", "projectors", "projector"), 0);

            var laptopsFromExplicit = Math.Max(ParseCount("laptops required for presenters",
                                                          "laptop required for presenters",
                                                          "laptops", "laptop"), 0);
            var laptopsFromPresenters = Math.Max(ParseCount("number of presenters", "presenters", "presenter"), 0);
            var laptopsFromPresentns = Math.Max(ParseCount("number of presentations", "presentations", "presentation"), 0);
            var laptopsCount = Math.Max(laptopsFromExplicit, Math.Max(laptopsFromPresenters, laptopsFromPresentns));

            // ---- NEGATIVE SIGNALS: presenter/client will bring their own laptop ----
            static bool ContainsAny(string? s, params string[] phrases)
                => !string.IsNullOrWhiteSpace(s) &&
                   phrases.Any(p => s!.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

            var presenterDetails = FindValue("presenter details", "presenter", "presenters") ?? string.Empty;
            var avNotes = FindValue("av", "audio visual", "audio-visual") ?? string.Empty;
            var extraNotes = FindValue("notes", "requirements", "adjustments") ?? string.Empty;

            bool presenterBringsLaptop =
                ContainsAny(presenterDetails, "laptop provided by presenter", "presenter provided laptop",
                            "presenter will bring laptop", "bring own laptop", "byo laptop",
                            "using own laptop", "client will bring laptop", "client provided laptop", "presenter’s laptop") ||
                ContainsAny(avNotes, "presenter will bring laptop", "bring own laptop", "byo laptop", "using own laptop") ||
                ContainsAny(extraNotes, "presenter will bring laptop", "bring own laptop", "byo laptop", "using own laptop");

            if (presenterBringsLaptop)
            {
                laptopsCount = 0; // do not add our laptop bundle/components
            }
            else
            {
                // NEW: infer laptops from AV text like: "Projector, screen, laptop for the speaker"
                int laptopsFromAv = 0;
                var lm = Regex.Match(avNotes, @"\b(\d+)\s*laptops?\b", RegexOptions.IgnoreCase);
                if (lm.Success && int.TryParse(lm.Groups[1].Value, out var ln))
                {
                    laptopsFromAv = ln;
                }
                else if (Regex.IsMatch(avNotes, @"\blaptop\b", RegexOptions.IgnoreCase))
                {
                    // At least one laptop mentioned with no explicit number
                    laptopsFromAv = 1;
                }

                if (laptopsFromAv > 0)
                {
                    laptopsCount = Math.Max(laptopsCount, laptopsFromAv);
                }
            }

            // ---------- EQUIPMENT TEXT ----------
            var equipmentText = FindValue("equipment", "audio visual", "audio-visual") ?? string.Empty;
            var micsFromEquip = Regex.Match(equipmentText, @"\b(\d+)\s*(wireless\s+microphones?|mics?)\b").Groups[1].Value switch
            {
                var s when int.TryParse(s, out var n) => n,
                _ => 0
            };
            var projectorsFromEquip = Regex.IsMatch(equipmentText, @"projector", RegexOptions.IgnoreCase) ? 1 : 0;
            var speakersFromEquip = Regex.IsMatch(equipmentText, @"speakers?", RegexOptions.IgnoreCase) ? 1 : 0;

            var speakersCount = Math.Max(speakersCountLabel, speakersFromEquip);
            var projectorsCount = Math.Max(projectorsCountLabel, projectorsFromEquip);
            var wirelessMicCount = Math.Max(speakersCount, micsFromEquip);

            var hasSpeakers = wirelessMicCount > 0;
            var hasProjectors = projectorsCount > 0;
            var hasLaptops = laptopsCount > 0;
            if (!hasSpeakers && !hasProjectors && !hasLaptops) return;

            // ---------- BOOKING ----------
            var bookingNo = session.GetString("Draft:BookingNo");
            if (string.IsNullOrWhiteSpace(bookingNo)) return;

            var booking = await db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
            if (booking == null) return;

            var date = booking.SDate ?? DateTime.Today;

            // ---------- DRIVERS (reserved for future rules) ----------
            var drivers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["wireless_mic_count"] = wirelessMicCount,
                ["projector_count"] = projectorsCount,
                ["laptop_count"] = laptopsCount
            };

            // ---------- wanted list ----------
            var wanted = new Dictionary<string, (int qty, string? comment)>(StringComparer.OrdinalIgnoreCase);
            void AddWanted(string code, int qty, string? cmt)
            {
                if (qty <= 0 || string.IsNullOrWhiteSpace(code)) return;
                var norm = code.Trim().ToUpperInvariant();
                if (wanted.TryGetValue(norm, out var cur))
                    wanted[norm] = (cur.qty + qty, string.IsNullOrWhiteSpace(cmt) ? cur.comment : cmt);
                else
                    wanted[norm] = (qty, cmt);
            }

            // ---------- LAPTOP PACKAGE (PCLPRO) + COMPONENTS METADATA ----------
            if (hasLaptops)
            {
                // Load components for PCLPRO so we can ensure they exist in tblInvmas.
                var comps = await db.VwProdsComponents
                    .AsNoTracking()
                    .Where(v => v.ParentCode == "PCLPRO" && (v.VariablePart == null || v.VariablePart == 0))
                    .ToListAsync(ct);

                if (comps.Count > 0)
                {
                    // Ensure all components exist in tblInvmas (for grouping, pictures, etc.)
                    var compCodes = comps.Select(c => c.ProductCode).ToList();
                    var existingCodes = new HashSet<string>(
                        await db.TblInvmas.AsNoTracking()
                            .Where(m => compCodes.Contains(m.product_code))
                            .Select(m => m.product_code)
                            .ToListAsync(ct),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var c in comps)
                    {
                        if (!existingCodes.Contains(c.ProductCode))
                        {
                            db.TblInvmas.Add(new TblInvmas
                            {
                                product_code = c.ProductCode,
                                groupFld = "LAPTOP"
                            });
                        }
                    }
                    await db.SaveChangesAsync(ct);
                }

                // Add the **package** itself to the wanted list; components will be expanded later.
                if (laptopsCount > 0)
                {
                    AddWanted("PCLPRO", laptopsCount, null);
                }
            }

            // ---------- WIRELESS MIC PACKAGE (QLXD2SK) + COMPONENTS METADATA ----------
            // Assume QLXD2SK provides 2 wireless mics per package. Adjust if different.
            if (hasSpeakers)
            {
                const int perPackageMics = 2; // change to 1 if it's a single-channel kit
                var micPkgQty = (int)Math.Ceiling((double)wirelessMicCount / perPackageMics);

                if (micPkgQty > 0)
                {
                    // Ensure components for QLXD2SK exist in invmas
                    var micComps = await db.VwProdsComponents
                        .AsNoTracking()
                        .Where(v => v.ParentCode == "QLXD2SK" && (v.VariablePart == null || v.VariablePart == 0))
                        .ToListAsync(ct);

                    if (micComps.Count > 0)
                    {
                        var compCodes = micComps.Select(c => c.ProductCode).ToList();
                        var existingCodes = new HashSet<string>(
                            await db.TblInvmas.AsNoTracking()
                                .Where(m => compCodes.Contains(m.product_code))
                                .Select(m => m.product_code)
                                .ToListAsync(ct),
                            StringComparer.OrdinalIgnoreCase);

                        foreach (var c in micComps)
                        {
                            if (!existingCodes.Contains(c.ProductCode))
                            {
                                db.TblInvmas.Add(new TblInvmas
                                {
                                    product_code = c.ProductCode,
                                    groupFld = "MIC"
                                });
                            }
                        }
                        await db.SaveChangesAsync(ct);
                    }

                    // Add the wireless mic package itself; components will be expanded later.
                    AddWanted("QLXD2SK", micPkgQty, null);
                }
            }

            if (wanted.Count == 0) return;

            // ---------- RATES ----------
            var wantedCodeSet = new HashSet<string>(wanted.Keys.Select(k => k.Trim().ToUpperInvariant()),
                                                    StringComparer.OrdinalIgnoreCase);

            var rateRows = await db.TblRatetbls
                .AsNoTracking()
                .Where(r => r.TableNo == (byte)0)
                .Select(r => new { Code = (r.product_code ?? string.Empty).Trim().ToUpper(), Rate = r.rate_1st_day })
                .ToListAsync(ct);

            var rates = rateRows
                .Where(x => wantedCodeSet.Contains(x.Code))
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Rate, StringComparer.OrdinalIgnoreCase);

            double GetUnitRate(string codeNorm) => rates.TryGetValue(codeNorm, out var r1) ? r1 ?? 0d : 0d;

            // ---------- PERSIST (with package / component rules) ----------
            int NextSeqNo()
                => (db.TblItemtrans.Where(t => t.BookingNoV32 == bookingNo)
                                   .Select(t => (int?)t.SeqNo).Max() ?? 0) + 1;

            async Task<List<VwProdsComponents>> GetFixedComponentsAsync(string parentCode)
            {
                return await db.VwProdsComponents.AsNoTracking()
                    .Where(v => v.ParentCode == parentCode && (v.VariablePart == null || v.VariablePart == 0))
                    .OrderBy(v => v.SubSeqNo)
                    .ToListAsync(ct);
            }

            TblItemtran NewBaseLine(string code, int seq, int subSeq, int itemType,
                                    decimal qty, double unitRate, double price, string? cmt)
            {
                return new TblItemtran
                {
                    BookingNoV32 = bookingNo,
                    HeadingNo = 0,
                    SeqNo = seq,
                    SubSeqNo = subSeq,
                    TransTypeV41 = 0,
                    ProductCodeV42 = code,
                    DelTimeHour = booking.del_time_h ?? 0,
                    DelTimeMin = booking.del_time_m ?? 0,
                    ReturnTimeHour = booking.ret_time_h ?? 0,
                    ReturnTimeMin = booking.ret_time_m ?? 0,
                    TransQty = qty,
                    UnitRate = unitRate,
                    Price = price,
                    ItemType = (byte)itemType,   // 0=normal, 1=package, 2=component
                    DaysUsing = 1,
                    SubHireQtyV61 = 0m,
                    CommentDescV42 = cmt,
                    FirstDate = date,
                    RetnDate = date,
                    BookDate = date,
                    PDate = date,
                    AddedAtCheckout = false,
                    UndiscAmt = 0,
                    AssignType = 0,
                    QtyShort = 1,
                    AvailRecFlag = true,
                    SubRentalLinkID = 0,
                    ReturnToLocn = 20,
                    TransToLocn = 20,
                    FromLocn = 20
                };
            }

            foreach (var (codeNorm, val) in wanted)
            {
                var totalQty = Math.Max(val.qty, 1);

                // Is this code a package? If vwProdsComponents has fixed components for it, then yes.
                var components = await GetFixedComponentsAsync(codeNorm);

                if (components.Count == 0)
                {
                    // ----- NOT A PACKAGE: single normal item (itemtype = 0) -----
                    var unitRate = GetUnitRate(codeNorm);
                    var newPrice = Math.Round(unitRate * totalQty, 2);

                    var existing = await db.TblItemtrans.FirstOrDefaultAsync(x =>
                        x.BookingNoV32 == bookingNo &&
                        ((x.ProductCodeV42 ?? string.Empty).Trim().ToUpper()) == codeNorm, ct);

                    if (existing == null)
                    {
                        var seq = NextSeqNo();
                        db.TblItemtrans.Add(NewBaseLine(
                            code: codeNorm,
                            seq: seq,
                            subSeq: 0,
                            itemType: 0,
                            qty: totalQty,
                            unitRate: unitRate,
                            price: newPrice,
                            cmt: val.comment));
                    }
                    else
                    {
                        existing.TransQty = totalQty;
                        if (existing.UnitRate == null || existing.UnitRate == 0) existing.UnitRate = GetUnitRate(codeNorm);
                        var effectiveRate = existing.UnitRate ?? 0;
                        existing.Price = Math.Round(effectiveRate * totalQty, 2);
                        if (!string.IsNullOrWhiteSpace(val.comment)) existing.CommentDescV42 = val.comment;
                    }

                    continue;
                }

                // ----- PACKAGE: parent + components -----
                var seqNo = NextSeqNo();

                // 1) Parent/package line (itemtype = 1) – this carries the price.
                {
                    var pkgUnitRate = GetUnitRate(codeNorm);
                    var pkgPrice = Math.Round(pkgUnitRate * totalQty, 2);

                    var parentLine = NewBaseLine(
                        code: codeNorm,
                        seq: seqNo,
                        subSeq: 0,
                        itemType: 1,
                        qty: totalQty,
                        unitRate: pkgUnitRate,
                        price: pkgPrice,
                        cmt: val.comment);

                    db.TblItemtrans.Add(parentLine);
                }

                // 2) Components (itemtype = 2), sub_seq_no from view, ParentCode set.
                int autoSubSeq = 1;
                foreach (var comp in components)
                {
                    var compCode = comp.ProductCode.Trim().ToUpperInvariant();
                    var subSeq = comp.SubSeqNo.GetValueOrDefault((byte)autoSubSeq++);
                    var perPackageQty = comp.Qty.GetValueOrDefault(1);
                    var totalCompQty = (decimal)(perPackageQty * totalQty);

                    var compLine = NewBaseLine(
                        code: compCode,
                        seq: seqNo,
                        subSeq: subSeq,
                        itemType: 2,
                        qty: totalCompQty,
                        unitRate: 0,        // components unpriced, package line carries total
                        price: 0,
                        cmt: null);

                    compLine.ParentCode = codeNorm;   // link to package
                    db.TblItemtrans.Add(compLine);
                }
            }

            await db.SaveChangesAsync(ct);
        }






        private static bool TryParseTime(string text, out TimeSpan time)
        {
            // Accepts "10:00 AM", "7:30PM", "9 AM", etc.
            time = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var m = Regex.Match(text, @"\b(\d{1,2})(?::(\d{2}))?\s*(AM|PM)?\b", RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            int h = int.Parse(m.Groups[1].Value);
            int mins = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            string ampm = m.Groups[3].Value;
            if (!string.IsNullOrEmpty(ampm))
            {
                bool pm = ampm.Equals("PM", StringComparison.OrdinalIgnoreCase);
                if (h == 12) h = pm ? 12 : 0;
                else if (pm) h += 12;
            }
            time = new TimeSpan(h, mins, 0);
            return true;
        }

        private static double RoundHours(TimeSpan span)
        {
            // convert to hours with 2 decimals
            return Math.Round(span.TotalHours, 2);
        }

        public async Task InsertCrewRowsAsync(
      BookingDbContext db,
      string bookingNo,
      IDictionary<string, string> facts,
      CancellationToken ct)
        {
            // Read summary facts
            facts.TryGetValue("Start", out var startText);
            facts.TryGetValue("End", out var endText);
            facts.TryGetValue("Setup", out var setupText);
            facts.TryGetValue("Pack Up", out var packText);
            facts.TryGetValue("Rehearsal", out var rehearsalText);
            facts.TryGetValue("Technical Support", out var techSupportText);

            var booking = await db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
            if (booking == null) return;

            // --- Parse times from text like "10:00 AM"
            bool TryParseTime(string s, out TimeSpan ts)
            {
                ts = default;
                if (string.IsNullOrWhiteSpace(s)) return false;
                s = s.Trim();
                if (DateTime.TryParse(s, out var dt))
                {
                    ts = dt.TimeOfDay;
                    return true;
                }
                return false;
            }

            // --- Round a TimeSpan to hours as double (e.g., 3.5h)
            double RoundHours(TimeSpan span)
            {
                // round to nearest 15 minutes
                var minutes = Math.Round(span.TotalMinutes / 15.0) * 15.0;
                return Math.Max(0.25, minutes / 60.0);
            }

            // Times & durations
            var startOk = TryParseTime(startText ?? "", out var startTS);
            var endOk = TryParseTime(endText ?? "", out var endTS);
            var day = booking.SDate?.Date ?? DateTime.Today;

            var eventHours = (startOk && endOk && endTS > startTS) ? RoundHours(endTS - startTS) : 1.0; // default 1h
            var setupHours = 1.0;
            var packHours = 1.0;
            var rehHours = 1.0;

            // Presence flags
            bool hasRehearsal = !string.IsNullOrWhiteSpace(rehearsalText);
            bool wantTech = !(techSupportText ?? "").Contains("Not required", StringComparison.OrdinalIgnoreCase);

            // People (from your sheet)
            int personsSetup = 2;
            int personsPack = 4;
            int personsReh = 7;
            int personsTech = 3;

            // Crew rate (per person per hour)
            const double unitRate = 110.0;

            // Convert a double hours value into Hours/Minutes tinyints
            static (byte Hours, byte Minutes) Hm(double hours)
            {
                if (hours < 0) hours = 0;
                var h = (int)Math.Floor(hours);
                var m = (int)Math.Round((hours - h) * 60.0);
                if (m == 60) { h += 1; m = 0; }
                return ((byte)Math.Clamp(h, 0, 255), (byte)Math.Clamp(m, 0, 59));
            }

            int NextCrewSeqNo() =>
                (db.TblCrews.Where(t => t.BookingNoV32 == bookingNo)
                            .Select(t => (int?)t.SeqNo).Max() ?? 0) + 1;

            // Add one crew line using proper data types for tblCrew
            // PER GUIDE:
            // - techrateisHourorDay = "H" not "D"
            // - Person field should be EMPTY (not the number of persons)
            // - TransQty = number of people
            // - Start time from del_time_hour/min (based on collected time)
            // - End time = start + 1 hour for setup/packdown/rehearsal; event end for operate/tech
            // - StraightTime = hours + mins
            void AddCrew(string productCode, int task, int persons, double hours, double price, 
                byte startHour, byte startMin, byte endHour, byte endMin)
            {
                var (h, m) = Hm(hours);
                // Calculate S.T. (StraightTime) = hours + mins as decimal
                var straightTime = h + (m / 60.0);

                db.TblCrews.Add(new TblCrew
                {
                    // Keys/identity
                    BookingNoV32 = bookingNo,
                    HeadingNo = 0,
                    SeqNo = NextCrewSeqNo(),
                    SubSeqNo = 0,

                    // PER GUIDE: Use "AVTECH" product code and get rate from tblInvmas_Labour_Rates
                    ProductCodeV42 = "AVTECH",
                    
                    // PER GUIDE: Start time (del_time) based on collected time
                    DelTimeHour = startHour,
                    DelTimeMin = startMin,
                    // PER GUIDE: End time based on task type
                    ReturnTimeHour = endHour,
                    ReturnTimeMin = endMin,

                    // PER GUIDE: TransQty = number of people
                    TransQty = persons,
                    UnitRate = unitRate,
                    Price = price,

                    // Duration (tinyint)
                    Hours = h,
                    Minutes = m,

                    // PER GUIDE: Person field should be EMPTY, not the number of people
                    Person = null,
                    Task = (byte?)task,

                    // PER GUIDE: techrateIsHourorDay = "H" not "D"
                    TechrateIsHourOrDay = "H",

                    // First/return dates
                    FirstDate = day,
                    RetnDate = day,

                    // PER GUIDE: StraightTime = hours + mins worked
                    GroupSeqNo = 0,
                    StraightTime = straightTime,

                    // Common NOT NULL defaults
                    MeetTechOnSite = false,
                    TechIsConfirmed = false,

                    // NOT NULL: HourlyRateID (decimal(10,0)) – safe default
                    HourlyRateID = 0M,
                    UnpaidHours = 0,
                    UnpaidMins = 0
                });
            }
            try
            {
                // PER GUIDE: Calculate start/end times for each task type
                // Setup time from booking, or default to 7:00
                var setupStartH = booking.del_time_h ?? 7;
                var setupStartM = booking.del_time_m ?? 0;
                // PER GUIDE: End time = start + 1 hour for setup/packdown/rehearsal
                var setupEndH = (byte)Math.Min(setupStartH + 1, 23);
                var setupEndM = setupStartM;
                
                // Pack time from booking ret_time (strike time), or default based on event end
                var packStartH = booking.ret_time_h ?? 18;
                var packStartM = booking.ret_time_m ?? 0;
                var packEndH = (byte)Math.Min(packStartH + 1, 23);
                var packEndM = packStartM;
                
                // Event start/end times for operate/technical support
                // Parse showStartTime and ShowEndTime (stored as "0930" format)
                byte eventStartH = 10, eventStartM = 0, eventEndH = 17, eventEndM = 0;
                if (!string.IsNullOrEmpty(booking.showStartTime) && booking.showStartTime.Length >= 4)
                {
                    byte.TryParse(booking.showStartTime[..2], out eventStartH);
                    byte.TryParse(booking.showStartTime.Substring(2, 2), out eventStartM);
                }
                if (!string.IsNullOrEmpty(booking.ShowEndTime) && booking.ShowEndTime.Length >= 4)
                {
                    byte.TryParse(booking.ShowEndTime[..2], out eventEndH);
                    byte.TryParse(booking.ShowEndTime.Substring(2, 2), out eventEndM);
                }
                
                // Rehearsal time from booking
                byte rehStartH = 9, rehStartM = 30;
                if (!string.IsNullOrEmpty(booking.RehearsalTime) && booking.RehearsalTime.Length >= 4)
                {
                    byte.TryParse(booking.RehearsalTime[..2], out rehStartH);
                    byte.TryParse(booking.RehearsalTime.Substring(2, 2), out rehStartM);
                }
                var rehEndH = (byte)Math.Min(rehStartH + 1, 23);
                var rehEndM = rehStartM;

                // Always add SETUP and PACKDOWN
                AddCrew("SETUP", 2, personsSetup, setupHours, 110, setupStartH, setupStartM, setupEndH, setupEndM);
                AddCrew("PACKDOWN", 4, personsPack, packHours, 110, packStartH, packStartM, packEndH, packEndM);

                // Optional rows
                if (hasRehearsal)
                    AddCrew("REHEARSAL", 7, personsReh, rehHours, 110, rehStartH, rehStartM, rehEndH, rehEndM);

                // PER GUIDE: Hours for operate/technician = event duration (not fixed 1 hour)
                // End time = event end time (not start + 1 hour)
                if (wantTech)
                    AddCrew("TECH", 3, personsTech, eventHours, 110, eventStartH, eventStartM, eventEndH, eventEndM);


                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Surface the real SQL message so you can see any remaining mismatch
                Exception root = ex;
                while (root.InnerException != null) root = root.InnerException;
                throw new InvalidOperationException($"tblCrew insert failed: {root.Message}", ex);
            }
        }


        // ---------- MAIN: Excel-less, JSON-driven item upsert ----------
        // Drop this method into your AzureAgentChatService (or wherever you call it from).

        //    public async Task UpsertItemsFromSummaryAsync(
        //BookingDbContext db, ISession session,
        //IEnumerable<DisplayMessage> messages, CancellationToken ct)
        //    {
        //        var lastAssistant = messages.LastOrDefault(m =>
        //            string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        //        if (lastAssistant == null) return;

        //        var raw = string.Join("\n", lastAssistant.Parts ?? Enumerable.Empty<string>());
        //        var facts = ParseSummaryFacts(raw);
        //        if (facts.Count == 0) return;

        //        bool wantsPresentation =
        //            (facts.TryGetValue("Equipment", out var eq) &&
        //                (eq.IndexOf("projector", StringComparison.OrdinalIgnoreCase) >= 0 ||
        //                 eq.IndexOf("laptop", StringComparison.OrdinalIgnoreCase) >= 0)) ||
        //            (facts.TryGetValue("Presenters", out var pr) &&
        //                pr.IndexOf("laptop", StringComparison.OrdinalIgnoreCase) >= 0) ||
        //            (facts.TryGetValue("Purpose", out var pu) &&
        //                pu.IndexOf("presentation", StringComparison.OrdinalIgnoreCase) >= 0);

        //        var roomText = facts.TryGetValue("Room", out var rtxt) ? rtxt.ToLowerInvariant() : string.Empty;

        //        string rulesPath = (_env != null)
        //            ? Path.Combine(_env.WebRootPath, "data", "item-rules.json")
        //            : Path.Combine(AppContext.BaseDirectory, "wwwroot", "data", "item-rules.json");
        //        if (!System.IO.File.Exists(rulesPath)) return;

        //        var rules = JsonSerializer.Deserialize<List<ItemRule>>(
        //                        await System.IO.File.ReadAllTextAsync(rulesPath, ct))
        //                    ?? new List<ItemRule>();
        //        if (rules.Count == 0) return;

        //        var bookingNo = session.GetString("Draft:BookingNo");
        //        if (string.IsNullOrWhiteSpace(bookingNo)) return;

        //        var booking = await db.TblBookings.FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);
        //        if (booking == null) return;

        //        var date = booking.SDate ?? DateTime.Today;

        //        // 1) drivers (sum across duplicate sourceLabels)
        //        var drivers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        //        foreach (var r in rules.Where(x => string.Equals(x.type, "driver", StringComparison.OrdinalIgnoreCase)))
        //        {
        //            if (string.IsNullOrWhiteSpace(r.key) || string.IsNullOrWhiteSpace(r.sourceLabel)) continue;
        //            if (!facts.TryGetValue(r.sourceLabel!, out var val)) continue;

        //            int qty = 0;
        //            if (string.Equals(r.qtyFrom, "regex", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(r.regex))
        //            {
        //                var m = Regex.Match(val, r.regex!, RegexOptions.IgnoreCase);
        //                if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) qty = n;
        //            }
        //            else if (string.Equals(r.qtyFrom, "labelNumber", StringComparison.OrdinalIgnoreCase))
        //            {
        //                var m = Regex.Match(val, "(\\d+)");
        //                if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) qty = n;
        //            }

        //            if (r.multiplier is int mul && mul > 1) qty *= mul;
        //            drivers[r.key!] = (drivers.TryGetValue(r.key!, out var cur) ? cur : 0) + Math.Max(0, qty);
        //        }

        //        // 2) bundles -> wanted
        //        var wanted = new Dictionary<string, (int qty, string? comment)>(StringComparer.OrdinalIgnoreCase);
        //        void AddWanted(string code, int qty, string? cmt)
        //        {
        //            if (qty <= 0 || string.IsNullOrWhiteSpace(code)) return;
        //            var norm = code.Trim().ToUpperInvariant();
        //            if (wanted.TryGetValue(norm, out var cur))
        //                wanted[norm] = (cur.qty + qty, string.IsNullOrWhiteSpace(cmt) ? cur.comment : cmt);
        //            else
        //                wanted[norm] = (qty, cmt);
        //        }

        //        foreach (var r in rules.Where(x => string.Equals(x.type, "bundle", StringComparison.OrdinalIgnoreCase)))
        //        {
        //            if (r.roomContains is { Length: > 0 })
        //            {
        //                bool all = r.roomContains.All(tok => roomText.Contains(tok, StringComparison.OrdinalIgnoreCase));
        //                if (!all) continue;
        //            }
        //            if (r.requirePresentation == true && !wantsPresentation) continue;
        //            if (r.bundleItems == null) continue;

        //            foreach (var bi in r.bundleItems)
        //            {
        //                int qty = 0;
        //                switch ((bi.qtyFrom ?? "literal").ToLowerInvariant())
        //                {
        //                    case "literal":
        //                        qty = bi.literalQty ?? 1;
        //                        break;
        //                    case "optionalfixed":
        //                        qty = bi.literalQty ?? 1; // 0 => omit
        //                        break;
        //                    case "usedriverqty":
        //                        if (!string.IsNullOrWhiteSpace(bi.driverKey) && drivers.TryGetValue(bi.driverKey!, out var dq))
        //                            qty = dq;
        //                        break;
        //                    case "maxof":
        //                        var list = (bi.driverKeys ?? Array.Empty<string>())
        //                                   .Select(k => drivers.TryGetValue(k, out var v) ? v : 0);
        //                        qty = Math.Max(bi.defaultQty ?? 0, list.DefaultIfEmpty(0).Max());
        //                        break;
        //                }
        //                AddWanted(bi.product_code, qty, bi.comment);
        //            }
        //        }
        //        if (wanted.Count == 0) return;

        //        // 3) preload unit rates (TableNo=0)
        //        var wantedCodeSet = new HashSet<string>(wanted.Keys, StringComparer.OrdinalIgnoreCase);
        //        var rateRows = await db.TblRatetbls
        //            .AsNoTracking()
        //            .Where(r => r.TableNo == (byte)0)
        //            .Select(r => new { Code = (r.product_code ?? string.Empty).Trim().ToUpper(), Rate = r.rate_1st_day })
        //            .ToListAsync(ct);

        //        var rates = rateRows
        //            .Where(x => wantedCodeSet.Contains(x.Code))
        //            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
        //            .ToDictionary(g => g.Key, g => g.First().Rate, StringComparer.OrdinalIgnoreCase);

        //        decimal GetUnitRate(string codeNorm)
        //            => rates.TryGetValue(codeNorm, out var r1) ? Convert.ToDecimal(r1) : 0m;

        //        // 4) persist
        //        int NextSeqNo()
        //            => (db.TblItemtrans.Where(t => t.BookingNoV32 == bookingNo)
        //                               .Select(t => (int?)t.SeqNo).Max() ?? 0) + 1;

        //        foreach (var (codeNorm, val) in wanted)
        //        {
        //            var qty = val.qty;
        //            var cmt = val.comment;

        //            var unitRate = GetUnitRate(codeNorm);
        //            var newPrice = Math.Round(unitRate * qty, 2);

        //            var existing = await db.TblItemtrans
        //                .FirstOrDefaultAsync(x =>
        //                    x.BookingNoV32 == bookingNo &&
        //                    (x.ProductCodeV42 ?? "").Trim().ToUpper() == codeNorm, ct);

        //            if (existing == null)
        //            {
        //                db.TblItemtrans.Add(new TblItemtran
        //                {
        //                    BookingNoV32 = bookingNo,
        //                    HeadingNo = 0,
        //                    SeqNo = NextSeqNo(),
        //                    SubSeqNo = 0,

        //                    TransTypeV41 = 0,
        //                    ProductCodeV42 = codeNorm,

        //                    DelTimeHour = booking.del_time_h ?? 0,
        //                    DelTimeMin = booking.del_time_m ?? 0,
        //                    ReturnTimeHour = booking.ret_time_h ?? 0,
        //                    ReturnTimeMin = booking.ret_time_m ?? 0,

        //                    TransQty = qty,
        //                    UnitRate = (double?)unitRate,
        //                    Price = (double?)newPrice,

        //                    ItemType = 0,
        //                    DaysUsing = 1,
        //                    SubHireQtyV61 = 0m,

        //                    CommentDescV42 = cmt,
        //                    FirstDate = date,
        //                    RetnDate = date,
        //                    BookDate = date,
        //                    PDate = date,

        //                    AddedAtCheckout = false,
        //                    UndiscAmt = 0,

        //                    AssignType = 0,
        //                    QtyShort = 1,
        //                    AvailRecFlag = true,
        //                    SubRentalLinkID = 0,
        //                    ReturnToLocn = (short)20,
        //                    TransToLocn = (short)20,
        //                    FromLocn = (short)20
        //                });
        //            }
        //            else
        //            {
        //                var finalQty = Math.Max(qty, 1);
        //                existing.TransQty = finalQty;
        //                if (existing.UnitRate == null || existing.UnitRate == 0)
        //                    existing.UnitRate = (double?)unitRate;
        //                var effectiveRate = (double?)existing.UnitRate ?? 0;
        //                existing.Price = (double?)Math.Round(effectiveRate * finalQty, 2);
        //                if (!string.IsNullOrWhiteSpace(cmt)) existing.CommentDescV42 = cmt;
        //            }
        //        }

        //        try
        //        {
        //            await db.SaveChangesAsync(ct);
        //        }
        //        catch (DbUpdateException ex)
        //        {
        //            Exception root = ex;
        //            while (root.InnerException != null) root = root.InnerException;
        //            throw new InvalidOperationException($"tblItemtrans insert/update failed: {root.Message}", ex);
        //        }
        //    }

        public async Task<string?> TrySaveBookingAsync(
            BookingDbContext db,
            ISession session,
            IEnumerable<DisplayMessage> messages,
            decimal? contactId,
            CancellationToken ct)
        {
            try
            {
                static int? ToHHmm(TimeSpan? t) => t == null ? (int?)null : (t.Value.Hours * 100 + t.Value.Minutes);
                static TimeSpan? ParseTime(string? s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    return TimeSpan.TryParse(s, out var ts) ? ts : null;
                }
                static string? Trunc(string? s, int len)
                    => string.IsNullOrEmpty(s) ? s : (s.Length <= len ? s : s.Substring(0, len));

                DateTime NowAest()
                {
#if WINDOWS
            var tz = TimeZoneInfo.FindSystemTimeZoneById("E. Australia Standard Time");
#else
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
#endif
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                }

                // ---- local helper: read "Room: Westin Ballroom" from last assistant summary ----
                static string? ExtractRoomFromSummary(IEnumerable<DisplayMessage> msgs)
                {
                    var lastAssistant = msgs.LastOrDefault(m =>
                        string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
                    if (lastAssistant == null) return null;

                    var text = string.Join("\n", lastAssistant.Parts ?? Enumerable.Empty<string>());
                    if (string.IsNullOrWhiteSpace(text)) return null;

                    foreach (var rawLine in text.Split('\n'))
                    {
                        var line = rawLine.Trim();
                        // e.g. "• Room: Westin Ballroom" or "Room: Westin Ballroom"
                        var match = Regex.Match(line, @"^(?:•\s*)?room\s*:\s*(.+)$",
                            RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var value = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(value))
                                return value;
                        }
                    }

                    return null;
                }

                var (dateDto, _) = ExtractEventDate(messages);
                var date = dateDto?.DateTime.Date;

                var startTs = ParseTime(session.GetString("Draft:StartTime"));
                var endTs = ParseTime(session.GetString("Draft:EndTime"));
                var setupTs = ParseTime(session.GetString("Draft:SetupTime"));
                var rehearsalTs = ParseTime(session.GetString("Draft:RehearsalTime"));
                var packTs = ParseTime(session.GetString("Draft:PackUpTime"));

                // Fallback: extract times from conversation messages if session doesn't have them
                if (startTs == null || endTs == null)
                {
                    var (extractedStart, extractedEnd, _) = ExtractEventTime(messages);
                    if (startTs == null) startTs = extractedStart;
                    if (endTs == null) endTs = extractedEnd;
                }

                // old logic: button-driven choice (often null for you)
                var roomFromChoice = ExtractLastChoice(messages, "Choose room:");
                // new logic: pull from summary bullet
                var roomFromSummary = ExtractRoomFromSummary(messages);

                var room = roomFromChoice ?? roomFromSummary;
                var layout = ExtractLastChoice(messages, "Choose layout:");
                var attendees = ExtractAttendees(messages);
                var evtType = ExtractEventType(messages) ?? "Meeting";

                if (date == null || startTs == null || endTs == null)
                    return null;

                var bookingNo = session.GetString("Draft:BookingNo");

                TblBooking row;
                if (!string.IsNullOrWhiteSpace(bookingNo))
                {
                    row = await GetOrCreateBookingAsync(db, bookingNo, ct);
                }
                else
                {
                    bookingNo = await GenerateNextBookingNoAsync(db, ct);
                    session.SetString("Draft:BookingNo", bookingNo);
                    row = await GetOrCreateBookingAsync(db, bookingNo, ct);
                }

                // ---- map fields ----
                if (!string.IsNullOrWhiteSpace(room))
                    row.VenueRoom = Trunc(room, 100);

                if (!string.IsNullOrWhiteSpace(evtType))
                    row.EventType = Trunc(evtType, 50);

                // if (attendees.HasValue) row.ExpAttendees = attendees;

                if (contactId.HasValue)
                    row.ContactID = (int?)contactId.Value;

                // single-day
                row.SetDate = date;
                row.ShowSDate = date;
                row.ShowEdate = date;

                // HHmm integer columns
                var hhmmStart = ToHHmm(startTs);
                var hhmmEnd = ToHHmm(endTs);
                var hhmmSetup = ToHHmm(setupTs);
                var hhmmReh = ToHHmm(rehearsalTs);
                var hhmmPack = ToHHmm(packTs);

                if (hhmmStart.HasValue) row.showStartTime = hhmmStart.Value.ToString();
                if (hhmmEnd.HasValue) row.ShowEndTime = hhmmEnd.Value.ToString();
                if (hhmmSetup.HasValue) row.setupTimeV61 = hhmmSetup.Value.ToString();
                if (hhmmReh.HasValue) row.RehearsalTime = hhmmReh.Value.ToString();
                if (hhmmPack.HasValue) row.StrikeTime = hhmmPack.Value.ToString();

                row.booking_type_v32 = 2;
                row.CustCode = "C04518";
                row.CustID = 13740;
                row.VenueID = 16;
                row.contact_nameV6 = "Megan Suurenbroek";
                row.BookingProgressStatus = 1;
                row.EntryDate = DateTime.Now;
                row.From_locn = 20;
                row.return_to_locn = 20;
                row.Trans_to_locn = 20;

                await db.SaveChangesAsync(ct);
                return bookingNo;
            }
            catch (DbUpdateException ex)
            {
                var root = ex;
                while (root.InnerException != null) root = (DbUpdateException)root.InnerException;
                throw new InvalidOperationException($"Booking upsert failed: {root.Message}", ex);
            }
        }

    }
}
