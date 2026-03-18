# Event time picker input issue
_Exported on 2/4/2026 at 23:47:04 GMT+5:30 from Cursor (2.4.28)_

---

**User**

Also don't ask for the time as the AI will show the time picker anyway. Look at this conversation, since the AI ask me for time, i provided it but when it shows the time picker, the event start time and end time that I mentioned were not picked up.



---

**Cursor**


Investigating the codebase to find where the AI is prompted to ask for time and how the time picker gets its default values so we can fix both issues.



Tracing where session keys are set and where we extract start/end times from the conversation.



Checking where `build_time_picker` is invoked and how the agent instructions are structured.



Checking how time parsing works for "9am" / "5 pm" and where the build_time_picker handler gets its defaults:



Checking TryParseTimeToken and TryParseSingleTime for "9am" / "5 pm":



Checking when the transcript is available during the build_time_picker tool call and whether the handler can access it:



Checking whether ConversationExtractionService or AzureAgentChatService exposes extraction to the handler:



Creating the plan.


---

**User**

Implement the plan as specified, it is attached for your reference. Do NOT edit the plan file itself.

To-do's from the plan have already been created. Do not create them again. Mark them as in_progress as you work, starting with the first one. Don't stop until you have completed all the to-dos.

---

**Cursor**

I'll start by updating the schedule instructions in `AgentToolInstaller.cs` so the AI stops asking for times and relies on `build_time_picker`, then I'll wire up the extraction and prefill logic in the chat service and tool handler.

First I updated the agent instructions so the time picker is the only way to capture schedule times and the AI is told not to ask for them in natural language; next I'll wire up extraction and prefill logic in the chat service and tool handler so user-stated times appear in the picker.

I’ve extended the extraction logic and time-picker prefill in `AzureAgentChatService` and will now hook the same behaviour into the `build_time_picker` tool handler so both paths respect user-stated times.

### What I changed

- **Stopped the AI asking for times (uses time picker instead)**  
  In `AgentToolInstaller.cs`, the SCHEDULE section now explicitly tells the agent not to ask for event times in natural language and to always use `build_time_picker` once venue and dates are confirmed:

```320:324:MicrohireAgentChat/Services/AgentToolInstaller.cs
                "**GATE CHECK - Before showing time picker, verify you have:**\n" +
                "- [ ] Event dates confirmed by user (only needs to happen once)\n" +
                "- [ ] Venue confirmed\n\n" +
                "### SCHEDULE (internal - do not show this heading or any step number to user) [MUST COMPLETE BEFORE AV REQUIREMENTS]\n" +
                "After venue and dates are confirmed:\n" +
                "- Do NOT ask the user for event \"time\", \"start/end time\", or \"when the event runs\" in natural language. The schedule will always be captured via the time picker.\n" +
                "1. Call build_time_picker with the confirmed date\n" +
                "2. OUTPUT the outputToUser EXACTLY AS-IS so the picker appears\n" +
                "3. Wait for user to submit their schedule times\n\n" +
```

- **Extract “starts at 9am and ends at 5 pm” and pre-fill session**  
  In `AzureAgentChatService.cs`, `ExtractEventTime` now understands phrases like “starts at 9am and ends at 5 pm” and converts them into start/end `TimeSpan`s using the existing time parser, and `MaybeAppendTimePickerAsync` uses those extracted times to set `Draft:StartTime` / `Draft:EndTime` before building the picker:

```1933:1963:MicrohireAgentChat/Services/AzureAgentChatService.cs
        public (TimeSpan? startTime, TimeSpan? endTime, string? matched) ExtractEventTime(IEnumerable<DisplayMessage> messages)
        {
            var ordered = messages.OrderBy(m => m.Timestamp).ToList();
            static string JoinParts(DisplayMessage m) => string.Join(" ", m.Parts ?? Enumerable.Empty<string>());

            // Special-case: phrases like "starts at 9am and ends at 5 pm"
            bool TryParseStartEndPhrase(string text, out TimeSpan? start, out TimeSpan? end, out string? matched)
            {
                start = null;
                end = null;
                matched = null;

                var pattern = @"(?:event\s+)?(?:start|starts|begin|begins|beginning)(?:\s+at)?\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm))(?:(?!\d).)*?(?:event\s+)?(?:end|ends|finish|finishes)(?:\s+at)?\s+(\d{1,2}(?::\d{2})?\s*(?:am|pm))";
                var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (!m.Success)
                    return false;

                string Clean(string s) => Regex.Replace(s, @"\s+", "");

                var startToken = Clean(m.Groups[1].Value);
                var endToken = Clean(m.Groups[2].Value);

                if (TryParseSingleTime(startToken, out var s) && TryParseSingleTime(endToken, out var e))
                {
                    start = s;
                    end = e;
                    matched = m.Value;
                    return true;
                }

                return false;
            }
            // ... existing range-based patterns remain unchanged ...
```

