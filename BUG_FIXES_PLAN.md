# Bug Fixes Plan - Microhire Agent Chat

## Overview
This document outlines 8 major bugs identified from user feedback screenshots, along with root cause analysis and proposed fixes.

## Implementation Status

### ✅ COMPLETED (4 bugs fixed - 50%)
- **Bug #1:** Schedule Time Parsing Error - ✅ FIXED
- **Bug #2:** Incorrect Screen/Projector Assumption - ✅ FIXED
- **Bug #3:** Case Sensitivity Issue - Teams Recognition - ✅ FIXED
- **Bug #4:** Organization Name Parsing Error - ✅ FIXED

### ⏳ REMAINING (4 bugs to fix - 50%)
- **Bug #5:** Not Acknowledging User Questions - ⏳ PENDING (High Priority)
- **Bug #6:** Asking Redundant Questions - ⏳ PENDING (High Priority)
- **Bug #7:** Multi-Day Event Tracking - ⏳ PENDING (Lower Priority)
- **Bug #8:** Not Acknowledging All Information - ⏳ PENDING (High Priority)

### Implementation Summary
**Last Updated:** 2026-01-16
**Progress:** 4 of 8 bugs completed (50%)
**Build Status:** ✅ All changes compile successfully, no linter errors
**Next Session Focus:** Bugs #5, #6, #8 (High Priority UX issues)

---

## Bug #1: Schedule Time Parsing Error - "Start 9am" Misinterpretation ✅ FIXED

### Status
**✅ COMPLETED** - Fixed on 2026-01-16

### Implementation Details
1. **Added `ExtractEventTimesWithContext` method** in `AzureAgentChatService.cs`:
   - Distinguishes between event start ("start 9am"), event end ("finish 5pm"), and rehearsal times
   - Handles relative time parsing: "1 hour before" calculates from event start
   - Defaults rehearsal to 1 hour before event start if not specified

2. **Enhanced `ExtractScheduleTimes`** in `ConversationExtractionService.cs`:
   - Added patterns for "Start", "End", "Finish" with semantic context
   - Improved pattern matching for event start vs rehearsal

3. **Updated Agent Instructions** in `AgentToolInstaller.cs`:
   - Added TIME INTERPRETATION RULES section
   - Clear rules: "start [time]" = event start, not rehearsal
   - Defaults: rehearsal 1hr before start, setup 2hrs before, pack-up 2hrs after

### Files Modified
- `MicrohireAgentChat/Services/AzureAgentChatService.cs` (lines ~1103-1160)
- `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (lines ~335-342)
- `MicrohireAgentChat/Services/AgentToolInstaller.cs` (lines ~222-227)

### Problem
**User Input:** "start 9am and finish 5, allow for 1 hour before for rehearsal"

**AI Response:** 
- Setup Time: 8:00 AM
- Rehearsal Time: 9:00 AM  
- Event Start Time: 10:00 AM
- Event End Time: 5:00 PM

**Issue:** The AI incorrectly interpreted "start 9am" as the rehearsal time instead of the event start time. The user clearly meant the event starts at 9am, with rehearsal 1 hour before (8am).

### Root Cause
The schedule parsing logic in `ChatController.cs` and `ConversationExtractionService.cs` doesn't properly distinguish between:
- Event start time ("start 9am")
- Rehearsal time ("rehearsal at 9am" or "1 hour before")
- Setup time

The parser likely defaults to common patterns without understanding the semantic meaning of "start" vs "rehearsal".

### Proposed Fix
1. **Enhance time parsing in `ChatController.cs`** (`TryCaptureScheduleSelection` method):
   - Add explicit patterns for "start" and "finish" keywords
   - When "start [time]" is detected, set that as event start time
   - When "rehearsal" or "1 hour before" is mentioned, calculate rehearsal relative to start time
   - When "finish" or "end" is mentioned, set that as event end time

2. **Update `ConversationExtractionService.ExtractScheduleTimes`**:
   - Add pattern: `@"(?:event\s+)?start(?:s|ing)?\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)"`
   - Add pattern: `@"(?:event\s+)?(?:finish|end)(?:es|ing)?\s+(\d{1,2}(?::\d{2})?\s*(?:AM|PM)?)"`
   - Improve relative time parsing: "1 hour before" should calculate from event start

3. **Files to Modify:**
   - `MicrohireAgentChat/Controllers/ChatController.cs` (lines ~167-194)
   - `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (lines ~327-379)
   - `MicrohireAgentChat/Services/AzureAgentChatService.cs` (lines ~1043-1087)

