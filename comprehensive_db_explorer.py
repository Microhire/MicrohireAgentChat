#!/usr/bin/env python3
"""
Comprehensive Production Database Explorer
=========================================

This script explores the entire production database to understand:
- All tables and their structures
- Relationships between tables
- Sample data from key tables
- How to create contacts, bookings, equipment, crew assignments
- Complete booking workflow with all components

Supports both SQL Server (production) and PostgreSQL (testing/development)
"""

import pymssql
import psycopg2
import json
import os
from datetime import datetime
from tabulate import tabulate
from psycopg2.extras import RealDictCursor
import pandas as pd

class DatabaseExplorer:
    """Comprehensive database explorer for both SQL Server and PostgreSQL"""

    def __init__(self):
        # SQL Server (Production) connection details
        self.sql_server_config = {
            'server': '116.90.5.144',
            'port': 41383,
            'database': 'AITESTDB',
            'username': 'PowerBI-Consult',
            'password': '2tW@ostq3a3_9oV3m-TBQu3w'
        }

        # PostgreSQL (Development/Testing) connection details
        self.postgres_config = {
            'host': 'localhost',
            'port': 5432,
            'database': 'postgres',
            'user': 'postgres',
            'password': 'postgres'
        }

        self.db_type = None
        self.connection = None
        self.cursor = None

    def connect_sql_server(self):
        """Connect to SQL Server production database"""
        try:
            print("🔌 Connecting to SQL Server (Production)...")
            self.connection = pymssql.connect(
                server=self.sql_server_config['server'],
                port=self.sql_server_config['port'],
                user=self.sql_server_config['username'],
                password=self.sql_server_config['password'],
                database=self.sql_server_config['database'],
                as_dict=True
            )
            self.cursor = self.connection.cursor()
            self.db_type = 'sql_server'
            print("✅ Connected to SQL Server production database")
            return True
        except Exception as e:
            print(f"❌ SQL Server connection failed: {e}")
            return False

    def connect_postgres(self):
        """Connect to PostgreSQL development database"""
        try:
            print("🔌 Connecting to PostgreSQL (Development)...")
            conn_str = f"host={self.postgres_config['host']} port={self.postgres_config['port']} dbname={self.postgres_config['database']} user={self.postgres_config['user']} password={self.postgres_config['password']}"
            self.connection = psycopg2.connect(conn_str)
            self.cursor = self.connection.cursor(cursor_factory=RealDictCursor)
            self.db_type = 'postgres'
            print("✅ Connected to PostgreSQL development database")
            return True
        except Exception as e:
            print(f"❌ PostgreSQL connection failed: {e}")
            return False

    def connect_database(self):
        """Try to connect to either database"""
        if self.connect_sql_server():
            return True
        elif self.connect_postgres():
            return True
        else:
            print("❌ Could not connect to any database")
            return False

    def get_all_tables(self):
        """Get all tables in the database"""
        if self.db_type == 'sql_server':
            self.cursor.execute("""
                SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME
            """)
            return self.cursor.fetchall()
        else:  # postgres
            self.cursor.execute("""
                SELECT schemaname as table_schema, tablename as table_name, 'BASE TABLE' as table_type
                FROM pg_tables
                WHERE schemaname = 'public'
                ORDER BY tablename
            """)
            return self.cursor.fetchall()

    def get_table_columns(self, schema, table):
        """Get detailed column information for a table"""
        if self.db_type == 'sql_server':
            query = """
            SELECT
                c.COLUMN_NAME as column_name,
                c.DATA_TYPE as data_type,
                c.IS_NULLABLE as is_nullable,
                c.COLUMN_DEFAULT as column_default,
                c.CHARACTER_MAXIMUM_LENGTH as max_length,
                c.NUMERIC_PRECISION as numeric_precision,
                c.NUMERIC_SCALE as numeric_scale,
                c.ORDINAL_POSITION as ordinal_position,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END as is_primary_key,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 'YES' ELSE 'NO' END as is_foreign_key,
                fk.REFERENCED_TABLE_NAME,
                fk.REFERENCED_COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                    AND tc.TABLE_NAME = ku.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                AND c.TABLE_NAME = pk.TABLE_NAME
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            LEFT JOIN (
                SELECT
                    fk.name AS FK_NAME,
                    SCHEMA_NAME(tp.schema_id) AS TABLE_SCHEMA,
                    tp.name AS TABLE_NAME,
                    cp.name AS COLUMN_NAME,
                    tr.name AS REFERENCED_TABLE_NAME,
                    cr.name AS REFERENCED_COLUMN_NAME
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
                INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
                INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                INNER JOIN sys.columns cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
                INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id
            ) fk ON c.TABLE_SCHEMA = fk.TABLE_SCHEMA
                AND c.TABLE_NAME = fk.TABLE_NAME
                AND c.COLUMN_NAME = fk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = %s AND c.TABLE_NAME = %s
            ORDER BY c.ORDINAL_POSITION
            """
            self.cursor.execute(query, (schema, table))
            return self.cursor.fetchall()
        else:  # postgres
            self.cursor.execute("""
                SELECT
                    c.column_name,
                    c.data_type,
                    CASE WHEN c.is_nullable = 'YES' THEN 'YES' ELSE 'NO' END as is_nullable,
                    c.column_default,
                    c.character_maximum_length as max_length,
                    c.numeric_precision,
                    c.numeric_scale,
                    c.ordinal_position,
                    CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_primary_key,
                    CASE WHEN fk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_foreign_key,
                    ccu.table_name as referenced_table_name,
                    ccu.column_name as referenced_column_name
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT ku.table_name, ku.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage ku
                        ON tc.constraint_name = ku.constraint_name
                        AND tc.table_schema = ku.table_schema
                        AND tc.table_name = ku.table_name
                    WHERE tc.constraint_type = 'PRIMARY KEY' AND tc.table_schema = 'public'
                ) pk ON c.table_name = pk.table_name AND c.column_name = pk.column_name
                LEFT JOIN information_schema.key_column_usage ku
                    ON c.table_name = ku.table_name
                    AND c.column_name = ku.column_name
                    AND ku.constraint_name IN (
                        SELECT constraint_name FROM information_schema.table_constraints
                        WHERE constraint_type = 'FOREIGN KEY' AND table_schema = 'public'
                    )
                LEFT JOIN information_schema.constraint_column_usage ccu
                    ON ku.constraint_name = ccu.constraint_name
                WHERE c.table_schema = %s AND c.table_name = %s
                ORDER BY c.ordinal_position
            """, (schema, table))
            return self.cursor.fetchall()

    def get_row_count(self, schema, table):
        """Get row count for a table"""
        try:
            if self.db_type == 'sql_server':
                self.cursor.execute(f"SELECT COUNT(*) as row_count FROM [{schema}].[{table}]")
            else:
                self.cursor.execute(f"SELECT COUNT(*) as row_count FROM {schema}.{table}")
            result = self.cursor.fetchone()
            return result['row_count'] if result else 0
        except Exception as e:
            return f"Error: {str(e)}"

    def get_sample_data(self, schema, table, limit=5):
        """Get sample data from table"""
        try:
            if self.db_type == 'sql_server':
                self.cursor.execute(f"SELECT TOP {limit} * FROM [{schema}].[{table}]")
            else:
                self.cursor.execute(f"SELECT * FROM {schema}.{table} LIMIT {limit}")
            return self.cursor.fetchall()
        except Exception as e:
            return None

    def analyze_table(self, schema, table, description=""):
        """Analyze a table and return comprehensive data structure"""
        print(f"\n{'='*100}")
        print(f"TABLE: {schema}.{table}")
        if description:
            print(f"DESCRIPTION: {description}")
        print(f"{'='*100}")

        # Get columns
        columns = self.get_table_columns(schema, table)
        if columns:
            print(f"\n📋 COLUMNS ({len(columns)}):")
            column_data = []
            for col in columns:
                nullable = "NULL" if col['is_nullable'] == 'YES' else "NOT NULL"
                data_type = col['data_type']
                if col.get('max_length'):
                    data_type += f"({col['max_length']})"
                elif col.get('numeric_precision') and col.get('numeric_scale') is not None:
                    data_type += f"({col['numeric_precision']},{col['numeric_scale']})"
                elif col.get('numeric_precision'):
                    data_type += f"({col['numeric_precision']})"

                default = f" DEFAULT {col['column_default']}" if col.get('column_default') else ""
                pk = "🔑" if col['is_primary_key'] == 'YES' else ""
                fk = "🔗" if col['is_foreign_key'] == 'YES' else ""

                column_data.append([
                    f"{pk}{fk} {col['column_name']}",
                    data_type,
                    nullable + default
                ])

            print(tabulate(column_data, headers=['Column', 'Data Type', 'Constraints'], tablefmt='grid'))

        # Get primary keys
        pks = [col for col in columns if col['is_primary_key'] == 'YES']
        if pks:
            print(f"\n🔑 PRIMARY KEY: {', '.join([pk['column_name'] for pk in pks])}")

        # Get foreign keys
        fks = [col for col in columns if col['is_foreign_key'] == 'YES']
        if fks:
            print(f"\n🔗 FOREIGN KEYS:")
            for fk in fks:
                ref_table = fk.get('referenced_table_name', 'Unknown')
                ref_col = fk.get('referenced_column_name', 'Unknown')
                print(f"  - {fk['column_name']} → {ref_table}.{ref_col}")

        # Get row count
        row_count = self.get_row_count(schema, table)
        print(f"\n📊 ROW COUNT: {row_count:,}")

        # Get sample data
        samples = self.get_sample_data(schema, table, limit=3)
        if samples:
            print(f"\n📝 SAMPLE DATA (first 3 rows):")
            if samples:
                # Convert to list of dicts for tabulate
                sample_dicts = []
                for sample in samples:
                    sample_dict = dict(sample)
                    sample_dicts.append(sample_dict)

                # Show only first 10 columns to avoid wide tables
                if sample_dicts:
                    headers = list(sample_dicts[0].keys())[:10]
                    table_data = []
                    for sample in sample_dicts:
                        row = [sample.get(h, '') for h in headers]
                        table_data.append(row)

                    print(tabulate(table_data, headers=headers, tablefmt='grid'))
                    if len(sample_dicts[0]) > 10:
                        print(f"  ... and {len(sample_dicts[0]) - 10} more columns")
        else:
            print(f"\n📝 SAMPLE DATA: No sample data available")

        return {
            'name': table,
            'schema': schema,
            'description': description,
            'row_count': row_count,
            'columns': columns,
            'sample_data': samples or []
        }

    def explore_database_overview(self):
        """Get overview of entire database"""
        print("\n" + "="*100)
        print("🔍 DATABASE OVERVIEW")
        print("="*100)

        # Get all tables
        tables = self.get_all_tables()
        print(f"\n📋 TOTAL TABLES FOUND: {len(tables)}")

        # Group by schema
        schema_counts = {}
        for table in tables:
            schema = table['table_schema'] if self.db_type == 'postgres' else table['TABLE_SCHEMA']
            schema_counts[schema] = schema_counts.get(schema, 0) + 1

        print("\n📊 TABLES BY SCHEMA:")
        for schema, count in schema_counts.items():
            print(f"  - {schema}: {count} tables")

        # Show all tables
        print(f"\n📋 ALL TABLES:")
        table_list = []
        for table in tables:
            if self.db_type == 'postgres':
                schema = table['table_schema']
                name = table['table_name']
            else:
                schema = table['TABLE_SCHEMA']
                name = table['TABLE_NAME']
            table_list.append([schema, name])

        print(tabulate(table_list, headers=['Schema', 'Table'], tablefmt='grid'))

    def explore_key_tables(self):
        """Explore all key tables related to bookings, inventory, users, contacts, equipment"""
        key_tables = [
            # Bookings and core entities
            ('dbo', 'tblbookings', 'Main bookings table - stores all booking/order details including customer, venue, dates, and financial information'),
            ('dbo', 'tblContact', 'Contact persons table - stores contact information for individuals'),
            ('dbo', 'tblcust', 'Customer/organization table - stores customer companies and organizations'),
            ('dbo', 'tblLinkCustContact', 'Customer-contact link table - links contacts to customer organizations (many-to-many)'),

            # Equipment and Inventory
            ('dbo', 'tblinvmas', 'Products/inventory master table - master list of all products, packages, and equipment'),
            ('dbo', 'tblitemtran', 'Equipment transactions table - tracks all equipment items in bookings including quantities, rates, and dates'),
            ('dbo', 'vwProdsComponents', 'View showing product components - displays what items/components make up package products'),

            # Labor and Crew
            ('dbo', 'tblcrew', 'Labour/crew table - stores labour assignments for bookings with tasks, hours, and rates'),
            ('dbo', 'tbltask', 'Labour tasks table - defines labour task types (setup, pack down, operate, etc.)'),
            ('dbo', 'tblInvmas_Labour_Rates', 'Labour rates table - stores labour rates for products by location'),

            # Other related tables
            ('dbo', 'tblSalesper', 'Salespersons table - stores sales representative information'),
            ('dbo', 'tblbooknote', 'Booking notes table - stores conversation transcripts and booking notes'),
        ]

        print(f"\n🔍 ANALYZING KEY TABLES:")
        print("="*100)

        results = []
        for schema, table, description in key_tables:
            try:
                table_data = self.analyze_table(schema, table, description)
                results.append(table_data)
            except Exception as e:
                print(f"\n❌ ERROR analyzing {schema}.{table}: {e}")

        # Save comprehensive results
        output_file = f'database_comprehensive_analysis_{datetime.now().strftime("%Y%m%d_%H%M%S")}.json'
        with open(output_file, 'w') as f:
            json.dump(results, f, indent=2, default=str)
        print(f"\n💾 Detailed analysis saved to: {output_file}")

        return results

    def explore_relationships(self):
        """Explore and document relationships between tables"""
        print(f"\n🔗 DATABASE RELATIONSHIPS ANALYSIS")
        print("="*100)

        relationships = {
            "tblbookings": {
                "references": {
                    "tblContact": "contactid → ID (booking contact person)",
                    "tblcust": "custid → ID (booking customer/organization)",
                    "tblSalesper": "salesperid → ID (sales representative)"
                },
                "referenced_by": {
                    "tblitemtran": "booking_no_v32 → booking_no (equipment in booking)",
                    "tblcrew": "booking_no → booking_no (crew in booking)",
                    "tblbooknote": "bookingno → booking_no (notes/transcripts for booking)"
                }
            },
            "tblContact": {
                "references": {},
                "referenced_by": {
                    "tblbookings": "contactid → ID",
                    "tblLinkCustContact": "contactid → ID (links contacts to organizations)"
                }
            },
            "tblcust": {
                "references": {},
                "referenced_by": {
                    "tblbookings": "custid → ID",
                    "tblLinkCustContact": "customer_code → customer_code (links organizations to contacts)"
                }
            },
            "tblinvmas": {
                "references": {},
                "referenced_by": {
                    "tblitemtran": "product_code_v42 → product_code",
                    "vwProdsComponents": "product_code → product_code (product components)"
                }
            },
            "tblitemtran": {
                "references": {
                    "tblbookings": "booking_no_v32 → booking_no",
                    "tblinvmas": "product_code_v42 → product_code"
                },
                "referenced_by": {}
            },
            "tblcrew": {
                "references": {
                    "tblbookings": "booking_no → booking_no",
                    "tbltask": "taskid → ID (labour task type)"
                },
                "referenced_by": {}
            },
            "tblLinkCustContact": {
                "references": {
                    "tblcust": "customer_code → customer_code",
                    "tblContact": "contactid → ID"
                },
                "referenced_by": {}
            }
        }

        for table, rels in relationships.items():
            print(f"\n📋 {table.upper()}")
            print("-" * 50)

            if rels['references']:
                print("🔗 REFERENCES (outgoing relationships):")
                for ref_table, description in rels['references'].items():
                    print(f"  → {ref_table}: {description}")

            if rels['referenced_by']:
                print("🔙 REFERENCED BY (incoming relationships):")
                for ref_table, description in rels['referenced_by'].items():
                    print(f"  ← {ref_table}: {description}")

    def show_creation_workflow(self):
        """Show step-by-step guide for creating bookings with all components"""
        print(f"\n🚀 BOOKING CREATION WORKFLOW")
        print("="*100)

        workflow = [
            {
                "step": 1,
                "title": "Create or Find Contact Person",
                "table": "tblContact",
                "description": "Create contact record for the person making the booking",
                "required_fields": [
                    "contactname (full name)",
                    "firstname, surname",
                    "email, cell (phone)",
                    "position (job title)"
                ],
                "optional_fields": ["address details, notes"],
                "example": {
                    "contactname": "Michael Knight",
                    "firstname": "Michael",
                    "surname": "Knight",
                    "email": "michael@yes100attendees.com",
                    "cell": "07111111111",
                    "position": "Events Coordinator"
                }
            },
            {
                "step": 2,
                "title": "Create or Find Customer Organization",
                "table": "tblcust",
                "description": "Create customer/organization record if new company",
                "required_fields": [
                    "organisationv6 (company name)",
                    "customer_code (unique code like 'C14503')"
                ],
                "optional_fields": ["address, phone, contact details"],
                "note": "Most organizations already exist - search first"
            },
            {
                "step": 3,
                "title": "Link Contact to Organization",
                "table": "tblLinkCustContact",
                "description": "Create relationship between contact person and their organization",
                "required_fields": [
                    "customer_code (from tblcust)",
                    "contactid (from tblContact)"
                ]
            },
            {
                "step": 4,
                "title": "Generate Booking Number",
                "table": "tblbookings",
                "description": "Create unique booking number following fiscal year pattern",
                "pattern": "YYNNNN (e.g., 250001 for fiscal year 2025)",
                "logic": "Find highest existing number for current fiscal year and increment"
            },
            {
                "step": 5,
                "title": "Create Main Booking Record",
                "table": "tblbookings",
                "description": "Create the main booking with all event details",
                "key_fields": [
                    "booking_no (generated)",
                    "contact_namev6, organizationv6",
                    "custid, contactid",
                    "sdate (event date)",
                    "venueid, venueroom",
                    "showstarttime, showendtime",
                    "setuptimev61, striketime",
                    "price_quoted, hire_price, labour, insurance_v5, sundry_total",
                    "days_using, expattendees, showname"
                ]
            },
            {
                "step": 6,
                "title": "Add Equipment Items",
                "table": "tblitemtran",
                "description": "Add all equipment/products needed for the event",
                "key_fields": [
                    "booking_no_v32 (links to booking)",
                    "product_code_v42 (from tblinvmas)",
                    "item_desc (description)",
                    "hire_rate (daily rate)",
                    "qty (quantity)",
                    "line_total (qty × rate)"
                ],
                "note": "Each equipment item gets its own row"
            },
            {
                "step": 7,
                "title": "Add Labour/Crew Assignments",
                "table": "tblcrew",
                "description": "Add labour requirements (setup crew, operators, strike crew)",
                "key_fields": [
                    "booking_no (links to booking)",
                    "crew_desc (description of work)",
                    "taskid (from tbltask - setup, operate, strike)",
                    "hours (total labour hours)",
                    "rate (hourly rate)",
                    "line_total (hours × rate)"
                ]
            },
            {
                "step": 8,
                "title": "Add Conversation Transcript",
                "table": "tblbooknote",
                "description": "Store the complete chat conversation that led to booking",
                "key_fields": [
                    "bookingno (booking number)",
                    "textline (full transcript)",
                    "notetype (1 for transcripts)",
                    "createdate"
                ]
            }
        ]

        for step_info in workflow:
            print(f"\n{step_info['step']}. {step_info['title']}")
            print("-" * 60)
            print(f"📋 Table: {step_info['table']}")
            print(f"📝 Description: {step_info['description']}")

            if 'required_fields' in step_info:
                print("✅ Required Fields:")
                for field in step_info['required_fields']:
                    print(f"  - {field}")

            if 'optional_fields' in step_info:
                print("📝 Optional Fields:")
                for field in step_info['optional_fields']:
                    print(f"  - {field}")

            if 'pattern' in step_info:
                print(f"🔢 Pattern: {step_info['pattern']}")

            if 'logic' in step_info:
                print(f"🧠 Logic: {step_info['logic']}")

            if 'note' in step_info:
                print(f"💡 Note: {step_info['note']}")

            if 'example' in step_info:
                print("📋 Example:")
                for key, value in step_info['example'].items():
                    print(f"  {key}: {value}")

    def explore_sample_booking_workflow(self):
        """Explore a complete sample booking to show all relationships"""
        print(f"\n🔍 SAMPLE BOOKING WORKFLOW ANALYSIS")
        print("="*100)

        try:
            # Find a recent booking with equipment and crew
            if self.db_type == 'sql_server':
                self.cursor.execute("""
                    SELECT TOP 1 b.booking_no, b.contact_namev6, b.organizationv6,
                           b.sdate, b.venueid, b.venueroom, b.price_quoted, b.expattendees,
                           COUNT(i.id) as equipment_count, COUNT(c.id) as crew_count
                    FROM tblbookings b
                    LEFT JOIN tblitemtran i ON b.booking_no = i.booking_no_v32
                    LEFT JOIN tblcrew c ON b.booking_no = c.booking_no
                    WHERE b.booking_no IS NOT NULL
                    GROUP BY b.booking_no, b.contact_namev6, b.organizationv6,
                             b.sdate, b.venueid, b.venueroom, b.price_quoted, b.expattendees
                    HAVING COUNT(i.id) > 0 OR COUNT(c.id) > 0
                    ORDER BY b.id DESC
                """)
            else:
                self.cursor.execute("""
                    SELECT b.booking_no, b.contact_namev6, b.organizationv6,
                           b.sdate, b.venueid, b.venueroom, b.price_quoted, b.expattendees,
                           COUNT(i.id) as equipment_count, COUNT(c.id) as crew_count
                    FROM tblbookings b
                    LEFT JOIN tblitemtran i ON b.booking_no = i.booking_no_v32
                    LEFT JOIN tblcrew c ON b.booking_no = c.booking_no
                    WHERE b.booking_no IS NOT NULL
                    GROUP BY b.booking_no, b.contact_namev6, b.organizationv6,
                             b.sdate, b.venueid, b.venueroom, b.price_quoted, b.expattendees
                    HAVING COUNT(i.id) > 0 OR COUNT(c.id) > 0
                    ORDER BY b.id DESC
                    LIMIT 1
                """)

            booking = self.cursor.fetchone()
            if booking:
                print("📋 SAMPLE BOOKING FOUND:")
                print(json.dumps(dict(booking), indent=2, default=str))

                booking_no = booking['booking_no']

                # Get equipment items
                print(f"\n🔧 EQUIPMENT ITEMS for booking {booking_no}:")
                if self.db_type == 'sql_server':
                    self.cursor.execute("""
                        SELECT product_code_v42, item_desc, hire_rate, qty, line_total
                        FROM tblitemtran
                        WHERE booking_no_v32 = %s
                        ORDER BY id
                    """, (booking_no,))
                else:
                    self.cursor.execute("""
                        SELECT product_code_v42, item_desc, hire_rate, qty, line_total
                        FROM tblitemtran
                        WHERE booking_no_v32 = %s
                        ORDER BY id
                    """, (booking_no,))

                equipment = self.cursor.fetchall()
                if equipment:
                    eq_data = []
                    for item in equipment:
                        eq_data.append([
                            item['product_code_v42'] or 'N/A',
                            item['item_desc'] or 'N/A',
                            f"${item['hire_rate'] or 0:.2f}",
                            item['qty'] or 0,
                            f"${item['line_total'] or 0:.2f}"
                        ])
                    print(tabulate(eq_data, headers=['Product Code', 'Description', 'Rate', 'Qty', 'Total'], tablefmt='grid'))
                else:
                    print("No equipment items found")

                # Get crew items
                print(f"\n👷 CREW ITEMS for booking {booking_no}:")
                if self.db_type == 'sql_server':
                    self.cursor.execute("""
                        SELECT crew_desc, hours, rate, line_total
                        FROM tblcrew
                        WHERE booking_no = %s
                        ORDER BY id
                    """, (booking_no,))
                else:
                    self.cursor.execute("""
                        SELECT crew_desc, hours, rate, line_total
                        FROM tblcrew
                        WHERE booking_no = %s
                        ORDER BY id
                    """, (booking_no,))

                crew = self.cursor.fetchall()
                if crew:
                    crew_data = []
                    for item in crew:
                        crew_data.append([
                            item['crew_desc'] or 'N/A',
                            item['hours'] or 0,
                            f"${item['rate'] or 0:.2f}",
                            f"${item['line_total'] or 0:.2f}"
                        ])
                    print(tabulate(crew_data, headers=['Description', 'Hours', 'Rate', 'Total'], tablefmt='grid'))
                else:
                    print("No crew items found")

                # Get related contact and customer info
                print(f"\n👤 CONTACT & CUSTOMER INFO for booking {booking_no}:")
                contact_name = booking['contact_namev6']
                organization = booking['organizationv6']

                if self.db_type == 'sql_server':
                    self.cursor.execute("""
                        SELECT c.contactname, c.firstname, c.surname, c.email, c.cell, c.position,
                               cust.organisationv6, cust.customer_code
                        FROM tblContact c
                        JOIN tblLinkCustContact lcc ON c.id = lcc.contactid
                        JOIN tblcust cust ON lcc.customer_code = cust.customer_code
                        WHERE c.contactname = %s AND cust.organisationv6 = %s
                    """, (contact_name, organization))
                else:
                    self.cursor.execute("""
                        SELECT c.contactname, c.firstname, c.surname, c.email, c.cell, c.position,
                               cust.organisationv6, cust.customer_code
                        FROM tblcontact c
                        JOIN tbllinkcustcontact lcc ON c.id = lcc.contactid
                        JOIN tblcust cust ON lcc.customer_code = cust.customer_code
                        WHERE c.contactname = %s AND cust.organisationv6 = %s
                    """, (contact_name, organization))

                contact_info = self.cursor.fetchone()
                if contact_info:
                    print(json.dumps(dict(contact_info), indent=2, default=str))
                else:
                    print("Contact information not found")

            else:
                print("❌ No suitable sample booking found with equipment/crew")

        except Exception as e:
            print(f"❌ Error exploring sample booking: {e}")

    def run_full_analysis(self):
        """Run complete database analysis"""
        print("🚀 STARTING COMPREHENSIVE DATABASE ANALYSIS")
        print("="*100)
        print(f"Timestamp: {datetime.now()}")
        print("="*100)

        if not self.connect_database():
            return

        try:
            # 1. Database overview
            self.explore_database_overview()

            # 2. Key tables analysis
            self.explore_key_tables()

            # 3. Relationships
            self.explore_relationships()

            # 4. Sample booking workflow
            self.explore_sample_booking_workflow()

            # 5. Creation guide
            self.show_creation_workflow()

            print(f"\n✅ ANALYSIS COMPLETE at {datetime.now()}")

        except Exception as e:
            print(f"❌ ERROR during analysis: {e}")
        finally:
            if self.connection:
                self.connection.close()

def main():
    """Main function"""
    explorer = DatabaseExplorer()
    explorer.run_full_analysis()

if __name__ == "__main__":
    main()
