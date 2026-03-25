using MicrohireAgentChat.Data;
using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

public class QuoteGenerationService
{
    private readonly BookingDbContext _db;
    private readonly PdfFromBlankService _pdfService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<QuoteGenerationService> _logger;

    public QuoteGenerationService(
        BookingDbContext db,
        PdfFromBlankService pdfService,
        IWebHostEnvironment env,
        ILogger<QuoteGenerationService> logger)
    {
        _db = db;
        _pdfService = pdfService;
        _env = env;
        _logger = logger;
    }

    public async Task<(bool success, string? pdfUrl, string? error)> GenerateQuoteForBookingAsync(
        string bookingNo,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting quote generation for booking {BookingNo}", bookingNo);

        try
        {
            // 1. Load booking with related data
            _logger.LogInformation("Loading booking data for {BookingNo}", bookingNo);
            var booking = await _db.TblBookings
                .FirstOrDefaultAsync(b => b.booking_no == bookingNo, ct);

            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingNo} not found", bookingNo);
                return (false, null, $"Booking {bookingNo} not found");
            }

            _logger.LogInformation("Booking {BookingNo} found with ID {BookingId}", bookingNo, booking.ID);

            // 2. Load venue information
            _logger.LogInformation("Loading venue data for VenueID {VenueID}", booking.VenueID);
            TblVenue? venue = null;
            if (booking.VenueID > 0)
            {
                venue = await _db.TblVenues
                    .FirstOrDefaultAsync(v => v.ID == booking.VenueID, ct);
                if (venue != null)
                {
                    _logger.LogInformation("Venue loaded: {VenueName}", venue.VenueName);
                }
            }

            // 3. Load contact
            _logger.LogInformation("Loading contact data for booking {BookingNo}", bookingNo);
            TblContact? contact = null;
            if (booking.ContactID.HasValue)
            {
                _logger.LogInformation("Loading contact with ID {ContactId}", booking.ContactID.Value);
                contact = await _db.Contacts
                    .FirstOrDefaultAsync(c => c.Id == booking.ContactID.Value, ct);
                if (contact == null)
                {
                    _logger.LogWarning("Contact with ID {ContactId} not found", booking.ContactID.Value);
                }
                else
                {
                    _logger.LogInformation("Contact loaded: {ContactName}", contact.Contactname);
                }
            }
            else
            {
                _logger.LogWarning("Booking {BookingNo} has no ContactID", bookingNo);
            }

            // 4. Load organization
            _logger.LogInformation("Loading organization data for booking {BookingNo}", bookingNo);
            TblCust? organization = null;
            if (booking.CustID.HasValue)
            {
                _logger.LogInformation("Loading organization with ID {OrgId}", booking.CustID.Value);
                organization = await _db.TblCusts
                    .FirstOrDefaultAsync(c => c.ID == booking.CustID.Value, ct);
                if (organization == null)
                {
                    _logger.LogWarning("Organization with ID {OrgId} not found", booking.CustID.Value);
                }
                else
                {
                    _logger.LogInformation("Organization loaded: {OrgName}", organization.OrganisationV6);
                }
            }
            else
            {
                _logger.LogWarning("Booking {BookingNo} has no CustID", bookingNo);
            }

            // 5. Load equipment items with inventory details for pricing
            _logger.LogInformation("Loading equipment items for booking {BookingNo} (ID: {BookingId})", bookingNo, booking.ID);
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
            _logger.LogInformation("Loaded {InventoryCount} inventory items for price/description lookup", inventoryItems.Count);

            // 6b. Load rate table data for proper pricing (tableNo=0 is default retail rate)
            var rateItemsList = await _db.TblRatetbls
                .Where(r => productCodes.Contains(r.product_code) && r.TableNo == 0)
                .ToListAsync(ct);
            var rateItems = rateItemsList
                .GroupBy(r => r.product_code ?? "")
                .ToDictionary(g => g.Key, g => g.First());
            _logger.LogInformation("Loaded {RateCount} rate table entries for pricing", rateItems.Count);