---

## Bug #2: Incorrect Screen/Projector Assumption ✅ FIXED

### Status
**✅ COMPLETED** - Fixed on 2026-01-16

### Implementation Details
1. **Updated Agent Instructions** in `AgentToolInstaller.cs`:
   - Added EQUIPMENT RECOMMENDATION RULES section
   - Explicit rule: "NEVER assume that users bringing their own laptops means they don't need screens/projectors"
   - Rule: "NEVER say screens/projectors are not required just because users bring laptops"
   - Only exclude if user explicitly says "no screen", "don't need projector", "no display needed"
   - Always ask about display needs for presentations regardless of laptop ownership

2. **No code changes needed** - The fix was primarily in agent behavior/instructions, as the underlying logic was already correct but the agent was making incorrect inferences.

### Files Modified
- `MicrohireAgentChat/Services/AgentToolInstaller.cs` (lines ~228-234)

### Problem
**User Input:** Mentions audio, bringing own laptop, no operator, 8 people

**AI Response:** "Any screen or projector is not required as your provisions already satisfy the setup."

**User Feedback:** "nothing said about not needing a screen"

**Issue:** The AI incorrectly inferred that no screen/projector is needed when the user never stated this. The user only mentioned bringing their own laptop, which doesn't imply they don't need a screen/projector.

### Root Cause
The equipment recommendation logic in `SmartEquipmentRecommendationService.cs` or `EquipmentSearchService.cs` is making assumptions based on incomplete information. When a user mentions "bringing own laptop", the system incorrectly infers they don't need display equipment.

### Proposed Fix
1. **Update equipment inference logic:**
   - Remove automatic negative inference for screens/projectors when laptop is mentioned
   - Only exclude equipment when explicitly stated: "no screen", "don't need projector", etc.
   - When user brings own laptop, still ask about display needs or recommend based on event type

2. **Enhance `EquipmentSearchService.ParseUserRequirements`:**
   - Add explicit negative pattern detection: `@"(?:no|don't need|not|without)\s+(screen|projector|display)"`
   - Only exclude equipment if negative pattern is found
   - Default to recommending screen/projector for presentations unless explicitly excluded

3. **Update `SmartEquipmentRecommendationService`:**
   - Remove logic that assumes "no screen needed" when laptop is provided by user
   - Add explicit check for negative statements before excluding equipment

4. **Files to Modify:**
   - `MicrohireAgentChat/Services/SmartEquipmentRecommendationService.cs` (lines ~260-314)
   - `MicrohireAgentChat/Services/EquipmentSearchService.cs` (lines ~150-231)
   - `MicrohireAgentChat/Services/AzureAgentChatService.cs` (lines ~3779-3818)

---

## Bug #3: Case Sensitivity Issue - "teams" vs "Teams" Meeting Recognition ✅ FIXED

### Status
**✅ COMPLETED** - Fixed on 2026-01-16

### Implementation Details
1. **Enhanced `ParseUserRequirements`** in `EquipmentSearchService.cs`:
   - Added case-insensitive Microsoft Teams recognition patterns
   - Detects: "teams", "microsoft teams", "ms teams", "video conference", "video conferencing"
   - Automatically recommends required equipment when Teams is mentioned:
     - Laptop (if not already provided)
     - Webcam
     - Microphone
     - Speaker
   - Note: Internet connection is assumed to be available

2. **Pattern Matching:**
   - Uses `StringComparison.OrdinalIgnoreCase` for case-insensitive matching
   - Handles variations: "teams meeting", "microsoft teams", "ms teams"

### Files Modified
- `MicrohireAgentChat/Services/EquipmentSearchService.cs` (lines ~210-263)

### Problem
**User Input:** "I'd like to run a teams meeting as well"

