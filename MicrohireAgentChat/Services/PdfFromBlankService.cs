// Services/PdfFromBlankService.cs
using System.Linq;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Hosting;

public class PdfFromBlankService
{
    private readonly IWebHostEnvironment _env;
    public PdfFromBlankService(IWebHostEnvironment env) => _env = env;

    // Template page layout constants (A4: 595 x 842 points)
    // The BLANK template has:
    // - Page 1: Full red cover design with white bar at y~168 for title
    // - Pages 2+: Content pages with red arrow at top-left, red line at y~755
    
    // Page 1 (Cover) measurements
    private const float COVER_TITLE_Y = 168f;        // Center of white bar
    private const float COVER_TITLE_X = 85f;         // Left edge of white bar
    private const float COVER_TITLE_WIDTH = 425f;    // Width of white bar
    
    // Pages 2+ (Content pages) - IMPORTANT: red line is at y~755
    // Content must start BELOW the red line
    private const float CONTENT_LEFT = 50f;
    private const float CONTENT_RIGHT = 545f;
    private const float SECTION_TITLE_Y = 770f;      // Section title ABOVE the red line
    private const float EVENT_TITLE_Y = 745f;        // Event title just at the red line
    private const float CONTENT_START_Y = 710f;      // Main content starts BELOW red line
    private const float FOOTER_Y = 35f;              // Footer reference position

    /// <summary>Sanitize a string for use as a filename segment (e.g. from Reference).</summary>
    private static string SanitizeFilenameSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var segment = new string(value.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return segment.Length > 0 ? segment : "";
    }

