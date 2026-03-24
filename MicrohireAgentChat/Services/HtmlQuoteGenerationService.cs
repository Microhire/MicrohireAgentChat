using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using System.Text;
using System.Web;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Generates dynamic HTML quotes based on the quote-viewer.html template.
/// More reliable than PDF generation - returns HTML that can be displayed in an iframe.
/// </summary>
public partial class HtmlQuoteGenerationService
{
    private readonly BookingDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HtmlQuoteGenerationService> _logger;

    public HtmlQuoteGenerationService(
        BookingDbContext db,
        IWebHostEnvironment env,
        ILogger<HtmlQuoteGenerationService> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    public async Task<(bool success, string? htmlUrl, string? error)> GenerateHtmlQuoteForBookingAsync(
        string bookingNo,
        CancellationToken ct = default,
        ISession? session = null)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("[QUOTE GEN] Starting HTML quote generation for booking {BookingNo} at {StartTime}", bookingNo, startTime);

        try
        {
            // 1. Load booking with related data
            var booking = await _db.TblBookings
                .FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);

            if (booking == null)
            {
                _logger.LogWarning("[QUOTE GEN] Booking {BookingNo} not found in database", bookingNo);
                return (false, null, $"Booking {bookingNo} not found");
            }
            
            _logger.LogInformation("[QUOTE GEN] Loaded booking {BookingNo}: ContactID={ContactId}, CustID={CustId}, VenueID={VenueId}, Date={EventDate}",
                bookingNo, booking.ContactID, booking.CustID, booking.VenueID, booking.dDate);

            // 2. Load venue information
            TblVenue? venue = null;
            if (booking.VenueID > 0)
            {
                venue = await _db.TblVenues
                    .FirstOrDefaultAsync(v => v.ID == booking.VenueID, ct);
            }
            
            // If session has a venue name that doesn't match the loaded venue, try to find the correct one
            string? sessionVenueNameForLookup = session?.GetString("Draft:VenueName");
            if (!string.IsNullOrWhiteSpace(sessionVenueNameForLookup) && venue != null &&
                VenueNamesMismatch(sessionVenueNameForLookup, venue.VenueName))
            {
                var correctVenue = await _db.TblVenues
                    .FirstOrDefaultAsync(v => v.VenueName != null && 
                        v.VenueName.ToLower().Contains(sessionVenueNameForLookup.ToLower()), ct);
                if (correctVenue != null)
                {
                    _logger.LogInformation("[QUOTE GEN] Session venue '{SessionVenue}' differs from DB venue '{DbVenue}' (ID={VenueId}). Using correct venue '{CorrectVenue}' (ID={CorrectId})",
                        sessionVenueNameForLookup, venue.VenueName, booking.VenueID, correctVenue.VenueName, correctVenue.ID);
                    venue = correctVenue;
                }
            }

            // 3. Load contact
            TblContact? contact = null;
            if (booking.ContactID.HasValue)
            {
                contact = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.Id == booking.ContactID.Value, ct);
            }

            // 4. Load organization
            TblCust? organization = null;
            if (booking.CustID.HasValue)
            {
                organization = await _db.TblCusts
                    .FirstOrDefaultAsync(c => c.ID == booking.CustID.Value, ct);
            }

            // 5. Load equipment items
            int? bookingIdAsInt = null;
            if (booking.ID <= int.MaxValue && booking.ID >= int.MinValue)
            {
                bookingIdAsInt = decimal.ToInt32(decimal.Truncate(booking.ID));
            }

            var items = await _db.TblItemtrans
                .Where(i => i.BookingNoV32 == bookingNo || (bookingIdAsInt.HasValue && i.BookingId == bookingIdAsInt.Value))
                .ToListAsync(ct);
            _logger.LogInformation("Loaded {ItemCount} equipment items", items.Count);

            // 6. Load inventory master data for descriptions and prices
            var productCodes = items.Select(i => i.ProductCodeV42).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            var inventoryItemsList = await _db.TblInvmas
                .Where(inv => productCodes.Contains(inv.product_code))
                .ToListAsync(ct);
            var inventoryItems = inventoryItemsList
                .GroupBy(inv => inv.product_code ?? "")
                .ToDictionary(g => g.Key, g => g.First());

            // 6b. Load rate table data for proper pricing
            var rateItemsList = await _db.TblRatetbls
                .Where(r => productCodes.Contains(r.product_code) && r.TableNo == 0)
                .ToListAsync(ct);
            var rateItems = rateItemsList
                .GroupBy(r => r.product_code ?? "")
                .ToDictionary(g => g.Key, g => g.First());