**AI Response:** "It seems there isn't a specific equipment match for 'teams meeting' in the system."

**User Feedback:** "Fault in the system not being able to differentiate between 'teams' and 'Teams'."

**Issue:** The system doesn't recognize "teams" (lowercase) as referring to Microsoft Teams, a common video conferencing platform. This is a case-sensitivity issue.

### Root Cause
The equipment search or keyword matching logic doesn't normalize "teams" to recognize it as Microsoft Teams. The system likely does exact or case-sensitive matching.

### Proposed Fix
1. **Add Microsoft Teams recognition:**
   - Create a normalization function that maps "teams" (case-insensitive) to "Microsoft Teams" or "video conferencing"
   - Add to equipment type recognition: `case "teams": case "microsoft teams":`

2. **Update `EquipmentSearchService.ParseUserRequirements`:**
   - Add pattern: `@"\b(teams|microsoft\s+teams|ms\s+teams)\s+meeting\b"` (case-insensitive)
   - Map to video conferencing equipment requirements (webcam, microphone, speaker, internet)

3. **Enhance `SmartEquipmentRecommendationService`:**
   - Add case for "teams" or "microsoft teams" that recommends:
     - Laptop (if not already provided)
     - Webcam
     - Microphone/Speaker system
     - Internet connection note

4. **Files to Modify:**
   - `MicrohireAgentChat/Services/EquipmentSearchService.cs` (lines ~150-231)
   - `MicrohireAgentChat/Services/SmartEquipmentRecommendationService.cs` (lines ~260-314)
   - Consider adding to `AgentToolInstaller.cs` instructions for Teams recognition

---

## Bug #4: Organization Name Parsing Error - "Called THE gully" ✅ FIXED

### Status
**✅ COMPLETED** - Fixed on 2026-01-16

### Implementation Details
1. **Enhanced `CleanOrgName` method** in `ConversationExtractionService.cs`:
   - Added verb prefix removal: "called", "named", "is", "are", "company is", "business is", etc.
   - Added regex pattern to strip leading verbs before parsing
   - Handles: "Called THE gully" → "THE gully"

2. **Enhanced `CleanupOrgLeft` method** in `AzureAgentChatService.cs`:
   - Added leading verb stripping using regex
   - Removes: "called", "named", "is", "are", "company is", "business is", "organisation is", "organization is", etc.

3. **Added preprocessing** in both `TryParseOrgAddress` methods:
   - Strips leading verbs from entire text before parsing
   - Handles cases like "Called THE gully, located in winston glades"
   - Pattern: `@"^(?:called|named|is|are|company\s+is|business\s+is|organisation\s+is|organization\s+is|org\s+is|the\s+company\s+is|the\s+business\s+is)\s+(.+)$"`

### Files Modified
- `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (lines ~969-984, ~772-779)
- `MicrohireAgentChat/Services/AzureAgentChatService.cs` (lines ~3698-3707, ~3608-3615)

### Problem
**User Input:** "Jim Micro, new customer, Called THE gully, and located in winston glades. 0412564733, Jim@Micro.com, Supervisor"

**AI Response:** Organization: "Called THE Gully"

**User Feedback:** "Failed to recognise the business is called 'THE gully' not 'Called THE gully'. Could be case sensitive."

**Issue:** The parser incorrectly included the word "Called" as part of the organization name. The organization should be "THE gully", not "Called THE gully".

### Root Cause
The `TryParseOrgAddress` method in `ConversationExtractionService.cs` or `AzureAgentChatService.cs` doesn't properly strip leading verbs like "Called", "Named", "Is", etc. before extracting the organization name.

### Proposed Fix
1. **Update `TryParseOrgAddress` method:**
   - Add preprocessing to remove leading verbs: "called", "named", "is", "are", "company is", "business is", etc.
   - Pattern: `@"^(?:called|named|is|are|company\s+is|business\s+is|organisation\s+is|organization\s+is)\s+(.+)$"` (case-insensitive)
   - Strip these prefixes before parsing organization name

2. **Enhance organization name cleaning:**
   - Update `CleanupOrgLeft` function to handle more verb patterns
   - Add to existing regex: `@"^(?:called|named|is|are|company\s+is|business\s+is)\s+(.+)$"`

3. **Files to Modify:**
   - `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (lines ~752-871, specifically `TryParseOrgAddress`)
   - `MicrohireAgentChat/Services/AzureAgentChatService.cs` (lines ~3534-3600, `TryParseOrgAddress` method)

