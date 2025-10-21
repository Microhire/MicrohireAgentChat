namespace MicrohireAgentChat.Models
{
    public sealed class DisplayMessage
    {
        public string Role { get; set; } = string.Empty;      // "user", "assistant", "system"
        public DateTimeOffset Timestamp { get; set; }         // when the message was created
        public List<string> Parts { get; set; } = new();      // the text/content parts

        // convenience constructor
        public DisplayMessage(string role, DateTimeOffset ts, IEnumerable<string> parts)
        {
            Role = role;
            Timestamp = ts;
            Parts = parts.ToList();
        }
        public string FullText { get; set; } = "";
        public string Html { get; set; } = "";
        public DisplayMessage() { }
    }
}
