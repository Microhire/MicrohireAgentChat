#!/usr/bin/env python3
"""
Create a complete test booking with contact, equipment, crew, and transcript.
This will create a new booking that should appear in the inventory management tool.
"""

import psycopg2
import json
from datetime import datetime
from psycopg2.extras import RealDictCursor

def create_complete_booking():
    """Create a complete booking with contact, organization, equipment, crew, and transcript"""

    # Connect to PostgreSQL database
    conn = psycopg2.connect(
        host='localhost',
        port='5432',
        dbname='postgres',
        user='postgres',
        password='postgres'
    )
    cursor = conn.cursor(cursor_factory=RealDictCursor)

    now = datetime.now()
    print('🚀 CREATING COMPLETE BOOKING FOR TESTING')
    print('=' * 60)

    # 1. Create new contact
    print('\n1. Creating new contact...')
    contact_data = {
        'contactname': 'Alex Johnson',
        'firstname': 'Alex',
        'surname': 'Johnson',
        'email': 'alex.johnson@testcompany.com',
        'cell': '0412345678',
        'position': 'Event Manager'
    }

    cursor.execute('''
        INSERT INTO tblcontact (
            contactname, firstname, surname, email, cell, position,
            createdate, lastupdate, lastcontact, active
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
        RETURNING id
    ''', (
        contact_data['contactname'], contact_data['firstname'], contact_data['surname'],
        contact_data['email'], contact_data['cell'], contact_data['position'],
        now, now, now, True
    ))
    contact_id = cursor.fetchone()['id']
    print(f'   ✓ Contact created: {contact_data["contactname"]} (ID: {contact_id})')

    # 2. Create/find organization
    print('\n2. Finding organization...')
    cursor.execute('SELECT id, customer_code FROM tblcust WHERE organisationv6 = %s', ('Test Events Company',))
    org = cursor.fetchone()

    if not org:
        print('   ⚠ Organization not found, using existing one...')
        # Use an existing organization - PostgreSQL syntax
        cursor.execute('SELECT id, customer_code, organisationv6 FROM tblcust ORDER BY id DESC LIMIT 1')
        org = cursor.fetchone()

    if org:
        org_id = org['id']
        customer_code = org['customer_code']
        org_name = org['organisationv6']
        print(f'   ✓ Using organization: {org_name} (Code: {customer_code})')
    else:
        print('   ❌ No organization found!')
        return False

    # 3. Link contact to organization
    print('\n3. Linking contact to organization...')
    cursor.execute('''
        SELECT COUNT(*) as count FROM tbllinkcustcontact
        WHERE customer_code = %s AND contactid = %s
    ''', (customer_code, contact_id))
    link_count = cursor.fetchone()['count']

    if link_count == 0:
        cursor.execute('INSERT INTO tbllinkcustcontact (customer_code, contactid) VALUES (%s, %s)',
                      (customer_code, contact_id))
        print(f'   ✓ Linked contact {contact_id} to organization {customer_code}')
    else:
        print(f'   ✓ Link already exists')

    # 4. Generate booking number
    print('\n4. Generating booking number...')
    fiscal_year = 2025 if now.month >= 7 else 2024
    year_short = fiscal_year % 100

    cursor.execute('''
        SELECT MAX(CAST(SUBSTRING(booking_no, 3, 4) AS INTEGER)) as max_seq
        FROM tblbookings
        WHERE booking_no LIKE %s
    ''', (f'{year_short:02d}%',))
    result = cursor.fetchone()
    max_seq = result['max_seq'] if result and result['max_seq'] else 0
    next_seq = max_seq + 1
    booking_no = f'{year_short:02d}{next_seq:04d}'
    print(f'   ✓ Generated booking number: {booking_no}')

    # 5. Create main booking
    print('\n5. Creating main booking...')
    event_date = datetime(2025, 12, 15)  # Future date
    setup_time = datetime(2025, 12, 15, 7, 0, 0)
    show_start = datetime(2025, 12, 15, 19, 0, 0)
    show_end = datetime(2025, 12, 15, 23, 0, 0)
    strike_time = datetime(2025, 12, 15, 23, 30, 0)

    # Create the booking with only the essential columns
    cursor.execute('''
        INSERT INTO tblbookings (
            booking_no, order_no, booking_type_v32, status, bookingprogressstatus,
            bbookingiscomplete, sdate, showsdate, showedate,
            showstarttime, showendtime, setuptimev61, striketime,
            venueid, venueroom, contact_namev6, organizationv6, custid, contactid,
            price_quoted, hire_price, labour, insurance_v5, sundry_total,
            days_using, expattendees, showname, order_date
        ) VALUES (
            %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
            %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s
        )
        RETURNING id
    ''', (
        booking_no, booking_no, 2, 0, 1,  # booking basics
        False,  # bbookingiscomplete
        event_date, show_start, show_end,  # dates
        '1900', '2300', '0700', '2330',  # times as strings
        1, 'Grand Ballroom',  # venueid, venueroom
        contact_data['contactname'], org_name, org_id, contact_id,  # contact/org
        8500.00, 6500.00, 1500.00, 300.00, 200.00,  # financial
        1, 150, 'Corporate Holiday Party - Test Booking', now  # days, attendees, showname, order_date
    ))
    booking_id = cursor.fetchone()['id']
    print(f'   ✓ Booking created with ID: {booking_id}')

    # 6. Add equipment items
    print('\n6. Adding equipment items...')
    equipment_items = [
        ('SPK-PKG', 'Complete Speaker Package', 1, 2500.00),
        ('LIGHT-PKG', 'LED Lighting Package', 1, 1800.00),
        ('PROJ-SYS', 'Projection System', 1, 1200.00),
        ('MIC-PKG', 'Wireless Microphone Package', 1, 800.00),
        ('STAGE-BASIC', 'Basic Staging Setup', 1, 1200.00)
    ]

    for product_code, description, qty, hire_rate in equipment_items:
        cursor.execute('''
            INSERT INTO tblitemtran (
                booking_no_v32, item_desc, hire_rate, qty, line_total,
                createdate, item_type
            ) VALUES (%s, %s, %s, %s, %s, %s, %s)
        ''', (
            booking_no, f'{product_code} - {description}', hire_rate, qty, hire_rate * qty,
            now, 'Equipment'
        ))

    print(f'   ✓ Added {len(equipment_items)} equipment items')

    # 7. Add crew/labour
    print('\n7. Adding crew/labour...')
    crew_items = [
        ('Technical Director', 600.00, 8),  # 8 hours
        ('Audio Engineer', 400.00, 8),     # 8 hours
        ('Lighting Technician', 400.00, 8), # 8 hours
        ('Stage Hands (2 people)', 500.00, 16), # 2 people × 8 hours
        ('Setup Crew (2 people)', 400.00, 4),   # 2 people × 2 hours
        ('Strike Crew (2 people)', 300.00, 4)   # 2 people × 2 hours
    ]

    for desc, rate, hours in crew_items:
        cursor.execute('''
            INSERT INTO tblcrew (
                booking_no, crew_desc, hours, rate, line_total,
                createdate
            ) VALUES (%s, %s, %s, %s, %s, %s)
        ''', (
            booking_no, desc, hours, rate, rate, now
        ))

    print(f'   ✓ Added {len(crew_items)} crew assignments')

    # 8. Add conversation transcript
    print('\n8. Adding conversation transcript...')
    transcript = '''=== CONVERSATION TRANSCRIPT ===
[2025-11-20 14:00:00] USER: Hi, I'm Alex Johnson from Test Events Company. We need to book equipment for our corporate holiday party.

[2025-11-20 14:00:15] ASSISTANT: Hello Alex! I'd be happy to help you book equipment for your corporate holiday party. Could you please tell me:
1. When is the event?
2. Where is the venue?
3. What equipment do you need?
4. How many people will attend?

[2025-11-20 14:01:00] USER: The event is on December 15, 2025 at a hotel ballroom. We expect 150 attendees for a corporate holiday party.

[2025-11-20 14:01:15] ASSISTANT: Great! So we have:
- Event: December 15, 2025
- Venue: Hotel Ballroom
- Attendees: 150
- Type: Corporate Holiday Party

What equipment do you need? For example: sound system, lighting, staging, projection?

[2025-11-20 14:02:00] USER: We need a complete setup: sound system with speakers and wireless mics, LED lighting, projection system, and basic staging.

[2025-11-20 14:02:15] ASSISTANT: Perfect! Let me get your contact details for the booking.

What's your phone number and email address?

[2025-11-20 14:02:30] USER: My email is alex.johnson@testcompany.com and phone is 0412345678. I'm the Event Manager.

[2025-11-20 14:02:45] ASSISTANT: Thanks Alex! Now for the logistics:

- Setup time: 7:00 AM
- Show start: 7:00 PM
- Show end: 11:00 PM
- Strike time: 11:30 PM

Does this work for you?

[2025-11-20 14:03:00] USER: Yes, that timing works perfectly.

[2025-11-20 14:03:15] ASSISTANT: Based on your requirements, here's the quote:

**Equipment:**
- Complete Speaker Package: $2,500
- LED Lighting Package: $1,800
- Projection System: $1,200
- Wireless Microphone Package: $800
- Basic Staging Setup: $1,200

**Labour:**
- Technical Director (8 hours): $600
- Audio Engineer (8 hours): $400
- Lighting Technician (8 hours): $400
- Stage Hands (16 hours): $500
- Setup Crew (4 hours): $400
- Strike Crew (4 hours): $300

**Total Equipment: $7,500**
**Total Labour: $2,600**
**Insurance & Sundries: $500**
**Grand Total: $8,500**

Shall I proceed with creating this booking?

[2025-11-20 14:04:00] USER: Yes, please create the booking.'''

    cursor.execute('''
        INSERT INTO tblbooknote (
            bookingno, textline, notetype, createdate
        ) VALUES (%s, %s, %s, %s)
    ''', (booking_no, transcript, 1, now))

    print('   ✓ Conversation transcript added')

    # Commit everything
    conn.commit()
    print('\n' + '=' * 60)
    print('✅ SUCCESS! Complete booking created in database')
    print('=' * 60)
    print(f'📋 Booking Number: {booking_no}')
    print(f'👤 Contact: {contact_data["contactname"]} (ID: {contact_id})')
    print(f'🏢 Organization: {org_name} (ID: {org_id})')
    print(f'📅 Event Date: {event_date.strftime("%Y-%m-%d")}')
    print(f'📍 Venue: Grand Ballroom')
    print(f'👥 Attendees: 150')
    print(f'💰 Total Quote: $8,500')
    print(f'🔧 Equipment Items: {len(equipment_items)}')
    print(f'👷 Crew Assignments: {len(crew_items)}')
    print('\n🎯 Check your inventory management tool for this booking!')
    print(f'   Look for booking: {booking_no}')
    print(f'   Contact: Alex Johnson')

    conn.close()
    return True

if __name__ == '__main__':
    create_complete_booking()
