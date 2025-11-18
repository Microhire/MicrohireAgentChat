using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    [Table("vwProdsComponents")]
    public sealed class VwProdsComponents
    {
        // Adjust names if your view uses different column names
        [Column("parent_code")] public string ParentCode { get; set; } = null!;
        [Column("product_code")] public string ProductCode { get; set; } = null!;
        [Column("variable_part")] public byte? VariablePart { get; set; } // 0/1 in SQL (tinyint/bit)
        [Column("qty_v5")] public double? Qty { get; set; }          // component quantity (if present)
        [Column("sub_seq_no")] public byte? SubSeqNo { get; set; }
    }
}
