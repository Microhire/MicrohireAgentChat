import pymssql

# Test what the search would return
conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB'
)
cursor = conn.cursor()

# Test 1: Screen search (simulating the EF query)
print("=" * 80)
print("TEST 1: SCREEN SEARCH (what the service should return)")
print("=" * 80)
cursor.execute("""
    SELECT TOP 10 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE RTRIM(LTRIM(i.category)) IN ('SCREEN', 'GRNDVW')
    AND (
        LOWER(i.descriptionv6) LIKE '%screen%' OR
        LOWER(i.descriptionv6) LIKE '%fastfold%' OR
        LOWER(i.descriptionv6) LIKE '%projection%' OR
        LOWER(i.descriptionv6) LIKE '%stumpfl%'
    )
    AND LOWER(i.descriptionv6) NOT LIKE '%long term hire%'
    AND LOWER(i.descriptionv6) NOT LIKE '%discontinued%'
    AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: [{row[1]}] {row[2]} - ${row[3]}")

# Test 2: Wireless lapel/clip mic search
print("\n" + "=" * 80)
print("TEST 2: WIRELESS LAPEL/CLIP MIC SEARCH")
print("=" * 80)
cursor.execute("""
    SELECT TOP 10 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE RTRIM(LTRIM(i.category)) IN ('W/MIC')
    AND (
        LOWER(i.descriptionv6) LIKE '%lapel%' OR
        LOWER(i.descriptionv6) LIKE '%lavalier%' OR
        LOWER(i.descriptionv6) LIKE '%beltpack%' OR
        LOWER(i.descriptionv6) LIKE '%clip%'
    )
    AND LOWER(i.descriptionv6) NOT LIKE '%long term hire%'
    AND LOWER(i.descriptionv6) NOT LIKE '%discontinued%'
    AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: [{row[1]}] {row[2]} - ${row[3]}")

# Test 3: Check what 'wireless microphone' search would return
print("\n" + "=" * 80)
print("TEST 3: WIRELESS MICROPHONE SEARCH (general)")
print("=" * 80)
cursor.execute("""
    SELECT TOP 10 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE RTRIM(LTRIM(i.category)) IN ('W/MIC')
    AND (
        LOWER(i.descriptionv6) LIKE '%wireless%' OR
        LOWER(i.descriptionv6) LIKE '%radio mic%' OR
        LOWER(i.descriptionv6) LIKE '%shure%' OR
        LOWER(i.descriptionv6) LIKE '%mipro%'
    )
    AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: [{row[1]}] {row[2]} - ${row[3]}")

# Test 4: Check projector search
print("\n" + "=" * 80)
print("TEST 4: PROJECTOR SEARCH")
print("=" * 80)
cursor.execute("""
    SELECT TOP 10 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE RTRIM(LTRIM(i.category)) IN ('PROJECTR', 'EPSON')
    AND (
        LOWER(i.descriptionv6) LIKE '%projector%' OR
        LOWER(i.descriptionv6) LIKE '%lumen%' OR
        LOWER(i.descriptionv6) LIKE '%laser%'
    )
    AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: [{row[1]}] {row[2]} - ${row[3]}")

# Test 5: Check laptop search  
print("\n" + "=" * 80)
print("TEST 5: LAPTOP SEARCH (Windows)")
print("=" * 80)
cursor.execute("""
    SELECT TOP 10 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE RTRIM(LTRIM(i.category)) IN ('LAPTOP')
    AND (
        LOWER(i.descriptionv6) LIKE '%dell%' OR
        LOWER(i.descriptionv6) LIKE '%lenovo%' OR
        LOWER(i.descriptionv6) LIKE '%hp%' OR
        LOWER(i.descriptionv6) LIKE '%production level%' OR
        LOWER(i.descriptionv6) LIKE '%pc%'
    )
    AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: [{row[1]}] {row[2]} - ${row[3]}")

conn.close()
print("\nDone!")
