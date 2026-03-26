using System.Text.Json;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Services;

public sealed class ChatSessionPersistenceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChatSessionPersistenceService> _logger;

    public ChatSessionPersistenceService(AppDbContext db, ILogger<ChatSessionPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Snapshots all relevant Draft:* keys from the session into a dictionary.
    /// This is safe to call from the UI thread before passing to a background task.
    /// </summary>
    public Dictionary<string, string> SnapshotDraftState(ISession session)
    {
        var snapshot = new Dictionary<string, string>();
        foreach (var key in session.Keys)
        {
            if (key.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase) ||
                key == "ContactSaveCompleted" ||
                key == "IslaGreeted" ||
                key.StartsWith("Ack:", StringComparison.OrdinalIgnoreCase))
            {
                var val = session.GetString(key);
                if (val != null) snapshot[key] = val;
            }
        }
        return snapshot;
    }

    /// <summary>
    /// Persists the draft state snapshot to the AgentThreads table.
    /// </summary>
    public async Task SaveAsync(string userKey, string email, Dictionary<string, string> snapshot, CancellationToken ct = default)
    {
        try
        {
            var row = await _db.AgentThreads.FirstOrDefaultAsync(x => x.UserKey == userKey, ct);
            if (row == null)
            {
                _logger.LogWarning("Cannot save draft state: no AgentThread found for UserKey {UserKey}", userKey);
                return;
            }

            row.Email = email.Trim().ToLowerInvariant();
            row.DraftStateJson = JsonSerializer.Serialize(snapshot);
            row.LastSeenUtc = DateTime.UtcNow;

            _db.AgentThreads.Update(row);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Persisted draft state for {Email} (UserKey: {UserKey})", email, userKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save draft state for {Email}", email);
        }
    }

    /// <summary>
    /// Finds the most recent session for a verified email address.
    /// </summary>
    public async Task<AgentThread?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _db.AgentThreads
            .OrderByDescending(x => x.LastSeenUtc)
            .FirstOrDefaultAsync(x => x.Email == normalized, ct);
    }

    /// <summary>
    /// Restores saved draft keys to the current session, avoiding overwriting existing data.
    /// </summary>
    public void RestoreToSession(ISession session, AgentThread saved)
    {
        if (string.IsNullOrWhiteSpace(saved.DraftStateJson)) return;

        try
        {
            var state = JsonSerializer.Deserialize<Dictionary<string, string>>(saved.DraftStateJson);
            if (state == null) return;

            foreach (var kvp in state)
            {
                // Only restore if the key is missing from current session
                if (string.IsNullOrWhiteSpace(session.GetString(kvp.Key)))
                {
                    session.SetString(kvp.Key, kvp.Value);
                }
            }

            // Always restore the thread ID to reconnect the conversation
            session.SetString("AgentThreadId", saved.ThreadId);
            _logger.LogInformation("Restored session state from Thread {ThreadId}", saved.ThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore session state from JSON blob");
        }
    }
}