            // 6c. Load crew/labor data
            var crewRows = await _db.TblCrews
                .Where(c => c.BookingNoV32 == bookingNo)
                .OrderBy(c => c.Task).ThenBy(c => c.SeqNo)
                .ToListAsync(ct);
            _logger.LogInformation("Loaded {CrewCount} crew/labor rows for booking {BookingNo}", crewRows.Count, bookingNo);

            // 7. Build quote data (pass session overrides for venue/contact when available)
            string? sessionVenueName = session?.GetString("Draft:VenueName");
            string? sessionContactName = session?.GetString("Draft:ContactName");
            string? sessionOrganisation = session?.GetString("Draft:Organisation");
            var quoteData = BuildQuoteData(booking, venue, contact, organization, items, inventoryItems, rateItems,
                crewRows, sessionVenueName, sessionContactName, sessionOrganisation);

            // 8. Generate HTML
            var html = GenerateHtml(quoteData);

            // 9. Save to file
            var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var outDir = Path.Combine(webRoot, "files", "quotes");
            Directory.CreateDirectory(outDir);

            var outName = $"Quote-{bookingNo}-{DateTime.UtcNow:yyyyMMddHHmmss}.html";
            var dest = Path.Combine(outDir, outName);

            await File.WriteAllTextAsync(dest, html, ct);

            // 10. Pre-generate PDF from the HTML so downloads are instant and styled
            var pdfName = Path.ChangeExtension(outName, ".pdf");
            var pdfDest = Path.Combine(outDir, pdfName);
            await GeneratePdfFromHtmlAsync(html, pdfDest, _logger);

            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("[QUOTE GEN] Quote generated successfully for booking {BookingNo}: " +
                "File={FileName}, HtmlLength={HtmlLength} chars, ElapsedMs={ElapsedMs}",
                bookingNo, outName, html.Length, elapsedMs);

