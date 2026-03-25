namespace MicrohireAgentChat.Services;

/// <summary>
/// Renders quote HTML to PDF using a process-wide Chromium instance (one browser, many pages).
/// </summary>
public interface IPlaywrightQuotePdfRenderer
{
    Task<bool> GeneratePdfFromHtmlAsync(
        string html,
        string pdfOutputPath,
        ILogger? logger = null,
        CancellationToken cancellationToken = default,
        string? quoteTraceId = null);
}
