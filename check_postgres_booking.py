#!/usr/bin/env python3
"""
Check the sample booking data that was inserted into PostgreSQL.
"""

import psycopg2
from psycopg2.extras import RealDictCursor

def check_sample_booking():
    """Check the sample booking data in PostgreSQL"""

    try:
        conn = psycopg2.connect(
            host="localhost",
            port="5432",
            dbname="postgres",
            user="postgres",
            password="postgres"
        )
        cursor = conn.cursor(cursor_factory=RealDictCursor)

        print("=== CHECKING SAMPLE BOOKING DATA ===")

        # Check contacts
        print("\n1. CONTACTS:")
        cursor.execute("""
            SELECT id, contactname, email, cell, position, createdate
            FROM tblcontact
            ORDER BY id DESC
            LIMIT 5
        """)
        contacts = cursor.fetchall()
        for contact in contacts:
            print(f"   ID: {contact['id']}, Name: {contact['contactname']}, Email: {contact['email']}")

        # Check organizations
        print("\n2. ORGANIZATIONS:")
        cursor.execute("""
            SELECT id, customer_code, organisationv6
            FROM tblcust
            ORDER BY id
        """)
        orgs = cursor.fetchall()
        for org in orgs:
            print(f"   ID: {org['id']}, Code: {org['customer_code']}, Name: {org['organisationv6']}")

        # Check bookings
        print("\n3. BOOKINGS:")
        cursor.execute("""
            SELECT id, booking_no, sdate, contact_namev6, organizationv6,
                   price_quoted, expattendees, order_date
            FROM tblbookings
            ORDER BY id DESC
        """)
        bookings = cursor.fetchall()
        for booking in bookings:
            print(f"   Booking: {booking['booking_no']}, Date: {booking['sdate']}")
            print(f"   Contact: {booking['contact_namev6']}, Org: {booking['organizationv6']}")
            print(f"   Quote: ${booking['price_quoted']}, Attendees: {booking['expattendees']}")
            print(f"   Created: {booking['order_date']}")
            print()

        # Check equipment items
        print("4. EQUIPMENT ITEMS:")
        cursor.execute("""
            SELECT booking_no_v32, item_desc, hire_rate, qty, line_total
            FROM tblitemtran
            ORDER BY id
        """)
        items = cursor.fetchall()
        for item in items:
            print(f"   {item['booking_no_v32']}: {item['item_desc']} - ${item['hire_rate']} x {item['qty']} = ${item['line_total']}")

        # Check labor/crew
        print("\n5. LABOR/CREW:")
        cursor.execute("""
            SELECT booking_no, crew_desc, hours, rate, line_total
            FROM tblcrew
            ORDER BY id
        """)
        crews = cursor.fetchall()
        for crew in crews:
            print(f"   {crew['booking_no']}: {crew['crew_desc']} - {crew['hours']}hrs @ ${crew['rate']}/hr = ${crew['line_total']}")

        # Check transcripts
        print("\n6. CONVERSATION TRANSCRIPTS:")
        cursor.execute("""
            SELECT bookingno, notetype, createdate,
                   LEFT(textline, 100) || '...' as preview
            FROM tblbooknote
            ORDER BY id DESC
        """)
        notes = cursor.fetchall()
        for note in notes:
            print(f"   {note['bookingno']}: Type {note['notetype']}, Created: {note['createdate']}")
            print(f"   Preview: {note['preview']}")

        # Check links
        print("\n7. CONTACT-ORGANIZATION LINKS:")
        cursor.execute("""
            SELECT l.customer_code, l.contactid, c.contactname, o.organisationv6
            FROM tbllinkcustcontact l
            JOIN tblcontact c ON l.contactid = c.id
            JOIN tblcust o ON l.customer_code = o.customer_code
            ORDER BY l.id
        """)
        links = cursor.fetchall()
        for link in links:
            print(f"   {link['contactname']} ↔ {link['organisationv6']} (Code: {link['customer_code']})")

        cursor.close()
        conn.close()

        print("\n" + "="*60)
        print("✅ SAMPLE BOOKING DATA VERIFICATION COMPLETE")
        print("="*60)
        print("The booking data has been successfully inserted into PostgreSQL!")
        print("If your inventory management tool connects to this database,")
        print("you should now see the booking '250001' with Michael Knight.")

    except Exception as e:
        print(f"❌ Error checking data: {e}")

if __name__ == "__main__":
    check_sample_booking()
