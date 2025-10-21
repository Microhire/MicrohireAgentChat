// Services/PdfFromBlankService.cs
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Hyphenation;
using iText.Layout.Properties;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Hosting;

public class PdfFromBlankService
{
    private readonly IWebHostEnvironment _env;
    public PdfFromBlankService(IWebHostEnvironment env) => _env = env;

    public (string fileName, string fullPath) Generate(QuoteAllFields q)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var src = Path.Combine(webRoot, "files", "quotes", "Quote-BLANK-TEMPLATE.pdf");
        if (!File.Exists(src))
            throw new FileNotFoundException("Blank template not found at /wwwroot/files/quotes/Quote-BLANK-TEMPLATE.pdf", src);

        var outDir = Path.Combine(webRoot, "files", "quotes");
        Directory.CreateDirectory(outDir);

        var outName = $"Quote-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var dest = Path.Combine(outDir, outName);

        using var reader = new PdfReader(src);
        using var writer = new PdfWriter(dest);
        using var pdf = new PdfDocument(reader, writer);

        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        // ========================= PAGE 1: OVERVIEW =========================
        {
            var page = pdf.GetPage(1);
            var c = new PdfCanvas(page);

            // Brand colours
            var redBright = new iText.Kernel.Colors.DeviceRgb(230, 0, 0);   // title + underline tone
            var redDark = new iText.Kernel.Colors.DeviceRgb(180, 30, 20); // "OVERVIEW" tone

            // ---- Tuned placement to match the snapshot/template ----
            const float ARROW_TIP_X = 92f;   // tip of the red triangle in the artwork
            const float LEFT_MARGIN = 56f;   // body text left margin
            const float OVERVIEW_X = 72f;
            const float OVERVIEW_Y = 820f;  // lowered slightly so it breathes
            const float GAP_OVERVIEW_TITLE = 56f;   // tighter gap between the two header lines
            const float TITLE_LEFT_OFFSET = 22f;   // how far title sits to the right of the triangle tip

            const float OVERVIEW_SIZE = 36f;
            const float TITLE_SIZE = 38f;

            // ---------- Header ----------
            // OVERVIEW (dark red)
            Text(c, bold, OVERVIEW_SIZE, OVERVIEW_X, OVERVIEW_Y, "OVERVIEW", redDark);

            // Title (“ARCSOPT MEETING” or q.EventTitle) in bright red, aligned to the triangle
            float xTitle = ARROW_TIP_X + TITLE_LEFT_OFFSET;
            float yTitle = OVERVIEW_Y - GAP_OVERVIEW_TITLE;

            var titleRect = new iText.Kernel.Geom.Rectangle(
                xTitle,
                yTitle - (TITLE_SIZE * 0.72f),   // baseline → rectangle origin (approx. cap-height)
                820f,
                TITLE_SIZE * 1.2f
            );

            using (var titleCanvas = new Canvas(c, titleRect))
            {
                titleCanvas.Add(
                    new Paragraph(q.EventTitle)
                        .SetFont(bold)
                        .SetFontSize(TITLE_SIZE)
                        .SetFontColor(redBright)
                        .SetCharacterSpacing(0.2f) // subtle tightening to mimic the sample weight
                        .SetMargin(0).SetPadding(0)
                );
            }

            // ---------- Contact block (safe: uses fields you already have) ----------
            var contactLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(q.ContactName)) contactLines.Add(q.ContactName);
            if (!string.IsNullOrWhiteSpace(q.Client)) contactLines.Add(q.Client);
            if (!string.IsNullOrWhiteSpace(q.Email)) contactLines.Add(q.Email);

            // Start well below the underline so nothing collides
            float yCursor = 708f;
            foreach (var line in contactLines)
            {
                Text(c, font, 12, LEFT_MARGIN, yCursor, line);
                yCursor -= 18f; // 12pt + a bit of leading
            }

