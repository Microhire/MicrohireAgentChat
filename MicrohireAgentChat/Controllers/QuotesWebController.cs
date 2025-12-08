using System.Text.RegularExpressions;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace MicrohireAgentChat.Controllers;

public sealed class QuotesWebController : Controller
{
    private readonly AzureAgentChatService _chat;
    private readonly BookingDbContext _bookingDb;
    private readonly ILogger<QuotesWebController> _logger;

    public QuotesWebController(AzureAgentChatService chat, BookingDbContext bookingDb, ILogger<QuotesWebController> logger)
    {
        _chat = chat;
        _bookingDb = bookingDb;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Page1(int id, string type)
    {
        //Cover
        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 6, true);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }
    

    [HttpGet]
    public async Task<IActionResult> Page2(int id, string type)
    {
        //OVERVIEW
        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 6, true);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page3(int id, string type)
    {
        // EQUIPMENT Place + Vision
        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 6, true);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }



    [HttpGet]
    public async Task<IActionResult> Page4(int id, string type)
    {
        // EQUIPMENT DATA
        try
        {
            var quote = await ViewbagQuoteReport(id, type, 6, null, true);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page5(int id, string type)
    {
        // EQUIPMENT AUDIO
        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 0, false);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page6(int id, string type)
    {
        // EQUIPMENT LIGHTING
        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 0, false);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page7(int id, string type)
    {
        // EQUIPMENT RECORDING
        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 0, false);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page8(int id, string type)
    {
        // EQUIPMENT DRAPE		

        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 0, false);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page9(int id, string type)
    {
        // TECHNICAL SERVICES		

        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 0, false);
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page10(int id, string type)
    {
        // BUDGET SUMMARY		

        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 0, false);

            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Quote is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    public async Task<QuoteAllFields?> ViewbagQuoteReport(int id, string type, int? skip, int? take, bool loadImage = false)
    {
        try
        {
            var quote = await GetCompileDataAsync(id);
            if (quote is not null)
            {
                var skippedPages = new List<int>();

                if (quote.AudioRows is null || quote.AudioRows.Count == 0) skippedPages.Add(4);
                if (quote.LightingRows is null || quote.LightingRows.Count == 0) skippedPages.Add(5);
                if (quote.RecordingRows is null || quote.RecordingRows.Count == 0) skippedPages.Add(6);
                if (quote.DrapeRows is null || quote.DrapeRows.Count == 0) skippedPages.Add(7);
                if (quote.LabourRows is null || quote.LabourRows.Count == 0) skippedPages.Add(8);

                ViewBag.SkippedPages = skippedPages;
            }

            ViewBag.Id = quote?.Reference;
            ViewData["Name"] = quote?.EventTitle;
            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quote for booking ID {Id}", id);
            TempData["Error"] = "Quote is not exists";
            return null;
        }
    }

    /// <summary>
    /// Fetches real data from the database for the given booking ID
    /// </summary>
    public async Task<QuoteAllFields?> GetCompileDataAsync(int id)
    {
        try
        {
            // Load booking
            var booking = await _bookingDb.TblBookings
                .FirstOrDefaultAsync(b => b.ID == id);

            if (booking == null)
            {
                _logger.LogWarning("Booking with ID {Id} not found", id);
                return null;
            }

            // Load venue
            TblVenue? venue = null;
            if (booking.VenueID > 0)
            {
                venue = await _bookingDb.TblVenues
                    .FirstOrDefaultAsync(v => v.ID == booking.VenueID);
            }

            // Load contact
            TblContact? contact = null;
            if (booking.ContactID.HasValue)
            {
                contact = await _bookingDb.Contacts
                    .FirstOrDefaultAsync(c => c.Id == booking.ContactID.Value);
            }

            // Load organization
            TblCust? organization = null;
            if (booking.CustID.HasValue)
            {
                organization = await _bookingDb.TblCusts
                    .FirstOrDefaultAsync(c => c.ID == booking.CustID.Value);
            }

            // Load equipment items
            var items = await _bookingDb.TblItemtrans
                .Where(i => i.BookingNoV32 == booking.booking_no || i.BookingId == booking.ID)
                .ToListAsync();

            // Load inventory master data for descriptions and prices
            var productCodes = items.Select(i => i.ProductCodeV42).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
            var inventoryItems = await _bookingDb.TblInvmas
                .Where(inv => productCodes.Contains(inv.product_code))
                .ToDictionaryAsync(inv => inv.product_code ?? "");

            // Load crew/labor
            var crew = await _bookingDb.TblCrews
                .Where(c => c.BookingNoV32 == booking.booking_no)
                .ToListAsync();

            // Build the quote fields
            return BuildQuoteFields(booking, venue, contact, organization, items, inventoryItems, crew);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compiling data for booking ID {Id}", id);
            return null;
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
        List<TblCrew> crew)
    {
        // Format dates
        var eventDate = booking.dDate?.ToString("dddd d MMMM yyyy") ?? booking.ShowSDate?.ToString("dddd d MMMM yyyy") ?? "";
        var setupDate = booking.SetDate?.ToString("dddd d MMMM yyyy") ?? eventDate;
        var rehearsalDate = booking.RehDate?.ToString("dddd d MMMM yyyy") ?? eventDate;
        var showStartDate = booking.ShowSDate?.ToString("dddd d MMMM yyyy") ?? eventDate;
        var showEndDate = booking.ShowEdate?.ToString("dddd d MMMM yyyy") ?? eventDate;

        // Format times
        var setupTime = FormatTime(booking.setupTimeV61);
        var rehearsalTime = FormatTime(booking.RehearsalTime);
        var eventStartTime = FormatTime(booking.showStartTime);
        var eventEndTime = FormatTime(booking.ShowEndTime);

        // Build venue information
        var venueName = venue?.VenueName ?? booking.VenueRoom ?? "Venue TBD";
        var venueAddress = venue?.FullAddress ?? organization?.Address_l1V6 ?? "Address TBD";
        var venueRoom = booking.VenueRoom ?? "Room TBD";

        // Build equipment rows categorized by groupFld
        var visionRows = new List<EquipmentRow>();
        var audioRows = new List<EquipmentRow>();
        var lightingRows = new List<EquipmentRow>();
        var recordingRows = new List<EquipmentRow>();
        var drapeRows = new List<EquipmentRow>();

        decimal visionTotal = 0, audioTotal = 0, lightingTotal = 0, recordingTotal = 0, drapeTotal = 0;

        foreach (var item in items)
        {
            // Get inventory info for better description and pricing
            TblInvmas? invItem = null;
            if (!string.IsNullOrEmpty(item.ProductCodeV42))
            {
                inventoryLookup.TryGetValue(item.ProductCodeV42, out invItem);
            }

            // Calculate line total
            var qty = item.TransQty ?? 1;
            var unitPrice = GetUnitPrice(item, invItem);
            var lineTotal = qty * unitPrice;

            // Get description
            var description = GetBestDescription(item, invItem);

            var row = new EquipmentRow(
                Description: description,
                Qty: qty > 0 ? qty.ToString("0") : "1",
                LineTotal: lineTotal.ToString("C"),
                IsGroup: false
            );

            // Categorize by groupFld from inventory
            var group = (invItem?.groupFld ?? "").ToUpper().Trim();
            
            switch (group)
            {
                case "VISION":
                case "VIDEO":
                case "PROJECTOR":
                case "COMPUTER":
                    visionRows.Add(row);
                    visionTotal += lineTotal;
                    break;
                case "AUDIO":
                case "SOUND":
                case "PA":
                    audioRows.Add(row);
                    audioTotal += lineTotal;
                    break;
                case "LIGHTING":
                case "LIGHT":
                case "LX":
                    lightingRows.Add(row);
                    lightingTotal += lineTotal;
                    break;
                case "RECORDING":
                case "RECORD":
                case "CAPTURE":
                    recordingRows.Add(row);
                    recordingTotal += lineTotal;
                    break;
                case "DRAPE":
                case "STAGING":
                case "SCENIC":
                case "RIGGING":
                    drapeRows.Add(row);
                    drapeTotal += lineTotal;
                    break;
                default:
                    // Use secondary classification based on description
                    var desc = description.ToLower();
                    if (desc.Contains("projector") || desc.Contains("screen") || desc.Contains("monitor") || 
                        desc.Contains("laptop") || desc.Contains("display") || desc.Contains("vision"))
                    {
                        visionRows.Add(row);
                        visionTotal += lineTotal;
                    }
                    else if (desc.Contains("mic") || desc.Contains("speaker") || desc.Contains("audio") || 
                             desc.Contains("sound") || desc.Contains("mixer") || desc.Contains("amplifier"))
                    {
                        audioRows.Add(row);
                        audioTotal += lineTotal;
                    }
                    else if (desc.Contains("light") || desc.Contains("led") || desc.Contains("spot") || desc.Contains("wash"))
                    {
                        lightingRows.Add(row);
                        lightingTotal += lineTotal;
                    }
                    else if (desc.Contains("record") || desc.Contains("capture") || desc.Contains("stream"))
                    {
                        recordingRows.Add(row);
                        recordingTotal += lineTotal;
                    }
                    else if (desc.Contains("drape") || desc.Contains("stage") || desc.Contains("riser") || desc.Contains("backdrop"))
                    {
                        drapeRows.Add(row);
                        drapeTotal += lineTotal;
                    }
                    else
                    {
                        // Default to vision for uncategorized items
                        visionRows.Add(row);
                        visionTotal += lineTotal;
                    }
                    break;
            }
        }

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
        var serviceChargeRate = 0.10m;
        var serviceCharge = rentalTotal * serviceChargeRate;
        var subtotalExGst = rentalTotal + labourTotal + serviceCharge;
        var gstRate = 0.10m;
        var gst = subtotalExGst * gstRate;
        var grandTotal = subtotalExGst + gst;

        // Use booking prices if available
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

            // Location block with proper venue data
            Location: venueName,
            Address: venueAddress,
            Room: venueRoom,
            DateRange: $"{showStartDate} to {showEndDate}",

            // Contact block
            DeliveryContact: contact?.Contactname ?? booking.contact_nameV6 ?? "Contact Name",
            AccountMgrName: booking.Salesperson ?? "Nishal Kumar",
            AccountMgrMobile: "+61 04 84814633",
            AccountMgrEmail: "nishal.kumar@microhire.com.au",

            // Footer ref
            Reference: booking.booking_no ?? $"BOOKING-{booking.ID}",

            // Schedule confirmation
            SetupDate: setupDate,
            SetupTime: setupTime,
            RehearsalDate: rehearsalDate,
            RehearsalTime: rehearsalTime,
            EventStartDate: showStartDate,
            EventStartTime: eventStartTime,
            EventEndDate: showEndDate,
            EventEndTime: eventEndTime,

            // Room header
            RoomNoteHeader: venueRoom,
            RoomNoteStarts: $"Event Starts - {eventStartTime}",
            RoomNoteEnds: $"Event Ends - {eventEndTime}",
            RoomNoteTotal: equipmentSubtotal.ToString("C"),

            // Equipment rows
            VisionRows: visionRows.Count > 0 ? visionRows : new List<EquipmentRow>(),
            AudioRows: audioRows.Count > 0 ? audioRows : null,
            LightingRows: lightingRows.Count > 0 ? lightingRows : null,
            RecordingRows: recordingRows.Count > 0 ? recordingRows : null,
            DrapeRows: drapeRows.Count > 0 ? drapeRows : null,

            // Totals
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

            // Budget notes
            BudgetNotesTopLine: "The team at Microhire look forward to working with you to make every aspect of your event a success.",
            BudgetValidityLine: "To ensure that your event receives the best possible equipment and technical personnel, please confirm that all details are correct including dates, timing and quantities. Note that our pricing is valid for 30 days and our resources are subject to availability at the time of booking.",
            BudgetConfirmLine: "Please confirm your acceptance of the proposal and its inclusions by returning a signed copy of the Confirmation of Services page, so we can proceed with your requirements.",
            BudgetContactLine: "However, if you wish to discuss any additions or updates regarding our proposal, please do not hesitate to contact me on the details below.",
            BudgetSignoffLine: "We look forward to working with you on a seamless and successful event.",

            // Footer
            FooterOfficeLine1: $"Microhire | {venueName}",
            FooterOfficeLine2: $"Microhire @ {venueAddress}",

            // Confirmation page
            ConfirmP1: $"On behalf of {organization?.OrganisationV6 ?? "the client"}, I accept this proposal and wish to proceed with the details that are confirmed to be correct.",
            ConfirmP2: "Upon request, any additions or amendments will be updated to this proposal accordingly.",
            ConfirmP3: "We understand that equipment and personnel are not allocated until this document is signed and returned.",
            ConfirmP4: "This proposal and billing details are subject to Microhire's terms and conditions.",
            ConfirmTermsUrl: "https://www.microhire.com.au/terms-conditions/"
        );
    }

    private decimal GetUnitPrice(TblItemtran item, TblInvmas? invItem)
    {
        if (item.Price.HasValue && item.Price.Value > 0)
            return (decimal)item.Price.Value;
        if (item.UnitRate.HasValue && item.UnitRate.Value > 0)
            return (decimal)item.UnitRate.Value;
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

    private string FormatTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return "";
        if (time.Length == 4 && int.TryParse(time, out _))
        {
            return $"{time.Substring(0, 2)}:{time.Substring(2, 2)}";
        }
        return time;
    }
}
