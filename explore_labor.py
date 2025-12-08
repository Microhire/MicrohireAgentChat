import pymssql

conn = pymssql.connect(server='192.168.1.120', user='sa', password='Dataman01', database='monacip_prod_copy')
cursor = conn.cursor(as_dict=True)

print("=== SAMPLE LABOR/CREW RECORDS FROM tblcrew ===")
cursor.execute("""
    SELECT TOP 5 
        c.ID, c.booking_no_v32, c.product_code_v42, c.trans_qty, c.price, c.unitRate,
        c.hours, c.Minutes, c.task, c.person, c.techrateIsHourorDay,
        c.del_time_hour, c.del_time_min, c.return_time_hour, c.return_time_min,
        c.StraightTime, c.seq_no, c.sub_seq_no, c.heading_no
    FROM TblCrew c
    WHERE c.booking_no_v32 LIKE 'C118%'
    ORDER BY c.booking_no_v32, c.seq_no
""")
for row in cursor.fetchall():
    print("\n--- Crew Record ---")
    for k, v in row.items():
        if v is not None:
            print(f"  {k}: {v}")

print("\n\n=== LABOR RATES FROM tblInvmas_Labour_Rates (AVTECH ID=2939, Locn=20) ===")
cursor.execute("""
    SELECT ID, tblInvmasID, Labour_rate, Locn, IsDefault, rate_no
    FROM tblInvmas_Labour_Rates 
    WHERE tblInvmasID = 2939 AND Locn = 20
    ORDER BY IsDefault DESC
""")
for row in cursor.fetchall():
    print("\n--- Rate Record ---")
    for k, v in row.items():
        if v is not None:
            print(f"  {k}: {v}")

print("\n\n=== ALL LOCN=20 RATES ===")
cursor.execute("""
    SELECT TOP 10 ID, tblInvmasID, Labour_rate, Locn, IsDefault
    FROM tblInvmas_Labour_Rates 
    WHERE Locn = 20
    ORDER BY Labour_rate DESC
""")
for row in cursor.fetchall():
    print(f"  ID={row['ID']}, InvmasID={row['tblInvmasID']}, Rate={row['Labour_rate']}, Default={row['IsDefault']}")

print("\n\n=== TASK TYPES IN USE (from existing crew records) ===")
cursor.execute("""
    SELECT DISTINCT task, COUNT(*) as count
    FROM TblCrew
    WHERE task IS NOT NULL
    GROUP BY task
    ORDER BY count DESC
""")
for row in cursor.fetchall():
    print(f"  Task {row['task']}: {row['count']} records")

print("\n\n=== SAMPLE BOOKING WITH CREW (C1184900002) ===")
cursor.execute("""
    SELECT booking_no, showStartTime, ShowEndTime, setupTimeV61, StrikeTime, RehearsalTime,
           labour, hire_price, price_quoted
    FROM tblbookings WHERE booking_no = 'C1184900002'
""")
row = cursor.fetchone()
if row:
    print("Booking Details:")
    for k, v in row.items():
        if v is not None:
            print(f"  {k}: {v}")

cursor.execute("""
    SELECT * FROM TblCrew WHERE booking_no_v32 = 'C1184900002' ORDER BY seq_no
""")
print("\nCrew for this booking:")
for row in cursor.fetchall():
    print(f"\n  Task={row['task']}, Qty={row['trans_qty']}, Hours={row['hours']}, Mins={row['Minutes']}, Price={row['price']}")
    print(f"  ProductCode={row['product_code_v42']}, Person='{row['person']}', HourOrDay={row['techrateIsHourorDay']}")
    print(f"  DelTime={row['del_time_hour']}:{row['del_time_min']}, ReturnTime={row['return_time_hour']}:{row['return_time_min']}")
    print(f"  StraightTime={row['StraightTime']}, seq_no={row['seq_no']}")

conn.close()