            // Greeting
            yCursor -= 10f;
            var firstName = string.IsNullOrWhiteSpace(q.ContactName) ? "Client"
                         : q.ContactName.Trim().Split(' ')[0];
            Text(c, font, 12, LEFT_MARGIN, yCursor, $"Dear {firstName},");

            // ---------- Intro paragraph (fully justified) ----------
            float paraTop = yCursor - 16f;  // gap under greeting
            float paraH = 118f;
            float paraY = paraTop - paraH;

            var introBox = new iText.Kernel.Geom.Rectangle(LEFT_MARGIN, paraY, 814f, paraH);
            using (var canv = new Canvas(new PdfCanvas(page), introBox))
            {
                canv.Add(new Paragraph(
                    "Thank you for the opportunity to present our audio-visual production services for your upcoming event at The Westin Brisbane. " +
                    "We are pleased to provide our proposal in the following pages, based on the information we have received and our recommendations for a seamless and successful event. " +
                    "Our team are committed to achieving your event objectives and welcome the opportunity to discuss your requirements and any budget parameters in further detail. " +
                    "If you have any questions or need further information, please do not hesitate to contact me on the details below. Thank you again and we look forward to speaking to you soon.")
                    .SetFont(font)
                    .SetFontSize(12)
                    .SetFixedLeading(15)
                    .SetFontColor(iText.Kernel.Colors.ColorConstants.DARK_GRAY)
                    .SetTextAlignment(TextAlignment.JUSTIFIED)
                    .SetHyphenation(new HyphenationConfig("en", "AU", 2, 2))
                    .SetMargin(0).SetPadding(0)
                );
            }

