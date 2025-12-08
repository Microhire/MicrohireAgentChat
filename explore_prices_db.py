#!/usr/bin/env python3
"""
Explore the Microhire database to understand pricing structure.
Connects to SQL Server and queries for:
- Equipment items (tblitemtran)
- Product pricing (tblinvmas, tblRatetbl)
- Recent bookings with items
"""

import pymssql
from decimal import Decimal
from tabulate import tabulate

# Connection details from appsettings.json
SERVER = "116.90.5.144"
PORT = 41383
DATABASE = "AITESTDB"
USER = "PowerBI-Consult"
PASSWORD = "2tW@ostq3a3_9oV3m-TBQu3w"


def get_connection():
    """Establish database connection."""
    try:
        conn = pymssql.connect(
            server=SERVER,
            port=PORT,
            database=DATABASE,
            user=USER,
            password=PASSWORD
        )
        print("✓ Connected to database")
        return conn
    except Exception as e:
        print(f"✗ Connection failed: {e}")
        return None


def explore_rate_table(conn):
    """Explore the tblRatetbl rate table."""
    print("\n" + "="*80)
    print("RATE TABLE (tblRatetbl) - Pricing Structure")
    print("="*80)
    
    cursor = conn.cursor()
    
    # Get sample rates with prices
    cursor.execute("""
        SELECT TOP 20 
            ProductCode,
            tableNo,
            hourly_rate,
            half_day,
            rate_1st_day,
            rate_extra_days,
            rate_week
        FROM tblRatetbl 
        WHERE rate_1st_day IS NOT NULL AND rate_1st_day > 0
        ORDER BY rate_1st_day DESC
    """)
    
    rows = cursor.fetchall()
    if rows:
        headers = ["ProductCode", "TableNo", "Hourly", "HalfDay", "1stDay", "ExtraDays", "Week"]
        print(tabulate(rows, headers=headers, tablefmt="grid"))
    else:
        print("No rate data found with prices > 0")
    
    # Check table numbers
    cursor.execute("""
        SELECT tableNo, COUNT(*) as cnt, 
               AVG(rate_1st_day) as avg_1st_day,
               MIN(rate_1st_day) as min_1st_day,
               MAX(rate_1st_day) as max_1st_day
        FROM tblRatetbl 
        WHERE rate_1st_day > 0
        GROUP BY tableNo
        ORDER BY tableNo
    """)
    
    rows = cursor.fetchall()
    if rows:
        print("\n\nRate Table Number Distribution:")
        headers = ["TableNo", "Count", "Avg 1st Day", "Min", "Max"]
        print(tabulate(rows, headers=headers, tablefmt="grid"))


def explore_inventory_prices(conn):
    """Explore tblinvmas pricing."""
    print("\n" + "="*80)
    print("INVENTORY PRICES (tblinvmas)")
    print("="*80)
    
    cursor = conn.cursor()
    
    # Get products with retail prices
    cursor.execute("""
        SELECT TOP 20 
            product_code,
            RTRIM(descriptionV6) as description,
            groupFld,
            retail_price,
            cost_price,
            trade_price
        FROM tblinvmas 
        WHERE retail_price IS NOT NULL AND retail_price > 0
        ORDER BY retail_price DESC
    """)
    
    rows = cursor.fetchall()
    if rows:
        headers = ["ProductCode", "Description", "Group", "Retail", "Cost", "Trade"]
        print(tabulate(rows, headers=headers, tablefmt="grid"))
    
    # Stats
    cursor.execute("""
        SELECT 
            COUNT(*) as total_products,
            SUM(CASE WHEN retail_price > 0 THEN 1 ELSE 0 END) as with_retail_price,
            SUM(CASE WHEN cost_price > 0 THEN 1 ELSE 0 END) as with_cost_price,
            AVG(retail_price) as avg_retail
        FROM tblinvmas
    """)
    
    row = cursor.fetchone()
    print(f"\n\nInventory Statistics:")
    print(f"  Total products: {row[0]}")
    print(f"  With retail price > 0: {row[1]}")
    print(f"  With cost price > 0: {row[2]}")
    print(f"  Average retail price: ${row[3]:.2f}" if row[3] else "  Average retail: N/A")


