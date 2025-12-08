#!/usr/bin/env python3
"""Check if booking 250027 exists in the database"""

import pymssql

def main():
    conn = pymssql.connect(
        server='116.90.5.144',
        port=41383,
        user='PowerBI-Consult',
        password='2tW@ostq3a3_9oV3m-TBQu3w',
        database='AITESTDB'
    )
    cursor = conn.cursor(as_dict=True)
    
    print("=" * 80)
    print("CHECKING BOOKING 250027")
    print("=" * 80)
    
    # Check tblbookings
    print("\n1. Checking tblbookings for booking_no = '250027':")
    cursor.execute("SELECT ID, booking_no, showName, CustID, ContactID, VenueID, SDate, ShowSDate, status FROM tblbookings WHERE booking_no = '250027'")
    rows = cursor.fetchall()
    if rows:
        for row in rows:
            print(f"   Found: ID={row['ID']}, booking_no={row['booking_no']}, showName={row['showName']}")
            print(f"          CustID={row['CustID']}, ContactID={row['ContactID']}, VenueID={row['VenueID']}")
            print(f"          SDate={row['SDate']}, ShowSDate={row['ShowSDate']}, status={row['status']}")
    else:
        print("   NOT FOUND in tblbookings!")
    
    # Also check by ID 70335
    print("\n2. Checking tblbookings for ID = 70335:")
    cursor.execute("SELECT ID, booking_no, showName, CustID, ContactID, VenueID, SDate, ShowSDate, status FROM tblbookings WHERE ID = 70335")
    rows = cursor.fetchall()
    if rows:
        for row in rows:
            print(f"   Found: ID={row['ID']}, booking_no={row['booking_no']}, showName={row['showName']}")
            print(f"          CustID={row['CustID']}, ContactID={row['ContactID']}, VenueID={row['VenueID']}")
            print(f"          SDate={row['SDate']}, ShowSDate={row['ShowSDate']}, status={row['status']}")
    else:
        print("   NOT FOUND!")
    
    # Check tblItemtran for booking 250027
    print("\n3. Checking tblItemtran for booking_no_v32 = '250027':")
    cursor.execute("SELECT COUNT(*) as cnt FROM tblItemtran WHERE booking_no_v32 = '250027'")
    row = cursor.fetchone()
    print(f"   Found {row['cnt']} items in tblItemtran")
    
    # Check tblbooknote
    print("\n4. Checking tblbooknote for bookingNo = '250027':")
    cursor.execute("SELECT COUNT(*) as cnt FROM tblbooknote WHERE bookingNo = '250027'")
    row = cursor.fetchone()
    print(f"   Found {row['cnt']} notes in tblbooknote")
    
    # Check the most recent bookings
    print("\n5. Most recent 5 bookings in tblbookings:")
    cursor.execute("""
        SELECT TOP 5 ID, booking_no, showName, CustID, ContactID, EntryDate 
        FROM tblbookings 
        ORDER BY ID DESC
    """)
    rows = cursor.fetchall()
    for row in rows:
        print(f"   ID={row['ID']}, booking_no={row['booking_no']}, showName={row['showName']}, EntryDate={row['EntryDate']}")
    
    # Check bookings for customer 14574
    print("\n6. Bookings for CustID = 14574:")
    cursor.execute("SELECT ID, booking_no, showName, status FROM tblbookings WHERE CustID = 14574")
    rows = cursor.fetchall()
    if rows:
        for row in rows:
            print(f"   ID={row['ID']}, booking_no={row['booking_no']}, showName={row['showName']}, status={row['status']}")
    else:
        print("   No bookings found for this customer!")
    
    # Check bookings for contact 21057
    print("\n7. Bookings for ContactID = 21057:")
    cursor.execute("SELECT ID, booking_no, showName, status FROM tblbookings WHERE ContactID = 21057")
    rows = cursor.fetchall()
    if rows:
        for row in rows:
            print(f"   ID={row['ID']}, booking_no={row['booking_no']}, showName={row['showName']}, status={row['status']}")
    else:
        print("   No bookings found for this contact!")
    
    conn.close()

if __name__ == "__main__":
    main()