---

## Bug #5: Not Acknowledging User Questions - Room Setup Suggestions

### Problem
**User Input:** "Meeting with 12 people, at the westin in the Thive room. the date is the 29th of jan 2028. What is suggested for the thrive room, what is optimal"

**AI Response:** Shows time picker interface instead of answering the question about room setup suggestions.

**User Feedback:** "Did not acknowledge what was said. When I asked what is suggested in response for number 5 in the first set of questions it asked regarding the event details."

**Issue:** The AI ignored the user's question about room setup suggestions and jumped straight to showing the time picker. The user asked a direct question that should be answered first.

### Root Cause
The agent flow in `AgentToolInstaller.cs` or the orchestration logic prioritizes collecting schedule information over answering user questions. The system doesn't detect when a user is asking a question vs. providing information.

### Proposed Fix
1. **Add question detection logic:**
   - Detect question patterns: "what is", "what are", "how should", "what's suggested", "what's optimal", etc.
   - When a question is detected, answer it before moving to next step in flow

2. **Update agent instructions in `AgentToolInstaller.cs`:**
   - Add rule: "If user asks a question, answer it immediately before proceeding with data collection"
   - Add rule: "When user asks about room setup suggestions, call `list_westin_rooms` or `get_room_images` to provide recommendations"

3. **Enhance room recommendation:**
   - When user asks about room setup for a specific room, provide:
     - Optimal setup style for that room
     - Capacity information
     - Equipment recommendations for that room size

4. **Files to Modify:**
   - `MicrohireAgentChat/Services/AgentToolInstaller.cs` (add question handling rules)
   - Consider adding a question detection service or method
   - Update agent prompt to prioritize answering questions

---

## Bug #6: Asking Redundant Questions - Already Answered Information

### Problem
**User Input:** "Running interviews for top-tier chefs"

**AI Response:** Asks "can you confirm the event type (e.g., interviews)?"

**User Feedback:** "Asking me questions I have already answered - inefficient." "NOTE: Interview for Interview!"

**Issue:** The AI asks for confirmation of information the user already provided. The user clearly stated "Running interviews" but the AI still asks to confirm the event type.

### Root Cause
The system doesn't maintain a proper memory/state of what information has already been provided. The agent asks questions from a template without checking if the information was already given in the conversation.

### Proposed Fix
1. **Implement conversation state tracking:**
   - Create a `ConversationState` class that tracks:
     - Event type (extracted vs. confirmed)
     - Contact info (extracted vs. confirmed)
     - Equipment needs (mentioned vs. confirmed)
     - Dates, venues, attendees (extracted vs. confirmed)

2. **Update extraction before asking questions:**
   - Before asking "What is the event type?", first call `ExtractEventType` from conversation
   - If event type is found, confirm it instead of asking: "I see you mentioned interviews. Is that correct?"
   - Only ask if nothing was found

3. **Enhance `ConversationExtractionService`:**
   - Add `ExtractEventType` method that looks for event type keywords
   - Add `HasInformationBeenProvided` method to check if info exists before asking

4. **Update agent flow:**
   - Modify `AgentToolInstaller.cs` instructions to:
     - "Before asking for event type, check if user already mentioned it"
     - "If found, confirm it instead of asking again"
     - "Only ask questions for missing information"

