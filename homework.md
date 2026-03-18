# Event time picker input issue
_Exported on 2/4/2026 at 23:45:19 GMT+5:30 from Cursor (2.4.28)_

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

