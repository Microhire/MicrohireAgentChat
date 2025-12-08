namespace MicrohireAgentChat.Services.Shared;

/// <summary>
/// Interface for persisting crew/labor records to database
/// Maps to tblcrew table
/// </summary>
public interface ICrewPersistence
{
    /// <summary>
    /// Upserts crew/labor rows for a booking.
    /// Maps to tblcrew with proper column constraints.
    /// Typical tasks: Setup (2), Packdown (4), Rehearsal (7), Tech support (3).
    /// </summary>
    /// <param name="bookingNo">Booking number (booking_no_v32 varchar 35)</param>
    /// <param name="crewRows">List of crew assignments</param>
    /// <param name="ct">Cancellation token</param>
    Task UpsertCrewRowsAsync(
        string bookingNo,
        IEnumerable<CrewData> crewRows,
        CancellationToken ct);
}

/// <summary>
/// Crew/labor data model
/// Maps to key columns in tblcrew table
/// </summary>
public sealed class CrewData
{
    // Required fields
    public string ProductCode { get; set; } = "AVTECH";      // product_code_v42 varchar(30) - typically AVTECH
    public byte? Task { get; set; }                           // task tinyint (2=Setup, 4=Packdown, 7=Rehearsal, 3=Tech, 8=Driver, 13=Project Manager)
    public string Person { get; set; } = string.Empty;       // person char(30) - can store count as string
    
    // Hours & pricing
    public byte Hours { get; set; }                           // hours tinyint
    public byte Minutes { get; set; }                         // Minutes tinyint
    public double UnitRate { get; set; }                      // unitRate float(53) - usually 110.0
    public double Price { get; set; }                         // price float(53)
    
    // Sequencing
    public int SeqNo { get; set; }                            // seq_no decimal(19,0)
    public int SubSeqNo { get; set; }                         // sub_seq_no int
    public byte HeadingNo { get; set; }                       // heading_no tinyint
    
    // Times (tinyint nullable) - copied from booking
    public byte? DelTimeHour { get; set; }                    // del_time_hour
    public byte? DelTimeMin { get; set; }                     // del_time_min
    public byte? ReturnTimeHour { get; set; }                 // return_time_hour
    public byte? ReturnTimeMin { get; set; }                  // return_time_min
    
    // Dates
    public DateTime FirstDate { get; set; }                   // FirstDate datetime
    public DateTime RetnDate { get; set; }                    // RetnDate datetime
    
    // Required flags with defaults
    public string TechrateIsHourOrDay { get; set; } = "H";   // techrateIsHourorDay char(1) - 'H' for hourly, 'D' for day
    public decimal HourlyRateID { get; set; }                 // HourlyRateID decimal(10,0) NOT NULL
    public int UnpaidHours { get; set; }                      // UnpaidHours int NOT NULL
    public int UnpaidMins { get; set; }                       // UnpaidMins int NOT NULL
    public bool MeetTechOnSite { get; set; }                  // MeetTechOnSite bit NOT NULL
    public bool TechIsConfirmed { get; set; }                 // TechIsConfirmed bit NOT NULL
    
    // Optional fields
    public int TransQty { get; set; } = 1;                    // trans_qty int
    public int GroupSeqNo { get; set; }                       // GroupSeqNo int
    public double DaysUsing { get; set; } = 1.0;              // days_using float(53)
    public double? StraightTime { get; set; }                 // StraightTime float(53) - 1.0 for straight time
    public double? OverTime { get; set; }                     // OverTime float(53)
    public double? DoubleTime { get; set; }                   // DoubleTime float(53)
    public double? TechRate { get; set; }                     // TechRate float(53)
    public double? TechPay { get; set; }                      // TechPay float(53)
}

