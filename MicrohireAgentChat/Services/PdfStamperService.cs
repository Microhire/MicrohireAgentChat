// Services/PdfStamperService.cs
using MicrohireAgentChat.Helpers;
using System.Linq;
using iText.Bouncycastleconnector;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Commons.Bouncycastle;
namespace MicrohireAgentChat.Services
{
    public record QuoteFields(
    string Client,
    string ContactName,
    string Email,
    string EventDateLine,   // e.g., "Fri 17 Oct 2025 — Setup 07:30 — Event 08:00 to 17:00"
    string Reference,       // e.g., "C1374000002 - 001"
    string VisionTotal,     // e.g., "$619.10"
    string AudioTotal,      // e.g., "$584.42"
    string TotalIncGst      // e.g., "$1,879.76 inc GST"
);
    public class PdfStamperService
    {
        private readonly IWebHostEnvironment _env;
        public PdfStamperService(IWebHostEnvironment env) => _env = env;

        private static string SanitizeFilenameSegment(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var segment = new string(value.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
            return segment.Length > 0 ? segment : "";
        }

        public (string fileName, string fullPath) Stamp(QuoteFields q)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var src = Path.Combine(webRoot, "files", "quotes", "Quote-TEMPLATE.pdf");
            if (!File.Exists(src))
                throw new FileNotFoundException("Template not found at /wwwroot/files/quotes/Quote-TEMPLATE.pdf", src);

            var outDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var identifier = SanitizeFilenameSegment(q.Reference);
            var outName = string.IsNullOrEmpty(identifier)
                ? $"Quote-{timestamp}.pdf"
                : $"Quote-{identifier}-{timestamp}.pdf";
            var dest = Path.Combine(outDir, outName);
            //_ = BouncyCastleFactoryCreator.GetInstance();
            using var reader = new PdfReader(src);
            using var writer = new PdfWriter(dest);
            using var pdf = new PdfDocument(reader, writer);

            // Fonts
            PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

            // -------- Page 1 --------
            {
                var page1 = pdf.GetPage(1);
                var c = new PdfCanvas(page1);
                c.BeginText()
                 .SetFontAndSize(font, 10)
                 .SetColor(ColorConstants.BLACK, true) // <-- fill (non-stroking) color for text
                 .MoveText(85, 695).ShowText(q.Client)
                 .MoveText(0, -16).ShowText(q.ContactName)
                 .MoveText(0, -16).ShowText(q.Email)
                 .MoveText(240, 48).ShowText(q.EventDateLine)
                 .MoveText(150, -560).ShowText($"Ref No: {q.Reference}")
                 .EndText();
            }

            // -------- Page 2 --------
            if (pdf.GetNumberOfPages() >= 2)
            {
                var page2 = pdf.GetPage(2);
                var c2 = new PdfCanvas(page2);
                c2.BeginText()
                  .SetFontAndSize(font, 10)
                  .SetColor(ColorConstants.BLACK, true) // <-- fill color
                  .MoveText(475, 190).ShowText(q.VisionTotal)
                  .MoveText(475, 85).ShowText(q.AudioTotal)
                  .EndText();
            }

            // -------- Page 4 --------
            if (pdf.GetNumberOfPages() >= 4)
            {
                var page4 = pdf.GetPage(4);
                var c4 = new PdfCanvas(page4);
                c4.BeginText()
                  .SetFontAndSize(fontBold, 11)
                  .SetColor(ColorConstants.BLACK, true) // <-- fill color
                  .MoveText(465, 210).ShowText(q.TotalIncGst.Replace(" inc GST", ""))
                  .EndText();
            }

            // -------- Page 5 --------
            if (pdf.GetNumberOfPages() >= 5)
            {
                var page5 = pdf.GetPage(5);
                var c5 = new PdfCanvas(page5);
                c5.BeginText()
                  .SetFontAndSize(fontBold, 11)
                  .SetColor(ColorConstants.BLACK, true) // <-- fill color
                  .MoveText(160, 480).ShowText(q.TotalIncGst)
                  .MoveText(170, -36).ShowText(q.Reference)
                  .EndText();
            }

            pdf.Close();
            return (outName, dest);
        }
    }
}
