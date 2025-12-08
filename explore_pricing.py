import pymssql

conn = pymssql.connect(
    server='103.4.19.170',
    user='micrologin',
    password='m1cr0loG1n',
    database='MBMaster'
)
cursor = conn.cursor()

print("="*60)
print("EXPLORING RATE TABLES")
print("="*60)

# Check tblRatetbl structure
print("\n--- tblRatetbl columns ---")
cursor.execute("""
    SELECT COLUMN_NAME, DATA_TYPE 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'tblRatetbl'
    ORDER BY ORDINAL_POSITION
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]}")

# Sample rate data
print("\n--- Sample rate data (first 5) ---")
cursor.execute("""
    SELECT TOP 5 * FROM tblRatetbl
""")
cols = [col[0] for col in cursor.description]
print(f"  Columns: {cols}")
for row in cursor.fetchall():
    print(f"  {row}")

# Check rates for a specific product (Dell G7)
print("\n--- Rates for Dell G7 laptop (DELLG7) ---")
cursor.execute("""
    SELECT * FROM tblRatetbl WHERE product_code LIKE '%DELLG7%'
""")
for row in cursor.fetchall():
    print(f"  {row}")

# Check rates for projector
print("\n--- Rates for Panasonic projector ---")
cursor.execute("""
    SELECT TOP 3 * FROM tblRatetbl WHERE product_code LIKE '%DZ870%'
""")
for row in cursor.fetchall():
    print(f"  {row}")

print("\n" + "="*60)
print("EXPLORING tblInvmas PRICE COLUMNS")
print("="*60)

# Check price columns in tblInvmas
print("\n--- Sample product with prices ---")
cursor.execute("""
    SELECT TOP 3 product_code, descriptionv6, cost_price, retail_price, wholesale_price, trade_price
    FROM tblInvmas 
    WHERE product_code = 'DELLG7'
""")
for row in cursor.fetchall():
    print(f"  {row}")

print("\n" + "="*60)
print("EXPLORING tblItemtran (booking items)")
print("="*60)

# Check tblItemtran structure
print("\n--- tblItemtran columns ---")
cursor.execute("""
    SELECT COLUMN_NAME, DATA_TYPE 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'tblItemtran'
    ORDER BY ORDINAL_POSITION
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]}")

# Sample booking items
print("\n--- Sample booking items (recent) ---")
cursor.execute("""
    SELECT TOP 5 ID, BookingId, product_code, quantity, description, 
           price_1stday, price_addtl_day, total_price
    FROM tblItemtran
    ORDER BY ID DESC
""")
cols = [col[0] for col in cursor.description]
print(f"  Columns: {cols}")
for row in cursor.fetchall():
    print(f"  {row}")

conn.close()
print("\nDone!")
