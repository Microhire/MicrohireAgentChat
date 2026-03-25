using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Keeps a single Playwright <see cref="IBrowser"/> alive for the app lifetime and creates a fresh
/// <see cref="IPage"/> per PDF. Avoids per-request Chromium launch (the main steady-state cost after HTML generation).
/// </summary>
public sealed class PlaywrightQuotePdfRenderer : IPlaywrightQuotePdfRenderer, IHostedService
{
    /// <summary>Max wait (ms) for Chromium to start when (re)launching the shared browser.</summary>
    private const float ChromiumLaunchTimeoutMs = 120_000f;

    private readonly ILogger<PlaywrightQuotePdfRenderer> _logger;
    private readonly SemaphoreSlim _pdfGate = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightQuotePdfRenderer(ILogger<PlaywrightQuotePdfRenderer> logger)
    {
        _logger = logger;
    }

    public async Task<bool> GeneratePdfFromHtmlAsync(
        string html,
        string pdfOutputPath,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        await _pdfGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var log = logger ?? _logger;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureBrowserReadyAsync(log, cancellationToken).ConfigureAwait(false);

            if (_browser is null || !_browser.IsConnected)
            {
                log.LogWarning("[QUOTE GEN] Shared Chromium not available after init.");
                return false;
            }

            IPage? page = null;
            try
            {
                page = await _browser.NewPageAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var contentSw = Stopwatch.StartNew();
                await page.SetContentAsync(html, new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 90_000
                }).ConfigureAwait(false);
                contentSw.Stop();
                log.LogInformation("[QUOTE GEN] Playwright SetContent (Load) finished in {Ms}ms", contentSw.ElapsedMilliseconds);

                cancellationToken.ThrowIfCancellationRequested();
                var pdfSw = Stopwatch.StartNew();
                await page.PdfAsync(new PagePdfOptions
                {
                    Path = pdfOutputPath,
                    Format = "A4",
                    PrintBackground = true,
                    Margin = new() { Top = "10mm", Bottom = "12mm", Left = "10mm", Right = "10mm" }
                }).ConfigureAwait(false);
                pdfSw.Stop();
                log.LogInformation("[QUOTE GEN] Playwright PdfAsync finished in {Ms}ms", pdfSw.ElapsedMilliseconds);
            }
            finally
            {
                if (page != null)
                {
                    try
                    {
                        await page.CloseAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "[QUOTE GEN] Failed to close Playwright page.");
                    }
                }
            }

            if (!File.Exists(pdfOutputPath) || new FileInfo(pdfOutputPath).Length == 0)
            {
                log.LogWarning("[QUOTE GEN] PDF output missing or empty at {Path}", pdfOutputPath);
                await InvalidateBrowserAsync(log).ConfigureAwait(false);
                return false;
            }

            log.LogInformation("[QUOTE GEN] PDF pre-generated: {Path} ({Size} bytes)",
                pdfOutputPath, new FileInfo(pdfOutputPath).Length);
            return true;
        }
        catch (OperationCanceledException ex)
        {
            log.LogWarning(ex, "[QUOTE GEN] PDF generation cancelled for {Path}", pdfOutputPath);
            return false;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "[QUOTE GEN] PDF generation failed for {Path}", pdfOutputPath);
            await InvalidateBrowserAsync(log).ConfigureAwait(false);
            return false;
        }
        finally
        {
            _pdfGate.Release();
        }
    }

    private async Task EnsureBrowserReadyAsync(ILogger? logger, CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true })
            return;

        await PlaywrightBootstrap.EnsureChromiumReadyAsync(logger, cancellationToken).ConfigureAwait(false);

        if (_browser is { IsConnected: true })
            return;

        await DisposeBrowserOnlyAsync().ConfigureAwait(false);

        logger?.LogInformation("[QUOTE GEN] Playwright CreateAsync (shared instance) starting…");
        _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);

        logger?.LogInformation("[QUOTE GEN] Chromium LaunchAsync starting (timeout {TimeoutMs}ms, shared browser)…", ChromiumLaunchTimeoutMs);
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" },
            Timeout = ChromiumLaunchTimeoutMs
        }).ConfigureAwait(false);

        logger?.LogInformation("[QUOTE GEN] Shared Chromium browser ready.");
    }

    private async Task InvalidateBrowserAsync(ILogger? logger)
    {
        try
        {
            await DisposeBrowserOnlyAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[QUOTE GEN] Error while invalidating shared Chromium.");
        }
    }

    private async Task DisposeBrowserOnlyAsync()
    {
        if (_browser != null)
        {
            try
            {
                await _browser.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            _browser = null;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _pdfGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeBrowserOnlyAsync().ConfigureAwait(false);
            if (_playwright != null)
            {
                _playwright.Dispose();
                _playwright = null;
            }
        }
        finally
        {
            _pdfGate.Release();
        }
    }
}
