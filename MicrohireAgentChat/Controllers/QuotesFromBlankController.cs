using MicrohireAgentChat.Helpers;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class QuotesFromBlankController : Controller
{
    private readonly PdfFromBlankService _svc;

    public QuotesFromBlankController(PdfFromBlankService svc)
    {
        _svc = svc;
    }

    // POST /quotes/fill-from-blank => { url: "..." }
    [HttpPost("/quotes/fill-from-blank")]
    public IActionResult FillFromBlank([FromBody] QuoteAllFields body)
    {
        var (name, _) = _svc.Generate(body);
        var url = $"{Request.Scheme}://{Request.Host}/files/quotes/{Uri.EscapeDataString(name)}";
        return Ok(new { url });
    }

    // GET /quotes/fill-from-blank/static => quick test with static data
    [HttpGet("/quotes/fill-from-blank/static")]
    // Controllers/QuotesFromBlankController.cs
    public IActionResult FillFromBlankStatic()
    {
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
            ConfirmP4: "This proposal and billing details are subject to Microhire’s terms and conditions.",
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

        var (name, _) = _svc.Generate(fields);
        var url = $"{Request.Scheme}://{Request.Host}/files/quotes/{Uri.EscapeDataString(name)}";
        return Ok(new { url });
    }
}
