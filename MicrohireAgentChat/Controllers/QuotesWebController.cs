using System.Text.RegularExpressions;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace MicrohireAgentChat.Controllers;

public sealed class QuotesWebController : Controller
{
    private readonly AzureAgentChatService _chat;
    private readonly BookingDbContext _bookingDb;

    // Detect: "Choose time: 09:00–10:30" (supports hyphen or en dash)
    private static readonly Regex ChooseTimeRe =
        new(@"^Choose\s*time:\s*(\d{1,2}:\d{2})\s*[–-]\s*(\d{1,2}:\d{2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Detect: "Choose schedule: key=HH:mm; key=HH:mm; ..."
    private static readonly Regex ChooseScheduleRe =
        new(@"^\s*Choose\s+schedule\s*:\s*(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Exact scripted greeting per your “Final Script for AI”
    private const string GreetingText =
        "Hello, my name is Isla from Microhire. What is your full name?";

    public QuotesWebController(AzureAgentChatService chat, BookingDbContext bookingDb)
    {
        _chat = chat;
        _bookingDb = bookingDb;
    }

    [HttpGet]
    // public async Task<IActionResult> Index()
    // {
    //     try
    //     {
    //         return View(quote);
    //     }
    //     catch (Exception ex)
    //     {
    //         ModelState.AddModelError(string.Empty, ex.Message);
    //         return View(quote);
    //     }
    // }

    
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
            TempData["Error"] = "Strategy is not exists";
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
            TempData["Error"] = "Strategy is not exists";
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
            TempData["Error"] = "Strategy is not exists";
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
            TempData["Error"] = "Strategy is not exists";
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

            ViewBag.Id = id;
            ViewBag.Type = type ?? "Tax";
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
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

            ViewBag.Id = id;
            ViewBag.Type = type ?? "Tax";
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
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

            ViewBag.Id = id;
            ViewBag.Type = type ?? "Tax";
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
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

            ViewBag.Id = id;
            ViewBag.Type = type ?? "Tax";
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
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

            ViewBag.Id = id;
            ViewBag.Type = type ?? "Tax";
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
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

            ViewBag.Id = id;
            ViewBag.Type = type ?? "Tax";
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Page11(int id, string type)
    {
        // Contract		

        try
        {
            var quote = await ViewbagQuoteReport(id, type, null, 0, false);

            ViewBag.Id = id;
            ViewBag.Type = type ?? "Tax";
            return View(quote);
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
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
                // Page1  - Cover
                // Page2  - OVERVIEW
                // Page3  -  EQUIPMENT Place + Vision
                // Page4  -  EQUIPMENT DATA
                // Page5  -  EQUIPMENT AUDIO
                // Page6  -  EQUIPMENT LIGHTING
                // Page7  -  EQUIPMENT RECORDING
                // Page8  -  EQUIPMENT DRAPE		
                // Page9  -  TECHNICAL SERVICES		
                // Page10  -  BUDGET SUMMARY		
                // Page11  -  Contract		
                
                var skippedPages = new List<int>();

                if (quote.VisionRows is null) skippedPages.Add(3);
                if (quote.AudioRows is null) skippedPages.Add(4);
                if (quote.LabourRows is null) skippedPages.Add(5);
                if (quote.LightingRows is null) skippedPages.Add(6);
                if (quote.RecordingRows is null) skippedPages.Add(7);
                if (quote.DrapeRows is null) skippedPages.Add(8);

                ViewBag.SkippedPages = skippedPages;
            }

            // ViewBag.YourStrategy = yourStrategy?.Sections;
            // ViewBag.ContactId = yourStrategy?.ContactId;
            ViewBag.Id = id;
            // ViewBag.Type = type;
            ViewData["Name"] = quote.EventTitle;
            return quote;
        }
        catch
        {
            TempData["Error"] = "Strategy is not exists";
            return null;
        }
    }
    public async Task<QuoteAllFields?> GetCompileDataAsync(int id)
    {
        // var quote = await GetAsync(id);
        // var yourStrategySegment = await GetSectionsAsync(yourStrategy?.id ?? id, 1, 100);
        // if (yourStrategy is not null && yourStrategySegment?.Items is not null)
        // {
        //     yourStrategy.Sections = yourStrategySegment.Items.Where(x => x != null).ToList()!;
        // }


        var fields = new QuoteAllFields(
            // Overview header
            Client: "ARCSOPT",
            ContactName: "Megan Suurenbroek",
            Email: "admin@arcsopt.org",
            EventDate:"",
            // Section headings
            EventTitle: "ARCSOPT MEETING",

            // Location block (left column)
            Location: "The Westin Brisbane",
            Address: "111 Mary Street  Brisbane City QLD 4000",
            Room: "Westin Ballroom I",
            DateRange: "Friday 17 October 2025 to Friday 17 October 2025",

            // Right column contact block
            DeliveryContact: "Tamara Lamb",
            AccountMgrName: "Nishal Kumar",
            AccountMgrMobile: "+61 04 84814633",
            AccountMgrEmail: "nishal.kumar@microhire.com.au",

            // Footer ref
            Reference: "C1374000002 - 001",

            // “Please confirm dates and times are accurate”
            SetupDate: "Friday 17 October 2025",
            SetupTime: "07:30",
            RehearsalDate: "Friday 17 October 2025",
            RehearsalTime: "07:30",
            EventStartDate: "Friday 17 October 2025",
            EventStartTime: "08:00",
            EventEndDate: "Friday 17 October 2025",
            EventEndTime: "17:00",

            // Page 2: header note for room
            RoomNoteHeader: "Westin Ballroom 1",
            RoomNoteStarts: "Event Starts - 0800",
            RoomNoteEnds: "Event Ends - 1700",
            RoomNoteTotal: "$0.00",

            // Vision rows (with group headings)
            VisionRows: new List<EquipmentRow> {
            new("Single Screen & FHD Projector Package", null, null, IsGroup: true),
            new("Includes:", null, null),
            new("Full HD Digital Projector", null, null),
            new("120' Motorised projection Screen - 16:9", null, null),
            new("HDMI Input Cable", null, null),
            new(">Client Supplied Laptop", null, null),
            new(">Laptop to be operated from lectern", null, null),
            new("Westin Ballroom Single Projector Package*", "1", ""),
            new("Wireless Presenter/Clicker", null, null, IsGroup: true),
            new("Logitech Wireless Presenter", "1", "")
            },
            VisionTotal: "$619.10",

            // Audio rows
            AudioRows: new List<EquipmentRow> {
            new("PA Speaker System", null, null, IsGroup: true),
            new("Westin Single Ballroom Ceiling Speaker System", "1", ""),
            new("Audio Control Desk", null, null, IsGroup: true),
            new("6 Channel Audio Mixer", "1", ""),
            new("Microphones", null, null, IsGroup: true),
            new("2 x Wireless Handheld Microphone", null, null),
            new("SINGLE Radio Mic Receiver - Shure QLXD4 K52", "2", "")
            },
            AudioTotal: "$584.42",

            // Page 3: Technical Services table
            LabourRows: new List<LaborRow> {
            new("AV Technician", "Setup",          "1", "17/10/25 06:00", "17/10/25 07:30", "01:30", "$165.00"),
            new("AV Technician", "Test & Connect", "1", "17/10/25 07:30", "17/10/25 08:30", "01:00", "$110.00"),
            new("AV Technician", "Pack Down",      "1", "17/10/25 17:00", "17/10/25 18:00", "01:00", "$110.00")
            },
            LabourTotal: "$385.00",

            // Page 4: Budget Summary numbers
            RentalTotal: "$1,203.52",
            ServiceCharge: "$120.35",
            SubTotalExGst: "$1,708.87",
            Gst: "$170.89",
            GrandTotalIncGst: "$1,879.76",

            // Page 4: body notes
            BudgetNotesTopLine: "The team at Microhire look forward to working with you to make every aspect of your event a success.",
            BudgetValidityLine: "To ensure that your event receives the best possible equipment and technical personnel, please confirm that all details are correct including dates, timing and quantities. Note that our pricing is valid until WED 7 MAY 2025 and our resources are subject to availability at the time of booking.",
            BudgetConfirmLine: "Please confirm your acceptance of the proposal and its inclusions by returning a signed copy of the Confirmation of Services page, so we can proceed with your requirements.",
            BudgetContactLine: "However, if you wish to discuss any additions or updates regarding our proposal, please do not hesitate to contact me on the details below.",
            BudgetSignoffLine: "We look forward to working with you on a seamless and successful event.",

            // Page 4: signature block address lines
            FooterOfficeLine1: "Microhire | Westin Brisbane",
            FooterOfficeLine2: "Microhire @ 111 Mary St, Brisbane City QLD 4000",

            // Page 5: confirmation page text + terms URL
            ConfirmP1: "On behalf of ARCSOPT, I accept this proposal and wish to proceed with the details that are confirmed to be correct.",
            ConfirmP2: "Upon request, any additions or amendments will be updated to this proposal accordingly.",
            ConfirmP3: "We understand that equipment and personnel are not allocated until this document is signed and returned.",
            ConfirmP4: "This proposal and billing details are subject to Microhire's terms and conditions.",
            ConfirmTermsUrl: "https://www.microhire.com.au/terms-conditions/",


            LightingRows: new List<EquipmentRow> {
            },
            LightingTotal: "$619.10",
            RecordingRows: new List<EquipmentRow> {
            },
            RecordingTotal: "$619.10",
            DrapeRows: new List<EquipmentRow> {
            },
            DrapeTotal: "$619.10"
        );
        return fields;
    }

}
