// Models/QuoteAllFields.cs
namespace MicrohireAgentChat.Models;

public sealed record QuoteAllFields(
    // Overview (letter header)
    string Client,                 // ARCSOPT
    string ContactName,            // Megan Suurenbroek
    string Email,                  // admin@arcsopt.org
    string EventDate,
    // Overview > header line under title (human-readable)
    string EventTitle,             // ARCSOPT MEETING (for section headings)

    // Location table
    string Location,               // The Westin Brisbane
    string Address,                // 111 Mary Street  Brisbane City QLD 4000
    string Room,                   // Westin Ballroom I
    string DateRange,              // Friday 17 October 2025 to Friday 17 October 2025

    // Contacts
    string DeliveryContact,        // Tamara Lamb
    string AccountMgrName,         // Nishal Kumar
    string AccountMgrMobile,       // +61 04 84814633
    string AccountMgrEmail,        // nishal.kumar@microhire.com.au

    // Footer reference
    string Reference,              // C1374000002 - 001

    // “Please confirm the following dates and times are accurate” block
    string SetupDate,              // Friday 17 October 2025
    string SetupTime,              // 07:30
    string RehearsalDate,          // Friday 17 October 2025
    string RehearsalTime,          // 07:30
    string EventStartDate,         // Friday 17 October 2025
    string EventStartTime,         // 08:00
    string EventEndDate,           // Friday 17 October 2025
    string EventEndTime,           // 17:00

    // Page 2: Equipment & Services header block
    string RoomNoteHeader,         // Westin Ballroom 1
    string RoomNoteStarts,         // Event Starts - 0800
    string RoomNoteEnds,           // Event Ends - 1700
    string RoomNoteTotal,          // $0.00

    // Vision section (support detailed rows + groups/subheads)
    List<EquipmentRow>? VisionRows, // include group titles as rows with null qty/price
    string VisionTotal,            // $619.10

    // Audio section
    List<EquipmentRow>? AudioRows,
    string AudioTotal,             // $584.42

    // Page 3: Technical Services
    List<LaborRow>? LabourRows,     // Description, Task, Qty, Start, Finish, Hrs, Total
    string LabourTotal,            // $385.00

    // ------- ADDED
    List<EquipmentRow>? LightingRows,
    string LightingTotal,
    List<EquipmentRow>? RecordingRows,
    string RecordingTotal,
    List<EquipmentRow>? DrapeRows,
    string DrapeTotal,
    // ------- END ADDED


    // Page 4: Budget Summary totals
    string RentalTotal,            // $1,203.52
    string ServiceCharge,          // $120.35
    string SubTotalExGst,          // $1,708.87
    string Gst,                    // $170.89
    string GrandTotalIncGst,       // $1,879.76

    // Page 4: body notes (bold validity date text is fine inline)
    string BudgetNotesTopLine,     // “The team at Microhire look forward…”
    string BudgetValidityLine,     // “valid until WED 7 MAY 2025…”
    string BudgetConfirmLine,      // “Please confirm your acceptance…”
    string BudgetContactLine,      // “However, if you wish to discuss…”
    string BudgetSignoffLine,      // “We look forward to working with you…”

    // Page 4: Account manager signature block
    string FooterOfficeLine1,      // Microhire | Westin Brisbane
    string FooterOfficeLine2,      // Microhire @ 111 Mary St, Brisbane City QLD 4000

    // Page 5: Confirmation of Services
    string ConfirmP1,              // “On behalf of ARCSOPT, I accept…”
    string ConfirmP2,              // “Upon request…”
    string ConfirmP3,              // “We understand that equipment…”
    string ConfirmP4,              // “This proposal and billing details…”
    string ConfirmTermsUrl         // https://www.microhire.com.au/terms-conditions/
)
{
}

public sealed record EquipmentRow(
    string Description, 
    string? Qty, 
    string? LineTotal, 
    bool IsGroup = false,
    bool IsComponent = false  // True for package components (shown indented, no price)
);

public sealed record LaborRow(
    string Description, // AV Technician
    string Task,        // Setup / Test & Connect / Pack Down
    string Qty,         // "1"
    string Start,       // "17/10/25 06:00"
    string Finish,      // "17/10/25 07:30"
    string Hrs,         // "01:30"
    string Total        // "$165.00"
);
