-- Insert Sample Booking Data
-- Run this script in SQL Server Management Studio to create a test booking
-- This simulates the exact data that the chat booking process would create

USE AITESTDB;
GO

DECLARE @Now DATETIME = GETDATE();
DECLARE @EventDate DATETIME = '2025-03-15';
DECLARE @ContactID INT;
DECLARE @OrgID INT = 14503; -- Known organization ID from previous runs
DECLARE @CustomerCode VARCHAR(50) = 'C14503';
DECLARE @BookingNo VARCHAR(20);

-- Generate booking number (next available for 2025)
SELECT @BookingNo = '25' + RIGHT('0000' + CAST(ISNULL(MAX(CAST(SUBSTRING(booking_no, 3, 4) AS INT)), 0) + 1 AS VARCHAR(4)), 4)
FROM tblbookings
WHERE booking_no LIKE '25%';

PRINT 'Generated Booking Number: ' + @BookingNo;
PRINT 'Contact Email: michael@yes100attendees.com';
PRINT 'Organization: Yes 100 Attendees (ID: 14503)';
PRINT 'Event Date: 2025-03-15';
PRINT 'Total Quote: $6,900.00';
PRINT '';

-- 1. Insert/Update Contact
PRINT '1. Creating/Updating Contact...';
IF EXISTS (SELECT 1 FROM tblContact WHERE Email = 'michael@yes100attendees.com')
BEGIN
    UPDATE tblContact SET
        Contactname = 'Michael Knight',
        firstname = 'Michael',
        surname = 'Knight',
        Email = 'michael@yes100attendees.com',
        Cell = '07111111111',
        position = 'Events Coordinator',
        LastUpdate = @Now,
        LastContact = @Now
    WHERE Email = 'michael@yes100attendees.com';

    SELECT @ContactID = ID FROM tblContact WHERE Email = 'michael@yes100attendees.com';
    PRINT '   Updated existing contact, ID: ' + CAST(@ContactID AS VARCHAR(10));
END
ELSE
BEGIN
    INSERT INTO tblContact (
        Contactname, firstname, surname, Email, Cell, position,
        CreateDate, LastUpdate, LastContact, Active
    ) VALUES (
        'Michael Knight', 'Michael', 'Knight', 'michael@yes100attendees.com',
        '07111111111', 'Events Coordinator', @Now, @Now, @Now, 1
    );

    SET @ContactID = SCOPE_IDENTITY();
    PRINT '   Created new contact, ID: ' + CAST(@ContactID AS VARCHAR(10));
END

-- 2. Link Contact to Organization (if not already linked)
PRINT '2. Linking Contact to Organization...';
IF NOT EXISTS (SELECT 1 FROM tblLinkCustContact WHERE Customer_Code = @CustomerCode AND ContactID = @ContactID)
BEGIN
    INSERT INTO tblLinkCustContact (Customer_Code, ContactID)
    VALUES (@CustomerCode, @ContactID);
    PRINT '   Linked contact ' + CAST(@ContactID AS VARCHAR(10)) + ' to organization ' + @CustomerCode;
END
ELSE
BEGIN
    PRINT '   Contact already linked to organization';
END

-- 3. Insert Main Booking Record
PRINT '3. Creating Booking Record...';
INSERT INTO tblbookings (
    booking_no, order_no, booking_type_v32, status, BookingProgressStatus,
    bBookingIsComplete, SDate, rDate, SetDate, ShowSDate, ShowEdate, RehDate,
    order_date, EntryDate, showStartTime, ShowEndTime, setupTimeV61, StrikeTime,
    del_time_h, del_time_m, ret_time_h, ret_time_m,
    VenueID, VenueRoom, contact_nameV6, OrganizationV6, CustID, ContactID,
    price_quoted, hire_price, labour, insurance_v5, sundry_total, Tax2,
    days_using, expAttendees, From_locn, Trans_to_locn, return_to_locn,
    invoiced, perm_casual, TaxAuthority1, TaxAuthority2, showName
) VALUES (
    @BookingNo, @BookingNo, 2, 0, 1,  -- booking basics: type=2(Quote/Booking), status=0(enquiry), progress=1(enquiry)
    0,  -- bBookingIsComplete = false
    @EventDate, @EventDate, @EventDate,  -- SDate, rDate, SetDate all = event date
    '2025-03-15 18:00:00', '2025-03-15 22:00:00', @EventDate,  -- ShowSDate, ShowEdate, RehDate
    @Now, @Now,  -- order_date, EntryDate
    '1800', '2200', '0800', '2300',  -- times as HHMM strings: showStart, showEnd, setup, strike
    8, 0, 23, 0,  -- del_time_h/m, ret_time_h/m as bytes
    1, 'Main Ballroom',  -- VenueID (default=1), VenueRoom
    'Michael Knight', 'Yes 100 Attendees', @OrgID, @ContactID,  -- contact and org info
    6900.00, 5200.00, 1600.00, 100.00, 100.00, 0.00,  -- financial: quoted, hire, labour, insurance, service charge, GST
    1, 100, 20, 20, 20,  -- days_using, expAttendees, locations (warehouse=20)
    'N', 'Y', 0, 1,  -- invoiced=N, perm_casual=Y, tax authorities
    'Corporate Event - Yes 100 Attendees'  -- showName
);

