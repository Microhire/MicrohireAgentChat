# Complete Guide: Creating Customer, Contact, and Booking

## Overview
This guide documents the complete process for creating a new customer, contact, and booking in the AITESTDB production database.

## Database Connection
```python
import pymssql
from datetime import datetime

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB',
    as_dict=True
)
cursor = conn.cursor()
```

## Step 1: Create New Contact

### Required Fields
- `Contactname` - Full name
- `firstname` - First name
- `surname` - Last name
- `Email` - Email address
- `Cell` - Phone number
- `position` - Job title
- `CreateDate` - Creation timestamp
- `LastUpdate` - Last update timestamp
- `LastContact` - Last contact timestamp
- `Active` - 'Y' or 'N'

### SQL
```python
cursor.execute('''
    INSERT INTO tblContact (
        Contactname, firstname, surname, Email, Cell, position,
        CreateDate, LastUpdate, LastContact, Active
    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
''', (
    'Upul Harischandra',      # Full name
    'Upul',                    # First name
    'Harischandra',            # Last name
    'upul@testevents.com',     # Email
    '0412345678',              # Phone
    'Event Manager',           # Position
    datetime.now(),            # CreateDate
    datetime.now(),            # LastUpdate
    datetime.now(),            # LastContact
    'Y'                        # Active
))
conn.commit()

# Get the new contact ID
cursor.execute('SELECT TOP 1 ID FROM tblContact WHERE Contactname = %s', ('Upul Harischandra',))
contact_result = cursor.fetchone()
contact_id = contact_result['ID']
```

## Step 2: Create New Customer

### Generate Customer Code
Customer codes follow the pattern `C#####` where ##### is an incrementing number.

```python
cursor.execute('''
    SELECT MAX(CAST(SUBSTRING(customer_code, 2, LEN(customer_code)-1) AS INT)) as max_cust 
    FROM tblcust 
    WHERE customer_code LIKE %s
''', ('C%',))
max_cust_result = cursor.fetchone()
max_cust = max_cust_result['max_cust'] if max_cust_result['max_cust'] else 12440
new_cust_num = max_cust + 1
new_cust_code = f'C{new_cust_num}'
```

### Required Fields
- `customer_code` - Generated code (e.g., C14504)
- `OrganisationV6` - Company name
- `contactV6` - Contact name (text field)
- `iLink_ContactID` - **CRITICAL** - Link to tblContact.ID
- `CustCDate` - Creation date

### SQL
```python
cursor.execute('''
    INSERT INTO tblcust (
        customer_code, OrganisationV6, contactV6, iLink_ContactID, CustCDate
    ) VALUES (%s, %s, %s, %s, %s)
''', (
    new_cust_code,              # Generated code
    'Test Events Company',      # Organization name
    'Upul Harischandra',        # Contact name (text)
    contact_id,                 # **CRITICAL** - Link to contact record
    datetime.now()              # Creation date
))

# Get the new customer ID
cursor.execute('SELECT TOP 1 ID FROM tblcust WHERE customer_code = %s', (new_cust_code,))
cust_result = cursor.fetchone()
customer_id = cust_result['ID']
```

### IMPORTANT: Link Contact to Customer
Also create the link in `tblLinkCustContact`:

```python
cursor.execute('''
    INSERT INTO tblLinkCustContact (customer_code, contactid) 
    VALUES (%s, %s)
''', (new_cust_code, contact_id))
```

## Step 3: Create Booking

### Generate Booking Number
Booking numbers follow the pattern `C########` (8 digits after C).

```python
cursor.execute('''
    SELECT MAX(CAST(SUBSTRING(booking_no, 2, 8) AS INT)) as max_booking 
    FROM tblbookings 
    WHERE booking_no LIKE %s
''', ('C%',))
max_booking_result = cursor.fetchone()
max_booking = max_booking_result['max_booking'] if max_booking_result['max_booking'] else 124400000
next_booking_num = max_booking + 1
booking_no = f'C{next_booking_num:08d}'
```

### Required Fields

#### Booking Identification
- `booking_no` - Generated booking number (e.g., C14414001)
- `order_no` - Usually same as booking_no
- `booking_type_v32` - Type (2 = standard)
- `status` - Status code (0 = active)
- `BookingProgressStatus` - Progress (1 = in progress)
- `bBookingIsComplete` - 0 = incomplete, 1 = complete

