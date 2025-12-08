# Booking Structure Analysis

## Issue
The booking created by the script doesn't appear in your inventory management tool because:
1. **Your tool connects to SQL Server** (where booking `C1374900080` exists)
2. **The script inserts into PostgreSQL** (which has a simplified schema)
3. **Missing required fields** that the tool expects to display bookings properly

## What to Check in Booking C1374900080

Run these queries against your **SQL Server production database** to see the proper structure:

### 1. Booking Main Record
```sql
SELECT 
    booking_no, order_no, CustCode, contact_nameV6, OrganizationV6,
    VenueID, VenueRoom, status, booking_type_v32, BookingProgressStatus,
    price_quoted, hire_price, labour, insurance_v5, sundry_total,
    SDate, ShowSDate, ShowEdate, SetDate, RehDate,
    showStartTime, ShowEndTime, setupTimeV61, StrikeTime,
    expAttendees, showName, order_date, ID
FROM tblbookings
WHERE booking_no = 'C1374900080' OR CustCode = 'C1374900080'
```

### 2. Equipment Items Structure (CRITICAL)
```sql
SELECT 
    ID, booking_no_v32, heading_no, seq_no, sub_seq_no,
    trans_type_v41, product_code_v42, Comment_desc_v42,
    trans_qty, price, unitRate, item_type, days_using,
    PackageLevel, ParentCode, GroupSeqNo,
    booking_id, AvailRecFlag, AssignType, QtyShort,
    View_Logi, View_client, FirstDate, RetnDate
FROM tblitemtran
WHERE booking_no_v32 = 'C1374900080'
ORDER BY heading_no, seq_no, sub_seq_no
```

**Key Fields for Equipment Items:**
- `product_code_v42` (char(30)) - **REQUIRED**: Product code like "SOUNDPKG", "LIGHTPKG"
- `Comment_desc_v42` (char(70)) - Description
- `trans_qty` (decimal) - Quantity
- `price` (float) - Price per unit
- `unitRate` (float) - Unit rate
- `heading_no` (tinyint) - Grouping/heading (usually 0)
- `seq_no` (decimal) - Sequence number (1, 2, 3...)
- `sub_seq_no` (int) - Sub-sequence (usually 0 for top-level items)
- `trans_type_v41` (tinyint) - Transaction type (12 = rental)
- `item_type` (tinyint) - Item type (1 = equipment)
- `booking_id` (int) - **REQUIRED**: Links to tblbookings.ID
- `AvailRecFlag` (bit) - **REQUIRED**: Must be True/1
- `AssignType` (tinyint) - **REQUIRED**: Usually 0
- `QtyShort` (int) - **REQUIRED**: Usually 0
- `PackageLevel` (smallint) - For packages (NULL for regular items, 1+ for package components)
- `ParentCode` (varchar(30)) - For package components (NULL for top-level)

### 3. Package Structure
If booking C1374900080 has packages, check:
```sql
SELECT 
    i.product_code_v42, i.Comment_desc_v42, i.PackageLevel, i.ParentCode,
    i.trans_qty, i.price, i.seq_no, i.sub_seq_no
FROM tblitemtran i
WHERE i.booking_no_v32 = 'C1374900080'
ORDER BY i.PackageLevel NULLS LAST, i.seq_no, i.sub_seq_no
```

**Package Structure:**
- Top-level package: `PackageLevel = NULL`, `ParentCode = NULL`
- Package components: `PackageLevel = 1` (or higher), `ParentCode = <package_code>`

### 4. Crew Items
```sql
SELECT 
    ID, booking_no, crew_desc, hours, rate, line_total,
    createdate
FROM tblcrew
WHERE booking_no = 'C1374900080'
ORDER BY ID
```

## Required Fields Summary

### For tblbookings:
- `booking_no` - Booking number
- `order_no` - Order number (can be same as booking_no)
- `CustCode` - Customer code (e.g., "C14503")
- `contact_nameV6` - Contact name
- `OrganizationV6` - Organization name
- `VenueID` - **REQUIRED**: Must be valid venue ID (NOT NULL)
- `status` - Status (usually 0)
- `booking_type_v32` - Booking type (usually 2)
- `BookingProgressStatus` - Progress status (usually 1)
- `order_date` - Order date

### For tblitemtran:
- `booking_no_v32` - Booking number
- `product_code_v42` - **REQUIRED**: Product code (char(30))
- `Comment_desc_v42` - Description (char(70))
- `trans_qty` - Quantity
- `price` - Price
- `unitRate` - Unit rate
- `heading_no` - Heading number (usually 0)
- `seq_no` - Sequence number
- `sub_seq_no` - Sub-sequence (usually 0)
- `trans_type_v41` - Transaction type (12 for rental)
- `item_type` - Item type (1 for equipment)
- `booking_id` - **REQUIRED**: Links to tblbookings.ID
- `AvailRecFlag` - **REQUIRED**: Must be True/1
- `AssignType` - **REQUIRED**: Usually 0
- `QtyShort` - **REQUIRED**: Usually 0
- `View_Logi` - Usually True
- `View_client` - Usually True
- `FirstDate` - First date
- `RetnDate` - Return date

## Next Steps

1. **Query booking C1374900080** in SQL Server to see the exact structure
2. **Update the insert script** to match that structure exactly
3. **Insert into SQL Server** (not PostgreSQL) if your tool connects to SQL Server
4. **Verify all required fields** are populated, especially:
   - `VenueID` (NOT NULL)
   - `product_code_v42` (NOT NULL)
   - `booking_id` (links to booking)
   - `AvailRecFlag`, `AssignType`, `QtyShort` (required flags)

## Tool Display Columns

Based on your tool's display, it shows:
- Code (likely `booking_no` or `CustCode`)
- Organisation (`OrganizationV6`)
- Out Date and Time (`SDate` + delivery times)
- In Date and Time (`rDate` + return times)
- Booking Type (`booking_type_v32`)
- Date Added (`order_date`)
- Progress Status (`BookingProgressStatus`)
- Show Name (`showName`)
- Project (possibly `showName` or custom field)
- Invoice # (`invoice_no`)
- PO No. (possibly `order_no`)
- Stat (`status`)
- Salesperson (`Salesperson`)
- Project Manager (`ProjectManager`)
- Revenue (`price_quoted`)
- Venue (`VenueID` + `VenueRoom`)
- Workflow (possibly `BookingProgressStatus`)
- Payment Terms (possibly custom field)

Make sure all these fields are populated for the booking to display properly!


