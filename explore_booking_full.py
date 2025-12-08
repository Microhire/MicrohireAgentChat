import pymssql

try:
    conn = pymssql.connect(
        server='116.90.5.144',
        port=41383,
        user='PowerBI-Consult',
        password='2tW@ostq3a3_9oV3m-TBQu3w',
        database='AITESTDB'
    )
    cursor = conn.cursor(as_dict=True)
    
    # Get column info for tblitemtran
    print("=== COLUMNS IN tblitemtran ===")
    cursor.execute("""
        SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'tblitemtran' 
        ORDER BY ORDINAL_POSITION
    """)
    for row in cursor.fetchall():
        print(f"  {row['COLUMN_NAME']} ({row['DATA_TYPE']})")
    
    # Get column info for tblcrew
    print("\n=== COLUMNS IN tblcrew ===")
    cursor.execute("""
        SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'tblcrew' 
        ORDER BY ORDINAL_POSITION
    """)
    for row in cursor.fetchall():
        print(f"  {row['COLUMN_NAME']} ({row['DATA_TYPE']})")
    
    # Get most recent booking
    print("\n=== MOST RECENT BOOKING ===")
    cursor.execute("""
        SELECT TOP 1 booking_no, ID FROM tblbookings 
        WHERE booking_no IS NOT NULL
        ORDER BY EntryDate DESC
    """)
    booking = cursor.fetchone()
    if booking:
        booking_no = booking['booking_no']
        print(f"Booking: {booking_no}")
        
        # Get items - find correct column name
        cursor.execute("""
            SELECT TOP 5 * FROM tblitemtran WHERE booking_no_v32 = %s ORDER BY seq_no, sub_seq_no
        """, (booking_no,))
        items = cursor.fetchall()
        print(f"\n=== EQUIPMENT (tblItemtran) for {booking_no} ({len(items)} items) ===")
        for item in items:
            print(f"\n  --- Item ---")
            for col, val in item.items():
                if val is not None and str(val).strip():
                    print(f"    {col}: {val}")
        
        # Get crew
        cursor.execute("""
            SELECT * FROM tblcrew WHERE booking_no_v32 = %s ORDER BY Task
        """, (booking_no,))
        crew = cursor.fetchall()
        print(f"\n=== LABOR (tblCrew) for {booking_no} ({len(crew)} crew) ===")
        for c in crew:
            print(f"\n  --- Crew ---")
            for col, val in c.items():
                if val is not None and str(val).strip():
                    print(f"    {col}: {val}")
    
    # Labor rates
    print("\n\n=== LABOR RATES (tblInvmas_Labour_Rates with tblinvmasID=2939) ===")
    cursor.execute("""
        SELECT TOP 5 * FROM tblInvmas_Labour_Rates WHERE tblinvmasID = 2939 AND Locn = 20 AND IsDefault = 1
    """)
    rates = cursor.fetchall()
    for r in rates:
        print("\n--- Rate ---")
        for col, val in r.items():
            if val is not None and str(val).strip():
                print(f"  {col}: {val}")
    
    conn.close()
    
except Exception as e:
    print(f"Error: {e}")
    import traceback
    traceback.print_exc()