def explore_item_transactions(conn, booking_no=None):
    """Explore tblitemtran to see item prices."""
    print("\n" + "="*80)
    print("ITEM TRANSACTIONS (tblitemtran) - Prices in bookings")
    print("="*80)
    
    cursor = conn.cursor()
    
    # Find a recent booking with items that have prices
    if not booking_no:
        cursor.execute("""
            SELECT TOP 1 booking_no_v32
            FROM tblitemtran 
            WHERE price IS NOT NULL AND price > 0
            ORDER BY ID DESC
        """)
        row = cursor.fetchone()
        if row:
            booking_no = row[0]
    
    if booking_no:
        print(f"\nLooking at booking: {booking_no}")
        
        cursor.execute("""
            SELECT 
                RTRIM(product_code_v42) as product_code,
                RTRIM(Comment_desc_v42) as description,
                trans_qty as qty,
                price,
                unitRate,
                days_using,
                item_type
            FROM tblitemtran 
            WHERE booking_no_v32 = %s
            AND product_code_v42 IS NOT NULL
            ORDER BY seq_no, sub_seq_no
        """, (booking_no,))
        
        rows = cursor.fetchall()
        if rows:
            headers = ["ProductCode", "Description", "Qty", "Price", "UnitRate", "Days", "ItemType"]
            print(tabulate(rows, headers=headers, tablefmt="grid"))
        else:
            print("No items found")
    
    # Check overall price distribution
    cursor.execute("""
        SELECT 
            COUNT(*) as total_items,
            SUM(CASE WHEN price IS NOT NULL AND price > 0 THEN 1 ELSE 0 END) as with_price,
            SUM(CASE WHEN unitRate IS NOT NULL AND unitRate > 0 THEN 1 ELSE 0 END) as with_unit_rate,
            AVG(CASE WHEN price > 0 THEN price END) as avg_price,
            AVG(CASE WHEN unitRate > 0 THEN unitRate END) as avg_unit_rate
        FROM tblitemtran
        WHERE product_code_v42 IS NOT NULL
    """)
    
    row = cursor.fetchone()
    print(f"\n\nItem Transaction Statistics:")
    print(f"  Total items with product codes: {row[0]}")
    print(f"  With price > 0: {row[1]}")
    print(f"  With unitRate > 0: {row[2]}")
    if row[3]:
        print(f"  Average price (when set): ${row[3]:.2f}")
    if row[4]:
        print(f"  Average unit rate (when set): ${row[4]:.2f}")


def explore_combined_pricing(conn):
    """See how prices are connected between tables."""
    print("\n" + "="*80)
    print("COMBINED PRICING LOOKUP (itemtran + invmas + ratetbl)")
    print("="*80)
    
    cursor = conn.cursor()
    
    # Find items with all pricing info
    cursor.execute("""
        SELECT TOP 30
            RTRIM(it.product_code_v42) as product_code,
            RTRIM(COALESCE(im.PrintedDesc, im.descriptionV6, it.Comment_desc_v42)) as description,
            im.groupFld,
            it.trans_qty,
            it.price as item_price,
            it.unitRate as item_unit_rate,
            im.retail_price as inv_retail,
            rt.rate_1st_day as rate_1st_day,
            rt.hourly_rate as rate_hourly,
            COALESCE(it.price, it.unitRate, rt.rate_1st_day, im.retail_price, 0) as effective_price
        FROM tblitemtran it
        LEFT JOIN tblinvmas im ON RTRIM(it.product_code_v42) = RTRIM(im.product_code)
        LEFT JOIN tblRatetbl rt ON RTRIM(it.product_code_v42) = RTRIM(rt.ProductCode) AND rt.tableNo = 0
        WHERE it.product_code_v42 IS NOT NULL
        AND (it.price > 0 OR it.unitRate > 0 OR rt.rate_1st_day > 0 OR im.retail_price > 0)
        ORDER BY it.ID DESC
    """)
    
    rows = cursor.fetchall()
    if rows:
        headers = ["Code", "Description", "Group", "Qty", "ItemPrice", "UnitRate", "InvRetail", "Rate1stDay", "RateHourly", "Effective"]
        print(tabulate(rows, headers=headers, tablefmt="grid"))


