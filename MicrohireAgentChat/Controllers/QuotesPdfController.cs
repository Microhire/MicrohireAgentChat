using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Mvc;

namespace MicrohireAgentChat.Controllers
{
    public class QuotesPdfController : Controller
    {
        private readonly PdfStamperService _stamper;
        private readonly IWebHostEnvironment _env;

        public QuotesPdfController(PdfStamperService stamper, IWebHostEnvironment env)
        {
            _stamper = stamper;
            _env = env;
        }

        // POST /quotes/stamp   --> returns { url: "..." }
        [HttpPost("/quotes/stamp")]
        public IActionResult Stamp([FromBody] QuoteFields body)
        {
            var (name, path) = _stamper.Stamp(body);
            var url = $"{Request.Scheme}://{Request.Host}/files/quotes/{Uri.EscapeDataString(name)}";
            return Ok(new { url });
        }

        // GET /quotes/generate-static  --> quick test without payload
        [HttpGet("/quotes/generate-static")]
        public IActionResult GenerateStatic()
        {
            var fields = new QuoteFields(
                Client: "ARCSOPT",
                ContactName: "Megan Suurenbroek",
                Email: "admin@arcsopt.org",
                EventDateLine: "Fri 17 Oct 2025 — Setup 07:30 — Event 08:00 to 17:00",
                Reference: "C1374000002 - 001",
                VisionTotal: "$619.10",
                AudioTotal: "$584.42",
                TotalIncGst: "$1,879.76 inc GST"
            );

            var (name, _) = _stamper.Stamp(fields);
            var url = $"{Request.Scheme}://{Request.Host}/files/quotes/{Uri.EscapeDataString(name)}";
            return Ok(new { url });
        }

        // Optional helpers
        [HttpGet("/quotes/preview")]
        public IActionResult Preview([FromQuery] string file)
        {
            var safe = Path.GetFileName(file);
            if (!QuoteFilesPaths.IsSafeQuoteFileName(safe))
                return NotFound();
            var path = Path.Combine(QuoteFilesPaths.GetPhysicalQuotesDirectory(_env), safe);
            if (!System.IO.File.Exists(path)) return NotFound();
            return File(System.IO.File.OpenRead(path), "application/pdf");
        }

        [HttpGet("/quotes/download")]
        public IActionResult Download([FromQuery] string file)
        {
            var safe = Path.GetFileName(file);
            if (!QuoteFilesPaths.IsSafeQuoteFileName(safe))
                return NotFound();
            var path = Path.Combine(QuoteFilesPaths.GetPhysicalQuotesDirectory(_env), safe);
            if (!System.IO.File.Exists(path)) return NotFound();
            return File(System.IO.File.OpenRead(path), "application/pdf", fileDownloadName: safe);
        }
    }
}
