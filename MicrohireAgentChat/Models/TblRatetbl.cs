namespace MicrohireAgentChat.Models
{
    public sealed class TblRatetbl
    {
        // Assuming rates are keyed by product + table number (no separate ID)
        public string product_code { get; set; } = null!;
        public byte TableNo { get; set; }

        // Base day rate (adjust precision/type in OnModelCreating to match DB)
        public double rate_1st_day { get; set; }

        // (Optional) add more columns if you have them in the DB:
        // public decimal? rate_2nd_day { get; set; }
        // public decimal? rate_week { get; set; }
        // public string? description { get; set; }
    }
}