def explore_recent_bookings(conn):
    """Find recent bookings with equipment."""
    print("\n" + "="*80)
    print("RECENT BOOKINGS WITH EQUIPMENT")
    print("="*80)
    
    cursor = conn.cursor()
    
    cursor.execute("""
        SELECT TOP 10
            b.booking_no,
            RTRIM(b.showName) as event_name,
            b.dDate as event_date,
            b.price_quoted,
            (SELECT COUNT(*) FROM tblitemtran WHERE booking_no_v32 = b.booking_no AND product_code_v42 IS NOT NULL) as item_count,
            (SELECT SUM(COALESCE(price, 0)) FROM tblitemtran WHERE booking_no_v32 = b.booking_no) as total_item_price
        FROM tblbookings b
        WHERE b.booking_no LIKE 'AI%' OR b.booking_no LIKE 'C1%'
        ORDER BY b.ID DESC
    """)
    
    rows = cursor.fetchall()
    if rows:
        headers = ["BookingNo", "EventName", "Date", "QuotedPrice", "ItemCount", "TotalItemPrice"]
        print(tabulate(rows, headers=headers, tablefmt="grid"))


def get_product_price(conn, product_code):
    """Get the effective price for a product code."""
    cursor = conn.cursor()
    
    cursor.execute("""
        SELECT 
            RTRIM(im.product_code) as product_code,
            RTRIM(COALESCE(im.PrintedDesc, im.descriptionV6)) as description,
            im.groupFld,
            im.retail_price,
            rt.rate_1st_day,
            rt.hourly_rate,
            rt.half_day
        FROM tblinvmas im
        LEFT JOIN tblRatetbl rt ON RTRIM(im.product_code) = RTRIM(rt.ProductCode) AND rt.tableNo = 0
        WHERE im.product_code = %s
    """, (product_code,))
    
    row = cursor.fetchone()
    if row:
        print(f"\nProduct: {row[0]}")
        print(f"  Description: {row[1]}")
        print(f"  Group: {row[2]}")
        print(f"  Retail Price: ${row[3]:.2f}" if row[3] else "  Retail Price: N/A")
        print(f"  Rate 1st Day: ${row[4]:.2f}" if row[4] else "  Rate 1st Day: N/A")
        print(f"  Hourly Rate: ${row[5]:.2f}" if row[5] else "  Hourly Rate: N/A")
        print(f"  Half Day: ${row[6]:.2f}" if row[6] else "  Half Day: N/A")
        
        # Effective price
        effective = row[4] or row[3] or row[5] or 0
        print(f"  EFFECTIVE PRICE: ${effective:.2f}")
        return effective
    else:
        print(f"Product {product_code} not found")
        return 0