    public (string fileName, string fullPath) Generate(QuoteAllFields q, string? bookingNo = null)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var outDir = Path.Combine(webRoot, "files", "quotes");
        Directory.CreateDirectory(outDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var identifier = !string.IsNullOrEmpty(bookingNo)
            ? bookingNo
            : SanitizeFilenameSegment(q.Reference);
        var outName = string.IsNullOrEmpty(identifier)
            ? $"Quote-{timestamp}.pdf"
            : $"Quote-{identifier}-{timestamp}.pdf";
        var dest = Path.Combine(outDir, outName);

        var src = Path.Combine(webRoot, "files", "quotes", "Quote-BLANK-TEMPLATE.pdf");
        if (!File.Exists(src))
        {
            GenerateSimplePdfFallback(dest, q);
            return (outName, dest);
        }

        using var reader = new PdfReader(src);
        using var writer = new PdfWriter(dest);
        using var pdf = new PdfDocument(reader, writer);

        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var redBright = new DeviceRgb(200, 30, 30);
        var redDark = new DeviceRgb(150, 20, 20);

        // ========================= PAGE 1: COVER =========================
        {
            var c = new PdfCanvas(pdf.GetPage(1));
            
            // Event title in the white bar (centered)
            var titleText = q.EventTitle?.ToUpper() ?? "EVENT";
            var titleWidth = bold.GetWidth(titleText, 26f);
            var titleX = COVER_TITLE_X + (COVER_TITLE_WIDTH - titleWidth) / 2;
            Text(c, bold, 26f, titleX, COVER_TITLE_Y, titleText, redDark);
            
            // Reference at bottom
            Text(c, font, 9f, CONTENT_LEFT, FOOTER_Y, $"Ref No: {q.Reference}");
        }

        // ========================= PAGE 2: OVERVIEW =========================
        if (pdf.GetNumberOfPages() >= 2)
        {
            var c = new PdfCanvas(pdf.GetPage(2));

            // Header (above red line)
            Text(c, bold, 12f, CONTENT_LEFT, SECTION_TITLE_Y, "OVERVIEW", redDark);
            Text(c, bold, 20f, CONTENT_LEFT + 15f, EVENT_TITLE_Y, q.EventTitle?.ToUpper() ?? "", redBright);

            // Contact block (right side) - stays at top
            float contactX = 400f;
            float contactY = CONTENT_START_Y;
            
            if (!string.IsNullOrWhiteSpace(q.ContactName))
            { Text(c, font, 10f, contactX, contactY, q.ContactName); contactY -= 14f; }
            if (!string.IsNullOrWhiteSpace(q.Client))
            { Text(c, font, 10f, contactX, contactY, q.Client); contactY -= 14f; }
            if (!string.IsNullOrWhiteSpace(q.Email))
            { Text(c, font, 10f, contactX, contactY, q.Email); contactY -= 18f; }

            var firstName = string.IsNullOrWhiteSpace(q.ContactName) ? "Client" : q.ContactName.Trim().Split(' ')[0];
            Text(c, font, 10f, contactX, contactY, $"Dear {firstName},");
            
            // LEFT COLUMN - All content starts at same level as "Dear [Name],"
            float rowH = 14f;
            float labX = CONTENT_LEFT;
            float valX = 190f;
            float y = contactY; // Start at same Y as "Dear Megan,"
            
            // 1. Location details
            y -= 5f; // Small gap after "Dear..."
            DetailRow(c, bold, font, labX, valX, y, "LOCATION:", q.Location); y -= rowH;
            DetailRow(c, bold, font, labX, valX, y, "ADDRESS:", q.Address); y -= rowH;
            DetailRow(c, bold, font, labX, valX, y, "ROOM:", q.Room); y -= rowH;
            DetailRow(c, bold, font, labX, valX, y, "DATE RANGE:", q.DateRange); y -= rowH;
            y -= 8f; // blank line
            
            // 2. Contact details
            DetailRow(c, bold, font, labX, valX, y, "DELIVERY CONTACT:", q.DeliveryContact); y -= rowH;
            DetailRow(c, bold, font, labX, valX, y, "ACCOUNT MANAGER:", q.AccountMgrName); y -= rowH;
            DetailRow(c, bold, font, labX, valX, y, "MOBILE:", q.AccountMgrMobile); y -= rowH;
            DetailRow(c, bold, font, labX, valX, y, "EMAIL:", q.AccountMgrEmail); y -= rowH;
            y -= 12f; // gap before intro
            
            // 3. Intro paragraph
            var introBox = new iText.Kernel.Geom.Rectangle(CONTENT_LEFT, y - 80f, 320f, 80f);
            using (var canv = new Canvas(new PdfCanvas(pdf.GetPage(2)), introBox))
            {
                canv.Add(new Paragraph(
                    "Thank you for the opportunity to present our audio-visual production services for your upcoming event. " +
                    "We are pleased to provide our proposal in the following pages, based on the information we have received " +
                    "and our recommendations for a seamless and successful event. Our team are committed to achieving your event " +
                    "objectives and welcome the opportunity to discuss your requirements and any budget parameters in further detail.")
                    .SetFont(font).SetFontSize(9f).SetFixedLeading(11.5f)
                    .SetFontColor(ColorConstants.DARK_GRAY)
                    .SetTextAlignment(TextAlignment.JUSTIFIED)
                    .SetMargin(0).SetPadding(0));
            }
            y -= 95f; // after intro paragraph
            
            // 4. Schedule confirmation
            Text(c, font, 9f, CONTENT_LEFT, y, "Please confirm the following dates and times are accurate:", ColorConstants.DARK_GRAY);
            y -= 18f;
            TimeRow(c, bold, font, CONTENT_LEFT, y, "SETUP BY:", q.SetupDate, "Time: " + q.SetupTime); y -= rowH;
            TimeRow(c, bold, font, CONTENT_LEFT, y, "REHEARSAL:", q.RehearsalDate, "Time: " + q.RehearsalTime); y -= rowH;
            TimeRow(c, bold, font, CONTENT_LEFT, y, "EVENT START:", q.EventStartDate, "Time: " + q.EventStartTime); y -= rowH;
            TimeRow(c, bold, font, CONTENT_LEFT, y, "EVENT END:", q.EventEndDate, "Time: " + q.EventEndTime);

            Text(c, font, 9f, CONTENT_LEFT, FOOTER_Y, $"Ref No: {q.Reference}");
        }

        // ========================= PAGE 3: EQUIPMENT & SERVICES =========================
        if (pdf.GetNumberOfPages() >= 3)
        {
            var c = new PdfCanvas(pdf.GetPage(3));

            Text(c, bold, 12f, CONTENT_LEFT, SECTION_TITLE_Y, "EQUIPMENT & SERVICES", redDark);
            Text(c, bold, 20f, CONTENT_LEFT + 15f, EVENT_TITLE_Y, q.EventTitle?.ToUpper() ?? "", redBright);

            float y = CONTENT_START_Y;

            // Room header
            if (!string.IsNullOrWhiteSpace(q.RoomNoteHeader))
            {
                Text(c, bold, 10f, CONTENT_LEFT, y, q.RoomNoteHeader); y -= 14f;
                Text(c, font, 9f, CONTENT_LEFT, y, q.RoomNoteStarts ?? ""); y -= 12f;
                Text(c, font, 9f, CONTENT_LEFT, y, q.RoomNoteEnds ?? ""); y -= 14f;
                Text(c, bold, 10f, CONTENT_LEFT, y, $"{q.RoomNoteHeader} Total");
                TextRight(c, bold, 10f, CONTENT_RIGHT, y, q.RoomNoteTotal ?? "$0.00");
                y -= 25f;
            }

            // Vision section
            if (q.VisionRows?.Any() == true)
            {
                Text(c, bold, 11f, CONTENT_LEFT, y, "VISION", redDark); y -= 16f;
                y = DrawEquipmentList(c, font, bold, CONTENT_LEFT, y, q.VisionRows);
                y -= 6f;
                Text(c, bold, 10f, CONTENT_LEFT, y, "Vision Total");
                TextRight(c, bold, 10f, CONTENT_RIGHT, y, q.VisionTotal ?? "$0.00");
                y -= 25f;
            }

            // Audio section
            if (q.AudioRows?.Any() == true)
            {
                Text(c, bold, 11f, CONTENT_LEFT, y, "AUDIO", redDark); y -= 16f;
                y = DrawEquipmentList(c, font, bold, CONTENT_LEFT, y, q.AudioRows);
                y -= 6f;
                Text(c, bold, 10f, CONTENT_LEFT, y, "Audio Total");
                TextRight(c, bold, 10f, CONTENT_RIGHT, y, q.AudioTotal ?? "$0.00");
                y -= 25f;
            }

            // Lighting section
            if (q.LightingRows?.Any() == true)
            {
                Text(c, bold, 11f, CONTENT_LEFT, y, "LIGHTING", redDark); y -= 16f;
                y = DrawEquipmentList(c, font, bold, CONTENT_LEFT, y, q.LightingRows);
                y -= 6f;
                Text(c, bold, 10f, CONTENT_LEFT, y, "Lighting Total");
                TextRight(c, bold, 10f, CONTENT_RIGHT, y, q.LightingTotal ?? "$0.00");
            }

            Text(c, font, 9f, CONTENT_LEFT, FOOTER_Y, $"Ref No: {q.Reference}");
        }

        // ========================= PAGE 4: TECHNICAL SERVICES =========================
        if (pdf.GetNumberOfPages() >= 4)
        {
            var c = new PdfCanvas(pdf.GetPage(4));

            Text(c, bold, 12f, CONTENT_LEFT, SECTION_TITLE_Y, "TECHNICAL SERVICES", redDark);
            Text(c, bold, 20f, CONTENT_LEFT + 15f, EVENT_TITLE_Y, q.EventTitle?.ToUpper() ?? "", redBright);

            float y = CONTENT_START_Y;

            // Table headers
            Text(c, bold, 8f, CONTENT_LEFT, y, "Description");
            Text(c, bold, 8f, 160f, y, "Task");
            Text(c, bold, 8f, 250f, y, "Qty");
            Text(c, bold, 8f, 280f, y, "Start");
            Text(c, bold, 8f, 380f, y, "Finish");
            Text(c, bold, 8f, 470f, y, "Hrs");
            Text(c, bold, 8f, 510f, y, "Total");
            y -= 18f;

            if (q.LabourRows?.Any() == true)
            {
                foreach (var row in q.LabourRows)
                {
                    var lines = Wrap(row.Description ?? "", 22).ToList();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        Text(c, font, 8f, CONTENT_LEFT, y, lines[i]);
                        if (i == 0)
                        {
                            Text(c, font, 8f, 160f, y, row.Task ?? "");
                            Text(c, font, 8f, 250f, y, row.Qty ?? "");
                            Text(c, font, 8f, 280f, y, row.Start ?? "");
                            Text(c, font, 8f, 380f, y, row.Finish ?? "");
                            Text(c, font, 8f, 470f, y, row.Hrs ?? "");
                            TextRight(c, font, 8f, CONTENT_RIGHT, y, row.Total ?? "");
                        }
                        y -= 12f;
                        if (y < 80) break;
                    }
                    y -= 4f;
                    if (y < 80) break;
                }
            }
            else
            {
                Text(c, font, 9f, CONTENT_LEFT, y, "No technical services required for this event.");
            }

            // Labour Total at fixed position
            Text(c, bold, 10f, CONTENT_LEFT, 80f, "Labour Total");
            TextRight(c, bold, 10f, CONTENT_RIGHT, 80f, q.LabourTotal ?? "$0.00");

            Text(c, font, 9f, CONTENT_LEFT, FOOTER_Y, $"Ref No: {q.Reference}");
        }

