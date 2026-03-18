#!/usr/bin/env python3
"""Explore venue-specific AV packages in tblInvmas (equipment table)."""

import pymssql

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB',
    as_dict=True
)
cursor = conn.cursor()

print("=" * 90)
print("VENUE/ROOM-SPECIFIC PACKAGES IN tblInvmas (equipment table)")
print("=" * 90)

# 1. Product codes from item-rules.json (Westin Brisbane packages)
westin_codes = [
    'WSBBSPRO', 'WSBBDPRO',  # Westin Ballroom Projector
    'WSBALLAU', 'WSBFBALL',  # Westin Ballroom Audio
    'WSBELAUD', 'WSBELSAD',  # Elevate Audio
    'WSBTHAUD', 'WSBTHAV',   # Thrive Audio/AV
]
print("\n1. WESTIN BRISBANE PACKAGES (from item-rules.json product codes):")
print("-" * 90)
cursor.execute("""
    SELECT product_code, descriptionv6, PrintedDesc, category, groupFld, 
           product_type_v41, product_Config, retail_price, SubCategory
    FROM tblInvmas 
    WHERE RTRIM(product_code) IN %s
    ORDER BY product_code
""", [tuple(c.strip() for c in westin_codes)])
rows = cursor.fetchall()
if rows:
    for r in rows:
        desc = (r['descriptionv6'] or r['PrintedDesc'] or '').strip()
        cat = (r['category'] or '')[:10]
        typ = r.get('product_type_v41', '')
        print("  %-12s | %-55s | Cat: %-10s | Type: %s" % (r['product_code'].strip(), desc[:55], cat, typ))
else:
    print("  No rows found - trying individual codes...")
    for code in westin_codes:
        cursor.execute("""
            SELECT product_code, descriptionv6, category FROM tblInvmas 
            WHERE RTRIM(product_code) = %s
        """, (code,))
        r = cursor.fetchone()
        status = 'FOUND - ' + (r['descriptionv6'] or '')[:50] if r else 'NOT FOUND'
        print("  %s: %s" % (code, status))

# 2. Venue Installed items
print("\n2. VENUE INSTALLED ITEMS (description contains 'Venue Installed'):")
print("-" * 90)
cursor.execute("""
    SELECT TOP 30 product_code, descriptionv6, PrintedDesc, category, groupFld
    FROM tblInvmas 
    WHERE (descriptionv6 LIKE '%Venue Installed%' OR PrintedDesc LIKE '%Venue Installed%')
    ORDER BY product_code
""")
rows = cursor.fetchall()
for r in rows:
    desc = (r['descriptionv6'] or r['PrintedDesc'] or '').strip()
    print("  %-15s | %-60s | %s" % (r['product_code'].strip(), desc[:60], r['category'] or ''))

# 3. Westin / Ballroom / Elevate / Thrive / Four Points in description
print("\n3. PACKAGES CONTAINING WESTIN, BALLROOM, ELEVATE, THRIVE, FOUR POINTS:")
print("-" * 90)
cursor.execute("""
    SELECT product_code, descriptionv6, PrintedDesc, category, groupFld, product_Config
    FROM tblInvmas 
    WHERE (descriptionv6 LIKE '%Westin%' OR PrintedDesc LIKE '%Westin%'
           OR descriptionv6 LIKE '%Ballroom%' OR PrintedDesc LIKE '%Ballroom%'
           OR descriptionv6 LIKE '%Elevate%' OR PrintedDesc LIKE '%Elevate%'
           OR descriptionv6 LIKE '%Thrive%' OR PrintedDesc LIKE '%Thrive%'
           OR descriptionv6 LIKE '%Four Points%' OR PrintedDesc LIKE '%Four Points%')
    ORDER BY descriptionv6
""")
rows = cursor.fetchall()
for r in rows:
    desc = (r['descriptionv6'] or r['PrintedDesc'] or '').strip()
    pkg = '(Package)' if r.get('product_Config') == 1 else ''
    cat = (r['category'] or '')[:10]
    print("  %-15s | %-55s | %-10s %s" % (r['product_code'].strip(), desc[:55], cat, pkg))

