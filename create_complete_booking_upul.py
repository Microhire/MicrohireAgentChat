#!/usr/bin/env python3
"""
Create complete booking with Upul Harischandra and new customer
Uses the exact same database connection as the application
"""

import pymssql
from datetime import datetime

def create_complete_booking():
    """Create complete booking with new customer and Upul Harischandra"""
    
    # Use EXACT connection string from appsettings.json
    conn = pymssql.connect(
        server='116.90.5.144',
        port=41383,
        user='PowerBI-Consult',
        password='2tW@ostq3a3_9oV3m-TBQu3w',
        database='AITESTDB',
        as_dict=True
    )
    cursor = conn.cursor()
    
    print('🚀 CREATING COMPLETE BOOKING')
    print('=' * 60)
    
    try:
        # 1. Generate new customer code
        cursor.execute('SELECT MAX(CAST(SUBSTRING(customer_code, 2, LEN(customer_code)-1) AS INT)) as max_cust FROM tblcust WHERE customer_code LIKE %s', ('C%',))
        max_cust_result = cursor.fetchone()
        max_cust = max_cust_result['max_cust'] if max_cust_result['max_cust'] else 12440
        new_cust_num = max_cust + 1
        new_cust_code = f'C{new_cust_num}'
        print(f'✅ Generated customer code: {new_cust_code}')
        
        # 2. Create new customer
        new_customer_name = 'Test Events Company'
        cursor.execute('''
            INSERT INTO tblcust (
                customer_code, OrganisationV6, contactV6, CustCDate
            ) VALUES (%s, %s, %s, %s)
        ''', (
            new_cust_code, new_customer_name, 'Upul Harischandra', datetime.now()
        ))
        print(f'✅ Created customer: {new_customer_name}')
        
        # 3. Get customer ID
        cursor.execute('SELECT TOP 1 ID FROM tblcust WHERE customer_code = %s', (new_cust_code,))
        new_cust_result = cursor.fetchone()
        new_cust_id = new_cust_result['ID']
        
        # 4. Find or create Upul Harischandra
        cursor.execute('SELECT TOP 1 ID, Contactname FROM tblContact WHERE Contactname LIKE %s', ('%Upul%',))
        upul = cursor.fetchone()
        
        if upul:
            upul_id = upul['ID']
            upul_name = upul['Contactname']
            print(f'✅ Using existing contact: {upul_name}')
        else:
            cursor.execute('''
                INSERT INTO tblContact (
                    Contactname, firstname, surname, Email, Cell, position,
                    CreateDate, LastUpdate, LastContact, Active
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ''', (
                'Upul Harischandra', 'Upul', 'Harischandra',
                'upul@testevents.com', '0412345678', 'Event Manager',
                datetime.now(), datetime.now(), datetime.now(), 'Y'
            ))
            conn.commit()
            
            cursor.execute('SELECT TOP 1 ID, Contactname FROM tblContact WHERE Contactname = %s', ('Upul Harischandra',))
            upul = cursor.fetchone()
            upul_id = upul['ID']
            upul_name = upul['Contactname']
            print(f'✅ Created contact: {upul_name}')
        
        # 5. Link contact to customer
        cursor.execute('SELECT COUNT(*) as cnt FROM tblLinkCustContact WHERE customer_code = %s AND contactid = %s', (new_cust_code, upul_id))
        link_exists = cursor.fetchone()['cnt']
        
        if link_exists == 0:
            cursor.execute('INSERT INTO tblLinkCustContact (customer_code, contactid) VALUES (%s, %s)', (new_cust_code, upul_id))
            print(f'✅ Linked contact to customer')
        
        # 6. Generate booking number
        cursor.execute('SELECT MAX(CAST(SUBSTRING(booking_no, 2, 8) AS INT)) as max_booking FROM tblbookings WHERE booking_no LIKE %s', ('C%',))
        max_booking_result = cursor.fetchone()
        max_booking = max_booking_result['max_booking'] if max_booking_result['max_booking'] else 124400000
        next_booking_num = max_booking + 1
        booking_no = f'C{next_booking_num:08d}'
        print(f'✅ Generated booking number: {booking_no}')
        
        # 7. Create booking
        event_date = datetime(2025, 12, 20)
        show_start = datetime(2025, 12, 20, 19, 0, 0)
        show_end = datetime(2025, 12, 20, 23, 0, 0)
        
        cursor.execute('''
            INSERT INTO tblbookings (
                booking_no, order_no, booking_type_v32, status, BookingProgressStatus,
                bBookingIsComplete, Sdate, ShowSdate, ShowEdate,
                ShowStartTime, ShowEndTime, SetupTimeV61, StrikeTime,
                VenueID, VenueRoom, contact_nameV6, OrganizationV6, CustID, ContactID,
                price_quoted, hire_price, labour, insurance_v5, sundry_total,
                days_using, expAttendees, showName, order_date
            ) VALUES (
                %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s,
                %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s
            )
        ''', (
            booking_no, booking_no, 2, 0, 1,
            0,
            event_date, show_start, show_end,
            '1900', '2300', '0700', '2330',
            1, 'Grand Ballroom',
            upul_name, new_customer_name, new_cust_id, upul_id,
            8500.00, 6500.00, 1500.00, 300.00, 200.00,
            1, 150, 'Corporate Holiday Party - Test Booking', datetime.now()
        ))
        
        # Get booking ID
        cursor.execute('SELECT TOP 1 ID FROM tblbookings WHERE booking_no = %s', (booking_no,))
        booking_result = cursor.fetchone()
        booking_id = booking_result['ID']
        print(f'✅ Booking created: {booking_no} (ID: {booking_id})')
        
        # 8. Add equipment
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
        
        print(f'✅ Added {len(equipment_items)} equipment items')
        
        # 9. Add crew
        crew_items = [
            ('TECHDIR', 'Technical Director', 600.00, 8),
            ('AUDENG', 'Audio Engineer', 400.00, 8),
            ('LIGHTECH', 'Lighting Technician', 400.00, 8),
            ('STAGEHND', 'Stage Hands', 500.00, 16),
            ('SETUP', 'Setup Crew', 400.00, 4),
            ('STRIKE', 'Strike Crew', 300.00, 4)
        ]
        
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
        
        print(f'✅ Added {len(crew_items)} crew assignments')
        
        # 10. Add transcript
        transcript = '''=== CONVERSATION TRANSCRIPT ===
[2025-11-20 14:00:00] USER: Hi, I'm Upul Harischandra from Test Events Company. We need to book equipment for a corporate holiday party.

[2025-11-20 14:02:00] USER: We need sound system, lighting, projection, and staging for 150 people.

[2025-11-20 14:04:00] USER: Yes, please create the booking.'''
        
        cursor.execute('''
            INSERT INTO tblbooknote (
                bookingNo, line_no, text_line, NoteType
            ) VALUES (%s, %s, %s, %s)
        ''', (booking_no, 1, transcript, 1))
        
        print('✅ Conversation transcript added')
        
        # Commit everything
        conn.commit()
        
        print('\n' + '=' * 60)
        print('✅ SUCCESS! Complete booking created')
        print('=' * 60)
        print(f'📋 Booking Number: {booking_no}')
        print(f'👤 Contact: {upul_name} (ID: {upul_id})')
        print(f'🏢 NEW Customer: {new_customer_name} (Code: {new_cust_code}, ID: {new_cust_id})')
        print(f'📅 Event Date: {event_date.strftime("%Y-%m-%d")}')
        print(f'💰 Total Quote: $8,500')
        print(f'🔧 Equipment Items: {len(equipment_items)}')
        print(f'👷 Crew Assignments: {len(crew_items)}')
        print('\n🎯 Check your inventory management tool now!')
        print(f'   Look for booking: {booking_no}')
        print(f'   Contact: {upul_name}')
        print(f'   Customer: {new_customer_name}')
        
        return True
        
    except Exception as e:
        print(f'\n❌ ERROR: {e}')
        conn.rollback()
        return False
    finally:
        conn.close()

if __name__ == '__main__':
    create_complete_booking()