5. **Files to Modify:**
   - `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (add event type extraction)
   - `MicrohireAgentChat/Services/AgentToolInstaller.cs` (update flow instructions)
   - Consider creating `ConversationStateService.cs` for state management

---

## Bug #7: Not Tracking Multi-Day Event Details Properly

### Problem
**User Input:** Mentions "banquet style on the second day in the evening after 2pm" for a 3-day event

**AI Response:** "After the banquet-style dinner on the first day, we can certainly switch back to a classroom setup for the third day."

**User Feedback:** "The banquet style was actually for the second day not the first. Not keeping proper track."

**Issue:** The AI incorrectly tracked which day has which setup. The user said banquet on day 2, but AI said it was on day 1.

### Root Cause
The system doesn't have proper data structures or logic to track multi-day events with different setups per day. The booking system may only support single-day setups, or the extraction logic doesn't parse day-specific information.

### Proposed Fix
1. **Create multi-day event tracking:**
   - Add `MultiDayEventDetails` class to track:
     - Day number (1, 2, 3, etc.)
     - Setup style per day
     - Equipment needs per day
     - Schedule per day

2. **Enhance date/day parsing:**
   - Add patterns to extract day references: "first day", "second day", "day 1", "day 2", "on day 3", etc.
   - Map these to specific dates in multi-day events

3. **Update extraction service:**
   - Add `ExtractMultiDayEventDetails` method
   - Parse patterns like: "banquet style on the second day", "classroom setup for day 1", etc.
   - Store as dictionary: `Dictionary<int, DayEventDetails>`

4. **Update booking creation:**
   - Modify booking creation to handle multiple days with different setups
   - May need to create multiple booking records or extend schema

5. **Enhance agent instructions:**
   - Update `AgentToolInstaller.cs` to:
     - "When user mentions multi-day events, track setup style per day"
     - "Confirm day-specific details: 'So day 1 is classroom, day 2 is banquet, day 3 is classroom?'"

6. **Files to Modify:**
   - `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (add multi-day parsing)
   - `MicrohireAgentChat/Models/` (create `MultiDayEventDetails.cs`)
   - `MicrohireAgentChat/Services/Orchestration/BookingOrchestrationService.cs` (handle multi-day)
   - `MicrohireAgentChat/Services/AgentToolInstaller.cs` (update instructions)

---

## Bug #8: Not Acknowledging All Information in User Messages

### Problem
**User Input:** Comprehensive message with budget ($20,000), event type (3 day special), location (Westin Brisbane, Elevate room), dates (March 25-27, 2028), attendees (25 people), setup (classroom), and special request (banquet style on second day after 2pm)

**AI Response:** Only shows time picker for March 25, 2028, ignoring all other information.

**User Feedback:** "Did not acknowledge everything else that was said. Reading error perhaps?"

**Issue:** The AI only responded to one aspect (schedule) and ignored all other provided information (budget, multi-day, location, attendees, setup changes).

### Root Cause
The agent prioritizes schedule collection over acknowledging and processing all provided information. The system may be too focused on the next step in the flow rather than processing the entire message.

### Proposed Fix
1. **Implement comprehensive message processing:**
   - Before responding, extract ALL information from user message:
     - Budget
     - Event type
     - Dates (all dates for multi-day)
     - Location/venue
     - Attendees
     - Setup requirements
     - Special requests

2. **Add acknowledgment step:**
   - After processing message, acknowledge all extracted information:
     - "Thank you! I've noted: 3-day event, $20,000 budget, 25 attendees, Westin Brisbane Elevate room, March 25-27, classroom setup with banquet on day 2. Now let me confirm the schedule..."

3. **Update agent instructions:**
   - "Always acknowledge all information provided in user messages before moving to next step"
   - "If user provides multiple pieces of information, extract and confirm all of them"
   - "Don't jump to time picker if other information was also provided"

4. **Enhance extraction service:**
   - Add `ExtractAllEventInformation` method that extracts:
     - Budget (patterns: "$X", "budget of X", "X dollars")
     - All dates mentioned
     - All venue/room information
     - All setup styles
     - All special requests

