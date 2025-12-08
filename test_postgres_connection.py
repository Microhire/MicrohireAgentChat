#!/usr/bin/env python3
"""
Test PostgreSQL connection with different parameters to find the correct setup.
"""

import psycopg2
from psycopg2 import OperationalError

def test_connection(host, port, database, user, password):
    """Test a specific connection configuration"""
    conn_str = f"host={host} port={port} dbname={database} user={user} password={password}"
    try:
        print(f"Testing: {host}:{port}/{database} as {user}")
        conn = psycopg2.connect(conn_str, connect_timeout=5)
        cursor = conn.cursor()
        cursor.execute("SELECT version();")
        version = cursor.fetchone()[0]
        print(f"✅ SUCCESS! PostgreSQL version: {version[:50]}...")

        # Test if our tables exist
        cursor.execute("""
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_name IN ('tblbookings', 'tblcontact', 'tblcust', 'tblitemtran', 'tblcrew', 'tblbooknote')
            ORDER BY table_name;
        """)
        tables = cursor.fetchall()
        print(f"📋 Found tables: {[t[0] for t in tables]}")

        cursor.close()
        conn.close()
        return True

    except OperationalError as e:
        print(f"❌ FAILED: {str(e)}")
        return False
    except Exception as e:
        print(f"❌ ERROR: {str(e)}")
        return False

def main():
    """Test different PostgreSQL connection configurations"""

    print("=== PostgreSQL Connection Test ===")
    print("Testing different connection parameters to find the correct setup...")
    print()

    # Common PostgreSQL configurations to try
    test_configs = [
        # Standard PostgreSQL defaults
        {"host": "116.90.5.144", "port": "5432", "database": "AITESTDB", "user": "PowerBI-Consult", "password": "2tW@ostq3a3_9oV3m-TBQu3w"},
        {"host": "116.90.5.144", "port": "5432", "database": "postgres", "user": "PowerBI-Consult", "password": "2tW@ostq3a3_9oV3m-TBQu3w"},

        # Try different ports (common PostgreSQL ports)
        {"host": "116.90.5.144", "port": "41383", "database": "AITESTDB", "user": "PowerBI-Consult", "password": "2tW@ostq3a3_9oV3m-TBQu3w"},
        {"host": "116.90.5.144", "port": "41383", "database": "postgres", "user": "PowerBI-Consult", "password": "2tW@ostq3a3_9oV3m-TBQu3w"},

        # Try different users/databases
        {"host": "116.90.5.144", "port": "5432", "database": "AITESTDB", "user": "postgres", "password": "2tW@ostq3a3_9oV3m-TBQu3w"},
        {"host": "116.90.5.144", "port": "5432", "database": "postgres", "user": "postgres", "password": "2tW@ostq3a3_9oV3m-TBQu3w"},

        # Try localhost (maybe it's running locally)
        {"host": "localhost", "port": "5432", "database": "AITESTDB", "user": "PowerBI-Consult", "password": "2tW@ostq3a3_9oV3m-TBQu3w"},
        {"host": "localhost", "port": "5432", "database": "postgres", "user": "postgres", "password": "postgres"},
    ]

    print("Testing various PostgreSQL connection configurations...")
    print("=" * 60)

    successful_configs = []

    for config in test_configs:
        success = test_connection(**config)
        if success:
            successful_configs.append(config)
        print()

    print("=" * 60)
    if successful_configs:
        print(f"✅ Found {len(successful_configs)} working configuration(s):")
        for config in successful_configs:
            print(f"   {config['host']}:{config['port']}/{config['database']} as {config['user']}")
        print()
        print("🎉 Great! PostgreSQL is accessible.")
        print("You can now run the sample booking insertion script.")
        print("Update the connection details in insert_sample_booking_postgres.py")
        print("or run the SQL script directly: insert_sample_booking_postgres.sql")
    else:
        print("❌ No working PostgreSQL configurations found.")
        print()
        print("Possible issues:")
        print("- PostgreSQL is not running on the expected host/port")
        print("- Firewall blocking connections")
        print("- Different authentication method required")
        print("- Database name is different")
        print()
        print("Please check:")
        print("1. Is PostgreSQL running? (sudo systemctl status postgresql)")
        print("2. What port is it listening on? (netstat -tlnp | grep postgres)")
        print("3. What databases exist? (psql -l)")
        print("4. Can you connect locally? (psql -d your_database)")
        print()
        print("Or run the SQL script directly in your PostgreSQL client:")
        print("insert_sample_booking_postgres.sql")

if __name__ == "__main__":
    main()