```615:625:MicrohireAgentChat/Services/AzureAgentChatService.cs
            // 5) Send loading message first
            var loadingMessage = "I'm preparing a time picker for your event schedule. One moment please...";
            AgentsClient.Messages.CreateMessage(threadId, Azure.AI.Agents.Persistent.MessageRole.Agent, loadingMessage);

            // 6) Try to pre-fill schedule times from conversation if not already in session
            var session = _http.HttpContext?.Session;
            if (session != null)
            {
                var currentStart = session.GetString("Draft:StartTime");
                var currentEnd = session.GetString("Draft:EndTime");

                if (string.IsNullOrWhiteSpace(currentStart) || string.IsNullOrWhiteSpace(currentEnd))
                {
                    var (startTime, endTime, matchedTimeText) = ExtractEventTime(messages);
                    if (startTime.HasValue)
                    {
                        session.SetString("Draft:StartTime", startTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER: Prefilled Draft:StartTime from conversation match '{Matched}' => {Start}", matchedTimeText, startTime);
                    }
                    if (endTime.HasValue)
                    {
                        session.SetString("Draft:EndTime", endTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER: Prefilled Draft:EndTime from conversation match '{Matched}' => {End}", matchedTimeText, endTime);
                    }
                }
            }

            // 7) Read schedule times from session, fallback to defaults if not found
            var setupTime = session?.GetString("Draft:SetupTime") ?? "07:00";
            var rehearsalTime = session?.GetString("Draft:RehearsalTime") ?? "09:30";
            var startTime = session?.GetString("Draft:StartTime") ?? "10:00";
            var endTime = session?.GetString("Draft:EndTime") ?? "16:00";
            var packupTime = session?.GetString("Draft:PackupTime") ?? "18:00";
```

- **`build_time_picker` tool now also pre-fills from conversation**  
  In `AgentToolHandlerService.cs`, `HandleBuildTimePicker` now reads the transcript, calls `ExtractEventTime`, and writes `Draft:StartTime` / `Draft:EndTime` into session before building the multitime payload, so the picker reflects user-stated times even when invoked explicitly by the agent:

```225:268:MicrohireAgentChat/Services/AgentToolHandlerService.cs
        var titleDateText = normalizedDate.HasValue
            ? normalizedDate.Value.ToString("d MMMM yyyy")
            : (string.IsNullOrEmpty(dateIso) ? "your event" : dateIso);
        title = $"Confirm your schedule for {titleDateText}";
        string? defStart = doc.RootElement.TryGetProperty("defaultStart", out var ds) ? ds.GetString() : "09:00";
        string? defEnd = doc.RootElement.TryGetProperty("defaultEnd", out var de) ? de.GetString() : "10:00";
        int stepMinutes = doc.RootElement.TryGetProperty("stepMinutes", out var sm) && sm.ValueKind == JsonValueKind.Number
                            ? sm.GetInt32() : 30;

        // Try to pre-fill start/end times from the conversation if not already stored
        var session = _http.HttpContext?.Session;
        if (session != null)
        {
            var currentStart = session.GetString("Draft:StartTime");
            var currentEnd = session.GetString("Draft:EndTime");

            if (string.IsNullOrWhiteSpace(currentStart) || string.IsNullOrWhiteSpace(currentEnd))
            {
                var chatService = _http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService;
                if (chatService != null)
                {
                    var (_, messages) = chatService.GetTranscript(threadId);
                    var (startTime, endTime, matchedTimeText) = chatService.ExtractEventTime(messages);

                    if (startTime.HasValue)
                    {
                        session.SetString("Draft:StartTime", startTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER TOOL: Prefilled Draft:StartTime from conversation match '{Matched}' => {Start}", matchedTimeText, startTime);
                    }
                    if (endTime.HasValue)
                    {
                        session.SetString("Draft:EndTime", endTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER TOOL: Prefilled Draft:EndTime from conversation match '{Matched}' => {End}", matchedTimeText, endTime);
                    }
                }
            }
        }

        // Read schedule times from session, fallback to defaults if not found
        var setupTime = session?.GetString("Draft:SetupTime") ?? "07:00";
        var rehearsalTime = session?.GetString("Draft:RehearsalTime") ?? "09:30";
        var startTime = session?.GetString("Draft:StartTime") ?? "10:00";
        var endTime = session?.GetString("Draft:EndTime") ?? "16:00";
        var packupTime = session?.GetString("Draft:PackupTime") ?? "18:00";
```

