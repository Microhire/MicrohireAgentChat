# Production Database Exploration Guide
## Complete Analysis of Bookings, Inventory, Users, Contacts & Equipment

This guide provides a comprehensive understanding of the Microhire production database structure and how to create complete bookings with all components.

## Database Connection

**Production Database (SQL Server):**
- Host: 116.90.5.144:41383
- Database: AITESTDB
- User: PowerBI-Consult
- Tables: ~50+ tables with 68,966 bookings

**Development Database (PostgreSQL):**
- Host: localhost:5432
- Database: postgres
- User: postgres
- Used for testing and development

## Key Tables Overview

### 📊 Database Statistics
- **Total Tables:** 50+ tables across dbo schema
- **Total Bookings:** 68,966 bookings
- **Total Contacts:** 20,757 contact persons
- **Total Customers:** Organizations with full company details
- **Total Equipment Items:** Extensive inventory catalog
- **Total Crew Records:** Labour assignments across bookings

---

## 🔑 Core Tables & Relationships

### 1. tblbookings - Main Bookings Table
**Purpose:** Stores all booking/order details including customer, venue, dates, and financial information

**Key Fields:**
- `ID` (Primary Key)
- `booking_no` - Unique booking number (e.g., C0448500001)
- `contact_namev6`, `organizationv6` - Contact and company names
- `custid`, `contactid` - Foreign keys to customer and contact tables
- `sdate` - Event date
- `venueid`, `venueroom` - Venue location details
- `showstarttime`, `showendtime` - Event timing
- `setuptimev61`, `striketime` - Setup/strike times
- `price_quoted`, `hire_price`, `labour`, `insurance_v5`, `sundry_total` - Financial breakdown
- `days_using`, `expattendees`, `showname` - Event details

**Relationships:**
- → `tblContact` (contactid → ID)
- → `tblcust` (custid → ID)
- → `tblSalesper` (salesperid → ID)
- ← `tblitemtran` (booking_no_v32 → booking_no)
- ← `tblcrew` (booking_no → booking_no)
- ← `tblbooknote` (bookingno → booking_no)

### 2. tblContact - Contact Persons Table
**Purpose:** Stores contact information for individuals

**Key Fields:**
- `ID` (Primary Key)
- `contactname` - Full name
- `firstname`, `surname`
- `email`, `cell` - Contact details
- `position` - Job title
- `CustCodeLink` - Links to customer codes

**Relationships:**
- ← `tblbookings` (contactid → ID)
- ← `tblLinkCustContact` (contactid → ID)

### 3. tblcust - Customer/Organization Table
**Purpose:** Stores customer companies and organizations

**Key Fields:**
- `ID` (Primary Key)
- `Customer_code` - Unique customer code (e.g., C14503)
- `OrganisationV6` - Company name
- `contactV6` - Primary contact name
- `Address_l1V6`, `Address_l2V6`, `Address_l3V6` - Address
- `emailAddress`, `webAddress` - Contact details

**Relationships:**
- ← `tblbookings` (custid → ID)
- ← `tblLinkCustContact` (customer_code → customer_code)

### 4. tblLinkCustContact - Customer-Contact Link Table
**Purpose:** Links contacts to customer organizations (many-to-many)

**Key Fields:**
- `customer_code` (Foreign Key to tblcust)
- `contactid` (Foreign Key to tblContact)

### 5. tblitemtran - Equipment Transactions Table
**Purpose:** Tracks all equipment items in bookings

**Key Fields:**
- `ID` (Primary Key)
- `booking_no_v32` - Links to booking
- `product_code_v42` - Equipment product code
- `item_desc` - Description
- `hire_rate` - Daily rate
- `qty` - Quantity
- `line_total` - Total cost
- `trans_type_v41` - Transaction type

**Relationships:**
- → `tblbookings` (booking_no_v32 → booking_no)
- → `tblinvmas` (product_code_v42 → product_code)

### 6. tblinvmas - Products/Inventory Master Table
**Purpose:** Master list of all products, packages, and equipment

**Key Fields:**
- `ID` (Primary Key)
- `product_code` - Unique product identifier
- `product_name` - Product name
- `hire_rate` - Standard hire rate
- `category`, `subcategory` - Product classification

**Relationships:**
- ← `tblitemtran` (product_code_v42 → product_code)
- ← `vwProdsComponents` (product_code → product_code)

### 7. tblcrew - Labour/Crew Table
**Purpose:** Stores labour assignments for bookings

