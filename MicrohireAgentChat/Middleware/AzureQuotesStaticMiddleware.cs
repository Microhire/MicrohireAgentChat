using MicrohireAgentChat.Helpers;
using Microsoft.AspNetCore.Hosting;

namespace MicrohireAgentChat.Middleware;

/// <summary>
/// On Azure App Service, serves <c>/files/quotes/*</c> from <c>%HOME%/data/quotes</c> because generated files are not under read-only <c>wwwroot</c>.
/// </summary>
public sealed class AzureQuotesStaticMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public AzureQuotesStaticMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!QuoteFilesPaths.IsAzureAppService)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/files/quotes/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var encoded = path["/files/quotes/".Length..];
        if (string.IsNullOrEmpty(encoded))
        {
            await _next(context);
            return;
        }

        string fileName;
        try
        {
            fileName = Uri.UnescapeDataString(encoded);
        }
        catch
        {
            await _next(context);
            return;
        }

        fileName = Path.GetFileName(fileName);
        if (!QuoteFilesPaths.IsSafeQuoteFileName(fileName))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var physicalDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
        var fullPath = Path.GetFullPath(Path.Combine(physicalDir, fileName));
        if (!fullPath.StartsWith(Path.GetFullPath(physicalDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!File.Exists(fullPath))
        {
            await _next(context);
            return;
        }

        var contentType = fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? "text/html; charset=utf-8"
            : fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? "application/json; charset=utf-8"
                : "application/pdf";

        context.Response.ContentType = contentType;
        context.Response.Headers.CacheControl = "private, max-age=60";
        await context.Response.SendFileAsync(fullPath);
    }
}
