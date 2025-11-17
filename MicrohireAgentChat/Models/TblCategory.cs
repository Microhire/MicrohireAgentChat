using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    [Table("tblCategory")]
    public sealed class TblCategory
    {
        [Key]
        [Column("category_code")]
        [MaxLength(30)]
        public string CategoryCode { get; set; } = null!;

        [Column("cat_descV6")]
        [MaxLength(50)]
        public string? CategoryDescription { get; set; }

        [Column("Group_code")]
        [MaxLength(30)]
        public string? GroupCode { get; set; }

        [Column("ParentCategoryCode")]
        [MaxLength(30)]
        public string? ParentCategoryCode { get; set; }

        [Column("CategoryType")]
        public byte? CategoryType { get; set; } // 0 = parent, 1 = sub-category
    }
}