#### Dates (CRITICAL - Must be set correctly)
- `dDate` - **Delivery date** (when equipment goes out)
- `rDate` - **Return date** (when equipment comes back)
- `Sdate` - Event date
- `ShowSdate` - Show start date/time
- `ShowEdate` - Show end date/time
- `SetDate` - Setup date
- `ADelDate` - Actual delivery date
- `RehDate` - Rehearsal date
- `PreDate` - Prep date
- `PickupRetDate` - Pickup/return date
- `order_date` - Order date (usually today)

#### Times (CRITICAL - Must be set correctly)
- `del_time_h` - Delivery hour (0-23)
- `del_time_m` - Delivery minute (0-59)
- `ret_time_h` - Return hour (0-23)
- `ret_time_m` - Return minute (0-59)
- `ShowStartTime` - Show start time (4-digit string, e.g., '1900')
- `ShowEndTime` - Show end time (4-digit string, e.g., '2300')
- `SetupTimeV61` - Setup time (4-digit string, e.g., '0700')
- `StrikeTime` - Strike time (4-digit string, e.g., '2330')

#### Venue
- `VenueID` - Venue ID (1 = default)
- `VenueRoom` - Room name (e.g., 'Grand Ballroom')

#### Contact/Customer Links
- `contact_nameV6` - Contact name (text)
- `OrganizationV6` - Organization name (text)
- `CustID` - Customer ID (from tblcust.ID)
- `ContactID` - Contact ID (from tblContact.ID)

#### Financial
- `price_quoted` - Total quoted price
- `hire_price` - Equipment hire price
- `labour` - Labour cost
- `insurance_v5` - Insurance cost
- `sundry_total` - Sundry items total

#### Event Details
- `days_using` - Number of days
- `expAttendees` - Expected attendees
- `showName` - Event name/description

### SQL
```python
event_date = datetime(2025, 12, 20)
delivery_date = datetime(2025, 12, 20)
return_date = datetime(2025, 12, 21)
show_start = datetime(2025, 12, 20, 19, 0, 0)
show_end = datetime(2025, 12, 20, 23, 0, 0)

cursor.execute('''
    INSERT INTO tblbookings (
        booking_no, order_no, booking_type_v32, status, BookingProgressStatus,
        bBookingIsComplete,
        dDate, rDate, Sdate, ShowSdate, ShowEdate, SetDate, ADelDate, RehDate, PreDate, PickupRetDate,
        del_time_h, del_time_m, ret_time_h, ret_time_m,
        ShowStartTime, ShowEndTime, SetupTimeV61, StrikeTime,
        VenueID, VenueRoom,
        contact_nameV6, OrganizationV6, CustID, ContactID,
        price_quoted, hire_price, labour, insurance_v5, sundry_total,
        days_using, expAttendees, showName, order_date
    ) VALUES (
        %s, %s, %s, %s, %s, %s,
        %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
        %s, %s, %s, %s,
        %s, %s, %s, %s,
        %s, %s,
        %s, %s, %s, %s,
        %s, %s, %s, %s, %s,
        %s, %s, %s, %s
    )
''', (
    booking_no, booking_no, 2, 0, 1,
    0,
    delivery_date, return_date, event_date, show_start, show_end, event_date, delivery_date, event_date, delivery_date, return_date,
    7, 0, 23, 30,
    '1900', '2300', '0700', '2330',
    1, 'Grand Ballroom',
    'Upul Harischandra', 'Test Events Company', customer_id, contact_id,
    8500.00, 6500.00, 1500.00, 300.00, 200.00,
    1, 150, 'Corporate Holiday Party - Test Booking', datetime.now()
))

# Get booking ID
cursor.execute('SELECT TOP 1 ID FROM tblbookings WHERE booking_no = %s', (booking_no,))
booking_result = cursor.fetchone()
booking_id = booking_result['ID']
```

## Step 4: Add Equipment Items

Equipment items are stored in `tblitemtran`.

### Required Fields
- `booking_no_v32` - Booking number
- `booking_id` - Booking ID
- `heading_no` - Heading number (usually 1)
- `seq_no` - Sequence number (usually 1)
- `sub_seq_no` - Sub-sequence number (increments for each item)
- `trans_type_v41` - Transaction type (1 = hire)
- `Comment_desc_v42` - Item description
- `price` - Item price
- `trans_qty` - Quantity

