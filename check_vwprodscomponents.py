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
    
    # Sample data for package PCLPRO
    print("=== vwProdsComponents for PCLPRO (variable_part=0) ===")
    cursor.execute("""
        SELECT * FROM vwProdsComponents 
        WHERE parent_code = 'PCLPRO' AND variable_part = 0
        ORDER BY sub_seq_no
    """)
    for row in cursor.fetchall():
        print("\n--- Component ---")
        for col, val in row.items():
            if val is not None and str(val).strip():
                print(f"  {col}: {val}")
    
    # Check another sample booking with crew/labor
    print("\n\n=== FINDING A BOOKING WITH CREW ===")
    cursor.execute("""
        SELECT TOP 1 c.*, b.ShowSdate, b.ShowEdate, b.showStartTime, b.ShowEndTime, b.setupTimeV61
        FROM tblcrew c 
        JOIN tblbookings b ON c.booking_no_v32 = b.booking_no
        WHERE c.price > 0
        ORDER BY c.ID DESC
    """)
    row = cursor.fetchone()
    if row:
        print("\n--- Crew Row with all details ---")
        for col, val in row.items():
            if val is not None and str(val).strip():
                print(f"  {col}: {val}")
    
    conn.close()
    
except Exception as e:
    print(f"Error: {e}")
    import traceback
    traceback.print_exc()
