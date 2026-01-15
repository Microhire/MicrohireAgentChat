using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicrohireAgentChat.Models
{
    [Table("tblInvmas")]
    public sealed class TblInvmas
    {
        [Key] public string? product_code { get; set; }

        public string? PictureFileName { get; set; }
        public string? category { get; set; }
        public string? descriptionv6 { get; set; }
        public string? PrintedDesc { get; set; }
        public string? groupFld { get; set; }
        
        // Price columns from actual database schema
        public double? cost_price { get; set; }
        public double? retail_price { get; set; }
        public double? wholesale_price { get; set; }
        public double? trade_price { get; set; }
        
        // Additional properties for equipment search
        [Column("IsInTrashCan")]
        public string? IsInTrashCan { get; set; }
        
        [Column("product_type_v41")]
        public byte? ProductTypeV41 { get; set; }  // 0=normal item, 1=package
        
        [Column("on_hand")]
        public double? OnHand { get; set; }
        
        /// <summary>
        /// SubCategory groups products within a category.
        /// For LAPTOP category: LAPPACK (packages), SHOWLAP (high-end show laptops), MIDRLAP (mid-range), CONTL (control laptops)
        /// For MACBOOK category: MBPPACK (packages), etc.
        /// </summary>
        [Column("SubCategory")]
        public string? SubCategory { get; set; }
        
        /// <summary>
        /// Product configuration type.
        /// 0 = Individual product (e.g., DELL3580, PC-MOUSE)
        /// 1 = Package (e.g., PCLPRO, PCLP-L1) - contains multiple components
        /// </summary>
        [Column("product_Config")]
        public byte? ProductConfig { get; set; }
    }
}