**Key Fields:**
- `ID` (Primary Key)
- `booking_no` - Links to booking
- `crew_desc` - Description of work
- `taskid` - Labour task type
- `hours` - Total hours
- `rate` - Hourly rate
- `line_total` - Total labour cost

**Relationships:**
- → `tblbookings` (booking_no → booking_no)
- → `tbltask` (taskid → ID)

### 8. tbltask - Labour Tasks Table
**Purpose:** Defines labour task types (setup, pack down, operate, etc.)

**Key Fields:**
- `ID` (Primary Key)
- `task_name` - Task description (e.g., "Setup", "Truck Load Up")
- `TaskType` - Task category

### 9. tblSalesper - Salespersons Table
**Purpose:** Stores sales representative information

**Key Fields:**
- `ID` (Primary Key)
- `salesperson_code` - Unique sales rep code
- `Salesperson_name` - Full name

### 10. tblbooknote - Booking Notes Table
**Purpose:** Stores conversation transcripts and booking notes

**Key Fields:**
- `ID` (Primary Key)
- `bookingNo` - Links to booking
- `text_line` - Note content (can be very long for transcripts)
- `NoteType` - Type of note (1 = transcript)

---

## 🚀 Complete Booking Creation Workflow

### Step 1: Create or Find Contact Person
**Table:** `tblContact`

**Required Fields:**
- `contactname` (full name)
- `firstname`, `surname`
- `email`, `cell` (phone)
- `position` (job title)

**Example:**
```sql
INSERT INTO tblContact (
    contactname, firstname, surname, email, cell, position,
    createdate, lastupdate, lastcontact, active
) VALUES (
    'Michael Knight',
    'Michael',
    'Knight',
    'michael@yes100attendees.com',
    '07111111111',
    'Events Coordinator',
    GETDATE(), GETDATE(), GETDATE(), 1
)
```

### Step 2: Create or Find Customer Organization
**Table:** `tblcust`

**Note:** Most organizations already exist - search first!

**Required Fields:**
- `organisationv6` (company name)
- `customer_code` (unique code like 'C14503')

**Example Search:**
```sql
SELECT id, customer_code, organisationv6
FROM tblcust
WHERE organisationv6 LIKE '%Yes 100 Attendees%'
```

### Step 3: Link Contact to Organization
**Table:** `tblLinkCustContact`

**Required Fields:**
- `customer_code` (from tblcust)
- `contactid` (from tblContact)

**Example:**
```sql
INSERT INTO tblLinkCustContact (customer_code, contactid)
VALUES ('C14503', 12345)
```

### Step 4: Generate Booking Number
**Table:** `tblbookings`

**Pattern:** YYNNNN (e.g., 250001 for fiscal year 2025)

**Logic:** Find highest existing number for current fiscal year and increment

**Example:**
```sql
-- Get next booking number for fiscal year 2025
SELECT '25' + RIGHT('0000' + CAST(
    ISNULL(MAX(CAST(SUBSTRING(booking_no, 3, 4) AS INT)), 0) + 1 AS VARCHAR(4)
), 4) as next_booking_no
FROM tblbookings
WHERE booking_no LIKE '25%'
```

### Step 5: Create Main Booking Record
**Table:** `tblbookings`

**Critical Fields:**
- `booking_no` (generated)
- `contact_namev6`, `organizationv6`
- `custid`, `contactid`
- `sdate` (event date)
- `venueid`, `venueroom`
- `showstarttime`, `showendtime`
- `setuptimev61`, `striketime`
- `price_quoted`, `hire_price`, `labour`, `insurance_v5`, `sundry_total`
- `days_using`, `expattendees`, `showname`

**Example:**
```sql
INSERT INTO tblbookings (
    booking_no, order_no, booking_type_v32, status, bookingprogressstatus,
    bbookingiscomplete, sdate, rdate, setdate, showsdate, showedate, rehdate,
    showstarttime, showendtime, setuptimev61, striketime,
    venueid, venueroom, contact_namev6, organizationv6, custid, contactid,
    price_quoted, hire_price, labour, insurance_v5, sundry_total,
    days_using, expattendees, showname, order_date
) VALUES (
    '250001', '250001', 2, 0, 1,  -- booking basics
    0,  -- bbookingiscomplete
    '2025-03-15', '2025-03-15', '2025-03-15', '2025-03-15', '2025-03-15', '2025-03-15',  -- dates
    '1800', '2200', '0800', '2300',  -- times
    1, 'Main Ballroom',  -- venue
    'Michael Knight', 'Yes 100 Attendees', 123, 456,  -- contact/org
    6900.00, 5200.00, 1600.00, 100.00, 100.00,  -- financial
    1, 100, 'Corporate Event - Yes 100 Attendees', GETDATE()  -- details
)
```