### SQL
```python
equipment_items = [
    ('SPK-PKG', 'Complete Speaker Package', 2500.00),
    ('LIGHT-PKG', 'LED Lighting Package', 1800.00),
    ('PROJ-SYS', 'Projection System', 1200.00),
    ('MIC-PKG', 'Wireless Microphone Package', 800.00),
    ('STAGE-BASIC', 'Basic Staging Setup', 1200.00)
]

for i, (product_code, description, price) in enumerate(equipment_items):
    cursor.execute('''
        INSERT INTO tblitemtran (
            booking_no_v32, heading_no, seq_no, sub_seq_no, trans_type_v41,
            Comment_desc_v42, price, trans_qty, booking_id
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
    ''', (
        booking_no, 1, 1, i+1, 1,
        f'{product_code} - {description}', price, 1, booking_id
    ))
```

## Step 5: Add Crew Assignments

Crew is stored in `tblcrew` (similar structure to equipment).

### Required Fields
- `booking_no_v32` - Booking number
- `heading_no` - Heading number (usually 0 for crew)
- `seq_no` - Sequence number (start at 65530 or higher)
- `sub_seq_no` - Sub-sequence (usually 0)
- `product_code_v42` - Crew product code
- `price` - Crew rate
- `hours` - Hours worked
- `trans_qty` - Quantity (usually 1)

### SQL
```python
crew_items = [
    ('TECHDIR', 'Technical Director', 600.00, 8),
    ('AUDENG', 'Audio Engineer', 400.00, 8),
    ('LIGHTECH', 'Lighting Technician', 400.00, 8),
    ('STAGEHND', 'Stage Hands', 500.00, 16),
    ('SETUP', 'Setup Crew', 400.00, 4),
    ('STRIKE', 'Strike Crew', 300.00, 4)
]

# Get current max seq_no for crew
cursor.execute('SELECT MAX(seq_no) as max_seq FROM tblcrew WHERE booking_no_v32 = %s', (booking_no,))
max_seq_result = cursor.fetchone()
max_seq = max_seq_result['max_seq'] if max_seq_result['max_seq'] else 65530
start_seq = max_seq + 1

for i, (product_code, description, rate, hours) in enumerate(crew_items):
    cursor.execute('''
        INSERT INTO tblcrew (
            booking_no_v32, heading_no, seq_no, sub_seq_no, 
            product_code_v42, price, hours, trans_qty
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
    ''', (
        booking_no, 0, start_seq + i, 0,
        product_code, rate, hours, 1
    ))
```

## Step 6: Add Conversation Transcript (Optional)

Store conversation history in `tblbooknote`.

### Required Fields
- `bookingNo` - Booking number
- `line_no` - Line number (start at 1)
- `text_line` - Text content
- `NoteType` - Note type (1 = conversation)

### SQL
```python
transcript = '''=== CONVERSATION TRANSCRIPT ===
[2025-11-20 14:00:00] USER: Hi, I'm Upul Harischandra from Test Events Company. We need to book equipment for a corporate holiday party.

[2025-11-20 14:02:00] USER: We need sound system, lighting, projection, and staging for 150 people.

[2025-11-20 14:04:00] USER: Yes, please create the booking.'''

cursor.execute('''
    INSERT INTO tblbooknote (
        bookingNo, line_no, text_line, NoteType
    ) VALUES (%s, %s, %s, %s)
''', (booking_no, 1, transcript, 1))
```

## Step 7: Commit Transaction

```python
conn.commit()
conn.close()
```

## Critical Points to Remember

### 1. Customer-Contact Link
**MUST** set `iLink_ContactID` in `tblcust` to the contact's ID. Without this:
- ❌ Customer record will show warning about missing contact link
- ❌ Contact won't be properly associated with customer

```python
# CRITICAL: Set iLink_ContactID when creating customer
cursor.execute('''
    INSERT INTO tblcust (
        customer_code, OrganisationV6, contactV6, iLink_ContactID, CustCDate
    ) VALUES (%s, %s, %s, %s, %s)
''', (new_cust_code, org_name, contact_name, contact_id, datetime.now()))
```