With these changes, the assistant should no longer ask you for the event time in text; instead it will show the time picker after dates/venue are confirmed, and if you’ve already said something like “starts at 9am and ends at 5 pm”, those times will be used as the initial Event Start/End in the picker.

---

## Test Flows for Time Picker Display Order and Triggering Fixes

### Fix Summary
Two issues were addressed:
1. **Display Order Bug**: The time picker UI was appearing before its introductory "I'm preparing a time picker..." message, disrupting conversation flow.
2. **Inappropriate Triggering**: The time picker was auto-appending even when users asked unrelated follow-up questions (e.g., about room options/layout).

### Changes Made

**File**: `MicrohireAgentChat/Services/AzureAgentChatService.cs` - Method `MaybeAppendTimePickerAsync`

1. **Merged loading message with picker UI** (lines 713-717):
   - Removed separate `CreateMessage` call for loading text
   - Combined preface text and picker JSON into a single assistant message
   - Ensures "I'm preparing a time picker..." always appears above the picker UI

2. **Added user message gating** (lines 618-660):
   - Checks last user message before auto-appending picker
   - Skips picker if user asks non-time-related questions
   - Detects room/layout questions specifically
   - Logs skip reasons for debugging

### Test Flow 1: Room Options Follow-Up (Bug Reproduction)

**Scenario**: User asks about room options instead of answering the time question.

**Steps**:
1. Start a new conversation
2. Provide event details (venue, date, attendees)
3. AI asks: "What time would you like your event to start and end?"
4. User responds: "What are the options for this room?" (or "Can you explain the room layout?")

**Expected Behavior**:
- ✅ **NO time picker appears**
- ✅ AI responds to the room question normally
- ✅ Log shows: `TIME PICKER: Skipping auto-append for room/layout follow-up`
- ✅ Conversation continues naturally without interruption

**Verification**:
- Check browser console/logs for skip message
- Verify no time picker UI appears in chat
- Confirm AI answers the room question

---### Test Flow 2: Normal Time Picker Display (Happy Path)

**Scenario**: User provides schedule information or agrees to set schedule.

**Steps**:
1. Start a new conversation
2. Provide event details (venue, date, attendees)
3. AI asks: "What time would you like your event to start and end?"
4. User responds with one of:
   - "Let's set the schedule now"
   - "The event starts at 9am and ends at 5pm"
   - "I need setup at 8am, start at 10am, end at 4pm"
   - Or any message containing time/schedule keywords**Expected Behavior**:
- ✅ **Single assistant message bubble appears** containing:
  1. "I'm preparing a time picker for your event schedule. One moment please..."
  2. "Perfect! I've confirmed your event date as **[date]**. Here's the time picker for you to confirm your schedule:"
  3. Multi-time picker UI (Setup, Rehearsal, Start, End, Pack Up)
- ✅ Loading text appears **above** the picker UI (never below)
- ✅ If user mentioned times, they are pre-filled in the picker

**Verification**:
- Visual inspection: loading text is above picker
- Check that times mentioned by user appear in picker defaults
- Verify single message bubble (not two separate bubbles)

---

### Test Flow 3: Question Without Time Keywords

**Scenario**: User asks a question that doesn't mention time/schedule.**Steps**:
1. Start a new conversation
2. Provide event details (venue, date, attendees)
3. AI asks: "What time would you like your event to start and end?"
4. User responds with questions like:
   - "How many people can this room hold?"
   - "What equipment is included?"
   - "Can I see photos of the room?"
   - "What's the cancellation policy?"

**Expected Behavior**:
- ✅ **NO time picker appears**
- ✅ AI answers the question normally
- ✅ Log shows: `TIME PICKER: Skipping auto-append because the last user message is a non-time-related question`
- ✅ Time picker will appear later when user actually provides schedule info

**Verification**:
- Check logs for skip message with user question text
- Verify no picker UI appears
- Confirm conversation flow is natural

---

### Test Flow 4: Time-Related Question (Should Trigger)