        // ========================= PAGE 5: BUDGET SUMMARY =========================
        if (pdf.GetNumberOfPages() >= 5)
        {
            var c = new PdfCanvas(pdf.GetPage(5));

            Text(c, bold, 12f, CONTENT_LEFT, SECTION_TITLE_Y, "BUDGET SUMMARY", redDark);
            Text(c, bold, 20f, CONTENT_LEFT + 15f, EVENT_TITLE_Y, q.EventTitle?.ToUpper() ?? "", redBright);

            // Terms (left column)
            float termsY = CONTENT_START_Y;
            Text(c, bold, 10f, CONTENT_LEFT, termsY, "TERMS & CONDITIONS"); termsY -= 18f;

            var terms = new[] {
                q.BudgetNotesTopLine ?? "The team at Microhire look forward to working with you to make every aspect of your event a success.",
                q.BudgetValidityLine ?? "To ensure that your event receives the best possible equipment and technical personnel, please confirm that all details are correct including dates, timing and quantities. Note that our pricing is valid for 30 days and our resources are subject to availability at the time of booking.",
                q.BudgetConfirmLine ?? "Please confirm your acceptance of the proposal and its inclusions by returning a signed copy of the Confirmation of Services page, so we can proceed with your requirements.",
                q.BudgetContactLine ?? "However, if you wish to discuss any additions or updates regarding our proposal, please do not hesitate to contact me on the details below.",
                q.BudgetSignoffLine ?? "We look forward to working with you on a seamless and successful event."
            };

            foreach (var term in terms)
            {
                foreach (var line in Wrap(term, 55))
                {
                    Text(c, font, 8.5f, CONTENT_LEFT, termsY, line, ColorConstants.DARK_GRAY);
                    termsY -= 11f;
                }
                termsY -= 6f;
            }

            // Budget breakdown (right column)
            float budgetY = CONTENT_START_Y;
            float labelX = 350f;
            float valueX = CONTENT_RIGHT;

            MoneyRow(c, font, bold, labelX, valueX, ref budgetY, "Rental Equipment", q.RentalTotal ?? "$0.00");
            MoneyRow(c, font, bold, labelX, valueX, ref budgetY, "Labour", q.LabourTotal ?? "$0.00");
            MoneyRow(c, font, bold, labelX, valueX, ref budgetY, "Service Charge", q.ServiceCharge ?? "$0.00");
            budgetY -= 8f;
            MoneyRow(c, font, bold, labelX, valueX, ref budgetY, "Sub Total (ex GST)", q.SubTotalExGst ?? "$0.00");
            MoneyRow(c, font, bold, labelX, valueX, ref budgetY, "GST (10%)", q.Gst ?? "$0.00");
            budgetY -= 8f;
            Text(c, bold, 10f, labelX, budgetY, "TOTAL (inc GST)");
            TextRight(c, bold, 10f, valueX, budgetY, q.GrandTotalIncGst ?? "$0.00");

            Text(c, font, 9f, CONTENT_LEFT, FOOTER_Y, $"Ref No: {q.Reference}");
        }

