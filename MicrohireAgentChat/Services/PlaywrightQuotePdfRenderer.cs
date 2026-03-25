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
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SemaphoreSlim _pdfGate = new(1, 1);
    /// <summary>Serializes shared browser creation/disposal vs. background pre-warm and invalidation.</summary>
    private readonly SemaphoreSlim _browserInitLock = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightQuotePdfRenderer(ILogger<PlaywrightQuotePdfRenderer> logger, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    public async Task<bool> GeneratePdfFromHtmlAsync(
        string html,
        string pdfOutputPath,
        ILogger? logger = null,
        CancellationToken cancellationToken = default,
        string? quoteTraceId = null)
    {
        var trace = quoteTraceId ?? "(no-trace)";
        var gateWaitSw = Stopwatch.StartNew();
        await _pdfGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        gateWaitSw.Stop();
        var log = logger ?? _logger;
        var gateSw = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            log.LogInformation(
                "[QUOTE GEN] PDF trace={Trace} phase=playwright_enter pdf_gate_waitMs={GateWaitMs} htmlChars={HtmlChars} pdfPath={PdfPath}",
                trace,
                gateWaitSw.ElapsedMilliseconds,
                html?.Length ?? 0,
                pdfOutputPath);

            await EnsureBrowserReadyAsync(log, cancellationToken, trace).ConfigureAwait(false);

            if (_browser is null || !_browser.IsConnected)
            {
                log.LogWarning("[QUOTE GEN] PDF trace={Trace} phase=browser_missing Shared Chromium not available after init.", trace);
                return false;
            }

            log.LogInformation("[QUOTE GEN] PDF trace={Trace} phase=browser_ok connected={Connected}", trace, _browser.IsConnected);

            IPage? page = null;
            try
            {
                var newPageSw = Stopwatch.StartNew();
                page = await _browser.NewPageAsync().ConfigureAwait(false);
                newPageSw.Stop();
                log.LogInformation("[QUOTE GEN] PDF trace={Trace} phase=new_page ms={Ms}", trace, newPageSw.ElapsedMilliseconds);

                cancellationToken.ThrowIfCancellationRequested();
                var contentSw = Stopwatch.StartNew();
                await page.SetContentAsync(html ?? string.Empty, new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 90_000
                }).ConfigureAwait(false);
                contentSw.Stop();
                log.LogInformation(
                    "[QUOTE GEN] PDF trace={Trace} phase=set_content_wait_load ms={Ms} htmlChars={HtmlChars}",
                    trace,
                    contentSw.ElapsedMilliseconds,
                    html?.Length ?? 0);

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
                log.LogInformation("[QUOTE GEN] PDF trace={Trace} phase=pdf_async ms={Ms} path={Path}", trace, pdfSw.ElapsedMilliseconds, pdfOutputPath);
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
                log.LogWarning(
                    "[QUOTE GEN] PDF trace={Trace} phase=output_invalid exists={Exists} len={Len} path={Path}",
                    trace,
                    File.Exists(pdfOutputPath),
                    File.Exists(pdfOutputPath) ? new FileInfo(pdfOutputPath).Length : 0,
                    pdfOutputPath);
                await InvalidateBrowserAsync(log).ConfigureAwait(false);
                return false;
            }

            var sz = new FileInfo(pdfOutputPath).Length;
            log.LogInformation(
                "[QUOTE GEN] PDF trace={Trace} phase=done_ok bytes={Bytes} path={Path}",
                trace,
                sz,
                pdfOutputPath);
            return true;
        }
        catch (OperationCanceledException ex)
        {
            log.LogWarning(ex, "[QUOTE GEN] PDF trace={Trace} phase=cancelled path={Path}", trace, pdfOutputPath);
            return false;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "[QUOTE GEN] PDF trace={Trace} phase=exception path={Path}", trace, pdfOutputPath);
            await InvalidateBrowserAsync(log).ConfigureAwait(false);
            return false;
        }
        finally
        {
            gateSw.Stop();
            log.LogInformation(
                "[QUOTE GEN] PDF trace={Trace} phase=gate_release totalWallMs={Ms} (PDF mutex held)",
                trace,
                gateSw.ElapsedMilliseconds);
            _pdfGate.Release();
        }
    }

    private async Task EnsureBrowserReadyAsync(ILogger? logger, CancellationToken cancellationToken, string trace)
    {
        if (_browser is { IsConnected: true })
        {
            logger?.LogInformation("[QUOTE GEN] PDF trace={Trace} phase=ensure_browser reuse_existing", trace);
            return;
        }

        await _browserInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser is { IsConnected: true })
            {
                logger?.LogInformation("[QUOTE GEN] PDF trace={Trace} phase=ensure_browser reuse_existing_after_lock", trace);
                return;
            }

            var bootstrapSw = Stopwatch.StartNew();
            logger?.LogInformation("[QUOTE GEN] PDF trace={Trace} phase=ensure_chromium_playwright_bootstrap", trace);
            await PlaywrightBootstrap.EnsureChromiumReadyAsync(logger, cancellationToken).ConfigureAwait(false);
            bootstrapSw.Stop();
            logger?.LogInformation(
                "[QUOTE GEN] PDF trace={Trace} phase=ensure_chromium_playwright_bootstrap_done ms={Ms}",
                trace,
                bootstrapSw.ElapsedMilliseconds);

            await DisposeBrowserOnlyAsync().ConfigureAwait(false);

            logger?.LogInformation("[QUOTE GEN] PDF trace={Trace} phase=playwright_create_async", trace);
            _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);

            var launchSw = Stopwatch.StartNew();
            logger?.LogInformation(
                "[QUOTE GEN] PDF trace={Trace} phase=chromium_launch timeoutMs={TimeoutMs}",
                trace,
                ChromiumLaunchTimeoutMs);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" },
                Timeout = ChromiumLaunchTimeoutMs
            }).ConfigureAwait(false);
            launchSw.Stop();

            logger?.LogInformation(
                "[QUOTE GEN] PDF trace={Trace} phase=chromium_launched sharedLaunchMs={Ms}",
                trace,
                launchSw.ElapsedMilliseconds);
        }
        finally
        {
            _browserInitLock.Release();
        }
    }

    private async Task InvalidateBrowserAsync(ILogger? logger)
    {
        await _browserInitLock.WaitAsync().ConfigureAwait(false);
        try
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
        finally
        {
            _browserInitLock.Release();
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await EnsureBrowserReadyAsync(_logger, _lifetime.ApplicationStopping, "(prewarm)").ConfigureAwait(false);
                    _logger.LogInformation("[QUOTE GEN] PDF trace=(prewarm) phase=prewarm_complete");
                }
                catch (OperationCanceledException)
                {
                    // App stopped before pre-warm finished.
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[QUOTE GEN] Pre-warm Chromium failed (non-fatal).");
                }
            },
            CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _pdfGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _browserInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                _browserInitLock.Release();
            }
        }
        finally
        {
            _pdfGate.Release();
        }
    }
}
