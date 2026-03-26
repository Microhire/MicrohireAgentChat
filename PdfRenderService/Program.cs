using System.Diagnostics;
using Microsoft.Playwright;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PdfRenderer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PdfRenderer>());

var app = builder.Build();

app.MapGet("/health", async (PdfRenderer renderer) =>
{
    var (ok, detail) = await renderer.ProbeAsync();
    return Results.Json(new
    {
        status = ok ? "healthy" : "degraded",
        chromiumReady = ok,
        detail,
        browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? "(not set)",
        pid = Environment.ProcessId,
        uptime = renderer.Uptime
    });
});

app.MapPost("/pdf/from-html", async (PdfRequest request, PdfRenderer renderer) =>
{
    if (string.IsNullOrWhiteSpace(request.Html))
        return Results.BadRequest(new { error = "html field is required" });

    var sw = Stopwatch.StartNew();
    var (ok, pdfBytes, error) = await renderer.RenderAsync(request.Html);
    sw.Stop();

    if (!ok || pdfBytes == null)
        return Results.Json(new { error = error ?? "PDF generation failed", ms = sw.ElapsedMilliseconds }, statusCode: 500);

    return Results.File(pdfBytes, "application/pdf", $"quote-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
});

app.Run();

record PdfRequest(string Html);

sealed class PdfRenderer : IHostedService, IAsyncDisposable
{
    private readonly ILogger<PdfRenderer> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private DateTime _startedUtc = DateTime.UtcNow;

    public string Uptime => (DateTime.UtcNow - _startedUtc).ToString(@"hh\:mm\:ss");

    public PdfRenderer(ILogger<PdfRenderer> logger) => _logger = logger;

    public async Task<(bool Ok, byte[]? Pdf, string? Error)> RenderAsync(string html)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureBrowserAsync().ConfigureAwait(false);
            if (_browser is not { IsConnected: true })
                return (false, null, "Chromium browser not available");

            var page = await _browser.NewPageAsync().ConfigureAwait(false);
            try
            {
                await page.SetContentAsync(html, new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 60_000
                }).ConfigureAwait(false);

                var pdfBytes = await page.PdfAsync(new PagePdfOptions
                {
                    Format = "A4",
                    PrintBackground = true,
                    Margin = new() { Top = "10mm", Bottom = "12mm", Left = "10mm", Right = "10mm" }
                }).ConfigureAwait(false);

                _logger.LogInformation("PDF rendered: {Bytes} bytes", pdfBytes.Length);
                return (true, pdfBytes, null);
            }
            finally
            {
                try { await page.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF render failed");
            await InvalidateBrowserAsync().ConfigureAwait(false);
            return (false, null, ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<(bool Ok, string Detail)> ProbeAsync()
    {
        try
        {
            await EnsureBrowserAsync().ConfigureAwait(false);
            return (_browser is { IsConnected: true }, _browser is { IsConnected: true } ? "Chromium connected" : "Browser not connected");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browser is { IsConnected: true })
            return;

        _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);

        if (_browser != null)
        {
            try { await _browser.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
            _browser = null;
        }

        _logger.LogInformation("Launching Chromium...");
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu" },
            Timeout = 60_000
        }).ConfigureAwait(false);
        _logger.LogInformation("Chromium launched successfully");
    }

    private async Task InvalidateBrowserAsync()
    {
        if (_browser != null)
        {
            try { await _browser.CloseAsync().ConfigureAwait(false); } catch { /* ignore */ }
            _browser = null;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _startedUtc = DateTime.UtcNow;
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Pre-warming Chromium...");
                await EnsureBrowserAsync().ConfigureAwait(false);
                _logger.LogInformation("Chromium pre-warm complete");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chromium pre-warm failed (will retry on first request)");
            }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await InvalidateBrowserAsync().ConfigureAwait(false);
            if (_playwright != null)
            {
                _playwright.Dispose();
                _playwright = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await InvalidateBrowserAsync().ConfigureAwait(false);
        _playwright?.Dispose();
    }
}
