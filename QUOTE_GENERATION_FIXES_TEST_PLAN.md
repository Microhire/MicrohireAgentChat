# Quote Generation Fixes - Test Plan

This document outlines comprehensive test scenarios to verify that all quote generation issues have been properly fixed.

## Test Environment Setup

**Prerequisites:**
- Application running in development or staging environment
- Access to application logs
- Clean browser session (clear cookies/session)

**Monitoring:**
- Watch application logs for `[QUOTE FLOW]` and `[QUOTE GEN]` entries
- Monitor session state for `Draft:QuoteComplete`, `Draft:QuoteUrl`, `Draft:QuoteTimestamp`

---

## Test Case 1: Normal Flow - First Time Quote Generation

**Objective:** Verify quote generates successfully on first attempt without errors

**Steps:**
1. Start fresh conversation
2. Provide all required information:
   - Full name: "Test User"
   - Email: "test@example.com"
   - Organisation: "Test Company"
   - Event date: [future date]
   - Venue: "Westin at Brisbane"
   - Attendees: 12
3. Select schedule times using time picker
4. Provide AV requirements:
   - "We need 2 microphones for presenters"
   - "We'll be showing slides"
5. Complete AV / wizard steps until equipment is saved (no long "Quote Summary" card expected)
6. Send explicit consent to generate the quote, e.g. type **"yes create quote"** (pre-quote Yes/No buttons are removed)

**Expected Results:**
- ✅ Quote generates successfully on first attempt
- ✅ Single success message: "Great news! I've successfully generated your quote for booking [BOOKING_NO]"
- ✅ "View Quote" link appears
- ✅ Clicking link opens quote HTML document
- ✅ Quote includes all mentioned equipment (microphones, projector, screen)
- ✅ No confusing error messages
- ✅ No "sales team will follow up" messages

**Logs to verify:**
```
[QUOTE FLOW] Decision point: ... LooksLikeConsent=True, WasQuoteGenerationPrompt=True/False, IsConsentToSummary=True
[QUOTE GEN] Starting HTML quote generation for booking [BOOKING_NO]
[QUOTE GEN] Quote generated successfully for booking [BOOKING_NO]: File=Quote-[BOOKING_NO]-*.html
```

---

## Test Case 2: Idempotency - Multiple "Yes Create Quote" Attempts

**Objective:** Verify system doesn't regenerate quote when user says "yes create quote" multiple times

**Steps:**
1. Complete Test Case 1 successfully
2. Without refreshing, type "yes create quote" again in chat
3. Wait for response
4. Type "create the quote" one more time
5. Wait for response

**Expected Results:**
- ✅ System returns existing quote immediately
- ✅ Same quote URL as original (no new file created)
- ✅ Success message appears each time: "Great news! I've successfully generated your quote for booking [BOOKING_NO]"
- ✅ No duplicate quote files generated
- ✅ No "generating..." or processing messages
- ✅ Response is instant (uses existing quote)

**Logs to verify:**
```
[QUOTE FLOW] Decision point: ... QuoteComplete=True, ExistingQuoteUrl=/files/quotes/Quote-[BOOKING_NO]-*.html
Quote already exists for booking [BOOKING_NO], returning existing quote
```

**File system check:**
- Only ONE quote file exists for the booking number: `Quote-[BOOKING_NO]-[timestamp].html`
- No multiple files with different timestamps

---

## Test Case 3: Context Memory - Teams Call Equipment

**Objective:** Verify AI remembers requirements mentioned early in conversation (especially video conferencing)

**Steps:**
1. Start fresh conversation
2. Provide contact info and event details
3. **Early mention:** "We'll be having a Teams call with remote attendees"
4. Continue conversation, select schedule
5. When asked about AV requirements, mention: "We'll need 2 microphones for the in-room presenters"
6. Review equipment recommendation

