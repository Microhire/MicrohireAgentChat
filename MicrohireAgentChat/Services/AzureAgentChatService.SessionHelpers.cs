// AzureAgentChatService partial
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using Markdig;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services
{
    public sealed partial class AzureAgentChatService
    {
        #region Persisted thread mapping

        public async Task ReplacePersistedThreadAsync(string userKey, string newThreadId, CancellationToken ct)
        {
            var row = await _appDb.AgentThreads.FirstOrDefaultAsync(x => x.UserKey == userKey, ct);
            var now = DateTime.UtcNow;

            if (row is null)
            {
                _appDb.AgentThreads.Add(new AgentThread
                {
                    UserKey = userKey,
                    ThreadId = newThreadId,
                    CreatedUtc = now,
                    LastSeenUtc = now
                });
            }
            else
            {
                row.ThreadId = newThreadId;
                row.LastSeenUtc = now;
                _appDb.AgentThreads.Update(row);
            }

            await _appDb.SaveChangesAsync(ct);
        }

        public async Task<string> EnsureThreadIdPersistedAsync(
            ISession session,
            string userKey,
            CancellationToken ct)
        {
            var saved = await _appDb.AgentThreads
                .AsNoTracking()
                .Where(t => t.UserKey == userKey)
                .Select(t => t.ThreadId)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(saved))
            {
                session.SetString(SessionKeyThreadId, saved);
                await TouchLastSeenAsync(userKey, ct);
                return saved;
            }

            var threadId = EnsureThreadId(session);

            var now = DateTime.UtcNow;
            var existing = await _appDb.AgentThreads
                .Where(t => t.UserKey == userKey || t.ThreadId == threadId)
                .FirstOrDefaultAsync(ct);

            if (existing is null)
            {
                _appDb.AgentThreads.Add(new AgentThread
                {
                    UserKey = userKey,
                    ThreadId = threadId,
                    CreatedUtc = now,
                    LastSeenUtc = now
                });
            }
            else
            {
                existing.UserKey = userKey;
                existing.ThreadId = threadId;
                existing.LastSeenUtc = now;
                _appDb.AgentThreads.Update(existing);
            }

            await _appDb.SaveChangesAsync(ct);
            return threadId;
        }

        private async Task TouchLastSeenAsync(string userKey, CancellationToken ct)
        {
            var row = await _appDb.AgentThreads
                .Where(t => t.UserKey == userKey)
                .FirstOrDefaultAsync(ct);

            if (row is not null)
            {
                row.LastSeenUtc = DateTime.UtcNow;
                _appDb.AgentThreads.Update(row);
                await _appDb.SaveChangesAsync(ct);
            }
        }

        public async Task<string?> GetSavedThreadIdAsync(string userKey, CancellationToken ct)
        {
            var t = await _appDb.AgentThreads
                .AsNoTracking()
                .Where(x => x.UserKey == userKey)
                .Select(x => x.ThreadId)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(t))
                await TouchLastSeenAsync(userKey, ct);

            return t;
        }

        public async Task<(string ThreadId, IEnumerable<DisplayMessage> Messages)?>
            GetTranscriptForUserAsync(string userKey, CancellationToken ct)
        {
            var threadId = await GetSavedThreadIdAsync(userKey, ct);
            if (string.IsNullOrWhiteSpace(threadId))
                return null;
            var result = GetTranscript(threadId);
            return result;
        }

        #endregion


        public (TimeSpan? startTime, TimeSpan? endTime, string? matched) ExtractEventTime(IEnumerable<DisplayMessage> messages)
            => _chatExtraction.ExtractEventTime(messages);

        /// <summary>
        /// Returns true when transcript has name + (email or phone), matching PostProcessAfterRunAsync save logic.
        /// </summary>
        private bool ShouldShowContactSavePending(IEnumerable<DisplayMessage> messages)
        {
            var messageList = messages.ToList();
            var transcript = string.Join(" ", messageList.Select(m => m.FullText ?? ""));
            if (!transcript.Contains('@') && !Regex.IsMatch(transcript, @"\+?\d{10,}"))
                return false;

            var contactInfo = _chatExtraction.ExtractContactInfo(messageList);
            var hasEmail = !string.IsNullOrWhiteSpace(contactInfo.Email);
            var hasPhone = !string.IsNullOrWhiteSpace(contactInfo.PhoneE164);
            return !string.IsNullOrWhiteSpace(contactInfo.Name) && (hasEmail || hasPhone);
        }

        /// <summary>
        /// Adds the "One moment please" assistant message to the thread for two-phase contact save.
        /// </summary>
        private void AddContactSavePendingMessage(string threadId)
        {
            var text = "Thank you for providing your contact details. I've noted your phone number and email address. I'll now save your contact details to proceed further. One moment, please!";
            AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, text);
        }

        private async Task PostProcessAfterRunAsync(IEnumerable<DisplayMessage> messages, CancellationToken ct)
        {
            try
            {
                var messageList = messages.ToList();
                AutoPersistContactInfoToSession(messageList);
                
                var transcript = string.Join(" ", messageList.Select(m => m.FullText ?? ""));
                if (!transcript.Contains('@') && !Regex.IsMatch(transcript, @"\+?\d{10,}"))
                {
                    return;
                }
                
                var (contactId, orgId) = await _orchestration.SaveContactAndOrganizationAsync(messageList, ct);

                var session = _http.HttpContext?.Session;
                if (session == null) return;

                if (contactId.HasValue)
                {
                    session.SetString("Draft:ContactId", contactId.Value.ToString());
                    _logger.LogInformation("[PROACTIVE] Stored ContactId={ContactId} in session", contactId.Value);
                }
                if (orgId.HasValue)
                {
                    session.SetString("Draft:OrgId", orgId.Value.ToString());
                    _logger.LogInformation("[PROACTIVE] Stored OrgId={OrgId} in session", orgId.Value);
                }

                if (contactId.HasValue && string.IsNullOrWhiteSpace(session.GetString("Draft:BookingNo")))
                {
                    var bookingNo = await _bookingPersistence.CreateBookingOnTheFlyAsync(session, messageList, ct);
                    if (!string.IsNullOrWhiteSpace(bookingNo))
                    {
                        session.SetString("Draft:BookingNo", bookingNo);
                        _logger.LogInformation("[PROACTIVE] Draft booking created: {BookingNo}", bookingNo);

                        try
                        {
                            await _bookingPersistence.SaveFullTranscriptToBooknoteAsync(bookingNo, messageList, ct);
                            _logger.LogInformation("[PROACTIVE] Transcript saved for new booking {BookingNo}", bookingNo);
                        }
                        catch (Exception txEx)
                        {
                            _logger.LogWarning(txEx, "[PROACTIVE] Failed to save transcript for booking {BookingNo}", bookingNo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Post-processing failed");
            }
        }
        
        /// <summary>
        /// Auto-extract and persist contact information and event date to session so that tool handlers
        /// (like recommend_equipment_for_event and generate_quote) can find them even if save_contact wasn't explicitly called.
        /// This prevents the agent from re-asking for contact details or dates that were already provided.
        /// </summary>
        private void AutoPersistContactInfoToSession(IEnumerable<DisplayMessage> messages)
        {
            try
            {
                var session = _http.HttpContext?.Session;
                if (session == null)
                {
                    _logger.LogDebug("No session available for auto-persisting contact info");
                    return;
                }
                
                // Extract contact info from conversation
                var contactInfo = _chatExtraction.ExtractContactInfo(messages);
                var (orgName, orgAddress) = _chatExtraction.ExtractOrganisationFromTranscript(messages);
                var (eventDate, _) = _chatExtraction.ExtractEventDate(messages);
                var eventType = ChatExtractionService.ExtractEventType(messages);
                
                // Only persist if we found something AND session doesn't already have it
                // (Don't overwrite explicit save_contact data)
                if (!string.IsNullOrWhiteSpace(contactInfo.Name) && 
                    string.IsNullOrWhiteSpace(session.GetString("Draft:ContactName")))
                {
                    session.SetString("Draft:ContactName", contactInfo.Name);
                    _logger.LogDebug("Auto-persisted contact name to session: {Name}", contactInfo.Name);
                }
                
                if (!string.IsNullOrWhiteSpace(contactInfo.Email) && 
                    string.IsNullOrWhiteSpace(session.GetString("Draft:ContactEmail")))
                {
                    session.SetString("Draft:ContactEmail", contactInfo.Email);
                    _logger.LogDebug("Auto-persisted contact email to session: {Email}", contactInfo.Email);
                }
                
                if (!string.IsNullOrWhiteSpace(contactInfo.PhoneE164) && 
                    string.IsNullOrWhiteSpace(session.GetString("Draft:ContactPhone")))
                {
                    session.SetString("Draft:ContactPhone", contactInfo.PhoneE164);
                    _logger.LogDebug("Auto-persisted contact phone to session: {Phone}", contactInfo.PhoneE164);
                }
                
                if (!string.IsNullOrWhiteSpace(contactInfo.Position) && 
                    string.IsNullOrWhiteSpace(session.GetString("Draft:Position")))
                {
                    session.SetString("Draft:Position", contactInfo.Position);
                    _logger.LogDebug("Auto-persisted contact position to session: {Position}", contactInfo.Position);
                }
                
                if (!string.IsNullOrWhiteSpace(orgName) && 
                    string.IsNullOrWhiteSpace(session.GetString("Draft:Organisation")))
                {
                    session.SetString("Draft:Organisation", orgName);
                    _logger.LogDebug("Auto-persisted organisation to session: {Org}", orgName);
                }

                if (!string.IsNullOrWhiteSpace(orgAddress) &&
                    string.IsNullOrWhiteSpace(session.GetString("Draft:OrganisationAddress")))
                {
                    session.SetString("Draft:OrganisationAddress", orgAddress);
                    _logger.LogDebug("Auto-persisted organisation address to session: {OrgAddress}", orgAddress);
                }
                
                // Auto-persist event date if extracted from conversation
                if (eventDate.HasValue && string.IsNullOrWhiteSpace(session.GetString("Draft:EventDate")))
                {
                    var dateStr = eventDate.Value.DateTime.ToString("yyyy-MM-dd");
                    session.SetString("Draft:EventDate", dateStr);
                    _logger.LogInformation("Auto-persisted event date to session: {Date}", dateStr);
                }

                if (!string.IsNullOrWhiteSpace(eventType) &&
                    string.IsNullOrWhiteSpace(session.GetString("Draft:EventType")))
                {
                    session.SetString("Draft:EventType", eventType);
                    _logger.LogDebug("Auto-persisted event type to session: {EventType}", eventType);
                }

                // Auto-persist venue and room from conversation (for Rental Point tblbookings)
                var (_, venueName, _, _) = _chatExtraction.ExtractVenueAndEventDate(messages);
                var roomName = _extraction.ExtractRoom(messages);
                if (!string.IsNullOrWhiteSpace(venueName) &&
                    string.IsNullOrWhiteSpace(session.GetString("Draft:VenueName")))
                {
                    session.SetString("Draft:VenueName", venueName);
                    _logger.LogDebug("Auto-persisted venue to session: {Venue}", venueName);
                }
                if (!string.IsNullOrWhiteSpace(roomName) &&
                    string.IsNullOrWhiteSpace(session.GetString("Draft:RoomName")))
                {
                    session.SetString("Draft:RoomName", roomName);
                    _logger.LogDebug("Auto-persisted room to session: {Room}", roomName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-persist contact info to session");
            }
        }


        // Robust 429 retry with exponential backoff + jitter.
        // Use for ALL Azure Agents SDK calls that can rate-limit.
        private static async Task<T> With429RetryAsync<T>(
            Func<Task<T>> action,
            string label,
            CancellationToken ct,
            int maxAttempts = 8,
            TimeSpan? maxElapsed = null)
        {
            var rand = new Random();
            int attempt = 0;
            var startedUtc = DateTime.UtcNow;
            var maxElapsedLocal = maxElapsed ?? TimeSpan.FromSeconds(60);

            // Start at ~2s; we’ll back off up to ~20s.
            var delay = TimeSpan.FromSeconds(2);

            // Helper: try to extract Retry-After seconds from exception
            static int? TryGetRetryAfterSeconds(Azure.RequestFailedException ex)
            {
                // 1) Some SDKs stash it in ex.Data
                try
                {
                    if (ex.Data != null)
                    {
                        if (ex.Data["Retry-After"] is string raStr && int.TryParse(raStr, out var secs1))
                            return secs1;
                        if (ex.Data["retry-after"] is string raStr2 && int.TryParse(raStr2, out var secs2))
                            return secs2;
                    }
                }
                catch { /* ignore */ }

                // 2) Sometimes the message includes it (best-effort)
                try
                {
                    var m = Regex.Match(ex.Message ?? string.Empty, @"Retry-After[:=\s]+(?<s>\d+)", RegexOptions.IgnoreCase);
                    if (m.Success && int.TryParse(m.Groups["s"].Value, out var secs))
                        return secs;
                }
                catch { /* ignore */ }

                return null;
            }

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    return await action();
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 429)
                {
                    attempt++;
                    var elapsed = DateTime.UtcNow - startedUtc;
                    if (attempt >= maxAttempts || elapsed >= maxElapsedLocal)
                    {
                        throw new TimeoutException(
                            $"[{label}] exceeded retry limits after {attempt} attempts and {elapsed.TotalSeconds:F1}s.",
                            ex);
                    }

                    // Prefer server hint if we can find one; otherwise use our current delay.
                    var hinted = TryGetRetryAfterSeconds(ex);
                    var wait = hinted.HasValue ? TimeSpan.FromSeconds(hinted.Value) : delay;
                    var remaining = maxElapsedLocal - elapsed;
                    if (wait > remaining)
                    {
                        wait = remaining;
                    }
                    if (wait <= TimeSpan.Zero)
                    {
                        throw new TimeoutException(
                            $"[{label}] exceeded retry time budget before next retry.",
                            ex);
                    }

                    // Add +/-30% jitter
                    var ms = (int)wait.TotalMilliseconds;
                    var withJitter = rand.Next((int)(ms * 0.7), (int)(ms * 1.3));
                    await Task.Delay(withJitter, ct);

                    // Exponential backoff up to ~20s
                    var nextMs = Math.Min(ms * 2, 20_000);
                    delay = TimeSpan.FromMilliseconds(nextMs);
                }
                catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.Message.Contains("429"))
                {
                    // Some layers may surface 429 as HttpRequestException
                    attempt++;
                    var elapsed = DateTime.UtcNow - startedUtc;
                    if (attempt >= maxAttempts || elapsed >= maxElapsedLocal)
                    {
                        throw new TimeoutException(
                            $"[{label}] exceeded retry limits after {attempt} attempts and {elapsed.TotalSeconds:F1}s.",
                            httpEx);
                    }
                    var baseMs = Math.Min(2000 * Math.Pow(2, attempt - 1), 20000); // 2s -> 4 -> 8 -> 16 -> 20
                    var jitter = rand.Next((int)(baseMs * 0.7), (int)(baseMs * 1.3));
                    var remainingMs = Math.Max(0, (maxElapsedLocal - elapsed).TotalMilliseconds);
                    jitter = (int)Math.Min(jitter, remainingMs);
                    if (jitter <= 0)
                    {
                        throw new TimeoutException(
                            $"[{label}] exceeded retry time budget before next retry.",
                            httpEx);
                    }
                    await Task.Delay(jitter, ct);
                }
            }
        }

        // Save multi-schedule selection to session for time picker persistence
        private void SaveMultiScheduleToSession(ScheduleSelection s)
        {
            var session = _http.HttpContext?.Session;
            if (session == null) return;

            if (s.Setup != default) session.SetString("Draft:SetupTime", s.Setup.ToString(@"hh\:mm"));
            if (s.Rehearsal != default) session.SetString("Draft:RehearsalTime", s.Rehearsal.ToString(@"hh\:mm"));
            if (s.Start.HasValue) session.SetString("Draft:StartTime", s.Start.Value.ToString(@"hh\:mm"));
            if (s.End.HasValue) session.SetString("Draft:EndTime", s.End.Value.ToString(@"hh\:mm"));
            if (s.PackUp != default) session.SetString("Draft:PackupTime", s.PackUp.ToString(@"hh\:mm"));
            
            // Save event date and mark as confirmed when schedule is submitted via time picker
            if (s.EventDate.HasValue)
            {
                session.SetString("Draft:EventDate", s.EventDate.Value.ToString("yyyy-MM-dd"));
                session.SetString("Draft:DateConfirmed", "1");
                _logger.LogInformation("[DATE CONFIRM] Date confirmed via time picker: {EventDate}", s.EventDate.Value.ToString("yyyy-MM-dd"));
            }
            
            _logger.LogInformation("Saved multi-schedule to session: Setup={Setup}, Rehearsal={Rehearsal}, Start={Start}, End={End}, PackUp={PackUp}, EventDate={EventDate}",
                s.Setup, s.Rehearsal, s.Start, s.End, s.PackUp, s.EventDate);
        }

    }
}
