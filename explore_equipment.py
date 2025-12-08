import pymssql
from tabulate import tabulate

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB',
    as_dict=True
)
cursor = conn.cursor()

print("=" * 80)
print("EXPLORING EQUIPMENT DATABASE - tblinvmas")
print("=" * 80)

# 1. Check laptop-related items
print("\n1. LAPTOP-RELATED ITEMS (searching in description, category, groupFld):")
cursor.execute('''
    SELECT TOP 20 
        product_code, 
        descriptionv6,
        PrintedDesc,
        category, 
        groupFld,
        SubCategory,
        retail_price,
        PictureFileName
    FROM tblinvmas 
    WHERE descriptionv6 LIKE '%laptop%' 
       OR PrintedDesc LIKE '%laptop%'
       OR product_code LIKE '%LAPTOP%'
       OR category LIKE '%LAPTOP%'
    ORDER BY product_code
''')
laptops = cursor.fetchall()
print(f"Found {len(laptops)} laptop items:")
for row in laptops:
    print(f"  Code: {row['product_code']:<15} | Desc: {(row['descriptionv6'] or row['PrintedDesc'] or '')[:50]:<50} | Cat: {row['category']} | Group: {row['groupFld']}")

# 2. Check computer group
print("\n2. COMPUTER GROUP ITEMS (groupFld = 'COMPUTER'):")
cursor.execute('''
    SELECT TOP 20 
        product_code, 
        descriptionv6,
        category, 
        groupFld,
        retail_price
    FROM tblinvmas 
    WHERE groupFld = 'COMPUTER'
    ORDER BY category, product_code
''')
computers = cursor.fetchall()
print(f"Found items in COMPUTER group. Sample:")
for row in computers:
    print(f"  Code: {row['product_code']:<15} | Desc: {(row['descriptionv6'] or '')[:45]:<45} | Cat: {row['category']}")

# 3. Check Windows-related items
print("\n3. WINDOWS-RELATED ITEMS:")
cursor.execute('''
    SELECT TOP 15 
        product_code, 
        descriptionv6,
        PrintedDesc,
        category, 
        groupFld
    FROM tblinvmas 
    WHERE descriptionv6 LIKE '%windows%' 
       OR PrintedDesc LIKE '%windows%'
       OR descriptionv6 LIKE '%dell%'
       OR descriptionv6 LIKE '%lenovo%'
       OR descriptionv6 LIKE '%hp %'
    ORDER BY product_code
''')
windows = cursor.fetchall()
print(f"Found {len(windows)} Windows/PC items:")
for row in windows:
    print(f"  Code: {row['product_code']:<15} | Desc: {(row['descriptionv6'] or row['PrintedDesc'] or '')[:50]}")

# 4. Check MacBook items
print("\n4. MACBOOK ITEMS:")
cursor.execute('''
    SELECT TOP 15 
        product_code, 
        descriptionv6,
        category, 
        groupFld,
        PictureFileName
    FROM tblinvmas 
    WHERE descriptionv6 LIKE '%macbook%' 
       OR PrintedDesc LIKE '%macbook%'
       OR product_code LIKE '%MAC%'
    ORDER BY product_code
''')
macs = cursor.fetchall()
print(f"Found {len(macs)} MacBook items:")
for row in macs:
    print(f"  Code: {row['product_code']:<15} | Desc: {(row['descriptionv6'] or '')[:50]} | Pic: {row['PictureFileName']}")

# 5. Check projector items
print("\n5. PROJECTOR ITEMS:")
cursor.execute('''
    SELECT TOP 15 
        product_code, 
        descriptionv6,
        category, 
        groupFld,
        PictureFileName
    FROM tblinvmas 
    WHERE descriptionv6 LIKE '%projector%' 
       OR category = 'PROJECTR'
       OR groupFld = 'PROJECTR'
    ORDER BY product_code
''')
projectors = cursor.fetchall()
print(f"Found {len(projectors)} projector items:")
for row in projectors:
    print(f"  Code: {row['product_code']:<15} | Desc: {(row['descriptionv6'] or '')[:45]:<45} | Cat: {row['category']}")

# 6. Check microphone items
print("\n6. MICROPHONE ITEMS:")
cursor.execute('''
    SELECT TOP 15 
        product_code, 
        descriptionv6,
        category, 
        groupFld
    FROM tblinvmas 
    WHERE descriptionv6 LIKE '%microphone%' 
       OR descriptionv6 LIKE '%mic%'
       OR category LIKE '%MIC%'
    ORDER BY product_code
''')
mics = cursor.fetchall()
print(f"Found {len(mics)} microphone items:")
for row in mics:
    print(f"  Code: {row['product_code']:<15} | Desc: {(row['descriptionv6'] or '')[:45]:<45} | Cat: {row['category']}")

# 7. Categories and Groups summary
print("\n7. ALL CATEGORIES (with counts):")
cursor.execute('''
    SELECT category, COUNT(*) as cnt 
    FROM tblinvmas 
    WHERE category IS NOT NULL
    GROUP BY category 
    ORDER BY cnt DESC
''')
cats = cursor.fetchall()
print("Top 20 categories:")
for row in cats[:20]:
    print(f"  {row['category']:<15}: {row['cnt']} items")

print("\n8. ALL GROUPS (groupFld with counts):")
cursor.execute('''
    SELECT groupFld, COUNT(*) as cnt 
    FROM tblinvmas 
    WHERE groupFld IS NOT NULL
    GROUP BY groupFld 
    ORDER BY cnt DESC
''')
groups = cursor.fetchall()
for row in groups[:15]:
    print(f"  {row['groupFld']:<15}: {row['cnt']} items")

conn.close()
print("\n" + "=" * 80)
