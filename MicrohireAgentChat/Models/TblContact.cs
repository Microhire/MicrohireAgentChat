using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    [Table("tblContact")]
    public sealed class TblContact
    {
        // decimal(10,0) in DB
        [Key]
        [Column("ID", TypeName = "decimal(10, 0)")]
        public decimal Id { get; set; }

        [Column("CustCodeLink")] public string? CustCodeLink { get; set; }

        [Column("Contactname")] public string? Contactname { get; set; }   // full display name (<=35)
        [Column("firstname")] public string? Firstname { get; set; }     // <=25
        [Column("surname")] public string? Surname { get; set; }       // <=35
        [Column("MidName")] public string? MidName { get; set; }
        [Column("position")] public string? Position { get; set; }
        [Column("Email")] public string? Email { get; set; }         // <=80
        [Column("Cell")] public string? Cell { get; set; }          // <=16 (use for mobile)
        [Column("Phone1")] public string? Phone1 { get; set; }        // <=16

        [Column("Active")] public string? Active { get; set; }        // 'Y' or 'N'
        [Column("LastContact")] public DateTime? LastContact { get; set; }
        [Column("LastAttempt")] public DateTime? LastAttempt { get; set; }
        [Column("CreateDate")] public DateTime? CreateDate { get; set; }
        [Column("LastUpdate")] public DateTime? LastUpdate { get; set; }

        // add more columns later if you need them
    }
}
