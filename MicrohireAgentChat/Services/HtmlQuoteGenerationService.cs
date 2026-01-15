using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Web;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Generates dynamic HTML quotes based on the quote-viewer.html template.
/// More reliable than PDF generation - returns HTML that can be displayed in an iframe.
/// </summary>
public class HtmlQuoteGenerationService
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
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting HTML quote generation for booking {BookingNo}", bookingNo);

        try
        {
            // 1. Load booking with related data
            var booking = await _db.TblBookings
                .FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);

            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingNo} not found", bookingNo);
                return (false, null, $"Booking {bookingNo} not found");
            }

            // 2. Load venue information
            TblVenue? venue = null;
            if (booking.VenueID > 0)
            {
                venue = await _db.TblVenues
                    .FirstOrDefaultAsync(v => v.ID == booking.VenueID, ct);
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
            var items = await _db.TblItemtrans
                .Where(i => i.BookingNoV32 == bookingNo || i.BookingId == booking.ID)
                .ToListAsync(ct);
            _logger.LogInformation("Loaded {ItemCount} equipment items", items.Count);

            // 6. Load inventory master data for descriptions and prices
            var productCodes = items.Select(i => i.ProductCodeV42).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            var inventoryItems = await _db.TblInvmas
                .Where(inv => productCodes.Contains(inv.product_code))
                .ToDictionaryAsync(inv => inv.product_code ?? "", ct);

            // 6b. Load rate table data for proper pricing
            var rateItems = await _db.TblRatetbls
                .Where(r => productCodes.Contains(r.product_code) && r.TableNo == 0)
                .ToDictionaryAsync(r => r.product_code ?? "", ct);

            // 7. Build quote data
            var quoteData = BuildQuoteData(booking, venue, contact, organization, items, inventoryItems, rateItems);

            // 8. Generate HTML
            var html = GenerateHtml(quoteData);

            // 9. Save to file
            var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var outDir = Path.Combine(webRoot, "files", "quotes");
            Directory.CreateDirectory(outDir);

            var outName = $"Quote-{bookingNo}-{DateTime.UtcNow:yyyyMMddHHmmss}.html";
            var dest = Path.Combine(outDir, outName);

            await File.WriteAllTextAsync(dest, html, ct);
            _logger.LogInformation("HTML quote generated: {FileName}", outName);

            var url = $"/files/quotes/{Uri.EscapeDataString(outName)}";
            return (true, url, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate HTML quote for booking {BookingNo}", bookingNo);
            return (false, null, ex.Message);
        }
    }

    private QuoteHtmlData BuildQuoteData(
        TblBooking booking,
        TblVenue? venue,
        TblContact? contact,
        TblCust? organization,
        List<TblItemtran> items,
        Dictionary<string, TblInvmas> inventoryLookup,
        Dictionary<string, TblRatetbl> rateLookup)
    {
        // Format dates
        var eventDate = booking.dDate ?? booking.ShowSDate ?? DateTime.Now;
        var eventDateStr = eventDate.ToString("dddd d MMMM yyyy");
        var eventDateShort = eventDate.ToString("d MMMM yyyy").ToUpper();

        // Format times
        var setupTime = FormatTime(booking.setupTimeV61) ?? "07:00";
        var rehearsalTime = FormatTime(booking.RehearsalTime) ?? "09:00";
        var eventStartTime = FormatTime(booking.showStartTime) ?? "10:00";
        var eventEndTime = FormatTime(booking.ShowEndTime) ?? "17:00";

        // Build venue information
        var venueName = venue?.VenueName ?? booking.VenueRoom ?? "Venue TBD";
        var venueAddress = venue?.FullAddress ?? organization?.Address_l1V6 ?? "Address TBD";
        var venueRoom = booking.VenueRoom ?? "Room TBD";

        // Build equipment sections
        var equipmentSections = BuildEquipmentSections(items, inventoryLookup, rateLookup);

        // Calculate totals
        decimal equipmentTotal = equipmentSections.Sum(s => s.Items.Sum(i => i.LineTotal));
        decimal gst = equipmentTotal * 0.10m;
        decimal grandTotal = equipmentTotal + gst;

        // Use booking price if set
        if (booking.price_quoted.HasValue && booking.price_quoted.Value > 0)
        {
            grandTotal = (decimal)booking.price_quoted.Value;
            gst = grandTotal / 1.1m * 0.1m;
            equipmentTotal = grandTotal - gst;
        }

        return new QuoteHtmlData
        {
            EventTitle = booking.showName ?? booking.OrganizationV6 ?? "Event",
            Reference = booking.booking_no ?? "TEMP-001",
            EventDate = eventDateShort,
            EventDateFull = eventDateStr,
            
            // Contact info
            ContactName = contact?.Contactname ?? booking.contact_nameV6 ?? "Contact",
            OrganizationName = organization?.OrganisationV6 ?? booking.OrganizationV6 ?? "Organization",
            ContactAddress = organization?.Address_l1V6 ?? "",
            ContactPhone = contact?.Phone1 ?? contact?.Cell ?? "",
            ContactEmail = contact?.Email ?? "",
            
            // Venue info
            VenueName = venueName,
            VenueAddress = venueAddress,
            VenueRoom = venueRoom,
            VenueLocation = venue?.City ?? "Brisbane",
            
            // Account manager
            AccountManagerName = booking.Salesperson ?? "Doug Miller",
            AccountManagerMobile = "+61 420 838 716",
            AccountManagerEmail = "Doug.Miller@microhire.com",
            
            // Schedule
            SetupDate = eventDateStr,
            SetupTime = setupTime,
            RehearsalDate = eventDateStr,
            RehearsalTime = rehearsalTime,
            EventStartDate = eventDateStr,
            EventStartTime = eventStartTime,
            EventEndDate = eventDateStr,
            EventEndTime = eventEndTime,
            
            // Equipment
            EquipmentSections = equipmentSections,
            EquipmentTotal = equipmentTotal,
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

    private string GenerateHtml(QuoteHtmlData data)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Microhire Quote - " + HttpUtility.HtmlEncode(data.EventTitle) + @"</title>
    <link href=""https://fonts.googleapis.com/css2?family=Montserrat:wght@400;500;600;700;800&family=Lato:wght@300;400;700&display=swap"" rel=""stylesheet"">
    <style>" + GetStyles() + @"</style>
</head>
<body>
    <div class=""page-container"">");

        // Page 1: Cover page
        sb.AppendLine(GenerateCoverPage(data));

        // Page 2: Overview page
        sb.AppendLine(GenerateOverviewPage(data));

        // Page 3: Equipment page
        sb.AppendLine(GenerateEquipmentPage(data));

        // Page 4: Technical Services (Labour)
        sb.AppendLine(GenerateTechnicalServicesPage(data));

        // Page 5: Budget Summary & Confirmation
        sb.AppendLine(GenerateBudgetSummaryPage(data));

        sb.AppendLine(@"    </div>
</body>
</html>");

        return sb.ToString();
    }

    private string GenerateCoverPage(QuoteHtmlData data)
    {
        return $@"
        <!-- PAGE 1: COVER -->
        <div class=""page cover-page"">
            <div class=""cover-main"">
                <div class=""cover-shape-1""></div>
                <div class=""cover-shape-2""></div>
                
                <div class=""cover-logo"">
                    <div class=""cover-logo-text""><span>M</span>icrohire.</div>
                    <div class=""cover-tagline"">Events That Inspire</div>
                </div>
                
                <div class=""cover-title"">
                    <h1>{HttpUtility.HtmlEncode(data.EventTitle)}</h1>
                </div>
            </div>
            
            <div class=""cover-footer"">
                <div class=""cover-info"">
                    <div class=""cover-date"">{HttpUtility.HtmlEncode(data.EventDate)}</div>
                    <div class=""cover-ref"">{HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                </div>
                <div class=""cover-venue-logo"">
                    <div class=""venue-name"">{HttpUtility.HtmlEncode(data.VenueName.ToUpper())}</div>
                    <div class=""venue-location"">{HttpUtility.HtmlEncode(data.VenueLocation)}</div>
                </div>
            </div>
        </div>";
    }

    private string GenerateOverviewPage(QuoteHtmlData data)
    {
        return $@"
        <!-- PAGE 2: OVERVIEW -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">OVERVIEW</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""contact-block"">
                    <div class=""contact-name"">{HttpUtility.HtmlEncode(data.ContactName)}</div>
                    <div class=""contact-company"">{HttpUtility.HtmlEncode(data.OrganizationName)}</div>
                    <div class=""contact-details"">
                        {HttpUtility.HtmlEncode(data.ContactAddress)}<br>
                        {HttpUtility.HtmlEncode(data.ContactPhone)}
                    </div>
                </div>
                
                <div class=""greeting"">Dear {HttpUtility.HtmlEncode(data.ContactName.Split(' ')[0])},</div>
                
                <p class=""intro-text"">
                    Thank you for the opportunity to present our audio-visual production services for your upcoming event at {HttpUtility.HtmlEncode(data.VenueName)}. We are pleased to provide our proposal in the following pages, based on the information we have received and our recommendations for a seamless and successful event. Our team are committed to achieving your event objectives and welcome the opportunity to discuss your requirements and any budget parameters in further detail. If you have any questions or need further information, please do not hesitate to contact me on the details below. Thank you again and we look forward to speaking to you soon.
                </p>
                
                <div class=""details-table"">
                    <div class=""detail-row"">
                        <div class=""detail-label"">Location</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.VenueName)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Address</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.VenueAddress)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Room</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.VenueRoom)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Date</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.EventDateFull)} to {HttpUtility.HtmlEncode(data.EventDateFull)}</div>
                    </div>
                </div>
                
                <div class=""section-divider""></div>
                
                <div class=""manager-section"">
                    <div class=""detail-row"">
                        <div class=""detail-label"">Event Account Manager</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.AccountManagerName)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Mobile Number</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.AccountManagerMobile)}</div>
                    </div>
                    <div class=""detail-row"">
                        <div class=""detail-label"">Email</div>
                        <div class=""detail-value"">{HttpUtility.HtmlEncode(data.AccountManagerEmail)}</div>
                    </div>
                </div>
                
                <p class=""schedule-intro"">Please confirm the following dates and times are accurate:</p>
                
                <div class=""schedule-table"">
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Setup By</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.SetupDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.SetupTime)}</div>
                    </div>
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Rehearsal</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.RehearsalDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.RehearsalTime)}</div>
                    </div>
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Event Start</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.EventStartDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.EventStartTime)}</div>
                    </div>
                    <div class=""schedule-row"">
                        <div class=""schedule-label"">Event End</div>
                        <div class=""schedule-date"">{HttpUtility.HtmlEncode(data.EventEndDate)}</div>
                        <div class=""schedule-time"">Time: {HttpUtility.HtmlEncode(data.EventEndTime)}</div>
                    </div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 2</div>
            </div>
        </div>";
    }

    private string GenerateEquipmentPage(QuoteHtmlData data)
    {
        var equipmentHtml = new StringBuilder();

        // Group equipment by Vision/Audio/etc
        var visionItems = data.EquipmentSections
            .Where(s => s.Category.Contains("Vision") || s.Category.Contains("Projector") || s.Category.Contains("Screen"))
            .SelectMany(s => s.Items).ToList();
        var audioItems = data.EquipmentSections
            .Where(s => s.Category.Contains("Audio") || s.Category.Contains("Microphone") || s.Category.Contains("Speaker"))
            .SelectMany(s => s.Items).ToList();

        // Vision section
        if (visionItems.Any() || data.EquipmentSections.Any())
        {
            equipmentHtml.AppendLine(@"
                <div class=""equipment-category-header"">Vision</div>");
            
            foreach (var section in data.EquipmentSections.Where(s => 
                s.Category.Contains("Vision") || s.Category.Contains("Projector") || 
                s.Category.Contains("Screen") || s.Category.Contains("AV")))
            {
                if (!string.IsNullOrEmpty(section.SubCategory))
                {
                    equipmentHtml.AppendLine($@"                <div class=""equipment-subcategory"">{HttpUtility.HtmlEncode(section.SubCategory)}</div>");
                }
                equipmentHtml.AppendLine($@"                <div class=""equipment-group-title"">Includes:</div>");

                foreach (var item in section.Items)
                {
                    var itemClass = item.IsComponent ? "component" : "";
                    equipmentHtml.AppendLine($@"
                    <div class=""equipment-row"">
                        <div class=""equipment-item {itemClass}"">{HttpUtility.HtmlEncode(item.Description)}</div>
                        <div class=""equipment-qty"">{(item.Quantity > 0 ? item.Quantity : 1)}</div>
                    </div>");
                }
            }

            // Vision total
            decimal visionTotal = data.EquipmentSections
                .Where(s => s.Category.Contains("Vision") || s.Category.Contains("Projector") || 
                    s.Category.Contains("Screen") || s.Category.Contains("AV"))
                .Sum(s => s.Items.Sum(i => i.LineTotal));
            equipmentHtml.AppendLine($@"
                <div class=""section-total"">
                    <div class=""section-total-label"">Vision Total</div>
                    <div class=""section-total-amount"">{visionTotal:C}</div>
                </div>");
        }

        // Audio section
        if (data.EquipmentSections.Any(s => s.Category.Contains("Audio") || s.Category.Contains("Microphone") || s.Category.Contains("Speaker")))
        {
            equipmentHtml.AppendLine(@"
                <div class=""equipment-category-header"">Audio</div>");

            foreach (var section in data.EquipmentSections.Where(s => 
                s.Category.Contains("Audio") || s.Category.Contains("Microphone") || s.Category.Contains("Speaker")))
            {
                foreach (var item in section.Items)
                {
                    var itemClass = item.IsComponent ? "component" : "";
                    equipmentHtml.AppendLine($@"
                    <div class=""equipment-row"">
                        <div class=""equipment-item {itemClass}"">{HttpUtility.HtmlEncode(item.Description)}</div>
                        <div class=""equipment-qty"">{(item.Quantity > 0 ? item.Quantity : 1)}</div>
                    </div>");
                }
            }

            // Audio total
            decimal audioTotal = data.EquipmentSections
                .Where(s => s.Category.Contains("Audio") || s.Category.Contains("Microphone") || s.Category.Contains("Speaker"))
                .Sum(s => s.Items.Sum(i => i.LineTotal));
            equipmentHtml.AppendLine($@"
                <div class=""section-total"">
                    <div class=""section-total-label"">Audio Total</div>
                    <div class=""section-total-amount"">{audioTotal:C}</div>
                </div>");
        }

        // Other equipment sections
        var otherSections = data.EquipmentSections.Where(s => 
            !s.Category.Contains("Vision") && !s.Category.Contains("Projector") && 
            !s.Category.Contains("Screen") && !s.Category.Contains("AV") &&
            !s.Category.Contains("Audio") && !s.Category.Contains("Microphone") && 
            !s.Category.Contains("Speaker")).ToList();

        foreach (var section in otherSections)
        {
            equipmentHtml.AppendLine($@"
                <div class=""equipment-category-header"">{HttpUtility.HtmlEncode(section.Category)}</div>");

            foreach (var item in section.Items)
            {
                equipmentHtml.AppendLine($@"
                    <div class=""equipment-row"">
                        <div class=""equipment-item"">{HttpUtility.HtmlEncode(item.Description)}</div>
                        <div class=""equipment-qty"">{(item.Quantity > 0 ? item.Quantity : 1)}</div>
                    </div>");
            }
        }

        return $@"
        <!-- PAGE 3: EQUIPMENT & SERVICES -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">EQUIPMENT & SERVICES</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""equipment-header"">
                    <div class=""equipment-header-desc"">Description</div>
                    <div class=""equipment-header-qty"">Qty.</div>
                </div>

                <div class=""room-header"">
                    <div class=""room-name"">{HttpUtility.HtmlEncode(data.VenueRoom)}</div>
                    <div class=""room-times"">Event Starts - {data.EventStartTime?.Replace(":", "")}<br>Event Ends - {data.EventEndTime?.Replace(":", "")}</div>
                </div>
                
                {equipmentHtml}
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 2</div>
            </div>
        </div>";
    }

    private string GenerateTechnicalServicesPage(QuoteHtmlData data)
    {
        var labourHtml = new StringBuilder();
        decimal labourTotal = 0;

        // Build labour rows from LaborItems
        if (data.LaborItems.Any())
        {
            foreach (var labor in data.LaborItems)
            {
                labourHtml.AppendLine($@"
                    <tr>
                        <td>{HttpUtility.HtmlEncode(labor.Description)}</td>
                        <td>{HttpUtility.HtmlEncode(labor.Task)}</td>
                        <td>{labor.Quantity}</td>
                        <td>{labor.StartDateTime}</td>
                        <td>{labor.EndDateTime}</td>
                        <td>{labor.Hours}</td>
                        <td>{labor.LineTotal:C}</td>
                    </tr>");
                labourTotal += labor.LineTotal;
            }
        }
        else
        {
            // Default labor rows if none specified
            labourHtml.AppendLine($@"
                <tr>
                    <td>AV Technician</td>
                    <td>Setup</td>
                    <td>1</td>
                    <td>{data.SetupDate?.Substring(Math.Max(0, data.SetupDate.Length - 10))} {data.SetupTime}</td>
                    <td>{data.EventStartDate?.Substring(Math.Max(0, data.EventStartDate.Length - 10))} {data.EventStartTime}</td>
                    <td>01:30</td>
                    <td>$165.00</td>
                </tr>
                <tr>
                    <td>AV Technician</td>
                    <td>Test & Connect</td>
                    <td>1</td>
                    <td>{data.RehearsalDate?.Substring(Math.Max(0, data.RehearsalDate.Length - 10))} {data.RehearsalTime}</td>
                    <td>{data.EventStartDate?.Substring(Math.Max(0, data.EventStartDate.Length - 10))} {data.EventStartTime}</td>
                    <td>01:00</td>
                    <td>$110.00</td>
                </tr>
                <tr>
                    <td>AV Technician</td>
                    <td>Pack Down</td>
                    <td>1</td>
                    <td>{data.EventEndDate?.Substring(Math.Max(0, data.EventEndDate.Length - 10))} {data.EventEndTime}</td>
                    <td>{data.EventEndDate?.Substring(Math.Max(0, data.EventEndDate.Length - 10))} 18:00</td>
                    <td>01:00</td>
                    <td>$110.00</td>
                </tr>");
            labourTotal = 385.00m;
        }

        data.LabourTotal = labourTotal;

        return $@"
        <!-- PAGE 4: TECHNICAL SERVICES -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">TECHNICAL SERVICES</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <table class=""labour-table"">
                    <thead>
                        <tr>
                            <th>Description</th>
                            <th>Task</th>
                            <th>Qty</th>
                            <th>Start Date/Time</th>
                            <th>Finish</th>
                            <th>Hrs</th>
                            <th>Total ($)</th>
                        </tr>
                    </thead>
                    <tbody>
                        {labourHtml}
                    </tbody>
                </table>
                
                <div class=""section-total labour-total"">
                    <div class=""section-total-label"">Labour Total</div>
                    <div class=""section-total-amount"">{labourTotal:C}</div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 3</div>
            </div>
        </div>";
    }

    private string GenerateBudgetSummaryPage(QuoteHtmlData data)
    {
        // Calculate service charge (7%)
        decimal serviceCharge = data.EquipmentTotal * 0.07m;
        decimal subTotalExGst = data.EquipmentTotal + data.LabourTotal + serviceCharge;
        decimal gst = subTotalExGst * 0.10m;
        decimal grandTotal = subTotalExGst + gst;

        // Update data totals
        data.Gst = gst;
        data.GrandTotal = grandTotal;

        // Calculate valid until date (30 days from now)
        var validUntil = DateTime.Now.AddDays(30).ToString("ddd d MMM yyyy").ToUpper();

        return $@"
        <!-- PAGE 5: BUDGET SUMMARY -->
        <div class=""page overview-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">BUDGET SUMMARY</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""budget-table"">
                    <div class=""budget-row"">
                        <div class=""budget-label"">Rental Equipment</div>
                        <div class=""budget-value"">{data.EquipmentTotal:C}</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">Labour</div>
                        <div class=""budget-value"">{data.LabourTotal:C}</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">Service Charge</div>
                        <div class=""budget-value"">{serviceCharge:C}</div>
                    </div>
                    <div class=""budget-row budget-divider"">
                        <div class=""budget-label""></div>
                        <div class=""budget-value"">----------------------</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">Sub Total (ex GST)</div>
                        <div class=""budget-value"">{subTotalExGst:C}</div>
                    </div>
                    <div class=""budget-row"">
                        <div class=""budget-label"">GST</div>
                        <div class=""budget-value"">{gst:C}</div>
                    </div>
                    <div class=""budget-row budget-divider"">
                        <div class=""budget-label""></div>
                        <div class=""budget-value"">----------------------</div>
                    </div>
                    <div class=""budget-row budget-total"">
                        <div class=""budget-label"">Total</div>
                        <div class=""budget-value"">{grandTotal:C}</div>
                    </div>
                </div>
                
                <div class=""closing-text"">
                    <p>The team at Microhire look forward to working with you to make every aspect of your event a success. To ensure that your event receives the best possible equipment and technical personnel, please confirm that all details are correct including dates, timing and quantities. Note that our pricing is <strong>valid until {validUntil}</strong> and our resources are subject to availability at the time of booking.</p>
                    <p>Please confirm your acceptance of the proposal and its inclusions by returning a signed copy of the Confirmation of Services page, so we can proceed with your requirements.</p>
                    <p>However, if you wish to discuss any additions or updates regarding our proposal, please do not hesitate to contact me on the details below.</p>
                    <p>We look forward to working with you on a seamless and successful event.</p>
                </div>
                
                <div class=""manager-signature"">
                    <div class=""manager-name"">{HttpUtility.HtmlEncode(data.AccountManagerName)} <span>Event Staging Manager</span></div>
                    <div class=""manager-company""><strong>Microhire</strong> | {HttpUtility.HtmlEncode(data.VenueName)}</div>
                    <div class=""manager-address"">Microhire @ 111 Mary St, Brisbane City QLD 4000</div>
                    <div class=""manager-contact"">
                        <span class=""label"">M</span> {HttpUtility.HtmlEncode(data.AccountManagerMobile)}<br>
                        <span class=""label"">T</span><br>
                        <span class=""label"">E</span> {HttpUtility.HtmlEncode(data.AccountManagerEmail)}
                    </div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 4</div>
            </div>
        </div>

        <!-- PAGE 6: CONFIRMATION OF SERVICES -->
        <div class=""page overview-page confirmation-page"">
            <div class=""page-header"">
                <div class=""page-section-title"">CONFIRMATION OF SERVICES</div>
                <div class=""page-event-title"">{HttpUtility.HtmlEncode(data.EventTitle.ToUpper())}</div>
            </div>
            
            <div class=""page-content"">
                <div class=""confirmation-text"">
                    <p>On behalf of {HttpUtility.HtmlEncode(data.OrganizationName)}, I accept this proposal and wish to proceed with the details that are confirmed to be correct.</p>
                    <p>Upon request, any additions or amendments will be updated to this proposal accordingly.</p>
                    <p>We understand that equipment and personnel are not allocated until this document is signed and returned.</p>
                    <p>This proposal and billing details are subject to Microhire's terms and conditions.<br>
                    <a href=""https://www.microhire.com.au/terms-conditions/"" target=""_blank"">https://www.microhire.com.au/terms-conditions/</a></p>
                </div>
                
                <div class=""confirmation-details"">
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">Reference Number</div>
                        <div class=""confirmation-value"">{HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                    </div>
                    <div class=""confirmation-row"">
                        <div class=""confirmation-label"">Total Quotation</div>
                        <div class=""confirmation-value"">{grandTotal:C} inc GST</div>
                    </div>
                </div>
                
                <div class=""signature-section"">
                    <div class=""signature-row"">
                        <div class=""signature-field"">
                            <label>Full Name</label>
                            <div class=""signature-line""></div>
                        </div>
                        <div class=""signature-field"">
                            <label>Date</label>
                            <div class=""signature-line""></div>
                        </div>
                    </div>
                    <div class=""signature-row"">
                        <div class=""signature-field"">
                            <label>Title / Position</label>
                            <div class=""signature-line""></div>
                        </div>
                        <div class=""signature-field"">
                            <label>Purchase Order (if applicable)</label>
                            <div class=""signature-line""></div>
                        </div>
                    </div>
                    <div class=""signature-row"">
                        <div class=""signature-field full-width"">
                            <label>Signature</label>
                            <div class=""signature-line signature-box""></div>
                        </div>
                    </div>
                </div>
            </div>
            
            <div class=""page-footer"">
                <div class=""footer-logo"">
                    <div class=""footer-logo-icon""></div>
                </div>
                <div class=""footer-ref"">Ref No: {HttpUtility.HtmlEncode(data.Reference)} - 001</div>
                <div class=""footer-page-info"">Microhire | {HttpUtility.HtmlEncode(data.EventTitle)} 5</div>
            </div>
        </div>";
    }

    private string GetStyles()
    {
        return @"
        :root {
            --microhire-red: #E81E25;
            --microhire-red-dark: #C91A20;
            --microhire-red-darker: #9E1D20;
            --microhire-accent: #FF3262;
            --text-dark: #1a1a1a;
            --text-gray: #4a4a4a;
            --text-light: #6b6b6b;
            --bg-light: #f5f5f5;
            --white: #ffffff;
        }

        * { margin: 0; padding: 0; box-sizing: border-box; }

        body {
            font-family: 'Lato', sans-serif;
            background: #2a2a2a;
            color: var(--text-dark);
            line-height: 1.6;
        }

        .page-container { max-width: 850px; margin: 0 auto; padding: 20px; }

        .page {
            background: var(--white);
            width: 100%;
            min-height: 1100px;
            margin-bottom: 40px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.4);
            position: relative;
            overflow: hidden;
        }

        /* Cover Page */
        .cover-page { background: var(--microhire-red); display: flex; flex-direction: column; }
        .cover-main { flex: 1; min-height: 900px; position: relative; padding: 40px; display: flex; flex-direction: column; }
        .cover-shape-1 { position: absolute; top: 0; left: 0; width: 50%; height: 70%; background: var(--microhire-red-dark); clip-path: polygon(0 0, 100% 0, 50% 100%, 0 100%); }
        .cover-shape-2 { position: absolute; top: 20%; right: 0; width: 55%; height: 80%; background: var(--microhire-red-darker); clip-path: polygon(40% 0, 100% 0, 100% 100%, 0 100%); }
        .cover-logo { position: relative; z-index: 10; text-align: right; margin-bottom: auto; }
        .cover-logo-text { font-family: 'Montserrat', sans-serif; font-weight: 800; font-size: 48px; color: var(--white); letter-spacing: 2px; }
        .cover-logo-text span { color: var(--white); font-weight: 300; }
        .cover-tagline { font-family: 'Montserrat', sans-serif; font-size: 11px; color: var(--white); letter-spacing: 4px; text-transform: uppercase; opacity: 0.9; }
        .cover-title { position: relative; z-index: 10; margin-top: auto; padding-bottom: 40px; }
        .cover-title h1 { font-family: 'Montserrat', sans-serif; font-weight: 300; font-size: clamp(32px, 5vw, 52px); color: var(--white); line-height: 1.2; }
        .cover-footer { background: var(--white); padding: 30px 40px; display: flex; justify-content: space-between; align-items: flex-end; }
        .cover-date { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 18px; color: var(--microhire-red); }
        .cover-ref { font-family: 'Montserrat', sans-serif; font-weight: 500; font-size: 14px; color: var(--microhire-red); margin-top: 5px; }
        .cover-venue-logo { text-align: right; }
        .venue-name { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 20px; color: var(--text-dark); letter-spacing: 2px; }
        .venue-location { font-family: 'Montserrat', sans-serif; font-size: 11px; color: var(--text-gray); letter-spacing: 3px; text-transform: uppercase; }

        /* Overview Page */
        .overview-page { padding: 0; display: flex; flex-direction: column; }
        .page-header { padding: 35px 45px 25px; border-bottom: 3px solid var(--microhire-red); }
        .page-section-title { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 14px; color: var(--microhire-red-darker); letter-spacing: 1px; margin-bottom: 8px; }
        .page-event-title { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: clamp(18px, 3vw, 24px); color: var(--microhire-red); display: flex; align-items: center; gap: 12px; }
        .page-event-title::before { content: ''; width: 0; height: 0; border-top: 10px solid transparent; border-bottom: 10px solid transparent; border-left: 16px solid var(--microhire-red); }
        .page-content { flex: 1; padding: 30px 45px; display: flex; flex-direction: column; }
        .contact-block { margin-bottom: 25px; }
        .contact-name { font-weight: 700; font-size: 15px; color: var(--text-dark); }
        .contact-company { font-weight: 400; font-size: 14px; color: var(--text-dark); }
        .contact-details { font-size: 13px; color: var(--text-gray); margin-top: 4px; }
        .greeting { font-size: 14px; color: var(--text-dark); margin: 20px 0 15px; }
        .intro-text { font-size: 13px; color: var(--text-gray); line-height: 1.7; text-align: justify; margin-bottom: 25px; }
        .details-table { margin-bottom: 25px; }
        .detail-row { display: flex; padding: 8px 0; border-bottom: 1px solid #eee; }
        .detail-row:last-child { border-bottom: none; }
        .detail-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; letter-spacing: 0.5px; width: 180px; flex-shrink: 0; }
        .detail-value { font-size: 13px; color: var(--text-dark); }
        .section-divider { height: 1px; background: linear-gradient(90deg, var(--microhire-red) 0%, transparent 100%); margin: 20px 0; opacity: 0.3; }
        .manager-section { margin-bottom: 25px; }
        .manager-section .detail-row { border-bottom: none; padding: 5px 0; }
        .schedule-intro { font-size: 12px; font-style: italic; color: var(--text-gray); margin-bottom: 15px; }
        .schedule-table { width: 100%; }
        .schedule-row { display: flex; padding: 6px 0; }
        .schedule-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; width: 130px; flex-shrink: 0; }
        .schedule-date { font-size: 13px; color: var(--text-dark); width: 220px; }
        .schedule-time { font-size: 13px; color: var(--text-gray); }
        .page-footer { padding: 20px 45px; display: flex; justify-content: space-between; align-items: center; border-top: 1px solid #eee; margin-top: auto; }
        .footer-logo { display: flex; align-items: center; gap: 8px; }
        .footer-logo-icon { width: 40px; height: 40px; background: var(--microhire-red); border-radius: 50%; display: flex; align-items: center; justify-content: center; position: relative; }
        .footer-logo-icon::after { content: 'M'; color: white; font-family: 'Montserrat', sans-serif; font-weight: 800; font-size: 16px; }
        .footer-ref { font-size: 11px; color: var(--text-light); }
        .footer-page-info { font-size: 11px; color: var(--text-light); }

        /* Equipment Page */
        .equipment-table { width: 100%; margin-bottom: 20px; }
        .equipment-header { display: flex; padding: 12px 0; border-bottom: 2px solid var(--microhire-red); margin-bottom: 15px; }
        .equipment-header-desc { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 12px; color: var(--microhire-red); flex: 1; }
        .equipment-header-qty { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 12px; color: var(--microhire-red); width: 80px; text-align: center; }
        .room-header { margin-bottom: 20px; }
        .room-name { font-weight: 700; font-size: 14px; color: var(--text-dark); }
        .room-times { font-style: italic; font-size: 12px; color: var(--text-gray); margin-top: 4px; }
        .equipment-category-header { font-style: italic; font-weight: 700; font-size: 14px; color: var(--text-dark); margin: 20px 0 10px; }
        .equipment-section { margin-bottom: 20px; }
        .equipment-category { font-style: italic; font-weight: 600; font-size: 13px; color: var(--text-dark); margin-bottom: 8px; }
        .equipment-subcategory { font-style: italic; text-decoration: underline; font-size: 13px; color: var(--text-dark); margin-bottom: 8px; }
        .equipment-group-title { font-style: italic; font-size: 12px; color: var(--text-gray); margin-bottom: 5px; }
        .equipment-row { display: flex; padding: 4px 0; align-items: center; }
        .equipment-item { flex: 1; font-size: 13px; color: var(--text-dark); }
        .equipment-item.main { font-weight: 600; }
        .equipment-item.component { margin-left: 15px; font-style: italic; color: var(--text-gray); }
        .equipment-qty { width: 80px; text-align: center; font-size: 13px; color: var(--text-dark); }
        .equipment-note { font-style: italic; font-size: 13px; color: var(--text-dark); margin: 15px 0 15px 15px; }
        .section-total { display: flex; justify-content: flex-end; padding: 15px 0; margin-top: 10px; border-top: 1px solid #eee; }
        .section-total-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 13px; color: var(--text-dark); margin-right: 30px; }
        .section-total-amount { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 14px; color: var(--text-dark); }

        /* Labour/Technical Services Page */
        .labour-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
        .labour-table th { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; text-align: left; padding: 10px 8px; border-bottom: 2px solid var(--microhire-red); }
        .labour-table td { font-size: 12px; color: var(--text-dark); padding: 8px; border-bottom: 1px solid #eee; }
        .labour-total { margin-top: 20px; }

        /* Budget Summary Page */
        .budget-table { max-width: 350px; margin-bottom: 30px; }
        .budget-row { display: flex; justify-content: space-between; padding: 6px 0; }
        .budget-label { font-size: 13px; color: var(--text-dark); }
        .budget-value { font-size: 13px; color: var(--text-dark); text-align: right; }
        .budget-divider .budget-value { color: var(--text-gray); }
        .budget-total { font-weight: 700; }
        .budget-total .budget-label, .budget-total .budget-value { font-size: 14px; }

        .closing-text { font-size: 12px; color: var(--text-gray); line-height: 1.7; margin-bottom: 30px; }
        .closing-text p { margin-bottom: 12px; }
        .closing-text strong { color: var(--text-dark); }

        .manager-signature { margin-top: 30px; }
        .manager-name { font-family: 'Montserrat', sans-serif; font-weight: 700; font-size: 16px; color: var(--text-dark); margin-bottom: 5px; }
        .manager-name span { font-weight: 400; font-size: 13px; margin-left: 10px; }
        .manager-company { font-size: 13px; color: var(--text-dark); margin-bottom: 3px; }
        .manager-address { font-size: 12px; color: var(--text-gray); margin-bottom: 10px; }
        .manager-contact { font-size: 12px; color: var(--text-dark); line-height: 1.8; }
        .manager-contact .label { color: var(--microhire-red); font-weight: 600; margin-right: 10px; }

        /* Confirmation Page */
        .confirmation-page .confirmation-text { font-size: 12px; color: var(--text-gray); line-height: 1.7; margin-bottom: 30px; }
        .confirmation-page .confirmation-text p { margin-bottom: 12px; }
        .confirmation-page .confirmation-text a { color: var(--microhire-red); }
        .confirmation-details { margin-bottom: 30px; }
        .confirmation-row { display: flex; margin-bottom: 10px; }
        .confirmation-label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--microhire-red); text-transform: uppercase; width: 150px; }
        .confirmation-value { font-size: 13px; color: var(--text-dark); }
        
        .signature-section { margin-top: 30px; }
        .signature-row { display: flex; gap: 30px; margin-bottom: 25px; }
        .signature-field { flex: 1; }
        .signature-field.full-width { flex: 1 1 100%; }
        .signature-field label { font-family: 'Montserrat', sans-serif; font-weight: 600; font-size: 11px; color: var(--text-gray); text-transform: uppercase; display: block; margin-bottom: 8px; }
        .signature-line { border-bottom: 1px solid var(--text-dark); height: 30px; }
        .signature-box { height: 80px; border: 1px solid var(--text-dark); border-radius: 4px; }

        @media print {
            body { background: white; }
            .page-container { padding: 0; }
            .page { box-shadow: none; margin-bottom: 0; page-break-after: always; }
            .page:last-child { page-break-after: avoid; }
        }

        @media (max-width: 600px) {
            .page-container { padding: 10px; }
            .cover-main { padding: 25px; }
            .cover-footer { padding: 20px 25px; }
            .page-header, .page-content, .page-footer { padding-left: 25px; padding-right: 25px; }
            .detail-label, .schedule-label { width: 120px; }
        }";
    }

    private decimal GetUnitPrice(TblItemtran item, TblInvmas? invItem, TblRatetbl? rateItem)
    {
        if (item.Price.HasValue && item.Price.Value > 0)
            return (decimal)item.Price.Value;
        if (item.UnitRate.HasValue && item.UnitRate.Value > 0)
            return (decimal)item.UnitRate.Value;
        if (rateItem?.rate_1st_day.HasValue == true && rateItem.rate_1st_day.Value > 0)
            return (decimal)rateItem.rate_1st_day.Value;
        if (invItem?.retail_price.HasValue == true && invItem.retail_price.Value > 0)
            return (decimal)invItem.retail_price.Value;
        return 0;
    }

    private string GetBestDescription(TblItemtran item, TblInvmas? invItem)
    {
        if (!string.IsNullOrWhiteSpace(item.CommentDescV42))
            return item.CommentDescV42.Trim();
        if (invItem != null)
        {
            if (!string.IsNullOrWhiteSpace(invItem.PrintedDesc))
                return invItem.PrintedDesc.Trim();
            if (!string.IsNullOrWhiteSpace(invItem.descriptionv6))
                return invItem.descriptionv6.Trim();
        }
        return item.ProductCodeV42?.Trim() ?? "Equipment Item";
    }

    private string? FormatTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return null;

        // Time is stored as HHmm (e.g., "0900")
        if (time.Length == 4 && int.TryParse(time, out _))
        {
            return $"{time.Substring(0, 2)}:{time.Substring(2, 2)}";
        }

        return time;
    }
}

// Supporting data models
public class QuoteHtmlData
{
    public string EventTitle { get; set; } = "";
    public string Reference { get; set; } = "";
    public string EventDate { get; set; } = "";
    public string EventDateFull { get; set; } = "";
    
    public string ContactName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string ContactAddress { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    
    public string VenueName { get; set; } = "";
    public string VenueAddress { get; set; } = "";
    public string VenueRoom { get; set; } = "";
    public string VenueLocation { get; set; } = "";
    
    public string AccountManagerName { get; set; } = "";
    public string AccountManagerMobile { get; set; } = "";
    public string AccountManagerEmail { get; set; } = "";
    
    public string SetupDate { get; set; } = "";
    public string SetupTime { get; set; } = "";
    public string RehearsalDate { get; set; } = "";
    public string RehearsalTime { get; set; } = "";
    public string EventStartDate { get; set; } = "";
    public string EventStartTime { get; set; } = "";
    public string EventEndDate { get; set; } = "";
    public string EventEndTime { get; set; } = "";
    
    public List<QuoteEquipmentSection> EquipmentSections { get; set; } = new();
    public List<QuoteLaborItem> LaborItems { get; set; } = new();
    public decimal EquipmentTotal { get; set; }
    public decimal LabourTotal { get; set; }
    public decimal Gst { get; set; }
    public decimal GrandTotal { get; set; }
}

public class QuoteLaborItem
{
    public string Description { get; set; } = "";
    public string Task { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string StartDateTime { get; set; } = "";
    public string EndDateTime { get; set; } = "";
    public string Hours { get; set; } = "";
    public decimal LineTotal { get; set; }
}

public class QuoteEquipmentSection
{
    public string Category { get; set; } = "";
    public string? SubCategory { get; set; }
    public List<QuoteEquipmentItem> Items { get; set; } = new();
}

public class QuoteEquipmentItem
{
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsMainItem { get; set; }
    public bool IsComponent { get; set; }
}

