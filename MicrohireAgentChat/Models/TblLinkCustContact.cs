namespace MicrohireAgentChat.Models
{
    public class TblLinkCustContact
    {
        public decimal ID { get; set; }                 // PK identity (decimal(10,0))
        public string? Customer_Code { get; set; }      // varchar(30)
        public decimal? ContactID { get; set; }
    }
}
