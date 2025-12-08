# Booking Creation Guide

## Overview

This document explains how to create bookings that will appear in your inventory management tool. We've created several scripts to help you understand the database structure and create properly formatted bookings.

## Database Structure Analysis

We analyzed the database structure and found that:

1. Your tool connects to **SQL Server** but our test scripts were inserting into **PostgreSQL**
2. PostgreSQL has a **simplified schema** compared to SQL Server
3. **Missing required fields** prevented bookings from appearing in your tool

### Key Required Fields

For a booking to appear in your tool, these fields are critical:

#### For tblbookings:
- `booking_no` - Booking number
- `CustCode` - Customer code (e.g., "C14503")
- `VenueID` - Must be a valid venue ID (NOT NULL)
- `contact_nameV6` - Contact name
- `OrganizationV6` - Organization name
- `order_date` - Order date

#### For tblitemtran (equipment items):
- `booking_no_v32` - Booking number
- `product_code_v42` - Product code (char(30))
- `booking_id` - Links to tblbookings.ID
- `AvailRecFlag` - Must be True/1
- `AssignType` - Usually 0
- `QtyShort` - Usually 0

## Scripts Created

We've created several scripts to help you:

1. **explore_bookings.py** - Explores PostgreSQL database structure
2. **analyze_sql_server_booking.py** - Analyzes SQL Server booking C1374900080 (update connection details)
3. **insert_booking_complete.py** - Inserts a complete booking into PostgreSQL with all fields
4. **insert_booking_sql_server.py** - Inserts a complete booking into SQL Server (update connection details)

## How to Use These Scripts

### 1. Explore PostgreSQL Structure
```bash
python explore_bookings.py
```
This shows the structure of bookings in PostgreSQL.

### 2. Analyze SQL Server Booking
Update the connection details in `analyze_sql_server_booking.py`, then run:
```bash
python analyze_sql_server_booking.py
```
This will analyze booking C1374900080 in SQL Server.

### 3. Insert Test Booking into PostgreSQL
```bash
python insert_booking_complete.py
```
This creates a booking in PostgreSQL with all required fields.

### 4. Insert Production Booking into SQL Server
Update the connection details in `insert_booking_sql_server.py`, then run:
```bash
python insert_booking_sql_server.py
```
This creates a booking in SQL Server that will appear in your tool.

## SQL Server Connection Setup

To connect to SQL Server, you need:

1. Install pyodbc:
```bash
pip install pyodbc
```

2. Install SQL Server ODBC drivers:
   - On Windows: Install Microsoft ODBC Driver for SQL Server
   - On Linux: Follow [Microsoft's guide](https://docs.microsoft.com/en-us/sql/connect/odbc/linux-mac/installing-the-microsoft-odbc-driver-for-sql-server)

3. Update the connection details in the scripts:
```python
server = 'YOUR_SQL_SERVER'  # e.g., 'localhost\\SQLEXPRESS'
database = 'YOUR_DATABASE'  # e.g., 'Microhire'
username = 'YOUR_USERNAME'  # e.g., 'sa'
password = 'YOUR_PASSWORD'  # e.g., 'password'
```

## Troubleshooting

If bookings don't appear in your tool, check:

1. **Connection** - Make sure you're inserting into the same database your tool connects to
2. **Required Fields** - Check that all required fields are populated correctly
3. **VenueID** - Must be a valid venue ID in your database
4. **CustCode** - Must be correctly formatted (e.g., "C14503")
5. **Equipment Items** - Must have product_code_v42, booking_id, and AvailRecFlag set

## Conclusion

The issue was that our test scripts were inserting into PostgreSQL with a simplified schema, while your tool connects to SQL Server with a more complex schema. By using the SQL Server script with all required fields, your bookings should now appear in your tool.

For any further questions or issues, refer to the analysis in `BOOKING_STRUCTURE_ANALYSIS.md` and the SQL Server script in `insert_booking_sql_server.py`.