            var url = $"/files/quotes/{Uri.EscapeDataString(outName)}";
            return (true, url, null);
        }
        catch (Exception ex)
        {
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var errorDetail = $"{ex.Message} {(ex.InnerException != null ? "-> " + ex.InnerException.Message : "")}";
            _logger.LogError(ex, "[QUOTE GEN] Failed to generate HTML quote for booking {BookingNo} after {ElapsedMs}ms: {ErrorMessage}", 
                bookingNo, elapsedMs, errorDetail);
            return (false, null, errorDetail);
        }
    }

    private QuoteHtmlData BuildQuoteData(
        TblBooking booking,
        TblVenue? venue,
        TblContact? contact,
        TblCust? organization,
        List<TblItemtran> items,
        Dictionary<string, TblInvmas> inventoryLookup,
        Dictionary<string, TblRatetbl> rateLookup,
        List<TblCrew> crewRows,
        string? sessionVenueName = null,
        string? sessionContactName = null,
        string? sessionOrganisation = null)
    {
        // Format dates - try multiple booking date fields, avoid using current date
        var eventDate = booking.dDate ?? booking.ShowSDate ?? booking.SetDate ?? booking.SDate ?? booking.ShowEdate ?? booking.RehDate;
        
        // Log warning if no event date found in booking - this indicates a data issue
        if (!eventDate.HasValue)
        {
            _logger.LogWarning("[QUOTE GEN] Booking {BookingNo} has no event date set (dDate, ShowSDate, SetDate, SDate, ShowEdate, RehDate all null). This may cause incorrect quote dates.", 
                booking.booking_no ?? "UNKNOWN");
            // Use order_date as last resort (when quote was created) rather than current date
            eventDate = booking.order_date ?? booking.EntryDate;
            if (eventDate.HasValue)
            {
                _logger.LogInformation("[QUOTE GEN] Using order_date/EntryDate {Date} as fallback for booking {BookingNo}", 
                    eventDate.Value, booking.booking_no ?? "UNKNOWN");
            }
        }
        
        // If still no date, this is a critical error - log and use a safe fallback
        if (!eventDate.HasValue)
        {
            _logger.LogError("[QUOTE GEN] CRITICAL: Booking {BookingNo} has no date fields set at all. Using today's date as emergency fallback.", 
                booking.booking_no ?? "UNKNOWN");
            eventDate = DateTime.Today;
        }
        
        var eventDateStr = eventDate.Value.ToString("dddd d MMMM yyyy");
        var eventDateShort = eventDate.Value.ToString("d MMMM yyyy").ToUpper();
        var setupDateValue = booking.SetDate ?? eventDate.Value;
        var rehearsalDateValue = booking.RehDate ?? eventDate.Value;
        var eventStartDateValue = booking.ShowSDate ?? booking.SDate ?? eventDate.Value;
        var eventEndDateValue = booking.ShowEdate ?? eventStartDateValue;

        var setupDateStr = setupDateValue.ToString("dddd d MMMM yyyy");
        var rehearsalDateStr = rehearsalDateValue.ToString("dddd d MMMM yyyy");
        var eventStartDateStr = eventStartDateValue.ToString("dddd d MMMM yyyy");
        var eventEndDateStr = eventEndDateValue.ToString("dddd d MMMM yyyy");
        var setupDateShort = setupDateValue.ToString("d MMM yyyy");
        var rehearsalDateShort = rehearsalDateValue.ToString("d MMM yyyy");
        var eventStartDateShort = eventStartDateValue.ToString("d MMM yyyy");
        var eventEndDateShort = eventEndDateValue.ToString("d MMM yyyy");

        // Format times
        var setupTime = FormatTime(booking.setupTimeV61) ?? "07:00";
        var rehearsalTime = FormatTime(booking.RehearsalTime) ?? "09:00";
        var eventStartTime = FormatTime(booking.showStartTime) ?? "10:00";
        var eventEndTime = FormatTime(booking.ShowEndTime) ?? "17:00";

        // All supported rooms are at the Westin Brisbane
        var venueName = WestinRoomCatalog.VenueName;
        var venueAddress = WestinRoomCatalog.VenueAddress;
        var venueRoom = StripProjectorAreaSuffix(booking.VenueRoom) ?? "Room TBD";

        // Build equipment sections
        var equipmentSections = BuildEquipmentSections(items, inventoryLookup, rateLookup);

        // Calculate totals
        decimal equipmentTotal = equipmentSections.Sum(s => s.Items.Sum(i => i.LineTotal));
        // Business rule: 10% service charge on total hire amount (rental equipment).
        decimal serviceCharge = equipmentTotal * 0.10m;
        decimal gst = equipmentTotal * 0.10m;
        decimal grandTotal = equipmentTotal + gst;

        // Use booking price if set
        if (booking.price_quoted.HasValue && booking.price_quoted.Value > 0)
        {
            grandTotal = (decimal)booking.price_quoted.Value;
            gst = grandTotal / 1.1m * 0.1m;
            equipmentTotal = grandTotal - gst;
        }

        var resolvedContactName = contact?.Contactname ?? booking.contact_nameV6;
        if (!string.IsNullOrWhiteSpace(sessionContactName))
            resolvedContactName = sessionContactName;
        
        var resolvedOrgName = organization?.OrganisationV6 ?? booking.OrganizationV6;
        if (!string.IsNullOrWhiteSpace(sessionOrganisation))
            resolvedOrgName = sessionOrganisation;

        var venueCity = "Brisbane";

        var laborItems = BuildLaborItems(crewRows, eventDate.Value);

        return new QuoteHtmlData
        {
            EventTitle = booking.showName ?? resolvedOrgName ?? "Event",
            Reference = booking.booking_no ?? "TEMP-001",
            EventDate = eventDateShort,
            EventDateFull = eventDateStr,
            
            // Contact info
            ContactName = resolvedContactName ?? "Contact",
            OrganizationName = resolvedOrgName ?? "Organization",
            ContactAddress = organization?.Address_l1V6 ?? "",
            ContactPhone = contact?.Phone1 ?? contact?.Cell ?? "",
            ContactEmail = contact?.Email ?? "",
            
            // Venue info
            VenueName = venueName,
            VenueAddress = venueAddress,
            VenueRoom = venueRoom,
            VenueLocation = venueCity,
            
            // Account manager
            AccountManagerName = booking.Salesperson ?? "Doug Miller",
            AccountManagerMobile = "+61 420 838 716",
            AccountManagerEmail = "Doug.Miller@microhire.com",
            
            // Schedule
            SetupDate = setupDateStr,
            SetupDateShort = setupDateShort,
            SetupTime = setupTime,
            RehearsalDate = rehearsalDateStr,
            RehearsalDateShort = rehearsalDateShort,
            RehearsalTime = rehearsalTime,
            EventStartDate = eventStartDateStr,
            EventStartDateShort = eventStartDateShort,
            EventStartTime = eventStartTime,
            EventEndDate = eventEndDateStr,
            EventEndDateShort = eventEndDateShort,
            EventEndTime = eventEndTime,
            
            // Equipment
            EquipmentSections = equipmentSections,
            LaborItems = laborItems,
            EquipmentTotal = equipmentTotal,
            ServiceCharge = serviceCharge,
            Gst = gst,
            GrandTotal = grandTotal
        };
    }

    private List<QuoteEquipmentSection> BuildEquipmentSections(
        List<TblItemtran> items,
        Dictionary<string, TblInvmas> inventoryLookup,
        Dictionary<string, TblRatetbl> rateLookup)
    {
        var sections = new List<QuoteEquipmentSection>();

        // Separate packages from standalone items and components
        var packages = items.Where(i => i.ItemType == 1).ToList();
        var standaloneItems = items.Where(i => i.ItemType == 0 && string.IsNullOrEmpty(i.ParentCode)).ToList();
        var components = items.Where(i => i.ItemType == 2 || !string.IsNullOrEmpty(i.ParentCode)).ToList();

        // Group components by parent
        var componentsByParent = components
            .GroupBy(c => c.ParentCode?.Trim() ?? "")
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Process packages
        if (packages.Any())
        {
            var avPackage = new QuoteEquipmentSection { Category = "AV Package", SubCategory = "Data Projector/Screen" };
            
            foreach (var package in packages)
            {
                inventoryLookup.TryGetValue(package.ProductCodeV42 ?? "", out var invItem);
                rateLookup.TryGetValue(package.ProductCodeV42 ?? "", out var rateItem);
                
                var qty = package.TransQty ?? 1;
                var unitPrice = GetUnitPrice(package, invItem, rateItem);
                var description = GetBestDescription(package, invItem);

                avPackage.Items.Add(new QuoteEquipmentItem
                {
                    Description = description,
                    Quantity = (int)qty,
                    UnitPrice = unitPrice,
                    LineTotal = qty * unitPrice,
                    IsMainItem = true
                });

                // Add components
                if (componentsByParent.TryGetValue(package.ProductCodeV42?.Trim() ?? "", out var comps))
                {
                    foreach (var comp in comps)
                    {
                        inventoryLookup.TryGetValue(comp.ProductCodeV42 ?? "", out var compInvItem);
                        var compDesc = GetBestDescription(comp, compInvItem);
                        var compQty = comp.TransQty ?? 1;

                        avPackage.Items.Add(new QuoteEquipmentItem
                        {
                            Description = compDesc,
                            Quantity = (int)compQty,
                            UnitPrice = 0, // Components included in package
                            LineTotal = 0,
                            IsComponent = true
                        });
                    }
                }
            }

            if (avPackage.Items.Any())
                sections.Add(avPackage);
        }

        // Process standalone items by category
        var categorizedItems = standaloneItems.GroupBy(i =>
        {
            var code = i.ProductCodeV42 ?? "";
            inventoryLookup.TryGetValue(code, out var inv);
            return CategorizeItem(code, inv);
        });

        foreach (var group in categorizedItems)
        {
            var section = new QuoteEquipmentSection { Category = group.Key };
            
            foreach (var item in group)
            {
                inventoryLookup.TryGetValue(item.ProductCodeV42 ?? "", out var invItem);
                rateLookup.TryGetValue(item.ProductCodeV42 ?? "", out var rateItem);
                
                var qty = item.TransQty ?? 1;
                var unitPrice = GetUnitPrice(item, invItem, rateItem);
                var description = GetBestDescription(item, invItem);

                section.Items.Add(new QuoteEquipmentItem
                {
                    Description = description,
                    Quantity = (int)qty,
                    UnitPrice = unitPrice,
                    LineTotal = qty * unitPrice,
                    IsMainItem = true
                });
            }

            if (section.Items.Any())
                sections.Add(section);
        }

        return sections;
    }

    private List<QuoteLaborItem> BuildLaborItems(List<TblCrew> crewRows, DateTime eventDate)
    {
        if (crewRows.Count == 0)
            return new List<QuoteLaborItem>();

        return crewRows.Select(c =>
        {
            var taskName = ResolveCrewTaskName(c.Task);
            var desc = ResolveCrewDescription(c.ProductCodeV42);
            var hours = c.Hours ?? 0;
            var minutes = c.Minutes ?? 0;
            var totalHours = hours + (minutes / 60.0);
            if (totalHours <= 0) totalHours = 1;

            var rate = (decimal)(c.UnitRate ?? c.Price ?? 110.0);
            if (c.Price is > 0 && c.UnitRate is null or 0)
                rate = (decimal)c.Price.Value / (decimal)Math.Max(totalHours, 1) / Math.Max(c.TransQty ?? 1, 1);

            var startDt = FormatCrewDateTime(c.FirstDate, eventDate, c.DelTimeHour, c.DelTimeMin);
            var endDt = FormatCrewDateTime(c.RetnDate, eventDate, c.ReturnTimeHour, c.ReturnTimeMin);

            return new QuoteLaborItem
            {
                Description = desc,
                Task = taskName,
                Quantity = c.TransQty ?? 1,
                StartDateTime = startDt,
                EndDateTime = endDt,
                Hours = FormatHoursMinutes(hours, minutes),
                LineTotal = rate * (decimal)totalHours * (c.TransQty ?? 1)
            };
        }).ToList();
    }

    private static string ResolveCrewTaskName(byte? taskCode)
    {
        return taskCode switch
        {
            2 => "Setup",
            3 => "Operate",
            4 => "Pack Down",
            7 => "Rehearsal",
            _ => "Setup"
        };
    }

    private static string ResolveCrewDescription(string? productCode)
    {
        return (productCode ?? "").Trim().ToUpperInvariant() switch
        {
            "AXTECH" => "Audio Technician",
            "VXTECH" => "Vision Technician",
            "LXTECH" => "Lighting Technician",
            "SAVTECH" => "Senior AV Technician",
            _ => "AV Technician"
        };
    }

    private static string FormatCrewDateTime(DateTime? fullDate, DateTime eventDate, byte? hour, byte? min)
    {
        if (fullDate.HasValue)
            return fullDate.Value.ToString("d MMMM yyyy HH:mm");

        var h = hour ?? 0;
        var m = min ?? 0;
        return $"{eventDate:d MMMM yyyy} {h:D2}:{m:D2}";
    }

    private static string FormatHoursMinutes(int hours, int minutes)
    {
        return $"{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// Treats minor naming variants (e.g. "Westin Brisbane" vs "The Westin Brisbane") as the same venue.
    /// </summary>
    private static bool VenueNamesMismatch(string? sessionVenueName, string? dbVenueName)
    {
        if (string.IsNullOrWhiteSpace(sessionVenueName) || string.IsNullOrWhiteSpace(dbVenueName))
            return false;

        var session = sessionVenueName.Trim().ToLowerInvariant();
        var db = dbVenueName.Trim().ToLowerInvariant();

        return !session.Contains(db) && !db.Contains(session);
    }

    private string CategorizeItem(string productCode, TblInvmas? inv)
    {
        var group = (inv?.groupFld ?? "").ToUpper().Trim();
        var desc = (inv?.descriptionv6 ?? productCode).ToLower();

        if (group.Contains("AUDIO") || group.Contains("SOUND") || 
            desc.Contains("mic") || desc.Contains("speaker") || desc.Contains("mixer"))
            return "Microphones & Audio";

        if (group.Contains("VISION") || group.Contains("VIDEO") ||
            desc.Contains("projector") || desc.Contains("screen") || desc.Contains("monitor"))
            return "Data Projector/Screen";

        if (group.Contains("COMPUTER") || desc.Contains("laptop") || desc.Contains("macbook"))
            return "Computers & Laptops";

        if (group.Contains("LIGHTING") || desc.Contains("light") || desc.Contains("led"))
            return "Lighting";

        return "Additional Equipment";
    }

    /// <summary>
    /// Converts an HTML string to a styled PDF using Playwright/Chromium.
    /// Uses SetContentAsync (no file:// URI) so Google Fonts and inline CSS
    /// render identically to what the user sees in the browser.
    /// This is intentionally static so it can be called from other services
    /// (e.g. after signing overlays the signature onto the HTML).
    /// </summary>
    public static async Task GeneratePdfFromHtmlAsync(string html, string pdfOutputPath, ILogger? logger = null)
    {
        try
        {
            using var pw = await Playwright.CreateAsync();
            await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.SetContentAsync(html);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.PdfAsync(new PagePdfOptions
            {
                Path = pdfOutputPath,
                Format = "A4",
                PrintBackground = true,
                Margin = new() { Top = "10mm", Bottom = "12mm", Left = "10mm", Right = "10mm" }
            });

            logger?.LogInformation("[QUOTE GEN] PDF pre-generated: {Path} ({Size} bytes)",
                pdfOutputPath, new FileInfo(pdfOutputPath).Length);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[QUOTE GEN] PDF pre-generation failed for {Path}. " +
                "PDF will be generated on-the-fly at download time.", pdfOutputPath);
        }
    }
}