**Expected Results:**
- ✅ Before showing recommendation, AI outputs summary: "Based on our conversation, I've noted the following requirements: [lists all]"
- ✅ AI mentions Teams call was mentioned
- ✅ Equipment recommendation includes:
  - Camera (for Teams call)
  - Microphone (for presenters + Teams)
  - Speakers (to hear remote attendees)
  - Display/projector (to show remote participants)
  - 2 microphones (as requested)
- ✅ AI asks: "Have I captured everything you mentioned? Let me know if anything is missing."
- ✅ Quote includes all equipment

**Context validation logs:**
```
Context validation found potential missing equipment: [warnings if any]
```

**What NOT to see:**
- ❌ Missing camera for Teams call
- ❌ Missing speakers for audio playback
- ❌ Quote summary without explicit confirmation of requirements

---

## Test Case 4: Context Memory - Recording Mentioned

**Objective:** Verify camera is included when recording is mentioned

**Steps:**
1. Start fresh conversation
2. Provide contact and event info
3. Early in conversation: "We'd like to record the presentations"
4. Later, when asked about AV: "We need a laptop and projector"
5. Review equipment recommendation

**Expected Results:**
- ✅ AI explicitly lists: "User mentioned recording" in summary
- ✅ Equipment includes camera for recording
- ✅ Equipment includes laptop and projector as requested
- ✅ AI confirms all requirements before proceeding

---

## Test Case 5: Missing Information Handling

**Objective:** Verify system prevents quote generation when required info is missing

**Steps:**
1. Start fresh conversation
2. Skip providing contact information
3. Try to jump ahead: "I need a quote for 2 microphones"
4. Wait for AI response

**Expected Results:**
- ✅ AI politely requests missing information: "I need to collect [specific fields] before proceeding"
- ✅ No confusing error messages
- ✅ No "technical issue" or "having trouble" messages
- ✅ Clear guidance on what's needed
- ✅ AI specifies: "customer name", "contact email or phone number", "organisation name"

**What NOT to see:**
- ❌ "There seems to be an issue"
- ❌ "Sales team will follow up"
- ❌ "Technical difficulties"
- ❌ "Unable to generate quote"

---

## Test Case 6: Price Display Messaging

**Objective:** Verify AI correctly describes price visibility

**Steps:**
1. Complete normal quote flow
2. After quote is generated, ask: "What's the price?"
3. Wait for response

**Expected Results:**
- ✅ AI responds with approved message:
  - "Your detailed quote with all pricing will be available in the generated document."
  - OR "The quote document will include a full breakdown of costs."
- ✅ AI does NOT say: "The price is $X" or "total is $Y"
- ✅ AI does NOT say: "Price is hidden in chat"

---

## Test Case 7: Session Recovery After Refresh

**Objective:** Verify system finds existing quote after page refresh

**Steps:**
1. Complete Test Case 1 successfully (quote generated)
2. Note the booking number
3. Refresh the browser page
4. Type: "Can I see my quote again?"

**Expected Results:**
- ✅ System finds existing quote file from disk
- ✅ Returns existing quote URL
- ✅ Quote link still works
- ✅ No regeneration attempted

**Logs to verify:**
```
Found existing quote file for booking [BOOKING_NO] (age: [X] seconds): /files/quotes/Quote-[BOOKING_NO]-*.html
```

---

## Test Case 8: Quote File Freshness Check

**Objective:** Verify system reuses recent quotes but allows regeneration of old ones

**Steps:**
1. Manually create old quote file (modify timestamp to be > 1 hour old)
2. Start conversation and complete quote flow for same booking
3. Observe behavior

**Expected Results:**
- ✅ System detects quote is > 1 hour old
- ✅ Generates new quote instead of reusing old one
- ✅ New quote file created with current timestamp

---

## Test Case 8a: Schedule Not Recognised After Time Picker (Fix Verification)

**Objective:** Verify the fix for the bug where the agent asked for the event schedule again after the user had already submitted it via the time picker (e.g. "It seems there might be an issue with the system recognising your schedule").

