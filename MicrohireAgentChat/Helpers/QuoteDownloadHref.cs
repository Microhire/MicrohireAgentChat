namespace MicrohireAgentChat.Helpers;

/// <summary>
/// Builds stable PDF download URLs for quote HTML paths (sibling .pdf next to .html; files live under wwwroot locally or %HOME%/data/quotes on Azure).
/// </summary>
public static class QuoteDownloadHref
{
    /// <summary>
    /// Returns <c>/quotes/download?file=...</c> using the actual on-disk PDF name (same basename as the HTML file).
    /// </summary>
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
        string pdfFileName;
        if (!string.IsNullOrEmpty(htmlFileName) &&
            htmlFileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            pdfFileName = Path.ChangeExtension(htmlFileName, ".pdf");
        }
        else
        {
            var safeBooking = string.IsNullOrWhiteSpace(bookingNo)
                ? DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                : bookingNo.Trim();
            pdfFileName = $"Quote-{safeBooking}.pdf";
        }

        return $"/quotes/download?file={Uri.EscapeDataString(pdfFileName)}";
    }

    /// <summary>Filename only (for <c>download</c> / <c>data-download-name</c>), matching the sibling PDF of the quote HTML.</summary>
    public static string GetPdfFileName(string quoteUrl, string? bookingNo)
    {
        if (string.IsNullOrWhiteSpace(quoteUrl))
            return "Quote.pdf";

        var path = quoteUrl.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            path = absolute.AbsolutePath;

        if (!path.StartsWith("/", StringComparison.Ordinal))
            path = "/" + path.TrimStart('/');

        var htmlFileName = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(htmlFileName) &&
            htmlFileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            return Path.ChangeExtension(htmlFileName, ".pdf");

        var safeBooking = string.IsNullOrWhiteSpace(bookingNo)
            ? "quote"
            : bookingNo.Trim();
        return $"Quote-{safeBooking}.pdf";
    }
}
