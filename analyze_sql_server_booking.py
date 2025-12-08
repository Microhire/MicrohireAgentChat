#!/usr/bin/env python3
"""
Script to analyze SQL Server booking C1374900080 structure.
This script should be run against your SQL Server database where the booking exists.
"""

import pyodbc
import json
from tabulate import tabulate

def get_connection_string():
    """
    Get SQL Server connection string.
    Replace these values with your actual SQL Server connection details.
    """
    server = 'YOUR_SQL_SERVER'  # e.g., 'localhost\\SQLEXPRESS'
    database = 'YOUR_DATABASE'  # e.g., 'Microhire'
    username = 'YOUR_USERNAME'  # e.g., 'sa'
    password = 'YOUR_PASSWORD'  # e.g., 'password'
    
    return f'DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server};DATABASE={database};UID={username};PWD={password}'

def analyze_booking():
    """Analyze booking C1374900080 in SQL Server"""
    try:
        conn_str = get_connection_string()
        conn = pyodbc.connect(conn_str)
        cursor = conn.cursor()
        
        print("=== ANALYZING SQL SERVER BOOKING C1374900080 ===\n")
        
        # 1. Get booking details
        print("1. Getting booking details...")
        cursor.execute("""
            SELECT 
                booking_no, order_no, CustCode, contact_nameV6, OrganizationV6,
                VenueID, VenueRoom, status, booking_type_v32, BookingProgressStatus,
                price_quoted, hire_price, labour, insurance_v5, sundry_total,
                SDate, ShowSDate, ShowEdate, SetDate, RehDate,
                showStartTime, ShowEndTime, setupTimeV61, StrikeTime,
                expAttendees, showName, order_date, ID
            FROM tblbookings
            WHERE booking_no = 'C1374900080' OR CustCode = 'C1374900080'
        """)
        
        booking = cursor.fetchone()
        if booking:
            columns = [column[0] for column in cursor.description]
            booking_dict = dict(zip(columns, booking))
            print("Booking found:")
            print(json.dumps(booking_dict, indent=2, default=str))
        else:
            print("Booking C1374900080 not found.")
            return
        
        booking_no = booking_dict['booking_no']
        booking_id = booking_dict['ID']
        
        # 2. Get equipment items
        print("\n2. Getting equipment items...")
        cursor.execute("""
            SELECT 
                ID, booking_no_v32, heading_no, seq_no, sub_seq_no,
                trans_type_v41, product_code_v42, Comment_desc_v42,
                trans_qty, price, unitRate, item_type, days_using,
                PackageLevel, ParentCode, GroupSeqNo,
                booking_id, AvailRecFlag, AssignType, QtyShort,
                View_Logi, View_client, FirstDate, RetnDate
            FROM tblitemtran
            WHERE booking_no_v32 = ?
            ORDER BY heading_no, seq_no, sub_seq_no
        """, booking_no)
        
        items = cursor.fetchall()
        if items:
            columns = [column[0] for column in cursor.description]
            items_list = [dict(zip(columns, item)) for item in items]
            print(f"Found {len(items)} equipment items:")
            print("Key fields for equipment items:")
            
            # Display key fields for each item
            for i, item in enumerate(items_list):
                print(f"\nItem {i+1}:")
                key_fields = {
                    'product_code_v42': item.get('product_code_v42'),
                    'Comment_desc_v42': item.get('Comment_desc_v42'),
                    'trans_qty': item.get('trans_qty'),
                    'price': item.get('price'),
                    'heading_no': item.get('heading_no'),
                    'seq_no': item.get('seq_no'),
                    'sub_seq_no': item.get('sub_seq_no'),
                    'trans_type_v41': item.get('trans_type_v41'),
                    'item_type': item.get('item_type'),
                    'booking_id': item.get('booking_id'),
                    'AvailRecFlag': item.get('AvailRecFlag'),
                    'AssignType': item.get('AssignType'),
                    'QtyShort': item.get('QtyShort'),
                    'PackageLevel': item.get('PackageLevel'),
                    'ParentCode': item.get('ParentCode')
                }
                print(json.dumps(key_fields, indent=2, default=str))
        else:
            print("No equipment items found for this booking.")
        
        # 3. Check for packages
        print("\n3. Checking for package structure...")
        cursor.execute("""
            SELECT 
                i.product_code_v42, i.Comment_desc_v42, i.PackageLevel, i.ParentCode,
                i.trans_qty, i.price, i.seq_no, i.sub_seq_no
            FROM tblitemtran i
            WHERE i.booking_no_v32 = ?
            ORDER BY i.PackageLevel, i.seq_no, i.sub_seq_no
        """, booking_no)
        
        packages = cursor.fetchall()
        if packages:
            columns = [column[0] for column in cursor.description]
            packages_list = [dict(zip(columns, package)) for package in packages]
            
            # Group by PackageLevel
            package_levels = {}
            for package in packages_list:
                level = package.get('PackageLevel')
                if level not in package_levels:
                    package_levels[level] = []
                package_levels[level].append(package)
            
            # Display packages by level
            for level, items in package_levels.items():
                print(f"\nPackage Level: {level if level is not None else 'NULL'}")
                print(tabulate(items, headers="keys"))
        
        # 4. Get crew items
        print("\n4. Getting crew items...")
        cursor.execute("""
            SELECT 
                ID, booking_no, crew_desc, hours, rate, line_total,
                createdate
            FROM tblcrew
            WHERE booking_no = ?
            ORDER BY ID
        """, booking_no)
        
        crews = cursor.fetchall()
        if crews:
            columns = [column[0] for column in cursor.description]
            crews_list = [dict(zip(columns, crew)) for crew in crews]
            print(f"Found {len(crews)} crew items:")
            print(tabulate(crews_list, headers="keys"))
        else:
            print("No crew items found for this booking.")
        
        cursor.close()
        conn.close()
        
        print("\n=== ANALYSIS COMPLETE ===")
        print("Use this information to update your insert_sample_booking_postgres.py script")
        print("with the correct fields and structure to match SQL Server.")
        
    except Exception as e:
        print(f"Error: {e}")
        print("\nTo run this script, you need to:")
        print("1. Install pyodbc: pip install pyodbc")
        print("2. Install SQL Server ODBC drivers")
        print("3. Update the connection string with your SQL Server details")

if __name__ == "__main__":
    analyze_booking()

