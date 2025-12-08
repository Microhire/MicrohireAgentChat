#!/usr/bin/env python3
"""Explore package and component structure in the database."""

import pymssql

# Connection details from appsettings.json
conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB'
)

cursor = conn.cursor(as_dict=True)

print("=" * 80)
print("1. EXPLORING PCLPRO PACKAGE (PC Show Laptop Production Level Pro)")
print("=" * 80)

# Get the PCLPRO package details from tblInvmas
cursor.execute("""
    SELECT TOP 5 
        product_code, 
        COALESCE(descriptionv6, PrintedDesc) as description,
        product_type_v41,
        category,
        groupFld,
        retail_price
    FROM tblInvmas 
    WHERE product_code LIKE '%PCLPRO%' OR product_code LIKE '%DELL3580%'
    ORDER BY product_code
""")
print("\n--- Products matching PCLPRO or DELL3580 ---")
for row in cursor.fetchall():
    print(f"  Code: {row['product_code'].strip()}")
    print(f"    Description: {row['description'].strip() if row['description'] else 'N/A'}")
    print(f"    Type: {row['product_type_v41']}, Category: {row['category']}, Group: {row['groupFld']}")
    print(f"    Retail Price: {row['retail_price']}")
    print()

print("\n" + "=" * 80)
print("2. COMPONENTS OF PCLPRO PACKAGE (from vwProdsComponents)")
print("=" * 80)

cursor.execute("""
    SELECT 
        parent_code,
        product_code,
        qty_v5,
        product_type_v41,
        DescriptionV6,
        SelectComp,
        variable_part
    FROM vwProdsComponents 
    WHERE RTRIM(parent_code) = 'PCLPRO'
    ORDER BY sub_seq_no
""")
print("\n--- Components of PCLPRO ---")
for row in cursor.fetchall():
    print(f"  Component: {row['product_code'].strip()}")
    print(f"    Description: {row['DescriptionV6'].strip() if row['DescriptionV6'] else 'N/A'}")
    print(f"    Qty: {row['qty_v5']}, Type: {row['product_type_v41']}, SelectComp: {row['SelectComp']}, Variable: {row['variable_part']}")
    print()

print("\n" + "=" * 80)
print("3. PRICING FOR PCLPRO FROM tblRatetbl")
print("=" * 80)

cursor.execute("""
    SELECT 
        product_code,
        TableNo,
        rate_1st_day,
        rate_extra_day,
        rate_weekly
    FROM tblRatetbl 
    WHERE RTRIM(product_code) = 'PCLPRO'
    ORDER BY TableNo
""")
print("\n--- Rates for PCLPRO ---")
for row in cursor.fetchall():
    print(f"  TableNo: {row['TableNo']}, 1st Day: ${row['rate_1st_day']}, Extra: ${row['rate_extra_day']}, Weekly: ${row['rate_weekly']}")

print("\n" + "=" * 80)
print("4. UNDERSTANDING product_type_v41 VALUES")
print("=" * 80)

cursor.execute("""
    SELECT 
        product_type_v41,
        COUNT(*) as count,
        STRING_AGG(RTRIM(product_code), ', ') as examples
    FROM (
        SELECT TOP 3 product_code, product_type_v41
        FROM tblInvmas
        WHERE product_type_v41 IS NOT NULL
        GROUP BY product_code, product_type_v41
    ) sub
    GROUP BY product_type_v41
    ORDER BY product_type_v41
""")

# Alternative simpler query
cursor.execute("""
    SELECT DISTINCT product_type_v41, COUNT(*) as cnt
    FROM tblInvmas
    WHERE product_type_v41 IS NOT NULL
    GROUP BY product_type_v41
    ORDER BY product_type_v41
""")
print("\n--- Product Type Distribution ---")
for row in cursor.fetchall():
    print(f"  Type {row['product_type_v41']}: {row['cnt']} products")

# Get examples for each type
for type_val in [0, 1, 2, 3, 4, 5]:
    cursor.execute(f"""
        SELECT TOP 3 RTRIM(product_code) as code, COALESCE(descriptionv6, PrintedDesc) as desc
        FROM tblInvmas
        WHERE product_type_v41 = {type_val}
    """)
    rows = cursor.fetchall()
    if rows:
        examples = [f"{r['code'].strip()} ({r['desc'].strip() if r['desc'] else 'N/A'})" for r in rows]
        print(f"    Type {type_val} examples: {', '.join(examples[:3])}")

print("\n" + "=" * 80)
print("5. HOW DELL3580 IS LINKED TO PACKAGES")
print("=" * 80)

cursor.execute("""
    SELECT 
        v.parent_code,
        p.descriptionv6 as parent_desc,
        p.product_type_v41 as parent_type,
        v.product_code,
        v.qty_v5,
        v.SelectComp,
        r.rate_1st_day as parent_rate
    FROM vwProdsComponents v
    LEFT JOIN tblInvmas p ON RTRIM(v.parent_code) = RTRIM(p.product_code)
    LEFT JOIN tblRatetbl r ON RTRIM(v.parent_code) = RTRIM(r.product_code) AND r.TableNo = 0
    WHERE RTRIM(v.product_code) = 'DELL3580'
""")
print("\n--- Packages containing DELL3580 ---")
for row in cursor.fetchall():
    print(f"  Parent: {row['parent_code'].strip() if row['parent_code'] else 'N/A'}")
    print(f"    Parent Desc: {row['parent_desc'].strip() if row['parent_desc'] else 'N/A'}")
    print(f"    Parent Type: {row['parent_type']}, Parent Rate: ${row['parent_rate']}")
    print(f"    Qty in package: {row['qty_v5']}, SelectComp: {row['SelectComp']}")
    print()

print("\n" + "=" * 80)
print("6. CHECKING IF THERE'S A DIRECT RATE FOR DELL3580")
print("=" * 80)

cursor.execute("""
    SELECT 
        product_code,
        TableNo,
        rate_1st_day,
        rate_extra_day
    FROM tblRatetbl 
    WHERE RTRIM(product_code) = 'DELL3580'
""")
print("\n--- Direct rates for DELL3580 ---")
rows = cursor.fetchall()
if rows:
    for row in rows:
        print(f"  TableNo: {row['TableNo']}, 1st Day: ${row['rate_1st_day']}, Extra: ${row['rate_extra_day']}")
else:
    print("  No direct rates found for DELL3580 (it's a component)")

cursor.execute("""
    SELECT 
        product_code,
        retail_price,
        product_type_v41
    FROM tblInvmas 
    WHERE RTRIM(product_code) = 'DELL3580'
""")
print("\n--- DELL3580 retail price from tblInvmas ---")
for row in cursor.fetchall():
    print(f"  Retail Price: ${row['retail_price']}, Type: {row['product_type_v41']}")

conn.close()
print("\n" + "=" * 80)
print("DONE")
print("=" * 80)

