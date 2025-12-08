using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    public sealed class TblBooking
    {
        // Keys (DB generates ID)
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal ID { get; set; }
        public string? booking_no { get; set; }

        // Basic
        public string? order_no { get; set; }
        public byte? status { get; set; }
        public bool? bBookingIsComplete { get; set; }

        // Venue / room
        public int VenueID { get; set; }          // used if you can map Venue Name → ID
        public string? VenueRoom { get; set; }     // "Room"

        // Core dates (CRITICAL per guide)
        public DateTime? dDate { get; set; }       // CRITICAL: "Delivery Date" (OUT date in UI)
        public DateTime? rDate { get; set; }       // CRITICAL: "Return Date" (IN date in UI)
        public DateTime? SDate { get; set; }       // "Show Start Date"
        public DateTime? order_date { get; set; }  // CRITICAL: Order date (shows as 1980 if not set)
        public DateTime? ShowSDate { get; set; }
        public DateTime? ShowEdate { get; set; }

        // NEW (limited fields): extra dates we need
        public DateTime? SetDate { get; set; }     // “Setup Date”
        public DateTime? RehDate { get; set; }     // “Rehearsal Date”

        // Times (stored as strings in DB)
        public string? showStartTime { get; set; } // “Show Start Time”  HHmm
        public string? ShowEndTime { get; set; }   // “Show End Time”    HHmm

        // Money / numbers (limited set we’re extracting)
        public double? price_quoted { get; set; }  // “Quote Total Amount inc GST”
        public double? hire_price { get; set; }    // “Equipment Cost”
        public double? labour { get; set; }        // “Labor Cost”
        public double? sundry_total { get; set; }  // “Service Charge” (added)
        public double? Tax2 { get; set; }
        // Booking meta (limited set)
        public byte booking_type_v32 { get; set; }     // “Booking Type” (tinyint in your schema)
        public byte? BookingProgressStatus { get; set; }// “Booking Status” (tinyint in your schema)

        // Text fields we need
        public string? contact_nameV6 { get; set; }     // “Contact Name”
        public string? showName { get; set; }           // “Show Name” (added)
        public string? OrganizationV6 { get; set; }     // “Organization”
        public string? Salesperson { get; set; }        // “Sales Person Code”
        public decimal? CustID { get; set; }             // “Customer ID” (added)
        public string? CustCode { get; set; }
        // Linking to saved contact
        public decimal? ContactID { get; set; }         // FK to tblContact (added)

        // ——— Existing properties you already had (left as-is) ———

        public byte? payment_type { get; set; }
        public double? deposit_quoted_v50 { get; set; }
        public byte? docs_produced { get; set; }
        public double? delivery { get; set; }
        public double? percent_disc { get; set; }
        public int? delivery_viav71 { get; set; }
        public string? delivery_time { get; set; }
        public int? pickup_viaV71 { get; set; }
        public string? pickup_time { get; set; }
        public string? invoiced { get; set; }
        public decimal? invoice_no { get; set; }
        public string? event_code { get; set; }
        public double? discount_rate { get; set; }
        public string? same_address { get; set; }
        public double? insurance_v5 { get; set; }
        public int? days_using { get; set; }
        public double? un_disc_amount { get; set; }
        public byte? del_time_h { get; set; }
        public byte? del_time_m { get; set; }
        public byte? ret_time_h { get; set; }
        public byte? ret_time_m { get; set; }
        public int? Item_cnt { get; set; }
        public double? sales_discount_rate { get; set; }
        public double? sales_amount { get; set; }
        public double? tax1 { get; set; }
        public byte? division { get; set; }
        public string? sales_tax_no { get; set; }
        public string? last_modified_by { get; set; }
        public string? delivery_address_exist { get; set; }
        public double? sales_percent_disc { get; set; }
        public byte? pricing_scheme_used { get; set; }
        public double? days_charged_v51 { get; set; }
        public double? sale_of_asset { get; set; }
        public int? From_locn { get; set; }
        public int? return_to_locn { get; set; }
        public double? retail_value { get; set; }
        public string? perm_casual { get; set; }
        public string? setupTimeV61 { get; set; }
        public string? RehearsalTime { get; set; }
        public string? StrikeTime { get; set; }
        public int? Trans_to_locn { get; set; }
        public decimal? transferNo { get; set; }
        public string? currencyStr { get; set; }
        public string? ConfirmedBy { get; set; }
        public string? ConfirmedDocRef { get; set; }
        public int? expAttendees { get; set; }
        public byte? HourBooked { get; set; }
        public byte? MinBooked { get; set; }
        public byte? SecBooked { get; set; }
        public int? TaxAuthority1 { get; set; }
        public int? TaxAuthority2 { get; set; }
        public string? EventType { get; set; } // you already had this; not in limited-save path
        public DateTime? EntryDate { get; set; }     // “Rehearsal Date”
    }
}
