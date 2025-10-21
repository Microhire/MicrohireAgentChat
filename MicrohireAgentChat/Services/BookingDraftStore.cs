using System;
using System.Collections.Concurrent;

namespace MicrohireAgentChat.Services
{
    public sealed class BookingDraft
    {
        public string? RoomKey { get; set; }       // e.g., "thrive-boardroom" (if you already capture it)
        public DateOnly? Date { get; set; }        // chosen date
        public TimeSpan? Start { get; set; }       // chosen start time
        public TimeSpan? End { get; set; }         // chosen end time
        public string? EventName { get; set; }
        public TimeSpan? Setup { get; set; }
        public TimeSpan? Rehearsal { get; set; }
        public TimeSpan? PackUp { get; set; }    // optional, if you store it
    }

    public interface IBookingDraftStore
    {
        BookingDraft GetOrCreate(string threadId);
        void Upsert(string threadId, Action<BookingDraft> apply);
        BookingDraft? TryGet(string threadId);
    }

    public sealed class BookingDraftStore : IBookingDraftStore
    {
        private readonly ConcurrentDictionary<string, BookingDraft> _map = new();

        public BookingDraft GetOrCreate(string threadId) =>
            _map.GetOrAdd(threadId, _ => new BookingDraft());

        public void Upsert(string threadId, Action<BookingDraft> apply)
        {
            var draft = _map.GetOrAdd(threadId, _ => new BookingDraft());
            apply(draft);
        }

        public BookingDraft? TryGet(string threadId)
        {
            _map.TryGetValue(threadId, out var draft);
            return draft;
        }
    }
}
