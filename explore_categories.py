import pymssql

# Connection to actual server
conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB'
)
cursor = conn.cursor()

# 1. Get all unique categories with count
print("=" * 80)
print("ALL CATEGORIES IN DATABASE (top 60 with item counts)")
print("=" * 80)
cursor.execute("""
    SELECT RTRIM(LTRIM(category)) as cat, COUNT(*) as cnt
    FROM tblInvmas
    WHERE category IS NOT NULL AND category <> ''
    GROUP BY RTRIM(LTRIM(category))
    ORDER BY cnt DESC
""")
categories = cursor.fetchall()
for cat, cnt in categories[:60]:
    print(f"  '{cat}' => {cnt} items")

# 2. Check SCREEN category specifically
print("\n" + "=" * 80)
print("SCREEN CATEGORY ITEMS")
print("=" * 80)
cursor.execute("""
    SELECT TOP 20 product_code, RTRIM(LTRIM(category)) as cat, descriptionv6, PrintedDesc, product_type_v41
    FROM tblInvmas
    WHERE RTRIM(LTRIM(category)) LIKE '%SCREEN%'
    ORDER BY product_code
""")
for row in cursor.fetchall():
    is_pkg = "(PACKAGE)" if row[4] == 1 else ""
    print(f"  {row[0]}: [{row[1]}] {row[2] or row[3] or 'No desc'} {is_pkg}")

# 3. Check W/MIC category specifically
print("\n" + "=" * 80)
print("W/MIC (Wireless Microphone) CATEGORY ITEMS with pricing")
print("=" * 80)
cursor.execute("""
    SELECT TOP 30 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, i.PrintedDesc, 
           i.product_type_v41, r.rate_1st_day
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE RTRIM(LTRIM(i.category)) = 'W/MIC'
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    is_pkg = "(PACKAGE)" if row[4] == 1 else ""
    price = f"${row[5]}" if row[5] else "$0"
    print(f"  {row[0]}: [{row[1]}] {row[2] or row[3] or 'No desc'} - {price} {is_pkg}")

# 4. Check what has 'lapel' or 'clip' or 'lavalier' in description
print("\n" + "=" * 80)
print("LAPEL/CLIP/LAVALIER MICROPHONES")
print("=" * 80)
cursor.execute("""
    SELECT TOP 30 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day, i.product_type_v41
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE LOWER(i.descriptionv6) LIKE '%lapel%' 
       OR LOWER(i.descriptionv6) LIKE '%clip%'
       OR LOWER(i.descriptionv6) LIKE '%lavalier%'
       OR LOWER(i.descriptionv6) LIKE '%beltpack%'
       OR LOWER(i.PrintedDesc) LIKE '%lapel%'
       OR LOWER(i.PrintedDesc) LIKE '%lavalier%'
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    is_pkg = "(PKG)" if row[4] == 1 else ""
    price = f"${row[3]}" if row[3] else "$0"
    print(f"  {row[0]}: [{row[1]}] {row[2]} - {price} {is_pkg}")

# 5. Check PROJECTR category
print("\n" + "=" * 80)
print("PROJECTR CATEGORY - Projectors with pricing")
print("=" * 80)
cursor.execute("""
    SELECT TOP 20 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE RTRIM(LTRIM(i.category)) = 'PROJECTR'
    AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]}: [{row[1]}] {row[2]} - ${row[3]}")

# 6. Check categories that might be screens
print("\n" + "=" * 80)
print("SEARCHING FOR SCREEN-RELATED CATEGORIES")
print("=" * 80)
cursor.execute("""
    SELECT DISTINCT RTRIM(LTRIM(category)) as cat
    FROM tblInvmas
    WHERE LOWER(category) LIKE '%screen%'
       OR LOWER(category) LIKE '%proj%'
       OR LOWER(category) LIKE '%fastfold%'
       OR LOWER(category) LIKE '%display%'
""")
for row in cursor.fetchall():
    print(f"  Category: '{row[0]}'")

# 7. Search for screens by description
print("\n" + "=" * 80)
print("ITEMS WITH 'SCREEN' OR 'FASTFOLD' IN DESCRIPTION (with pricing)")
print("=" * 80)
cursor.execute("""
    SELECT TOP 30 i.product_code, RTRIM(LTRIM(i.category)) as cat, i.descriptionv6, r.rate_1st_day, i.product_type_v41
    FROM tblInvmas i
    LEFT JOIN tblRatetbl r ON LTRIM(RTRIM(i.product_code)) = LTRIM(RTRIM(r.ProductCode)) AND r.tableNo = 0
    WHERE (LOWER(i.descriptionv6) LIKE '%screen%' OR LOWER(i.descriptionv6) LIKE '%fastfold%')
    AND LOWER(i.descriptionv6) NOT LIKE '%touch screen%'
    AND LOWER(i.descriptionv6) NOT LIKE '%screenshot%'
    AND r.rate_1st_day > 0
    ORDER BY r.rate_1st_day DESC
""")
for row in cursor.fetchall():
    is_pkg = "(PKG)" if row[4] == 1 else ""
    print(f"  {row[0]}: [{row[1]}] {row[2]} - ${row[3]} {is_pkg}")

# 8. Check groupFld for VISION group items
print("\n" + "=" * 80)
print("VISION GROUP - All Categories")
print("=" * 80)
cursor.execute("""
    SELECT RTRIM(LTRIM(category)) as cat, COUNT(*) as cnt
    FROM tblInvmas
    WHERE RTRIM(LTRIM(groupFld)) = 'VISION'
    GROUP BY RTRIM(LTRIM(category))
    ORDER BY cnt DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]} => {row[1]} items")

# 9. Check AUDIO group categories
print("\n" + "=" * 80)
print("AUDIO GROUP - All Categories")
print("=" * 80)
cursor.execute("""
    SELECT RTRIM(LTRIM(category)) as cat, COUNT(*) as cnt
    FROM tblInvmas
    WHERE RTRIM(LTRIM(groupFld)) = 'AUDIO'
    GROUP BY RTRIM(LTRIM(category))
    ORDER BY cnt DESC
""")
for row in cursor.fetchall():
    print(f"  {row[0]} => {row[1]} items")

conn.close()
print("\nDone!")