5. **Files to Modify:**
   - `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (add comprehensive extraction)
   - `MicrohireAgentChat/Services/AgentToolInstaller.cs` (update acknowledgment rules)
   - `MicrohireAgentChat/Services/AzureAgentChatService.cs` (add acknowledgment logic)

---

## Implementation Priority

### ✅ Completed (High & Medium Priority)
1. **Bug #1:** Schedule Time Parsing - ✅ FIXED - Users can't proceed if times are wrong
2. **Bug #2:** Screen/Projector Assumption - ✅ FIXED - Can lead to missing equipment
3. **Bug #3:** Teams Meeting Recognition - ✅ FIXED - Missing common use case
4. **Bug #4:** Organization Name Parsing - ✅ FIXED - Data quality issue

### ⏳ Remaining High Priority (User Experience Critical)
1. **Bug #5:** Not Acknowledging Questions - ⏳ PENDING - Frustrating user experience
2. **Bug #6:** Redundant Questions - ⏳ PENDING - Inefficient and annoying
3. **Bug #8:** Not Acknowledging All Information - ⏳ PENDING - Major UX issue

### ⏳ Remaining Lower Priority (Edge Cases)
4. **Bug #7:** Multi-Day Event Tracking - ⏳ PENDING - Less common but important for complex events

---

## Testing Plan

For each bug fix:
1. Create unit tests with the exact user input from screenshots
2. Verify the expected output matches user expectations
3. Test edge cases and variations
4. Integration testing with full conversation flow
5. User acceptance testing with similar scenarios

---

## Notes

- All fixes should maintain backward compatibility
- Consider adding logging for debugging extraction issues
- May need to update agent prompts/instructions in Azure AI Studio
- Some fixes may require database schema changes (multi-day events)

---

## Next Steps for Remaining Bugs

### Bug #5: Not Acknowledging User Questions
**Priority:** High
**Approach:**
- Add question detection logic to identify when user asks questions vs provides information
- Update agent instructions to prioritize answering questions before data collection
- Consider adding a `QuestionDetectionService` or method to detect question patterns
- When room setup questions are asked, call `list_westin_rooms` or `get_room_images` tools

**Key Files to Modify:**
- `MicrohireAgentChat/Services/AgentToolInstaller.cs` (add question handling rules)
- Consider: `MicrohireAgentChat/Services/QuestionDetectionService.cs` (new file)

### Bug #6: Asking Redundant Questions
**Priority:** High
**Approach:**
- Implement conversation state tracking to remember what information has been provided
- Before asking questions, extract information from conversation first
- If information found, confirm it instead of asking: "I see you mentioned interviews. Is that correct?"
- Only ask questions for missing information

**Key Files to Modify:**
- `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (add `ExtractEventType` method)
- `MicrohireAgentChat/Services/AgentToolInstaller.cs` (update flow instructions)
- Consider: `MicrohireAgentChat/Services/ConversationStateService.cs` (new file for state management)

### Bug #7: Multi-Day Event Tracking
**Priority:** Lower (edge case but important)
**Approach:**
- Create `MultiDayEventDetails` class to track day-specific information
- Add day parsing patterns: "first day", "second day", "day 1", "day 2", "on day 3"
- Map day references to specific dates
- Store as `Dictionary<int, DayEventDetails>` for day-specific setups

**Key Files to Modify:**
- `MicrohireAgentChat/Models/MultiDayEventDetails.cs` (new file)
- `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (add `ExtractMultiDayEventDetails`)
- `MicrohireAgentChat/Services/AgentToolInstaller.cs` (update instructions)
- May need database schema changes for multi-day support

### Bug #8: Not Acknowledging All Information
**Priority:** High
**Approach:**
- Implement comprehensive message processing before responding
- Extract ALL information: budget, event type, dates, location, attendees, setup, special requests
- Add acknowledgment step that lists all extracted information
- Update agent instructions to always acknowledge before moving to next step

**Key Files to Modify:**
- `MicrohireAgentChat/Services/Extraction/ConversationExtractionService.cs` (add `ExtractAllEventInformation`)
- `MicrohireAgentChat/Services/AgentToolInstaller.cs` (update acknowledgment rules)
- `MicrohireAgentChat/Services/AzureAgentChatService.cs` (add acknowledgment logic)

---

## Technical Notes for Next Session

1. **Build Status:** All current changes compile successfully with 0 errors
2. **Code Quality:** All changes follow best practices with proper regex patterns and case-insensitive matching
3. **Testing:** Unit tests should be created for each bug fix using exact user inputs from bug reports
4. **Agent Instructions:** Changes to `AgentToolInstaller.cs` require the agent to be restarted/reloaded to take effect
5. **Backward Compatibility:** All fixes maintain backward compatibility with existing functionality
