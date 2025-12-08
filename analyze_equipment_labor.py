#!/usr/bin/env python3
"""
Comprehensive analysis of equipment and labor products in tblinvmas
"""

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

print('=' * 80)
print('EQUIPMENT AND LABOR ANALYSIS')
print('=' * 80)

# 1. Product Types Overview
print('\n📊 PRODUCT TYPES OVERVIEW:')
print('-' * 80)
cursor.execute('''
    SELECT 
        product_type_v41,
        COUNT(*) as count,
        CASE 
            WHEN product_type_v41 = 0 THEN 'Hire Equipment (Individual Items)'
            WHEN product_type_v41 = 1 THEN 'Labour/Service Items'
            WHEN product_type_v41 = 2 THEN 'Packages'
            WHEN product_type_v41 = 3 THEN 'Venues'
            ELSE 'Other'
        END as type_name
    FROM tblinvmas
    GROUP BY product_type_v41
    ORDER BY product_type_v41
''')
types = cursor.fetchall()
print(tabulate(types, headers='keys', tablefmt='grid'))

# 2. Hire Equipment Analysis (product_type_v41 = 0)
print('\n\n🔧 HIRE EQUIPMENT (Individual Items) - Type 0:')
print('-' * 80)
print('Sample items:')
cursor.execute('''
    SELECT TOP 20
        product_code, descriptionV6, groupFld, SubCategory,
        hire_price, cost_price, on_hand, asset_track
    FROM tblinvmas
    WHERE product_type_v41 = 0
    ORDER BY product_code
''')
hire_items = cursor.fetchall()
for item in hire_items:
    print(f"  {item['product_code'].strip():20} | {item['descriptionV6'][:35]:35} | Group: {(item['groupFld'] or '')[:15]:15} | Price: ${item['hire_price'] or 0:7.2f} | Stock: {item['on_hand'] or 0:3.0f}")

# 3. Equipment Groups
print('\n\n📁 EQUIPMENT GROUPS (Top 20):')
print('-' * 80)
cursor.execute('''
    SELECT TOP 20
        groupFld, 
        COUNT(*) as item_count,
        AVG(hire_price) as avg_price,
        SUM(on_hand) as total_stock
    FROM tblinvmas
    WHERE product_type_v41 = 0 AND groupFld IS NOT NULL
    GROUP BY groupFld
    ORDER BY COUNT(*) DESC
''')
groups = cursor.fetchall()
print(tabulate(groups, headers='keys', tablefmt='grid'))

# 4. Equipment Categories
print('\n\n📂 EQUIPMENT CATEGORIES (Top 20):')
print('-' * 80)
cursor.execute('''
    SELECT TOP 20
        category, 
        COUNT(*) as item_count,
        AVG(hire_price) as avg_price
    FROM tblinvmas
    WHERE product_type_v41 = 0 AND category IS NOT NULL
    GROUP BY category
    ORDER BY COUNT(*) DESC
''')
categories = cursor.fetchall()
print(tabulate(categories, headers='keys', tablefmt='grid'))

# 5. Equipment SubCategories
print('\n\n📑 EQUIPMENT SUBCATEGORIES (Top 20):')
print('-' * 80)
cursor.execute('''
    SELECT TOP 20
        SubCategory, 
        COUNT(*) as item_count,
        AVG(hire_price) as avg_price
    FROM tblinvmas
    WHERE product_type_v41 = 0 AND SubCategory IS NOT NULL AND SubCategory != ''
    GROUP BY SubCategory
    ORDER BY COUNT(*) DESC
''')
subcategories = cursor.fetchall()
print(tabulate(subcategories, headers='keys', tablefmt='grid'))

# 6. Labor Items Analysis (product_type_v41 = 1)
print('\n\n👷 LABOR/SERVICE ITEMS - Type 1:')
print('-' * 80)
cursor.execute('''
    SELECT 
        product_code, descriptionV6, groupFld, SubCategory,
        hire_price, cost_price, person_required
    FROM tblinvmas
    WHERE product_type_v41 = 1
    ORDER BY product_code
''')
labor_items = cursor.fetchall()
print(f'Total labor items: {len(labor_items)}\n')
print(tabulate(labor_items, headers='keys', tablefmt='grid'))

