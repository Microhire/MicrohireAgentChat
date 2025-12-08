using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models;

/// <summary>
/// Rate table containing pricing for products
/// Different TableNo values represent different pricing schemes/tiers
/// TableNo 0 is typically the default/retail rate
/// </summary>
[Table("tblRatetbl")]
public sealed class TblRatetbl
{
    [Key]
    public decimal ID { get; set; }
    
    [Column("ProductCode")]
    [StringLength(30)]
    public string? product_code { get; set; }
    
    [Column("tableNo")]
    public byte? TableNo { get; set; }
    
    public double? hourly_rate { get; set; }
    public double? half_day { get; set; }
    public double? rate_1st_day { get; set; }
    public double? rate_extra_days { get; set; }
    public double? rate_week { get; set; }
    public double? rate_long_term { get; set; }
    public double? deposit { get; set; }
    public double? damage_waiver_rate { get; set; }
    public double? DayWeekRate { get; set; }
    public double? MinimumRental { get; set; }
    public double? ReplacementValue { get; set; }
    public double? Rate3rdDay { get; set; }
    public double? Rate4thDay { get; set; }
    public double? Rate2ndWeek { get; set; }
    public double? Rate3rdWeek { get; set; }
    public double? RateAdditionalMonth { get; set; }
    public double? RatePrep { get; set; }
    public double? RateWrap { get; set; }
    public double? RatePreLight { get; set; }
}
