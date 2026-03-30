using System.Net;

namespace MicrohireAgentChat.Helpers;

/// <summary>
/// Builds download URLs for quote HTML. Legacy PDF paths redirect via <c>/quotes/download</c>.
/// </summary>
public static class QuoteDownloadHref
{
    /// <summary>Public path to the HTML quote file (same basename as the stored quote URL).</summary>
    public static string Build(string quoteUrl, string? bookingNo)
    {
        if (string.IsNullOrWhiteSpace(quoteUrl))
            return "#";

        var path = quoteUrl.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            path = absolute.AbsolutePath;

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path.TrimStart('/');

        var htmlFileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(htmlFileName))
        {
            var safeBooking = string.IsNullOrWhiteSpace(bookingNo)
                ? DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                : bookingNo.Trim();
            htmlFileName = $"Quote-{safeBooking}.html";
        }
        else if (!htmlFileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            htmlFileName = Path.ChangeExtension(htmlFileName, ".html");

        return $"/files/quotes/{Uri.EscapeDataString(htmlFileName)}";
    }

    public static string GetHtmlFileName(string quoteUrl, string? bookingNo)
    {
        if (string.IsNullOrWhiteSpace(quoteUrl))
            return "Quote.html";

        var path = quoteUrl.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            path = absolute.AbsolutePath;

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path.TrimStart('/');

        var htmlFileName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(htmlFileName) &&
            htmlFileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            return htmlFileName;

        var safeBooking = string.IsNullOrWhiteSpace(bookingNo)
            ? "quote"
            : bookingNo.Trim();
        return $"Quote-{safeBooking}.html";
    }

    public static string BuildPendingDownloadAnchor(string quoteUrl, string? bookingNo)
    {
        var name = WebUtility.HtmlEncode(GetHtmlFileName(quoteUrl, bookingNo));
        var href = WebUtility.HtmlEncode(Build(quoteUrl, bookingNo));
        return $"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"isla-quote-download\" data-quote-download=\"1\" data-download-name=\"{name}\"><i class=\"ph ph-download\"></i> download it</a>";
    }

    public static string BuildPendingSignedDownloadButton(string quoteUrl, string? bookingNo)
    {
        var name = WebUtility.HtmlEncode(GetHtmlFileName(quoteUrl, bookingNo));
        var href = WebUtility.HtmlEncode(Build(quoteUrl, bookingNo));
        return $"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer\" class=\"isla-quote-download-btn isla-quote-download\" data-quote-download=\"1\" data-download-name=\"{name}\">Download Signed Quote</a>";
    }
}
