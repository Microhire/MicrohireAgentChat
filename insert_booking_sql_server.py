#!/usr/bin/env python3
"""
Insert complete booking data into SQL Server with all required fields.
This script creates a booking that will appear in your inventory management tool.

Requirements:
1. Install pyodbc: pip install pyodbc
2. Install SQL Server ODBC drivers
3. Update the connection details below with your SQL Server information
"""

import pyodbc
import json
from datetime import datetime

def get_sql_server_connection():
    """
    Get SQL Server connection.
    Replace these values with your actual SQL Server connection details.
    """
    server = 'YOUR_SQL_SERVER'  # e.g., 'localhost\\SQLEXPRESS'
    database = 'YOUR_DATABASE'  # e.g., 'Microhire'
    username = 'YOUR_USERNAME'  # e.g., 'sa'
    password = 'YOUR_PASSWORD'  # e.g., 'password'
    
    conn_str = f'DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server};DATABASE={database};UID={username};PWD={password}'
    
    try:
        return pyodbc.connect(conn_str)
    except Exception as e:
        print(f"❌ SQL Server connection error: {e}")
        return None

def insert_complete_booking():
    """Insert complete booking data with all required fields"""
    conn = get_sql_server_connection()
    if not conn:
        return False
    
    cursor = conn.cursor()
    
    try:
        now = datetime.now()
        print("=== INSERTING COMPLETE BOOKING INTO SQL SERVER ===\n")
        
        # 1. Check if contact exists
        print("1. Checking/creating contact...")
        cursor.execute("""
            SELECT id FROM tblContact WHERE email = ?
        """, ('michael@yes100attendees.com',))
        contact_row = cursor.fetchone()
        
        if contact_row:
            contact_id = contact_row[0]
            print(f"   ✓ Contact exists: ID={contact_id}")
            
            # Update contact with latest info
            cursor.execute("""
                UPDATE tblContact SET
                    contactname = ?,
                    firstname = ?,
                    surname = ?,
                    email = ?,
                    cell = ?,
                    position = ?,
                    lastupdate = ?,
                    lastcontact = ?
                WHERE id = ?
            """, (
                'Michael Knight',
                'Michael',
                'Knight',
                'michael@yes100attendees.com',
                '07111111111',
                'Events Coordinator',
                now,
                now,
                contact_id
            ))
        else:
            # Insert new contact
            cursor.execute("""
                INSERT INTO tblContact (
                    contactname, firstname, surname, email, cell, position,
                    createdate, lastupdate, lastcontact, active
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                'Michael Knight',
                'Michael',
                'Knight',
                'michael@yes100attendees.com',
                '07111111111',
                'Events Coordinator',
                now, now, now, 1  # 1 = True in SQL Server bit field
            ))
            cursor.execute("SELECT @@IDENTITY")
            contact_id = int(cursor.fetchone()[0])
            print(f"   ✓ Created contact: ID={contact_id}")
        
        # 2. Check if organization exists
        print("2. Checking organization...")
        cursor.execute("""
            SELECT id, customer_code FROM tblcust WHERE organisationv6 = ?
        """, ('Yes 100 Attendees',))
        org_row = cursor.fetchone()
        
        if org_row:
            org_id = org_row[0]
            customer_code = org_row[1]
            print(f"   ✓ Organization exists: ID={org_id}, Code={customer_code}")
        else:
            print("   ⚠ Organization 'Yes 100 Attendees' not found - creating it")
            # Create organization with customer code
            customer_code = 'C14503'
            cursor.execute("""
                INSERT INTO tblcust (
                    organisationv6, customer_code, createdate
                ) VALUES (?, ?, ?)
            """, (
                'Yes 100 Attendees',
                customer_code,
                now
            ))
            cursor.execute("SELECT @@IDENTITY")
            org_id = int(cursor.fetchone()[0])
            print(f"   ✓ Created organization: ID={org_id}, Code={customer_code}")
        
        # 3. Link contact to organization (if not already linked)
        print("3. Linking contact to organization...")
        cursor.execute("""
            SELECT COUNT(*) as count FROM tbllinkcustcontact
            WHERE customer_code = ? AND contactid = ?
        """, (customer_code, contact_id))
        link_count = cursor.fetchone()[0]
        
        if link_count == 0:
            cursor.execute("""
                INSERT INTO tbllinkcustcontact (customer_code, contactid)
                VALUES (?, ?)
            """, (customer_code, contact_id))
            print(f"   ✓ Linked contact {contact_id} to org {customer_code}")
        else:
            print(f"   ✓ Link already exists: contact {contact_id} ↔ org {customer_code}")
        
        # 4. Generate booking number
        print("4. Generating booking number...")
        fiscal_year = 2025 if now.month >= 7 else 2024
        year_short = fiscal_year % 100
        
        # Find the highest existing booking number for this year
        cursor.execute("""
            SELECT MAX(CAST(SUBSTRING(booking_no, 3, 4) AS INT)) as max_seq
            FROM tblbookings
            WHERE booking_no LIKE ?
        """, (f"{year_short:02d}%",))
        result = cursor.fetchone()
        max_seq = result[0] if result[0] is not None else 0
        next_seq = max_seq + 1
        booking_no = f"{year_short:02d}{next_seq:04d}"
        print(f"   ✓ Generated booking number: {booking_no}")
        
        # 5. Insert booking with all required fields
        print("5. Creating booking with all required fields...")
        event_date = datetime(2025, 3, 15)
        setup_time = datetime(2025, 3, 15, 8, 0, 0)  # 8:00 AM
        show_start = datetime(2025, 3, 15, 18, 0, 0)  # 6:00 PM
        show_end = datetime(2025, 3, 15, 22, 0, 0)    # 10:00 PM
        strike_time = datetime(2025, 3, 15, 23, 0, 0) # 11:00 PM
        
        # Insert booking with all required fields for SQL Server
        cursor.execute("""
            INSERT INTO tblbookings (
                booking_no, order_no, booking_type_v32, status, bookingprogressstatus,
                bbookingiscomplete, sdate, rdate, setdate, showsdate, showedate, rehdate,
                showstarttime, showendtime, setuptimev61, striketime,
                del_time_h, del_time_m, ret_time_h, ret_time_m,
                venueid, venueroom, contact_namev6, organizationv6, custid, contactid,
                price_quoted, hire_price, labour, insurance_v5, sundry_total,
                days_using, expattendees, showname, order_date,
                payment_type, docs_produced, delivery, percent_disc, sales_discount_rate,
                CustCode
            ) VALUES (
                ?, ?, ?, ?, ?,
                ?, ?, ?, ?, ?, ?, ?,
                ?, ?, ?, ?,
                ?, ?, ?, ?,
                ?, ?, ?, ?, ?, ?,
                ?, ?, ?, ?, ?,
                ?, ?, ?, ?,
                ?, ?, ?, ?, ?,
                ?
            )
        """, (
            booking_no, booking_no, 2, 0, 1,  # booking basics
            0,  # bbookingiscomplete (0 = False in SQL Server bit field)
            event_date, event_date, event_date, show_start, show_end, event_date,  # dates
            '1800', '2200', '0800', '2300',  # times as strings
            8, 0, 23, 0,  # del/ret times as smallints
            1, 'Main Ballroom',  # venueid, venueroom (default venue)
            'Michael Knight', 'Yes 100 Attendees', org_id, contact_id,  # contact/org
            6900.00, 5200.00, 1600.00, 100.00, 100.00,  # financial
            1, 100, 'Corporate Event - Yes 100 Attendees', now,  # days, attendees, showname, order_date
            0, 0, 0.00, 0.00, 0.00,  # payment_type, docs_produced, delivery, percent_disc, sales_discount_rate
            customer_code  # CustCode - CRITICAL for tool display
        ))
        
        # Get the booking ID that was just created
        cursor.execute("SELECT @@IDENTITY")
        booking_id = int(cursor.fetchone()[0])
        print(f"   ✓ Booking created with ID: {booking_id}")
        
        # 6. Insert equipment items with all required fields for SQL Server
        print("6. Adding equipment items with all required fields...")
        
        # Equipment items with full structure matching SQL Server schema
        equipment_items = [
            # (product_code, description, qty, price, heading_no, seq_no, sub_seq_no, item_type)
            ("SOUNDPKG", "Sound System Package", 1, 2500.00, 0, 1, 0, 1),
            ("LIGHTPKG", "Lighting Setup Package", 1, 1800.00, 0, 2, 0, 1),
            ("STAGEPKG", "Staging Package", 1, 900.00, 0, 3, 0, 1)
        ]
        
        for product_code, description, qty, price, heading_no, seq_no, sub_seq_no, item_type in equipment_items:
            # Truncate description to 70 chars (Comment_desc_v42 is char(70))
            desc_truncated = (description[:67] + '...') if len(description) > 70 else description.ljust(70)
            
            cursor.execute("""
                INSERT INTO tblitemtran (
                    booking_no_v32, product_code_v42, Comment_desc_v42,
                    trans_qty, price, unitRate,
                    heading_no, seq_no, sub_seq_no,
                    trans_type_v41, item_type, days_using,
                    booking_id, AvailRecFlag, AssignType, QtyShort,
                    FirstDate, RetnDate, BookDate,
                    View_Logi, View_client
                ) VALUES (
                    ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?, ?,
                    ?, ?, ?,
                    ?, ?
                )
            """, (
                booking_no, product_code.ljust(30), desc_truncated,  # booking_no, product_code (char30), description (char70)
                qty, price, price,  # trans_qty, price, unitRate (same as price for single item)
                heading_no, seq_no, sub_seq_no,  # heading_no, seq_no, sub_seq_no
                12, item_type, 1,  # trans_type_v41 (12 = rental), item_type (1 = equipment), days_using
                booking_id, 1, 0, 0,  # booking_id, AvailRecFlag (1=True), AssignType, QtyShort
                event_date, event_date, event_date,  # FirstDate, RetnDate, BookDate
                1, 1  # View_Logi (1=True), View_client (1=True)
            ))
            print(f"   ✓ Added equipment item: {product_code} - {description}")
        
        # 7. Insert labor/crew items
        print("7. Adding labor/crew items...")
        labor_items = [
            ("Setup Crew (2 people, 2 hours)", 400.00, 4),  # 2 people × 2 hours = 4 hours
            ("Show Crew (3 people, 4 hours)", 1200.00, 12), # 3 people × 4 hours = 12 hours
            ("Strike Crew (2 people, 1 hour)", 200.00, 2)   # 2 people × 1 hour = 2 hours
        ]
        
        for desc, amount, hours in labor_items:
            cursor.execute("""
                INSERT INTO tblcrew (
                    booking_no, crew_desc, hours, rate, line_total,
                    createdate
                ) VALUES (?, ?, ?, ?, ?, ?)
            """, (
                booking_no, desc, hours, amount / hours, amount, now
            ))
            print(f"   ✓ Added crew item: {desc}")
        
        # 8. Insert conversation transcript
        print("8. Adding conversation transcript...")
        transcript = """
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
"""
        
        cursor.execute("""
            INSERT INTO tblbooknote (
                bookingno, textline, notetype, createdate
            ) VALUES (?, ?, ?, ?)
        """, (booking_no, transcript, 1, now))  # notetype 1 = transcript
        print(f"   ✓ Added conversation transcript")
        
        # Commit transaction
        conn.commit()
        print(f"\n✅ SUCCESS! Complete booking created in SQL Server: {booking_no}")
        print(f"   - Contact ID: {contact_id}")
        print(f"   - Organization ID: {org_id}")
        print(f"   - Booking ID: {booking_id}")
        print(f"   - Event Date: 2025-03-15")
        print(f"   - Total Quote: $6,900")
        print(f"   - Attendees: 100")
        
        cursor.close()
        conn.close()
        
        return True
    
    except Exception as e:
        print(f"❌ ERROR: {e}")
        try:
            conn.rollback()
        except:
            pass
        return False

def main():
    """Main function"""
    print("=== INSERT COMPLETE BOOKING INTO SQL SERVER ===")
    print("This will create a booking with all required fields in SQL Server.")
    print("IMPORTANT: Update the connection details in get_sql_server_connection() first!")
    print()
    
    success = insert_complete_booking()
    
    if success:
        print("\n🎉 Complete booking inserted successfully into SQL Server!")
        print("Now check your inventory management tool to see if this booking appears.")
        print("\nIf the booking doesn't appear, check:")
        print("1. VenueID is set correctly (must be a valid venue ID)")
        print("2. CustCode is set correctly")
        print("3. Equipment items have product_code_v42, booking_id, and AvailRecFlag set")
    else:
        print("\n❌ Failed to insert booking data.")
        print("Check SQL Server connection details and try again.")
        print("Make sure you've installed pyodbc and SQL Server ODBC drivers.")

if __name__ == "__main__":
    main()

