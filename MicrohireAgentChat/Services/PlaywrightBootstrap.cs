using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MicrohireAgentChat.Helpers;
using Microsoft.Playwright;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Ensures Playwright drivers and Chromium are present. On Azure App Service, publish may omit a full
/// browser install; we set a writable <c>PLAYWRIGHT_BROWSERS_PATH</c> under the site and run
/// <c>playwright install chromium</c> via the official API when needed.
/// Also sets <c>PLAYWRIGHT_DRIVER_SEARCH_PATH</c> to the app publish folder (same as official
/// <c>playwright.ps1</c>) so the bundled <c>.playwright/node</c> driver is used instead of
/// <c>%HOME%\.playwright\...</c>, which is often missing or wrong on App Service.
/// </summary>
public static class PlaywrightBootstrap
{
    private static readonly SemaphoreSlim InstallGate = new(1, 1);
    private static volatile bool _installAttempted;

    /// <summary>
    /// Call once at process start (before any <see cref="Playwright.CreateAsync"/>).
    /// Sets <c>PLAYWRIGHT_DRIVER_SEARCH_PATH</c> to the app output folder when the bundled <c>.playwright</c>
    /// directory exists (or when unset and there is no bundle). That overrides incorrect host env values.
    /// Sets <c>PLAYWRIGHT_BROWSERS_PATH</c> when unset,
    /// preferring <c>HOME</c>/<c>USERPROFILE</c> so browsers land on a writable path when
    /// <c>WEBSITE_RUN_FROM_PACKAGE</c> makes the site root read-only.
    /// </summary>
    public static void ConfigureBrowserDirectory(IWebHostEnvironment env)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(Playwright).Assembly.Location);
        var searchRoot = !string.IsNullOrEmpty(assemblyDir) && Directory.Exists(assemblyDir)
            ? assemblyDir
            : AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var currentDriverSearch = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var driverSearchRoot = ResolvePlaywrightDriverSearchRoot(searchRoot);

        if (driverSearchRoot != null)
        {
            // Package includes .playwright/node under search root or under wwwroot (some deploy layouts).
            if (!string.Equals(currentDriverSearch, driverSearchRoot, StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", driverSearchRoot);
        }
        else if (string.IsNullOrEmpty(currentDriverSearch))
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", searchRoot);
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")))
        {
            var bundledBrowsers = Path.Combine(searchRoot, "pw-browsers");
            if (Directory.Exists(bundledBrowsers) && Directory.EnumerateFileSystemEntries(bundledBrowsers).Any())
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", bundledBrowsers);
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")))
            return;

        var baseDir = QuoteFilesPaths.IsAzureAppService
            ? QuoteFilesPaths.ResolveAzurePersistentHome(env)
            : Environment.GetEnvironmentVariable("HOME")
              ?? Environment.GetEnvironmentVariable("USERPROFILE")
              ?? env.ContentRootPath;

        var dir = Path.Combine(baseDir, "playwright-browsers");
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", dir);
    }

    /// <summary>
    /// Directory that should be PLAYWRIGHT_DRIVER_SEARCH_PATH (parent of .playwright), or null if bundle missing.
    /// </summary>
    private static string? ResolvePlaywrightDriverSearchRoot(string searchRoot)
    {
        if (Directory.Exists(Path.Combine(searchRoot, ".playwright", "node")))
            return searchRoot;
        var nested = Path.Combine(searchRoot, "wwwroot");
        if (Directory.Exists(Path.Combine(nested, ".playwright", "node")))
            return nested;
        return null;
    }

    /// <summary>
    /// Returns true when <c>chrome.exe</c> (Windows) or a Playwright Chromium <c>chrome</c> binary exists under
    /// <c>PLAYWRIGHT_BROWSERS_PATH</c>. In that case we skip <see cref="TryProbePlaywrightAsync"/> so the app pays
    /// one Chromium launch in <see cref="PlaywrightQuotePdfRenderer"/> instead of probe + shared browser.
    /// </summary>
    public static bool IsBundledChromiumExecutablePresent()
    {
        var browsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (string.IsNullOrEmpty(browsers) || !Directory.Exists(browsers))
            return false;

        try
        {
            if (OperatingSystem.IsWindows())
                return Directory.EnumerateFiles(browsers, "chrome.exe", SearchOption.AllDirectories).Any();

            return Directory.EnumerateFiles(browsers, "chrome", SearchOption.AllDirectories)
                .Any(p => string.Equals(Path.GetFileName(p), "chrome", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Probes Playwright; on driver/browser errors runs <c>install chromium</c> once per process (serialized).
    /// Skips the probe launch when a bundled Chromium executable is already on disk (see <see cref="IsBundledChromiumExecutablePresent"/>).
    /// </summary>
    public static async Task EnsureChromiumReadyAsync(ILogger? logger, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (IsBundledChromiumExecutablePresent())
        {
            sw.Stop();
            logger?.LogInformation(
                "[Playwright] Bundled Chromium on disk; skipping probe launch. bootstrapMs={Ms} {Summary}",
                sw.ElapsedMilliseconds,
                GetStartupBrowserPathSummary());
            return;
        }

        if ((await TryProbePlaywrightAsync(cancellationToken).ConfigureAwait(false)).Ok)
        {
            sw.Stop();
            logger?.LogInformation("[Playwright] Probe launch succeeded. bootstrapMs={Ms}", sw.ElapsedMilliseconds);
            return;
        }

        sw.Stop();
        logger?.LogWarning("[Playwright] Probe failed after {Ms} ms; may run install.", sw.ElapsedMilliseconds);

        await InstallGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var recheck = await TryProbePlaywrightAsync(cancellationToken).ConfigureAwait(false);
            if (recheck.Ok)
                return;

            if (_installAttempted)
            {
                logger?.LogWarning(
                    "[Playwright] Install already ran this process; probe still failing: {Reason}. {Diag}",
                    recheck.ErrorDetail ?? "(unknown)",
                    BuildPlaywrightDiagSnapshot());
                return;
            }

            logger?.LogWarning(
                "[Playwright] Chromium probe failed before install: {Reason}. {Diag}",
                recheck.ErrorDetail ?? "(unknown)",
                BuildPlaywrightDiagSnapshot());

            _installAttempted = true;
            logger?.LogWarning("[Playwright] Running bundled installer: install chromium (may take a few minutes on first run)...");

            var (installExitCode, installOutput) = await Task.Run(
                () => RunInstallChromiumWithTimeout(TimeSpan.FromMinutes(10)),
                cancellationToken).ConfigureAwait(false);

            if (installExitCode != 0)
            {
                logger?.LogError(
                    "[Playwright] install chromium exited with code {Code}. Console output: {Output}",
                    installExitCode,
                    string.IsNullOrWhiteSpace(installOutput) ? "(none captured — CLI may write directly to stderr)" : installOutput);
            }
            else
                logger?.LogInformation("[Playwright] install chromium completed successfully.");

            var after = await TryProbePlaywrightAsync(cancellationToken).ConfigureAwait(false);
            if (!after.Ok)
            {
                logger?.LogError(
                    "[Playwright] Still not usable after install: {Reason}. {Diag}",
                    after.ErrorDetail ?? "(unknown)",
                    BuildPlaywrightDiagSnapshot());
            }
        }
        finally
        {
            InstallGate.Release();
        }
    }

    /// <summary>
    /// Windows Azure: prefer <c>playwright.ps1 install chromium</c> in a child process so we can kill it on timeout.
    /// Other hosts: <see cref="Microsoft.Playwright.Program.Main"/> (no hard kill).
    /// </summary>
    private static (int Code, string? Output) RunInstallChromiumWithTimeout(TimeSpan timeout)
    {
        if (OperatingSystem.IsWindows())
        {
            var asmDir = Path.GetDirectoryName(typeof(Playwright).Assembly.Location);
            if (!string.IsNullOrEmpty(asmDir))
            {
                var ps1 = Path.Combine(asmDir, "playwright.ps1");
                if (File.Exists(ps1))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{ps1}\" install chromium",
                            WorkingDirectory = asmDir,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables())
                        {
                            var k = kv.Key?.ToString();
                            if (string.IsNullOrEmpty(k)) continue;
                            psi.Environment[k] = kv.Value?.ToString() ?? "";
                        }

                        using var proc = Process.Start(psi);
                        if (proc == null)
                            return (-1, "Could not start powershell for playwright install.");

                        var completed = proc.WaitForExit((int)Math.Clamp(timeout.TotalMilliseconds, 1, int.MaxValue));
                        if (!completed)
                        {
                            try
                            {
                                proc.Kill(entireProcessTree: true);
                            }
                            catch
                            {
                                // ignored
                            }

                            return (-2, $"playwright install chromium exceeded {timeout.TotalMinutes:0} minute timeout and was terminated.");
                        }

                        var stdout = proc.StandardOutput.ReadToEnd();
                        var stderr = proc.StandardError.ReadToEnd();
                        var combined = string.Join(Environment.NewLine, new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                        return (proc.ExitCode, string.IsNullOrEmpty(combined) ? null : combined);
                    }
                    catch (Exception ex)
                    {
                        return (-1, $"PowerShell install failed: {ex.Message}");
                    }
                }
            }
        }

        return RunInstallChromiumWithCapturedConsole();
    }

    private static (int Code, string? Output) RunInstallChromiumWithCapturedConsole()
    {
        var sb = new StringBuilder();
        var writer = new StringWriter(sb);
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        int code;
        try
        {
            Console.SetOut(writer);
            Console.SetError(writer);
            code = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }

        var text = sb.ToString().Trim();
        return (code, string.IsNullOrEmpty(text) ? null : text);
    }

    /// <summary>
    /// One-line summary for startup logs: effective paths and whether <c>chrome.exe</c> exists under the browser directory.
    /// </summary>
    public static string GetStartupBrowserPathSummary()
    {
        var driverSearch = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH") ?? "(null)";
        var browsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? "(null)";
        string chromium = "(unknown)";
        try
        {
            if (!string.IsNullOrEmpty(browsers) && Directory.Exists(browsers))
            {
                var chrome = Directory.GetFiles(browsers, "chrome.exe", SearchOption.AllDirectories).FirstOrDefault();
                chromium = string.IsNullOrEmpty(chrome) ? "(no chrome.exe under PLAYWRIGHT_BROWSERS_PATH)" : chrome;
            }
        }
        catch
        {
            chromium = "(error enumerating browsers path)";
        }

        return
            $"PLAYWRIGHT_DRIVER_SEARCH_PATH={driverSearch}; PLAYWRIGHT_BROWSERS_PATH={browsers}; Chromium={chromium}";
    }

    private static string BuildPlaywrightDiagSnapshot()
    {
        var driverSearch = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH") ?? "(null)";
        var browsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ?? "(null)";
        var asmDir = Path.GetDirectoryName(typeof(Playwright).Assembly.Location) ?? "(null)";
        var bundled = Path.Combine(asmDir, ".playwright");
        var nestedBundled = Path.Combine(asmDir, "wwwroot", ".playwright");
        var hasNode = Directory.Exists(Path.Combine(bundled, "node"));
        var hasNestedNode = Directory.Exists(Path.Combine(nestedBundled, "node"));
        return
            $"{GetStartupBrowserPathSummary()}; " +
            $"PlaywrightAssemblyDir={asmDir}; .playwright/node at root={hasNode}; .playwright/node under wwwroot={hasNestedNode}";
    }

    private static async Task<(bool Ok, string? ErrorDetail)> TryProbePlaywrightAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var pw = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-dev-shm-usage" },
                // Without this, LaunchAsync can hang indefinitely on App Service if Chromium is wedged.
                Timeout = 120_000
            }).ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