**Steps:**
1. Start fresh conversation
2. Provide contact info: full name, new customer, organisation, email/phone
3. Provide event: boardroom meeting at Westin Brisbane, 6 people, 11 March 2026 (or another future date)
4. When the time picker appears, submit schedule: e.g. Setup 7:00 AM, Rehearsal 9:30 AM, Event 10:00 AM–4:00 PM, Pack up 6:00 PM
5. Confirm AV requirements: boardroom style, 2 presenters with slides, Mac laptop, need audio
6. When the agent summarises equipment and asks "Have I captured everything?", reply: "yep all good"
7. When requirements are complete, send **"yes create quote"** (or wait for structured wizard to finish and quote success if the server generates without that turn)

**Expected Results:**
- ✅ After step 4 the agent confirms the schedule and continues to AV requirements (no repeat "please confirm your schedule")
- ✅ After step 6 the agent does **not** say "It seems there might be an issue with the system recognising your schedule" or ask to confirm times again
- ✅ After step 7 the quote is generated (or attempted) without injecting "I need your event schedule before I can create the quote"
- ✅ No second time picker or "please use the time picker" message once schedule was already submitted

**What NOT to see:**
- ❌ "It seems there might be an issue with the system recognising your schedule"
- ❌ "I need your event schedule before I can create the quote" (when schedule was already submitted via time picker)
- ❌ Time picker shown again after step 4

**Replay scenario:** Use the "Schedule Recognition Fix" bug reproduction scenario in the UI to replay the same flow (contact → event → time picker submit → AV → "yep all good" → "yes create quote") and confirm the same expected results.

---

## Test Case 9: Error Message Compliance

**Objective:** Verify AI never uses banned error phrases

**Steps:**
1. Monitor all conversations for banned phrases
2. Intentionally cause various failure scenarios (if possible in test environment)

**Banned phrases to watch for (should NEVER appear):**
- ❌ "technical issue"
- ❌ "technical hiccup"
- ❌ "system issue"
- ❌ "having trouble"
- ❌ "unable to generate"
- ❌ "can't generate"
- ❌ "sales team will follow up"
- ❌ "there seems to be"
- ❌ "let me try again"

**Approved phrases only:**
- ✅ "I need to collect [specific info] before proceeding"
- ✅ "Great news! I've successfully generated your quote"
- ✅ "Could you please provide [specific field]?"

---

## Test Case 10: Comprehensive Multi-Requirement Test

**Objective:** Test complex scenario with multiple requirements mentioned at different times

**Steps:**
1. Start conversation: "Hi, we're planning a hybrid meeting with Teams"
2. Provide contact info
3. Continue: "We'll have 3 speakers presenting"
4. Mention: "We want to record it for our archive"
5. Add: "There will be videos with sound"
6. Provide venue, date, attendees
7. Select schedule times
8. When asked about equipment needs, say: "I think I've already told you everything"
9. Review equipment recommendation

**Expected Results:**
- ✅ AI explicitly summarizes: "Based on our conversation, you mentioned:"
  - Hybrid meeting with Teams → camera, mic, speakers, display
  - 3 speakers → 3 microphones
  - Recording → camera (if not already included)
  - Videos with sound → projector, screen, speakers
- ✅ AI asks for confirmation before generating recommendation
- ✅ Quote includes ALL equipment:
  - Camera
  - 3 microphones
  - Speakers
  - Projector + screen
- ✅ No equipment missing

---

## Test Case 11: Logging Verification

**Objective:** Verify comprehensive logging is working

**Steps:**
1. Run any test case above
2. Monitor application logs throughout

**Expected log entries:**
```
[QUOTE FLOW] Decision point: UserText='...', LooksLikeConsent=True/False, WasSummaryAsk=True/False, IsConsentToSummary=True/False, ExistingBookingNo=..., QuoteComplete=..., ExistingQuoteUrl=...

[QUOTE GEN] Starting HTML quote generation for booking [BOOKING_NO] at [timestamp]

[QUOTE GEN] Loaded booking [BOOKING_NO]: ContactID=..., CustID=..., VenueID=..., Date=...

[QUOTE GEN] Quote generated successfully for booking [BOOKING_NO]: File=..., HtmlLength=... chars, ElapsedMs=...

Quote state synchronized for booking [BOOKING_NO]: QuoteUrl=..., QuoteComplete=1, Timestamp=...
```

