using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Models
{
    // Models/TblCrew.cs
    public sealed class TblCrew
    {
        public decimal ID { get; set; }               // decimal(10,0) identity
        public string BookingNoV32 { get; set; } = null!;
        public byte HeadingNo { get; set; }
        public int SeqNo { get; set; }
        public int SubSeqNo { get; set; }
        public string? ProductCodeV42 { get; set; }   // store "SETUP", "PACKDOWN", etc (or use a proper column)
        public byte? DelTimeHour { get; set; }
        public byte? DelTimeMin { get; set; }
        public short? ReturnTimeHour { get; set; }
        public short? ReturnTimeMin { get; set; }
        public double? Price { get; set; }
        public int? TransQty { get; set; }        // persons
        public decimal? HourlyRateID { get; set; }            // computed
        public double? UnitRate { get; set; }         // 110

        public int? Hours { get; set; }            // total hours
        public int? Minutes { get; set; }           // leftover minutes (usually 0)
        public string? Person { get; set; }           // optional note
        public int? Task { get; set; }             // label
        public bool? TechrateIsHourorDay { get; set; }// true = hour
        public DateTime? FirstDate { get; set; }
        public DateTime? RetnDate { get; set; }
        public int? GroupSeqNo { get; set; }
        public decimal? StraightTime { get; set; }
        public bool? TechIsConfirmed { get; set; }
        public bool? MeetTechOnSite { get; set; }
    }



}