PRINT '   Created booking: ' + @BookingNo;

-- 4. Insert Equipment Items
PRINT '4. Adding Equipment Items...';
INSERT INTO tblitemtran (booking_no_v32, item_desc, hire_rate, qty, line_total, CreateDate, item_type)
VALUES
    (@BookingNo, 'Sound System Package', 2500.00, 1, 2500.00, @Now, 'Equipment'),
    (@BookingNo, 'Lighting Setup Package', 1800.00, 1, 1800.00, @Now, 'Equipment'),
    (@BookingNo, 'Staging Package', 900.00, 1, 900.00, @Now, 'Equipment');

PRINT '   Added 3 equipment items';

-- 5. Insert Labor/Crew
PRINT '5. Adding Labor/Crew...';
INSERT INTO tblcrew (booking_no, crew_desc, hours, rate, line_total, CreateDate)
VALUES
    (@BookingNo, 'Setup Crew (2 people, 2 hours)', 4, 100.00, 400.00, @Now),
    (@BookingNo, 'Show Crew (3 people, 4 hours)', 12, 100.00, 1200.00, @Now),
    (@BookingNo, 'Strike Crew (2 people, 1 hour)', 2, 100.00, 200.00, @Now);

PRINT '   Added 3 labor entries';

-- 6. Insert Conversation Transcript
PRINT '6. Adding Conversation Transcript...';
DECLARE @Transcript NVARCHAR(MAX) = '
=== CONVERSATION TRANSCRIPT ===

[2025-01-20 10:00:00] USER: Hi, I''m Michael Knight from Yes 100 Attendees. I need to book equipment for an event.

[2025-01-20 10:00:05] ASSISTANT: Hello Michael! I''d be happy to help you book equipment for your event. Could you please tell me:
1. When is the event?
2. Where is the venue?
3. What equipment do you need?
4. How many people will attend?

[2025-01-20 10:00:15] USER: The event is on 15 March 2025 at the Westin Melbourne. We expect 100 attendees.

[2025-01-20 10:00:20] ASSISTANT: Great! So we have:
- Event: 15 March 2025
- Venue: Westin Melbourne
- Attendees: 100

What equipment do you need? For example: sound system, lighting, staging, etc.

[2025-01-20 10:00:30] USER: We need a full sound system with microphones, speakers, and mixer. Also lighting setup with LED lights, and staging for the main area.

[2025-01-20 10:00:35] ASSISTANT: Perfect! Let me get your contact details for the booking.

What''s your phone number and email address?

[2025-01-20 10:00:45] USER: My email is michael@yes100attendees.com and phone is 07111111111. I''m the Events Coordinator.

[2025-01-20 10:00:50] ASSISTANT: Thanks Michael! Now for the logistics:

- Setup time: 8:00 AM
- Show start: 6:00 PM
- Show end: 10:00 PM
- Strike time: 11:00 PM

Does this work for you?

[2025-01-20 10:01:00] USER: Yes, that timing works perfectly.

[2025-01-20 10:01:05] ASSISTANT: Based on your requirements, here''s the quote:

**Equipment:**
- Sound System: $2,500
- Lighting Setup: $1,800
- Staging: $900

**Labor:**
- Setup Crew (2 people, 2 hours): $400
- Show Crew (3 people, 4 hours): $1,200
- Strike Crew (2 people, 1 hour): $200

**Total: $6,900** (includes GST)

Shall I proceed with creating this booking?

[2025-01-20 10:01:15] USER: Yes, please create the booking.
';

INSERT INTO tblbooknote (BookingNo, TextLine, NoteType, CreateDate)
VALUES (@BookingNo, @Transcript, 1, @Now);  -- NoteType 1 = transcript

PRINT '   Added conversation transcript';

-- Final Summary
PRINT '';
PRINT '=== BOOKING CREATION COMPLETE ===';
PRINT 'Booking Number: ' + @BookingNo;
PRINT 'Contact: Michael Knight (ID: ' + CAST(@ContactID AS VARCHAR(10)) + ')';
PRINT 'Organization: Yes 100 Attendees (ID: ' + CAST(@OrgID AS VARCHAR(10)) + ')';
PRINT 'Event Date: 2025-03-15';
PRINT 'Venue: Default Venue (ID: 1)';
PRINT 'Total Quote: $6,900.00';
PRINT 'Attendees: 100';
PRINT 'Equipment Items: 3';
PRINT 'Labor Entries: 3';
PRINT '';
PRINT 'Now check your inventory management tool to see if this booking appears!';
PRINT 'Look for booking number: ' + @BookingNo;
GO
