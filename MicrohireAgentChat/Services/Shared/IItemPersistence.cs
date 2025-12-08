namespace MicrohireAgentChat.Services.Shared;

/// <summary>
/// Interface for persisting equipment items and packages to database
/// Maps to tblitemtran table
/// </summary>
public interface IItemPersistence
{
    /// <summary>
    /// Upserts equipment items for a booking.
    /// Handles both simple items (item_type=0) and packages (item_type=1) with components (item_type=2).
    /// Maps to tblitemtran with proper column constraints.
    /// </summary>
    /// <param name="bookingNo">Booking number (booking_no_v32 varchar 35)</param>
    /// <param name="items">List of items to add/update</param>
    /// <param name="ct">Cancellation token</param>
    Task UpsertItemsAsync(
        string bookingNo,
        IEnumerable<ItemData> items,
        CancellationToken ct);

    /// <summary>
    /// Gets unit rate for a product code from tblRatetbls (TableNo=0)
    /// </summary>
    Task<double> GetUnitRateAsync(string productCode, CancellationToken ct);

    /// <summary>
    /// Gets package components from vwProdsComponents view
    /// Only returns fixed (non-variable) components
    /// </summary>
    Task<List<PackageComponent>> GetPackageComponentsAsync(string parentCode, CancellationToken ct);
}

/// <summary>
/// Equipment item data model
/// Maps to key columns in tblitemtran table
/// </summary>
public sealed class ItemData
{
    // Required fields
    public string ProductCode { get; set; } = string.Empty;  // product_code_v42 char(30)
    public decimal Quantity { get; set; }                     // trans_qty decimal(19,0)
    
    // Pricing
    public double UnitRate { get; set; }                      // unitRate float(53)
    public double Price { get; set; }                         // price float(53)
    
    // Item classification
    public byte ItemType { get; set; }                        // item_type tinyint (0=normal, 1=package, 2=component)
    public string? ParentCode { get; set; }                   // ParentCode varchar(30) - for components
    
    // Sequencing
    public int SeqNo { get; set; }                            // seq_no decimal(19,0)
    public int SubSeqNo { get; set; }                         // sub_seq_no int
    public byte HeadingNo { get; set; }                       // heading_no tinyint
    
    // Times (tinyint nullable)
    public byte? DelTimeHour { get; set; }                    // del_time_hour
    public byte? DelTimeMin { get; set; }                     // del_time_min
    public byte? ReturnTimeHour { get; set; }                 // return_time_hour
    public byte? ReturnTimeMin { get; set; }                  // return_time_min
    
    // Dates
    public DateTime FirstDate { get; set; }                   // FirstDate datetime
    public DateTime RetnDate { get; set; }                    // RetnDate datetime
    public DateTime BookDate { get; set; }                    // BookDate datetime
    public DateTime PDate { get; set; }                       // PDate datetime
    
    // Misc
    public int DaysUsing { get; set; }                        // days_using int
    public string? Comment { get; set; }                      // Comment_desc_v42 char(70)
    public decimal SubHireQty { get; set; }                   // sub_hire_qtyV61 decimal(19,0)
    public double UndiscAmt { get; set; }                     // Undisc_amt float(53)
    
    // Flags
    public bool AddedAtCheckout { get; set; }                 // AddedAtCheckout bit
    public bool AvailRecFlag { get; set; } = true;           // AvailRecFlag bit NOT NULL
    
    // Locations (int nullable)
    public int? FromLocn { get; set; }                        // From_locn
    public int? TransToLocn { get; set; }                     // Trans_to_locn
    public int? ReturnToLocn { get; set; }                    // return_to_locn
    
    // Other required fields with defaults
    public byte TransType { get; set; }                       // trans_type_v41 tinyint
    public byte AssignType { get; set; }                      // AssignType tinyint NOT NULL
    public int QtyShort { get; set; } = 1;                    // QtyShort int NOT NULL
    public int SubRentalLinkID { get; set; }                  // SubRentalLinkID int NOT NULL
}

/// <summary>
/// Package component from vwProdsComponents view
/// </summary>
public sealed class PackageComponent
{
    public string ProductCode { get; set; } = string.Empty;  // ProductCode from view
    public string ParentCode { get; set; } = string.Empty;   // ParentCode from view
    public decimal? Qty { get; set; }                         // Qty - quantity per package
    public byte? SubSeqNo { get; set; }                       // sub_seq_no for ordering
    public byte? VariablePart { get; set; }                   // 0=fixed, 1=variable
}

