namespace MicrohireAgentChat.Models
{
    public sealed class AgentThread
    {
        public int Id { get; set; }
        public string UserKey { get; set; } = null!;
        public string ThreadId { get; set; } = null!;
        public string? Email { get; set; }       // verified email, used for cross-session lookup
        public string? DraftStateJson { get; set; } // serialized Dictionary<string,string> of all Draft:* keys
        public DateTime CreatedUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}
