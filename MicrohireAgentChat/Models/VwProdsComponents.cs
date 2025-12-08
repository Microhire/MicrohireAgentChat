using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    [Table("vwProdsComponents")]
    public sealed class VwProdsComponents
    {
        [Key]
        [Column("ID")] public decimal ID { get; set; }
        
        [Column("parent_code")] public string ParentCode { get; set; } = null!;
        [Column("product_code")] public string ProductCode { get; set; } = null!;
        [Column("variable_part")] public byte? VariablePart { get; set; } // 0=fixed, 1=variable, 2=alternative
        [Column("qty_v5")] public double? Qty { get; set; }          // component quantity
        [Column("sub_seq_no")] public byte? SubSeqNo { get; set; }
        [Column("SelectComp")] public string? SelectComp { get; set; } // Y=selectable, N=fixed
        [Column("DescriptionV6")] public string? Description { get; set; }
    }
}