# 4. Pricing for Westin packages (tblRatetbl)
print("\n4. PRICING FOR WESTIN PACKAGES (tblRatetbl, TableNo=0):")
print("-" * 90)
cursor.execute("""
    SELECT r.ProductCode, r.tableNo, r.rate_1st_day, r.rate_extra_days, r.rate_week
    FROM tblRatetbl r
    WHERE r.tableNo = 0 AND RTRIM(r.ProductCode) IN %s
    ORDER BY r.ProductCode
""", [tuple(c.strip() for c in westin_codes)])
rows = cursor.fetchall()
if rows:
    for r in rows:
        r1 = float(r['rate_1st_day'] or 0)
        re = float(r['rate_extra_days'] or 0)
        rw = float(r['rate_week'] or 0)
        code = r.get('ProductCode') or r.get('product_code') or ''
        print("  %-12s | 1st: $%.2f | Extra: $%.2f | Week: $%.2f" % (str(code).strip(), r1, re, rw))
else:
    print("  No pricing found for Westin codes")

# 5. Component codes: VACSPKR, VVSCR120, EBG7400U, EB1450UI
print("\n5. COMPONENT CODES (VACSPKR, VVSCR*, screens, projectors):")
print("-" * 90)
comp_codes = ['VACSPKR', 'VVSCR120', 'VVSCR115', 'VVSCR100', 'EBG7400U', 'EB1450UI']
cursor.execute("""
    SELECT product_code, descriptionv6, PrintedDesc, category
    FROM tblInvmas 
    WHERE RTRIM(product_code) IN ('VACSPKR','VVSCR120','VVSCR115','VVSCR100','EBG7400U','EB1450UI')
       OR (descriptionv6 LIKE '%Ceiling LoudSpeaker%' OR descriptionv6 LIKE '%Motorised Projection%')
    ORDER BY product_code
""")
rows = cursor.fetchall()
for r in rows:
    desc = (r['descriptionv6'] or r['PrintedDesc'] or '').strip()
    print("  %-15s | %-60s | %s" % (r['product_code'].strip(), desc[:60], r['category'] or ''))

# 6. vwProdsComponents - packages containing these components
print("\n6. PACKAGES CONTAINING VACSPKR (Venue Installed Ceiling LoudSpeaker):")
print("-" * 90)
cursor.execute("""
    SELECT v.parent_code, v.product_code, v.qty_v5, p.descriptionv6 as parent_desc
    FROM vwProdsComponents v
    LEFT JOIN tblInvmas p ON RTRIM(v.parent_code) = RTRIM(p.product_code)
    WHERE RTRIM(v.product_code) = 'VACSPKR'
    ORDER BY v.parent_code
""")
rows = cursor.fetchall()
for r in rows:
    pdesc = str(r['parent_desc'] or '')[:45]
    print("  Parent: %-15s | Contains: %s x%s | %s" % (r['parent_code'].strip(), r['product_code'].strip(), r['qty_v5'], pdesc))

# 7. All packages with "Package" in description (AV/vision/audio packages)
print("\n7. AV/VISION/AUDIO PACKAGES (description contains 'Package'):")
print("-" * 90)
cursor.execute("""
    SELECT TOP 50 product_code, descriptionv6, category, product_Config
    FROM tblInvmas 
    WHERE (descriptionv6 LIKE '%Package%' OR PrintedDesc LIKE '%Package%')
      AND (descriptionv6 LIKE '%Projector%' OR descriptionv6 LIKE '%Speaker%' 
           OR descriptionv6 LIKE '%Audio%' OR descriptionv6 LIKE '%Vision%'
           OR descriptionv6 LIKE '%AV%' OR descriptionv6 LIKE '%Ballroom%'
           OR descriptionv6 LIKE '%Elevate%' OR descriptionv6 LIKE '%Thrive%'
           OR descriptionv6 LIKE '%Four Points%')
    ORDER BY descriptionv6
""")
rows = cursor.fetchall()
for r in rows:
    desc = (r['descriptionv6'] or '').strip()[:55]
    print("  %-15s | %-55s | %s" % (r['product_code'].strip(), desc, r['category'] or ''))

conn.close()
print("\n" + "=" * 90)
print("DONE")
print("=" * 90)
