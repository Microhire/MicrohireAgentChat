#!/usr/bin/env python3
"""
List all tables in PostgreSQL database and show structure of booking-related tables.
"""

import psycopg2
from psycopg2.extras import RealDictCursor

def get_connection():
    """Connect to PostgreSQL database"""
    try:
        conn = psycopg2.connect(
            host="localhost",
            port="5432",
            dbname="postgres",
            user="postgres",
            password="postgres"
        )
        return conn
    except Exception as e:
        print(f"❌ Connection failed: {e}")
        return None

def list_all_tables():
    """List all tables in the database"""
    conn = get_connection()
    if not conn:
        return

    try:
        cursor = conn.cursor(cursor_factory=RealDictCursor)

        # Get all tables in public schema
        cursor.execute("""
            SELECT
                schemaname,
                tablename,
                tableowner,
                tablespace,
                hasindexes,
                hasrules,
                hastriggers,
                rowsecurity
            FROM pg_tables
            WHERE schemaname = 'public'
            ORDER BY tablename;
        """)

        tables = cursor.fetchall()

        print("=== ALL TABLES IN DATABASE ===")
        print(f"Total tables: {len(tables)}")
        print()

        if len(tables) == 0:
            print("📋 No tables found in the public schema.")
            print("This means the booking system tables haven't been created yet.")
        else:
            print("<15")
            print("-" * 80)
            for table in tables:
                indexes = "✓" if table['hasindexes'] else "✗"
                triggers = "✓" if table['hastriggers'] else "✗"
                rules = "✓" if table['hasrules'] else "✗"

                print("<15")

        cursor.close()
        conn.close()

        return [table['tablename'] for table in tables]

    except Exception as e:
        print(f"❌ Error listing tables: {e}")
        return []

def show_table_structure(table_name):
    """Show detailed structure of a specific table"""
    conn = get_connection()
    if not conn:
        return

    try:
        cursor = conn.cursor(cursor_factory=RealDictCursor)

        print(f"\n=== TABLE: {table_name} ===")

        # Get column information
        cursor.execute("""
            SELECT
                column_name,
                data_type,
                is_nullable,
                column_default,
                character_maximum_length,
                numeric_precision,
                numeric_scale
            FROM information_schema.columns
            WHERE table_name = %s AND table_schema = 'public'
            ORDER BY ordinal_position;
        """, (table_name,))

        columns = cursor.fetchall()

        if len(columns) == 0:
            print(f"❌ Table '{table_name}' does not exist or has no columns.")
            return

        print(f"Columns: {len(columns)}")
        print("<25")
        print("-" * 120)

        for col in columns:
            nullable = "NULL" if col['is_nullable'] == 'YES' else "NOT NULL"
            default = str(col['column_default']) if col['column_default'] else ""
            max_len = str(col['character_maximum_length']) if col['character_maximum_length'] else ""
            precision = f"({col['numeric_precision']},{col['numeric_scale']})" if col['numeric_precision'] else ""

            data_type = col['data_type']
            if max_len:
                data_type += f"({max_len})"
            elif precision:
                data_type += precision

            print("<25")

        # Get indexes
        cursor.execute("""
            SELECT
                indexname,
                indexdef
            FROM pg_indexes
            WHERE tablename = %s AND schemaname = 'public';
        """, (table_name,))

        indexes = cursor.fetchall()

        if indexes:
            print(f"\nIndexes: {len(indexes)}")
            for idx in indexes:
                print(f"  • {idx['indexname']}: {idx['indexdef']}")

        # Get foreign keys
        cursor.execute("""
            SELECT
                tc.constraint_name,
                kcu.column_name,
                ccu.table_name AS foreign_table_name,
                ccu.column_name AS foreign_column_name
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
              ON tc.constraint_name = kcu.constraint_name
              AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
              ON ccu.constraint_name = tc.constraint_name
              AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_name = %s
              AND tc.table_schema = 'public';
        """, (table_name,))

        fks = cursor.fetchall()

        if fks:
            print(f"\nForeign Keys: {len(fks)}")
            for fk in fks:
                print(f"  • {fk['constraint_name']}: {fk['column_name']} → {fk['foreign_table_name']}.{fk['foreign_column_name']}")

        cursor.close()
        conn.close()

    except Exception as e:
        print(f"❌ Error getting table structure: {e}")

def show_booking_tables_structure():
    """Show structure of all booking-related tables"""
    booking_tables = [
        'tblbookings',
        'tblcontact',
        'tblcust',
        'tblitemtran',
        'tblcrew',
        'tblbooknote',
        'tbllinkcustcontact'
    ]

    print("\n" + "="*80)
    print("BOOKING SYSTEM TABLES STRUCTURE")
    print("="*80)

    all_tables = list_all_tables()

    print(f"\n📋 Database has {len(all_tables)} total tables")

    found_tables = []
    missing_tables = []

    for table in booking_tables:
        if table in all_tables:
            found_tables.append(table)
            show_table_structure(table)
        else:
            missing_tables.append(table)

    if missing_tables:
        print(f"\n⚠️  MISSING TABLES: {len(missing_tables)}")
        for table in missing_tables:
            print(f"   • {table} - Does not exist in database")

    print(f"\n📊 SUMMARY:")
    print(f"   • Found: {len(found_tables)} booking tables")
    print(f"   • Missing: {len(missing_tables)} booking tables")
    print(f"   • Total database tables: {len(all_tables)}")

def main():
    """Main function"""
    print("=== PostgreSQL Database Analysis ===")

    # First list all tables
    all_tables = list_all_tables()

    # Then show booking table structures
    show_booking_tables_structure()

    print("\n" + "="*80)
    print("CONCLUSION")
    print("="*80)

    booking_tables = ['tblbookings', 'tblcontact', 'tblcust', 'tblitemtran', 'tblcrew', 'tblbooknote', 'tbllinkcustcontact']
    existing_booking_tables = [t for t in booking_tables if t in all_tables]

    if len(existing_booking_tables) == len(booking_tables):
        print("✅ All booking system tables exist and are properly structured!")
        print("The database is ready for booking operations.")
    elif len(existing_booking_tables) > 0:
        print(f"⚠️  Partial booking system: {len(existing_booking_tables)}/{len(booking_tables)} tables exist")
        print("Some tables are missing and need to be created.")
    else:
        print("❌ No booking system tables found in the database.")
        print("The booking system tables need to be created before use.")

    print(f"\nTotal tables in database: {len(all_tables)}")
    print(f"Booking tables found: {len(existing_booking_tables)}")
    print(f"Booking tables missing: {len(booking_tables) - len(existing_booking_tables)}")

if __name__ == "__main__":
    main()
