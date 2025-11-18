using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    [Table("tblcust")]
    public sealed class TblCust
    {

        [Key]
        [Column("ID")]
        public decimal ID { get; set; }

        [MaxLength(120)]
        [Column("OrganisationV6")]
        public string? OrganisationV6 { get; set; }

        [MaxLength(200)]
        [Column("Address_l1V6")]
        public string? Address_l1V6 { get; set; }

        [MaxLength(200)]
        [Column("Customer_code")]
        public string? Customer_code { get; set; }


    }
}
