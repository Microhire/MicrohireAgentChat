#!/usr/bin/env python3
"""
Insert sample booking data directly into the database to test if it appears in the inventory management tool.
This simulates the exact data that would be created by the booking process.
"""

import pyodbc
import json
from datetime import datetime, timezone
import uuid

def get_connection_string():
    """Get database connection from appsettings.json"""
    try:
        with open('MicrohireAgentChat/appsettings.json', 'r') as f:
            config = json.load(f)
            return config['ConnectionStrings']['BookingsDb']
    except Exception as e:
        print(f"Error reading connection string: {e}")
        return None

def insert_sample_data():
    """Insert sample booking data into the database"""

    conn_str = get_connection_string()
    if not conn_str:
        print("❌ Could not get connection string")
        return False

    try:
        print("🔌 Connecting to database...")
        conn = pyodbc.connect(conn_str)
        cursor = conn.cursor()

        # Start transaction
        conn.autocommit = False

        now = datetime.now()

        print("📝 Inserting sample data...")

        # 1. Check if contact already exists, if not create it
        print("1. Checking/creating contact...")
        cursor.execute("""
            SELECT ID FROM tblContact WHERE Email = ?
        """, ('michael@yes100attendees.com',))
        contact_row = cursor.fetchone()

        if contact_row:
            contact_id = contact_row[0]
            print(f"   ✓ Contact exists: ID={contact_id}")

            # Update contact with latest info
            cursor.execute("""
                UPDATE tblContact SET
                    Contactname = ?,
                    firstname = ?,
                    surname = ?,
                    Email = ?,
                    Cell = ?,
                    position = ?,
                    LastUpdate = ?,
                    LastContact = ?
                WHERE ID = ?
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
                    Contactname, firstname, surname, Email, Cell, position,
                    CreateDate, LastUpdate, LastContact, Active
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                'Michael Knight',
                'Michael',
                'Knight',
                'michael@yes100attendees.com',
                '07111111111',
                'Events Coordinator',
                now, now, now, 1
            ))
            contact_id = cursor.execute("SELECT @@IDENTITY").fetchone()[0]
            print(f"   ✓ Created contact: ID={contact_id}")

        # 2. Check if organization exists
        print("2. Checking organization...")
        cursor.execute("""
            SELECT ID, Customer_code FROM tblcust WHERE OrganisationV6 = ?
        """, ('Yes 100 Attendees',))
        org_row = cursor.fetchone()

        if org_row:
            org_id = org_row[0]
            customer_code = org_row[1]
            print(f"   ✓ Organization exists: ID={org_id}, Code={customer_code}")
        else:
            print("   ⚠ Organization 'Yes 100 Attendees' not found - this should exist")
            # Don't create org - it should already exist from previous runs
            org_id = 14503  # Use known ID from previous logs
            customer_code = 'C14503'

        # 3. Link contact to organization (if not already linked)
        print("3. Linking contact to organization...")
        cursor.execute("""
            SELECT COUNT(*) FROM tblLinkCustContact
            WHERE Customer_Code = ? AND ContactID = ?
        """, (customer_code, contact_id))
        link_count = cursor.fetchone()[0]

        if link_count == 0:
            cursor.execute("""
                INSERT INTO tblLinkCustContact (Customer_Code, ContactID)
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
            SELECT MAX(CAST(SUBSTRING(booking_no, 3, 4) AS INT))
            FROM tblbookings
            WHERE booking_no LIKE ?
        """, (f"{year_short:02d}%",))
        max_seq = cursor.fetchone()[0]
        next_seq = (max_seq + 1) if max_seq else 1
        booking_no = f"{year_short:02d}{next_seq:04d}"
        print(f"   ✓ Generated booking number: {booking_no}")

        # 5. Insert booking
        print("5. Creating booking...")
        event_date = datetime(2025, 3, 15)
        setup_time = datetime(2025, 3, 15, 8, 0, 0)  # 8:00 AM
        show_start = datetime(2025, 3, 15, 18, 0, 0)  # 6:00 PM
        show_end = datetime(2025, 3, 15, 22, 0, 0)    # 10:00 PM
        strike_time = datetime(2025, 3, 15, 23, 0, 0) # 11:00 PM

        cursor.execute("""
            INSERT INTO tblbookings (
                booking_no, order_no, booking_type_v32, status, BookingProgressStatus,
                bBookingIsComplete, SDate, rDate, SetDate, ShowSDate, ShowEdate, RehDate,
                order_date, EntryDate, showStartTime, ShowEndTime, setupTimeV61, StrikeTime,
                del_time_h, del_time_m, ret_time_h, ret_time_m,
                VenueID, VenueRoom, contact_nameV6, OrganizationV6, CustID, ContactID,
                price_quoted, hire_price, labour, insurance_v5, sundry_total, Tax2,
                days_using, expAttendees, From_locn, Trans_to_locn, return_to_locn,
                invoiced, perm_casual, TaxAuthority1, TaxAuthority2, showName,
                sales_tax_no, last_modified_by, delivery_address_exist, percent_disc,
                division, docs_produced, event_code, discount_rate, same_address,
                un_disc_amount, sales_discount_rate, sales_amount, tax1,
                delivery_viav71, pickup_viaV71, delivery_time, pickup_time,
                payment_type, deposit_quoted_v50, Item_cnt, sales_percent_disc,
                days_charged_v51, sale_of_asset, retail_value, division,
                HourBooked, MinBooked, SecBooked, currencyStr, ConfirmedBy,
                ConfirmedDocRef, transferNo
            ) VALUES (
                ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
                ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
                ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
                ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
            )
        """, (
            booking_no, booking_no, 2, 0, 1,  # booking basics
            False,  # bBookingIsComplete
            event_date, event_date, event_date, show_start, show_end, event_date,  # dates
            now, now,  # order_date, EntryDate
            '1800', '2200', '0800', '2300',  # times as strings
            8, 0, 23, 0,  # del/ret times as bytes
            1, 'Main Ballroom',  # VenueID, VenueRoom (default venue)
            'Michael Knight', 'Yes 100 Attendees', org_id, contact_id,  # contact/org
            6900.00, 5200.00, 1600.00, 100.00, 100.00, 0.00,  # financial
            1, 100, 20, 20, 20,  # days, attendees, locations
            'N', 'Y', 0, 1,  # invoiced, perm_casual, tax authorities
            'Corporate Event - Yes 100 Attendees',  # showName
            None, None, None, 0.00,  # various null/default fields
            None, 0, None, 0.00, None,  # more null/default
            0.00, 0.00, 0.00, 0.00,  # financial
            None, None, None, None,  # delivery times
            None, 0.00, 0, 0.00,  # payment, deposit, item count
            0.00, 0.00, 0.00, None,  # more financial
            None, None, None, None, None,  # confirmed by, etc.
            None, None, None, None, None,  # final null fields
            None  # transferNo
        ))

        # 6. Insert equipment items
        print("6. Adding equipment items...")
        equipment_items = [
            ("Sound System Package", 2500.00, 1),
            ("Lighting Setup Package", 1800.00, 1),
            ("Staging Package", 900.00, 1)
        ]

        for item_name, hire_rate, qty in equipment_items:
            cursor.execute("""
                INSERT INTO tblitemtran (
                    booking_no_v32, item_desc, hire_rate, qty, line_total,
                    CreateDate, item_type
                ) VALUES (?, ?, ?, ?, ?, ?, ?)
            """, (
                booking_no, item_name, hire_rate, qty, hire_rate * qty,
                now, 'Equipment'
            ))

        # 7. Insert labor/crew
        print("7. Adding labor/crew...")
        labor_items = [
            ("Setup Crew (2 people, 2 hours)", 400.00, 4),  # 2 people × 2 hours = 4 hours
            ("Show Crew (3 people, 4 hours)", 1200.00, 12), # 3 people × 4 hours = 12 hours
            ("Strike Crew (2 people, 1 hour)", 200.00, 2)   # 2 people × 1 hour = 2 hours
        ]

        for desc, amount, hours in labor_items:
            cursor.execute("""
                INSERT INTO tblcrew (
                    booking_no, crew_desc, hours, rate, line_total,
                    CreateDate
                ) VALUES (?, ?, ?, ?, ?, ?)
            """, (
                booking_no, desc, hours, amount / hours, amount, now
            ))

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
                BookingNo, TextLine, NoteType, CreateDate
            ) VALUES (?, ?, ?, ?)
        """, (booking_no, transcript, 1, now))  # NoteType 1 = transcript

        # Commit transaction
        conn.commit()
        print(f"\n✅ SUCCESS! Sample booking created: {booking_no}")
        print(f"   - Contact ID: {contact_id}")
        print(f"   - Organization ID: {org_id}")
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
    print("=== Insert Sample Booking Data ===")
    print("This will create a complete booking record in the database")
    print("to test if it appears in your inventory management tool.")
    print()

    success = insert_sample_data()

    if success:
        print("\n🎉 Booking inserted successfully!")
        print("Now check your inventory management tool to see if this booking appears.")
        print("Look for booking number starting with '25' and contact 'Michael Knight'.")
        print("\nYou can also run: python check_booking_in_db.py")
        print("Or use the SQL queries in check_recent_bookings.sql")
    else:
        print("\n❌ Failed to insert booking data.")
        print("Check database connection and try again.")

if __name__ == "__main__":
    main()
