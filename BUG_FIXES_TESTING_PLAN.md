# Bug Fixes Testing Plan - Microhire Agent Chat

## Overview
This document provides comprehensive testing plans for the 4 bugs that were fixed on 2026-01-16. Each test case uses the exact user inputs from the original bug reports to verify the fixes work correctly.

**Last Updated:** 2026-01-16  
**Status:** Ready for Testing  
**Bugs Covered:** #1, #2, #3, #4

---

## Test Environment Setup

### Prerequisites
1. Application running in development/test environment
2. Access to chat interface
3. Ability to view agent responses and extracted data
4. Database access to verify extracted information (for Bug #4)

### Test Data Requirements
- Test venue: The Westin Brisbane
- Test rooms: Thive, Elevate (or any available test rooms)
- Test dates: Use future dates for availability checks

---

## Bug #1: Schedule Time Parsing Error - "Start 9am" Misinterpretation

### Test Case 1.1: Basic Event Start Time
**User Input:**
```
start 9am and finish 5, allow for 1 hour before for rehearsal
```

**Expected Behavior:**
- Event Start Time: 9:00 AM
- Event End Time: 5:00 PM
- Rehearsal Time: 8:00 AM (1 hour before event start)
- Setup Time: 7:00 AM (default 2 hours before event start)
- Pack Up Time: 7:00 PM (default 2 hours after event end)

**Verification Steps:**
1. Enter the user input in chat
2. Check the schedule summary displayed by the agent
3. Verify rehearsal is at 8:00 AM (not 9:00 AM)
4. Verify event start is at 9:00 AM
5. Verify event end is at 5:00 PM

**Pass Criteria:**
- ✅ Rehearsal time is 8:00 AM (1 hour before 9:00 AM)
- ✅ Event start time is 9:00 AM
- ✅ Event end time is 5:00 PM
- ✅ Times are in chronological order: Setup < Rehearsal < Start < End < Pack Up

---

### Test Case 1.2: Event Start with Explicit Rehearsal Time
**User Input:**
```
event starts at 10am, rehearsal at 9am, finish at 4pm
```

**Expected Behavior:**
- Event Start Time: 10:00 AM
- Rehearsal Time: 9:00 AM
- Event End Time: 4:00 PM

**Verification Steps:**
1. Enter the user input
2. Verify all three times are correctly parsed
3. Verify chronological order is maintained

**Pass Criteria:**
- ✅ Event start is 10:00 AM
- ✅ Rehearsal is 9:00 AM (explicitly stated, not calculated)
- ✅ Event end is 4:00 PM

---

### Test Case 1.3: Relative Rehearsal Time
**User Input:**
```
start 2pm, finish 6pm, need 2 hours before for rehearsal
```

**Expected Behavior:**
- Event Start Time: 2:00 PM
- Rehearsal Time: 12:00 PM (2 hours before 2:00 PM)
- Event End Time: 6:00 PM

**Verification Steps:**
1. Enter the user input
2. Verify rehearsal is calculated as 2 hours before start
3. Verify all times are correct

**Pass Criteria:**
- ✅ Rehearsal is 12:00 PM (2 hours before 2:00 PM)
- ✅ Event start is 2:00 PM
- ✅ Event end is 6:00 PM

---

### Test Case 1.4: Default Rehearsal (No Rehearsal Mentioned)
**User Input:**
```
start 9am, finish 5pm
```

**Expected Behavior:**
- Event Start Time: 9:00 AM
- Rehearsal Time: 8:00 AM (default 1 hour before)
- Event End Time: 5:00 PM

**Verification Steps:**
1. Enter the user input
2. Verify rehearsal defaults to 1 hour before start
3. Verify setup defaults to 2 hours before start

**Pass Criteria:**
- ✅ Rehearsal defaults to 8:00 AM (1 hour before 9:00 AM)
- ✅ Setup defaults to 7:00 AM (2 hours before 9:00 AM)

---

### Test Case 1.5: Edge Case - 24-Hour Format
**User Input:**
```
start 14:00, finish 18:00, rehearsal 1 hour before
```

**Expected Behavior:**
- Event Start Time: 2:00 PM (14:00)
- Rehearsal Time: 1:00 PM (13:00)
- Event End Time: 6:00 PM (18:00)

**Verification Steps:**
1. Enter the user input
2. Verify 24-hour format is correctly parsed
3. Verify times are displayed correctly

**Pass Criteria:**
- ✅ All times correctly parsed from 24-hour format
- ✅ Rehearsal calculated correctly

---

## Bug #2: Incorrect Screen/Projector Assumption

### Test Case 2.1: User Brings Own Laptop - Should Still Ask About Display
**User Input:**
```
I need audio equipment for 8 people. I'll be bringing my own laptop. No operator needed.
```

**Expected Behavior:**
- Agent should NOT say "screens/projectors are not required"
- Agent should ask about display needs: "Will you need to show slides or videos?"
- Agent should recommend screen/projector if presentations are mentioned

**Verification Steps:**
1. Enter the user input
2. Check agent response
3. Verify agent does NOT assume no screen/projector needed
4. Verify agent asks about display needs

**Pass Criteria:**
- ✅ Agent does NOT say "screens/projectors are not required"
- ✅ Agent asks about display needs or presentations
- ✅ Agent does not make negative assumptions about display equipment

---

### Test Case 2.2: User Brings Laptop + Mentions Presentations
**User Input:**
```
I'll bring my own laptop. We'll have presentations with slides. Need audio for 8 people.
```

**Expected Behavior:**
- Agent should recommend projector + screen
- Agent should NOT exclude display equipment
- Agent should acknowledge laptop but still recommend display

**Verification Steps:**
1. Enter the user input
2. Check equipment recommendations
3. Verify projector + screen are recommended
4. Verify agent doesn't say display is not needed

**Pass Criteria:**
- ✅ Projector and screen are recommended
- ✅ Agent acknowledges user's laptop but still recommends display
- ✅ No negative assumptions about display needs

---

### Test Case 2.3: Explicit Exclusion - Should Work
**User Input:**
```
I'll bring my own laptop. We don't need a screen or projector. Just audio equipment.
```

**Expected Behavior:**
- Agent should respect explicit exclusion
- Agent should NOT recommend screen/projector
- Agent should only recommend audio equipment

**Verification Steps:**
1. Enter the user input
2. Check equipment recommendations
3. Verify screen/projector are NOT recommended
4. Verify only audio equipment is recommended

**Pass Criteria:**
- ✅ Screen/projector are NOT recommended (explicitly excluded)
- ✅ Audio equipment is recommended
- ✅ Agent respects user's explicit statement

---

### Test Case 2.4: No Laptop Mentioned - Normal Flow
**User Input:**
```
Need equipment for a presentation. 10 people. Will show slides.
```

**Expected Behavior:**
- Agent should recommend projector + screen
- Agent should ask about laptop preference
- Normal equipment recommendation flow

**Verification Steps:**
1. Enter the user input
2. Check equipment recommendations
3. Verify projector + screen are recommended
4. Verify agent asks about laptop preference

**Pass Criteria:**
- ✅ Projector and screen are recommended
- ✅ Agent asks about laptop preference
- ✅ Normal flow works as expected

---

## Bug #3: Case Sensitivity Issue - Teams Meeting Recognition

### Test Case 3.1: Lowercase "teams"
**User Input:**
```
I'd like to run a teams meeting as well
```

**Expected Behavior:**
- Agent should recognize "teams" as Microsoft Teams
- Agent should recommend: laptop, webcam, microphone, speaker
- Agent should NOT say "there isn't a specific equipment match"

**Verification Steps:**
1. Enter the user input
2. Check agent response
3. Verify Microsoft Teams is recognized
4. Verify equipment recommendations include: laptop, webcam, mic, speaker
5. Check that agent doesn't say equipment match not found

**Pass Criteria:**
- ✅ "teams" is recognized as Microsoft Teams (case-insensitive)
- ✅ Equipment recommendations include: laptop, webcam, microphone, speaker
- ✅ Agent does NOT say "no equipment match found"

---

### Test Case 3.2: Uppercase "Teams"
**User Input:**
```
We need to run a Teams meeting during the event
```

**Expected Behavior:**
- Same as Test Case 3.1
- Case-insensitive recognition should work

**Verification Steps:**
1. Enter the user input
2. Verify same behavior as lowercase "teams"

**Pass Criteria:**
- ✅ "Teams" (uppercase) is recognized
- ✅ Same equipment recommendations as Test Case 3.1

---

### Test Case 3.3: "Microsoft Teams" Full Name
**User Input:**
```
Need equipment for a Microsoft Teams video conference
```

**Expected Behavior:**
- Full name should be recognized
- Same equipment recommendations

**Verification Steps:**
1. Enter the user input
2. Verify recognition and recommendations

**Pass Criteria:**
- ✅ "Microsoft Teams" is recognized
- ✅ Equipment recommendations are correct

---

### Test Case 3.4: "MS Teams" Variation
**User Input:**
```
Setting up an MS Teams meeting for remote participants
```

**Expected Behavior:**
- Variation should be recognized
- Same equipment recommendations

**Verification Steps:**
1. Enter the user input
2. Verify recognition works

**Pass Criteria:**
- ✅ "MS Teams" is recognized
- ✅ Equipment recommendations are correct

---

### Test Case 3.5: Teams + Other Equipment
**User Input:**
```
Need a teams meeting setup plus 2 wireless microphones for speakers
```

**Expected Behavior:**
- Teams equipment should be recommended
- Additional microphones should be recommended
- No duplicates

**Verification Steps:**
1. Enter the user input
2. Verify both Teams equipment and additional mics are recommended
3. Verify no duplicate recommendations

**Pass Criteria:**
- ✅ Teams equipment (laptop, webcam, mic, speaker) recommended
- ✅ Additional 2 wireless microphones recommended
- ✅ No duplicate equipment

---

## Bug #4: Organization Name Parsing Error - "Called THE gully"

### Test Case 4.1: "Called" Prefix Removal
**User Input:**
```
Jim Micro, new customer, Called THE gully, and located in winston glades. 0412564733, Jim@Micro.com, Supervisor
```

**Expected Behavior:**
- Organization Name: "THE gully" (NOT "Called THE gully")
- Address: "winston glades"
- Contact: Jim Micro, 0412564733, Jim@Micro.com, Supervisor

**Verification Steps:**
1. Enter the user input
2. Check extracted organization name
3. Verify "Called" is NOT included in organization name
4. Verify address is correctly extracted
5. Check database/storage to confirm correct organization name

**Pass Criteria:**
- ✅ Organization name is "THE gully" (without "Called")
- ✅ Address is "winston glades"
- ✅ Contact information is correctly extracted
- ✅ Database stores correct organization name

---

### Test Case 4.2: "Named" Prefix
**User Input:**
```
John Smith, new customer. Named ABC Company, located in Brisbane. 0400123456, john@abc.com
```

**Expected Behavior:**
- Organization Name: "ABC Company" (NOT "Named ABC Company")

**Verification Steps:**
1. Enter the user input
2. Verify "Named" is stripped from organization name

**Pass Criteria:**
- ✅ Organization name is "ABC Company" (without "Named")

---

### Test Case 4.3: "Is" Prefix
**User Input:**
```
Sarah Jones, is XYZ Corporation, based in Sydney. 0400987654, sarah@xyz.com
```

**Expected Behavior:**
- Organization Name: "XYZ Corporation" (NOT "is XYZ Corporation")

**Verification Steps:**
1. Enter the user input
2. Verify "is" is stripped from organization name

**Pass Criteria:**
- ✅ Organization name is "XYZ Corporation" (without "is")

---

### Test Case 4.4: "Company is" Prefix
**User Input:**
```
Mike Brown, company is Tech Solutions, located in Melbourne. 0400555666, mike@tech.com
```

**Expected Behavior:**
- Organization Name: "Tech Solutions" (NOT "company is Tech Solutions")

**Verification Steps:**
1. Enter the user input
2. Verify "company is" is stripped

**Pass Criteria:**
- ✅ Organization name is "Tech Solutions" (without "company is")

---

### Test Case 4.5: "Business is" Prefix
**User Input:**
```
Lisa White, business is Creative Designs, in Perth. 0400111222, lisa@creative.com
```

**Expected Behavior:**
- Organization Name: "Creative Designs" (NOT "business is Creative Designs")

**Verification Steps:**
1. Enter the user input
2. Verify "business is" is stripped

**Pass Criteria:**
- ✅ Organization name is "Creative Designs" (without "business is")

---

### Test Case 4.6: No Prefix - Should Work Normally
**User Input:**
```
Tom Green, ABC Industries, located in Adelaide. 0400333444, tom@abc.com
```

**Expected Behavior:**
- Organization Name: "ABC Industries" (no change needed)

**Verification Steps:**
1. Enter the user input
2. Verify organization name is correctly extracted without modification

**Pass Criteria:**
- ✅ Organization name is "ABC Industries" (unchanged, correct)

---

### Test Case 4.7: Case Sensitivity - "CALLED" Uppercase
**User Input:**
```
Jane Doe, CALLED THE BIG COMPANY, in Canberra. 0400444555, jane@big.com
```

**Expected Behavior:**
- Organization Name: "THE BIG COMPANY" (NOT "CALLED THE BIG COMPANY")
- Case-insensitive prefix removal should work

**Verification Steps:**
1. Enter the user input
2. Verify uppercase "CALLED" is stripped

**Pass Criteria:**
- ✅ Organization name is "THE BIG COMPANY" (case-insensitive prefix removal works)

---

## Integration Testing

### Test Case I.1: Multiple Bugs in One Conversation
**User Input:**
```
Hi, I'm Jim Micro from Called THE gully in winston glades. 
We need to run a teams meeting for 8 people. 
Event starts at 9am, finishes at 5pm, with 1 hour rehearsal before.
I'll bring my own laptop but we'll show slides.
```

**Expected Behavior:**
- Organization: "THE gully" (Bug #4 fixed)
- Teams recognized and equipment recommended (Bug #3 fixed)
- Event start: 9am, Rehearsal: 8am (Bug #1 fixed)
- Screen/projector recommended despite user bringing laptop (Bug #2 fixed)

**Verification Steps:**
1. Enter the complete user input
2. Verify all four bugs are fixed in a single conversation
3. Check each aspect independently

**Pass Criteria:**
- ✅ All four bug fixes work together
- ✅ No conflicts between fixes
- ✅ Complete information extracted correctly

---

## Regression Testing

### Test Case R.1: Existing Functionality Still Works
**Test Scenarios:**
1. Normal booking flow without any of the bug scenarios
2. Standard equipment recommendations
3. Standard organization name parsing (without prefixes)
4. Standard time parsing (without relative times)

**Expected Behavior:**
- All existing functionality should work as before
- No breaking changes introduced

**Verification Steps:**
1. Run standard booking flow
2. Verify no errors or unexpected behavior
3. Compare with previous working version

**Pass Criteria:**
- ✅ All existing functionality works
- ✅ No regressions introduced
- ✅ Backward compatibility maintained

---

## Test Execution Checklist

### Pre-Testing
- [ ] Test environment is set up and running
- [ ] Database access is available
- [ ] Chat interface is accessible
- [ ] Test data is prepared

### Bug #1 Testing
- [ ] Test Case 1.1: Basic Event Start Time
- [ ] Test Case 1.2: Event Start with Explicit Rehearsal Time
- [ ] Test Case 1.3: Relative Rehearsal Time
- [ ] Test Case 1.4: Default Rehearsal
- [ ] Test Case 1.5: 24-Hour Format

### Bug #2 Testing
- [ ] Test Case 2.1: User Brings Own Laptop
- [ ] Test Case 2.2: User Brings Laptop + Presentations
- [ ] Test Case 2.3: Explicit Exclusion
- [ ] Test Case 2.4: No Laptop Mentioned

### Bug #3 Testing
- [ ] Test Case 3.1: Lowercase "teams"
- [ ] Test Case 3.2: Uppercase "Teams"
- [ ] Test Case 3.3: "Microsoft Teams" Full Name
- [ ] Test Case 3.4: "MS Teams" Variation
- [ ] Test Case 3.5: Teams + Other Equipment

### Bug #4 Testing
- [ ] Test Case 4.1: "Called" Prefix Removal
- [ ] Test Case 4.2: "Named" Prefix
- [ ] Test Case 4.3: "Is" Prefix
- [ ] Test Case 4.4: "Company is" Prefix
- [ ] Test Case 4.5: "Business is" Prefix
- [ ] Test Case 4.6: No Prefix
- [ ] Test Case 4.7: Case Sensitivity

### Integration Testing
- [ ] Test Case I.1: Multiple Bugs in One Conversation

### Regression Testing
- [ ] Test Case R.1: Existing Functionality Still Works

### Post-Testing
- [ ] All test results documented
- [ ] Bugs logged for any failures
- [ ] Test report generated

---

## Test Results Template

### Test Execution Log

**Date:** _______________  
**Tester:** _______________  
**Environment:** _______________

| Test Case | Status | Notes | Bug # |
|-----------|--------|-------|-------|
| 1.1 | ⬜ Pass / ⬜ Fail | | #1 |
| 1.2 | ⬜ Pass / ⬜ Fail | | #1 |
| 1.3 | ⬜ Pass / ⬜ Fail | | #1 |
| 1.4 | ⬜ Pass / ⬜ Fail | | #1 |
| 1.5 | ⬜ Pass / ⬜ Fail | | #1 |
| 2.1 | ⬜ Pass / ⬜ Fail | | #2 |
| 2.2 | ⬜ Pass / ⬜ Fail | | #2 |
| 2.3 | ⬜ Pass / ⬜ Fail | | #2 |
| 2.4 | ⬜ Pass / ⬜ Fail | | #2 |
| 3.1 | ⬜ Pass / ⬜ Fail | | #3 |
| 3.2 | ⬜ Pass / ⬜ Fail | | #3 |
| 3.3 | ⬜ Pass / ⬜ Fail | | #3 |
| 3.4 | ⬜ Pass / ⬜ Fail | | #3 |
| 3.5 | ⬜ Pass / ⬜ Fail | | #3 |
| 4.1 | ⬜ Pass / ⬜ Fail | | #4 |
| 4.2 | ⬜ Pass / ⬜ Fail | | #4 |
| 4.3 | ⬜ Pass / ⬜ Fail | | #4 |
| 4.4 | ⬜ Pass / ⬜ Fail | | #4 |
| 4.5 | ⬜ Pass / ⬜ Fail | | #4 |
| 4.6 | ⬜ Pass / ⬜ Fail | | #4 |
| 4.7 | ⬜ Pass / ⬜ Fail | | #4 |
| I.1 | ⬜ Pass / ⬜ Fail | | All |
| R.1 | ⬜ Pass / ⬜ Fail | | Reg |

**Total Passed:** ___ / 22  
**Total Failed:** ___ / 22  
**Pass Rate:** ___%

---

## Known Issues / Notes

### Testing Considerations
1. **Agent Instructions:** Changes to `AgentToolInstaller.cs` require agent restart/reload to take effect
2. **Time Format:** System should handle both 12-hour (9am) and 24-hour (14:00) formats
3. **Case Sensitivity:** All text matching should be case-insensitive
4. **Database Verification:** For Bug #4, verify organization names are stored correctly in database

### Edge Cases to Monitor
- Very short organization names (< 3 characters)
- Very long organization names (> 60 characters)
- Multiple time mentions in one message
- Multiple equipment types mentioned together
- Special characters in organization names

---

## Sign-Off

**Test Completed By:** _______________  
**Date:** _______________  
**Approved By:** _______________  
**Date:** _______________

**Overall Status:** ⬜ All Tests Passed / ⬜ Issues Found  
**Ready for Production:** ⬜ Yes / ⬜ No

---

## Appendix: Test Data Examples

### Sample User Inputs for Quick Testing

**Bug #1 Quick Test:**
```
start 9am and finish 5, allow for 1 hour before for rehearsal
```

**Bug #2 Quick Test:**
```
I'll bring my own laptop. We'll have presentations with slides.
```

**Bug #3 Quick Test:**
```
I'd like to run a teams meeting as well
```

**Bug #4 Quick Test:**
```
Jim Micro, new customer, Called THE gully, and located in winston glades.
```
