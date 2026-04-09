// Controllers/ChatController.cs
using Azure.Identity;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Models; // for DisplayMessage if needed
using MicrohireAgentChat.Services;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace MicrohireAgentChat.Controllers;

public sealed class ChatController : Controller
{
    private readonly AzureAgentChatService _chat;
    private readonly AppDbContext _appDb;
    private readonly BookingDbContext _bookingDb;
    private readonly BookingOrchestrationService _orchestration;
    private readonly HtmlQuoteGenerationService _htmlQuoteGen;
    private readonly ItemPersistenceService _itemPersistence;
    private readonly ContactResolutionService _contactResolution;
    private readonly BookingPersistenceService _bookingPersistence;
    private readonly ConversationReplayService _replayService;
    private readonly AutoTestCustomerService _autoTestService;
    private readonly BookingQueryService _bookingQuery;
    private readonly DevModeOptions _devOptions;
    private readonly AutoTestOptions _autoTestOptions;
    private readonly IWebHostEnvironment _env;
    private readonly IRazorViewEngine _razorViewEngine;
    private readonly ILogger<ChatController> _logger;
    private readonly AgentToolHandlerService _toolHandler;
    private readonly ChatSessionPersistenceService _sessionPersistence;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWestinRoomCatalog _westinRoomCatalog;
    private readonly IHostApplicationLifetime _hostLifetime;

    // Detect: "Choose time: 09:00–10:30" (supports hyphen or en dash)
    private static readonly Regex ChooseTimeRe =
        new(@"^Choose\s*time:\s*(\d{1,2}:\d{2})\s*[–-]\s*(\d{1,2}:\d{2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Detect: "Choose schedule: key=HH:mm; key=HH:mm; ..."
    private static readonly Regex ChooseScheduleRe =
        new(@"^\s*Choose\s+schedule\s*:\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ContactFormRe =
        new(@"^\s*Contact\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EventFormRe =
        new(@"^\s*Event\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AvExtrasFormRe =
        new(@"^\s*AV\s+Extras\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EmailFormRe =
        new(@"^\s*Email\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VenueConfirmFormRe =
        new(@"^\s*VenueConfirm\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EventDetailsFormRe =
        new(@"^\s*EventDetails\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BaseAvFormRe =
        new(@"^\s*BaseAv\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FollowUpAvFormRe =
        new(@"^\s*FollowUpAv\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>True if the name is the assistant's (Isla, Microhire, or both). Used to avoid saving assistant name as contact.</summary>
    private static bool LooksLikeAssistantName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().ToLowerInvariant();
        return t == "isla" || t == "microhire" || (t.Contains("isla") && t.Contains("microhire"));
    }

    // Opening line for Azure thread (intro only — email gate copy lives on the structured email form message)
    private const string GreetingText =
        "Hello, my name is Isla from Microhire. I'm here to help you plan AV equipment hire and quotes for your event.";
    private const string AwaitingQuoteReviewPromptKey = "Draft:AwaitingQuoteReviewPrompt";
    private const string QuoteReviewPromptShownKey = "Draft:QuoteReviewPromptShown";
    private const string ProjectorPromptShownKey = "Draft:ProjectorPromptShown";
    private const string ProjectorPromptThreadIdKey = "Draft:ProjectorPromptThreadId";
    private const string ProjectorAreaCapturedKey = "Draft:ProjectorAreaCaptured";
    private const string ProjectorAreaThreadIdKey = "Draft:ProjectorAreaThreadId";

    public ChatController(
        AzureAgentChatService chat,
        AppDbContext appDb,
        BookingDbContext bookingDb,
        BookingOrchestrationService orchestration,
        HtmlQuoteGenerationService htmlQuoteGen,
        ItemPersistenceService itemPersistence,
        ContactResolutionService contactResolution,
        BookingPersistenceService bookingPersistence,
        ConversationReplayService replayService,
        AutoTestCustomerService autoTestService,
        BookingQueryService bookingQuery,
        IOptions<DevModeOptions> devOptions,
        IOptions<AutoTestOptions> autoTestOptions,
        IWebHostEnvironment env,
        IRazorViewEngine razorViewEngine,
        ILogger<ChatController> logger,
        AgentToolHandlerService toolHandler,
        ChatSessionPersistenceService sessionPersistence,
        IServiceScopeFactory scopeFactory,
        IWestinRoomCatalog westinRoomCatalog,
        IHostApplicationLifetime hostLifetime)
    {
        _chat = chat;
        _appDb = appDb;
        _bookingDb = bookingDb;
        _orchestration = orchestration;
        _htmlQuoteGen = htmlQuoteGen;
        _itemPersistence = itemPersistence;
        _contactResolution = contactResolution;
        _bookingPersistence = bookingPersistence;
        _replayService = replayService;
        _autoTestService = autoTestService;
        _bookingQuery = bookingQuery;
        _devOptions = devOptions.Value;
        _autoTestOptions = autoTestOptions.Value;
        _env = env;
        _razorViewEngine = razorViewEngine;
        _logger = logger;
        _toolHandler = toolHandler;
        _sessionPersistence = sessionPersistence;
        _scopeFactory = scopeFactory;
        _westinRoomCatalog = westinRoomCatalog;
        _hostLifetime = hostLifetime;
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

    /// <summary>
    /// Persists draft state after the HTTP request completes using a new DI scope (request-scoped DbContext must not be used from Task.Run).
    /// </summary>
    private void QueuePersistDraftStateBackground(string email, string userKey, Dictionary<string, string> snapshot)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var persistence = scope.ServiceProvider.GetRequiredService<ChatSessionPersistenceService>();
                await persistence.SaveAsync(userKey, email, snapshot, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist draft state for {Email}", email);
            }
        });
    }

    #region agent log
    private const string Debug105ef6LogPath = "/Users/nitwit-watson/INTENT/repos/Microhire-sales-portal/.cursor/debug-105ef6.log";

    private void Debug105ef6(string hypothesisId, string location, string message, Dictionary<string, object?> data)
    {
        try
        {
            var line = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["sessionId"] = "105ef6",
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            try
            {
                System.IO.File.AppendAllText(Debug105ef6LogPath, line + "\n");
            }
            catch
            {
                /* Azure App Service: path not on server */
            }

            _logger.LogWarning("AGENT_DEBUG_105ef6 {Payload}", line);
        }
        catch
        {
            /* never throw from debug log */
        }
    }
    #endregion

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
        HttpContext.Session.Remove("Draft:EntrySource");
        HttpContext.Session.Remove("Draft:EmailGateCompleted");
        HttpContext.Session.Remove("Draft:LeadVerifyEmail");
        HttpContext.Session.Remove("Draft:NeedManualContact");
        HttpContext.Session.Remove("Draft:BookingLookupApplied");
        HttpContext.Session.Remove("Draft:VenueConfirmSubmitted");
        HttpContext.Session.Remove("Draft:BaseAvSubmitted");
        HttpContext.Session.Remove("Draft:BaseAvSubmittedForThread");
        HttpContext.Session.Remove("Draft:FollowUpAvSubmitted");
        HttpContext.Session.Remove("Draft:EventEndDate");
        HttpContext.Session.Remove("Draft:WantsOperator");
        HttpContext.Session.Remove("Draft:WantsRehearsalOperator");
        HttpContext.Session.Remove("Draft:RehearsalOperator");
        HttpContext.Session.Remove("Draft:BuiltInProjector");
        HttpContext.Session.Remove("Draft:BuiltInScreen");
        HttpContext.Session.Remove("Draft:BuiltInSpeakers");
        HttpContext.Session.Remove("Draft:CombinedProjectorScreen");
        HttpContext.Session.Remove("Draft:ProjectorPlacementChoice");
        HttpContext.Session.Remove("Draft:Flipchart");
        HttpContext.Session.Remove("Draft:LaptopMode");
        HttpContext.Session.Remove("Draft:LaptopQty");
        HttpContext.Session.Remove("Draft:AdapterOwnLaptops");
        HttpContext.Session.Remove("Draft:MicType");
        HttpContext.Session.Remove("Draft:MicQty");
        HttpContext.Session.Remove("Draft:Lectern");
        HttpContext.Session.Remove("Draft:FoldbackMonitor");
        HttpContext.Session.Remove("Draft:WirelessPresenter");
        HttpContext.Session.Remove("Draft:LaptopSwitcher");
        HttpContext.Session.Remove("Draft:StageLaptop");
        HttpContext.Session.Remove("Draft:VideoConference");
        HttpContext.Session.Remove("Draft:LeadSeededExpectedAttendees");
    }

    /// <summary>
    /// Session keys to keep when the DB has no persisted draft for this email (e.g. row deleted) but the
    /// browser still holds an old Azure thread id and wizard flags. Matches lead prefill + identity.
    /// </summary>
    private static readonly string[] SessionKeysPreservedWhenResettingChatProgress =
    {
        "Draft:EntrySource",
        "Draft:EmailGateCompleted",
        "Draft:ContactEmail",
        "Draft:LeadVerifyEmail",
        "Draft:ContactName",
        "Draft:ContactFirstName",
        "Draft:ContactLastName",
        "Draft:ContactPhone",
        "Draft:Organisation",
        "Draft:OrganisationAddress",
        "Draft:Position",
        "Draft:VenueName",
        "Draft:RoomName",
        "Draft:EventDate",
        "Draft:EventEndDate",
        "Draft:ExpectedAttendees",
        "Draft:LeadSeededExpectedAttendees",
        // Wizard progress + submissions (survive thread reset when AgentThreads draft row is missing)
        "Draft:VenueConfirmSubmitted",
        "Draft:EventFormSubmitted",
        "Draft:EventType",
        "Draft:SetupStyle",
        "Draft:WantsOperator",
        "Draft:WantsRehearsalOperator",
        "Draft:RehearsalOperator",
        "Draft:SetupTime",
        "Draft:RehearsalTime",
        "Draft:StartTime",
        "Draft:EndTime",
        "Draft:PackupTime",
        "Draft:PresenterCount",
        "Draft:BuiltInProjector",
        "Draft:BuiltInScreen",
        "Draft:BuiltInSpeakers",
        "Draft:CombinedProjectorScreen",
        "Draft:ProjectorPlacementChoice",
        "Draft:Flipchart",
        "Draft:LaptopMode",
        "Draft:LaptopQty",
        "Draft:AdapterOwnLaptops",
        "Draft:MicType",
        "Draft:MicQty",
        "Draft:Lectern",
        "Draft:FoldbackMonitor",
        "Draft:WirelessPresenter",
        "Draft:LaptopSwitcher",
        "Draft:StageLaptop",
        "Draft:VideoConference",
        "Draft:BookingNo",
        "Draft:ActiveLeadToken",
    };

    private Dictionary<string, string> SnapshotPreservedChatIdentityKeys()
    {
        var snap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in SessionKeysPreservedWhenResettingChatProgress)
        {
            var v = HttpContext.Session.GetString(key);
            if (!string.IsNullOrEmpty(v))
                snap[key] = v;
        }

        return snap;
    }

    private static void RestorePreservedChatIdentityKeys(ISession session, Dictionary<string, string> snap)
    {
        foreach (var kvp in snap)
            session.SetString(kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Clears quote, wizard, and Azure thread binding while restoring identity/lead prefill. Then creates
    /// a new Azure thread and persists the mapping for <paramref name="userKey"/>.
    /// </summary>
    private async Task ResetChatProgressForFreshPersistenceAsync(string userKey, CancellationToken ct)
    {
        var snap = SnapshotPreservedChatIdentityKeys();
        ClearBookingAndQuoteDraftState();
        RestorePreservedChatIdentityKeys(HttpContext.Session, snap);

        var session = HttpContext.Session;
        session.Remove("AgentThreadId");
        session.Remove("IslaGreeted");
        session.Remove("Draft:ContactFormSubmitted");
        session.Remove("Draft:ShowContactSummary");
        session.Remove("Draft:SpeakerCount");
        session.Remove("Draft:NeedsClicker");
        session.Remove("Draft:NeedsRecording");
        session.Remove("Draft:TechStartTime");
        session.Remove("Draft:TechEndTime");
        session.Remove("Draft:TechWholeEvent");
        session.Remove("Draft:AvExtrasSubmitted");
        session.Remove("Draft:TechnicianCoverage");
        session.Remove("Draft:SummaryEquipmentRequests");
        session.Remove("Draft:QuoteTimestamp");
        session.Remove("Draft:IsContentHeavy");
        session.Remove("Draft:IsContentLight");
        session.Remove("Draft:NeedsSdiCross");
        session.Remove(GenerateQuoteFlag);
        session.Remove("Draft:ContactId");
        session.Remove("Draft:CustomerCode");
        session.Remove("Draft:OrgId");
        session.Remove("Ack:Budget");
        session.Remove("Ack:Attendees");
        session.Remove("Ack:SetupStyle");
        session.Remove("Ack:Venue");
        session.Remove("Ack:SpecialRequests");
        session.Remove("Ack:Dates");
        session.Remove("ContactSavePending");
        session.Remove("ContactSaveCompleted");

        var newThreadId = _chat.EnsureThreadId(session);
        await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, ct);
        await _chat.EnsureGreetingAsync(session, GreetingText, ct);
    }

    /// <summary>
    /// After a DB wipe, the browser session can still hold wizard flags and an Azure thread id.
    /// If there is no <see cref="AgentThreads"/> row for the verified email, reset when progress looks non-trivial.
    /// </summary>
    private async Task MaybeResetOrphanSessionAfterDbWipeAsync(string userKey, CancellationToken ct)
    {
        if (!string.Equals(HttpContext.Session.GetString("Draft:EmailGateCompleted"), "1", StringComparison.Ordinal))
            return;

        var email = HttpContext.Session.GetString("Draft:ContactEmail");
        if (string.IsNullOrWhiteSpace(email))
            return;

        var saved = await _sessionPersistence.FindByEmailAsync(email.Trim(), ct);
        if (saved != null)
            return;

        var s = HttpContext.Session;
        var stale =
            string.Equals(s.GetString("Draft:QuoteComplete"), "1", StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(s.GetString("Draft:BookingNo"))
            || string.Equals(s.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal)
            || string.Equals(s.GetString("Draft:FollowUpAvSubmitted"), "1", StringComparison.Ordinal)
            || string.Equals(s.GetString("Draft:VenueConfirmSubmitted"), "1", StringComparison.Ordinal)
            || string.Equals(s.GetString("Draft:EventFormSubmitted"), "1", StringComparison.Ordinal);

        if (!stale)
            return;

        _logger.LogInformation(
            "Reconciling stale session: no AgentThreads row for verified email {Email}; resetting chat progress.",
            email);
        await ResetChatProgressForFreshPersistenceAsync(userKey, ct);
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

    /// <summary>Fresh antiforgery token for JS (partial reloads may omit the form token in DOM).</summary>
    [HttpGet]
    [IgnoreAntiforgeryToken]
    public IActionResult AntiforgeryToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var token = tokens.RequestToken;
        if (string.IsNullOrEmpty(token))
            return StatusCode(500, new { error = "Antiforgery token unavailable" });
        return Json(new { token });
    }

    // Full page
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                "Chat Index start TraceId={TraceId} Path={Path}",
                HttpContext.TraceIdentifier,
                Request.Path);

            var userKey = GetUserKey();
            // SQL for hydration must not use Request.Aborted (navigate-away cancels and used to blank the UI).
            using var sqlPageLoadCts = CancellationTokenSource.CreateLinkedTokenSource(_hostLifetime.ApplicationStopping);
            var sqlCt = sqlPageLoadCts.Token;
            var startedFreshLeadChat = false;
            var leadDatabaseHit = false;

            // Sales-portal lead link: new Azure thread + cleared draft so we never merge an old chat with this enquiry.
            var leadIdStr = Request.Query["leadId"].FirstOrDefault();
            // #region agent log
            Debug105ef6("H5", "ChatController.Index:entry", "Index start", new Dictionary<string, object?>
            {
                ["env"] = _env.EnvironmentName,
                ["userKeyLen"] = userKey.Length,
                ["hasLeadIdQuery"] = !string.IsNullOrWhiteSpace(leadIdStr),
                ["sessionIsAvailable"] = HttpContext.Session.IsAvailable
            });
            // #endregion

            if (!string.IsNullOrWhiteSpace(leadIdStr) && Guid.TryParse(leadIdStr, out var leadToken))
            {
                var lead = await _appDb.WestinLeads.AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Token == leadToken, sqlCt);
                leadDatabaseHit = lead != null;

                if (lead != null && lead.QuoteSignedUtc.HasValue)
                {
                    return View("LeadExpired", new Models.LeadExpiredViewModel
                    {
                        Organisation = lead.Organisation,
                        BookingNo = lead.BookingNo,
                        SignedAtUtc = lead.QuoteSignedUtc.Value
                    });
                }

                if (lead != null)
                {
                    var activeLeadToken = HttpContext.Session.GetString("Draft:ActiveLeadToken");
                    var hasPersistedThread = !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("AgentThreadId"));
                    if (string.Equals(activeLeadToken, leadToken.ToString("D"), StringComparison.OrdinalIgnoreCase)
                        && hasPersistedThread)
                    {
                        // Refresh with the same lead link: keep chat thread and draft (do not restart from email gate).
                        _logger.LogInformation("Lead token {Token} already active for this session; skipping thread reset.", leadToken);
                    }
                    else
                    {
                    HttpContext.Session.SetString("Draft:ActiveLeadToken", leadToken.ToString("D"));
                    ClearBookingAndQuoteDraftState();
                    HttpContext.Session.Remove("Draft:ContactFormSubmitted");
                    HttpContext.Session.Remove("Draft:EventFormSubmitted");
                    HttpContext.Session.Remove("Draft:ShowContactSummary");
                    HttpContext.Session.Remove("AgentThreadId");
                    HttpContext.Session.Remove("IslaGreeted");
                    HttpContext.Session.Remove("Draft:ContactId");
                    HttpContext.Session.Remove("Draft:CustomerCode");
                    HttpContext.Session.Remove("Draft:OrgId");
                    HttpContext.Session.Remove("Ack:Budget");
                    HttpContext.Session.Remove("Ack:Attendees");
                    HttpContext.Session.Remove("Ack:SetupStyle");
                    HttpContext.Session.Remove("Ack:Venue");
                    HttpContext.Session.Remove("Ack:SpecialRequests");
                    HttpContext.Session.Remove("Ack:Dates");

                    HttpContext.Session.SetString("Draft:EntrySource", "lead");

                    // Always clear lead-seeded fields before re-applying from new lead, so no stale data
                    // from a previous lead session carries over when fields are absent on the new lead.
                    HttpContext.Session.Remove("Draft:ContactName");
                    HttpContext.Session.Remove("Draft:ContactPhone");
                    HttpContext.Session.Remove("Draft:Organisation");
                    HttpContext.Session.Remove("Draft:OrganisationAddress");
                    HttpContext.Session.Remove("Draft:VenueName");
                    HttpContext.Session.Remove("Draft:RoomName");
                    HttpContext.Session.Remove("Draft:EventDate");
                    HttpContext.Session.Remove("Draft:ExpectedAttendees");

                    var contactName = $"{lead.FirstName} {lead.LastName}".Trim();
                    if (!string.IsNullOrWhiteSpace(contactName)) HttpContext.Session.SetString("Draft:ContactName", contactName);
                    if (!string.IsNullOrWhiteSpace(lead.Email) && TryValidateEmailFormat(lead.Email, out var leadEmailNorm))
                    {
                        HttpContext.Session.Remove("Draft:ContactEmail");
                        HttpContext.Session.SetString("Draft:LeadVerifyEmail", leadEmailNorm);
                    }
                    else
                    {
                        HttpContext.Session.Remove("Draft:ContactEmail");
                        HttpContext.Session.Remove("Draft:LeadVerifyEmail");
                    }

                    if (!string.IsNullOrWhiteSpace(lead.PhoneNumber)) HttpContext.Session.SetString("Draft:ContactPhone", lead.PhoneNumber);
                    if (!string.IsNullOrWhiteSpace(lead.Organisation)) HttpContext.Session.SetString("Draft:Organisation", lead.Organisation);
                    if (!string.IsNullOrWhiteSpace(lead.OrganisationAddress)) HttpContext.Session.SetString("Draft:OrganisationAddress", lead.OrganisationAddress);
                    if (!string.IsNullOrWhiteSpace(lead.Venue)) HttpContext.Session.SetString("Draft:VenueName", lead.Venue);
                    if (!string.IsNullOrWhiteSpace(lead.Room)) HttpContext.Session.SetString("Draft:RoomName", lead.Room);
                    if (!string.IsNullOrWhiteSpace(lead.EventStartDate)) HttpContext.Session.SetString("Draft:EventDate", NormalizeToIsoDateOrEmpty(lead.EventStartDate));
                    if (!string.IsNullOrWhiteSpace(lead.EventEndDate)) HttpContext.Session.SetString("Draft:EventEndDate", NormalizeToIsoDateOrEmpty(lead.EventEndDate));
                    if (!string.IsNullOrWhiteSpace(lead.Attendees))
                    {
                        HttpContext.Session.SetString("Draft:ExpectedAttendees", lead.Attendees);
                        HttpContext.Session.SetString("Draft:LeadSeededExpectedAttendees", lead.Attendees.Trim());
                    }

                    // Reuse the booking created at lead-save time so quote generation updates it
                    // instead of creating a second booking (see LeadsController.Create -> WestinLead.BookingNo).
                    if (!string.IsNullOrWhiteSpace(lead.BookingNo))
                    {
                        HttpContext.Session.SetString("Draft:BookingNo", lead.BookingNo);
                    }

                    if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:LeadVerifyEmail")))
                    {
                        HttpContext.Session.Remove("Draft:EmailGateCompleted");
                        HttpContext.Session.Remove("Draft:ContactFormSubmitted");
                        HttpContext.Session.Remove("Draft:NeedManualContact");
                    }
                    else
                    {
                        HttpContext.Session.SetString("Draft:EmailGateCompleted", "1");
                        HttpContext.Session.SetString("Draft:ContactFormSubmitted", "1");
                        HttpContext.Session.Remove("Draft:NeedManualContact");
                    }

                    _logger.LogInformation("Pre-filled session from lead {LeadId} for {Email}; starting new chat thread.", lead.Id, lead.Email);

                    // Contact already exists on the lead record — skip Azure two-phase "saving contact" bubble.
                    HttpContext.Session.SetString("ContactSaveCompleted", "1");

                    var newThreadId = _chat.EnsureThreadId(HttpContext.Session);
                    await _chat.ReplacePersistedThreadAsync(userKey, newThreadId, sqlCt);
                    await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);
                    startedFreshLeadChat = true;
                    }
                }

                // #region agent log
                Debug105ef6("H2", "ChatController.Index:afterLeadLookup", "Lead token lookup", new Dictionary<string, object?>
                {
                    ["leadDatabaseHit"] = leadDatabaseHit,
                    ["startedFreshLeadChat"] = startedFreshLeadChat
                });
                // #endregion
            }
            else if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EntrySource")))
            {
                HttpContext.Session.SetString("Draft:EntrySource", "general");
            }

            // Check for existing quote based on booking number in session
            var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");
            if (!string.IsNullOrWhiteSpace(bookingNo))
            {
                // Check for existing HTML quotes
                var quotesDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
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

            if (!startedFreshLeadChat)
            {
                await MaybeResetOrphanSessionAfterDbWipeAsync(userKey, sqlCt);

                await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, sqlCt);

                // Restore draft state from DB when the session has no EntrySource (cold load edge case).
                var emailForRestore = HttpContext.Session.GetString("Draft:ContactEmail");
                if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EntrySource"))
                    && !string.IsNullOrWhiteSpace(emailForRestore))
                {
                    try
                    {
                        var saved = await _sessionPersistence.FindByEmailAsync(emailForRestore, sqlCt);
                        if (saved != null && !string.IsNullOrWhiteSpace(saved.DraftStateJson))
                            _sessionPersistence.RestoreToSession(HttpContext.Session, saved);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Session restore by email skipped on Index for TraceId={TraceId}", HttpContext.TraceIdentifier);
                    }
                }

                await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);
            }

            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var (_, messages) = _chat.GetTranscript(threadId);
            var msgList = messages.ToList();
            // #region agent log
            Debug105ef6("H3", "ChatController.Index:beforeForms", "Transcript before EnsureStructuredFormsInChat", new Dictionary<string, object?>
            {
                ["threadIdSuffix"] = threadId.Length > 8 ? threadId[^8..] : threadId,
                ["msgCount"] = msgList.Count,
                ["quoteComplete"] = HttpContext.Session.GetString("Draft:QuoteComplete") ?? "",
                ["entrySource"] = HttpContext.Session.GetString("Draft:EntrySource") ?? ""
            });
            // #endregion
            EnsureStructuredFormsInChat(msgList);
            EnsureQuoteReadyCardInMessages(msgList);
            // #region agent log
            Debug105ef6("H4", "ChatController.Index:afterForms", "Messages after EnsureStructuredFormsInChat", new Dictionary<string, object?>
            {
                ["msgCount"] = msgList.Count,
                ["quoteComplete"] = HttpContext.Session.GetString("Draft:QuoteComplete") ?? ""
            });
            // #endregion
            RedactPricesForUiInPlace(msgList);
            ViewData["DevModeEnabled"] = _devOptions.Enabled;
            ViewData["IsDevelopment"] = _env.IsDevelopment();
            _logger.LogInformation("DevMode enabled: {_devOptions.Enabled}", _devOptions.Enabled);
            ViewData["QuoteComplete"] = false;
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            ViewData["LeadToken"] = HttpContext.Session.GetString("Draft:ActiveLeadToken") ?? "";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return View(msgList);
        }
        catch (OperationCanceledException ex)
        {
            // Do not rethrow — cancellation would surface as a generic 500 via UseExceptionHandler.
            _logger.LogInformation(
                ex,
                "Chat Index canceled (requestAborted={RequestAborted}); attempting session/Azure transcript fallback.",
                HttpContext.RequestAborted.IsCancellationRequested);

            var userKey = GetUserKey();
            try
            {
                using var sqlRetryCts = CancellationTokenSource.CreateLinkedTokenSource(_hostLifetime.ApplicationStopping);
                var sqlCt = sqlRetryCts.Token;
                await MaybeResetOrphanSessionAfterDbWipeAsync(userKey, sqlCt);
                await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, sqlCt);
            }
            catch (Exception retryEx)
            {
                _logger.LogWarning(retryEx, "Chat Index cancel fallback: SQL hydrate failed; using session thread only.");
            }

            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            List<DisplayMessage> msgList;
            try
            {
                var (_, messages) = _chat.GetTranscript(threadId);
                msgList = messages.ToList();
            }
            catch (Exception transcriptEx)
            {
                _logger.LogWarning(transcriptEx, "Chat Index cancel fallback: transcript unavailable for thread suffix {Suffix}",
                    threadId.Length > 8 ? threadId[^8..] : threadId);
                SetScheduleTimesInViewData();
                ViewData["ProgressStep"] = "";
                ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
                ViewData["QuoteComplete"] = false;
                ViewData["DevModeEnabled"] = _devOptions.Enabled;
                ViewData["IsDevelopment"] = _env.IsDevelopment();
                return View(Enumerable.Empty<DisplayMessage>());
            }