# 7. Labor Groups
print('\n\n👥 LABOR GROUPS:')
print('-' * 80)
cursor.execute('''
    SELECT 
        groupFld, 
        COUNT(*) as item_count,
        AVG(hire_price) as avg_rate,
        MIN(hire_price) as min_rate,
        MAX(hire_price) as max_rate
    FROM tblinvmas
    WHERE product_type_v41 = 1 AND groupFld IS NOT NULL
    GROUP BY groupFld
    ORDER BY COUNT(*) DESC
''')
labor_groups = cursor.fetchall()
print(tabulate(labor_groups, headers='keys', tablefmt='grid'))

# 8. Check for packages (product_type_v41 = 2)
print('\n\n📦 PACKAGES - Type 2:')
print('-' * 80)
cursor.execute('''
    SELECT COUNT(*) as package_count
    FROM tblinvmas
    WHERE product_type_v41 = 2
''')
package_count = cursor.fetchone()['package_count']
print(f'Total packages: {package_count}')

if package_count > 0:
    cursor.execute('''
        SELECT TOP 10
            product_code, descriptionV6, groupFld, hire_price
        FROM tblinvmas
        WHERE product_type_v41 = 2
        ORDER BY product_code
    ''')
    packages = cursor.fetchall()
    print(tabulate(packages, headers='keys', tablefmt='grid'))
else:
    print('No packages found with product_type_v41 = 2')
    print('\nChecking for packages in vwProdsComponents view...')

# 9. Check vwProdsComponents for package structure
print('\n\n🔗 PACKAGE COMPONENTS (vwProdsComponents):')
print('-' * 80)
cursor.execute('''
    SELECT TOP 20
        parent_code, product_code, qty, price
    FROM vwProdsComponents
    ORDER BY parent_code, product_code
''')
components = cursor.fetchall()
if components:
    print('Sample package components:')
    print(tabulate(components, headers='keys', tablefmt='grid'))
    
    # Count unique packages
    cursor.execute('SELECT COUNT(DISTINCT parent_code) as package_count FROM vwProdsComponents')
    pkg_count = cursor.fetchone()['package_count']
    print(f'\nTotal packages with components: {pkg_count}')
else:
    print('No package components found')

# 10. Equipment with components
print('\n\n🔧 ITEMS WITH COMPONENTS:')
print('-' * 80)
cursor.execute('''
    SELECT 
        components_del, components_inv, components_quote,
        COUNT(*) as count
    FROM tblinvmas
    WHERE product_type_v41 = 0
    GROUP BY components_del, components_inv, components_quote
    ORDER BY COUNT(*) DESC
''')
components_flags = cursor.fetchall()
print(tabulate(components_flags, headers='keys', tablefmt='grid'))

# 11. Sample items with components
cursor.execute('''
    SELECT TOP 10
        product_code, descriptionV6, groupFld, hire_price,
        components_del, components_inv, components_quote
    FROM tblinvmas
    WHERE product_type_v41 = 0 
        AND (components_del = 'Y' OR components_inv = 'Y' OR components_quote = 'Y')
    ORDER BY product_code
''')
items_with_components = cursor.fetchall()
if items_with_components:
    print('\nSample items with components:')
    print(tabulate(items_with_components, headers='keys', tablefmt='grid'))