### Step 6: Add Equipment Items
**Table:** `tblitemtran`

**Each equipment item gets its own row**

**Required Fields:**
- `booking_no_v32` (links to booking)
- `product_code_v42` (from tblinvmas)
- `item_desc` (description)
- `hire_rate` (daily rate)
- `qty` (quantity)
- `line_total` (qty × rate)

**Example:**
```sql
INSERT INTO tblitemtran (
    booking_no_v32, product_code_v42, item_desc, hire_rate, qty, line_total,
    trans_type_v41, createdate
) VALUES
('250001', 'SOUNDPKG', 'Sound System Package', 2500.00, 1, 2500.00, 1, GETDATE()),
('250001', 'LIGHTPKG', 'Lighting Setup Package', 1800.00, 1, 1800.00, 1, GETDATE()),
('250001', 'STAGEPKG', 'Staging Package', 900.00, 1, 900.00, 1, GETDATE())
```

### Step 7: Add Labour/Crew Assignments
**Table:** `tblcrew`

**Each labour requirement gets its own row**

**Required Fields:**
- `booking_no` (links to booking)
- `crew_desc` (description of work)
- `taskid` (from tbltask - setup, operate, strike)
- `hours` (total labour hours)
- `rate` (hourly rate)
- `line_total` (hours × rate)

**Example:**
```sql
INSERT INTO tblcrew (
    booking_no, crew_desc, hours, rate, line_total, createdate
) VALUES
('250001', 'Setup Crew (2 people, 2 hours)', 4, 100.00, 400.00, GETDATE()),
('250001', 'Show Crew (3 people, 4 hours)', 12, 200.00, 2400.00, GETDATE()),
('250001', 'Strike Crew (2 people, 1 hour)', 2, 100.00, 200.00, GETDATE())
```

### Step 8: Add Conversation Transcript
**Table:** `tblbooknote`

**Store the complete chat conversation**

**Required Fields:**
- `bookingNo` (booking number)
- `textline` (full transcript - can be very long)
- `notetype` (1 for transcripts)
- `createdate`

**Example:**
```sql
INSERT INTO tblbooknote (
    bookingNo, textline, notetype, createdate
) VALUES (
    '250001',
    '=== CONVERSATION TRANSCRIPT ===
[2025-01-20 10:00:00] USER: Hi, I need to book equipment...
[2025-01-20 10:01:15] USER: Yes, please create the booking.',
    1, GETDATE()
)
```

---

## 📋 Sample Data Insights

### Recent Booking Example:
- **Booking:** C0448500001
- **Contact:** Various contacts
- **Organization:** Multiple organizations
- **Equipment:** Sound systems, lighting, staging
- **Crew:** Setup, operating, strike crews
- **Financial:** $6,900+ total quotes with labour breakdowns

### Equipment Categories:
- **Sound Systems:** Packages with microphones, speakers, mixers
- **Lighting:** LED setups, intelligent lights
- **Staging:** Theatres, cocktail squares, trestle tables
- **AV Equipment:** Projectors, screens, computers

### Labour Task Types:
- Truck Load Up/Unload
- Setup
- Operating
- Pack Down/Strike
- Technical support

---

## 🔍 Key Relationships Summary

```
tblbookings (Central)
├──→ tblContact (contact person)
├──→ tblcust (customer organization)
├──→ tblSalesper (sales rep)
├──← tblitemtran (equipment items)
├──← tblcrew (labour assignments)
└──← tblbooknote (transcripts/notes)

tblitemtran
├──→ tblbookings
└──→ tblinvmas (equipment catalog)

tblcrew
├──→ tblbookings
└──→ tbltask (labour task types)

tblLinkCustContact
├──→ tblcust
└──→ tblContact
```

---

## 🛠️ Usage Scripts

The comprehensive exploration script `comprehensive_db_explorer.py` provides:
- Complete table analysis with columns, keys, and relationships
- Sample data from all key tables
- Step-by-step booking creation guide
- Relationship mapping
- JSON export of full analysis

**Run the explorer:**
```bash
cd /home/nitwit/INTENT/MicrohireAgentChat
source db_venv/bin/activate
python comprehensive_db_explorer.py
```

This guide provides everything needed to understand and work with the production database for creating complete bookings with contacts, equipment, venues, and crew assignments.
