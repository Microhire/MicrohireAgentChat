using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models;

[Table("TblCrew")]
public sealed class TblCrew
{
    [Column("ID")] public decimal ID { get; set; }                    // decimal(10,0) identity
    [Column("booking_no_v32")] public string BookingNoV32 { get; set; } = null!; // varchar(35)

    [Column("heading_no")] public byte? HeadingNo { get; set; }              // tinyint NULL
    [Column("seq_no")] public decimal? SeqNo { get; set; }               // decimal(19,0) NULL
    [Column("sub_seq_no")] public int? SubSeqNo { get; set; }                // int NULL

    [Column("product_code_v42")] public string? ProductCodeV42 { get; set; }       // varchar(30) NULL

    // times (all tinyint → byte?)
    [Column("del_time_hour")] public byte? DelTimeHour { get; set; }
    [Column("del_time_min")] public byte? DelTimeMin { get; set; }
    [Column("return_time_hour")] public byte? ReturnTimeHour { get; set; }
    [Column("return_time_min")] public byte? ReturnTimeMin { get; set; }

    [Column("trans_qty")] public int? TransQty { get; set; }                // int NULL
    [Column("price")] public double? Price { get; set; }                // float NULL
    [Column("rate_selected")] public byte? RateSelected { get; set; }           // tinyint NULL

    [Column("hours")] public byte? Hours { get; set; }                  // tinyint NULL
    [Column("Minutes")] public byte? Minutes { get; set; }                // tinyint NULL

    [Column("person")] public string? Person { get; set; }               // char(30) NULL
    [Column("task")] public byte? Task { get; set; }                   // tinyint NULL

    [Column("TechRate")] public double? TechRate { get; set; }             // float NULL
    [Column("TechPay")] public double? TechPay { get; set; }              // float NULL
    [Column("unitRate")] public double? UnitRate { get; set; }             // float NULL

    [Column("techrateIsHourorDay")] public string? TechrateIsHourOrDay { get; set; }  // char(1) NULL

    [Column("FirstDate")] public DateTime? FirstDate { get; set; }          // datetime NULL
    [Column("RetnDate")] public DateTime? RetnDate { get; set; }           // datetime NULL

    [Column("GroupSeqNo")] public int? GroupSeqNo { get; set; }              // int NULL
    [Column("StraightTime")] public double? StraightTime { get; set; }         // float NULL
    [Column("OverTime")] public double? OverTime { get; set; }             // float NULL
    [Column("DoubleTime")] public double? DoubleTime { get; set; }           // float NULL

    [Column("UseCustomRate")] public bool? UseCustomRate { get; set; }          // bit NULL
    [Column("CustomRate")] public double? CustomRate { get; set; }           // float NULL
    [Column("HourOrDay")] public string? HourOrDay { get; set; }            // char(1) NULL
    [Column("ShortTurnaround")] public bool? ShortTurnaround { get; set; }        // bit NULL

    [Column("HourlyRateID")] public decimal HourlyRateID { get; set; }         // decimal(10,0) NOT NULL

    [Column("UnpaidHours")] public int? UnpaidHours { get; set; }             // int NULL
    [Column("UnpaidMins")] public int? UnpaidMins { get; set; }              // int NULL

    [Column("TechIsConfirmed")] public bool TechIsConfirmed { get; set; }         // bit NOT NULL
    [Column("MeetTechOnSite")] public bool MeetTechOnSite { get; set; }          // bit NOT NULL
}