        // ========================= PAGE 6: CONFIRMATION =========================
        if (pdf.GetNumberOfPages() >= 6)
        {
            var c = new PdfCanvas(pdf.GetPage(6));

            Text(c, bold, 12f, CONTENT_LEFT, SECTION_TITLE_Y, "CONFIRMATION OF SERVICES", redDark);
            Text(c, bold, 20f, CONTENT_LEFT + 15f, EVENT_TITLE_Y, q.EventTitle?.ToUpper() ?? "", redBright);

            float y = CONTENT_START_Y;
            var confirmTexts = new[] {
                q.ConfirmP1 ?? $"On behalf of {q.Client ?? "the client"}, I accept this proposal and wish to proceed with the details that are confirmed to be correct.",
                q.ConfirmP2 ?? "Upon request, any additions or amendments will be updated to this proposal accordingly.",
                q.ConfirmP3 ?? "We understand that equipment and personnel are not allocated until this document is signed and returned.",
                q.ConfirmP4 ?? "This proposal and billing details are subject to Microhire's terms and conditions."
            };

            foreach (var text in confirmTexts)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    foreach (var line in Wrap(text, 95))
                    {
                        Text(c, font, 9.5f, CONTENT_LEFT, y, line);
                        y -= 13f;
                    }
                    y -= 8f;
                }
            }

            if (!string.IsNullOrWhiteSpace(q.ConfirmTermsUrl))
            {
                y -= 5f;
                Text(c, font, 9f, CONTENT_LEFT, y, "Terms and Conditions: " + q.ConfirmTermsUrl, ColorConstants.BLUE);
            }

            // Signature section
            float sigY = 200f;
            float sigX = 350f;
            Text(c, bold, 10f, sigX, sigY, "REFERENCE DETAILS"); sigY -= 18f;
            Text(c, font, 9f, sigX, sigY, "Reference Number: " + (q.Reference ?? "")); sigY -= 14f;
            Text(c, font, 9f, sigX, sigY, "Total: " + (q.GrandTotalIncGst ?? "$0.00") + " inc GST"); sigY -= 25f;

            Text(c, bold, 10f, sigX, sigY, "PAYMENT DETAILS"); sigY -= 18f;
            Text(c, font, 9f, sigX, sigY, "Account Name: Microhire Pty Ltd"); sigY -= 14f;
            Text(c, font, 9f, sigX, sigY, "BSB: 064-000"); sigY -= 14f;
            Text(c, font, 9f, sigX, sigY, "Account Number: 1527 3541"); sigY -= 14f;
            Text(c, font, 9f, sigX, sigY, "Reference: " + (q.Reference ?? "")); sigY -= 25f;

            Text(c, bold, 10f, sigX, sigY, "CLIENT ACCEPTANCE"); sigY -= 18f;
            Text(c, font, 9f, sigX, sigY, "Signature: _______________________________"); sigY -= 14f;
            Text(c, font, 9f, sigX, sigY, "Name: ___________________________________"); sigY -= 14f;
            Text(c, font, 9f, sigX, sigY, "Date: ____________________________________"); sigY -= 14f;
            Text(c, font, 9f, sigX, sigY, "Position: ________________________________");

            Text(c, font, 9f, CONTENT_LEFT, FOOTER_Y, $"Ref No: {q.Reference}");
        }

        pdf.Close();
        return (outName, dest);
    }

    private static void GenerateSimplePdfFallback(string dest, QuoteAllFields q)
    {
        using var writer = new PdfWriter(dest);
        using var pdf = new PdfDocument(writer);
        using var doc = new Document(pdf);

        doc.SetMargins(28f, 28f, 28f, 28f);

        doc.Add(new Paragraph("Microhire Quote").SetFontSize(18f).SetBold());
        doc.Add(new Paragraph(Safe(q.EventTitle, "Event")).SetFontSize(13f).SetBold());

        doc.Add(new Paragraph($"Reference: {Safe(q.Reference)}"));
        doc.Add(new Paragraph($"Client: {Safe(q.Client)}"));
        doc.Add(new Paragraph($"Contact: {Safe(q.ContactName)}"));
        doc.Add(new Paragraph($"Email: {Safe(q.Email)}"));
        doc.Add(new Paragraph($"Location: {Safe(q.Location)}"));
        doc.Add(new Paragraph($"Room: {Safe(q.Room)}"));
        doc.Add(new Paragraph($"Date Range: {Safe(q.DateRange)}"));

        AddEquipmentSection(doc, "Vision", q.VisionRows, q.VisionTotal);
        AddEquipmentSection(doc, "Audio", q.AudioRows, q.AudioTotal);
        AddEquipmentSection(doc, "Lighting", q.LightingRows, q.LightingTotal);
        AddEquipmentSection(doc, "Recording", q.RecordingRows, q.RecordingTotal);
        AddEquipmentSection(doc, "Drape", q.DrapeRows, q.DrapeTotal);
        AddLaborSection(doc, q.LabourRows, q.LabourTotal);

        doc.Add(new Paragraph("Budget Summary").SetFontSize(12f).SetBold().SetMarginTop(12f));
        doc.Add(new Paragraph($"Rental Equipment: {Safe(q.RentalTotal, "$0.00")}"));
        doc.Add(new Paragraph($"Labour: {Safe(q.LabourTotal, "$0.00")}"));
        doc.Add(new Paragraph($"Service Charge: {Safe(q.ServiceCharge, "$0.00")}"));
        doc.Add(new Paragraph($"Sub Total (ex GST): {Safe(q.SubTotalExGst, "$0.00")}"));
        doc.Add(new Paragraph($"GST: {Safe(q.Gst, "$0.00")}"));
        doc.Add(new Paragraph($"Total (inc GST): {Safe(q.GrandTotalIncGst, "$0.00")}").SetBold());
    }

    private static void AddEquipmentSection(Document doc, string title, List<EquipmentRow>? rows, string? total)
    {
        doc.Add(new Paragraph(title).SetFontSize(11f).SetBold().SetMarginTop(10f));

        if (rows == null || rows.Count == 0)
        {
            doc.Add(new Paragraph("No items"));
            return;
        }

        foreach (var row in rows)
        {
            if (row.IsGroup)
            {
                doc.Add(new Paragraph(Safe(row.Description)).SetBold());
                continue;
            }

            var prefix = row.IsComponent ? "  - " : "- ";
            var qty = string.IsNullOrWhiteSpace(row.Qty) ? string.Empty : $" x{row.Qty.Trim()}";
            var lineTotal = string.IsNullOrWhiteSpace(row.LineTotal) ? string.Empty : $" ({row.LineTotal.Trim()})";
            doc.Add(new Paragraph($"{prefix}{Safe(row.Description)}{qty}{lineTotal}").SetFontSize(9f));
        }

        doc.Add(new Paragraph($"{title} Total: {Safe(total, "$0.00")}").SetBold().SetFontSize(9f));
    }

    private static void AddLaborSection(Document doc, List<LaborRow>? rows, string? total)
    {
        doc.Add(new Paragraph("Technical Services").SetFontSize(11f).SetBold().SetMarginTop(10f));

        if (rows == null || rows.Count == 0)
        {
            doc.Add(new Paragraph("No technical services"));
            return;
        }

        foreach (var row in rows)
        {
            doc.Add(
                new Paragraph(
                    $"- {Safe(row.Description)} | {Safe(row.Task)} | Qty {Safe(row.Qty, "0")} | " +
                    $"{Safe(row.Start)} -> {Safe(row.Finish)} | {Safe(row.Hrs)} | {Safe(row.Total, "$0.00")}")
                .SetFontSize(9f));
        }

        doc.Add(new Paragraph($"Labour Total: {Safe(total, "$0.00")}").SetBold().SetFontSize(9f));
    }

    private static string Safe(string? value, string fallback = "N/A")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    // ==================== HELPERS ====================

    private static void Text(PdfCanvas c, PdfFont f, float size, float x, float y, string txt, Color? col = null)
    {
        if (string.IsNullOrEmpty(txt)) return;
        c.BeginText().SetFontAndSize(f, size).SetColor(col ?? ColorConstants.BLACK, true)
         .MoveText(x, y).ShowText(txt).EndText();
    }

    private static void TextRight(PdfCanvas c, PdfFont f, float size, float xRight, float y, string txt, Color? col = null)
    {
        if (string.IsNullOrEmpty(txt)) return;
        Text(c, f, size, xRight - f.GetWidth(txt, size), y, txt, col);
    }

    private static void DetailRow(PdfCanvas c, PdfFont labelFont, PdfFont valueFont, float xLabel, float xValue, float y, string label, string? value)
    {
        Text(c, labelFont, 9f, xLabel, y, label, new DeviceRgb(180, 30, 30));
        Text(c, valueFont, 9f, xValue, y, value ?? "");
    }

    private static void TimeRow(PdfCanvas c, PdfFont bold, PdfFont normal, float x, float y, string label, string? date, string? time)
    {
        Text(c, bold, 9f, x, y, label, new DeviceRgb(180, 30, 30));
        Text(c, normal, 9f, x + 110f, y, date ?? "");
        Text(c, bold, 9f, x + 340f, y, time ?? "");
    }

    private static void MoneyRow(PdfCanvas c, PdfFont labelFont, PdfFont valueFont, float lx, float rx, ref float y, string label, string value)
    {
        Text(c, labelFont, 9.5f, lx, y, label);
        TextRight(c, valueFont, 9.5f, rx, y, value);
        y -= 16f;
    }

    private static float DrawEquipmentList(PdfCanvas c, PdfFont normal, PdfFont bold, float x, float y, List<EquipmentRow>? rows)
    {
        if (rows == null || rows.Count == 0) return y;

        foreach (var r in rows)
        {
            if (r.IsGroup)
            {
                Text(c, bold, 9.5f, x, y, r.Description?.ToUpper() ?? "");
                y -= 14f;
                continue;
            }

            var isComp = r.IsComponent;
            var fontSize = isComp ? 8.5f : 9f;
            var color = isComp ? ColorConstants.DARK_GRAY : ColorConstants.BLACK;
            var indent = isComp ? 20f : 0f;

            var lines = Wrap(r.Description ?? "", isComp ? 55 : 60).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                Text(c, normal, fontSize, x + indent, y, lines[i], color);
                if (i == 0 && !isComp)
                {
                    if (!string.IsNullOrWhiteSpace(r.Qty))
                        TextRight(c, normal, fontSize, x + 420f, y, r.Qty!);
                    if (!string.IsNullOrWhiteSpace(r.LineTotal))
                        TextRight(c, normal, fontSize, x + 495f, y, r.LineTotal!);
                }
                y -= isComp ? 10f : 12f;
            }
            y -= isComp ? 2f : 4f;
            if (y < 70) break;
        }
        return y;
    }

    private static IEnumerable<string> Wrap(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new List<string>();
        var len = 0;
        foreach (var w in words)
        {
            var add = (line.Count == 0 ? 0 : 1) + w.Length;
            if (len + add > maxChars) { yield return string.Join(' ', line); line.Clear(); len = 0; }
            line.Add(w); len += add;
        }
        if (line.Count > 0) yield return string.Join(' ', line);
    }
}
