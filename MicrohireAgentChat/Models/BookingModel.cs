namespace MicrohireAgentChat.Models
{
    namespace MicrohireAgentChat.Models
    {
        using System.Collections.Generic;

        public sealed record BookingPdfDto(
            string? ShowName,
            string? VenueName,
            string? VenueRoom,
            string? ContactNameV6,
            string? OrganizationV6,
            string? AddressL1V6,
            string? AddressL3V6,
            string? Phone1,
            string? Email,
            string? SalespersonName,
            string? CellCountryCodeAndCell,
            string? ShowDateLabel,   // e.g., "Friday 25 July 2025 to Friday 25 July 2025"
            string? SetupTime,       // "07:30"
            string? RehearsalTime,   // "07:30"
            string? ShowStartTime,   // "08:45"
            string? ShowEndTime,     // e.g., "17:30" (use StrikeTime if present)
                                     // Money (already formatted as strings)
            string? HirePrice,       // from b.hire_price
            string? Labour,          // from b.labour
            string? InsuranceV5,     // from b.insurance_v5
            string? Tax2,            // from b.Tax2
            string? PriceQuoted,     // from b.price_quoted
            List<BookingGroupDto> Items,
            List<BookingCrewDto> Crew
        );

        public sealed record BookingLineDto(
        string? CommentDescV42,
        decimal? TransQty,
        string? Price            // formatted per-line price (from TblItemtran.Price)
    );

        public sealed record BookingGroupDto(
            string GroupFld,                 // e.g. "AUDIO"
            List<BookingLineDto> Lines, string? TotalPrice
        );
        public sealed record BookingCrewDto(
            string? DescriptionV6,   // TblCrew.descriptionv6 (fallback to CommentDescV42 if needed)
            int? Task,            // TblCrew.task
            decimal? TransQty,       // TblCrew.trans_qty (or Itemtran.TransQty if you prefer)
            string? StartAt,         // "dd MMM yyyy HH:mm"
            string? EndAt,           // "dd MMM yyyy HH:mm"
            string? HoursMinutes,    // "H:MM"
            string? Price            // formatted "N2"
        );

        public sealed record BookingResponse(BookingPdfDto Pdf);

        public class BookingModel
        {
            public string? showName { get; set; } = "";
            public string? contact_nameV6 { get; set; } = "";
            public string? OrganizationV6 { get; set; } = "";
            public string? Address_l1V6 { get; set; } = "";
            public string? Address_l3V6 { get; set; } = "";
            public string? Phone1 { get; set; } = "";
            public string? Email { get; set; } = "";
            public string? VenueRoom { get; set; } = "";
            public string? ShowSDate { get; set; } = "";
            public string? ShowEDate { get; set; } = "";
            public string? VenueContact { get; set; } = "";
            public string? Salesperson_name { get; set; } = "";
            public string? CellCountryCode { get; set; } = "";
            public string? setupTimeV61 { get; set; } = "";
            public string? RehearsalTime { get; set; } = "";
            public string? showStartTime { get; set; } = "";
            public string? showEndTime { get; set; } = "";
            public string? Comment_desc_v42 { get; set; } = "";
            public string? Description { get; set; } = "";
            public string? Task { get; set; } = "";
            public string? Quantity { get; set; } = "";
            public string? StartDateTime { get; set; } = "";
            public string? EndDateTime { get; set; } = "";
            public string? Hours { get; set; } = "";
            public string? Total { get; set; } = "";
            public string? hire_price { get; set; } = "";
            public string? labour { get; set; } = "";
            public string? insurance_v5 { get; set; } = "";
            public string? Tax2 { get; set; } = "";
            public string? price_quoted { get; set; } = "";
        }
    }

}
