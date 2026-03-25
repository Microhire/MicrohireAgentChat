namespace MicrohireAgentChat.Services;

/// <summary>
/// Runs Playwright Chromium install at startup when the host is misconfigured or browsers are missing,
/// so the first quote PDF does not fail cold.
/// </summary>
public sealed class PlaywrightBootstrapHostedService : IHostedService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PlaywrightBootstrapHostedService> _logger;

    public PlaywrightBootstrapHostedService(
        IWebHostEnvironment env,
        ILogger<PlaywrightBootstrapHostedService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Do not await Chromium install here. On Azure App Service, ANCM enforces a startup time limit;
        // probing + `playwright install` can exceed it and surface as HTTP 500.37 while `/api/*` fails.
        _ = RunBootstrapInBackgroundAsync();
        return Task.CompletedTask;
    }

    private async Task RunBootstrapInBackgroundAsync()
    {
        try
        {
            await PlaywrightBootstrap.EnsureChromiumReadyAsync(_logger, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Playwright] Startup bootstrap failed; PDF generation will retry on first use.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
