# Venues Guide

## Overview
This guide documents the complete structure of venues in the AITESTDB database, including how venues are stored, linked to bookings, and managed.

---

## Table of Contents
1. [Venue Structure](#venue-structure)
2. [Venue Tables](#venue-tables)
3. [Venue Types](#venue-types)
4. [Venue Relationships](#venue-relationships)
5. [Using Venues in Bookings](#using-venues-in-bookings)
6. [Creating and Managing Venues](#creating-and-managing-venues)
7. [Complete Examples](#complete-examples)

---

## Venue Structure

### 1. Main Venue Table: `tblVenues`

The primary table for storing venue information.

**Key Fields:**
- `ID` - Primary key (unique venue identifier)
- `VenueName` - Venue name
- `ContactName` - Primary contact name
- `ContactID` - Links to `tblContact.ID`
- `WebPage` - Website URL
- `Address1` - Street address line 1
- `Address2` - Street address line 2
- `City` - City
- `State` - State/Province
- `Country` - Country
- `ZipCode` - Postal/ZIP code
- `Phone1CountryCode` - Primary phone country code
- `Phone1AreaCode` - Primary phone area code
- `Phone1Digits` - Primary phone number
- `Phone1Ext` - Primary phone extension
- `Phone2CountryCode` - Secondary phone country code
- `Phone2AreaCode` - Secondary phone area code
- `Phone2Digits` - Secondary phone number
- `Phone2Ext` - Secondary phone extension
- `FaxCountryCode` - Fax country code
- `FaxAreaCode` - Fax area code
- `FaxDigits` - Fax number
- `Type` - Venue type (integer)
- `BookingNo` - Booking number prefix
- `VenueNickname` - Short name/nickname
- `VenueTextType` - Text description of venue type
- `DefaultFolder` - Default folder path
- `CellCountryCode` - Mobile phone country code
- `CellAreaCode` - Mobile phone area code
- `CellDigits` - Mobile phone number

**Total Venues:** 21,379 venues

### 2. Venue Address Table: `tblVenueAddress`

Stores additional address information for venues (supports multiple addresses per venue).

**Key Fields:**
- `ID` - Primary key
- `VenueID` - Links to `tblVenues.ID`
- `Address1` - Street address line 1
- `Address2` - Street address line 2
- `City` - City
- `State` - State/Province
- `Country` - Country
- `ZipCode` - Postal/ZIP code

### 3. Venue Phone Table: `tblVenuePhone`

Stores phone numbers for venues (supports multiple phones per venue).

**Key Fields:**
- `ID` - Primary key
- `VenueID` - Links to `tblVenues.ID`
- `CountryCode` - Country code
- `AreaCode` - Area code
- `Digits` - Phone number digits
- `Extension` - Extension number
- `PhoneType` - Type of phone (e.g., "Main", "Mobile", "Fax")

### 4. Venue Contact Link Table: `tblLinkVenueContact`

Links venues to contacts (many-to-many relationship).

**Key Fields:**
- `ID` - Primary key
- `VenueID` - Links to `tblVenues.ID`
- `ContactID` - Links to `tblContact.ID`

### 5. Venue View: `vwAllVenues`

A view that combines venue information from multiple tables for easy querying.

---

## Venue Types

### Type Field

The `Type` field in `tblVenues` is an integer:
- `0` - Standard venue (207 venues)
- `1` - Other type (21,172 venues)

Most venues use Type = 1.

### Venue Text Type

The `VenueTextType` field provides a text description of the venue type (e.g., "Hotel", "Conference Center", "Outdoor Venue").

---

## Venue Relationships

### 1. Venue → Contact

**Primary Contact:**
- `tblVenues.ContactID` → `tblContact.ID`
- One primary contact per venue

**Additional Contacts:**
- `tblLinkVenueContact` table
- Many contacts can be linked to one venue
- Many venues can be linked to one contact

### 2. Venue → Address

- `tblVenues` has basic address fields
- `tblVenueAddress` can store additional addresses
- One venue can have multiple addresses

### 3. Venue → Phone

- `tblVenues` has phone fields (Phone1, Phone2, Fax, Cell)
- `tblVenuePhone` can store additional phone numbers
- One venue can have multiple phone numbers

### 4. Venue → Bookings

- `tblbookings.VenueID` → `tblVenues.ID`
- `tblbookings.VenueRoom` - Room name within venue
- One venue can have many bookings
- One booking has one venue (or VenueID = 0 for no venue)

---

## Using Venues in Bookings

### 1. Venue Fields in Bookings

**In `tblbookings` table:**
- `VenueID` - Links to `tblVenues.ID` (0 = no venue)
- `VenueRoom` - Room name/identifier within the venue

### 2. Most Used Venues

**Top Venues by Booking Count:**

| VenueID | Venue Name | Room | Bookings |
|---------|------------|------|----------|
| 0 | (No venue) | N/A | 18,449 |
| 19 | Various | Studio 4 | 496 |
| 10 | Various | Great Room | 484 |
| 17 | Various | Terrace Room | 479 |
| 19 | Various | Great Room | 461 |
| 2 | Doltone House Jones Bay Wharf | The Heritage Wharf (Full Pier) | 420 |
| 17 | Various | Avro & Bristol Room | 400 |
| 10 | Various | Studio One | 386 |
| 18 | Various | Grand Ballroom | 376 |
| 17 | Various | Avro Room | 362 |

**Note:** VenueID = 0 is used when no specific venue is assigned.

### 3. Adding Venue to Booking

When creating a booking, set:
```python
VenueID = venue_id  # From tblVenues.ID
VenueRoom = 'Grand Ballroom'  # Room name (optional)
```

**Example:**
```python
cursor.execute('''
    INSERT INTO tblbookings (
        booking_no, order_no, ...
        VenueID, VenueRoom, ...
    ) VALUES (
        %s, %s, ...
        %s, %s, ...
    )
''', (
    booking_no, order_no, ...
    18,  # VenueID
    'Grand Ballroom',  # VenueRoom
    ...
))
```

---

## Creating and Managing Venues

### 1. Create New Venue

**Basic Venue Creation:**
```python
cursor.execute('''
    INSERT INTO tblVenues (
        VenueName, ContactName, ContactID,
        Address1, City, State, Country, ZipCode,
        Phone1CountryCode, Phone1AreaCode, Phone1Digits,
        Type, VenueTextType
    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
''', (
    'New Venue Name',
    'Contact Name',
    contact_id,  # From tblContact.ID
    '123 Main Street',
    'Sydney',
    'NSW',
    'Australia',
    '2000',
    '61',  # Country code
    '2',   # Area code
    '12345678',  # Phone number
    1,  # Type
    'Conference Center'  # VenueTextType
))

# Get the new venue ID
venue_id = cursor.lastrowid if hasattr(cursor, 'lastrowid') else None
# Or query it:
cursor.execute('SELECT TOP 1 ID FROM tblVenues WHERE VenueName = %s', ('New Venue Name',))
venue_result = cursor.fetchone()
venue_id = venue_result['ID']
```

### 2. Add Venue Address

```python
cursor.execute('''
    INSERT INTO tblVenueAddress (
        VenueID, Address1, Address2, City, State, Country, ZipCode
    ) VALUES (%s, %s, %s, %s, %s, %s, %s)
''', (
    venue_id,
    '123 Main Street',
    'Suite 100',
    'Sydney',
    'NSW',
    'Australia',
    '2000'
))
```

### 3. Add Venue Phone

```python
cursor.execute('''
    INSERT INTO tblVenuePhone (
        VenueID, CountryCode, AreaCode, Digits, Extension, PhoneType
    ) VALUES (%s, %s, %s, %s, %s, %s)
''', (
    venue_id,
    '61',  # Country code
    '2',   # Area code
    '12345678',  # Phone number
    '',  # Extension
    'Main'  # PhoneType
))
```

### 4. Link Contact to Venue

```python
cursor.execute('''
    INSERT INTO tblLinkVenueContact (
        VenueID, ContactID
    ) VALUES (%s, %s)
''', (venue_id, contact_id))
```

### 5. Update Venue

```python
cursor.execute('''
    UPDATE tblVenues SET
        VenueName = %s,
        ContactName = %s,
        Address1 = %s,
        City = %s,
        State = %s
    WHERE ID = %s
''', (
    'Updated Venue Name',
    'Updated Contact',
    '456 New Street',
    'Melbourne',
    'VIC',
    venue_id
))
```

### 6. Find Venue by Name

```python
cursor.execute('''
    SELECT ID, VenueName, ContactID
    FROM tblVenues
    WHERE VenueName LIKE %s
''', ('%Venue Name%',))
venues = cursor.fetchall()
```

---

## Complete Examples

### Example 1: Create Complete Venue

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

# 1. Create venue
cursor.execute('''
    INSERT INTO tblVenues (
        VenueName, ContactName, ContactID,
        Address1, City, State, Country, ZipCode,
        Phone1CountryCode, Phone1AreaCode, Phone1Digits,
        Type, VenueTextType
    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
''', (
    'Grand Convention Center',
    'John Smith',
    None,  # ContactID (can be set later)
    '100 Convention Way',
    'Sydney',
    'NSW',
    'Australia',
    '2000',
    '61',
    '2',
    '98765432',
    1,
    'Conference Center'
))

# Get venue ID
cursor.execute('SELECT TOP 1 ID FROM tblVenues WHERE VenueName = %s', ('Grand Convention Center',))
venue_result = cursor.fetchone()
venue_id = venue_result['ID']

# 2. Add address
cursor.execute('''
    INSERT INTO tblVenueAddress (
        VenueID, Address1, City, State, Country, ZipCode
    ) VALUES (%s, %s, %s, %s, %s, %s)
''', (
    venue_id,
    '100 Convention Way',
    'Sydney',
    'NSW',
    'Australia',
    '2000'
))

# 3. Add phone
cursor.execute('''
    INSERT INTO tblVenuePhone (
        VenueID, CountryCode, AreaCode, Digits, PhoneType
    ) VALUES (%s, %s, %s, %s, %s)
''', (
    venue_id,
    '61',
    '2',
    '98765432',
    'Main'
))

# 4. Link contact (if contact exists)
contact_id = 12345  # Example contact ID
cursor.execute('''
    INSERT INTO tblLinkVenueContact (
        VenueID, ContactID
    ) VALUES (%s, %s)
''', (venue_id, contact_id))

conn.commit()
conn.close()

print(f'Venue created: ID {venue_id}')
```

### Example 2: Use Venue in Booking

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

# Find venue
cursor.execute('SELECT ID FROM tblVenues WHERE VenueName = %s', ('Grand Convention Center',))
venue = cursor.fetchone()
venue_id = venue['ID'] if venue else 0

# Create booking with venue
booking_no = 'C14414001'
event_date = datetime(2025, 12, 20)

cursor.execute('''
    INSERT INTO tblbookings (
        booking_no, order_no, booking_type_v32, status, BookingProgressStatus,
        bBookingIsComplete, Sdate, ShowSdate, ShowEdate,
        VenueID, VenueRoom,
        contact_nameV6, OrganizationV6, CustID, ContactID,
        price_quoted, order_date
    ) VALUES (
        %s, %s, %s, %s, %s, %s, %s, %s, %s,
        %s, %s,
        %s, %s, %s, %s,
        %s, %s
    )
''', (
    booking_no, booking_no, 2, 0, 1,
    0, event_date, event_date, event_date,
    venue_id,  # VenueID
    'Grand Ballroom',  # VenueRoom
    'John Doe', 'Test Company', customer_id, contact_id,
    8500.00, datetime.now()
))

conn.commit()
conn.close()
```

### Example 3: Get Venue Information

```python
import pymssql

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB',
    as_dict=True
)
cursor = conn.cursor()

venue_id = 18

# Get venue
cursor.execute('SELECT * FROM tblVenues WHERE ID = %s', (venue_id,))
venue = cursor.fetchone()

if venue:
    print(f"Venue: {venue['VenueName']}")
    print(f"Contact: {venue.get('ContactName', 'N/A')}")
    print(f"Address: {venue.get('Address1', 'N/A')}")
    print(f"City: {venue.get('City', 'N/A')}")
    
    # Get addresses
    cursor.execute('SELECT * FROM tblVenueAddress WHERE VenueID = %s', (venue_id,))
    addresses = cursor.fetchall()
    for addr in addresses:
        print(f"  Address: {addr['Address1']}, {addr['City']}, {addr['State']} {addr['ZipCode']}")
    
    # Get phones
    cursor.execute('SELECT * FROM tblVenuePhone WHERE VenueID = %s', (venue_id,))
    phones = cursor.fetchall()
    for phone in phones:
        print(f"  Phone ({phone['PhoneType']}): {phone['AreaCode']} {phone['Digits']}")
    
    # Get contacts
    cursor.execute('''
        SELECT c.Contactname, c.Cell, c.Email
        FROM tblLinkVenueContact lvc
        JOIN tblContact c ON lvc.ContactID = c.ID
        WHERE lvc.VenueID = %s
    ''', (venue_id,))
    contacts = cursor.fetchall()
    for contact in contacts:
        print(f"  Contact: {contact['Contactname']} | {contact.get('Cell', 'N/A')} | {contact.get('Email', 'N/A')}")

conn.close()
```

### Example 4: List All Venues

```python
import pymssql

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB',
    as_dict=True
)
cursor = conn.cursor()

cursor.execute('''
    SELECT v.ID, v.VenueName, v.City, v.State,
           COUNT(b.ID) as booking_count
    FROM tblVenues v
    LEFT JOIN tblbookings b ON v.ID = b.VenueID
    WHERE v.ID > 0
    GROUP BY v.ID, v.VenueName, v.City, v.State
    ORDER BY COUNT(b.ID) DESC
    LIMIT 20
''')

venues = cursor.fetchall()
for venue in venues:
    print(f"ID: {venue['ID']:5} | {venue['VenueName']:40} | {venue.get('City', 'N/A'):15} | {venue['booking_count']:4} bookings")

conn.close()
```

---

## Venue Items in Inventory

### Venue Products (product_type_v41 = 3)

There are **182 items** in `tblinvmas` with `product_type_v41 = 3` (Venue type).

**Groups:**
- **SALES** - 97 items (consumables, USB drives, etc.)
- **PROJECTS** - 85 items (projection screens, speakers, etc.)

**Categories:**
- CONSUMAB - 87 items
- QSC/QSYS - 33 items
- BIAMP - 7 items
- IPORT - 6 items
- VISIONAR - 6 items
- Others

**Note:** These are NOT the same as venues in `tblVenues`. These are products/equipment items that are categorized as "venue" type, likely for venue-related equipment or consumables.

---

## Summary

### Venue Tables

| Table | Purpose | Key Fields |
|-------|---------|------------|
| `tblVenues` | Main venue information | ID, VenueName, ContactID, Address fields |
| `tblVenueAddress` | Additional addresses | VenueID, Address1, City, State, ZipCode |
| `tblVenuePhone` | Phone numbers | VenueID, CountryCode, AreaCode, Digits, PhoneType |
| `tblLinkVenueContact` | Contact links | VenueID, ContactID |
| `vwAllVenues` | Combined view | All venue information |

### Using Venues

1. **In Bookings:**
   - Set `VenueID` in `tblbookings` (0 = no venue)
   - Set `VenueRoom` for room name

2. **Creating Venues:**
   - Create in `tblVenues`
   - Optionally add addresses, phones, contacts

3. **Finding Venues:**
   - Query `tblVenues` by name or ID
   - Use `vwAllVenues` for combined information

### Key Points

- **VenueID = 0** means no venue assigned
- One venue can have multiple addresses, phones, and contacts
- Venues are linked to bookings via `VenueID` and `VenueRoom`
- **21,379 venues** in the database
- Most used venue ID is 0 (no venue), followed by IDs 19, 17, 10, 18, 2

---

## Database Connection

```python
import pymssql

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB',
    as_dict=True
)
```

---

## Reference Files

- **Schema Files:** `/home/nitwit/INTENT/MicrohireAgentChat/database_schema/`
  - Check for `tblVenues.json` if available
- **Related Guides:**
  - `COMPLETE_BOOKING_CREATION_GUIDE.md` - How to create bookings with venues
  - `EQUIPMENT_LABOR_STRUCTURE_GUIDE.md` - Equipment and labor structure