def list_products_by_group(conn, group=None):
    """List products by group with their prices."""
    cursor = conn.cursor()
    
    if group:
        cursor.execute("""
            SELECT 
                RTRIM(im.product_code) as product_code,
                RTRIM(COALESCE(im.PrintedDesc, im.descriptionV6)) as description,
                im.groupFld,
                im.retail_price,
                rt.rate_1st_day,
                COALESCE(rt.rate_1st_day, im.retail_price, 0) as effective_price
            FROM tblinvmas im
            LEFT JOIN tblRatetbl rt ON RTRIM(im.product_code) = RTRIM(rt.ProductCode) AND rt.tableNo = 0
            WHERE im.groupFld LIKE %s
            ORDER BY COALESCE(rt.rate_1st_day, im.retail_price, 0) DESC
        """, (f'%{group}%',))
        
        rows = cursor.fetchall()
        if rows:
            print(f"\n\nProducts in group '{group}':")
            headers = ["Code", "Description", "Group", "Retail", "Rate1stDay", "Effective"]
            print(tabulate(rows[:30], headers=headers, tablefmt="grid"))
            if len(rows) > 30:
                print(f"... and {len(rows) - 30} more")
    else:
        # List all groups
        cursor.execute("""
            SELECT DISTINCT groupFld, COUNT(*) as cnt
            FROM tblinvmas
            WHERE groupFld IS NOT NULL AND groupFld != ''
            GROUP BY groupFld
            ORDER BY groupFld
        """)
        
        rows = cursor.fetchall()
        print("\n\nAvailable Product Groups:")
        for row in rows:
            print(f"  {row[0]}: {row[1]} products")


def check_ai_bookings(conn):
    """Check the AI-created bookings and their items."""
    print("\n" + "="*80)
    print("AI-CREATED BOOKINGS AND ITEMS")
    print("="*80)
    
    cursor = conn.cursor()
    
    # Get AI bookings
    cursor.execute("""
        SELECT TOP 5
            b.booking_no,
            RTRIM(b.showName) as event_name,
            b.dDate,
            b.price_quoted
        FROM tblbookings b
        WHERE b.booking_no LIKE 'AI%'
        ORDER BY b.ID DESC
    """)
    
    bookings = cursor.fetchall()
    
    for booking in bookings:
        booking_no = booking[0]
        print(f"\n\n--- Booking: {booking_no} ({booking[1]}) ---")
        print(f"Date: {booking[2]}, Quoted: {booking[3]}")
        
        # Get items for this booking
        cursor.execute("""
            SELECT 
                RTRIM(it.product_code_v42) as product_code,
                RTRIM(COALESCE(im.PrintedDesc, im.descriptionV6, it.Comment_desc_v42)) as description,
                im.groupFld,
                it.trans_qty,
                it.price as item_price,
                it.unitRate as item_unit_rate,
                im.retail_price as inv_retail,
                rt.rate_1st_day,
                COALESCE(it.price, it.unitRate, rt.rate_1st_day, im.retail_price, 0) as effective_price
            FROM tblitemtran it
            LEFT JOIN tblinvmas im ON RTRIM(it.product_code_v42) = RTRIM(im.product_code)
            LEFT JOIN tblRatetbl rt ON RTRIM(it.product_code_v42) = RTRIM(rt.ProductCode) AND rt.tableNo = 0
            WHERE it.booking_no_v32 = %s
            AND it.product_code_v42 IS NOT NULL
            ORDER BY it.seq_no, it.sub_seq_no
        """, (booking_no,))
        
        items = cursor.fetchall()
        if items:
            headers = ["Code", "Description", "Group", "Qty", "ItemPrice", "UnitRate", "InvRetail", "Rate1stDay", "Effective"]
            print(tabulate(items, headers=headers, tablefmt="grid"))
            
            # Calculate total
            total = sum(row[8] * (row[3] or 1) for row in items if row[8])
            print(f"Total calculated: ${total:.2f}")
        else:
            print("No items found")


def main():
    conn = get_connection()
    if not conn:
        return
    
    try:
        # Explore all price-related tables
        explore_rate_table(conn)
        explore_inventory_prices(conn)
        explore_item_transactions(conn)
        explore_combined_pricing(conn)
        explore_recent_bookings(conn)
        
        # Check AI bookings specifically
        check_ai_bookings(conn)
        
        # List groups
        list_products_by_group(conn)
        
        # Check a few specific groups
        for group in ['VISION', 'AUDIO']:
            list_products_by_group(conn, group)
        
    finally:
        conn.close()
        print("\n✓ Connection closed")


if __name__ == "__main__":
    main()
