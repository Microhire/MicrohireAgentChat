#!/usr/bin/env python3
"""
Explore bookings in PostgreSQL database to find and analyze structure.
"""

import psycopg2
import json
from datetime import datetime
from psycopg2.extras import RealDictCursor
from tabulate import tabulate

def get_connection():
    """Connect to PostgreSQL database"""
    try:
        conn = psycopg2.connect(
            host="localhost",
            port="5432",
            dbname="postgres",
            user="postgres",
            password="postgres"
        )
        return conn
    except Exception as e:
        print(f"❌ Connection error: {e}")
        return None

def explore_bookings():
    """Explore bookings in the database"""
    conn = get_connection()
    if not conn:
        return
    
    cursor = conn.cursor(cursor_factory=RealDictCursor)
    
    print("=== EXPLORING BOOKINGS DATABASE ===\n")
    
    # 1. Look for booking C1374900080
    print("1. Searching for booking C1374900080...")
    cursor.execute("""
        SELECT booking_no, order_no, contact_namev6, organizationv6, custid, contactid,
               price_quoted, hire_price, labour, insurance_v5, sundry_total,
               sdate, showstarttime, showendtime, setuptimev61, striketime,
               venueid, venueroom, days_using, expattendees, showname
        FROM tblbookings
        WHERE booking_no = 'C1374900080' OR booking_no LIKE '%1374900080%'
    """)
    bookings = cursor.fetchall()
    
    if bookings:
        for booking in bookings:
            print(f"Found booking: {booking['booking_no']}")
            print(json.dumps(dict(booking), indent=2, default=str))
    else:
        print("Booking C1374900080 not found.")
        
        # Search for any bookings that might be similar
        print("\nSearching for any bookings with similar pattern...")
        cursor.execute("""
            SELECT booking_no, contact_namev6, organizationv6, price_quoted
            FROM tblbookings
            WHERE booking_no LIKE '%C%' OR booking_no LIKE '%1374%'
            LIMIT 5
        """)
        similar = cursor.fetchall()
        if similar:
            print("Found similar bookings:")
            print(tabulate([dict(b) for b in similar], headers="keys"))
    
    # 2. Get all bookings to analyze structure
    print("\n2. Listing all bookings to analyze structure...")
    cursor.execute("""
        SELECT booking_no, contact_namev6, organizationv6, price_quoted, 
               venueid, venueroom, sdate, order_date, id
        FROM tblbookings
        ORDER BY id DESC
        LIMIT 5
    """)
    all_bookings = cursor.fetchall()
    
    if all_bookings:
        print(f"Found {len(all_bookings)} bookings:")
        print(tabulate([dict(b) for b in all_bookings], headers="keys"))
        
        # Select the most recent booking for detailed analysis
        recent_booking = all_bookings[0]
        booking_no = recent_booking['booking_no']
        booking_id = recent_booking['id']
        
        print(f"\n3. Analyzing most recent booking: {booking_no} (ID: {booking_id})...")
        
        # Get equipment items for this booking
        cursor.execute("""
            SELECT *
            FROM tblitemtran
            WHERE booking_no_v32 = %s
            ORDER BY id
        """, (booking_no,))
        items = cursor.fetchall()
        
        if items:
            print(f"Found {len(items)} equipment items:")
            # Get column names
            columns = list(items[0].keys())
            print(f"Equipment item columns: {', '.join(columns)}")
            print("\nEquipment items:")
            print(tabulate([dict(item) for item in items], headers="keys"))
        else:
            print("No equipment items found for this booking.")
        
        # Get crew items for this booking
        cursor.execute("""
            SELECT *
            FROM tblcrew
            WHERE booking_no = %s
            ORDER BY id
        """, (booking_no,))
        crews = cursor.fetchall()
        
        if crews:
            print(f"\nFound {len(crews)} crew items:")
            # Get column names
            columns = list(crews[0].keys())
            print(f"Crew item columns: {', '.join(columns)}")
            print("\nCrew items:")
            print(tabulate([dict(crew) for crew in crews], headers="keys"))
        else:
            print("No crew items found for this booking.")
    else:
        print("No bookings found in the database.")
    
    # 3. Analyze table structures
    print("\n4. Analyzing table structures...")
    tables = ['tblbookings', 'tblitemtran', 'tblcrew']
    
    for table in tables:
        cursor.execute("""
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = %s
            ORDER BY ordinal_position
        """, (table,))
        columns = cursor.fetchall()
        
        print(f"\nTable: {table}")
        print(tabulate([dict(col) for col in columns], headers="keys"))
    
    cursor.close()
    conn.close()

if __name__ == "__main__":
    explore_bookings()

