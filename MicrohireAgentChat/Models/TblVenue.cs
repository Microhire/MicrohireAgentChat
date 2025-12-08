using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    [Table("tblVenues")]
    public sealed class TblVenue
    {
        [Key]
        public decimal ID { get; set; }
        
        public string? VenueName { get; set; }
        public string? ContactName { get; set; }
        public decimal? ContactID { get; set; }
        public string? WebPage { get; set; }
        
        // Address fields
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? ZipCode { get; set; }
        
        // Phone fields
        public string? Phone1CountryCode { get; set; }
        public string? Phone1AreaCode { get; set; }
        public string? Phone1Digits { get; set; }
        public string? Phone1Ext { get; set; }
        
        public string? Phone2CountryCode { get; set; }
        public string? Phone2AreaCode { get; set; }
        public string? Phone2Digits { get; set; }
        public string? Phone2Ext { get; set; }
        
        // Fax fields
        public string? FaxCountryCode { get; set; }
        public string? FaxAreaCode { get; set; }
        public string? FaxDigits { get; set; }
        
        // Cell/Mobile fields
        public string? CellCountryCode { get; set; }
        public string? CellAreaCode { get; set; }
        public string? CellDigits { get; set; }
        
        // Type and metadata
        public byte? Type { get; set; }
        public string? BookingNo { get; set; }
        public string? VenueNickname { get; set; }
        public string? VenueTextType { get; set; }
        public string? DefaultFolder { get; set; }
        
        // Helper properties
        [NotMapped]
        public string FullAddress => string.Join(", ", 
            new[] { Address1, Address2, City, State, ZipCode, Country }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        
        [NotMapped]
        public string Phone1Full => FormatPhone(Phone1CountryCode, Phone1AreaCode, Phone1Digits, Phone1Ext);
        
        private static string FormatPhone(string? country, string? area, string? digits, string? ext)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(country)) parts.Add($"+{country}");
            if (!string.IsNullOrWhiteSpace(area)) parts.Add(area);
            if (!string.IsNullOrWhiteSpace(digits)) parts.Add(digits);
            var phone = string.Join(" ", parts);
            if (!string.IsNullOrWhiteSpace(ext)) phone += $" ext {ext}";
            return phone;
        }
    }
}

