# Thread Persistence Testing Guide

## Overview

This document provides comprehensive testing procedures to verify that conversation threads are persisted correctly and survive session loss, app restarts, and Azure scaling events.

## Prerequisites

1. Ensure the `AgentThreads` table has been created in the BookingsDb database:
   ```sql
   -- Run this script if not already done:
   EXEC sp_executesql N'Scripts/CreateAgentThreadsTable.sql'
   ```

2. Verify the `BookingDbContext` has been updated with the `AgentThread` entity mapping.

3. Verify the `AzureAgentChatService` has the persistence code uncommented.

## Test Cases

### Test 1: Basic Thread Creation and Retrieval

**Objective:** Verify that a new thread is created and persisted to the database.

**Steps:**
1. Start the application
2. Open the chat interface in a browser
3. Send a test message: "Hello, my name is Test User"
4. Note the thread ID from the browser console or Foundry
5. Query the database:
   ```sql
   SELECT * FROM dbo.AgentThreads WHERE UserKey LIKE '%'
   ```
6. Verify the thread ID matches and has a creation timestamp

**Expected Result:**
- Thread ID is created and stored in the database
- `UserKey` is populated (either session ID or username)
- `CreatedUtc` and `LastSeenUtc` timestamps are set

---

### Test 2: Session Loss Recovery (Clear Cookies)

**Objective:** Verify that a conversation resumes with the same thread after cookies are cleared.

**Steps:**
1. Start a conversation: "Hello, my name is Alice from Company X"
2. Send at least 2-3 exchanges with the AI
3. **Note the Thread ID** (from browser dev tools or logs)
4. Clear browser cookies/site data:
   - In Chrome DevTools: `Application > Storage > Clear site data`
   - Or manually clear `.MicrohireAgent.Session` cookie
5. Refresh the page
6. Continue the conversation: "Can you summarize what we discussed?"
7. Verify the thread ID is the same

**Expected Result:**
- A new session cookie is created (different Session ID)
- BUT the thread ID remains the same
- The AI can reference previous messages in the conversation
- Database shows same thread with updated `LastSeenUtc`

---

### Test 3: App Restart Recovery

**Objective:** Verify that a conversation resumes after the application is restarted.

**Steps:**
1. Start a conversation: "Hello, I'm Bob from Organization Y"
2. Send multiple messages to establish context
3. Note the Thread ID
4. **Restart the application:**
   - Stop the web app in Visual Studio or Azure Portal
   - Wait 5 seconds
   - Restart the web app
5. Refresh the browser (keep the session cookie intact)
6. Continue the conversation: "What was my organization name?"

**Expected Result:**
- Session cookie is preserved (same Session ID)
- Thread is retrieved from database
- AI recalls all previous context
- No new thread is created

---

### Test 4: Multiple Users Don't Interfere (The Original Bug)

**Objective:** Verify that one user's messages don't interrupt another user's conversation.

**Steps:**
1. **User A Session:**
   - Open chat in Browser A (e.g., Chrome)
   - Send: "Hello, I'm User A"
   - Send: "I need 10 projectors for my event"
   - Note Thread ID for User A
   - Keep this session open

2. **User B Session:**
   - Open chat in Browser B (e.g., Firefox, Incognito, different computer)
   - Send: "Hello, I'm User B"
   - Send: "I need 5 microphones"
   - Note Thread ID for User B

3. **Verify Isolation:**
   - In Browser A: Send "What did I ask for?"
   - Expected: "You need 10 projectors"
   - In Browser B: Send "What did I ask for?"
   - Expected: "You need 5 microphones"

4. **Database Verification:**
   ```sql
   SELECT UserKey, ThreadId, CreatedUtc FROM dbo.AgentThreads ORDER BY CreatedUtc DESC
   ```
   - Should show 2 different entries with different `UserKey` and `ThreadId` values

**Expected Result:**
- Each user has their own thread
- No cross-contamination of messages
- No new threads are created mid-conversation for the same user

---

### Test 5: Azure Scaling Simulation

**Objective:** Verify thread persistence works with multiple app instances (simulating Azure auto-scaling).

**Prerequisites:**
- Application must be deployed to Azure App Service or a load-balanced environment

**Steps:**
1. Start a conversation in the app
2. Send context: "Hello, I'm Charlie from Tech Corp, I need 20 sound systems"
3. Note the Thread ID and current App Instance
4. Trigger an app instance scale-up:
   - In Azure Portal: App Service Plan > Scale up/down
   - Or restart one of multiple instances
5. During/after scaling, continue the conversation: "Confirm my event details"

