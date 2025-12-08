import pymssql

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB'
)
cursor = conn.cursor()

print("=== WIRELESS MIC (W/MIC) ITEMS ===")
cursor.execute("""
    SELECT product_code, descriptionv6, category, groupFld 
    FROM tblInvmas 
    WHERE category = 'W/MIC'
    ORDER BY descriptionv6
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]} | cat={row[2]} | grp={row[3]}")

print("\n=== LAPEL/LAVALIER MIC ITEMS ===")
cursor.execute("""
    SELECT product_code, descriptionv6, category, groupFld 
    FROM tblInvmas 
    WHERE descriptionv6 LIKE '%lapel%' OR descriptionv6 LIKE '%lavalier%' OR descriptionv6 LIKE '%beltpack%'
    ORDER BY descriptionv6
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]} | cat={row[2]} | grp={row[3]}")

print("\n=== HANDHELD MIC ITEMS ===")
cursor.execute("""
    SELECT product_code, descriptionv6, category, groupFld 
    FROM tblInvmas 
    WHERE descriptionv6 LIKE '%handheld%'
    ORDER BY descriptionv6
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]} | cat={row[2]} | grp={row[3]}")

print("\n=== ITEMS WITH PRICING (rate_1st_day > 0) - LAPTOPS ===")
cursor.execute("""
    SELECT TOP 10 i.product_code, i.descriptionv6, i.category, r.rate_1st_day
    FROM tblInvmas i
    JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE LTRIM(RTRIM(i.category)) = 'LAPTOP' AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]} | ${row[3]}")

print("\n=== ITEMS WITH PRICING - PROJECTORS ===")
cursor.execute("""
    SELECT TOP 10 i.product_code, i.descriptionv6, i.category, r.rate_1st_day
    FROM tblInvmas i
    JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE i.category = 'PROJECTR' AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]} | ${row[3]}")

print("\n=== ITEMS WITH PRICING - SCREENS ===")
cursor.execute("""
    SELECT TOP 10 i.product_code, i.descriptionv6, i.category, r.rate_1st_day
    FROM tblInvmas i
    JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE i.category = 'SCREEN' AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]} | ${row[3]}")

print("\n=== ITEMS WITH PRICING - WIRELESS MICS ===")
cursor.execute("""
    SELECT TOP 15 i.product_code, i.descriptionv6, i.category, r.rate_1st_day
    FROM tblInvmas i
    JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE i.category = 'W/MIC' AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: {row[1]} | ${row[3]}")

conn.close()
