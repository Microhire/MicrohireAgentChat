using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace MicrohireAgentChat.Controllers
{
    public class QuotesController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<QuotesController> _logger;
        private readonly IHostApplicationLifetime _hostLifetime;
        private readonly IPlaywrightQuotePdfRenderer _quotePdf;

        public QuotesController(
            IWebHostEnvironment env,
            ILogger<QuotesController> logger,
            IHostApplicationLifetime hostLifetime,
            IPlaywrightQuotePdfRenderer quotePdf)
        {
            _env = env;
            _logger = logger;
            _hostLifetime = hostLifetime;
            _quotePdf = quotePdf;
        }

        [HttpGet("/quotes/render")]
        public async Task<IActionResult> RenderPdf([FromQuery] string src, [FromQuery] string outFile)
        {
            if (string.IsNullOrWhiteSpace(src)) return BadRequest("src is required");
            if (string.IsNullOrWhiteSpace(outFile)) outFile = $"quote-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            outFile = Path.GetFileName(outFile);
            if (!outFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                outFile += ".pdf";

            if (!QuoteFilesPaths.TryResolveExistingQuoteFile(_env, src, out var srcPath) || !System.IO.File.Exists(srcPath))
                return NotFound("HTML source not found");

            // 1. Serve pre-generated PDF if it exists alongside the HTML
            var preGenPdfPath = Path.ChangeExtension(srcPath, ".pdf");
            if (System.IO.File.Exists(preGenPdfPath))
            {
                _logger.LogInformation("Serving pre-generated PDF for src {Source}", src);
                var pdfStream = System.IO.File.OpenRead(preGenPdfPath);
                return File(pdfStream, "application/pdf", fileDownloadName: outFile);
            }

            // 2. Generate on-the-fly with Playwright (SetContentAsync for reliable font loading)
            var outDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
            var outPath = Path.Combine(outDir, outFile);

            var html = await System.IO.File.ReadAllTextAsync(srcPath);
            using var pdfTimeout = new CancellationTokenSource(HtmlQuoteGenerationService.PdfGenerationAbsoluteTimeout);
            using var pdfWork = CancellationTokenSource.CreateLinkedTokenSource(
                _hostLifetime.ApplicationStopping,
                pdfTimeout.Token);
            var generated = await _quotePdf.GeneratePdfFromHtmlAsync(html, outPath, _logger, pdfWork.Token, HttpContext.TraceIdentifier);

            if (!generated || !System.IO.File.Exists(outPath))
                return StatusCode(500, "PDF generation failed");

            _logger.LogInformation("Serving on-the-fly Playwright PDF for src {Source}", src);
            var stream = System.IO.File.OpenRead(outPath);
            return File(stream, "application/pdf", fileDownloadName: outFile);
        }

        [HttpGet("/quotes/render-static")]
        public async Task<IActionResult> RenderStatic([FromQuery] string? outFile)
        {
            var html = StaticQuoteHtml(); // your existing method that returns the full HTML string
            if (string.IsNullOrWhiteSpace(outFile)) outFile = $"quote-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

            var outDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);
            var outPath = Path.Combine(outDir, outFile);

            using var pdfTimeout2 = new CancellationTokenSource(HtmlQuoteGenerationService.PdfGenerationAbsoluteTimeout);
            using var pdfWork2 = CancellationTokenSource.CreateLinkedTokenSource(
                _hostLifetime.ApplicationStopping,
                pdfTimeout2.Token);
            var generated = await _quotePdf.GeneratePdfFromHtmlAsync(html, outPath, _logger, pdfWork2.Token, HttpContext.TraceIdentifier);
            if (!generated || !System.IO.File.Exists(outPath))
                return StatusCode(500, "PDF generation failed");

            var stream = System.IO.File.OpenRead(outPath);
            return File(stream, "application/pdf", fileDownloadName: outFile); // forces download
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

      <div class="card">
        <p>Dear Megan,</p>
        <p class="muted small">
          Thank you for the opportunity to present our audio-visual production services for your upcoming event at The Westin Brisbane.
          Based on the information received, we recommend the following for a smooth event.
        </p>
      </div>

      <h2>Equipment & Services</h2>

      <h3>Vision</h3>
      <table>
        <thead><tr><th>Description</th><th class="right">Qty</th><th class="right">Line Total</th></tr></thead>
        <tbody>
          <tr><td>Westin Ballroom Single Projector Package<br><span class="small muted">Full HD Digital Projector, 120" motorised 16:9 screen, HDMI, client laptop at lectern, wireless presenter</span></td><td class="right">1</td><td class="right">$619.10</td></tr>
        </tbody>
        <tfoot><tr><td colspan="2" class="right">Vision Total</td><td class="right">$619.10</td></tr></tfoot>
      </table>

      <h3>Audio</h3>
      <table>
        <thead><tr><th>Description</th><th class="right">Qty</th><th class="right">Line Total</th></tr></thead>
        <tbody>
          <tr><td>Ceiling Speaker System</td><td class="right">1</td><td class="right">$0.00</td></tr>
          <tr><td>6 Channel Audio Mixer</td><td class="right">1</td><td class="right">$0.00</td></tr>
          <tr><td>2× Wireless Handheld (Shure QLXD4 K52)</td><td class="right">2</td><td class="right">$584.42</td></tr>
        </tbody>
        <tfoot><tr><td colspan="2" class="right">Audio Total</td><td class="right">$584.42</td></tr></tfoot>
      </table>

      <h2>Technical Services</h2>
      <table>
        <thead><tr><th>Task</th><th>Start</th><th>Finish</th><th class="right">Hrs</th><th class="right">Total ($)</th></tr></thead>
        <tbody>
          <tr><td>AV Technician Setup</td><td>17/10/25 07:30</td><td>17/10/25 09:00</td><td class="right">1.5</td><td class="right">$165.00</td></tr>
          <tr><td>AV Technician Test & Connect</td><td>17/10/25 08:30</td><td>17/10/25 09:30</td><td class="right">1.0</td><td class="right">$110.00</td></tr>
          <tr><td>AV Technician Pack Down</td><td>17/10/25 18:00</td><td>17/10/25 19:00</td><td class="right">1.0</td><td class="right">$110.00</td></tr>
        </tbody>
        <tfoot><tr><td colspan="4" class="right">Labour Total</td><td class="right">$385.00</td></tr></tfoot>
      </table>

      <h2>Budget Summary</h2>
      <table class="totals">
        <tbody>
          <tr><td class="right" style="width:80%;">Rental Equipment</td><td class="right">$1,203.52</td></tr>
          <tr><td class="right">Labour</td><td class="right">$385.00</td></tr>
          <tr class="line"><td class="right">Service Charge</td><td class="right">$120.35</td></tr>
          <tr><td class="right">Sub Total (ex GST)</td><td class="right">$1,708.87</td></tr>
          <tr class="line"><td class="right">GST</td><td class="right">$170.89</td></tr>
          <tr><td class="right"><strong>Total</strong></td><td class="right"><strong>$1,879.76</strong></td></tr>
        </tbody>
      </table>
      <p class="small muted">Pricing valid until Wed 7 May 2025. Resources subject to availability at booking.</p>

      <h2>Confirmation of Services</h2>
      <div class="card small">
        <p>On behalf of ARCSOPT, I accept this proposal and wish to proceed. We understand equipment and personnel are not allocated until this is signed and returned. This proposal is subject to Microhire’s terms & conditions.</p>
        <p><a href="https://www.microhire.com.au/terms-conditions/">microhire.com.au/terms-conditions/</a></p>
        <p><strong>Total Quotation Amount:</strong> $1,879.76 inc GST · <strong>Reference:</strong> C1374000002 - 001</p>
      </div>
    </body></html>
    """;
        }
    }
}
