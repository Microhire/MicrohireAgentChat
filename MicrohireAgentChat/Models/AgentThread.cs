namespace MicrohireAgentChat.Models
{
    public sealed class AgentThread
    {
        public int Id { get; set; }
        public string UserKey { get; set; } = null!;
        public string ThreadId { get; set; } = null!;
        public DateTime CreatedUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}
