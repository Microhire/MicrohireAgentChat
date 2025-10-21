namespace MicrohireAgentChat.Models
{
    public sealed class AgentChatHistoryViewModel
    {
        public string? UserKey { get; set; }
        public string? ThreadId { get; set; }
        public IEnumerable<DisplayMessage> Messages { get; set; } = Enumerable.Empty<DisplayMessage>();
        public string? Error { get; set; }

        // Event date
        public DateTimeOffset? EventDate { get; set; }
        public string? EventDateDisplay => EventDate?.ToString("dd MMM yyyy");
        public string? EventDateMatchedText { get; set; }

        // Venue
        public string? VenueName { get; set; }
        public string? VenueMatchedText { get; set; }

        // Event type
        public string? EventType { get; set; }
        public string? EventTypeMatchedText { get; set; }

        // --- NEW: Contact details ---
        public string? ContactName { get; set; }
        public string? ContactNameMatchedText { get; set; }

        public string? Email { get; set; }
        public string? EmailMatchedText { get; set; }

        // Store phone in normalized (E.164) form when possible, e.g. +61412xxxxxx
        public string? Phone { get; set; }
        public string? PhoneMatchedText { get; set; }
    }

}
