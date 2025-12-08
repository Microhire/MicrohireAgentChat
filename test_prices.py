#!/usr/bin/env python3
"""
Test that prices can be looked up correctly with trimming.
"""

import pymssql
from tabulate import tabulate

SERVER = "116.90.5.144"
PORT = 41383
DATABASE = "AITESTDB"
USER = "PowerBI-Consult"
PASSWORD = "2tW@ostq3a3_9oV3m-TBQu3w"


def main():
    conn = pymssql.connect(server=SERVER, port=PORT, database=DATABASE, user=USER, password=PASSWORD)
    cursor = conn.cursor()
    
    # Test with specific product codes that should have prices
    test_codes = ['2BAYCHAR', '20KDBKT', 'PT-DZ21K', 'QLXD2SK', '10X3FPSC']
    
    print("Testing price lookup for product codes:")
    print("="*80)
    
    for code in test_codes:
        print(f"\nProduct: {code}")
        
        # Check exact match
        cursor.execute("""
            SELECT ProductCode, rate_1st_day, hourly_rate
            FROM tblRatetbl
            WHERE ProductCode = %s AND tableNo = 0
        """, (code,))
        
        exact = cursor.fetchone()
        if exact:
            print(f"  Exact match: {exact[0]!r} -> rate_1st_day=${exact[1]}")
        else:
            # Try with RTRIM
            cursor.execute("""
                SELECT ProductCode, rate_1st_day, hourly_rate
                FROM tblRatetbl
                WHERE RTRIM(ProductCode) = %s AND tableNo = 0
            """, (code,))
            
            trimmed = cursor.fetchone()
            if trimmed:
                print(f"  Trimmed match: {trimmed[0]!r} -> rate_1st_day=${trimmed[1]}")
            else:
                print(f"  NOT FOUND in rate table")
        
        # Check inventory
        cursor.execute("""
            SELECT product_code, retail_price, RTRIM(descriptionV6) as description
            FROM tblinvmas
            WHERE RTRIM(product_code) = %s
        """, (code,))
        
        inv = cursor.fetchone()
        if inv:
            print(f"  Inventory: {inv[0]!r} -> retail=${inv[1]}, desc={inv[2]}")
        else:
            print(f"  NOT FOUND in inventory")
    
    # Show that the char padding is the issue
    print("\n\nDemonstrating char(30) padding issue:")
    print("="*80)
    cursor.execute("""
        SELECT TOP 5 ProductCode, LEN(ProductCode) as len, rate_1st_day
        FROM tblRatetbl
        WHERE tableNo = 0 AND rate_1st_day > 0
    """)
    
    rows = cursor.fetchall()
    headers = ["ProductCode (raw)", "Length", "Rate1stDay"]
    print(tabulate([(repr(r[0]), r[1], r[2]) for r in rows], headers=headers, tablefmt="grid"))
    
    conn.close()


if __name__ == "__main__":
    main()