**Expected Result:**
- Request may route to a different app instance
- Thread is retrieved from centralized database
- AI maintains conversation context
- No context loss occurs

---

### Test 6: Thread Isolation - Verify Unique Constraints

**Objective:** Verify database constraints prevent duplicate threads.

**Steps:**
1. Query the database for thread records:
   ```sql
   SELECT UserKey, ThreadId, COUNT(*) as Count 
   FROM dbo.AgentThreads 
   GROUP BY UserKey, ThreadId 
   HAVING COUNT(*) > 1
   ```

2. Verify results are empty

3. Check unique index integrity:
   ```sql
   EXEC sp_helpindex 'dbo.AgentThreads'
   ```

**Expected Result:**
- No duplicate entries found
- Unique indexes on both `UserKey` and `ThreadId` are enforced
- Database schema integrity is intact

---

## Verification Queries

### View All Active Threads
```sql
SELECT 
    Id,
    UserKey,
    ThreadId,
    CreatedUtc,
    LastSeenUtc,
    DATEDIFF(MINUTE, LastSeenUtc, GETUTCDATE()) as MinutesSinceLastSeen
FROM dbo.AgentThreads
ORDER BY LastSeenUtc DESC
```

### Check Thread Update Frequency
```sql
SELECT 
    ThreadId,
    CreatedUtc,
    LastSeenUtc,
    DATEDIFF(SECOND, CreatedUtc, LastSeenUtc) as DurationSeconds
FROM dbo.AgentThreads
WHERE LastSeenUtc > DATEADD(HOUR, -1, GETUTCDATE())
ORDER BY LastSeenUtc DESC
```

### Monitor Thread Churn (New threads per hour)
```sql
SELECT 
    DATEPART(HOUR, CreatedUtc) as CreationHour,
    COUNT(*) as ThreadsCreated
FROM dbo.AgentThreads
WHERE CreatedUtc > DATEADD(DAY, -1, GETUTCDATE())
GROUP BY DATEPART(HOUR, CreatedUtc)
ORDER BY CreationHour DESC
```

---

## Logging and Debugging

### Enable Debug Logging in AzureAgentChatService

Add these log statements to track persistence operations:

```csharp
_logger.LogInformation("EnsureThreadIdPersisted: Checking for saved thread for userKey={UserKey}", userKey);
_logger.LogInformation("EnsureThreadIdPersisted: Found saved thread {ThreadId} for userKey={UserKey}", saved, userKey);
_logger.LogInformation("EnsureThreadIdPersisted: Creating new thread {ThreadId} for userKey={UserKey}", threadId, userKey);
_logger.LogInformation("TouchLastSeen: Updated LastSeenUtc for userKey={UserKey}", userKey);
```

### Browser Console Logging

Add to the chat JavaScript to log thread information:
```javascript
console.log('Current Thread ID:', sessionStorage.getItem('AgentThreadId'));
console.log('Current Session ID:', sessionStorage.getItem('PersistUserKey'));
```

---

## Success Criteria

After implementing thread persistence, the following must be true:

1. ✅ **Thread Continuity:** Same conversation continues after session/cookie loss
2. ✅ **No New Threads:** No spurious new threads created mid-conversation
3. ✅ **User Isolation:** One user's messages don't appear in another user's thread
4. ✅ **Context Preserved:** AI recalls all previous messages in a thread
5. ✅ **Database Consistency:** No orphaned or duplicate threads in database
6. ✅ **Azure Scaling:** Thread retrieval works across multiple app instances
7. ✅ **Performance:** Thread lookup completes in <100ms (indexed query)

---

## Troubleshooting

### Issue: New Thread Created After Cookie Clear
**Cause:** `GetUserKey()` returning different value
**Solution:** Check if `PersistUserKey` is being properly restored from session

### Issue: Database Query Fails
**Cause:** Connection string or table doesn't exist
**Solution:** Run the CreateAgentThreadsTable.sql script and verify table exists

### Issue: Thread Not Found in Database
**Cause:** `EnsureThreadIdPersistedAsync` not being called or failed silently
**Solution:** Check for exceptions in `SaveChangesAsync(ct)` calls; add logging

### Issue: Multiple Instances Show Different Threads
**Cause:** Session cache not distributed across instances
**Solution:** Verify Azure App Service is using external session store or configure sticky sessions

---

## Performance Considerations

- **Thread Lookup:** Indexed on `UserKey`, should complete in <50ms
- **Thread Update:** `LastSeenUtc` update on every message, use efficient filtering
- **Database Roundtrips:** 1-2 per conversation start, 1 per `TouchLastSeen` call
- **Suggested Optimization:** Cache thread lookup for 5 minutes to reduce DB queries

