using Azure.AI.Projects;
using Azure.Identity;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Services;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using MicrohireAgentChat.Middleware;
using Microsoft.EntityFrameworkCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddAzureWebAppDiagnostics();

// Sends ILogger + request telemetry to the linked Application Insights resource (portal sets APPLICATIONINSIGHTS_* env vars).
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING"))
    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY")))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// Writable browser cache + startup install so quote PDFs work on Azure when publish omitted Chromium.
PlaywrightBootstrap.ConfigureBrowserDirectory(builder.Environment);

// Local overrides (gitignored) - copy appsettings.Development.Local.json.example and fill in your Gmail
builder.Configuration.AddJsonFile("appsettings.Development.Local.json", optional: true, reloadOnChange: true);

var azureAgentSection = builder.Configuration.GetSection("AzureAgent");
var azureAgentOptions = azureAgentSection.Get<AzureAgentOptions>() ?? new AzureAgentOptions();

// Allow environment variables to override
azureAgentOptions.Endpoint = Environment.GetEnvironmentVariable("AZURE_EXISTING_AIPROJECT_ENDPOINT") ?? azureAgentOptions.Endpoint;
azureAgentOptions.AgentId = Environment.GetEnvironmentVariable("AZURE_EXISTING_AGENT_ID") ?? azureAgentOptions.AgentId;
azureAgentOptions.TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? azureAgentOptions.TenantId;

builder.Services.Configure<AzureAgentOptions>(opt =>
{
    opt.Endpoint = azureAgentOptions.Endpoint;
    opt.AgentId = azureAgentOptions.AgentId;
    opt.TenantId = azureAgentOptions.TenantId;
});

builder.Services.Configure<DevModeOptions>(builder.Configuration.GetSection("DevMode"));
builder.Services.Configure<AutoTestOptions>(builder.Configuration.GetSection("AutoTest"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<RentalPointDefaultsOptions>(builder.Configuration.GetSection("RentalPointDefaults"));
builder.Services.Configure<LeadEmailOptions>(builder.Configuration.GetSection("LeadEmail"));
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var credentialOptions = new DefaultAzureCredentialOptions
{
    ExcludeInteractiveBrowserCredential = true
};

if (!string.IsNullOrEmpty(azureAgentOptions.TenantId))
{
    credentialOptions.TenantId = azureAgentOptions.TenantId;
}

var credential = new DefaultAzureCredential(credentialOptions);

builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureAgentOptions>>().Value;
    if (string.IsNullOrWhiteSpace(opts.Endpoint))
        throw new InvalidOperationException("AzureAgent:Endpoint is required.");
    return new AIProjectClient(new Uri(opts.Endpoint), credential);
});

builder.Services.AddDbContext<BookingDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString(
        "BookingsDb");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("Missing connection string 'BookingsDb'.");
    opt.UseSqlServer(cs);
    opt.EnableDetailedErrors();
    opt.EnableSensitiveDataLogging();
});
// AppDbContext for thread persistence (separate from client's BookingsDb)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("AppConnection");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("Missing connection string 'AppConnection'.");
    options.UseSqlServer(cs);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AzureAgentChatService>();
builder.Services.AddScoped<ChatSessionPersistenceService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddHostedService<PlaywrightBootstrapHostedService>();
builder.Services.AddHostedService<AgentToolInstaller>();
builder.Services.AddSingleton<PdfStamperService>();
builder.Services.AddSingleton<PdfFromBlankService>();
builder.Services.AddSingleton<IWestinRoomCatalog, WestinRoomCatalog>();
builder.Services.AddSingleton<IBookingDraftStore, BookingDraftStore>();

// Register new persistence and orchestration services
builder.Services.AddScoped<ConversationExtractionService>();
builder.Services.AddScoped<ChatExtractionService>();
builder.Services.AddScoped<ContactPersistenceService>();
builder.Services.AddScoped<OrganizationPersistenceService>();
builder.Services.AddScoped<ContactResolutionService>();
builder.Services.AddScoped<BookingPersistenceService>();
builder.Services.AddScoped<ItemPersistenceService>();
builder.Services.AddScoped<CrewPersistenceService>();
builder.Services.AddScoped<BookingOrchestrationService>();

// Register bug fix services
builder.Services.AddScoped<ConversationStateService>();
builder.Services.AddScoped<AcknowledgmentService>();
builder.Services.AddScoped<QuestionDetectionService>();

// HTTP client for AI services
builder.Services.AddHttpClient();

// Register new modular services (extracted from AzureAgentChatService)
builder.Services.AddScoped<EquipmentSearchService>();
builder.Services.AddScoped<AIEquipmentQueryService>(); // AI-powered dynamic equipment search
builder.Services.AddScoped<SmartEquipmentRecommendationService>(); // Smart equipment selection based on event context - no technical questions!
builder.Services.AddScoped<AgentToolHandlerService>();
builder.Services.AddScoped<BookingQueryService>();
builder.Services.AddScoped<TimePickerService>();
builder.Services.AddScoped<QuoteGenerationService>();
builder.Services.AddScoped<HtmlQuoteGenerationService>();
builder.Services.AddScoped<ConversationReplayService>();
builder.Services.AddScoped<AutoTestCustomerService>();
builder.Services.AddScoped<ILeadEmailService, LeadEmailService>();
builder.Services.AddScoped<ILeadSubmissionFollowUp, LeadSubmissionFollowUpService>();
builder.Services.AddControllersWithViews();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".MicrohireAgent.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<AzureQuotesStaticMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseSession();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Index}/{id?}");

app.Run();