**Verify:**
- ✅ All decision points are logged
- ✅ Timing information included
- ✅ State synchronization logged
- ✅ Errors include context and timing

---

## Success Criteria Summary

**All test cases must pass with these outcomes:**

### Core Functionality
- ✅ Quote generates successfully on first attempt
- ✅ Quote URL is valid and accessible
- ✅ Quote HTML contains all equipment mentioned

### Idempotency
- ✅ Multiple "yes create quote" requests return same quote
- ✅ No duplicate quote files generated
- ✅ Instant response when quote exists

### Context Memory
- ✅ Equipment mentioned early in conversation is included
- ✅ Teams/Zoom → camera + mic + speakers + display
- ✅ Recording → camera
- ✅ Videos with sound → speakers
- ✅ AI explicitly confirms requirements before generating

### Error Handling
- ✅ Clear messages about missing information
- ✅ No banned error phrases used
- ✅ No improvised error messages
- ✅ Approved templates only

### Price Display
- ✅ AI correctly describes prices are in document, not chat
- ✅ No mention of specific prices in chat
- ✅ No confusing statements about price visibility

### State Management
- ✅ Quote state synchronized across session and file system
- ✅ Session recovery works after refresh
- ✅ Timestamp tracking functional

### Logging
- ✅ All decision points logged
- ✅ Quote generation timing tracked
- ✅ State changes logged
- ✅ Errors include full context

---

## Regression Test Checklist

Run these checks after ANY code changes:

- [ ] Test Case 1: Normal flow works
- [ ] Test Case 2: Idempotency works
- [ ] Test Case 3: Context memory for Teams call
- [ ] Test Case 5: Missing info handled gracefully
- [ ] Test Case 6: Price messaging correct
- [ ] Test Case 11: Logging present

---

## Known Issues Fixed

This test plan verifies fixes for these specific issues from the screenshots:

1. **Back and forth quote generation** → Fixed by idempotency checks
2. **Missing camera for Teams call** → Fixed by context memory validation
3. **"Quote could not be created" switching to success** → Fixed by error message templates
4. **"Price is hidden in chat"** → Fixed by price display rules
5. **Multiple failed attempts** → Fixed by consolidated quote generation path

---

## Test Execution Log

Record results for each test run:

| Test Case | Date | Tester | Pass/Fail | Notes |
|-----------|------|--------|-----------|-------|
| TC1: Normal Flow | | | | |
| TC2: Idempotency | | | | |
| TC3: Context Memory (Teams) | | | | |
| TC4: Context Memory (Recording) | | | | |
| TC5: Missing Info | | | | |
| TC6: Price Display | | | | |
| TC7: Session Recovery | | | | |
| TC8: Quote Freshness | | | | |
| TC9: Error Messages | | | | |
| TC10: Multi-Requirement | | | | |
| TC11: Logging | | | | |

---

## Failure Investigation Guide

If any test fails, check:

1. **Application logs** - Look for `[QUOTE FLOW]` and `[QUOTE GEN]` entries
2. **Session state** - Inspect `Draft:QuoteComplete`, `Draft:QuoteUrl`, `Draft:QuoteTimestamp`
3. **File system** - Check `wwwroot/files/quotes/` for quote files
4. **Agent instructions** - Verify `AgentToolInstaller.cs` was updated
5. **Database** - Confirm booking record exists with correct equipment items

**Common Issues:**
- **Context memory fails** → Check `ValidateEquipmentContextFromConversation()` in `AgentToolHandlerService.cs`
- **Idempotency fails** → Check session flags and file system checks in `GenerateQuoteForBookingAsync()`
- **Wrong error messages** → Check agent instructions in `AgentToolInstaller.cs`
- **Missing equipment** → Check context validation warnings in logs
