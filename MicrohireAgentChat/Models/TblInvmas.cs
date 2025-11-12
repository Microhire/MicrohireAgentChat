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
    }
}
