# Equipment and Labor Structure Guide

## Overview
This guide documents the complete structure of equipment, labor, and packages in the AITESTDB database, including how to add them to bookings.

---

## Table of Contents
1. [Product Types](#product-types)
2. [Equipment Structure](#equipment-structure)
3. [Labor Structure](#labor-structure)
4. [Package Structure](#package-structure)
5. [Adding Equipment to Bookings](#adding-equipment-to-bookings)
6. [Adding Labor to Bookings](#adding-labor-to-bookings)
7. [Complete Examples](#complete-examples)

---

## Product Types

All products are stored in the `tblinvmas` table with a `product_type_v41` field:

| Type | Name | Count | Description |
|------|------|-------|-------------|
| 0 | Hire Equipment | 4,575 | Individual rental items |
| 1 | Labor/Service | 38 | Labor and service items |
| 2 | Packages | 0 | (Not used - packages are defined via components) |
| 3 | Venues | 182 | Venue items |

---

## Equipment Structure

### 1. Equipment Table: `tblinvmas`

**Key Fields:**
- `product_code` - Unique product code (e.g., "FOLD2.4", "EVOX8")
- `descriptionV6` - Product description
- `groupFld` - Equipment group/category
- `category` - Primary category
- `SubCategory` - Sub-category
- `product_type_v41` - Type (0 = equipment)
- `retail_price` - Retail/hire price
- `cost_price` - Cost price
- `on_hand` - Stock on hand
- `asset_track` - Whether item is asset tracked ('Y'/'N')
- `components_del` - Has components for delivery ('Y'/'N')
- `components_inv` - Has components for invoice ('Y'/'N')
- `components_quote` - Has components for quote ('Y'/'N')

### 2. Equipment Groups (Top 10)

| Group | Item Count | Description |
|-------|------------|-------------|
| VENUE | 1,145 | Venue-related items |
| VISION | 728 | Video/projection equipment |
| AUDIO | 626 | Audio equipment |
| CABLE | 420 | Cables and connectors |
| COMPUTER | 408 | Computer equipment |
| LIGHTING | 302 | Lighting equipment |
| RIGGING | 200 | Rigging equipment |
| STAGING | 167 | Stage equipment |
| LED-WALL | 117 | LED wall panels |
| TOOLS | 98 | Tools and accessories |

### 3. Sample Equipment Items

```
Product Code         Description                          Group           Price    Stock
1.0MLADD             1.0M Step Ladder                     TOOLS           $0.00    6
FOLD2.4              Megadeck Stage Riser Foldaway 2.4m   STAGING         $0.00    5
EVOX8                EVOX TWO-WAY ARRAY                   AUDIO           $0.00    2
VICFB32              VIC 32" Foldback Package             VISION          $0.00    2
```

**Note:** Most items show $0.00 in `retail_price` - actual pricing may be calculated dynamically or stored elsewhere.

### 4. Equipment Categories

Equipment is organized hierarchically:
- **Group** (e.g., AUDIO, VISION, LIGHTING)
  - **Category** (e.g., SPEAKER, PROJECTOR, LED)
    - **SubCategory** (e.g., ACTIVE, PASSIVE, WIRELESS)

---

## Labor Structure

### 1. Labor Items Table: `tblinvmas`

**Labor items have `product_type_v41 = 1`**

**Key Fields:**
- `product_code` - Labor code (e.g., "AVTECH", "DRIVER")
- `descriptionV6` - Labor description
- `groupFld` - Labor group (mostly "AVTECHS")
- `DefaultDayRateID` - Default day rate ID
- `DefaultHourlyRateID` - Default hourly rate ID
- `person_required` - Whether person is required ('Y'/'N')

### 2. Labor Items (38 total)

| Product Code | Description | Group |
|--------------|-------------|-------|
| AHSTECH | After Hours Service Technician | AVTECHS |
| AVTECH | AV Technician | AVTECHS |
| AXDIR | Audio Director | AVTECHS |
| AXTECH | Audio Technician | AVTECHS |
| CAMERMAN | Camera Operator | AVTECHS |
| CMSGTECH | Commissioning Technician | AVTECHS |
| CREATSET | Creative Technician | AVTECHS |
| CREWLEAD | Crew Chief/Team Leader | AVTECHS |
| DRIVER | Driver | AVTECHS |
| EXPOTECH | Exhibition Technician | AVTECHS |
| INSTTECH | Install Technician | AVTECHS |
| ITTECH | IT Technician | AVTECHS |
| LIGFSO | Follow Spot Operator | AVTECHS |
| LXDIR | Lighting Director | AVTECHS |
| LXTECH | Lighting Technician | AVTECHS |
| PRJTMAN | Project Manager | AVTECHS |
| RIGTECH | Rigging Technician | AVTECHS |
| SAVTECH | Senior AV Technician | AVTECHS |
| STAGEHAND | Stage Hand | AVTECHS |
| VXDIR | Video Director | AVTECHS |
| VXTECH | Video Technician | AVTECHS |

### 3. Labor Rates Table: `tblInvmas_Labour_Rates`

Labor rates are stored separately from the labor items.

**Key Fields:**
- `tblInvmasID` - Links to `tblinvmas.ID`
- `rate_no` - Rate number (1, 2, 3, etc.)
- `Labour_rate` - Rate amount
- `Locn` - Location ID
- `IsDefault` - Whether this is the default rate

**Example Rates:**
```
Product Code    Rate No    Amount
AHSTECH         1          $135.00
AVTECH          1          $110.00
AVTECH          2          $165.00
SAVTECH         1          $125.00
SAVTECH         2          $187.50
DRIVER          1          $110.00
```

**Rate Structure:**
- Rate 1: Standard rate
- Rate 2: Overtime/premium rate (typically 1.5x)
- Rate 3: Double time rate (typically 2x)

**Typical Rate Ranges:**
- $110 - $135: Standard technicians, drivers
- $165 - $217: Senior technicians, specialists
- Rates vary by location (`Locn` field)

---

## Package Structure

### 1. Packages Overview

**Total Packages:** 1,818 packages with components

Packages are NOT defined by `product_type_v41 = 2`. Instead, they are regular items (Type 0) that have components defined in the `vwProdsComponents` view.

### 2. Package Components View: `vwProdsComponents`

**Key Fields:**
- `parent_code` - Package product code
- `product_code` - Component product code
- `qty_v5` - Quantity of component in package
- `sub_seq_no` - Sequence number
- `variable_part` - Whether component is variable
- `SelectComp` - Whether component is selectable

### 3. How Packages Work

A package is an item in `tblinvmas` that has:
- `components_del = 'Y'` - Components expand on delivery
- `components_inv = 'Y'` - Components expand on invoice
- `components_quote = 'Y'` - Components expand on quote

When you add a package to a booking, the system can:
1. Add just the package (collapsed)
2. Add the package and all its components (expanded)

### 4. Package Example

**Package:** `PANA12K` - 12K Lumen DLP - Panasonic PT-RZ120

**Components:**
```
Component Code          Qty    Description
NSWTPROJ                1      NSW Projector Tower Package
PPRZ120                 1      Projector Plate Panasonic RZ120/RZ670/RW630
PT-DZ870                1      Projector 8500 lumen HD - Panasonic PT-DZ870EK
DROP0.5M                1      0.5m Dropper Bar with T.V. Clamp
REMPANA                 1      Panasonic Projector Remotes
PT-DZ21K                1      20k ANSI Lumen WUXGA DLP Projector PT-DZ21K2E
DROP1.0M                1      1.0m Dropper Bar with T.V. Clamp
SAFE100K                1      Safety Cable w/ Carabiner 4mm 100KG
HD10K-M                 1      Projector HD 10K Lumen - Christie HD10K M-Series
TKGF0156                1      Panasonic 870EK/770/970 1.7-2.4:1 STD Throw Lens
ETDLE080                1      Panasonic ET-DLE080 0.8 - 1.0:1 Short Throw Lens
ETDLE105                1      Panasonic ET-DLE105 1.0-1.3:1 Short Throw Lens
ETDLE150                1      Panasonic ET-DLE150 1.4-2.0:1 Short Throw Lens
ETDLE250                1      Panasonic ET-DLE250 2.4-3.8:1 Long Throw Lens
ETDLE350                1      Panasonic ET-DLE350 3.8-5.7:1 Long Throw Lens
ETDLE450                1      Panasonic ET-DLE450 5.6-9.0:1 Long Throw Lens
PANA12FM                1      Panasonic 12K Projector Stacking Frame
REUTL2M                 4      Reutlinger 2m c/w Carabiner
```

### 5. Nested Packages

Packages can contain other packages (nested components). The `nestedCompAcc` field indicates nested components.

---

## Adding Equipment to Bookings

### 1. Equipment Items Table: `tblitemtran`

**Key Fields:**
- `booking_no_v32` - Booking number
- `booking_id` - Booking ID
- `product_code_v42` - Product code (can be NULL for custom items)
- `Comment_desc_v42` - Item description
- `trans_qty` - Quantity
- `price` - Item price
- `heading_no` - Heading/section number
- `seq_no` - Sequence number
- `sub_seq_no` - Sub-sequence number
- `trans_type_v41` - Transaction type (1 = hire, 12 = sale, etc.)
- `item_type` - Item type (0 = equipment, 1 = labor, 2 = other)
- `AvailRecFlag` - Availability record flag
- `AssignType` - Assignment type
- `QtyShort` - Quantity short
- `createdate` - Creation date

### 2. Transaction Types (`trans_type_v41`)

| Type | Description |
|------|-------------|
| 1 | Hire/Rental |
| 12 | Sale |
| Others | Various transaction types |

### 3. Adding Equipment - SQL Example

```python
cursor.execute('''
    INSERT INTO tblitemtran (
        booking_no_v32, booking_id,
        heading_no, seq_no, sub_seq_no,
        product_code_v42, Comment_desc_v42,
        trans_qty, price,
        trans_type_v41, item_type,
        AvailRecFlag, AssignType, QtyShort,
        createdate
    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
''', (
    booking_no,           # Booking number
    booking_id,           # Booking ID
    1,                    # heading_no (usually 1)
    1,                    # seq_no (usually 1)
    sub_seq,              # sub_seq_no (increments for each item)
    'FOLD2.4',           # product_code_v42
    'Megadeck Stage Riser Foldaway 2.4m',  # Comment_desc_v42
    5,                    # trans_qty
    900.00,               # price
    1,                    # trans_type_v41 (1 = hire)
    0,                    # item_type (0 = equipment)
    False,                # AvailRecFlag
    0,                    # AssignType
    0,                    # QtyShort
    datetime.now()        # createdate
))
```

### 4. Adding Custom Items (No Product Code)

You can add items without a product code by setting `product_code_v42 = NULL`:

```python
cursor.execute('''
    INSERT INTO tblitemtran (
        booking_no_v32, booking_id,
        heading_no, seq_no, sub_seq_no,
        product_code_v42, Comment_desc_v42,
        trans_qty, price,
        trans_type_v41, item_type,
        createdate
    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
''', (
    booking_no, booking_id,
    1, 1, sub_seq,
    None,  # No product code
    'Custom staging setup - 6m x 2.4m',
    1, 1500.00,
    1, 0,
    datetime.now()
))
```

### 5. Sequence Numbering

Equipment items use a hierarchical sequence:
- `heading_no` - Groups items into sections (usually 1)
- `seq_no` - Main sequence (usually 1 for equipment)
- `sub_seq_no` - Sub-sequence (increments: 1, 2, 3, ...)

Example:
```
heading_no  seq_no  sub_seq_no  Item
1           1       1           First item
1           1       2           Second item
1           1       3           Third item
```

---

## Adding Labor to Bookings

### 1. Crew Table: `tblcrew`

**Key Fields:**
- `booking_no_v32` - Booking number
- `heading_no` - Heading number (usually 0 for crew)
- `seq_no` - Sequence number (usually starts at 65530)
- `sub_seq_no` - Sub-sequence (usually 0)
- `product_code_v42` - Labor product code (e.g., "AVTECH", "DRIVER")
- `price` - Labor rate
- `hours` - Hours worked
- `trans_qty` - Quantity (usually 1)
- `rate_selected` - Rate number selected
- `del_time_hour` - Delivery time hour
- `del_time_min` - Delivery time minute
- `return_time_hour` - Return time hour
- `return_time_min` - Return time minute
- `createdate` - Creation date

### 2. Adding Labor - SQL Example

```python
cursor.execute('''
    INSERT INTO tblcrew (
        booking_no_v32,
        heading_no, seq_no, sub_seq_no,
        product_code_v42,
        price, hours, trans_qty,
        createdate
    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
''', (
    booking_no,          # Booking number
    0,                   # heading_no (0 for crew)
    65530,               # seq_no (start at 65530)
    0,                   # sub_seq_no
    'AVTECH',           # product_code_v42
    110.00,              # price (rate per hour)
    8,                   # hours
    1,                   # trans_qty
    datetime.now()       # createdate
))
```

### 3. Crew Sequence Numbering

Crew items use a different sequence range:
- `heading_no` - Usually 0 for crew
- `seq_no` - Starts at 65530 and increments (65530, 65531, 65532, ...)
- `sub_seq_no` - Usually 0

Example:
```
heading_no  seq_no  sub_seq_no  Labor Item
0           65530   0           DRIVER (1 hour)
0           65531   0           AVTECH (2 hours)
0           65532   0           SAVTECH (6 hours)
0           65533   0           AVTECH (1 hour)
0           65534   0           DRIVER (1 hour)
```

### 4. Getting Labor Rates

To get the correct rate for a labor item:

```python
# Get default rate for a labor item
cursor.execute('''
    SELECT lr.Labour_rate, lr.rate_no
    FROM tblInvmas_Labour_Rates lr
    JOIN tblinvmas i ON lr.tblInvmasID = i.ID
    WHERE i.product_code = %s AND lr.IsDefault = 1
''', ('AVTECH',))
rate_info = cursor.fetchone()
labor_rate = rate_info['Labour_rate']  # e.g., 110.00
```

Or get all rates:

```python
# Get all rates for a labor item
cursor.execute('''
    SELECT lr.rate_no, lr.Labour_rate
    FROM tblInvmas_Labour_Rates lr
    JOIN tblinvmas i ON lr.tblInvmasID = i.ID
    WHERE i.product_code = %s
    ORDER BY lr.rate_no
''', ('AVTECH',))
rates = cursor.fetchall()
# rates = [{'rate_no': 1, 'Labour_rate': 110.00}, {'rate_no': 2, 'Labour_rate': 165.00}]
```

---

## Complete Examples

### Example 1: Add Equipment to Booking

```python
import pymssql
from datetime import datetime

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB'
)
cursor = conn.cursor()

booking_no = 'C14414001'
booking_id = 70305

# Add equipment items
equipment_items = [
    ('FOLD2.4', 'Megadeck Stage Riser Foldaway 2.4m', 5, 900.00),
    ('EVOX8', 'EVOX TWO-WAY ARRAY', 2, 206.00),
    ('VICFB32', 'VIC 32" Foldback Package', 2, 618.00),
]

for i, (product_code, description, qty, price) in enumerate(equipment_items, 1):
    cursor.execute('''
        INSERT INTO tblitemtran (
            booking_no_v32, booking_id,
            heading_no, seq_no, sub_seq_no,
            product_code_v42, Comment_desc_v42,
            trans_qty, price,
            trans_type_v41, item_type,
            createdate
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
    ''', (
        booking_no, booking_id,
        1, 1, i,
        product_code, description,
        qty, price,
        1, 0,
        datetime.now()
    ))

conn.commit()
conn.close()
```

### Example 2: Add Labor to Booking

```python
import pymssql
from datetime import datetime

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB'
)
cursor = conn.cursor()

booking_no = 'C14414001'

# Add crew items
crew_items = [
    ('DRIVER', 110.00, 1),
    ('AVTECH', 110.00, 2),
    ('SAVTECH', 125.00, 6),
    ('AVTECH', 110.00, 1),
    ('DRIVER', 110.00, 1),
]

for i, (product_code, rate, hours) in enumerate(crew_items):
    cursor.execute('''
        INSERT INTO tblcrew (
            booking_no_v32,
            heading_no, seq_no, sub_seq_no,
            product_code_v42,
            price, hours, trans_qty,
            createdate
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
    ''', (
        booking_no,
        0, 65530 + i, 0,
        product_code,
        rate, hours, 1,
        datetime.now()
    ))

conn.commit()
conn.close()
```

### Example 3: Add Package to Booking (Expanded)

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

booking_no = 'C14414001'
booking_id = 70305
package_code = 'PANA12K'

# Get package components
cursor.execute('''
    SELECT pc.product_code, c.descriptionV6, pc.qty_v5
    FROM vwProdsComponents pc
    LEFT JOIN tblinvmas c ON pc.product_code = c.product_code
    WHERE pc.parent_code = %s
    ORDER BY pc.sub_seq_no
''', (package_code,))
components = cursor.fetchall()

# Add each component to booking
for i, comp in enumerate(components, 1):
    cursor.execute('''
        INSERT INTO tblitemtran (
            booking_no_v32, booking_id,
            heading_no, seq_no, sub_seq_no,
            product_code_v42, Comment_desc_v42,
            trans_qty, price,
            trans_type_v41, item_type,
            createdate
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
    ''', (
        booking_no, booking_id,
        1, 1, i,
        comp['product_code'], comp['descriptionV6'],
        comp['qty_v5'], 0.00,  # Price would need to be calculated
        1, 0,
        datetime.now()
    ))

conn.commit()
conn.close()
```

---

## Summary

### Equipment
- **Table:** `tblinvmas` (Type 0)
- **Count:** 4,575 items
- **Organization:** Groups, Categories, SubCategories
- **Pricing:** `retail_price`, `cost_price`
- **Add to Booking:** `tblitemtran` table

### Labor
- **Table:** `tblinvmas` (Type 1)
- **Count:** 38 items
- **Rates:** `tblInvmas_Labour_Rates` table
- **Rate Types:** Standard, Overtime, Double Time
- **Add to Booking:** `tblcrew` table

### Packages
- **Definition:** `vwProdsComponents` view
- **Count:** 1,818 packages
- **Structure:** parent_code → product_code + qty_v5
- **Can be nested:** Packages can contain packages

### Key Differences

| Aspect | Equipment | Labor |
|--------|-----------|-------|
| Table for booking | `tblitemtran` | `tblcrew` |
| Product code field | `product_code_v42` | `product_code_v42` |
| Quantity field | `trans_qty` | `trans_qty` |
| Price field | `price` | `price` (rate) |
| Time field | N/A | `hours` |
| Sequence start | seq_no = 1 | seq_no = 65530 |
| Heading number | Usually 1 | Usually 0 |

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

- **Equipment Analysis:** `/home/nitwit/INTENT/MicrohireAgentChat/analyze_equipment_labor.py`
- **Schema Files:** `/home/nitwit/INTENT/MicrohireAgentChat/database_schema/`
  - `tblinvmas.json` - Equipment/labor master table
  - `tblInvmas_Labour_Rates.json` - Labor rates
  - `vwProdsComponents.json` - Package components
  - `tblitemtran.json` - Equipment items in bookings
  - `tblcrew.json` - Crew/labor in bookings

