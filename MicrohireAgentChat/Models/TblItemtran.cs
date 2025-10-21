namespace MicrohireAgentChat.Models
{
    public sealed class TblItemtran
    {
        // IDENTITY
        public decimal ID { get; set; } // decimal(10,0)

        // Natural/business columns
        public string BookingNoV32 { get; set; } = null!;              // varchar(35)
        public byte? HeadingNo { get; set; }                            // tinyint NULL
        public long? SeqNo { get; set; }                                // decimal(19,0) -> map via converter
        public int? SubSeqNo { get; set; }                              // int

        public byte? TransTypeV41 { get; set; }                         // tinyint
        public string? ProductCodeV42 { get; set; }                     // char(30)
        public byte? DelTimeHour { get; set; }                          // tinyint
        public byte? DelTimeMin { get; set; }                           // tinyint
        public short? ReturnTimeHour { get; set; }                      // tinyint in screenshot header (use short?)
        public short? ReturnTimeMin { get; set; }                       // tinyint in screenshot header (use short?)

        public decimal? TransQty { get; set; }                          // decimal(19,0)
        public double? Price { get; set; }                              // float
        public byte? ItemType { get; set; }                             // tinyint
        public int? DaysUsing { get; set; }                             // int
        public decimal? SubHireQtyV61 { get; set; }                     // decimal(19,0)

        public int? FromLocn { get; set; }                              // int
        public int? TransToLocn { get; set; }                           // int
        public int? ReturnToLocn { get; set; }                          // int
        public byte? BitFieldV41 { get; set; }                          // tinyint

        public byte? TimeBookedH { get; set; }                          // tinyint
        public byte? TimeBookedM { get; set; }                          // tinyint
        public byte? TimeBookedS { get; set; }                          // tinyint

        public decimal? QtyReturned { get; set; }                       // decimal(19,0)
        public decimal? QtyCheckedOut { get; set; }                     // decimal(19,0)

        public double? TechRateOrDaysCharged { get; set; }              // float
        public double? TechPay { get; set; }                            // float
        public double? UnitRate { get; set; }                           // float

        public bool? PrepOn { get; set; }                               // char(1) -> treat as bit/bool in EF
        public string? CommentDescV42 { get; set; }                     // char(70)
        public string? AssignTo { get; set; }                           // varchar(255)

        public DateTime? FirstDate { get; set; }                        // datetime
        public DateTime? RetnDate { get; set; }                         // datetime
        public DateTime? BookDate { get; set; }                         // datetime
        public DateTime? PDate { get; set; }                            // datetime

        public byte? PTimeH { get; set; }                               // tinyint
        public byte? PTimeM { get; set; }                               // tinyint

        public double? DayWeekRate { get; set; }                        // float
        public int? QtyReserved { get; set; }                           // int
        public bool? AddedAtCheckout { get; set; }                      // bit

        public int? GroupSeqNo { get; set; }                            // int
        public int? SubRentalLinkID { get; set; }                       // int
        public byte AssignType { get; set; }                            // tinyint NOT NULL
        public int QtyShort { get; set; }                               // int NOT NULL
        public int? QtyAvailable { get; set; }                          // int

        public short? PackageLevel { get; set; }                        // smallint
        public double? BeforeDiscountAmount { get; set; }               // float
        public double? QuickTurnAroundQty { get; set; }                 // float
        public bool? InRack { get; set; }                               // bit
        public double? CostPrice { get; set; }                          // float
        public bool? NodeCollapsed { get; set; }                        // bit
        public bool AvailRecFlag { get; set; }                          // bit NOT NULL

        public int? BookingId { get; set; }                             // int
        public double? UndiscAmt { get; set; }                          // float

        public bool? ViewLogi { get; set; }                             // bit
        public bool? ViewClient { get; set; }                           // bit
        public int? LogiHeadingNo { get; set; }                         // int
        public int? LogiGroupSeqNo { get; set; }                        // int
        public int? LogiSeqNo { get; set; }                             // int
        public int? LogiSubSeqNo { get; set; }                          // int

        public string? ParentCode { get; set; }                         // varchar(30)
    }
}
