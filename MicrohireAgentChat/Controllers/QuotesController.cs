using System.Diagnostics;
using MicrohireAgentChat.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace MicrohireAgentChat.Controllers;

/// <summary>Quote file helpers and diagnostics. PDF generation has been removed; quotes are HTML only.</summary>
public sealed class QuotesController : Controller
{
    private readonly IWebHostEnvironment _env;

    public QuotesController(IWebHostEnvironment env) => _env = env;

    /// <summary>Legacy PDF render URL: redirects to the HTML quote if <paramref name="src"/> resolves.</summary>
    [HttpGet("/quotes/render")]
    public IActionResult RenderPdf([FromQuery] string src)
    {
        if (string.IsNullOrWhiteSpace(src))
            return BadRequest("src is required");
        if (QuoteFilesPaths.TryResolveExistingQuoteFile(_env, src, out var srcPath) && System.IO.File.Exists(srcPath))
        {
            var rel = src.Trim();
            if (!rel.StartsWith("/", StringComparison.Ordinal))
                rel = "/" + rel.TrimStart('/');
            return Redirect(rel);
        }
        return NotFound("HTML quote not found");
    }

    /// <summary>Legacy download URL: if a sibling HTML exists for a requested .pdf name, redirect to it.</summary>
    [HttpGet("/quotes/download")]
    public IActionResult Download([FromQuery] string file)
    {
        var safe = Path.GetFileName(file ?? "");
        if (!QuoteFilesPaths.IsSafeQuoteFileName(safe))
            return NotFound();
        var dir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
        if (safe.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var htmlName = Path.ChangeExtension(safe, ".html");
            var htmlPath = Path.Combine(dir, htmlName);
            if (System.IO.File.Exists(htmlPath))
                return Redirect($"/files/quotes/{Uri.EscapeDataString(htmlName)}");
        }
        var path = Path.Combine(dir, safe);
        if (!System.IO.File.Exists(path))
            return NotFound();
        var contentType = safe.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? "text/html"
            : "application/octet-stream";
        return File(System.IO.File.OpenRead(path), contentType, fileDownloadName: safe);
    }

    [HttpGet("/quotes/test-pipeline")]
    public async Task<IActionResult> TestPipeline()
    {
        var result = new Dictionary<string, object?>();
        var quotesDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
        result["quotesDir"] = quotesDir;
        result["isAzure"] = QuoteFilesPaths.IsAzureAppService;

        var html = StaticQuoteHtml();
        var testName = $"Quote-TEST-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var htmlFileName = testName + ".html";
        var htmlPath = Path.Combine(quotesDir, htmlFileName);

        var htmlSw = Stopwatch.StartNew();
        try
        {
            await System.IO.File.WriteAllTextAsync(htmlPath, html);
            htmlSw.Stop();
            result["htmlOk"] = true;
            result["htmlPath"] = $"/files/quotes/{htmlFileName}";
            result["htmlBytes"] = new FileInfo(htmlPath).Length;
            result["htmlMs"] = htmlSw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            htmlSw.Stop();
            result["htmlOk"] = false;
            result["htmlMs"] = htmlSw.ElapsedMilliseconds;
            result["htmlError"] = ex.Message;
        }

        result["pdfOk"] = false;
        result["pdfNote"] = "PDF pipeline removed; HTML quotes only.";

        return Json(result);
    }

    private static string StaticQuoteHtml()
    {
        var css = """
    <style>
      body { font-family: Arial, Helvetica, sans-serif; color:#111; margin:24px; }
      h1,h2,h3 { margin: 0 0 12px; }
      h1 { font-size: 22px; letter-spacing: .3px; }
      h2 { font-size: 16px; margin-top: 24px; border-bottom: 1px solid #e5e5e5; padding-bottom: 6px; }
      p  { margin: 6px 0 10px; }
      .muted { color:#555; }
      .grid { display:grid; grid-template-columns: 180px 1fr; gap:8px 16px; }
      .card { border:1px solid #e5e5e5; border-radius:10px; padding:14px; margin:10px 0; }
      table { width:100%; border-collapse:collapse; margin: 6px 0 14px; }
      th, td { border-bottom:1px solid #eee; padding:8px 6px; text-align:left; vertical-align:top; }
      th { font-weight:600; font-size: 13px; }
      tfoot td { border-top:1px solid #ddd; font-weight:600; }
      .right { text-align:right; }
      .small { font-size:12px; }
      .totals td { border:none; padding:4px 0; }
      .totals .line { border-bottom:1px dashed #ddd; }
      a { color:#0a58ca; text-decoration:none; }
    </style>
    """;

        return $"""
    <!doctype html><html><head><meta charset="utf-8"><title>Microhire | ARCSOPT Meeting</title>{css}</head><body>
      <h1>ARCSOPT MEETING — Proposal</h1>
      <div class="grid">
        <div>Client</div><div><strong>ARCSOPT</strong> — Megan Suurenbroek, admin@arcsopt.org</div>
        <div>Venue</div><div><strong>The Westin Brisbane</strong>, 111 Mary Street Brisbane City QLD 4000 — Room: <strong>Westin Ballroom I</strong></div>
        <div>Dates</div><div>Fri 17 Oct 2025 — Setup 07:30, Rehearsal 07:30, Event 08:00 to 17:00</div>
        <div>Account Manager</div><div>Nishal Kumar · +61 04 84814633 · nishal.kumar@microhire.com.au</div>
        <div>Reference</div><div>C1374000002 - 001</div>
      </div>
      <p class="muted small">Pipeline test page (HTML only).</p>
    </body></html>
    """;
    }
}
