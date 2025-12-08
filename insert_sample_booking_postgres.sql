-- Insert Sample Booking Data for PostgreSQL
-- Run this script in your PostgreSQL client (pgAdmin, psql, etc.)
-- This simulates the exact data that the chat booking process would create

-- Connect to your database first, then run these commands:

-- Set current timestamp
\set NOW current_timestamp

-- 1. Insert/Update Contact
INSERT INTO tblcontact (
    contactname, firstname, surname, email, cell, position,
    createdate, lastupdate, lastcontact, active
) VALUES (
    'Michael Knight',
    'Michael',
    'Knight',
    'michael@yes100attendees.com',
    '07111111111',
    'Events Coordinator',
    :NOW, :NOW, :NOW, true
)
ON CONFLICT (email) DO UPDATE SET
    contactname = EXCLUDED.contactname,
    firstname = EXCLUDED.firstname,
    surname = EXCLUDED.surname,
    cell = EXCLUDED.cell,
    position = EXCLUDED.position,
    lastupdate = :NOW,
    lastcontact = :NOW;

-- Get the contact ID
CREATE TEMP TABLE temp_contact AS
SELECT id FROM tblcontact WHERE email = 'michael@yes100attendees.com';

-- Check if organization exists (assuming it should exist from previous runs)
-- If not, you may need to create it first or adjust the organization ID
CREATE TEMP TABLE temp_org AS
SELECT id, customer_code FROM tblcust WHERE organisationv6 = 'Yes 100 Attendees';

-- If organization doesn't exist, create it (uncomment and modify as needed):
/*
INSERT INTO tblcust (organisationv6, customer_code, createdate)
VALUES ('Yes 100 Attendees', 'C14503', :NOW);
*/

-- For now, assume organization exists with ID 14503
-- Link contact to organization (if not already linked)
INSERT INTO tbllinkcustcontact (customer_code, contactid)
SELECT 'C14503', tc.id
FROM temp_contact tc
WHERE NOT EXISTS (
    SELECT 1 FROM tbllinkcustcontact
    WHERE customer_code = 'C14503' AND contactid = tc.id
);

-- Generate booking number (next available for 2025)
CREATE TEMP TABLE temp_booking_no AS
SELECT '25' || LPAD(COALESCE(MAX(CAST(SUBSTRING(booking_no, 3, 4) AS INTEGER)), 0) + 1, 4, '0') as booking_no
FROM tblbookings
WHERE booking_no LIKE '25%';

-- Insert main booking record
INSERT INTO tblbookings (
    booking_no, order_no, booking_type_v32, status, bookingprogressstatus,
    bbookingiscomplete, sdate, rdate, setdate, showsdate, showedate, rehdate,
    order_date, entrydate, showstarttime, showendtime, setuptimev61, striketime,
    del_time_h, del_time_m, ret_time_h, ret_time_m,
    venueid, venueroom, contact_namev6, organizationv6, custid, contactid,
    price_quoted, hire_price, labour, insurance_v5, sundry_total, tax2,
    days_using, expattendees, from_locn, trans_to_locn, return_to_locn,
    invoiced, perm_casual, taxauthority1, taxauthority2, showname
) VALUES (
    (SELECT booking_no FROM temp_booking_no),  -- booking_no
    (SELECT booking_no FROM temp_booking_no),  -- order_no
    2, 0, 1,  -- booking_type_v32, status, bookingprogressstatus
    false,  -- bbookingiscomplete
    '2025-03-15', '2025-03-15', '2025-03-15',  -- sdate, rdate, setdate
    '2025-03-15 18:00:00', '2025-03-15 22:00:00', '2025-03-15',  -- showsdate, showedate, rehdate
    :NOW, :NOW,  -- order_date, entrydate
    '1800', '2200', '0800', '2300',  -- times as strings
    8, 0, 23, 0,  -- del_time_h/m, ret_time_h/m
    1, 'Main Ballroom',  -- venueid, venueroom
    'Michael Knight', 'Yes 100 Attendees',  -- contact_namev6, organizationv6
    14503,  -- custid (organization ID)
    (SELECT id FROM temp_contact),  -- contactid
    6900.00, 5200.00, 1600.00, 100.00, 100.00, 0.00,  -- financial
    1, 100, 20, 20, 20,  -- days_using, expattendees, locations
    'N', 'Y', 0, 1,  -- invoiced, perm_casual, tax authorities
    'Corporate Event - Yes 100 Attendees'  -- showname
);

-- Get the booking number for subsequent inserts
CREATE TEMP TABLE temp_new_booking AS
SELECT booking_no FROM temp_booking_no;

-- Insert equipment items
INSERT INTO tblitemtran (booking_no_v32, item_desc, hire_rate, qty, line_total, createdate, item_type)
VALUES
    ((SELECT booking_no FROM temp_new_booking), 'Sound System Package', 2500.00, 1, 2500.00, :NOW, 'Equipment'),
    ((SELECT booking_no FROM temp_new_booking), 'Lighting Setup Package', 1800.00, 1, 1800.00, :NOW, 'Equipment'),
    ((SELECT booking_no FROM temp_new_booking), 'Staging Package', 900.00, 1, 900.00, :NOW, 'Equipment');

-- Insert labor/crew
INSERT INTO tblcrew (booking_no, crew_desc, hours, rate, line_total, createdate)
VALUES
    ((SELECT booking_no FROM temp_new_booking), 'Setup Crew (2 people, 2 hours)', 4, 100.00, 400.00, :NOW),
    ((SELECT booking_no FROM temp_new_booking), 'Show Crew (3 people, 4 hours)', 12, 100.00, 1200.00, :NOW),
    ((SELECT booking_no FROM temp_new_booking), 'Strike Crew (2 people, 1 hour)', 2, 100.00, 200.00, :NOW);

-- Insert conversation transcript
INSERT INTO tblbooknote (bookingno, textline, notetype, createdate)
VALUES (
    (SELECT booking_no FROM temp_new_booking),
    $transcript$
=== CONVERSATION TRANSCRIPT ===

[2025-01-20 10:00:00] USER: Hi, I'm Michael Knight from Yes 100 Attendees. I need to book equipment for an event.

[2025-01-20 10:00:05] ASSISTANT: Hello Michael! I'd be happy to help you book equipment for your event. Could you please tell me:
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

What's your phone number and email address?

[2025-01-20 10:00:45] USER: My email is michael@yes100attendees.com and phone is 07111111111. I'm the Events Coordinator.

[2025-01-20 10:00:50] ASSISTANT: Thanks Michael! Now for the logistics:

- Setup time: 8:00 AM
- Show start: 6:00 PM
- Show end: 10:00 PM
- Strike time: 11:00 PM

Does this work for you?

[2025-01-20 10:01:00] USER: Yes, that timing works perfectly.

[2025-01-20 10:01:05] ASSISTANT: Based on your requirements, here's the quote:

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
$transcript$,
    1,  -- notetype 1 = transcript
    :NOW
);

-- Show results
SELECT 'Booking created successfully!' as status,
       (SELECT booking_no FROM temp_new_booking) as booking_number,
       'Michael Knight' as contact,
       'Yes 100 Attendees' as organization,
       '2025-03-15' as event_date,
       '$6,900.00' as total_quote,
       100 as attendees;

-- Clean up temp tables
DROP TABLE temp_contact;
DROP TABLE temp_org;
DROP TABLE temp_booking_no;
DROP TABLE temp_new_booking;
