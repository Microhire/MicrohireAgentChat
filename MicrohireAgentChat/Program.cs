using Azure.AI.Projects;
using Azure.Identity;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


builder.Services.Configure<AzureAgentOptions>(opt =>
{
    opt.Endpoint = Environment.GetEnvironmentVariable("AZURE_EXISTING_AIPROJECT_ENDPOINT")
                   ?? builder.Configuration["AzureAgent:Endpoint"];
    opt.AgentId = Environment.GetEnvironmentVariable("AZURE_EXISTING_AGENT_ID")
                   ?? builder.Configuration["AzureAgent:AgentId"];
});
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
     ExcludeInteractiveBrowserCredential = false
});

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
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("AppConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AzureAgentChatService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddHostedService<AgentToolInstaller>();
builder.Services.AddSingleton<PdfStamperService>();
builder.Services.AddSingleton<PdfFromBlankService>();
builder.Services.AddSingleton<IWestinRoomCatalog, WestinRoomCatalog>();
builder.Services.AddSingleton<IBookingDraftStore, BookingDraftStore>();
builder.Services.AddControllersWithViews();
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
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); 

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Index}/{id?}");

app.Run();
