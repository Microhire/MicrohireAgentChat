using MicrohireAgentChat.Data;
using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;
using System.Web;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Generates dynamic HTML quotes based on the quote-viewer.html template.
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
        ISession? session = null,
        string? quoteTraceId = null)
    {
        var trace = string.IsNullOrWhiteSpace(quoteTraceId) ? Guid.NewGuid().ToString("N")[..12] : quoteTraceId.Trim();
        var startTime = DateTime.UtcNow;
        _logger.LogInformation(
            "[QUOTE GEN] trace={Trace} phase=start booking={BookingNo} utc={Start:o} contentRoot={ContentRoot}",
            trace,
            bookingNo,
            startTime,
            _env.ContentRootPath);

        try
        {
            // 1. Load booking with related data
            var booking = await _db.TblBookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);

            if (booking == null)
            {
                _logger.LogWarning("[QUOTE GEN] trace={Trace} phase=booking_missing booking={BookingNo}", trace, bookingNo);
                return (false, null, $"Booking {bookingNo} not found");
            }

            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=booking_loaded booking={BookingNo} contactId={ContactId} custId={CustId} venueId={VenueId} eventDate={EventDate}",
                trace,
                bookingNo,
                booking.ContactID,
                booking.CustID,
                booking.VenueID,
                booking.dDate);

            // 2–5. Load venue, contact, organization, line items, and crew sequentially.
            // EF Core disallows concurrent operations on a single DbContext instance; parallel Task.WhenAll caused
            // "A second operation was started on this context instance before a previous operation completed."
            int? bookingIdAsInt = null;
            if (booking.ID <= int.MaxValue && booking.ID >= int.MinValue)
            {
                bookingIdAsInt = decimal.ToInt32(decimal.Truncate(booking.ID));
            }

            TblVenue? venue = booking.VenueID > 0
                ? await _db.TblVenues.AsNoTracking().FirstOrDefaultAsync(v => v.ID == booking.VenueID, ct).ConfigureAwait(false)
                : null;

            // If session has a venue name that doesn't match the loaded venue, try to find the correct one
            string? sessionVenueNameForLookup = session?.GetString("Draft:VenueName");
            if (!string.IsNullOrWhiteSpace(sessionVenueNameForLookup) && venue != null &&
                VenueNamesMismatch(sessionVenueNameForLookup, venue.VenueName))
            {
                var correctVenue = await _db.TblVenues
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.VenueName != null &&
                        v.VenueName.ToLower().Contains(sessionVenueNameForLookup.ToLower()), ct)
                    .ConfigureAwait(false);
                if (correctVenue != null)
                {
                    _logger.LogInformation("[QUOTE GEN] Session venue '{SessionVenue}' differs from DB venue '{DbVenue}' (ID={VenueId}). Using correct venue '{CorrectVenue}' (ID={CorrectId})",
                        sessionVenueNameForLookup, venue.VenueName, booking.VenueID, correctVenue.VenueName, correctVenue.ID);
                    venue = correctVenue;
                }
            }

            TblContact? contact = booking.ContactID.HasValue
                ? await _db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == booking.ContactID.Value, ct).ConfigureAwait(false)
                : null;

            TblCust? organization = booking.CustID.HasValue
                ? await _db.TblCusts.AsNoTracking().FirstOrDefaultAsync(c => c.ID == booking.CustID.Value, ct).ConfigureAwait(false)
                : null;

            var items = await _db.TblItemtrans
                .AsNoTracking()
                .Where(i => i.BookingNoV32 == bookingNo || (bookingIdAsInt.HasValue && i.BookingId == bookingIdAsInt.Value))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var crewRows = await _db.TblCrews
                .AsNoTracking()
                .Where(c => c.BookingNoV32 == bookingNo)
                .OrderBy(c => c.Task).ThenBy(c => c.SeqNo)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // 6. Load inventory master + rate table (sequential — same DbContext).
            var productCodes = items.Select(i => i.ProductCodeV42).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=itemtrans_loaded booking={BookingNo} itemCount={ItemCount} distinctProductCodes={CodeCount}",
                trace,
                bookingNo,
                items.Count,
                productCodes.Count);

            var inventoryItemsList = productCodes.Count > 0
                ? await _db.TblInvmas
                    .AsNoTracking()
                    .Where(inv => productCodes.Contains(inv.product_code))
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
                : new List<TblInvmas>();
            var inventoryItems = inventoryItemsList
                .GroupBy(inv => inv.product_code ?? "")
                .ToDictionary(g => g.Key, g => g.First());

            var rateItemsList = productCodes.Count > 0
                ? await _db.TblRatetbls
                    .AsNoTracking()
                    .Where(r => productCodes.Contains(r.product_code) && r.TableNo == 0)
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
                : new List<TblRatetbl>();
            var rateItems = rateItemsList
                .GroupBy(r => r.product_code ?? "")
                .ToDictionary(g => g.Key, g => g.First());
            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=crew_loaded booking={BookingNo} crewRows={CrewCount}",
                trace,
                bookingNo,
                crewRows.Count);

            // 7. Build quote data (pass session overrides for venue/contact when available)
            string? sessionVenueName = session?.GetString("Draft:VenueName");
            string? sessionContactName = session?.GetString("Draft:ContactName");
            string? sessionOrganisation = session?.GetString("Draft:Organisation");
            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=build_quote_data booking={BookingNo} sessionVenueOverride={HasVenue} sessionContactOverride={HasContact} sessionOrgOverride={HasOrg}",
                trace,
                bookingNo,
                !string.IsNullOrWhiteSpace(sessionVenueName),
                !string.IsNullOrWhiteSpace(sessionContactName),
                !string.IsNullOrWhiteSpace(sessionOrganisation));

            var quoteData = BuildQuoteData(booking, venue, contact, organization, items, inventoryItems, rateItems,
                crewRows, sessionVenueName, sessionContactName, sessionOrganisation);

            // 8. Generate HTML
            var htmlSw = Stopwatch.StartNew();
            var html = GenerateHtml(quoteData);
            htmlSw.Stop();
            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=html_string_done booking={BookingNo} ms={Ms} htmlChars={HtmlChars}",
                trace,
                bookingNo,
                htmlSw.ElapsedMilliseconds,
                html.Length);

            // 9. Save to file (Azure: %HOME%/data/quotes; local: wwwroot/files/quotes)
            var outDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);

            var outName = $"Quote-{bookingNo}-{DateTime.UtcNow:yyyyMMddHHmmss}.html";
            var dest = Path.Combine(outDir, outName);

            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=write_html_prepare booking={BookingNo} outDir={OutDir} fileName={FileName}",
                trace,
                bookingNo,
                outDir,
                outName);

            var writeSw = Stopwatch.StartNew();
            await File.WriteAllTextAsync(dest, html, ct);
            writeSw.Stop();
            long htmlFileLen = 0;
            try
            {
                if (File.Exists(dest))
                    htmlFileLen = new FileInfo(dest).Length;
            }
            catch
            {
                /* ignore */
            }

            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=html_written booking={BookingNo} ms={Ms} bytes={Bytes} path={Path}",
                trace,
                bookingNo,
                writeSw.ElapsedMilliseconds,
                htmlFileLen,
                dest);

            var url = QuoteFilesPaths.PublicUrlForFileName(outName);

            _logger.LogInformation(
                "[QUOTE GEN] trace={Trace} phase=success_html_returned booking={BookingNo} htmlFile={HtmlFile} htmlChars={HtmlChars} publicUrlPath=/files/quotes/{FileName}",
                trace, bookingNo, outName, html.Length, outName);

            return (true, url, null);
        }
        catch (Exception ex)
        {
            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var errorDetail = $"{ex.Message} {(ex.InnerException != null ? "-> " + ex.InnerException.Message : "")}";
            _logger.LogError(ex,
                "[QUOTE GEN] trace={Trace} phase=exception booking={BookingNo} afterMs={ElapsedMs} error={ErrorMessage}",
                trace,
                bookingNo,
                elapsedMs,
                errorDetail);
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
}