**Scenario**: User asks a question that mentions time/schedule keywords.

**Steps**:
1. Start a new conversation
2. Provide event details (venue, date, attendees)
3. AI asks: "What time would you like your event to start and end?"
4. User responds with questions like:
   - "What time slots are available?"
   - "Can I schedule setup earlier?"
   - "What's the latest end time?"
   - "I need rehearsal time too, is that possible?"

**Expected Behavior**:
- ✅ **Time picker appears** (user mentioned time/schedule keywords)
- ✅ Loading text appears above picker UI
- ✅ Picker shows with appropriate defaults**Verification**:
- Picker appears in correct order
- Log shows picker was appended (no skip message)

---

### Test Flow 5: Edge Case - Room Question WITH Time Mention

**Scenario**: User asks about room but also mentions time.

**Steps**:
1. Start a new conversation
2. Provide event details (venue, date, attendees)
3. AI asks: "What time would you like your event to start and end?"
4. User responds: "What are the room options? Also, I need the event to start at 9am."

**Expected Behavior**:
- ✅ **Time picker appears** (user mentioned "start at 9am")
- ✅ AI may also answer room question or show picker first
- ✅ Picker pre-fills with 9am start time

**Verification**:
- Picker appears because time keyword detected
- Start time is pre-filled to 9am

---

### Test Flow 6: Visual Order Verification

**Scenario**: Verify the loading message always appears before picker UI.

**Steps**:
1. Trigger time picker through any valid flow (Test Flow 2)
2. Observe the chat UI

**Expected Behavior**:
- ✅ In the **same assistant message bubble**, text appears in this order:
  1. "I'm preparing a time picker for your event schedule. One moment please..."
  2. "Perfect! I've confirmed your event date as **[date]**..."
  3. Time picker UI card
- ✅ **Never** see picker UI above the loading text
- ✅ **Never** see two separate bubbles (one loading, one picker)

**Verification**:
- Screenshot comparison: before fix vs after fix
- DOM inspection: single `.chat-message` element contains both text and UI
- No race conditions or ordering issues

---

### Logging Verification

**Check Application Logs** for these messages:

**When picker is skipped**:
```
TIME PICKER: Skipping auto-append because the last user message is a non-time-related question.
User: [user message text]
AssistantPrompt: [assistant message text]
```

or

```
TIME PICKER: Skipping auto-append for room/layout follow-up.
User: [user message text]
AssistantPrompt: [assistant message text]
```

**When picker is shown**:
```
TIME PICKER: Date validation passed. Proceeding to evaluate time picker trigger for [date]
TIME PICKER: Prefilled Draft:StartTime from conversation match '[matched text]' => [time]
TIME PICKER: Prefilled Draft:EndTime from conversation match '[matched text]' => [time]
```

---

### Automated Test Scenario

A new automated test scenario has been added to verify the fixes:

**Scenario Name**: `time_picker_display_order`  
**Location**: `ConversationReplayService.GenerateTimePickerDisplayOrderBugConversation()`

**How to Run**:
1. Open the chat interface
2. Click "Test Scenarios" button
3. Under "🐛 Bug Reproduction Tests", click "Time Picker Display Order"
4. The conversation will automatically play through the bug scenario

**What It Tests**:
- User asks "what are the options for this room?" after AI asks for schedule
- Verifies time picker does NOT appear (should be skipped)
- User asks "can you explain the room layout?"
- Verifies time picker still does NOT appear
- User finally provides schedule: "the event starts at 9am and ends at 5pm"
- Verifies time picker DOES appear with correct order (loading text above picker)

**Expected Log Output**:
```
TIME PICKER: Skipping auto-append for room/layout follow-up.
User: what are the options for this room?
AssistantPrompt: [AI's schedule question]
```

---

### Regression Testing Checklist

- [ ] Test Flow 1: Room options question does NOT trigger picker
- [ ] Test Flow 2: Normal flow shows picker with correct order
- [ ] Test Flow 3: Non-time questions do NOT trigger picker
- [ ] Test Flow 4: Time-related questions DO trigger picker
- [ ] Test Flow 5: Mixed questions with time keywords trigger picker
- [ ] Test Flow 6: Visual order is always correct (loading text above picker)
- [ ] Automated scenario `time_picker_display_order` passes
- [ ] Logs show appropriate skip/append messages
- [ ] No console errors or UI glitches
- [ ] Existing time extraction/prefill still works (from previous fix)