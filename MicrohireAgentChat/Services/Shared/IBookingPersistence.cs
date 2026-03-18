using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;

namespace MicrohireAgentChat.Services.Shared;

/// <summary>
/// Interface for persisting booking-related data to the database
/// </summary>
public interface IBookingPersistence
{
    /// <summary>
    /// Upserts a contact by email (primary key). Creates if not exists, updates if exists.
    /// Maps to tblContact table with proper column constraints.
    /// </summary>
    /// <param name="name">Full name (split into firstname/midname/surname internally)</param>
    /// <param name="email">Email address (max 80 chars) - used for lookup</param>
    /// <param name="phoneE164">Phone in E164 format (max 16 chars) - mapped to Cell column</param>
    /// <param name="position">Job title/position (max 50 chars)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Contact ID (decimal from tblContact.ID column)</returns>
    Task<decimal?> UpsertContactAsync(
        string? name,
        string? email,
        string? phoneE164,
        string? position,
        CancellationToken ct);

    /// <summary>
    /// Upserts an organization/customer record.
    /// Maps to tblcust table with Customer_code (varchar 30), OrganisationV6 (varchar 50), Address_l1V6 (char 50).
    /// Uses transaction-safe ID-based code generation (C#####).
    /// </summary>
    /// <param name="organisationName">Organization name (max 50 chars) - mapped to OrganisationV6</param>
    /// <param name="address">Physical address (max 50 chars) - mapped to Address_l1V6</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Organization ID (decimal from tblcust.ID column)</returns>
    Task<decimal?> UpsertOrganisationAsync(
        string organisationName,
        string? address,
        CancellationToken ct);

    /// <summary>
    /// Finds an existing organization by name (case-insensitive).
    /// Returns ID, Customer_code, and OrganisationV6 if found.
    /// </summary>
    Task<(decimal Id, string Code, string Name)?> FindOrganisationAsync(
        string organisationName,
        CancellationToken ct);

    /// <summary>
    /// Links a contact to an organization via tblLinkCustContact.
    /// Uses Customer_Code (varchar 30) and ContactID (decimal).
    /// </summary>
    Task LinkContactToOrganisationAsync(
        string customerCode,
        decimal contactId,
        CancellationToken ct);

    /// <summary>
    /// Saves or updates a booking record in tblbookings.
    /// Handles complex column mapping with proper data types and constraints.
    /// </summary>
    /// <param name="booking">Booking data model</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Booking number (varchar 35 from tblbookings.booking_no)</returns>
    Task<string> SaveBookingAsync(
        BookingData booking,
        CancellationToken ct);

    /// <summary>
    /// Generates next sequential booking number for a customer code.
    /// Reads from tblbookings.booking_no (varchar 35).
    /// </summary>
    Task<string> GenerateNextBookingNoAsync(string customerCode, CancellationToken ct);
}

/// <summary>
/// Data model for booking persistence
/// Maps to key columns in tblbookings table
/// </summary>
public sealed class BookingData
{
    // Identity
    public string? BookingNo { get; set; }  // booking_no varchar(35)
    public int? CustID { get; set; }        // CustID decimal(10,0) NOT NULL
    public int? VenueID { get; set; }       // VenueID int NOT NULL
    public string? CustCode { get; set; }   // CustCode varchar(30)
    
    // Contact
    public int? ContactID { get; set; }     // ContactID decimal(9,0)
    public string? ContactName { get; set; } // contact_nameV6 varchar(35)
    public string? OrganizationV6 { get; set; } // OrganizationV6 varchar(50)
    
    // Venue/Event
    public string? VenueRoom { get; set; }  // VenueRoom varchar(35)
    public string? ShowName { get; set; }   // showName varchar(50)
    public string? EventType { get; set; }  // EventType varchar(20)
    
    // Dates (all datetime nullable)
    public DateTime? SDate { get; set; }      // SDate - Show start date
    public DateTime? ShowSDate { get; set; }  // ShowSdate
    public DateTime? ShowEDate { get; set; }  // ShowEdate
    public DateTime? SetDate { get; set; }    // SetDate - Setup date
    public DateTime? RehDate { get; set; }    // RehDate - Rehearsal date
    public DateTime? RDate { get; set; }      // rDate - Return/finish date
    public DateTime? DDate { get; set; }      // dDate - Delivery date
    public DateTime? OrderDate { get; set; }  // order_date
    public DateTime? EntryDate { get; set; }  // EntryDate
    
    // Times (all varchar 4 - HHmm format)
    public string? ShowStartTime { get; set; }  // showStartTime varchar(4)
    public string? ShowEndTime { get; set; }    // ShowEndTime varchar(4)
    public string? SetupTime { get; set; }      // setupTimeV61 varchar(4)
    public string? RehearsalTime { get; set; }  // RehearsalTime varchar(4)
    public string? StrikeTime { get; set; }     // StrikeTime varchar(4) - pack up
    
    // Time components (tinyint)
    public byte? DelTimeH { get; set; }         // del_time_h tinyint
    public byte? DelTimeM { get; set; }         // del_time_m tinyint
    public byte? RetTimeH { get; set; }         // ret_time_h tinyint
    public byte? RetTimeM { get; set; }         // ret_time_m tinyint
    
    // Financial (all float 53 nullable)
    public double? PriceQuoted { get; set; }    // price_quoted
    public double? HirePrice { get; set; }      // hire_price
    public double? Labour { get; set; }         // labour
    public double? SundryTotal { get; set; }    // sundry_total (service charge)
    
    // Status/Type
    public byte? BookingType { get; set; }           // booking_type_v32 tinyint
    public byte? Status { get; set; }                // status tinyint
    public byte? BookingProgressStatus { get; set; } // BookingProgressStatus tinyint
    
    // Locations (all int nullable)
    public int? FromLocn { get; set; }          // From_locn
    public int? TransToLocn { get; set; }       // Trans_to_locn
    public int? ReturnToLocn { get; set; }      // return_to_locn
    
    // Misc
    public string? Salesperson { get; set; }    // Salesperson varchar(30)
    public int? ExpAttendees { get; set; }      // expAttendees int
}