### 2. Dates Must Be Set Correctly
**MUST** set these dates or they will show as NULL or 1980:
- `dDate` - Delivery date (OUT date in UI)
- `rDate` - Return date (IN date in UI)
- `order_date` - Order date (DATE ADDED in UI)
- All show dates and times

```python
# CRITICAL: Set all dates
cursor.execute('''
    INSERT INTO tblbookings (
        booking_no, dDate, rDate, order_date, ShowSdate, ShowEdate, ...
    ) VALUES (%s, %s, %s, %s, %s, %s, ...)
''', (booking_no, delivery_date, return_date, datetime.now(), show_start, show_end, ...))
```

### 3. Times Must Be Set
**MUST** set delivery and return times:
- `del_time_h` / `del_time_m` - Delivery time (hours/minutes)
- `ret_time_h` / `ret_time_m` - Return time (hours/minutes)
- `ShowStartTime` / `ShowEndTime` - Show times (4-digit strings)

### 4. Booking Number Format
- Customer codes: `C#####` (5 digits)
- Booking numbers: `C########` (8 digits)

### 5. Transaction Order
1. Create Contact first
2. Create Customer (with iLink_ContactID)
3. Link in tblLinkCustContact
4. Create Booking
5. Add Equipment
6. Add Crew
7. Add Notes
8. Commit

## Common Errors and Fixes

### Error: "Missing link to contact record"
**Cause:** `iLink_ContactID` is 0 or NULL in `tblcust`

**Fix:**
```python
cursor.execute('''
    UPDATE tblcust SET iLink_ContactID = %s WHERE customer_code = %s
''', (contact_id, customer_code))
```

### Error: Dates showing as 1980 or NULL
**Cause:** Dates not set during INSERT

**Fix:**
```python
cursor.execute('''
    UPDATE tblbookings SET
        dDate = %s, rDate = %s, order_date = %s
    WHERE booking_no = %s
''', (delivery_date, return_date, datetime.now(), booking_no))
```

### Error: Invalid column name
**Cause:** Column names are case-sensitive or incorrect

**Fix:** Check exact column names in database schema:
- `OrganisationV6` (not `organisationv6`)
- `contact_nameV6` (not `contact_namev6`)
- `Comment_desc_v42` (not `item_desc`)
- `booking_no_v32` (not `booking_no` in tblitemtran/tblcrew)

## Complete Working Example

See: `/home/nitwit/INTENT/MicrohireAgentChat/create_complete_booking_upul.py`

This script creates:
- ✅ New contact (Upul Harischandra)
- ✅ New customer (Test Events Company - C14504)
- ✅ Customer-contact link (iLink_ContactID)
- ✅ New booking (C14414001)
- ✅ Equipment items (5 items)
- ✅ Crew assignments (6 crew)
- ✅ Conversation transcript
- ✅ All dates and times set correctly

## Verification Checklist

After creating a booking, verify:
- [ ] Booking appears in inventory management tool
- [ ] Contact name shows correctly
- [ ] Customer/organization shows correctly
- [ ] Delivery date (OUT) shows correctly
- [ ] Return date (IN) shows correctly
- [ ] Order date shows correctly (not 1980)
- [ ] Customer record opens without warning
- [ ] Equipment items are visible
- [ ] Crew assignments are visible
- [ ] All dates and times are correct

## Database Connection String

From `appsettings.json`:
```
Server=116.90.5.144\SQLEXPRESS,41383;
Database=AITESTDB;
User Id=PowerBI-Consult;
Password=2tW@ostq3a3_9oV3m-TBQu3w;
TrustServerCertificate=True;
```

## Summary

Creating a complete booking requires:
1. **Contact** → `tblContact`
2. **Customer** → `tblcust` (with `iLink_ContactID` set)
3. **Link** → `tblLinkCustContact`
4. **Booking** → `tblbookings` (with all dates/times)
5. **Equipment** → `tblitemtran`
6. **Crew** → `tblcrew`
7. **Notes** → `tblbooknote`

**Most Critical:**
- Set `iLink_ContactID` in customer
- Set all date fields (`dDate`, `rDate`, `order_date`)
- Set all time fields (`del_time_h/m`, `ret_time_h/m`)
- Use correct column names (case-sensitive)
- Commit transaction at the end