# 12. Price ranges for equipment
print('\n\n💰 EQUIPMENT PRICE RANGES:')
print('-' * 80)
cursor.execute('''
    SELECT 
        CASE 
            WHEN hire_price = 0 THEN '$0 (Free/No charge)'
            WHEN hire_price < 50 THEN '$1-49'
            WHEN hire_price < 100 THEN '$50-99'
            WHEN hire_price < 200 THEN '$100-199'
            WHEN hire_price < 500 THEN '$200-499'
            WHEN hire_price < 1000 THEN '$500-999'
            ELSE '$1000+'
        END as price_range,
        COUNT(*) as item_count
    FROM tblinvmas
    WHERE product_type_v41 = 0
    GROUP BY 
        CASE 
            WHEN hire_price = 0 THEN '$0 (Free/No charge)'
            WHEN hire_price < 50 THEN '$1-49'
            WHEN hire_price < 100 THEN '$50-99'
            WHEN hire_price < 200 THEN '$100-199'
            WHEN hire_price < 500 THEN '$200-499'
            WHEN hire_price < 1000 THEN '$500-999'
            ELSE '$1000+'
        END
    ORDER BY 
        CASE 
            WHEN hire_price = 0 THEN 0
            WHEN hire_price < 50 THEN 1
            WHEN hire_price < 100 THEN 2
            WHEN hire_price < 200 THEN 3
            WHEN hire_price < 500 THEN 4
            WHEN hire_price < 1000 THEN 5
            ELSE 6
        END
''')
price_ranges = cursor.fetchall()
print(tabulate(price_ranges, headers='keys', tablefmt='grid'))

# 13. Labor rate ranges
print('\n\n💵 LABOR RATE RANGES:')
print('-' * 80)
cursor.execute('''
    SELECT 
        CASE 
            WHEN hire_price = 0 THEN '$0 (Free/No charge)'
            WHEN hire_price < 50 THEN '$1-49'
            WHEN hire_price < 100 THEN '$50-99'
            WHEN hire_price < 200 THEN '$100-199'
            WHEN hire_price < 500 THEN '$200-499'
            ELSE '$500+'
        END as rate_range,
        COUNT(*) as item_count
    FROM tblinvmas
    WHERE product_type_v41 = 1
    GROUP BY 
        CASE 
            WHEN hire_price = 0 THEN '$0 (Free/No charge)'
            WHEN hire_price < 50 THEN '$1-49'
            WHEN hire_price < 100 THEN '$50-99'
            WHEN hire_price < 200 THEN '$100-199'
            WHEN hire_price < 500 THEN '$200-499'
            ELSE '$500+'
        END
    ORDER BY 
        CASE 
            WHEN hire_price = 0 THEN 0
            WHEN hire_price < 50 THEN 1
            WHEN hire_price < 100 THEN 2
            WHEN hire_price < 200 THEN 3
            WHEN hire_price < 500 THEN 4
            ELSE 5
        END
''')
labor_rates = cursor.fetchall()
print(tabulate(labor_rates, headers='keys', tablefmt='grid'))

# 14. Check how equipment is added to bookings
print('\n\n📋 HOW EQUIPMENT IS ADDED TO BOOKINGS (tblitemtran):')
print('-' * 80)
cursor.execute('''
    SELECT TOP 10
        booking_no_v32, product_code_v42, Comment_desc_v42, 
        trans_qty, price, trans_type_v41, item_type
    FROM tblitemtran
    WHERE booking_no_v32 = 'C1244000009'
    ORDER BY seq_no, sub_seq_no
''')
booking_items = cursor.fetchall()
print('Sample equipment items from booking C1244000009:')
print(tabulate(booking_items, headers='keys', tablefmt='grid'))

# 15. Check how labor is added to bookings
print('\n\n👷 HOW LABOR IS ADDED TO BOOKINGS (tblcrew):')
print('-' * 80)
cursor.execute('''
    SELECT TOP 10
        booking_no_v32, product_code_v42, hours, price, trans_qty
    FROM tblcrew
    WHERE booking_no_v32 = 'C1244000009'
    ORDER BY seq_no
''')
booking_crew = cursor.fetchall()
print('Sample crew items from booking C1244000009:')
print(tabulate(booking_crew, headers='keys', tablefmt='grid'))

conn.close()

print('\n' + '=' * 80)
print('ANALYSIS COMPLETE')
print('=' * 80)