            // ---------- Details (single left column) ----------
            float rowH = 20f;
            float yTop = paraY - 28f;          // space after paragraph
            float labX = LEFT_MARGIN;
            float valX = 220f;
            int r = 0;

            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "LOCATION", q.Location);
            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "ADDRESS", q.Address);
            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "ROOM", q.Room);
            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "DATE", q.DateRange);
            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "DELIVERY CONTACT", q.DeliveryContact);
            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "EVENT ACCOUNT MANAGER", q.AccountMgrName);
            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "MOBILE NUMBER", q.AccountMgrMobile);
            InlineDetailAligned(c, bold, font, labX, valX, yTop - rowH * r++, "EMAIL", q.AccountMgrEmail);

            // ---------- Confirm & schedule ----------
            float confirmY = (yTop - rowH * (r - 1)) - 28f;
            Text(c, font, 12, LEFT_MARGIN, confirmY,
                 "Please confirm the following dates and times are accurate:",
                 iText.Kernel.Colors.ColorConstants.DARK_GRAY);

            float schedY = confirmY - 24f;
            TimeRow(c, bold, font, LEFT_MARGIN, schedY - rowH * 0, "SETUP BY", q.SetupDate, "Time: " + q.SetupTime);
            TimeRow(c, bold, font, LEFT_MARGIN, schedY - rowH * 1, "REHEARSAL", q.RehearsalDate, "Time: " + q.RehearsalTime);
            TimeRow(c, bold, font, LEFT_MARGIN, schedY - rowH * 2, "EVENT START", q.EventStartDate, "Time: " + q.EventStartTime);
            TimeRow(c, bold, font, LEFT_MARGIN, schedY - rowH * 3, "EVENT END", q.EventEndDate, "Time: " + q.EventEndTime);

            // Footer reference (leave as-is if your template draws its own footer)
            Text(c, font, 10, LEFT_MARGIN, 40f, $"Ref No: {q.Reference}");
        }













        // =================== PAGE 2: EQUIPMENT & SERVICES ===================
        if (pdf.GetNumberOfPages() >= 2)
        {
            var c = new PdfCanvas(pdf.GetPage(2));
            Text(c, bold, 12, 40, 780, "EQUIPMENT & SERVICES");
            Text(c, bold, 16, 40, 760, q.EventTitle);

            // Top table headers already in template; draw room note
            float y = 710;
            Text(c, bold, 10, 40, y, q.RoomNoteHeader); y -= 14;
            Text(c, font, 9, 40, y, q.RoomNoteStarts); y -= 12;
            Text(c, font, 9, 40, y, q.RoomNoteEnds); y -= 18;
            Text(c, bold, 10, 40, y, $"{q.RoomNoteHeader} Total");
            TextRight(c, bold, 10, 560, y, q.RoomNoteTotal);
            y -= 28;

            // Vision section
            Text(c, bold, 10.5f, 40, y, "Vision"); y -= 14;
            y = DrawEquipmentList(c, font, bold, 40, y, q.VisionRows);
            y -= 6;
            Text(c, bold, 10, 40, y, "Vision Total");
            TextRight(c, bold, 10, 560, y, q.VisionTotal);
            y -= 28;

            // Audio section
            Text(c, bold, 10.5f, 40, y, "Audio"); y -= 14;
            y = DrawEquipmentList(c, font, bold, 40, y, q.AudioRows);
            y -= 6;
            Text(c, bold, 10, 40, y, "Audio Total");
            TextRight(c, bold, 10, 560, y, q.AudioTotal);

            // Footer ref
            Text(c, font, 9, 40, 40, $"Ref No: {q.Reference}");
        }

        // =================== PAGE 3: TECHNICAL SERVICES =====================
        if (pdf.GetNumberOfPages() >= 3)
        {
            var c = new PdfCanvas(pdf.GetPage(3));
            Text(c, bold, 12, 40, 780, "TECHNICAL SERVICES");
            Text(c, bold, 16, 40, 760, q.EventTitle);

            // Headers line (template likely has rule)
            var y = 720f;
            // Table headings: Description | Task | Qty | Start | Finish | Hrs | Total ($)
            Text(c, bold, 9.5f, 40, y, "Description");
            Text(c, bold, 9.5f, 170, y, "Task");
            Text(c, bold, 9.5f, 255, y, "Qty");
            Text(c, bold, 9.5f, 290, y, "Start Date/Time");
            Text(c, bold, 9.5f, 380, y, "Finish Date/Time");
            Text(c, bold, 9.5f, 470, y, "Hrs");
            Text(c, bold, 9.5f, 510, y, "Total ($)");

            y -= 20f;

            foreach (var r in q.LabourRows)
            {
                Text(c, font, 9.5f, 40, y, r.Description);
                Text(c, font, 9.5f, 170, y, r.Task);
                Text(c, font, 9.5f, 255, y, r.Qty);
                Text(c, font, 9.5f, 290, y, r.Start);
                Text(c, font, 9.5f, 380, y, r.Finish);
                Text(c, font, 9.5f, 470, y, r.Hrs);
                TextRight(c, font, 9.5f, 560, y, r.Total);
                y -= 18f;
                if (y < 120) break;
            }

            // Labour Total
            TextRight(c, bold, 10, 560, 100, $"Labour Total   {q.LabourTotal}");

            // Footer ref
            Text(c, font, 9, 40, 40, $"Ref No: {q.Reference}");
        }

        // ====================== PAGE 4: BUDGET SUMMARY ======================
        if (pdf.GetNumberOfPages() >= 4)
        {
            var c = new PdfCanvas(pdf.GetPage(4));
            Text(c, bold, 12, 40, 780, "BUDGET SUMMARY");
            Text(c, bold, 16, 40, 760, q.EventTitle);

            float y = 720;
            MoneyRow(c, font, bold, 380, 560, ref y, "Rental Equipment", q.RentalTotal);
            MoneyRow(c, font, bold, 380, 560, ref y, "Labour", q.LabourTotal);
            MoneyRow(c, font, bold, 380, 560, ref y, "Service Charge", q.ServiceCharge);

            // dotted rule (optional)
            y -= 10; Text(c, font, 10, 380, y, "----------------------"); y -= 14;

            MoneyRow(c, font, bold, 380, 560, ref y, "Sub Total (ex GST)", q.SubTotalExGst);
            MoneyRow(c, font, bold, 380, 560, ref y, "GST", q.Gst);

            // dotted rule
            y -= 10; Text(c, font, 10, 380, y, "----------------------"); y -= 14;

            MoneyRow(c, bold, bold, 380, 560, ref y, "Total", q.GrandTotalIncGst);

            // Paragraph notes (as in your screenshot)
            float py = 560;
            foreach (var line in Wrap(q.BudgetNotesTopLine, 110)) { Text(c, font, 9, 40, py, line, ColorConstants.DARK_GRAY); py -= 12; }
            foreach (var line in Wrap(q.BudgetValidityLine, 110)) { Text(c, font, 9, 40, py, line, ColorConstants.DARK_GRAY); py -= 12; }
            foreach (var line in Wrap(q.BudgetConfirmLine, 110)) { Text(c, font, 9, 40, py, line, ColorConstants.DARK_GRAY); py -= 12; }
            foreach (var line in Wrap(q.BudgetContactLine, 110)) { Text(c, font, 9, 40, py, line, ColorConstants.DARK_GRAY); py -= 12; }
            foreach (var line in Wrap(q.BudgetSignoffLine, 110)) { Text(c, font, 9, 40, py, line, ColorConstants.DARK_GRAY); py -= 12; }

            // Signature block
            Text(c, bold, 10, 40, 140, q.AccountMgrName + "    Event Staging Manager");
            Text(c, font, 9, 40, 124, q.FooterOfficeLine1);
            Text(c, font, 9, 40, 110, q.FooterOfficeLine2);
            Text(c, bold, 9, 40, 96, "M"); Text(c, font, 9, 60, 96, q.AccountMgrMobile);
            Text(c, bold, 9, 40, 82, "T"); Text(c, font, 9, 60, 82, "");
            Text(c, bold, 9, 40, 68, "E"); Text(c, font, 9, 60, 68, q.AccountMgrEmail);

            // Footer ref
            Text(c, font, 9, 40, 40, $"Ref No: {q.Reference}");
        }

        // ================== PAGE 5: CONFIRMATION OF SERVICES =================
        if (pdf.GetNumberOfPages() >= 5)
        {
            var c = new PdfCanvas(pdf.GetPage(5));
            Text(c, bold, 12, 40, 780, "CONFIRMATION OF SERVICES");
            Text(c, bold, 16, 40, 760, q.EventTitle);

            float y = 720;
            foreach (var line in Wrap(q.ConfirmP1, 110)) { Text(c, font, 9.5f, 40, y, line); y -= 12; }
            foreach (var line in Wrap(q.ConfirmP2, 110)) { Text(c, font, 9.5f, 40, y, line); y -= 12; }
            foreach (var line in Wrap(q.ConfirmP3, 110)) { Text(c, font, 9.5f, 40, y, line); y -= 12; }
            foreach (var line in Wrap(q.ConfirmP4, 110)) { Text(c, font, 9.5f, 40, y, line); y -= 12; }

            // terms URL (your sample shows it as plain text)
            Text(c, font, 9.5f, 40, y - 6, q.ConfirmTermsUrl, ColorConstants.BLUE);

            // Right column: Reference / Total
            Text(c, font, 9.5f, 40, 130, "Reference Number");
            Text(c, font, 9.5f, 200, 130, q.Reference);
            Text(c, font, 9.5f, 40, 112, "Total Quotation");
            Text(c, font, 9.5f, 200, 112, q.GrandTotalIncGst + " inc GST");

            // Signature lines (template has lines; if not, draw them)
            // (Leave as-is to respect your blank template)
        }

        pdf.Close();
        return (outName, dest);
    }

    // ---------- helpers ----------
    private static void InlineDetailAligned(
    PdfCanvas c,
    PdfFont labelFont, PdfFont valueFont,
    float xLabel, float xValue, float y,
    string label, string value,
    float labelSize = 9.5f, float valueSize = 9.5f)
    {
        // label in red, bold
        c.BeginText().SetFontAndSize(labelFont, labelSize).SetColor(ColorConstants.RED, true)
         .MoveText(xLabel, y).ShowText(label).EndText();

        // value in black, regular; fixed x so all values line up
        c.BeginText().SetFontAndSize(valueFont, valueSize).SetColor(ColorConstants.BLACK, true)
         .MoveText(xValue, y).ShowText(value).EndText();
    }

    private static void InlineDetail(
    PdfCanvas c, PdfFont labelFont, PdfFont valueFont,
    float x, float y, string label, string value,
    Color? color = null, float gap = 6f)
    {
        var col = color ?? ColorConstants.RED; // match "SETUP BY"
                                               // draw LABEL:
        c.BeginText().SetFontAndSize(labelFont, 9.5f).SetColor(col, true)
         .MoveText(x, y).ShowText(label).EndText();

        // measure label width so value starts right after it
        var lw = labelFont.GetWidth(label, 9.5f);
        var valueX = x + lw + gap;

        // draw VALUE (same color to match request)
        c.BeginText().SetFontAndSize(valueFont, 9.5f).SetColor(col, true)
         .MoveText(valueX, y).ShowText(value).EndText();
    }


    private static void Text(PdfCanvas c, PdfFont f, float size, float x, float y, string txt, Color? col = null)
    {
        c.BeginText().SetFontAndSize(f, size).SetColor(col ?? ColorConstants.BLACK, true).MoveText(x, y).ShowText(txt).EndText();
    }
    private static void TextRight(PdfCanvas c, PdfFont f, float size, float xRight, float y, string txt, Color? col = null)
    {
        var w = f.GetWidth(txt, size);
        Text(c, f, size, xRight - w, y, txt, col);
    }
    private static void LabelValue(PdfCanvas c, PdfFont bold, PdfFont normal, float x, float y, string label, string value)
    {
        Text(c, bold, 9.5f, x, y, label);
        Text(c, normal, 9.5f, x + 140, y, value);
    }
    private static void TimeRow(PdfCanvas c, PdfFont bold, PdfFont normal, float x, float y, string label, string date, string right)
    {
        Text(c, bold, 9.5f, x, y, label, ColorConstants.RED);
        Text(c, normal, 9.5f, x + 140, y, date);
        Text(c, bold, 9.5f, x + 380, y, right);
    }
    private static float DrawEquipmentList(PdfCanvas c, PdfFont normal, PdfFont bold, float x, float y, List<EquipmentRow> rows)
    {
        foreach (var r in rows)
        {
            if (r.IsGroup)
            {
                Text(c, bold, 9.5f, x, y, r.Description);
                y -= 14;
                continue;
            }
            // Wrap long description
            var lines = Wrap(r.Description ?? "", 90).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                Text(c, normal, 9.5f, x, y, lines[i]);
                if (i == 0)
                {
                    if (!string.IsNullOrWhiteSpace(r.Qty)) Text(c, normal, 9.5f, 480, y, r.Qty!);
                    if (!string.IsNullOrWhiteSpace(r.LineTotal)) TextRight(c, normal, 9.5f, 560, y, r.LineTotal!);
                }
                y -= 14;
            }
            y -= 4;
            if (y < 140) break; // page guard
        }
        return y;
    }
    private static void MoneyRow(PdfCanvas c, PdfFont normal, PdfFont bold, float lx, float rx, ref float y, string label, string value)
    {
        Text(c, normal, 10, lx, y, label);
        TextRight(c, normal, 10, rx, y, value);
        y -= 18;
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
