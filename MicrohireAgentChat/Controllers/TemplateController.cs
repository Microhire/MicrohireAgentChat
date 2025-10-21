using Microsoft.AspNetCore.Mvc;

namespace MicrohireAgentChat.Controllers
{
    [ApiController]
    public class TemplateController : Controller
    {
        // POST /quotes/install-template  (multipart/form-data: file=<pdf>)
        [HttpPost("/quotes/install-template")]
        public async Task<IActionResult> InstallTemplate([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Missing PDF file.");
            if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)) return BadRequest("Please upload a PDF.");

            var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var outDir = Path.Combine(webRoot, "files", "quotes");
            Directory.CreateDirectory(outDir);

            var outPath = Path.Combine(outDir, "Quote-TEMPLATE.pdf");
            using var fs = System.IO.File.Create(outPath);
            await file.CopyToAsync(fs);

            var url = $"{Request.Scheme}://{Request.Host}/files/quotes/Quote-TEMPLATE.pdf";
            return Ok(new { message = "Template installed", url });
        }
    }
}