            // 7. Load crew/labor
            _logger.LogInformation("Loading crew/labor data for booking {BookingNo}", bookingNo);
            var crew = await _db.TblCrews
                .Where(c => c.BookingNoV32 == bookingNo)
                .ToListAsync(ct);
            _logger.LogInformation("Loaded {CrewCount} crew members", crew.Count);

            // 8. Build QuoteAllFields
            _logger.LogInformation("Building quote fields for booking {BookingNo}", bookingNo);
            var quoteFields = BuildQuoteFields(booking, venue, contact, organization, items, inventoryItems, rateItems, crew);
            _logger.LogInformation("Quote fields built successfully for booking {BookingNo}", bookingNo);

            // 9. Generate PDF
            _logger.LogInformation("Generating PDF for booking {BookingNo}", bookingNo);
            try
            {
                var (fileName, fullPath) = _pdfService.Generate(quoteFields, bookingNo);
                _logger.LogInformation("PDF generated successfully: {FileName}", fileName);

                // 10. Return URL
                var url = QuoteFilesPaths.PublicUrlForFileName(fileName);
                _logger.LogInformation("Quote generation completed for booking {BookingNo}: {Url}", bookingNo, url);

                return (true, url, null);
            }
            catch (Exception pdfEx)
            {
                _logger.LogError(pdfEx, "PDF generation failed for booking {BookingNo}", bookingNo);
                return (false, null, $"PDF generation failed: {pdfEx.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate quote for booking {BookingNo}", bookingNo);
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Builds the complete QuoteAllFields from all the database entities
    /// </summary>
    private QuoteAllFields BuildQuoteFields(
        TblBooking booking,
        TblVenue? venue,
        TblContact? contact,
        TblCust? organization,
        List<TblItemtran> items,
        Dictionary<string, TblInvmas> inventoryLookup,
        Dictionary<string, TblRatetbl> rateLookup,
        List<TblCrew> crew)
    {
        // Format dates
        var eventDate = booking.dDate?.ToString("dddd d MMMM yyyy") ?? booking.ShowSDate?.ToString("dddd d MMMM yyyy") ?? "";
        var setupDate = booking.SetDate?.ToString("dddd d MMMM yyyy") ?? eventDate;
        var rehearsalDate = booking.RehDate?.ToString("dddd d MMMM yyyy") ?? eventDate;
        var showStartDate = booking.ShowSDate?.ToString("dddd d MMMM yyyy") ?? eventDate;
        var showEndDate = booking.ShowEdate?.ToString("dddd d MMMM yyyy") ?? eventDate;
        var dateRange = string.Equals(showStartDate, showEndDate, StringComparison.Ordinal)
            ? showStartDate
            : $"{showStartDate} to {showEndDate}";

        // Format times
        var setupTime = FormatTime(booking.setupTimeV61);
        var rehearsalTime = FormatTime(booking.RehearsalTime);
        var eventStartTime = FormatTime(booking.showStartTime);
        var eventEndTime = FormatTime(booking.ShowEndTime);

        // All supported rooms are at the Westin Brisbane
        var venueName = WestinRoomCatalog.VenueName;
        var venueAddress = WestinRoomCatalog.VenueAddress;
        var venueRoom = StripProjectorAreaSuffix(booking.VenueRoom) ?? "Room TBD";

        // Build equipment rows categorized by groupFld, grouping packages with their components
        _logger.LogInformation("Categorizing {ItemCount} equipment items by group", items.Count);
        var visionRows = new List<EquipmentRow>();
        var audioRows = new List<EquipmentRow>();
        var lightingRows = new List<EquipmentRow>();
        var recordingRows = new List<EquipmentRow>();
        var drapeRows = new List<EquipmentRow>();
        var otherRows = new List<EquipmentRow>();

        decimal visionTotal = 0, audioTotal = 0, lightingTotal = 0, recordingTotal = 0, drapeTotal = 0;

        // Separate packages/standalone items from components
        // ItemType: 0=normal item, 1=package, 2=component
        var packages = items.Where(i => i.ItemType == 1).ToList();
        var standaloneItems = items.Where(i => i.ItemType == 0 && string.IsNullOrEmpty(i.ParentCode)).ToList();
        var components = items.Where(i => i.ItemType == 2 || !string.IsNullOrEmpty(i.ParentCode)).ToList();

        // Group components by their parent product code
        var componentsByParent = components
            .GroupBy(c => c.ParentCode?.Trim() ?? "")
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("Found {PackageCount} packages, {StandaloneCount} standalone items, {ComponentCount} components",
            packages.Count, standaloneItems.Count, components.Count);

        // Helper to add rows to appropriate category
        void AddToCategory(List<EquipmentRow> rows, decimal lineTotal, TblInvmas? invItem, string description)
        {
            var group = (invItem?.groupFld ?? "").ToUpper().Trim();
            
            switch (group)
            {
                case "VISION": case "VIDEO": case "PROJECTOR": case "COMPUTER":
                    visionRows.AddRange(rows);
                    visionTotal += lineTotal;
                    break;
                case "AUDIO": case "SOUND": case "PA":
                    audioRows.AddRange(rows);
                    audioTotal += lineTotal;
                    break;
                case "LIGHTING": case "LIGHT": case "LX":
                    lightingRows.AddRange(rows);
                    lightingTotal += lineTotal;
                    break;
                case "RECORDING": case "RECORD": case "CAPTURE":
                    recordingRows.AddRange(rows);
                    recordingTotal += lineTotal;
                    break;
                case "DRAPE": case "STAGING": case "SCENIC": case "RIGGING":
                    drapeRows.AddRange(rows);
                    drapeTotal += lineTotal;
                    break;
                default:
                    // Secondary classification based on description
                    var desc = description.ToLower();
                    if (desc.Contains("projector") || desc.Contains("screen") || desc.Contains("monitor") || 
                        desc.Contains("laptop") || desc.Contains("display") || desc.Contains("vision") ||
                        desc.Contains("macbook") || desc.Contains("computer"))
                    {
                        visionRows.AddRange(rows);
                        visionTotal += lineTotal;
                    }
                    else if (desc.Contains("mic") || desc.Contains("speaker") || desc.Contains("audio") || 
                             desc.Contains("sound") || desc.Contains("mixer") || desc.Contains("amplifier"))
                    {
                        audioRows.AddRange(rows);
                        audioTotal += lineTotal;
                    }
                    else if (desc.Contains("light") || desc.Contains("led") || desc.Contains("spot") || desc.Contains("wash"))
                    {
                        lightingRows.AddRange(rows);
                        lightingTotal += lineTotal;
                    }
                    else if (desc.Contains("record") || desc.Contains("capture") || desc.Contains("stream"))
                    {
                        recordingRows.AddRange(rows);
                        recordingTotal += lineTotal;
                    }
                    else if (desc.Contains("drape") || desc.Contains("stage") || desc.Contains("riser") || desc.Contains("backdrop"))
                    {
                        drapeRows.AddRange(rows);
                        drapeTotal += lineTotal;
                    }
                    else
                    {
                        // Default to vision
                        visionRows.AddRange(rows);
                        visionTotal += lineTotal;
                    }
                    break;
            }
        }

        // Process packages first - show package with price, then components without price
        foreach (var package in packages)
        {
            inventoryLookup.TryGetValue(package.ProductCodeV42 ?? "", out var invItem);
            rateLookup.TryGetValue(package.ProductCodeV42 ?? "", out var rateItem);
            
            var qty = package.TransQty ?? 1;
            var unitPrice = GetUnitPrice(package, invItem, rateItem);
            var lineTotal = qty * unitPrice;
            var description = GetBestDescription(package, invItem);

            var rowsToAdd = new List<EquipmentRow>();

            // Package row with price
            rowsToAdd.Add(new EquipmentRow(
                Description: description,
                Qty: qty > 0 ? qty.ToString("0") : "1",
                LineTotal: lineTotal.ToString("C"),
                IsGroup: false,
                IsComponent: false
            ));

            // Add components for this package (indented, no price)
            if (componentsByParent.TryGetValue(package.ProductCodeV42?.Trim() ?? "", out var comps))
            {
                foreach (var comp in comps)
                {
                    inventoryLookup.TryGetValue(comp.ProductCodeV42 ?? "", out var compInvItem);
                    var compDesc = GetBestDescription(comp, compInvItem);
                    var compQty = comp.TransQty ?? 1;
                    
                    rowsToAdd.Add(new EquipmentRow(
                        Description: $"  └ {compDesc}",  // Indented with visual indicator
                        Qty: compQty > 0 ? compQty.ToString("0") : "1",
                        LineTotal: null,  // No price for components (included in package)
                        IsGroup: false,
                        IsComponent: true
                    ));
                }
            }

            AddToCategory(rowsToAdd, lineTotal, invItem, description);
        }

        // Process standalone items (not packages, not components)
        foreach (var item in standaloneItems)
        {
            inventoryLookup.TryGetValue(item.ProductCodeV42 ?? "", out var invItem);
            rateLookup.TryGetValue(item.ProductCodeV42 ?? "", out var rateItem);

            var qty = item.TransQty ?? 1;
            var unitPrice = GetUnitPrice(item, invItem, rateItem);
            var lineTotal = qty * unitPrice;
            var description = GetBestDescription(item, invItem);

            var row = new EquipmentRow(
                Description: description,
                Qty: qty > 0 ? qty.ToString("0") : "1",
                LineTotal: lineTotal.ToString("C"),
                IsGroup: false,
                IsComponent: false
            );

            AddToCategory(new List<EquipmentRow> { row }, lineTotal, invItem, description);
        }

        _logger.LogInformation("Equipment categorization complete - Vision: {V}, Audio: {A}, Lighting: {L}, Recording: {R}, Drape: {D}",
            visionRows.Count, audioRows.Count, lightingRows.Count, recordingRows.Count, drapeRows.Count);

        // Build labor rows
        var labourRows = new List<LaborRow>();
        decimal labourTotal = 0;

        foreach (var crewMember in crew)
        {
            var hours = crewMember.Hours ?? 1;
            var minutes = crewMember.Minutes ?? 0;
            var totalHours = hours + (minutes / 60.0);
            var rate = (decimal)(crewMember.UnitRate ?? crewMember.Price ?? 0);
            var lineTotal = (decimal)totalHours * rate;
            labourTotal += lineTotal;

            var taskName = GetTaskName(crewMember.Task);
            var personName = crewMember.Person?.Trim() ?? "AV Technician";
            
            // Format times
            var startTime = crewMember.FirstDate?.ToString("dd/MM/yy HH:mm") ?? "";
            var endTime = crewMember.RetnDate?.ToString("dd/MM/yy HH:mm") ?? "";
            var hrsDisplay = $"{hours:00}:{minutes:00}";

            labourRows.Add(new LaborRow(
                Description: personName,
                Task: taskName,
                Qty: (crewMember.TransQty ?? 1).ToString(),
                Start: startTime,
                Finish: endTime,
                Hrs: hrsDisplay,
                Total: lineTotal.ToString("C")
            ));
        }

        // Calculate totals
        var equipmentSubtotal = visionTotal + audioTotal + lightingTotal + recordingTotal + drapeTotal;
        var rentalTotal = equipmentSubtotal;
        // Business rule: 10% service charge on total hire amount (rental equipment).
        var serviceCharge = rentalTotal * 0.10m;
        var subtotalExGst = rentalTotal + labourTotal + serviceCharge;
        var gstRate = 0.10m; // 10% GST
        var gst = subtotalExGst * gstRate;
        var grandTotal = subtotalExGst + gst;

        // Use booking prices if available (they may have been manually adjusted)
        if (booking.price_quoted.HasValue && booking.price_quoted.Value > 0)
        {
            grandTotal = (decimal)booking.price_quoted.Value;
            gst = grandTotal / 1.1m * 0.1m;
            subtotalExGst = grandTotal - gst;
        }

        return new QuoteAllFields(
            // Overview header
            Client: organization?.OrganisationV6 ?? booking.OrganizationV6 ?? "Client",
            ContactName: contact?.Contactname ?? booking.contact_nameV6 ?? "Contact",
            Email: contact?.Email ?? "contact@email.com",
            EventDate: eventDate,

            // Section headings
            EventTitle: booking.showName ?? booking.OrganizationV6 ?? "Event",

            // Location block - now with proper venue data
            Location: venueName,
            Address: venueAddress,
            Room: venueRoom,
            DateRange: dateRange,

            // Right column contact block
            DeliveryContact: contact?.Contactname ?? booking.contact_nameV6 ?? "Contact Name",
            AccountMgrName: booking.Salesperson ?? "Nishal Kumar",
            AccountMgrMobile: "+61 04 84814633",
            AccountMgrEmail: "nishal.kumar@microhire.com.au",

            // Footer ref
            Reference: booking.booking_no ?? "TEMP-001",

            // "Please confirm dates and times are accurate"
            SetupDate: setupDate,
            SetupTime: setupTime,
            RehearsalDate: rehearsalDate,
            RehearsalTime: rehearsalTime,
            EventStartDate: showStartDate,
            EventStartTime: eventStartTime,
            EventEndDate: showEndDate,
            EventEndTime: eventEndTime,

            // Page 2: header note for room
            RoomNoteHeader: venueRoom,
            RoomNoteStarts: $"Event Starts - {eventStartTime}",
            RoomNoteEnds: $"Event Ends - {eventEndTime}",
            RoomNoteTotal: equipmentSubtotal.ToString("C"),

            // Equipment rows - only include non-empty categories
            VisionRows: visionRows.Count > 0 ? visionRows : null,
            AudioRows: audioRows.Count > 0 ? audioRows : null,
            LightingRows: lightingRows.Count > 0 ? lightingRows : null,
            RecordingRows: recordingRows.Count > 0 ? recordingRows : null,
            DrapeRows: drapeRows.Count > 0 ? drapeRows : null,

            // Page 4: Totals with proper pricing
            VisionTotal: visionTotal.ToString("C"),
            AudioTotal: audioTotal.ToString("C"),
            LightingTotal: lightingTotal.ToString("C"),
            RecordingTotal: recordingTotal.ToString("C"),
            DrapeTotal: drapeTotal.ToString("C"),

            // Labor
            LabourRows: labourRows.Count > 0 ? labourRows : null,
            LabourTotal: labourTotal.ToString("C"),

            // Budget summary
            RentalTotal: rentalTotal.ToString("C"),
            ServiceCharge: serviceCharge.ToString("C"),
            SubTotalExGst: subtotalExGst.ToString("C"),
            Gst: gst.ToString("C"),
            GrandTotalIncGst: grandTotal.ToString("C"),

            // Page 4: Budget notes
            BudgetNotesTopLine: "The team at Microhire look forward to working with you to make every aspect of your event a success.",
            BudgetValidityLine: $"To ensure that your event receives the best possible equipment and technical personnel, please confirm that all details are correct including dates, timing and quantities. Note that our pricing is valid for 30 days and our resources are subject to availability at the time of booking.",
            BudgetConfirmLine: "Please confirm your acceptance of the proposal and its inclusions by returning a signed copy of the Confirmation of Services page, so we can proceed with your requirements.",
            BudgetContactLine: "However, if you wish to discuss any additions or updates regarding our proposal, please do not hesitate to contact me on the details below.",
            BudgetSignoffLine: "We look forward to working with you on a seamless and successful event.",

            // Page 4: signature block address lines
            FooterOfficeLine1: $"Microhire | {venueName}",
            FooterOfficeLine2: $"Microhire @ {venueAddress}",

            // Page 5: confirmation page text + terms URL
            ConfirmP1: $"On behalf of {organization?.OrganisationV6 ?? "the client"}, I accept this proposal and wish to proceed with the details that are confirmed to be correct.",
            ConfirmP2: "Upon request, any additions or amendments will be updated to this proposal accordingly.",
            ConfirmP3: "We understand that equipment and personnel are not allocated until this document is signed and returned.",
            ConfirmP4: "This proposal and billing details are subject to Microhire's terms and conditions.",
            ConfirmTermsUrl: "https://www.microhire.com.au/terms-conditions/"
        );
    }

    /// <summary>
    /// Gets the best available unit price from item, rate table, or inventory.
    /// UnitRate is the per-unit price in Rental Point; Price is UnitRate × Qty (total line price).
    /// UnitRate must be checked first to avoid double-counting when the caller multiplies by qty.
    /// </summary>
    private decimal GetUnitPrice(TblItemtran item, TblInvmas? invItem, TblRatetbl? rateItem)
    {
        if (item.UnitRate.HasValue && item.UnitRate.Value > 0)
            return (decimal)item.UnitRate.Value;
        // Rate table has the actual hire prices
        if (rateItem?.rate_1st_day.HasValue == true && rateItem.rate_1st_day.Value > 0)
            return (decimal)rateItem.rate_1st_day.Value;
        if (invItem?.retail_price.HasValue == true && invItem.retail_price.Value > 0)
            return (decimal)invItem.retail_price.Value;
        // Price is the total line price; divide by qty as a last resort to recover the unit price.
        if (item.Price.HasValue && item.Price.Value > 0)
            return (decimal)(item.Price.Value / Math.Max((double)(item.TransQty ?? 1), 1));
        return 0;
    }

    /// <summary>
    /// Gets the best available description from item or inventory
    /// </summary>
    private string GetBestDescription(TblItemtran item, TblInvmas? invItem)
    {
        // Priority: Comment description > Printed description > Description > Product code
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

    /// <summary>
    /// Converts task code to human-readable name
    /// </summary>
    private string GetTaskName(byte? taskCode)
    {
        return taskCode switch
        {
            1 => "Setup",
            2 => "Test & Connect",
            3 => "Operation",
            4 => "Pack Down",
            5 => "Standby",
            6 => "Rehearsal",
            7 => "Bump In",
            8 => "Bump Out",
            _ => "Technical Services"
        };
    }

    /// <summary>
    /// Check if a quote PDF already exists for the given booking
    /// </summary>
    public (bool exists, string? url) CheckExistingQuote(string bookingNo)
    {
        try
        {
            var quotesDir = QuoteFilesPaths.GetPhysicalQuotesDirectory(_env);

            if (!Directory.Exists(quotesDir))
                return (false, null);

            // Look for PDF files that contain the booking number
            var pdfFiles = Directory.GetFiles(quotesDir, $"Quote-*{bookingNo}*.pdf")
                                   .OrderByDescending(f => File.GetCreationTimeUtc(f))
                                   .ToArray();

            if (pdfFiles.Length > 0)
            {
                var fileName = Path.GetFileName(pdfFiles[0]);
                var url = QuoteFilesPaths.PublicUrlForFileName(fileName);
                return (true, url);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing quote for booking {BookingNo}", bookingNo);
            return (false, null);
        }
    }

    private string FormatTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return "";

        // Time is stored as HHmm (e.g., "0900")
        if (time.Length == 4 && int.TryParse(time, out _))
        {
            return $"{time.Substring(0, 2)}:{time.Substring(2, 2)}";
        }

        return time;
    }

    private static string? StripProjectorAreaSuffix(string? venueRoom)
    {
        if (string.IsNullOrWhiteSpace(venueRoom)) return venueRoom;
        var s = venueRoom.Trim();
        s = Regex.Replace(s, @"\s*-\s*Projector\s+Area(?:s)?\s*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*-\s*Projector\s+Area(?:s)?\s+[A-F](?:/[A-F])*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*\(Proj(?:ector)?\s+[A-F](?:/[A-F])*\)$", "", RegexOptions.IgnoreCase);
        return s.Trim();
    }
}
