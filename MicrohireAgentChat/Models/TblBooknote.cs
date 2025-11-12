namespace MicrohireAgentChat.Models
{
    public sealed class TblBooknote
    {
        public decimal Id { get; set; }                 // PK, decimal(10,0)
        public string? BookingNo { get; set; }          // varchar(35), NULL
        public byte? LineNo { get; set; }               // tinyint, NULL
        public string? TextLine { get; set; }           // varchar(max), NULL
        public byte? NoteType { get; set; }             // tinyint, NULL  (1=user, 2=assistant)
        public decimal? OperatorId { get; set; }        // decimal(10,0), NULL
    }
}