            EnsureStructuredFormsInChat(msgList);
            EnsureQuoteReadyCardInMessages(msgList);
            RedactPricesForUiInPlace(msgList);
            ViewData["DevModeEnabled"] = _devOptions.Enabled;
            ViewData["IsDevelopment"] = _env.IsDevelopment();
            ViewData["QuoteComplete"] = false;
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return View(msgList);
        }
        catch (Exception ex)
        {
            // #region agent log
            Debug105ef6("H1", "ChatController.Index:catch", "Index exception", new Dictionary<string, object?>
            {
                ["exType"] = ex.GetType().FullName ?? "",
                ["exMessage"] = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message
            });
            // #endregion
            var userMessage = ex is AuthenticationFailedException
                ? "Chat could not sign in to Azure (check AZURE_CLIENT_ID, AZURE_TENANT_ID, and AZURE_CLIENT_SECRET on the App Service, or use Managed Identity)."
                : ex.Message;
            ModelState.AddModelError(string.Empty, userMessage);
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

        if (string.Equals(HttpContext.Session.GetString("Draft:QuoteAccepted"), "1"))
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
        if (string.Equals(HttpContext.Session.GetString("Draft:QuoteAccepted"), "1"))
            return Json(new { success = false, error = "This quote has already been accepted. The chat session has ended." });

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
            EnsureStructuredFormsInChat(msgList);
            EnsureQuoteReadyCardInMessages(msgList);
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
                EnsureStructuredFormsInChat(msgList);
                EnsureQuoteReadyCardInMessages(msgList);
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
                text.Contains("our team will follow up with you to help complete your booking") ||
                text.Contains("being finalized now") ||
                text.Contains("being finalised now") ||
                (text.Contains("let's proceed with generating") && text.Contains("quote")) ||
                (text.Contains("i'm now finalising") && text.Contains("everything")))
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

    /// <summary>Pre-quote equipment summary step was removed; chat no longer shows Yes/Create quote from this signal.</summary>
    private static bool WasLastAssistantASummaryAsk(IReadOnlyList<DisplayMessage> messages) => false;

    /// <summary>
    /// Last assistant before the user reply is steering to quote generation (narrow phrases — see <see cref="QuoteSummaryAskHelpers.LooksLikeQuoteGenerationPromptNormalized"/>).
    /// Used to unlock <c>generate_quote</c> for short conversational consent ("yes") and progress UI, without restoring the old summary card.
    /// </summary>
    private static bool WasLastAssistantQuoteGenerationConsentPrompt(IReadOnlyList<DisplayMessage> messages)
    {
        if (messages == null || messages.Count < 1) return false;

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var m = messages[i];
            if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;
            if (QuoteSummaryAskHelpers.IsAssistantSubmittedFormUiMessage(m))
                continue;

            var raw = string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
            var t = QuoteSummaryAskHelpers.NormalizeForSummaryAsk(raw);
            return QuoteSummaryAskHelpers.LooksLikeQuoteGenerationPromptNormalized(t);
        }

        return false;
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
            EnsureQuoteReadyCardInMessages(msgList);
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
        return $"Choose schedule: date={dateIso}; setup=08:00; rehearsal=08:30; start=09:00; end=17:00";
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

        if (string.Equals(HttpContext.Session.GetString("Draft:QuoteAccepted"), "1"))
            return Json(new { success = false, error = "This quote has already been accepted. The chat session has ended." });

        try
        {
            var userKey = GetUserKey();
            var threadId = await _chat.EnsureThreadIdPersistedAsync(HttpContext.Session, userKey, ct);
            var (_, beforeMessages) = _chat.GetTranscript(threadId);
            var assistantCountBeforeTurn = CountAssistantMessages(beforeMessages);

            text = text.Trim();
            var rawUserTextForContact = text;   // keep original text for contact parsing

            var emailFormThisRequest = false;
            if (TryCaptureEmailFormSubmission(text, out var emailNorm))
            {
                var leadVerify = HttpContext.Session.GetString("Draft:LeadVerifyEmail");
                if (!string.IsNullOrEmpty(leadVerify)
                    && !string.Equals(emailNorm, leadVerify, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new
                    {
                        success = false,
                        error = "That email doesn’t match the address this enquiry was sent to. Please use the same email or ask your Microhire contact for help."
                    });
                }

                HttpContext.Session.SetString("Draft:ContactEmail", emailNorm);
                HttpContext.Session.SetString("Draft:EmailGateCompleted", "1");
                HttpContext.Session.Remove("Draft:LeadVerifyEmail");

                // Check for existing session to restore, but only if it's for the same booking
                var lookup = IsLeadEntry() ? null : await _bookingQuery.FindLatestUpcomingBookingForEmailAsync(emailNorm, ct);
                var existingSession = await _sessionPersistence.FindByEmailAsync(emailNorm, ct);
                
                bool shouldRestore = false;
                if (existingSession != null && !string.IsNullOrWhiteSpace(existingSession.DraftStateJson))
                {
                    if (IsLeadEntry())
                    {
                        // Lead entries: if they are returning to the same thread, restore it
                        shouldRestore = true;
                    }
                    else if (lookup?.Booking == null)
                    {
                        // No new booking found in RentalPoint, restore whatever progress they had
                        shouldRestore = true;
                    }
                    else
                    {
                        // We found a booking in RentalPoint. Check if it matches the one in our saved session.
                        try
                        {
                            var state = JsonSerializer.Deserialize<Dictionary<string, string>>(existingSession.DraftStateJson);
                            if (state != null && state.TryGetValue("Draft:BookingNo", out var savedBookingNo) && savedBookingNo == lookup.Booking.booking_no)
                            {
                                shouldRestore = true;
                            }
                            else
                            {
                                var oldBooking = (state != null && state.TryGetValue("Draft:BookingNo", out var sbn)) ? sbn : "none";
                                _logger.LogInformation("New booking {NewBooking} found for {Email}; starting fresh instead of restoring old session {OldBooking}", 
                                    lookup.Booking.booking_no, emailNorm, oldBooking);
                                
                                // FORCE START FRESH: clear progress and start a new Azure thread
                                ClearBookingAndQuoteDraftState();
                                HttpContext.Session.Remove("AgentThreadId");
                                var freshThreadId = _chat.EnsureThreadId(HttpContext.Session);
                                await _chat.ReplacePersistedThreadAsync(userKey, freshThreadId, ct);
                                await _chat.EnsureGreetingAsync(HttpContext.Session, GreetingText, ct);
                                
                                shouldRestore = false;
                            }
                        }
                        catch
                        {
                            shouldRestore = true; // fallback to restore if JSON is corrupt
                        }
                    }
                }

                var noPersistedDraftToRestore = existingSession == null
                    || string.IsNullOrWhiteSpace(existingSession.DraftStateJson);
                if (noPersistedDraftToRestore && !shouldRestore)
                {
                    _logger.LogInformation(
                        "No persisted chat draft for {Email}; resetting Azure thread and wizard session while preserving contact/lead keys.",
                        emailNorm);
                    await ResetChatProgressForFreshPersistenceAsync(userKey, ct);
                }

                if (shouldRestore && existingSession != null)
                {
                    try
                    {
                        // Restore thread + all draft state from the database
                        _sessionPersistence.RestoreToSession(HttpContext.Session, existingSession);

                        // Update the DB row's UserKey to this session's key for future lookups
                        // (FindByEmailAsync uses AsNoTracking — must mark entity modified before SaveChanges.)
                        existingSession.UserKey = GetUserKey();
                        existingSession.LastSeenUtc = DateTime.UtcNow;
                        _appDb.AgentThreads.Update(existingSession);
                        await _appDb.SaveChangesAsync(ct);

                        // Return the restored transcript immediately (skip booking lookup + AI agent call)
                        var restoredThreadId = _chat.EnsureThreadId(HttpContext.Session);
                        var (_, restoredMessages) = _chat.GetTranscript(restoredThreadId);
                        var restoredList = restoredMessages.ToList();
                        EnsureStructuredFormsInChat(restoredList);
                        EnsureQuoteReadyCardInMessages(restoredList);
                        RedactPricesForUiInPlace(restoredList);
                        ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
                        SetScheduleTimesInViewData();
                        ViewData["ProgressStep"] = DetermineProgressStep(restoredList);
                        var restoredHtml = await RenderPartialViewToStringAsync("_Messages", restoredList);
                        return Content(restoredHtml, "text/html");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to restore persisted chat session for {Email}; falling back to normal email verification flow.",
                            emailNorm);
                    }
                }

                if (IsLeadEntry())
                {
                    HttpContext.Session.Remove("Draft:NeedManualContact");
                    HttpContext.Session.Remove("Draft:BookingLookupApplied");
                    HttpContext.Session.SetString("ContactSaveCompleted", "1");
                    text = $"Email confirmed for enquiry: {emailNorm}. Continue with venue and event details already captured from the sales team.";
                }
                else
                {
                    // Use the lookup we already performed above
                    if (lookup == null)
                    {
                        HttpContext.Session.SetString("Draft:NeedManualContact", "1");
                        HttpContext.Session.Remove("Draft:BookingLookupApplied");
                        text = BuildEmailIntakeSyntheticMessage(emailNorm, null);
                    }
                    else if (lookup.Booking == null)
                    {
                        ApplyContactOnlyFromLookup(lookup.Contact);
                        HttpContext.Session.SetString("Draft:NeedManualContact", "1");
                        HttpContext.Session.Remove("Draft:BookingLookupApplied");
                        text = BuildEmailIntakeSyntheticMessage(emailNorm, lookup);
                    }
                    else
                    {
                        ApplyBookingPrefillToSession(lookup);
                        HttpContext.Session.SetString("Draft:BookingLookupApplied", "1");
                        // Contact is already on file from booking lookup — skip Azure two-phase "saving contact" bubble.
                        HttpContext.Session.SetString("ContactSaveCompleted", "1");
                        text = BuildEmailIntakeSyntheticMessage(emailNorm, lookup);
                    }
                }

                emailFormThisRequest = true;
            }

            var venueConfirmThisRequest = false;
            if (TryCaptureVenueConfirmFormSubmission(text, out var venueConfirm, out _))
            {
                SaveVenueConfirmToSession(venueConfirm);
                // Always use synthetic line so IsHiddenUserSyntheticMessage hides it; raw "yes" leaked as a bubble below later wizard forms.
                text = BuildVenueConfirmSyntheticMessage(venueConfirm);
                venueConfirmThisRequest = true;
            }

            var eventDetailsThisRequest = false;
            if (TryCaptureEventDetailsFormSubmission(text, out var eventDetails))
            {
                SaveEventDetailsToSession(eventDetails);
                text = BuildEventDetailsSyntheticMessage(eventDetails);
                eventDetailsThisRequest = true;
            }

            var baseAvThisRequest = false;
            if (TryCaptureBaseAvFormSubmission(text, out var baseAv, out _))
            {
                SaveBaseAvToSession(baseAv);
                // Always use synthetic line so IsHiddenUserSyntheticMessage hides it; raw userMessage leaked as a bubble below later wizard forms.
                text = BuildBaseAvSyntheticMessage(baseAv);
                TryPersistProjectorPlacementFromBaseAv(threadId, baseAv);
                baseAvThisRequest = true;
            }

            var followUpAvThisRequest = false;
            if (TryCaptureFollowUpAvFormSubmission(text, out var followUp))
            {
                SaveFollowUpAvToSession(followUp);
                text = BuildFollowUpAvSyntheticMessage(followUp);
                followUpAvThisRequest = true;
            }

            if (TryCaptureContactFormSubmission(text, out var contactForm))
            {
                SaveContactFormToSession(contactForm);
                text =
                    $"Contact details provided: Name {contactForm.FirstName} {contactForm.LastName}; " +
                    $"Organisation {contactForm.Organisation}; Location {contactForm.Location}; " +
                    $"Email {contactForm.Email}; Phone {contactForm.Phone}.";
            }

            if (TryCaptureEventFormSubmission(text, out var eventForm))
            {
                SaveEventFormToSession(eventForm);
                text =
                    $"Event details provided: venue {eventForm.Venue}; event type {eventForm.EventType}; " +
                    $"setup style {eventForm.SetupStyle}; attendees {eventForm.Attendees}; date {eventForm.Date}; " +
                    $"schedule setup {eventForm.SetupTime}, rehearsal {eventForm.RehearsalTime}, " +
                    $"start {eventForm.StartTime}, end {eventForm.EndTime}.";
            }

            if (TryCaptureAvExtrasFormSubmission(text, out var avExtras))
            {
                SaveAvExtrasToSession(avExtras);
                text =
                    $"AV extras provided: presenters {avExtras.Presenters}; speakers {avExtras.Speakers}; " +
                    $"wireless clicker {avExtras.Clicker}; audio video recording {avExtras.Recording}; " +
                    $"technician from {avExtras.TechStart} to {avExtras.TechEnd}; whole event coverage {avExtras.TechWholeEvent}.";
            }

            if (LooksLikeNewQuoteIntent(text))
            {
                ClearBookingAndQuoteDraftState();
                _logger.LogInformation("[QUOTE FLOW] New quote intent detected in SendPartial, cleared booking/quote draft state. UserText={UserText}", text.Length > 100 ? text.Substring(0, 100) + "..." : text);
            }

            // Unlock generate_quote: explicit phrases ("yes create quote") are unambiguous — set flag without
            // requiring last-assistant summary detection (matches isConsentToSummary + tool loop).
            // Conversational consent ("yes", "go ahead") still requires a prior summary ask to limit false positives.
            HttpContext.Session.Remove(GenerateQuoteFlag);
            var explicitCreateConsent = LooksLikeExplicitCreateQuoteConsent(text);
            var conversationalConsent = LooksLikeConsent(text);
            if (explicitCreateConsent)
            {
                HttpContext.Session.SetString(GenerateQuoteFlag, "1");
            }
            else if (conversationalConsent)
            {
                var (_, preMessages) = _chat.GetTranscript(threadId);
                if (WasLastAssistantQuoteGenerationConsentPrompt(preMessages.ToList()))
                {
                    HttpContext.Session.SetString(GenerateQuoteFlag, "1");
                }
            }

            // Structured wizard: follow-up AV form submit ("Get quote" / "Generate quote") is explicit quote intent.
            if (followUpAvThisRequest)
            {
                HttpContext.Session.SetString(GenerateQuoteFlag, "1");
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
                if (followUpAvThisRequest)
                {
                    // Recommend from session first; only append the user line to Azure after success so fallback
                    // SendAsync does not duplicate the user message.
                    var rec = await _toolHandler.RecommendEquipmentFromWizardSessionAsync(threadId, ct);
                    if (rec.Success)
                    {
                        await _chat.AppendUserMessageAsync(HttpContext.Session, text, ct);
                        // Server-driven quote: no agent run (avoids intermediate "consolidated AV requirements" turn).
                        var (_, mlAfter) = _chat.GetTranscript(threadId);
                        var msgListForQuote = mlAfter is List<DisplayMessage> lq ? lq : mlAfter.ToList();
                        var followUpEarly = await TryFollowUpAvQuotePipelineAsync(msgListForQuote, threadId, ct);
                        if (followUpEarly != null)
                        {
                            return followUpEarly;
                        }

                        (_, mlAfter) = _chat.GetTranscript(threadId);
                        sendResult = new SendAsyncResult(mlAfter, false);
                    }
                    else
                    {
                        // Equipment recommendation failed, but session already has base AV equipment
                        // from the earlier wizard step. Still use the server-side quote path to avoid
                        // the AI agent generating verbose/confusing text.
                        _logger.LogWarning("[FollowUpAv] RecommendEquipmentFromWizardSessionAsync failed: {Err}; using server-side quote path (not AI agent)", rec.ErrorMessage);
                        await _chat.AppendUserMessageAsync(HttpContext.Session, text, ct);
                        var (_, mlFallback) = _chat.GetTranscript(threadId);
                        var msgListFallback = mlFallback is List<DisplayMessage> lf ? lf : mlFallback.ToList();
                        var followUpFallback = await TryFollowUpAvQuotePipelineAsync(msgListFallback, threadId, ct);
                        if (followUpFallback != null)
                        {
                            return followUpFallback;
                        }

                        (_, mlFallback) = _chat.GetTranscript(threadId);
                        sendResult = new SendAsyncResult(mlFallback, false);
                    }
                }
                else if (emailFormThisRequest || venueConfirmThisRequest || eventDetailsThisRequest || baseAvThisRequest)
                {
                    // Structured wizard: persist user line to Azure for transcript continuity but skip the agent run.
                    // SendAsync post-processing (contact/booking DB, time picker, etc.) routinely exceeds client fetch
                    // timeouts for this step; EnsureStructuredFormsInChat supplies the next UI immediately.
                    var structuredStep = emailFormThisRequest ? "email gate"
                        : venueConfirmThisRequest ? "venue confirm"
                        : eventDetailsThisRequest ? "event details"
                        : "base AV";
                    _logger.LogInformation(
                        "[CHAT_FLOW] Skipping SendAsync for structured {Step}; AppendUserMessageAsync only ({Duration}s since send start)",
                        structuredStep,
                        (DateTime.UtcNow - sendStart).TotalSeconds);
                    await _chat.AppendUserMessageAsync(HttpContext.Session, text, ct);
                    var (_, mlSkip) = _chat.GetTranscript(threadId);
                    var listSkip = mlSkip is List<DisplayMessage> ls ? ls : mlSkip.ToList();
                    sendResult = new SendAsyncResult(listSkip, false);
                }
                else
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
                EnsureStructuredFormsInChat(partialList);
                EnsureQuoteReadyCardInMessages(partialList);
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
            EnsureStructuredFormsInChat(msgList);
            // Email/venue/event-details/base-AV structured steps skip SendAsync (user line only); no new assistant turn is expected.
            if (!HasAssistantDelta(assistantCountBeforeTurn, msgList)
                && !emailFormThisRequest
                && !venueConfirmThisRequest
                && !eventDetailsThisRequest
                && !baseAvThisRequest)
            {
                _logger.LogWarning("[CHAT_FLOW] SendPartial produced no assistant delta for thread {ThreadId} (sendFailed={SendFailed}). Appending fallback reply.", threadId, sendFailed);
                await AddAssistantMessageAndPersistAsync(msgList, BuildTransientFailureFallbackMessage(), ct);
                EnsureStructuredFormsInChat(msgList);
            }
            EnsureQuoteReadyCardInMessages(msgList);
            EnsureImmediateQuoteReviewPromptAfterQuoteSuccess(msgList);

            // If the last AI message contains confusing quote language, we'll handle it in the consent logic

            // Check if user confirmed a booking summary
            // IMPORTANT: Trigger when EITHER:
            // 1. User's message looks like consent AND the assistant was asking for confirmation (summary ask), OR
            // 2. User explicitly said "yes create quote" (or similar) - bypass last-assistant prompt detection because after AI responds,
            //    the "last assistant" is the AI's error message, not the original summary. Explicit phrase is unambiguous.
            var looksLikeConsent = LooksLikeConsent(text);
            var wasQuoteGenerationPrompt = WasLastAssistantQuoteGenerationConsentPrompt(msgList);
            var isExplicitCreateQuote = LooksLikeExplicitCreateQuoteConsent(text);
            var isConsentToSummary = (looksLikeConsent && wasQuoteGenerationPrompt) || isExplicitCreateQuote;

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
                "WasQuoteGenerationPrompt={WasQuoteGenerationPrompt}, " +
                "IsExplicitCreateQuote={IsExplicitCreateQuote}, " +
                "IsConsentToSummary={IsConsentToSummary}, " +
                "IsQuoteAccepted={IsQuoteAccepted}, " +
                "ExistingBookingNo={ExistingBookingNo}, " +
                "QuoteComplete={QuoteComplete}, " +
                "ExistingQuoteUrl={ExistingQuoteUrl}",
                text?.Substring(0, Math.Min(50, text?.Length ?? 0)),
                looksLikeConsent,
                wasQuoteGenerationPrompt,
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
                    // Check whether a digital signature file exists for this booking
                    var sigFile = Path.Combine(QuoteFilesPaths.GetPhysicalQuotesDirectory(_env), $"Quote-{bookingNo}-signature.json");
                    var hasSignatureFile = System.IO.File.Exists(sigFile);

                    if (!hasSignatureFile)
                    {
                        // No digital signature on file — redirect user to the proper signing flow
                        _logger.LogInformation("[QUOTE FLOW] Text acceptance for booking {BookingNo} but no signature file found. Showing acceptance CTA instead.", bookingNo);
                        AppendQuoteReviewPromptImmediately(msgList);
                        SetScheduleTimesInViewData();
                        ViewData["ProgressStep"] = DetermineProgressStep(msgList);
                        return PartialView("_Messages", msgList);
                    }

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

                    var confirmText = $"Your quote {bookingNo}, for a booking on {bookingDateStr} is confirmed. " +
                                      "One of our team members will be in contact with you shortly.";

                    var fallbackQuoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
                    var fallbackSignedActionsHtml = "";
                    if (!string.IsNullOrWhiteSpace(fallbackQuoteUrl))
                    {
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
                        Html = $"<p>{System.Net.WebUtility.HtmlEncode(confirmText)}</p>{fallbackSignedActionsHtml}"
                    };
                    await AddAssistantMessageAndPersistAsync(msgList, confirmationMessage, ct);
                    HttpContext.Session.SetString("Draft:QuoteAccepted", "1");
                    HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "0");
                    HttpContext.Session.SetString(QuoteReviewPromptShownKey, "1");

                    await MarkLeadAsSignedAsync(null, ct);

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

                    EnsureQuoteReadyCardInMessages(msgList);
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
                if (string.IsNullOrWhiteSpace(organisation) && !IsLeadEntry())
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
                // Recovery: Draft:ProjectorAreas may be lost between requests; re-parse from placement choice
                if (draftProjectorAreas.Count == 0
                    && string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal))
                {
                    var pc = HttpContext.Session.GetString("Draft:ProjectorPlacementChoice");
                    if (!string.IsNullOrEmpty(pc) && !string.Equals(pc, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        var roomForParse = HttpContext.Session.GetString("Draft:RoomName");
                        draftProjectorAreas = ParseProjectorPlacementToAllowedAreas(pc, roomForParse);
                        if (draftProjectorAreas.Count > 0)
                        {
                            HttpContext.Session.SetString("Draft:ProjectorAreas", string.Join(",", draftProjectorAreas));
                            HttpContext.Session.SetString("Draft:ProjectorArea", draftProjectorAreas[0]);
                            _logger.LogInformation("[PROJECTOR_AREA_RECOVERY] Recovered areas [{Areas}] from PlacementChoice={Choice} (HandleSmartEquipment)",
                                string.Join(",", draftProjectorAreas), pc);
                        }
                    }
                }
                var summaryReqJson = HttpContext.Session.GetString("Draft:SummaryEquipmentRequests") ?? string.Empty;
                var selectedEquipmentJsonForArea = HttpContext.Session.GetString("Draft:SelectedEquipment") ?? string.Empty;

                var isWestinBallroomFamily = IsDraftWestinBallroomFamily(draftVenueName, draftRoomName);

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
                var requiredAreaCount = 0;
                if (projectionNeeded)
                {
                    if (isWestinBallroomFamily)
                        requiredAreaCount = Math.Max(IsFullWestinBallroomRoomName(draftRoomName) ? 2 : 1,
                            projectorCount > 1 ? Math.Min(projectorCount, 3) : 1);
                    else
                        requiredAreaCount = projectorCount > 1 ? Math.Min(projectorCount, 3) : 1;
                }
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

                var allowSingleBaseAvFullBallroom = isWestinBallroomFamily
                    && projectionNeeded
                    && requiredAreaCount > 1
                    && validSelectedAreas.Count == 1
                    && IsFullWestinBallroomRoomName(draftRoomName)
                    && string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal);

                if (isWestinBallroomFamily && projectionNeeded && validSelectedAreas.Count < requiredAreaCount && !allowSingleBaseAvFullBallroom)
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
                    var expectedAttendeesVal = HttpContext.Session.GetString("Draft:ExpectedAttendees") ?? HttpContext.Session.GetString("Ack:Attendees");
                    if (!string.IsNullOrWhiteSpace(expectedAttendeesVal))
                        additionalFacts["expected_attendees"] = expectedAttendeesVal;

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
            
            // Persist draft state to database if email is verified
            var emailForSave = HttpContext.Session.GetString("Draft:ContactEmail");
            if (!string.IsNullOrWhiteSpace(emailForSave))
            {
                var snapshot = _sessionPersistence.SnapshotDraftState(HttpContext.Session);
                var currentUserKey = GetUserKey();
                QueuePersistDraftStateBackground(emailForSave, currentUserKey, snapshot);
            }

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

                // Ensure wizard forms still render even after an error — without this,
                // the user gets stuck on greeting + error with no way to continue.
                var msgCountBeforeForms = msgList.Count;
                EnsureStructuredFormsInChat(msgList);
                EnsureQuoteReadyCardInMessages(msgList);
                var formsWereInjected = msgList.Count > msgCountBeforeForms;

                // Only add error message if wizard forms didn't handle the recovery
                // (i.e., user is past all forms, in the AI chat phase)
                if (!formsWereInjected)
                {
                    var hasQuote = msgList.Any(m => m.Role == "assistant" && (m.FullText?.Contains("successfully generated your quote") == true || m.Html?.Contains("btn-primary") == true));
                    if (!hasQuote)
                    {
                        msgList.Add(new DisplayMessage
                        {
                            Role = "assistant",
                            Timestamp = DateTimeOffset.UtcNow,
                            Parts = new List<string> { "Could you please try sending your message again?" },
                            FullText = "Could you please try sending your message again?",
                            Html = "<p>Could you please try sending your message again?</p>"
                        });
                    }
                }

                RedactPricesForUiInPlace(msgList);
                SetScheduleTimesInViewData();
                ViewData["ShowQuoteCta"] = "0";
                ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
                ViewData["ProgressStep"] = DetermineProgressStep(msgList);

                // Persist draft state even on error if email exists
                var emailForErrorSave = HttpContext.Session.GetString("Draft:ContactEmail");
                if (!string.IsNullOrWhiteSpace(emailForErrorSave))
                {
                    var snapshot = _sessionPersistence.SnapshotDraftState(HttpContext.Session);
                    var currentUserKey = GetUserKey();
                    QueuePersistDraftStateBackground(emailForErrorSave, currentUserKey, snapshot);
                }

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

    /// <summary>
    /// After server-side <c>recommend_equipment_for_event</c> for follow-up AV, run the same contact / schedule /
    /// Westin projector guards and booking + <see cref="GenerateQuoteForBookingAsync"/> as the explicit consent path,
    /// without requiring the user to type "yes create quote".
    /// </summary>
    /// <returns>Non-null to short-circuit SendPartial; null when processing completed (caller refreshes transcript).</returns>
    private async Task<IActionResult?> TryFollowUpAvQuotePipelineAsync(List<DisplayMessage> msgList, string threadId, CancellationToken ct)
    {
        // Ensure forms are in chat BEFORE adding the quote so the quote stays at the bottom.
        EnsureStructuredFormsInChat(msgList);

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

        if (quoteCompleteNow && !string.IsNullOrWhiteSpace(quoteUrlNow) && quoteSuccessRecently)
        {
            _logger.LogInformation("[FollowUpAv] Skipping quote generation — quote already generated in recent messages.");
            if (!string.IsNullOrEmpty(summaryKey))
                HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey);

            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        if (!string.IsNullOrEmpty(summaryKey) &&
            string.Equals(summaryKey, lastPersistedKey, StringComparison.Ordinal))
        {
            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = WasLastAssistantASummaryAsk(msgList) ? "1" : "0";
            ViewData["QuoteAccepted"] = HttpContext.Session.GetString("Draft:QuoteAccepted") ?? "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        var contactName = HttpContext.Session.GetString("Draft:ContactName");
        var contactEmail = HttpContext.Session.GetString("Draft:ContactEmail");
        var contactPhone = HttpContext.Session.GetString("Draft:ContactPhone");
        var organisation = HttpContext.Session.GetString("Draft:Organisation");

        var missingContactFields = new List<string>();
        if (string.IsNullOrWhiteSpace(contactName))
            missingContactFields.Add("customer name");
        if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
            missingContactFields.Add("contact email or phone number");
        if (string.IsNullOrWhiteSpace(organisation) && !IsLeadEntry())
            missingContactFields.Add("organisation name");

        if (missingContactFields.Count > 0)
        {
            _logger.LogWarning("[FollowUpAv] Quote blocked - missing contact fields: {Fields}",
                string.Join(", ", missingContactFields));

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
            _logger.LogWarning("[FollowUpAv] Quote blocked - schedule not yet submitted");
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

        var draftVenueName = HttpContext.Session.GetString("Draft:VenueName");
        var draftRoomName = HttpContext.Session.GetString("Draft:RoomName");
        var draftProjectorAreas = ParseProjectorAreas(HttpContext.Session.GetString("Draft:ProjectorAreas"));
        if (draftProjectorAreas.Count == 0)
            draftProjectorAreas = ParseProjectorAreas(HttpContext.Session.GetString("Draft:ProjectorArea"));
        // Recovery: Draft:ProjectorAreas may be lost between requests; re-parse from placement choice
        if (draftProjectorAreas.Count == 0
            && string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal))
        {
            var pc = HttpContext.Session.GetString("Draft:ProjectorPlacementChoice");
            if (!string.IsNullOrEmpty(pc) && !string.Equals(pc, "none", StringComparison.OrdinalIgnoreCase))
            {
                draftProjectorAreas = ParseProjectorPlacementToAllowedAreas(pc, draftRoomName);
                if (draftProjectorAreas.Count > 0)
                {
                    HttpContext.Session.SetString("Draft:ProjectorAreas", string.Join(",", draftProjectorAreas));
                    HttpContext.Session.SetString("Draft:ProjectorArea", draftProjectorAreas[0]);
                    _logger.LogInformation("[PROJECTOR_AREA_RECOVERY] Recovered areas [{Areas}] from PlacementChoice={Choice} (FollowUpAv)",
                        string.Join(",", draftProjectorAreas), pc);
                }
            }
        }
        var summaryReqJson = HttpContext.Session.GetString("Draft:SummaryEquipmentRequests") ?? string.Empty;
        var selectedEquipmentJsonForArea = HttpContext.Session.GetString("Draft:SelectedEquipment") ?? string.Empty;

        var isWestinBallroomFamily = IsDraftWestinBallroomFamily(draftVenueName, draftRoomName);

        var projectionNeeded =
            summaryReqJson.Contains("projector", StringComparison.OrdinalIgnoreCase) ||
            summaryReqJson.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
            summaryReqJson.Contains("display", StringComparison.OrdinalIgnoreCase) ||
            selectedEquipmentJsonForArea.Contains("projector", StringComparison.OrdinalIgnoreCase) ||
            selectedEquipmentJsonForArea.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
            selectedEquipmentJsonForArea.Contains("display", StringComparison.OrdinalIgnoreCase);

        if (!projectionNeeded)
        {
            HttpContext.Session.Remove("Draft:ProjectorArea");
            HttpContext.Session.Remove("Draft:ProjectorAreas");
        }

        var projectorCount = GetRequestedProjectorCount(summaryReqJson);
        if (projectorCount <= 0 && selectedEquipmentJsonForArea.Contains("projector", StringComparison.OrdinalIgnoreCase))
            projectorCount = 1;
        var requiredAreaCount = 0;
        if (projectionNeeded)
        {
            if (isWestinBallroomFamily)
                requiredAreaCount = Math.Max(IsFullWestinBallroomRoomName(draftRoomName) ? 2 : 1,
                    projectorCount > 1 ? Math.Min(projectorCount, 3) : 1);
            else
                requiredAreaCount = projectorCount > 1 ? Math.Min(projectorCount, 3) : 1;
        }
        var allowedAreas = GetAllowedProjectorAreasForRoom(draftRoomName);
        var validSelectedAreas = draftProjectorAreas
            .Where(a => allowedAreas.Contains(a, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var promptAlreadyShown =
            HttpContext.Session.GetString(ProjectorPromptShownKey) == "1" &&
            HttpContext.Session.GetString(ProjectorPromptThreadIdKey) == threadId;
        var areaAlreadyCapturedInSession =
            HttpContext.Session.GetString(ProjectorAreaCapturedKey) == "1" &&
            HttpContext.Session.GetString(ProjectorAreaThreadIdKey) == threadId;

        // Base AV wizard submission is authoritative — skip the stale-session guard entirely.
        var baseAvSubmittedForQuote = string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal)
            && validSelectedAreas.Count > 0;

        if (!baseAvSubmittedForQuote && !promptAlreadyShown && !areaAlreadyCapturedInSession)
        {
            SyncProjectorPromptMarkers(msgList, threadId);
            promptAlreadyShown = HttpContext.Session.GetString(ProjectorPromptShownKey) == "1" &&
                                HttpContext.Session.GetString(ProjectorPromptThreadIdKey) == threadId;
        }

        if (!baseAvSubmittedForQuote && !promptAlreadyShown && !areaAlreadyCapturedInSession)
            validSelectedAreas.Clear();

        var allowSingleBaseAvFullBallroomFv = isWestinBallroomFamily
            && projectionNeeded
            && requiredAreaCount > 1
            && validSelectedAreas.Count == 1
            && IsFullWestinBallroomRoomName(draftRoomName)
            && string.Equals(HttpContext.Session.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal);

        if (isWestinBallroomFamily && projectionNeeded && validSelectedAreas.Count < requiredAreaCount && !allowSingleBaseAvFullBallroomFv)
        {
            _logger.LogWarning("[FollowUpAv] Quote blocked - missing projector area (Westin Ballroom with projection)");
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
            HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
            HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
            await AddAssistantMessageAndPersistAsync(msgList, areaRequestMessage, ct);
            RedactPricesForUiInPlace(msgList);
            ViewData["ShowQuoteCta"] = "0";
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);
            return PartialView("_Messages", msgList);
        }

        var existingBookingNo = HttpContext.Session.GetString("Draft:BookingNo");

        if (!string.IsNullOrWhiteSpace(existingBookingNo))
        {
            _logger.LogInformation("[FollowUpAv] Quote generation for existing booking {BookingNo}", existingBookingNo);
            await GenerateQuoteForBookingAsync(existingBookingNo, msgList, ct);
            HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey ?? string.Empty);
        }
        else
        {
            _logger.LogInformation("[FollowUpAv] No existing booking — creating booking then quote");

            var additionalFacts = new Dictionary<string, string>();
            var selectedEquipment = HttpContext.Session.GetString("Draft:SelectedEquipment");
            if (!string.IsNullOrWhiteSpace(selectedEquipment))
            {
                additionalFacts["selected_equipment"] = selectedEquipment;
                _logger.LogInformation("[FollowUpAv] Adding stored equipment to booking: {Equipment}", selectedEquipment);
            }
            var selectedLabor = HttpContext.Session.GetString("Draft:SelectedLabor");
            if (!string.IsNullOrWhiteSpace(selectedLabor))
            {
                additionalFacts["selected_labor"] = selectedLabor;
                var laborSummary = TryBuildLaborSummaryFromSelectedLabor(selectedLabor);
                if (!string.IsNullOrWhiteSpace(laborSummary))
                    additionalFacts["labor_summary"] = laborSummary;
            }
            var totalDayRate = HttpContext.Session.GetString("Draft:TotalDayRate");
            if (!string.IsNullOrWhiteSpace(totalDayRate))
                additionalFacts["price_quoted"] = totalDayRate;
            var eventType = HttpContext.Session.GetString("Draft:EventType");
            if (!string.IsNullOrWhiteSpace(eventType))
                additionalFacts["event_type"] = eventType;
            if (projectionNeeded)
            {
                var projectorAreaValue = HttpContext.Session.GetString("Draft:ProjectorArea");
                if (!string.IsNullOrWhiteSpace(projectorAreaValue))
                    additionalFacts["projector_area"] = projectorAreaValue;
                var projectorAreasValue = HttpContext.Session.GetString("Draft:ProjectorAreas");
                if (!string.IsNullOrWhiteSpace(projectorAreasValue))
                    additionalFacts["projector_areas"] = projectorAreasValue;
            }
            var venueName = HttpContext.Session.GetString("Draft:VenueName");
            if (!string.IsNullOrWhiteSpace(venueName))
                additionalFacts["venue_name"] = venueName;
            var roomName = HttpContext.Session.GetString("Draft:RoomName");
            if (!string.IsNullOrWhiteSpace(roomName))
                additionalFacts["venue_room"] = roomName;
            var expectedAttendeesVal = HttpContext.Session.GetString("Draft:ExpectedAttendees") ?? HttpContext.Session.GetString("Ack:Attendees");
            if (!string.IsNullOrWhiteSpace(expectedAttendeesVal))
                additionalFacts["expected_attendees"] = expectedAttendeesVal;

            var result = await _orchestration.ProcessConversationAsync(msgList, existingBookingNo, ct, additionalFacts);

            if (result.Success && !string.IsNullOrWhiteSpace(result.BookingNo))
            {
                HttpContext.Session.SetString("Draft:BookingNo", result.BookingNo!);
                if (result.ContactId.HasValue)
                    HttpContext.Session.SetString("Draft:ContactId", result.ContactId.Value.ToString());
                if (!string.IsNullOrWhiteSpace(result.CustomerCode))
                    HttpContext.Session.SetString("Draft:CustomerCode", result.CustomerCode!);

                HttpContext.Session.SetString("Draft:PersistedSummaryKey", summaryKey ?? string.Empty);
                SetConsent(HttpContext.Session, false);
                HttpContext.Session.SetString("Draft:ShowedBookingNo", "1");

                await GenerateQuoteForBookingAsync(result.BookingNo!, msgList, ct);
            }
            else if (result.Errors.Any())
            {
                _logger.LogError("[FollowUpAv] Booking creation failed: {Errors}", string.Join("; ", result.Errors));

                if (!string.IsNullOrWhiteSpace(existingBookingNo))
                {
                    await GenerateQuoteForBookingAsync(existingBookingNo, msgList, ct);
                }
                else
                {
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

        return null;
    }

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
                var (contactOrOrgChanged, renamedBookingNo) = await _bookingPersistence.SyncContactAndOrganisationForBookingAsync(bookingNo, HttpContext.Session, ct);
                if (!string.IsNullOrWhiteSpace(renamedBookingNo))
                {
                    bookingNo = renamedBookingNo;
                    HttpContext.Session.SetString("Draft:BookingNo", bookingNo);
                }
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

            try
            {
                var eventDetailsChanged = await _bookingPersistence.SyncEventDetailsForBookingAsync(bookingNo, HttpContext.Session, ct);
                if (eventDetailsChanged)
                {
                    HttpContext.Session.Remove("Draft:QuoteUrl");
                    HttpContext.Session.Remove("Draft:QuoteComplete");
                    HttpContext.Session.Remove("Draft:QuoteTimestamp");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync session event details (schedule/times) to booking {BookingNo} before quote generation", bookingNo);
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
                EnsureQuoteReadyCardInMessages(msgList);
                MarkQuoteReviewPromptPending();
                return;
            }
            
            // Add success message with existing quote
            var existingQuoteMessage = BuildQuoteReadyMessage(bookingNo, existingQuoteUrl);
            await AddAssistantMessageAndPersistAsync(msgList, existingQuoteMessage, ct);
            AppendQuoteReviewPromptImmediately(msgList);
            return;
        }
        
        // Disk fallback is for session-expiry resilience only.
        // If session still has equipment data, QuoteComplete was deliberately cleared
        // (e.g. Edit Quote) — skip the fallback and regenerate fresh.
        var sessionHasEquipment = !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:SelectedEquipment"));
        var quotesDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
        if (!sessionHasEquipment && Directory.Exists(quotesDir))
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
            // HTML quote generation can exceed 30s on cold start / remote DB; align with chat client timeout.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            try
            {
                var (quoteSuccess, quoteUrl, quoteError) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(bookingNo, linkedCts.Token, HttpContext.Session);

                // Retry once on failure — the booking may have just been created and DB propagation can lag.
                if (!quoteSuccess || string.IsNullOrWhiteSpace(quoteUrl))
                {
                    _logger.LogWarning("HTML quote generation failed for booking {BookingNo} (first attempt): {Error}. Retrying after 2s...", bookingNo, quoteError);
                    await Task.Delay(2000, linkedCts.Token);
                    (quoteSuccess, quoteUrl, quoteError) = await _htmlQuoteGen.GenerateHtmlQuoteForBookingAsync(bookingNo, linkedCts.Token, HttpContext.Session);
                }
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
                await RecoverQuoteFromDiskOrAnnounceWaitAsync(msgList, bookingNo, quotesDir, ct, "QuoteGenTimeout");
            }
            catch (OperationCanceledException oce)
            {
                // Client/gateway disconnected or request aborted while HTML/PDF still running — not the in-app 5-minute timeout.
                _logger.LogWarning(oce, "HTML quote generation cancelled (external) for booking {BookingNo}", bookingNo);
                await RecoverQuoteFromDiskOrAnnounceWaitAsync(msgList, bookingNo, quotesDir, ct, "QuoteGenCancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during quote generation for booking {BookingNo}", bookingNo);
            try
            {
                await RecoverQuoteFromDiskOrAnnounceWaitAsync(msgList, bookingNo, quotesDir, ct, "QuoteGenException");
            }
            catch (Exception persistEx)
            {
                _logger.LogError(persistEx, "Failed to persist quote-interrupted assistant message for booking {BookingNo}", bookingNo);
            }
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
            else
            {
                // Optimization: if no new prompt was added, don't return the full HTML partial.
                // This prevents the entire chat DOM from being replaced, avoiding UI flicker.
                return Json(new { success = true, noChange = true });
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
            var quoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            string? quoteFilePath = null;
            if (!string.IsNullOrWhiteSpace(quoteUrl) && QuoteFilesPaths.TryResolveExistingQuoteFile(_env, quoteUrl, out var resolvedQuotePath))
                quoteFilePath = resolvedQuotePath;

            // ---- Overlay signature onto quote HTML ----
            if (quoteFilePath != null && System.IO.File.Exists(quoteFilePath))
            {
                try
                {
                    var html = await System.IO.File.ReadAllTextAsync(quoteFilePath, ct);

                    var safeName = System.Net.WebUtility.HtmlEncode(fullName ?? "");
                    var safeDate = System.Net.WebUtility.HtmlEncode(signDate ?? "");
                    var safeTitle = System.Net.WebUtility.HtmlEncode(titlePosition ?? "");
                    var safePo = System.Net.WebUtility.HtmlEncode(purchaseOrder ?? "");
                    var filledLineStyle = "style=\"border-bottom:1px solid #333;height:30px;line-height:30px;font-size:13px;color:#222;padding-left:4px\"";

                    // Match empty or pre-filled full name line (generated quotes may pre-fill ContactName).
                    html = Regex.Replace(html,
                        @"(<label>Full Name</label>\s*)<div class=""signature-line""[^>]*>.*?</div>",
                        $"$1<div class=\"signature-line\" {filledLineStyle}>{safeName}</div>",
                        RegexOptions.Singleline);
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
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[SIGNING] Failed to overlay signature onto quote HTML for booking {BookingNo}", bookingNo);
                }
            }

            // ---- Persist signature metadata as JSON ----
            try
            {
                var sigDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
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

            var confirmText = $"Your quote {bookingNo}, for a booking on {bookingDateStr} is confirmed. " +
                              "One of our team members will be in contact with you shortly.";

            var signedActionsHtml = "";
            if (!string.IsNullOrWhiteSpace(quoteUrl))
            {
                signedActionsHtml = BuildSignedQuoteActionsHtml(quoteUrl, bookingNo);
            }

            EnsureStructuredFormsInChat(msgList);
            EnsureQuoteReadyCardInMessages(msgList);

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
                Html = $"<p>{System.Net.WebUtility.HtmlEncode(confirmText)}</p>{signedActionsHtml}"
            };
            await AddAssistantMessageAndPersistAsync(msgList, confirmationMessage, ct);

            HttpContext.Session.SetString("Draft:QuoteAccepted", "1");
            HttpContext.Session.SetString(AwaitingQuoteReviewPromptKey, "0");
            HttpContext.Session.SetString(QuoteReviewPromptShownKey, "1");

            await MarkLeadAsSignedAsync(fullName, ct);

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

    private async Task MarkLeadAsSignedAsync(string? signerName, CancellationToken ct)
    {
        var leadTokenStr = HttpContext.Session.GetString("Draft:ActiveLeadToken");
        if (string.IsNullOrWhiteSpace(leadTokenStr) || !Guid.TryParse(leadTokenStr, out var leadToken))
            return;

        var lead = await _appDb.WestinLeads
            .FirstOrDefaultAsync(l => l.Token == leadToken, ct);
        if (lead == null || lead.QuoteSignedUtc.HasValue)
            return;

        lead.QuoteSignedUtc = DateTime.UtcNow;
        lead.SignedByName = signerName;
        await _appDb.SaveChangesAsync(ct);
        _logger.LogInformation("[SIGNING] Marked lead {Token} as signed by {Name}", leadToken, signerName ?? "(unknown)");
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

            EnsureStructuredFormsInChat(msgList);
            EnsureQuoteReadyCardInMessages(msgList);
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

    /// <summary>
    /// Resets all form-submission and quote flags so every wizard form becomes editable again,
    /// while keeping the user's previously entered data (venue, room, dates, event type, schedule,
    /// AV preferences, etc.) so the forms prefill with the latest input.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditQuotePartial()
    {
        try
        {
            // Clear form-submitted flags
            HttpContext.Session.Remove("Draft:VenueConfirmSubmitted");
            HttpContext.Session.Remove("Draft:EventFormSubmitted");
            HttpContext.Session.Remove("Draft:BaseAvSubmitted");
            HttpContext.Session.Remove("Draft:BaseAvSubmittedForThread");
            HttpContext.Session.Remove("Draft:FollowUpAvSubmitted");

            // Clear quote state
            HttpContext.Session.Remove("Draft:QuoteComplete");
            HttpContext.Session.Remove("Draft:QuoteUrl");
            HttpContext.Session.Remove("Draft:QuoteAccepted");
            // Keep Draft:BookingNo and Draft:ShowedBookingNo so the existing booking
            // is updated (not recreated) when the user re-generates the quote.
            HttpContext.Session.Remove("Draft:PersistedSummaryKey");
            HttpContext.Session.Remove(AwaitingQuoteReviewPromptKey);
            HttpContext.Session.Remove(QuoteReviewPromptShownKey);

            // Clear AV selection/equipment state (will be rebuilt on re-submission)
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

            // Re-render with forms in editable state
            var threadId = _chat.EnsureThreadId(HttpContext.Session);
            var (_, messages) = _chat.GetTranscript(threadId);
            var msgList = messages is List<DisplayMessage> list ? list : messages.ToList();

            // Remove quote-related messages so the user can focus on editing the forms
            msgList.RemoveAll(m =>
            {
                if (!string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                    return false;
                var raw = string.Join("\n", (m.Parts ?? new List<string>()).Select(p => p ?? string.Empty))
                    .ToLowerInvariant();
                // "Great news! I have successfully generated your quote..." + Quote Ready UI
                if (raw.Contains("generated your quote") && (raw.Contains("\"quoteurl\"") || raw.Contains("\"quote_url\"")))
                    return true;
                // "Would you like to proceed and accept this quote?"
                if (raw.Contains("would you like to proceed and accept this quote"))
                    return true;
                return false;
            });

            EnsureStructuredFormsInChat(msgList);
            EnsureQuoteReadyCardInMessages(msgList);
            RedactPricesForUiInPlace(msgList);

            ViewData["ShowQuoteCta"] = "0";
            ViewData["QuoteComplete"] = false;
            SetScheduleTimesInViewData();
            ViewData["ProgressStep"] = DetermineProgressStep(msgList);

            return PartialView("_Messages", msgList);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed handling edit quote request");
            Response.StatusCode = 500;
            return Content("Unable to reset forms for editing.");
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
            .Split(new[] { ',', ';', '&', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries)
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
        if (room.Contains("ballroom 1", StringComparison.Ordinal) || room.Contains("ballroom-1", StringComparison.Ordinal))
            return new List<string> { "E", "D", "C" };
        if (room.Contains("ballroom 2", StringComparison.Ordinal) || room.Contains("ballroom-2", StringComparison.Ordinal))
            return new List<string> { "A", "F", "B" };
        return new List<string> { "A", "B", "C", "D", "E", "F" };
    }

    /// <summary>Matches <see cref="AgentToolHandlerService"/> Westin Ballroom family for quote/projector guards.</summary>
    private static bool IsDraftWestinBallroomFamily(string? draftVenueName, string? draftRoomName)
    {
        if (string.IsNullOrWhiteSpace(draftVenueName)
            || !draftVenueName.Contains("westin", StringComparison.OrdinalIgnoreCase)
            || !draftVenueName.Contains("brisbane", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(draftRoomName)) return false;
        return string.Equals(draftRoomName, "Westin Ballroom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Westin Ballroom 1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Westin Ballroom 2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Ballroom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Ballroom 1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(draftRoomName, "Ballroom 2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFullWestinBallroomRoomName(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        return room is "full westin ballroom" or "westin ballroom full" or "full ballroom";
    }

    private void TryPersistProjectorPlacementFromBaseAv(string threadId, BaseAvFormSubmission baseAv)
    {
        var venue = HttpContext.Session.GetString("Draft:VenueName");
        var room = HttpContext.Session.GetString("Draft:RoomName");
        if (!IsDraftWestinBallroomFamily(venue, room)) return;

        var areas = ParseProjectorPlacementToAllowedAreas(baseAv.ProjectorPlacement, room);
        if (areas.Count == 0) return;

        PersistProjectorAreaSelection(threadId, areas);
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

    /// <summary>
    /// After timeout, client disconnect, or unexpected errors, either attach a success message if a quote file exists
    /// or a "wait and refresh" assistant line. Ensures SendPartial sees an assistant delta so we do not append the generic
    /// "temporary issue" fallback on top of a quote attempt.
    /// </summary>
    private async Task RecoverQuoteFromDiskOrAnnounceWaitAsync(
        List<DisplayMessage> msgList,
        string bookingNo,
        string quotesDir,
        CancellationToken ct,
        string logContext)
    {
        var quoteFile = Directory.Exists(quotesDir)
            ? Directory.GetFiles(quotesDir, $"Quote-{bookingNo}-*.html")
                .OrderByDescending(f => System.IO.File.GetCreationTimeUtc(f))
                .FirstOrDefault()
            : null;

        if (!string.IsNullOrEmpty(quoteFile))
        {
            var quoteUrl = $"/files/quotes/{Path.GetFileName(quoteFile)}";
            HttpContext.Session.SetString("Draft:QuoteUrl", quoteUrl);
            HttpContext.Session.SetString("Draft:QuoteComplete", "1");
            HttpContext.Session.SetString("Draft:QuoteTimestamp", DateTime.UtcNow.ToString("O"));
            _logger.LogInformation("[{LogContext}] Recovered quote on disk for booking {BookingNo}: {QuoteUrl}", logContext, bookingNo, quoteUrl);
            await AddAssistantMessageAndPersistAsync(msgList, BuildQuoteReadyMessage(bookingNo, quoteUrl), ct);
            AppendQuoteReviewPromptImmediately(msgList);
        }
        else
        {
            _logger.LogWarning("[{LogContext}] No quote file on disk yet for booking {BookingNo}", logContext, bookingNo);
            var waitMessage = new DisplayMessage
            {
                Role = "assistant",
                Timestamp = DateTimeOffset.UtcNow,
                Parts = new List<string> {
                    $"Your quote for booking {bookingNo} is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance."
                },
                FullText = $"Your quote for booking {bookingNo} is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance.",
                Html = $"<p>Your quote for booking <strong>{bookingNo}</strong> is being generated. Please wait a moment and refresh the page, or contact our team if you need immediate assistance.</p>"
            };
            await AddAssistantMessageAndPersistAsync(msgList, waitMessage, ct);
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

    /// <summary>
    /// When the AI agent tool loop generates a quote, its text response lacks the {"ui":...} JSON
    /// that _Messages.cshtml needs to render the Quote Ready card. This method finds such messages
    /// and replaces them with the proper BuildQuoteReadyMessage that includes the UI card JSON.
    /// </summary>
    private void EnsureQuoteReadyCardInMessages(List<DisplayMessage> messages)
    {
        try
        {
            if (messages == null || messages.Count == 0)
                return;

            var quoteComplete = HttpContext.Session.GetString("Draft:QuoteComplete") == "1";
            if (!quoteComplete)
                return;

            var quoteUrl = HttpContext.Session.GetString("Draft:QuoteUrl");
            var bookingNo = HttpContext.Session.GetString("Draft:BookingNo");

            if (string.IsNullOrWhiteSpace(quoteUrl) || string.IsNullOrWhiteSpace(bookingNo))
                return;

            for (int i = 0; i < messages.Count; i++)
            {
                var m = messages[i];
                if (m == null || !IsAssistantMessageRole(m.Role))
                    continue;

                var raw = m.FullText ?? string.Join("\n\n", m.Parts ?? Enumerable.Empty<string>());
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var rawLower = raw.ToLowerInvariant();
                var isQuoteSuccessText = rawLower.Contains("successfully generated your quote")
                    && rawLower.Contains(bookingNo.ToLowerInvariant());
                var isQuotePendingText = rawLower.Contains("being finali")
                    && rawLower.Contains(bookingNo.ToLowerInvariant());

                if (!isQuoteSuccessText && !isQuotePendingText)
                    continue;

                // Already has the UI card JSON — leave it alone
                var partsRaw = string.Join("\n", m.Parts ?? Enumerable.Empty<string>());
                if (partsRaw.Contains("\"quoteUrl\"", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Replace plain text / pending text with proper card message
                _logger.LogInformation("[QUOTE CARD] Replacing plain-text quote message with Quote Ready card for booking {BookingNo}", bookingNo);
                messages[i] = BuildQuoteReadyMessage(bookingNo, quoteUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[QUOTE CARD] EnsureQuoteReadyCardInMessages failed (non-fatal)");
        }
    }

    private static DisplayMessage BuildPostQuoteReviewPromptMessage()
    {
        const string promptText = "Would you like to proceed and accept this quote?\n\nSelecting \"Accept Quote\" will notify Microhire to finalize the details.";
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
        var text = $"Great news! I have successfully generated your quote for booking {bookingNo}.";
        var uiJson = JsonSerializer.Serialize(new
        {
            ui = new { quoteUrl, bookingNo, isHtml = true }
        });
        var combined = text + "\n\n" + uiJson;
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { combined },
            FullText = text,
            Html = text
        };
    }

    private static string BuildSignedQuoteActionsHtml(string quoteUrl, string bookingNo)
    {
        var safeUrl = System.Net.WebUtility.HtmlEncode(quoteUrl ?? "");
        var safeRef = System.Net.WebUtility.HtmlEncode(bookingNo ?? "");
        var refHtml = string.IsNullOrWhiteSpace(bookingNo)
            ? ""
            : $"<p class=\"isla-quote-ref\">Quote Number: {safeRef}</p>";

        // Matches the Quote Ready card in _Messages.cshtml (isla-quote-ready + icon + CTA with external-link SVG).
        return $"""
<div class="isla-quote-ready">
<div class="isla-quote-icon">
<svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
<polyline points="14 2 14 8 20 8"></polyline>
<line x1="16" y1="13" x2="8" y2="13"></line>
<line x1="16" y1="17" x2="8" y2="17"></line>
<polyline points="10 9 9 9 8 9"></polyline>
</svg>
</div>
<div class="isla-quote-title">Signed Quote Ready!</div>
<p class="isla-quote-message">Your signed quote is ready to view.</p>
{refHtml}
<a href="{safeUrl}" target="_blank" rel="noopener noreferrer" class="isla-quote-download-btn isla-quote-open" data-quote-open="1">
<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
<path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
<polyline points="15 3 21 3 21 9"></polyline>
<line x1="10" y1="14" x2="21" y2="3"></line>
</svg>
View Signed Quote
</a>
</div>
""";
    }

    private sealed class ContactFormSubmission
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Organisation { get; set; } = "";
        public string Location { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
    }

    private sealed class EventFormSubmission
    {
        public string Venue { get; set; } = "";
        public string EventType { get; set; } = "";
        public string SetupStyle { get; set; } = "";
        public int Attendees { get; set; }
        public string Date { get; set; } = "";
        public string SetupTime { get; set; } = "";
        public string RehearsalTime { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string PackupTime { get; set; } = "";
    }

    private sealed class AvExtrasFormSubmission
    {
        public int Presenters { get; set; }
        public int Speakers { get; set; }
        public string Clicker { get; set; } = "no";
        public string Recording { get; set; } = "no";
        public string TechStart { get; set; } = "";
        public string TechEnd { get; set; } = "";
        public string TechWholeEvent { get; set; } = "no";
    }

    private sealed class VenueConfirmFormSubmission
    {
        public string VenueField { get; set; } = "";
        /// <summary>Westin room slug from the venue-confirm dropdown (client sends <c>room=</c> in the payload).</summary>
        public string RoomSlug { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public int Attendees { get; set; }
    }

    private sealed class EventDetailsFormSubmission
    {
        public string EventType { get; set; } = "";
        public string SetupStyle { get; set; } = "";
        public string SetupTime { get; set; } = "";
        public string RehearsalTime { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string PackupTime { get; set; } = "";
        public string WantsRehearsalOperator { get; set; } = "no";
        public string WantsOperator { get; set; } = "no";
    }

    private sealed class BaseAvFormSubmission
    {
        public bool BuiltInProjector { get; set; } = true;
        public bool BuiltInScreen { get; set; } = true;
        public bool BuiltInSpeakers { get; set; } = true;
        public string ProjectorPlacement { get; set; } = "";
        public int Presenters { get; set; }
        public string Flipchart { get; set; } = "no";
        public string LaptopMode { get; set; } = "none";
        public int LaptopQty { get; set; }
        public string AdapterOwnLaptops { get; set; } = "no";
    }

    private sealed class FollowUpAvFormSubmission
    {
        public string MicType { get; set; } = "";
        public int MicQty { get; set; }
        public string Lectern { get; set; } = "";
        public string FoldbackMonitor { get; set; } = "no";
        public string WirelessPresenter { get; set; } = "no";
        public string LaptopSwitcher { get; set; } = "no";
        public string StageLaptop { get; set; } = "no";
        public string VideoConference { get; set; } = "no";
    }

    private static bool TryCaptureEmailFormSubmission(string text, out string normalizedEmail)
    {
        normalizedEmail = "";
        var m = EmailFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        var raw = GetDecodedValue(data, "email");
        return TryValidateEmailFormat(raw, out normalizedEmail);
    }

    private static bool TryCaptureVenueConfirmFormSubmission(string text, out VenueConfirmFormSubmission submission, out string userMessage)
    {
        submission = new VenueConfirmFormSubmission();
        userMessage = "";
        var m = VenueConfirmFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.VenueField = GetDecodedValue(data, "venue");
        submission.RoomSlug = GetDecodedValue(data, "room");
        submission.StartDate = GetDecodedValue(data, "startDate");
        submission.EndDate = GetDecodedValue(data, "endDate");
        userMessage = GetDecodedValue(data, "userMessage");
        if (!int.TryParse(GetDecodedValue(data, "attendees"), out var att) || att < 1)
            return false;
        submission.Attendees = att;
        return !string.IsNullOrWhiteSpace(submission.VenueField)
               && !string.IsNullOrWhiteSpace(submission.StartDate);
    }

    private static bool TryCaptureEventDetailsFormSubmission(string text, out EventDetailsFormSubmission submission)
    {
        submission = new EventDetailsFormSubmission();
        var m = EventDetailsFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.EventType = GetDecodedValue(data, "eventType");
        submission.SetupStyle = GetDecodedValue(data, "setupStyle");
        submission.SetupTime = GetDecodedValue(data, "setup");
        submission.RehearsalTime = GetDecodedValue(data, "rehearsal");
        submission.StartTime = GetDecodedValue(data, "start");
        submission.EndTime = GetDecodedValue(data, "end");
        submission.PackupTime = GetDecodedValue(data, "packup");
        if (string.IsNullOrWhiteSpace(submission.PackupTime))
            submission.PackupTime = submission.EndTime;
        submission.WantsRehearsalOperator = GetDecodedValue(data, "wantsRehearsalOperator");
        submission.WantsOperator = GetDecodedValue(data, "wantsOperator");
        return !string.IsNullOrWhiteSpace(submission.EventType)
               && !string.IsNullOrWhiteSpace(submission.SetupTime)
               && !string.IsNullOrWhiteSpace(submission.RehearsalTime)
               && !string.IsNullOrWhiteSpace(submission.StartTime)
               && !string.IsNullOrWhiteSpace(submission.EndTime);
    }

    private static bool TryCaptureBaseAvFormSubmission(string text, out BaseAvFormSubmission submission, out string userMessage)
    {
        submission = new BaseAvFormSubmission();
        userMessage = "";
        var m = BaseAvFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        userMessage = GetDecodedValue(data, "userMessage");
        submission.BuiltInProjector = string.Equals(GetDecodedValue(data, "builtInProjector"), "yes", StringComparison.OrdinalIgnoreCase);
        submission.BuiltInScreen = string.Equals(GetDecodedValue(data, "builtInScreen"), "yes", StringComparison.OrdinalIgnoreCase);
        submission.BuiltInSpeakers = string.Equals(GetDecodedValue(data, "builtInSpeakers"), "yes", StringComparison.OrdinalIgnoreCase);
        submission.ProjectorPlacement = GetDecodedValue(data, "projectorPlacement");
        if (int.TryParse(GetDecodedValue(data, "presenters"), out var p))
            submission.Presenters = Math.Max(0, p);
        submission.Flipchart = GetDecodedValue(data, "flipchart");
        submission.LaptopMode = GetDecodedValue(data, "laptopMode");
        if (int.TryParse(GetDecodedValue(data, "laptopQty"), out var lq))
            submission.LaptopQty = Math.Max(0, lq);
        submission.AdapterOwnLaptops = GetDecodedValue(data, "adapterOwnLaptops");
        return true;
    }

    private static bool TryCaptureFollowUpAvFormSubmission(string text, out FollowUpAvFormSubmission submission)
    {
        submission = new FollowUpAvFormSubmission();
        var m = FollowUpAvFormRe.Match(text ?? "");
        if (!m.Success) return false;
        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.MicType = GetDecodedValue(data, "micType");
        if (int.TryParse(GetDecodedValue(data, "micQty"), out var mq))
            submission.MicQty = Math.Max(0, mq);
        submission.Lectern = GetDecodedValue(data, "lectern");
        submission.FoldbackMonitor = GetDecodedValue(data, "foldbackMonitor");
        submission.WirelessPresenter = GetDecodedValue(data, "wirelessPresenter");
        submission.LaptopSwitcher = GetDecodedValue(data, "laptopSwitcher");
        submission.StageLaptop = GetDecodedValue(data, "stageLaptop");
        submission.VideoConference = GetDecodedValue(data, "videoConference");
        return true;
    }

    private static bool TryCaptureContactFormSubmission(string text, out ContactFormSubmission submission)
    {
        submission = new ContactFormSubmission();
        var m = ContactFormRe.Match(text ?? "");
        if (!m.Success) return false;

        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.FirstName = GetDecodedValue(data, "firstName");
        submission.LastName = GetDecodedValue(data, "lastName");
        submission.Organisation = GetDecodedValue(data, "organisation");
        submission.Location = GetDecodedValue(data, "location");
        submission.Email = GetDecodedValue(data, "email");
        submission.Phone = GetDecodedValue(data, "phone");

        return !string.IsNullOrWhiteSpace(submission.FirstName)
               && !string.IsNullOrWhiteSpace(submission.LastName)
               && !string.IsNullOrWhiteSpace(submission.Organisation)
               && !string.IsNullOrWhiteSpace(submission.Location)
               && (!string.IsNullOrWhiteSpace(submission.Email) || !string.IsNullOrWhiteSpace(submission.Phone));
    }

    private static bool TryCaptureEventFormSubmission(string text, out EventFormSubmission submission)
    {
        submission = new EventFormSubmission();
        var m = EventFormRe.Match(text ?? "");
        if (!m.Success) return false;

        var data = ParseKeyValueBlob(m.Groups[1].Value);
        submission.Venue = GetDecodedValue(data, "venue");
        submission.EventType = GetDecodedValue(data, "eventType");
        submission.SetupStyle = GetDecodedValue(data, "setupStyle");
        submission.Date = GetDecodedValue(data, "date");
        submission.SetupTime = GetDecodedValue(data, "setup");
        submission.RehearsalTime = GetDecodedValue(data, "rehearsal");
        submission.StartTime = GetDecodedValue(data, "start");
        submission.EndTime = GetDecodedValue(data, "end");
        submission.PackupTime = GetDecodedValue(data, "packup");
        if (string.IsNullOrWhiteSpace(submission.PackupTime))
            submission.PackupTime = submission.EndTime;
        var attendeesText = GetDecodedValue(data, "attendees");
        if (!int.TryParse(attendeesText, out var attendees))
        {
            attendees = 0;
        }
        submission.Attendees = attendees;

        return !string.IsNullOrWhiteSpace(submission.Venue)
               && !string.IsNullOrWhiteSpace(submission.EventType)
               && submission.Attendees > 0
               && !string.IsNullOrWhiteSpace(submission.Date);
    }

    private static bool TryCaptureAvExtrasFormSubmission(string text, out AvExtrasFormSubmission submission)
    {
        submission = new AvExtrasFormSubmission();
        var m = AvExtrasFormRe.Match(text ?? "");
        if (!m.Success) return false;

        var data = ParseKeyValueBlob(m.Groups[1].Value);
        if (int.TryParse(GetDecodedValue(data, "presenters"), out var presenters))
            submission.Presenters = Math.Max(0, presenters);
        if (int.TryParse(GetDecodedValue(data, "speakers"), out var speakers))
            submission.Speakers = Math.Max(0, speakers);
        submission.Clicker = GetDecodedValue(data, "clicker");
        submission.Recording = GetDecodedValue(data, "recording");
        submission.TechStart = GetDecodedValue(data, "techStart");
        submission.TechEnd = GetDecodedValue(data, "techEnd");
        submission.TechWholeEvent = GetDecodedValue(data, "techWholeEvent");

        return true;
    }

    private void SaveContactFormToSession(ContactFormSubmission submission)
    {
        var wasManual = HttpContext.Session.GetString("Draft:NeedManualContact") == "1";
        var fullName = $"{submission.FirstName} {submission.LastName}".Trim();
        HttpContext.Session.SetString("Draft:ContactName", fullName);
        HttpContext.Session.SetString("Draft:ContactFirstName", submission.FirstName);
        HttpContext.Session.SetString("Draft:ContactLastName", submission.LastName);
        HttpContext.Session.SetString("Draft:Organisation", submission.Organisation);
        HttpContext.Session.SetString("Draft:OrganisationAddress", submission.Location);
        HttpContext.Session.SetString("Draft:ContactFormSubmitted", "1");
        if (!string.IsNullOrWhiteSpace(submission.Email))
            HttpContext.Session.SetString("Draft:ContactEmail", submission.Email);
        if (!string.IsNullOrWhiteSpace(submission.Phone))
            HttpContext.Session.SetString("Draft:ContactPhone", submission.Phone);
        HttpContext.Session.Remove("Draft:NeedManualContact");
        if (wasManual)
            HttpContext.Session.SetString("Draft:ShowContactSummary", "1");
    }

    private void SaveEventFormToSession(EventFormSubmission submission)
    {
        var (venueName, roomName) = ResolveVenueAndRoom(submission.Venue);
        if (!string.IsNullOrWhiteSpace(venueName))
            HttpContext.Session.SetString("Draft:VenueName", venueName);
        if (!string.IsNullOrWhiteSpace(roomName))
            HttpContext.Session.SetString("Draft:RoomName", roomName);

        HttpContext.Session.SetString("Draft:EventType", submission.EventType);
        HttpContext.Session.SetString("Draft:ExpectedAttendees", submission.Attendees.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:EventFormSubmitted", "1");
        if (!string.IsNullOrWhiteSpace(submission.SetupStyle))
            HttpContext.Session.SetString("Draft:SetupStyle", submission.SetupStyle);

        var eventDateNormalized = NormalizeToIsoDateOrEmpty(submission.Date);
        if (DateTime.TryParse(eventDateNormalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate)
            && TimeSpan.TryParse(submission.SetupTime, CultureInfo.InvariantCulture, out var setup)
            && TimeSpan.TryParse(submission.RehearsalTime, CultureInfo.InvariantCulture, out var rehearsal)
            && TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endForPackup))
        {
            TimeSpan? start = TimeSpan.TryParse(submission.StartTime, CultureInfo.InvariantCulture, out var startTs) ? startTs : null;
            TimeSpan? end = TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endTs) ? endTs : null;
            var packup = endForPackup;
            SaveScheduleToSession(setup, rehearsal, start, end, packup, eventDate);
        }
    }

    private void SaveAvExtrasToSession(AvExtrasFormSubmission submission)
    {
        HttpContext.Session.SetString("Draft:PresenterCount", submission.Presenters.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:SpeakerCount", submission.Speakers.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:NeedsClicker", submission.Clicker);
        HttpContext.Session.SetString("Draft:NeedsRecording", submission.Recording);
        HttpContext.Session.SetString("Draft:TechStartTime", submission.TechStart);
        HttpContext.Session.SetString("Draft:TechEndTime", submission.TechEnd);
        HttpContext.Session.SetString("Draft:TechWholeEvent", submission.TechWholeEvent);
        HttpContext.Session.SetString("Draft:AvExtrasSubmitted", "1");
    }

    private void SaveVenueConfirmToSession(VenueConfirmFormSubmission submission)
    {
        var (venueName, roomName) = ResolveVenueAndRoom(submission.VenueField);
        if (!string.IsNullOrWhiteSpace(venueName))
            HttpContext.Session.SetString("Draft:VenueName", venueName);

        var roomOptions = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var roomFromSlug = ResolveRoomDisplayNameFromVenueConfirmRoomToken(submission.RoomSlug, roomOptions);
        if (!string.IsNullOrWhiteSpace(roomFromSlug))
            HttpContext.Session.SetString("Draft:RoomName", roomFromSlug);
        else if (!string.IsNullOrWhiteSpace(roomName))
            HttpContext.Session.SetString("Draft:RoomName", roomName);
        else
            HttpContext.Session.Remove("Draft:RoomName");

        var startNorm = NormalizeToIsoDateOrEmpty(submission.StartDate);
        var endNorm = string.IsNullOrWhiteSpace(submission.EndDate) ? startNorm : NormalizeToIsoDateOrEmpty(submission.EndDate);
        HttpContext.Session.SetString("Draft:EventDate", startNorm);
        HttpContext.Session.SetString("Draft:EventEndDate", endNorm);
        HttpContext.Session.SetString("Draft:ExpectedAttendees", submission.Attendees.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:VenueConfirmSubmitted", "1");
    }

    /// <summary>
    /// Maps free-text event type to a canonical Westin setup for capacity and equipment (UI no longer asks explicitly).
    /// </summary>
    private static string InferSetupStyleFromEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return "Theatre";
        var t = eventType.ToLowerInvariant();

        static bool Has(string s, params string[] needles)
        {
            foreach (var n in needles)
                if (s.Contains(n, StringComparison.Ordinal)) return true;
            return false;
        }

        // Boardroom / small formal meetings
        if (Has(t, "board meeting", "boardroom", "board room", "executive meeting", "board retreat"))
            return "Boardroom";

        // Classroom-style / collaborative learning
        if (Has(t, "hackathon", "workshop", "training", "coding", "bootcamp", "classroom", "school", "course", "tutorial", "seminar", "lecture"))
            return "Classroom";

        // Seated dining / awards
        if (Has(t, "banquet", "gala dinner", "awards dinner", "seated dinner", "wedding breakfast", "wedding reception"))
            return "Banquet";

        // Standing / mingling
        if (Has(t, "cocktail", "networking", "drinks reception", "reception only", "mixer"))
            return "Cocktail";

        // Collaborative table layout
        if (Has(t, "u-shape", "u shape", "hollow square", "breakout"))
            return "U-Shape";

        // Presentations, showcases, keynotes
        if (Has(t, "showcase", "product launch", "product demo", "demo day", "keynote", "pitch", "theatre", "theater",
                "conference", "presentation", "town hall", "all-hands", "all hands", "webinar", "annual general", "agm"))
            return "Theatre";

        return "Theatre";
    }

    private void SaveEventDetailsToSession(EventDetailsFormSubmission submission)
    {
        HttpContext.Session.SetString("Draft:EventType", submission.EventType);
        if (!string.IsNullOrWhiteSpace(submission.SetupStyle))
            HttpContext.Session.SetString("Draft:SetupStyle", submission.SetupStyle);
        else
        {
            var room = HttpContext.Session.GetString("Draft:RoomName") ?? "";
            if (room.Contains("Thrive", StringComparison.OrdinalIgnoreCase))
                HttpContext.Session.SetString("Draft:SetupStyle", "Boardroom");
            else
                HttpContext.Session.SetString("Draft:SetupStyle", InferSetupStyleFromEventType(submission.EventType));
        }

        submission.SetupStyle = HttpContext.Session.GetString("Draft:SetupStyle") ?? submission.SetupStyle;
        HttpContext.Session.SetString("Draft:WantsRehearsalOperator", submission.WantsRehearsalOperator);
        HttpContext.Session.SetString("Draft:RehearsalOperator", submission.WantsRehearsalOperator);
        HttpContext.Session.SetString("Draft:WantsOperator", submission.WantsOperator);

        var dateStr = HttpContext.Session.GetString("Draft:EventDate") ?? "";
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate)
            && TimeSpan.TryParse(submission.SetupTime, CultureInfo.InvariantCulture, out var setup)
            && TimeSpan.TryParse(submission.RehearsalTime, CultureInfo.InvariantCulture, out var rehearsal)
            && TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endForPackup))
        {
            TimeSpan? start = TimeSpan.TryParse(submission.StartTime, CultureInfo.InvariantCulture, out var startTs) ? startTs : null;
            TimeSpan? end = TimeSpan.TryParse(submission.EndTime, CultureInfo.InvariantCulture, out var endTs) ? endTs : null;
            var packup = endForPackup;
            SaveScheduleToSession(setup, rehearsal, start, end, packup, eventDate);
        }

        HttpContext.Session.SetString("Draft:EventFormSubmitted", "1");
    }

    private void SaveBaseAvToSession(BaseAvFormSubmission s)
    {
        HttpContext.Session.SetString("Draft:BuiltInProjector", s.BuiltInProjector ? "yes" : "no");
        HttpContext.Session.SetString("Draft:BuiltInScreen", s.BuiltInScreen ? "yes" : "no");
        HttpContext.Session.SetString("Draft:BuiltInSpeakers", s.BuiltInSpeakers ? "yes" : "no");

        // Detect combined "Inbuilt projector and screen" checkbox (e.g. Elevate rooms).
        // When this combined checkbox is unchecked, the user means "no projection at all",
        // not "I want external projection equipment".
        var venue = HttpContext.Session.GetString("Draft:VenueName");
        var room = HttpContext.Session.GetString("Draft:RoomName");
        var baseLabels = VenueRoomPackagesCache.TryGetBaseEquipmentLabels(_env, venue, room);
        var hasCombinedPS = baseLabels.Any(l =>
            l.Contains("projector", StringComparison.OrdinalIgnoreCase) &&
            l.Contains("screen", StringComparison.OrdinalIgnoreCase));
        HttpContext.Session.SetString("Draft:CombinedProjectorScreen", hasCombinedPS ? "1" : "0");

        HttpContext.Session.SetString("Draft:ProjectorPlacementChoice", s.ProjectorPlacement);

        // Ballroom: when placement is "none", user declined projection — override projector/screen flags.
        if (string.Equals(s.ProjectorPlacement?.Trim(), "none", StringComparison.OrdinalIgnoreCase)
            && IsDraftWestinBallroomFamily(venue, room))
        {
            HttpContext.Session.SetString("Draft:BuiltInProjector", "no");
            HttpContext.Session.SetString("Draft:BuiltInScreen", "no");
        }

        HttpContext.Session.SetString("Draft:PresenterCount", s.Presenters.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:SpeakerCount", "0");
        HttpContext.Session.SetString("Draft:Flipchart", s.Flipchart);
        HttpContext.Session.SetString("Draft:LaptopMode", s.LaptopMode);
        HttpContext.Session.SetString("Draft:LaptopQty", s.LaptopQty.ToString(CultureInfo.InvariantCulture));
        HttpContext.Session.SetString("Draft:AdapterOwnLaptops", s.AdapterOwnLaptops);
        HttpContext.Session.SetString("Draft:NeedsClicker", "no");
        HttpContext.Session.SetString("Draft:NeedsRecording", "no");
        var ts = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00";
        var te = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00";
        HttpContext.Session.SetString("Draft:TechStartTime", ts);
        HttpContext.Session.SetString("Draft:TechEndTime", te);
        HttpContext.Session.SetString("Draft:TechWholeEvent", "yes");
        HttpContext.Session.SetString("Draft:BaseAvSubmitted", "1");
        HttpContext.Session.SetString("Draft:BaseAvSubmittedForThread", HttpContext.Session.GetString("AgentThreadId") ?? "");

        var roomForPlacement = HttpContext.Session.GetString("Draft:RoomName");
        var placementAreas = ParseProjectorPlacementToAllowedAreas(s.ProjectorPlacement, roomForPlacement);
        var threadId = HttpContext.Session.GetString("AgentThreadId") ?? "";
        if (placementAreas.Count > 0)
        {
            HttpContext.Session.SetString("Draft:ProjectorAreas", string.Join(",", placementAreas));
            HttpContext.Session.SetString("Draft:ProjectorArea", placementAreas[0]);
            // Mark projector area as captured so stale-session guards in both
            // AgentToolHandlerService and TryFollowUpAvQuotePipelineAsync trust these values.
            HttpContext.Session.SetString(ProjectorAreaCapturedKey, "1");
            HttpContext.Session.SetString(ProjectorAreaThreadIdKey, threadId);
            HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
            HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
        }
        else if (!string.IsNullOrWhiteSpace(s.ProjectorPlacement))
        {
            HttpContext.Session.SetString("Draft:ProjectorArea", s.ProjectorPlacement.Trim());
            HttpContext.Session.SetString(ProjectorAreaCapturedKey, "1");
            HttpContext.Session.SetString(ProjectorAreaThreadIdKey, threadId);
            HttpContext.Session.SetString(ProjectorPromptShownKey, "1");
            HttpContext.Session.SetString(ProjectorPromptThreadIdKey, threadId);
        }

        var mode = (s.LaptopMode ?? "").ToLowerInvariant();
        if (mode is "windows" or "mac")
        {
            HttpContext.Session.SetString("Draft:LaptopOwnershipAnswered", "1");
            HttpContext.Session.SetString("Draft:NeedsProvidedLaptop", "yes");
            HttpContext.Session.SetString("Draft:LaptopPreference", mode);
        }
        else
        {
            HttpContext.Session.SetString("Draft:LaptopOwnershipAnswered", "1");
            HttpContext.Session.SetString("Draft:NeedsProvidedLaptop", "no");
            HttpContext.Session.Remove("Draft:LaptopPreference");
        }
    }

    private void SaveFollowUpAvToSession(FollowUpAvFormSubmission s)
    {
        var presenterCount = 0;
        _ = int.TryParse(HttpContext.Session.GetString("Draft:PresenterCount"), out presenterCount);

        if (presenterCount <= 0)
        {
            HttpContext.Session.SetString("Draft:MicType", "");
            HttpContext.Session.SetString("Draft:MicQty", "0");
            HttpContext.Session.SetString("Draft:Lectern", "none");
            HttpContext.Session.SetString("Draft:FoldbackMonitor", "no");
            HttpContext.Session.SetString("Draft:WirelessPresenter", "no");
        }
        else
        {
            HttpContext.Session.SetString("Draft:MicType", s.MicType);
            HttpContext.Session.SetString("Draft:MicQty", s.MicQty.ToString(CultureInfo.InvariantCulture));
            HttpContext.Session.SetString("Draft:Lectern", s.Lectern);
            HttpContext.Session.SetString("Draft:FoldbackMonitor", s.FoldbackMonitor);
            HttpContext.Session.SetString("Draft:WirelessPresenter", s.WirelessPresenter);
        }

        HttpContext.Session.SetString("Draft:LaptopSwitcher", s.LaptopSwitcher);

        var lecternForStage = presenterCount <= 0 ? "none" : (s.Lectern ?? "").Trim();
        var lecternNotNone = lecternForStage.Length > 0
            && !string.Equals(lecternForStage, "none", StringComparison.OrdinalIgnoreCase);
        var stageQuestionApplicable = string.Equals(s.LaptopSwitcher, "yes", StringComparison.OrdinalIgnoreCase)
            && lecternNotNone;
        var stageLaptop = stageQuestionApplicable && string.Equals(s.StageLaptop, "yes", StringComparison.OrdinalIgnoreCase)
            ? "yes"
            : "no";
        s.StageLaptop = stageLaptop;
        HttpContext.Session.SetString("Draft:StageLaptop", stageLaptop);
        HttpContext.Session.SetString("Draft:VideoConference", s.VideoConference);
        HttpContext.Session.SetString("Draft:FollowUpAvSubmitted", "1");
        HttpContext.Session.SetString("Draft:AvExtrasSubmitted", "1");

        if (string.Equals(stageLaptop, "yes", StringComparison.OrdinalIgnoreCase))
            HttpContext.Session.SetString("Draft:NeedsSdiCross", "2");
        else
            HttpContext.Session.Remove("Draft:NeedsSdiCross");
    }

    private static Dictionary<string, string> ParseKeyValueBlob(string blob)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(blob)) return map;

        foreach (var segment in blob.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            map[parts[0]] = parts[1];
        }

        return map;
    }

    private static string GetDecodedValue(Dictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return "";
        try
        {
            return Uri.UnescapeDataString(raw).Trim();
        }
        catch
        {
            return raw.Trim();
        }
    }

    private static (string venueName, string roomName) ResolveVenueAndRoom(string venueField)
    {
        var value = (venueField ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return ("", "");

        var lower = value.ToLowerInvariant();
        if (lower.Contains("westin ballroom 1", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Westin Ballroom 1");
        if (lower.Contains("westin ballroom 2", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Westin Ballroom 2");
        if (lower.Contains("westin ballroom full", StringComparison.Ordinal) || lower.Equals("westin ballroom", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Westin Ballroom");
        if (lower.Contains("elevate 1", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Elevate 1");
        if (lower.Contains("elevate 2", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Elevate 2");
        if (lower.Contains("elevate full", StringComparison.Ordinal) || lower.Equals("elevate", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Elevate");
        if (lower.Contains("thrive", StringComparison.Ordinal))
            return ("The Westin Brisbane", "Thrive Boardroom");

        // Venue dropdown label only (no specific room) — do not store the venue name as Draft:RoomName.
        if (string.Equals(value, "Westin Brisbane", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "The Westin Brisbane", StringComparison.OrdinalIgnoreCase))
            return ("The Westin Brisbane", "");

        return ("The Westin Brisbane", value);
    }

    /// <summary>
    /// Maps the client venue-confirm <c>room=</c> token (slug or display name) to the canonical room name used in session.
    /// </summary>
    private static string? ResolveRoomDisplayNameFromVenueConfirmRoomToken(
        string? roomToken,
        IReadOnlyList<(string Slug, string Name)> options)
    {
        var t = (roomToken ?? "").Trim();
        if (string.IsNullOrEmpty(t))
            return null;

        foreach (var (slug, name) in options)
        {
            if (string.Equals(slug, t, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        foreach (var (slug, name) in options)
        {
            if (string.Equals(name, t, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        var matchedSlug = WestinRoomCatalog.MatchDraftRoomNameToSlug(t, options);
        if (matchedSlug == null)
            return null;

        foreach (var (slug, name) in options)
        {
            if (string.Equals(slug, matchedSlug, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return null;
    }

    /// <summary>
    /// True when session <c>Draft:RoomName</c> matches a quotable Westin venue-confirm room (not the venue label alone).
    /// </summary>
    private bool HasQuotableWestinRoomInDraft()
    {
        var room = (HttpContext.Session.GetString("Draft:RoomName") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(room))
            return false;
        var options = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        return WestinRoomCatalog.MatchDraftRoomNameToSlug(room, options) != null;
    }

    private static string NormalizeToIsoDateOrEmpty(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var t = raw.Trim().Replace('/', '-');
        var formats = new[] { "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd-M-yyyy", "d-MM-yyyy" };
        if (DateOnly.TryParseExact(t, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw.Trim();
    }

    private bool IsLeadEntry() =>
        string.Equals(HttpContext.Session.GetString("Draft:EntrySource"), "lead", StringComparison.OrdinalIgnoreCase);

    private static bool TryValidateEmailFormat(string email, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new MailAddress(email.Trim());
            normalized = addr.Address.ToLowerInvariant();
            return normalized.Contains('@', StringComparison.Ordinal) && normalized.Length <= 254;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyBookingPrefillToSession(BookingPrefillFromEmailResult lookup)
    {
        var c = lookup.Contact;
        var name = (c.Contactname ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"{c.Firstname ?? ""} {c.Surname ?? ""}".Trim();
        if (!string.IsNullOrWhiteSpace(name))
            HttpContext.Session.SetString("Draft:ContactName", name);

        if (!string.IsNullOrWhiteSpace(c.Email))
            HttpContext.Session.SetString("Draft:ContactEmail", c.Email.Trim().ToLowerInvariant());

        var b = lookup.Booking!;
        if (!string.IsNullOrWhiteSpace(b.OrganizationV6))
            HttpContext.Session.SetString("Draft:Organisation", b.OrganizationV6.Trim());
        else if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:Organisation")))
            HttpContext.Session.SetString("Draft:Organisation", "Event");

        if (!string.IsNullOrWhiteSpace(b.booking_no))
            HttpContext.Session.SetString("Draft:BookingNo", b.booking_no);

        var venueName = (lookup.VenueDisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(venueName))
            venueName = "The Westin Brisbane";
        HttpContext.Session.SetString("Draft:VenueName", venueName);

        var room = (b.VenueRoom ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(room))
            HttpContext.Session.SetString("Draft:RoomName", room);

        var startDay = b.ShowSDate ?? b.dDate ?? b.SDate;
        if (startDay.HasValue)
            HttpContext.Session.SetString("Draft:EventDate", startDay.Value.ToString("yyyy-MM-dd"));

        var endDay = b.ShowEdate ?? b.rDate;
        if (endDay.HasValue)
            HttpContext.Session.SetString("Draft:EventEndDate", endDay.Value.ToString("yyyy-MM-dd"));
        else if (startDay.HasValue)
            HttpContext.Session.SetString("Draft:EventEndDate", startDay.Value.ToString("yyyy-MM-dd"));

        if (b.expAttendees is > 0)
        {
            var exp = b.expAttendees.Value.ToString(CultureInfo.InvariantCulture);
            HttpContext.Session.SetString("Draft:ExpectedAttendees", exp);
            HttpContext.Session.SetString("Draft:LeadSeededExpectedAttendees", exp);
        }

        HttpContext.Session.SetString("Draft:ContactFormSubmitted", "1");
        HttpContext.Session.Remove("Draft:NeedManualContact");
    }

    private void ApplyContactOnlyFromLookup(TblContact c)
    {
        var name = (c.Contactname ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"{c.Firstname ?? ""} {c.Surname ?? ""}".Trim();
        if (!string.IsNullOrWhiteSpace(name))
            HttpContext.Session.SetString("Draft:ContactName", name);
        if (!string.IsNullOrWhiteSpace(c.Email))
            HttpContext.Session.SetString("Draft:ContactEmail", c.Email.Trim().ToLowerInvariant());
    }

    private static string BuildEmailIntakeSyntheticMessage(string email, BookingPrefillFromEmailResult? lookup)
    {
        if (lookup?.Booking != null)
            return $"Structured intake: email {email}; booking lookup found proposal {lookup.Booking.booking_no}.";
        if (lookup != null)
            return $"Structured intake: email {email}; booking lookup: no upcoming booking — manual contact required.";
        return $"Structured intake: email {email}; contact not found in system — manual contact required.";
    }

    private static string BuildVenueConfirmSyntheticMessage(VenueConfirmFormSubmission s) =>
        $"Venue confirm: venue {s.VenueField}; start {s.StartDate}; end {s.EndDate}; attendees {s.Attendees}.";

    private static string BuildEventDetailsSyntheticMessage(EventDetailsFormSubmission s) =>
        $"Event details provided: event type {s.EventType}; setup style {s.SetupStyle}; " +
        $"rehearsal operator {s.WantsRehearsalOperator}; operator {s.WantsOperator}; schedule setup {s.SetupTime}, rehearsal {s.RehearsalTime}, " +
        $"start {s.StartTime}, end {s.EndTime}.";

    private static string BuildBaseAvSyntheticMessage(BaseAvFormSubmission s) =>
        $"Base AV provided: built-in projector {s.BuiltInProjector}; screen {s.BuiltInScreen}; speakers {s.BuiltInSpeakers}; " +
        $"placement {s.ProjectorPlacement}; presenters {s.Presenters}; flipchart {s.Flipchart}; " +
        $"laptop mode {s.LaptopMode}; laptop qty {s.LaptopQty}; adapter for own laptops {s.AdapterOwnLaptops}.";

    private string BuildFollowUpAvSyntheticMessage(FollowUpAvFormSubmission s)
    {
        var pr = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0";
        return
            $"AV extras provided: presenters {pr}; speakers 0; wireless clicker no; audio video recording no; " +
            $"technician from {HttpContext.Session.GetString("Draft:TechStartTime")} to {HttpContext.Session.GetString("Draft:TechEndTime")}; " +
            $"whole event coverage yes. " +
            $"Follow-up: mic {s.MicType} qty {s.MicQty}; lectern {s.Lectern}; foldback {s.FoldbackMonitor}; " +
            $"wireless presenter {s.WirelessPresenter}; laptop switcher {s.LaptopSwitcher}; stage laptop {s.StageLaptop}; " +
            $"video conference {s.VideoConference}.";
    }

    /// <summary>Matches Azure SDK "Agent" and OpenAI-style "assistant" (see <see cref="AzureAgentChatService"/> transcript normalization).</summary>
    private static bool IsAssistantMessageRole(string? role) =>
        string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "agent", StringComparison.OrdinalIgnoreCase);

    private void EnsureStructuredFormsInChat(List<DisplayMessage> messages)
    {
        if (messages == null) return;
        var index = (messages.Count > 0 &&
                     IsAssistantMessageRole(messages[0].Role) &&
                     (messages[0].FullText ?? "").Contains("Hello, my name is Isla from Microhire", StringComparison.OrdinalIgnoreCase))
                     ? 1 : 0;

        var followUpAvSubmitted = HttpContext.Session.GetString("Draft:FollowUpAvSubmitted") == "1";

        // When the wizard is incomplete (quote not generated yet), strip any leftover
        // "Quote Ready" messages from a previous run so they don't render prematurely
        // (e.g. after Edit Quote resets the form flags but the Azure transcript still
        // contains the old quote-success message).
        if (HttpContext.Session.GetString("Draft:QuoteComplete") != "1")
        {
            messages.RemoveAll(m =>
            {
                if (!IsAssistantMessageRole(m.Role)) return false;
                var raw = string.Join("\n", (m.Parts ?? new List<string>()).Select(p => p ?? string.Empty))
                    .ToLowerInvariant();
                if (raw.Contains("\"quoteurl\"") || raw.Contains("\"quote_url\""))
                    return true;
                if (raw.Contains("generated your quote") && (raw.Contains("quote ready") || raw.Contains("view quote")))
                    return true;
                return false;
            });
        }

        var entrySource = HttpContext.Session.GetString("Draft:EntrySource") ?? "general";
        var emailGateComplete = HttpContext.Session.GetString("Draft:EmailGateCompleted") == "1";
        var needManualContact = HttpContext.Session.GetString("Draft:NeedManualContact") == "1";
        var contactFormSubmitted = HttpContext.Session.GetString("Draft:ContactFormSubmitted") == "1";
        var venueConfirmSubmitted = HttpContext.Session.GetString("Draft:VenueConfirmSubmitted") == "1";
        var eventFormSubmitted = HttpContext.Session.GetString("Draft:EventFormSubmitted") == "1";
        var baseAvSubmitted = HttpContext.Session.GetString("Draft:BaseAvSubmitted") == "1";

        // Lead links: after email verification, show venue wizard even if org/name are missing on the lead row.
        var hasContactDraft = contactFormSubmitted
            || (IsLeadEntry()
                && emailGateComplete
                && (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactEmail"))
                    || !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactPhone"))))
            || (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactName"))
                && !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:Organisation"))
                && (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactEmail"))
                    || !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:ContactPhone"))));

        var attendeesRaw = HttpContext.Session.GetString("Draft:ExpectedAttendees");
        _ = int.TryParse(attendeesRaw, out var attendees);
        var hasEventCoreDraft =
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:VenueName")) &&
            HasQuotableWestinRoomInDraft() &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EventType")) &&
            attendees > 0 &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EventDate")) &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:StartTime")) &&
            !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Draft:EndTime"));

        // 1) Email gate (direct visitors and sales-portal lead links — confirm email before continuing)
        if ((string.Equals(entrySource, "general", StringComparison.OrdinalIgnoreCase)
             || string.Equals(entrySource, "lead", StringComparison.OrdinalIgnoreCase))
            && !emailGateComplete)
        {
            messages.RemoveAll(IsLegacyContactPromptMessage);
            if (!messages.Any(m => MessageContainsUiType(m, "emailForm")))
            {
                var emailIntro = string.Equals(entrySource, "lead", StringComparison.OrdinalIgnoreCase)
                    ? "Please confirm the email address you used for your enquiry so we can verify it’s you."
                    : "Please enter your email address so I can verify your booking request.";
                messages.Insert(index++, BuildUiAssistantMessage(emailIntro, BuildEmailFormUiJson()));
            }
            return;
        }

        // 2) Manual contact when lookup did not return a booking
        if (needManualContact && !contactFormSubmitted)
        {
            messages.RemoveAll(IsLegacyContactPromptMessage);
            if (!messages.Any(m => MessageContainsUiType(m, "contactForm")))
            {
                messages.Insert(index++, BuildUiAssistantMessage("Please complete this quick contact form:", BuildContactFormUiJson()));
            }
            return;
        }

        // 3) Contact details on file
        if (!hasContactDraft)
        {
            messages.RemoveAll(IsLegacyContactPromptMessage);
            if (!messages.Any(m => MessageContainsUiType(m, "contactForm")))
            {
                messages.Insert(index++, BuildUiAssistantMessage("Please complete this quick contact form:", BuildContactFormUiJson()));
            }
            return;
        }
        else if (contactFormSubmitted && HttpContext.Session.GetString("Draft:ShowContactSummary") == "1")
        {
            messages.RemoveAll(m => MessageContainsUiType(m, "contactForm"));
            if (!messages.Any(m => MessageContainsUiType(m, "submittedForm") && (m.FullText ?? "").Contains("Contact details submitted")))
            {
                messages.Insert(index++, BuildUiAssistantMessage("Contact details submitted:", BuildSubmittedContactFormViewJson()));
            }
            else index++;
        }

        // 4) Venue + dates + attendees — always inject so submitted forms stay visible (view shows Confirmed + disabled).
        // While two-phase contact save is in flight ("One moment, please!"), do not inject the venue form —
        // it appeared above the follow-up "saved successfully" message and confused users.
        if (string.Equals(HttpContext.Session.GetString("ContactSavePending"), "1", StringComparison.Ordinal))
        {
            messages.RemoveAll(m => MessageContainsUiType(m, "venueConfirmForm"));
            return;
        }

        if (!messages.Any(m => MessageContainsUiType(m, "venueConfirmForm")))
        {
            var intro = HttpContext.Session.GetString("Draft:BookingLookupApplied") == "1"
                ? "I've found your booking details. Please review and confirm."
                : "Review your venue, room, and event dates in the form below.";
            messages.Insert(index++, BuildUiAssistantMessage(intro, BuildVenueConfirmFormUiJson()));
        }
        else index++;

        if (!venueConfirmSubmitted)
            return;

        // 5) Event type + schedule + operator — always (re-)inject when missing from transcript so session
        // values stay visible after thread resets; ViewData EventFormSubmitted drives Confirmed + disabled UI.
        if (!messages.Any(m => MessageContainsUiType(m, "eventDetailsForm")))
        {
            messages.Insert(index++, BuildUiAssistantMessage("Please tell me more about your event.", BuildEventDetailsFormUiJson()));
        }
        else index++;

        if (!hasEventCoreDraft || !eventFormSubmitted)
            return;

        // 6) Base AV package — always inject when missing from thread, embedding per-form submitted state
        // directly in the JSON so the Razor template is independent of the global session flag.
        // "Actually submitted" means the session flag is set AND the submission came from this same
        // Azure thread (matched via Draft:BaseAvSubmittedForThread), OR — legacy fallback — the thread
        // already contains a "Base AV provided:" user message.
        var currentThreadId = HttpContext.Session.GetString("AgentThreadId") ?? "";
        var baseAvForThread = HttpContext.Session.GetString("Draft:BaseAvSubmittedForThread") ?? "";
        var baseAvActuallySubmitted = baseAvSubmitted && (
            (!string.IsNullOrEmpty(baseAvForThread)
                && string.Equals(baseAvForThread, currentThreadId, StringComparison.Ordinal))
            || messages.Any(m =>
                string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (m.FullText ?? string.Join(" ", m.Parts ?? Enumerable.Empty<string>()))
                    .StartsWith("Base AV provided:", StringComparison.OrdinalIgnoreCase)));

        if (!messages.Any(m => MessageContainsUiType(m, "baseAvForm")))
        {
            messages.Insert(index++, BuildUiAssistantMessage(
                "Please review the included equipment for this room and confirm the options below.",
                BuildBaseAvFormUiJson(baseAvActuallySubmitted)));
        }
        else index++;

        if (!baseAvActuallySubmitted)
            return;

        // 7) Follow-up AV questions
        // Strip legacy tool-built avExtrasForm messages — they duplicate the server-injected followUpAvForm.
        messages.RemoveAll(IsAvExtrasFormMessage);

        if (!messages.Any(m => MessageContainsUiType(m, "followUpAvForm")))
        {
            messages.Insert(index++, BuildUiAssistantMessage(
                "Thanks for confirming the base AV package. I have a few follow-up questions.",
                BuildFollowUpAvFormUiJson()));
        }
        else index++;

        if (!followUpAvSubmitted)
            return;
    }

    private static bool IsLegacyContactPromptMessage(DisplayMessage message)
    {
        if (!string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;
        var text = (message.FullText ?? string.Join("\n", message.Parts ?? Enumerable.Empty<string>())).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (text.Contains("{\"ui\":", StringComparison.OrdinalIgnoreCase))
            return false;
        var lower = text.ToLowerInvariant();
        return lower.Contains("what is your full name")
            || lower.Contains("share your full name")
            || lower.Contains("please share your full name")
            || lower.Contains("could you please share your full name")
            || lower.Contains("full name to get started");
    }

    private static bool MessageContainsUiType(DisplayMessage message, string type)
    {
        var marker = $"\"type\":\"{type}\"";
        foreach (var part in message.Parts ?? Enumerable.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(part) && part.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return !string.IsNullOrWhiteSpace(message.FullText)
            && message.FullText.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAvExtrasFormMessage(DisplayMessage message)
    {
        if (MessageContainsUiType(message, "avExtrasForm"))
            return true;

        if (!string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;

        var text = (message.FullText ?? string.Join("\n", message.Parts ?? Enumerable.Empty<string>())).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("Please confirm these AV extras:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Please complete this AV extras form:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Finally, confirm your AV extras", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFollowUpAvFormMessage(DisplayMessage message)
    {
        if (MessageContainsUiType(message, "followUpAvForm"))
            return true;
        if (!string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            return false;
        var text = (message.FullText ?? string.Join("\n", message.Parts ?? Enumerable.Empty<string>())).Trim();
        return text.Contains("I have a few follow-up questions.", StringComparison.OrdinalIgnoreCase);
    }

    private static DisplayMessage BuildUiAssistantMessage(string preface, string uiJson)
    {
        var body = $"{preface}\n\n{uiJson}";
        return new DisplayMessage
        {
            Role = "assistant",
            Timestamp = DateTimeOffset.UtcNow,
            Parts = new List<string> { body },
            FullText = body,
            Html = preface
        };
    }

    private static string BuildContactFormUiJson()
    {
        var payload = new
        {
            ui = new
            {
                type = "contactForm",
                title = "Before we begin, please share your details",
                submitLabel = "Send details"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildEmailFormUiJson()
    {
        var defaultEmail = IsLeadEntry()
            ? ""
            : (HttpContext.Session.GetString("Draft:ContactEmail") ?? "");
        var payload = new { ui = new { type = "emailForm", title = "Email", submitLabel = "Continue", defaultEmail } };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildVenueConfirmFormUiJson()
    {
        var todayIso = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var start = HttpContext.Session.GetString("Draft:EventDate") ?? todayIso;
        var end = HttpContext.Session.GetString("Draft:EventEndDate") ?? start;
        var rooms = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var draftRoom = HttpContext.Session.GetString("Draft:RoomName")?.Trim() ?? "";
        var selectedSlug = WestinRoomCatalog.MatchDraftRoomNameToSlug(draftRoom, rooms);
        var roomOptions = rooms.Select(r => new { id = r.Slug, label = r.Name }).ToArray();
        var payload = new
        {
            ui = new
            {
                type = "venueConfirmForm",
                title = "Confirm your event",
                submitLabel = "Continue",
                attendees = HttpContext.Session.GetString("Draft:ExpectedAttendees") ?? "",
                startDate = start,
                endDate = end,
                minDate = todayIso,
                venueLabel = WestinRoomCatalog.VenueName,
                roomOptions,
                selectedRoomSlug = selectedSlug
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildEventDetailsFormUiJson()
    {
        var todayIso = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var payload = new
        {
            ui = new
            {
                type = "eventDetailsForm",
                title = "Event details",
                submitLabel = "Next",
                eventType = HttpContext.Session.GetString("Draft:EventType") ?? "",
                wantsRehearsalOperator = HttpContext.Session.GetString("Draft:WantsRehearsalOperator") ?? "",
                wantsOperator = HttpContext.Session.GetString("Draft:WantsOperator") ?? "",
                minDate = todayIso,
                schedule = new
                {
                    setup = HttpContext.Session.GetString("Draft:SetupTime") ?? "07:00",
                    rehearsal = HttpContext.Session.GetString("Draft:RehearsalTime") ?? "09:30",
                    start = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00",
                    end = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00",
                    packup = HttpContext.Session.GetString("Draft:PackupTime") ?? "18:00",
                    stepMinutes = 30
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildBaseAvFormUiJson(bool submitted = false)
    {
        var room = HttpContext.Session.GetString("Draft:RoomName") ?? "";
        var showPlacement = RoomSupportsProjectorPlacement(room);
        var floorPlanUrl = showPlacement ? "/images/westin/westin-ballroom/floor-plan.png" : "";
        var venueForPackages = HttpContext.Session.GetString("Draft:VenueName")?.Trim();
        if (string.IsNullOrWhiteSpace(venueForPackages))
            venueForPackages = WestinRoomCatalog.VenueName;
        var roomTrim = room.Trim();
        var roomOptionsForTitle = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var roomTitle = roomTrim;
        if (WestinRoomCatalog.MatchDraftRoomNameToSlug(roomTrim, roomOptionsForTitle) is { } titleSlug)
        {
            foreach (var (s, name) in roomOptionsForTitle)
            {
                if (string.Equals(s, titleSlug, StringComparison.OrdinalIgnoreCase))
                {
                    roomTitle = name;
                    break;
                }
            }
        }
        else if (string.IsNullOrWhiteSpace(roomTrim))
            roomTitle = "your room";

        var placementFromJson = VenueRoomPackagesCache.TryGetProjectorPlacementOptions(_env, venueForPackages, roomTrim);
        if (placementFromJson == null)
        {
            var packageRoomKey = ResolveWestinPackageRoomKeyForPlacement(roomTrim);
            if (!string.IsNullOrWhiteSpace(packageRoomKey)
                && !string.Equals(packageRoomKey, roomTrim, StringComparison.OrdinalIgnoreCase))
            {
                placementFromJson = VenueRoomPackagesCache.TryGetProjectorPlacementOptions(_env, venueForPackages, packageRoomKey);
            }
        }

        var baseEquipmentLabels = VenueRoomPackagesCache.TryGetBaseEquipmentLabels(_env, venueForPackages, roomTrim);
        var baseEquipment = baseEquipmentLabels.Count > 0
            ? baseEquipmentLabels.ToArray()
            : new[] { "Inbuilt projector", "Inbuilt screen", "Inbuilt speakers" };

        var payload = new
        {
            ui = new
            {
                type = "baseAvForm",
                submitted,
                title = $"Base AV package for {roomTitle}",
                submitLabel = "Next",
                roomName = room,
                baseEquipment,
                showProjectorPlacement = showPlacement,
                ballroomMode = showPlacement,
                floorPlanUrl,
                projectorPlacement = HttpContext.Session.GetString("Draft:ProjectorPlacementChoice") ?? "",
                placementOptions = placementFromJson ?? GetProjectorPlacementOptionsFallback(room),
                presenters = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0",
                flipchart = HttpContext.Session.GetString("Draft:Flipchart") ?? "no",
                laptopMode = HttpContext.Session.GetString("Draft:LaptopMode") ?? "none",
                laptopQty = HttpContext.Session.GetString("Draft:LaptopQty") ?? "0",
                adapterOwnLaptops = HttpContext.Session.GetString("Draft:AdapterOwnLaptops") ?? "no",
                builtInProjector = HttpContext.Session.GetString("Draft:BuiltInProjector") ?? "yes",
                builtInScreen = HttpContext.Session.GetString("Draft:BuiltInScreen") ?? "yes",
                builtInSpeakers = HttpContext.Session.GetString("Draft:BuiltInSpeakers") ?? "yes"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildFollowUpAvFormUiJson()
    {
        var presenters = 0;
        _ = int.TryParse(HttpContext.Session.GetString("Draft:PresenterCount"), out presenters);
        var laptopQty = 0;
        _ = int.TryParse(HttpContext.Session.GetString("Draft:LaptopQty"), out laptopQty);
        var roomName = HttpContext.Session.GetString("Draft:RoomName") ?? "";
        var payload = new
        {
            ui = new
            {
                type = "followUpAvForm",
                title = "Follow-up AV",
                submitLabel = "Generate quote",
                presenterCount = presenters,
                laptopQty,
                roomName,
                micType = HttpContext.Session.GetString("Draft:MicType") ?? "",
                micQty = HttpContext.Session.GetString("Draft:MicQty") ?? "0",
                lectern = HttpContext.Session.GetString("Draft:Lectern") ?? "",
                foldbackMonitor = HttpContext.Session.GetString("Draft:FoldbackMonitor") ?? "no",
                wirelessPresenter = HttpContext.Session.GetString("Draft:WirelessPresenter") ?? "no",
                laptopSwitcher = HttpContext.Session.GetString("Draft:LaptopSwitcher") ?? "no",
                stageLaptop = HttpContext.Session.GetString("Draft:StageLaptop") ?? "no",
                videoConference = HttpContext.Session.GetString("Draft:VideoConference") ?? "no"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private static bool RoomSupportsProjectorPlacement(string? roomName)
    {
        var r = (roomName ?? "").ToLowerInvariant();
        if (r.Contains("elevate", StringComparison.Ordinal))
            return false;
        // Only enable projector placement for the three specific Westin ballroom configurations
        var isBallroom1 = r.Contains("ballroom 1", StringComparison.Ordinal) || r.Contains("ballroom-1", StringComparison.Ordinal);
        var isBallroom2 = r.Contains("ballroom 2", StringComparison.Ordinal) || r.Contains("ballroom-2", StringComparison.Ordinal);
        // Full ballroom (but not the split rooms) - must contain "ballroom" but NOT "1" or "2"
        var isFullBallroom = r.Contains("ballroom", StringComparison.Ordinal)
            && !r.Contains("ballroom 1", StringComparison.Ordinal)
            && !r.Contains("ballroom-1", StringComparison.Ordinal)
            && !r.Contains("ballroom 2", StringComparison.Ordinal)
            && !r.Contains("ballroom-2", StringComparison.Ordinal);
        return isBallroom1 || isBallroom2 || isFullBallroom;
    }

    /// <summary>Maps draft/slug room labels to <c>venue-room-packages.json</c> keys for Westin ballroom variants.</summary>
    private static string? ResolveWestinPackageRoomKeyForPlacement(string? roomName)
    {
        var roomNorm = (roomName ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(roomNorm))
            return null;
        if (roomNorm.Contains("ballroom 1") || roomNorm.Contains("ballroom-1"))
            return "Westin Ballroom 1";
        if (roomNorm.Contains("ballroom 2") || roomNorm.Contains("ballroom-2"))
            return "Westin Ballroom 2";
        if (roomNorm.Contains("ballroom"))
            return "Westin Ballroom";
        return null;
    }

    /// <summary>Legacy placement list when <c>venue-room-packages.json</c> has no <c>projectorPlacementOptions</c> for the room.</summary>
    private static object[] GetProjectorPlacementOptionsFallback(string? roomName)
    {
        var allowed = GetAllowedProjectorAreasForRoom(roomName);
        var list = new List<object>();
        foreach (var a in allowed)
            list.Add(new { id = a, label = a });

        var room = (roomName ?? "").Trim().ToLowerInvariant();
        var isSplit = room is "westin ballroom 1" or "ballroom 1" or "westin ballroom 2" or "ballroom 2";
        if (!isSplit && allowed.Count == 6)
        {
            list.Add(new { id = "B+C", label = "B+C" });
            list.Add(new { id = "E+F", label = "E+F" });
        }

        return list.ToArray();
    }

    /// <summary>Parses projector placement (single area or A+B dual) against areas allowed for the Westin ballroom variant.</summary>
    private static List<string> ParseProjectorPlacementToAllowedAreas(string? placement, string? roomName)
    {
        var allowed = GetAllowedProjectorAreasForRoom(roomName);
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var p = (placement ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        if (string.IsNullOrEmpty(p))
            return result;

        var m = Regex.Match(p, @"^([A-F])\+([A-F])$");
        if (m.Success)
        {
            foreach (var g in new[] { m.Groups[1].Value, m.Groups[2].Value })
            {
                if (allowedSet.Contains(g) && !result.Exists(x => string.Equals(x, g, StringComparison.OrdinalIgnoreCase)))
                    result.Add(g);
            }

            return result;
        }

        if (p.Length == 1 && p[0] is >= 'A' and <= 'F' && allowedSet.Contains(p))
            result.Add(p);
        return result;
    }

    private string BuildSubmittedFollowUpAvViewJson()
    {
        var items = new List<(string label, string value)>();
        if (int.TryParse(HttpContext.Session.GetString("Draft:PresenterCount"), out var pc) && pc > 0)
        {
            items.Add(("Microphone", HttpContext.Session.GetString("Draft:MicType") ?? ""));
            items.Add(("Video conference", HttpContext.Session.GetString("Draft:VideoConference") ?? ""));
        }
        var payload = new { ui = new { type = "submittedForm", title = "AV selections confirmed", items } };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildEventFormUiJson()
    {
        var todayIso = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var rooms = _westinRoomCatalog.GetVenueConfirmRoomOptions();
        var draftRoom = HttpContext.Session.GetString("Draft:RoomName")?.Trim() ?? "";
        var selectedSlug = WestinRoomCatalog.MatchDraftRoomNameToSlug(draftRoom, rooms);
        var roomOptions = rooms.Select(r => new { id = r.Slug, label = r.Name }).ToArray();
        var payload = new
        {
            ui = new
            {
                type = "eventForm",
                title = "Great, now let's capture your event details",
                submitLabel = "Send event details",
                eventType = HttpContext.Session.GetString("Draft:EventType") ?? "",
                attendees = HttpContext.Session.GetString("Draft:ExpectedAttendees") ?? "",
                eventDate = HttpContext.Session.GetString("Draft:EventDate") ?? todayIso,
                minDate = todayIso,
                setupStyle = HttpContext.Session.GetString("Draft:SetupStyle") ?? "",
                venueLabel = WestinRoomCatalog.VenueName,
                roomOptions,
                selectedRoomSlug = selectedSlug,
                setupOptions = new[] { "Theatre", "Classroom", "Banquet", "Cocktail", "U-Shape", "Boardroom" },
                schedule = new
                {
                    setup = HttpContext.Session.GetString("Draft:SetupTime") ?? "07:00",
                    rehearsal = HttpContext.Session.GetString("Draft:RehearsalTime") ?? "09:30",
                    start = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00",
                    end = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00",
                    packup = HttpContext.Session.GetString("Draft:PackupTime") ?? "18:00",
                    stepMinutes = 30
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildAvExtrasFormUiJson()
    {
        var eventStart = HttpContext.Session.GetString("Draft:StartTime") ?? "10:00";
        var eventEnd = HttpContext.Session.GetString("Draft:EndTime") ?? "16:00";
        var payload = new
        {
            ui = new
            {
                type = "avExtrasForm",
                title = "Finally, confirm your AV extras",
                submitLabel = "Send AV extras",
                presenters = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0",
                speakers = HttpContext.Session.GetString("Draft:SpeakerCount") ?? "0",
                clicker = string.Equals(HttpContext.Session.GetString("Draft:NeedsClicker"), "yes", StringComparison.OrdinalIgnoreCase),
                recording = string.Equals(HttpContext.Session.GetString("Draft:NeedsRecording"), "yes", StringComparison.OrdinalIgnoreCase),
                techStart = HttpContext.Session.GetString("Draft:TechStartTime") ?? eventStart,
                techEnd = HttpContext.Session.GetString("Draft:TechEndTime") ?? eventEnd,
                eventStart,
                eventEnd,
                stepMinutes = 30
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildSubmittedContactFormViewJson()
    {
        var firstName = HttpContext.Session.GetString("Draft:ContactFirstName") ?? "";
        var lastName = HttpContext.Session.GetString("Draft:ContactLastName") ?? "";
        var org = HttpContext.Session.GetString("Draft:Organisation") ?? "";
        var location = HttpContext.Session.GetString("Draft:OrganisationAddress") ?? "";
        var email = HttpContext.Session.GetString("Draft:ContactEmail") ?? "";
        var phone = HttpContext.Session.GetString("Draft:ContactPhone") ?? "";

        var items = new List<(string label, string value)>();
        if (!string.IsNullOrWhiteSpace(firstName)) items.Add(("First name", firstName));
        if (!string.IsNullOrWhiteSpace(lastName)) items.Add(("Last name", lastName));
        if (!string.IsNullOrWhiteSpace(org)) items.Add(("Organisation", org));
        if (!string.IsNullOrWhiteSpace(location)) items.Add(("Location", location));
        if (!string.IsNullOrWhiteSpace(email)) items.Add(("Email", email));
        if (!string.IsNullOrWhiteSpace(phone)) items.Add(("Phone", phone));

        var payload = new
        {
            ui = new
            {
                type = "submittedForm",
                title = "Contact details submitted",
                items
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private string BuildSubmittedAvExtrasFormViewJson()
    {
        var presenters = HttpContext.Session.GetString("Draft:PresenterCount") ?? "0";
        var speakers = HttpContext.Session.GetString("Draft:SpeakerCount") ?? "0";
        var clicker = HttpContext.Session.GetString("Draft:NeedsClicker") ?? "no";
        var recording = HttpContext.Session.GetString("Draft:NeedsRecording") ?? "no";
        var techStart = HttpContext.Session.GetString("Draft:TechStartTime") ?? "";
        var techEnd = HttpContext.Session.GetString("Draft:TechEndTime") ?? "";

        var items = new List<(string label, string value)>();
        if (int.TryParse(presenters, out var pCount) && pCount > 0) items.Add(("Presenters", presenters));
        if (int.TryParse(speakers, out var sCount) && sCount > 0) items.Add(("Speakers", speakers));
        items.Add(("Wireless clicker", clicker.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No"));
        items.Add(("Audio/video recording", recording.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No"));
        if (!string.IsNullOrWhiteSpace(techStart) && !string.IsNullOrWhiteSpace(techEnd))
            items.Add(("Technician coverage", $"{techStart} to {techEnd}"));

        var payload = new
        {
            ui = new
            {
                type = "submittedForm",
                title = "AV extras confirmed",
                items
            }
        };
        return JsonSerializer.Serialize(payload);
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
        bool gotSetup = false, gotReh = false;

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
                    packup = ts; break;
            }
        }

        if (packup == default)
        {
            if (showEnd.HasValue)
                packup = showEnd.Value;
            else if (showStart.HasValue)
                packup = showStart.Value;
            else
                packup = rehearsal;
        }

        // Validate chronological order before returning
        if (!ValidateScheduleOrder(setup, rehearsal, showStart, showEnd))
        {
            return false;
        }

        return gotSetup && gotReh;
    }

    /// <summary>
    /// Validates that schedule times are in chronological order: Setup < Rehearsal < Start < End
    /// </summary>
    private static bool ValidateScheduleOrder(
        TimeSpan setup,
        TimeSpan rehearsal,
        TimeSpan? showStart,
        TimeSpan? showEnd)
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
        ViewData["VenueConfirmSubmitted"] = HttpContext.Session.GetString("Draft:VenueConfirmSubmitted") ?? "0";
        ViewData["EventFormSubmitted"] = HttpContext.Session.GetString("Draft:EventFormSubmitted") ?? "0";
        ViewData["BaseAvSubmitted"] = HttpContext.Session.GetString("Draft:BaseAvSubmitted") ?? "0";
        ViewData["FollowUpAvSubmitted"] = HttpContext.Session.GetString("Draft:FollowUpAvSubmitted") ?? "0";
        ViewData["WantsOperator"] = HttpContext.Session.GetString("Draft:WantsOperator") ?? "no";
        ViewData["QuoteUrl"] = HttpContext.Session.GetString("Draft:QuoteUrl") ?? "";
    }

    private string DetermineProgressStep(IEnumerable<DisplayMessage>? messages)
    {
        if (HttpContext.Session.GetString("Draft:QuoteComplete") == "1")
            return "fourth-step-active";

        var list = messages as IReadOnlyList<DisplayMessage> ?? messages?.ToList();
        var sess = HttpContext.Session;

        if (list != null && WasLastAssistantQuoteGenerationConsentPrompt(list))
            return "third-step-active";

        if (string.Equals(sess.GetString("Draft:FollowUpAvSubmitted"), "1", StringComparison.Ordinal)
            || string.Equals(sess.GetString("Draft:AvExtrasSubmitted"), "1", StringComparison.Ordinal))
            return "third-step-active";

        if (string.Equals(sess.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal)
            || string.Equals(sess.GetString("Draft:EventFormSubmitted"), "1", StringComparison.Ordinal))
            return "second-step-active";

        if (string.Equals(sess.GetString("Draft:VenueConfirmSubmitted"), "1", StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(sess.GetString("Draft:SetupTime")))
            return "second-step-active";

        if (string.Equals(sess.GetString("Draft:EmailGateCompleted"), "1", StringComparison.Ordinal)
            || string.Equals(sess.GetString("Draft:ContactFormSubmitted"), "1", StringComparison.Ordinal))
            return "first-step-active";

        if (string.Equals(sess.GetString("Draft:EntrySource"), "lead", StringComparison.OrdinalIgnoreCase))
            return "first-step-active";

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

            // Check session first to avoid repeated creation in a single chat session
            var sid = HttpContext.Session.GetString("Draft:ContactId");
            if (decimal.TryParse(sid, out var savedId)) return savedId;

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

            // Resolve using the "no-update" service. 
            // Note: Since we don't have an Org context here, it will likely create a new contact 
            // unless one was already linked to an org we somehow resolved.
            // But ResolveAsync handles the "create new if not linked" logic safely.
            var orgName = HttpContext.Session.GetString("Draft:Organisation");
            
            var res = await _contactResolution.ResolveAsync(
                name,
                email,
                phone,
                position,
                orgName,
                null,
                ct,
                leadAuthoritative: false);

            if (res.contactId.HasValue)
            {
                HttpContext.Session.SetString("Draft:ContactId", res.contactId.Value.ToString());
                return res.contactId;
            }

            return null;
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
        int timeoutSeconds = 300)
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
