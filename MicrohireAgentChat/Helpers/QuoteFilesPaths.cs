using Microsoft.AspNetCore.Hosting;

namespace MicrohireAgentChat.Helpers;

/// <summary>
/// Resolves where generated quote HTML/PDF (and related files) are stored and how they are exposed at <c>/files/quotes/...</c>.
/// On Azure App Service, outputs go to <c>%HOME%/data/quotes</c> (writable with Run-From-Package); locally they stay under <c>wwwroot/files/quotes</c>.
/// </summary>
public static class QuoteFilesPaths
{
    /// <summary>Azure App Service sets this on all app instances.</summary>
    public static bool IsAzureAppService =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

    /// <summary>
    /// Writable persistent root on Azure. Linux usually sets <c>HOME</c>; Windows App Service often does not,
    /// but persisted storage is still <c>D:\home</c>.
    /// </summary>
    public static string ResolveAzurePersistentHome(IWebHostEnvironment env)
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
            return home.Trim();

        if (OperatingSystem.IsWindows() && IsAzureAppService)
        {
            const string windowsAzureHome = @"D:\home";
            try
            {
                if (Directory.Exists(windowsAzureHome))
                    return windowsAzureHome;
            }
            catch
            {
                // ignore
            }
        }

        return Environment.GetEnvironmentVariable("USERPROFILE")
            ?? env.ContentRootPath;
    }

    /// <summary>Physical directory for quote outputs (created if missing).</summary>
    public static string GetPhysicalQuotesDirectory(IWebHostEnvironment env)
    {
        if (IsAzureAppService)
        {
            var dir = Path.Combine(ResolveAzurePersistentHome(env), "data", "quotes");
            Directory.CreateDirectory(dir);
            return dir;
        }

        var webRoot = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var local = Path.Combine(webRoot, "files", "quotes");
        Directory.CreateDirectory(local);
        return local;
    }

    /// <summary>Public URL path segment (always <c>/files/quotes/name</c> for links and session).</summary>
    public static string PublicUrlForFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        return $"/files/quotes/{Uri.EscapeDataString(safe)}";
    }

    /// <summary>
    /// Whether <paramref name="fileName"/> is allowed to be served from the quotes directory (no traversal, expected prefixes).
    /// </summary>
    public static bool IsSafeQuoteFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var name = Path.GetFileName(fileName);
        if (!string.Equals(name, fileName, StringComparison.Ordinal)) return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        if (!name.StartsWith("Quote-", StringComparison.OrdinalIgnoreCase)) return false;
        return name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves <paramref name="src"/> (URL path or relative path) to an existing quote HTML/PDF file on disk.
    /// Checks <c>wwwroot</c> first, then the active quotes directory (home data on Azure).
    /// </summary>
    public static bool TryResolveExistingQuoteFile(IWebHostEnvironment env, string src, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(src)) return false;

        var webRoot = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (TryResolveUnderWebRoot(src, webRoot, out var underWeb) && File.Exists(underWeb))
        {
            fullPath = underWeb;
            return true;
        }

        var path = src.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var abs))
            path = abs.AbsolutePath;
        path = path.TrimStart('/');
        const string p = "files/quotes/";
        if (!path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            return false;
        var encoded = path[p.Length..];
        if (string.IsNullOrEmpty(encoded)) return false;
        string name;
        try
        {
            name = Uri.UnescapeDataString(encoded);
        }
        catch
        {
            return false;
        }
        name = Path.GetFileName(name);
        if (!IsSafeQuoteFileName(name)) return false;

        var inActive = Path.GetFullPath(Path.Combine(GetPhysicalQuotesDirectory(env), name));
        if (File.Exists(inActive))
        {
            fullPath = inActive;
            return true;
        }

        return false;
    }

    private static bool TryResolveUnderWebRoot(string src, string webRoot, out string fullPath)
    {
        fullPath = string.Empty;
        var sourcePath = src.Trim();
        if (Uri.TryCreate(sourcePath, UriKind.Absolute, out var absoluteUri))
            sourcePath = absoluteUri.AbsolutePath;

        sourcePath = Uri.UnescapeDataString(sourcePath);
        sourcePath = sourcePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

        var rootFull = Path.GetFullPath(webRoot);
        var candidate = Path.GetFullPath(Path.Combine(rootFull, sourcePath));
        var rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            return false;

        fullPath = candidate;
        return true;
    }
}
