import pymssql
import json
from datetime import datetime

# Database connection details
server = '116.90.5.144'
port = 41383
database = 'AITESTDB'
username = 'PowerBI-Consult'
password = '2tW@ostq3a3_9oV3m-TBQu3w'

def connect_to_db():
    """Connect to the remote SQL Server database"""
    try:
        conn = pymssql.connect(
            server=server,
            port=port,
            user=username,
            password=password,
            database=database,
            as_dict=True
        )
        print("✓ Successfully connected to database AITESTDB")
        return conn
    except Exception as e:
        print(f"✗ Failed to connect: {e}")
        return None

def get_table_columns(cursor, schema, table):
    """Get detailed column information for a specific table"""
    query = """
    SELECT
        c.COLUMN_NAME,
        c.DATA_TYPE,
        c.IS_NULLABLE,
        c.COLUMN_DEFAULT,
        c.CHARACTER_MAXIMUM_LENGTH,
        c.NUMERIC_PRECISION,
        c.NUMERIC_SCALE,
        c.ORDINAL_POSITION,
        CASE
            WHEN pk.COLUMN_NAME IS NOT NULL THEN 'YES'
            ELSE 'NO'
        END AS IS_PRIMARY_KEY,
        CASE
            WHEN fk.COLUMN_NAME IS NOT NULL THEN 'YES'
            ELSE 'NO'
        END AS IS_FOREIGN_KEY,
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
    cursor.execute(query, (schema, table))
    return cursor.fetchall()

def get_row_count(cursor, schema, table):
    """Get row count for a table"""
    try:
        cursor.execute(f"SELECT COUNT(*) as row_count FROM [{schema}].[{table}]")
        result = cursor.fetchone()
        return result['row_count'] if result else 0
    except Exception as e:
        return f"Error: {str(e)}"

def get_sample_data(cursor, schema, table, limit=3):
    """Get sample data from table"""
    try:
        cursor.execute(f"SELECT TOP {limit} * FROM [{schema}].[{table}]")
        return cursor.fetchall()
    except Exception as e:
        return None

def analyze_table(cursor, schema, table, description):
    """Analyze a table and return comprehensive data structure"""
    print(f"\n{'='*80}")
    print(f"TABLE: {schema}.{table}")
    print(f"{'='*80}")

    # Get columns
    columns = get_table_columns(cursor, schema, table)
    if columns:
        print(f"COLUMNS ({len(columns)}):")
        for col in columns:
            nullable = "NULL" if col['IS_NULLABLE'] == 'YES' else "NOT NULL"
            data_type = col['DATA_TYPE']
            if col['CHARACTER_MAXIMUM_LENGTH']:
                data_type += f"({col['CHARACTER_MAXIMUM_LENGTH']})"
            elif col['NUMERIC_PRECISION'] and col['NUMERIC_SCALE'] is not None:
                data_type += f"({col['NUMERIC_PRECISION']},{col['NUMERIC_SCALE']})"
            elif col['NUMERIC_PRECISION']:
                data_type += f"({col['NUMERIC_PRECISION']})"

            default = f" DEFAULT {col['COLUMN_DEFAULT']}" if col['COLUMN_DEFAULT'] else ""
            print(f"  - {col['COLUMN_NAME']}: {data_type} {nullable}{default}")

    # Get primary keys
    pks = [col for col in columns if col['IS_PRIMARY_KEY'] == 'YES']
    if pks:
        print(f"\nPRIMARY KEY:")
        pk_cols = [pk['COLUMN_NAME'] for pk in pks]
        print(f"  - {', '.join(pk_cols)}")

    # Get foreign keys
    fks = [col for col in columns if col['IS_FOREIGN_KEY'] == 'YES']
    if fks:
        print(f"\nFOREIGN KEYS:")
        for fk in fks:
            print(f"  - {fk['COLUMN_NAME']} -> {fk['REFERENCED_TABLE_NAME']}.{fk['REFERENCED_COLUMN_NAME']}")

    # Get row count
    row_count = get_row_count(cursor, schema, table)
    print(f"\nROW COUNT: {row_count:,}")

    # Get sample data
    samples = get_sample_data(cursor, schema, table, limit=3)
    if samples:
        print(f"\nSAMPLE DATA (first 3 rows):")
        for i, sample in enumerate(samples):
            print(f"  Row {i+1}: {sample}")
    else:
        print(f"\nSAMPLE DATA: No sample data available")

    return {
        'name': table,
        'schema': schema,
        'description': description,
        'row_count': row_count,
        'columns': columns,
        'sample_data': samples or []
    }

def main():
    print("🔍 DATABASE STRUCTURE ANALYSIS")
    print("=" * 80)

    conn = connect_to_db()
    if not conn:
        return

    cursor = conn.cursor()

    # Get all tables
    cursor.execute("""
    SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_TYPE = 'BASE TABLE'
    ORDER BY TABLE_SCHEMA, TABLE_NAME
    """)
    tables = cursor.fetchall()

    print(f"\n📋 TABLES FOUND: {len(tables)}")
    for table in tables:
        print(f"  - {table['TABLE_SCHEMA']}.{table['TABLE_NAME']}")

    # Get all views
    cursor.execute("""
    SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_TYPE = 'VIEW'
    ORDER BY TABLE_SCHEMA, TABLE_NAME
    """)
    views = cursor.fetchall()

    print(f"\n👁️  VIEWS FOUND: {len(views)}")
    for view in views:
        print(f"  - {view['TABLE_SCHEMA']}.{view['TABLE_NAME']}")

    # Analyze specific tables mentioned by user
    key_tables = [
        ('dbo', 'tblbookings', 'Main bookings table - stores all booking/order details including customer, venue, dates, and financial information'),
        ('dbo', 'vwProdsComponents', 'View showing product components - displays what items/components make up package products'),
        ('dbo', 'tblitemtran', 'Equipment transactions table - tracks all equipment items in bookings including quantities, rates, and dates'),
        ('dbo', 'tblcrew', 'Labour/crew table - stores labour assignments for bookings with tasks, hours, and rates'),
        ('dbo', 'tblContact', 'Contact persons table - stores contact information for individuals'),
        ('dbo', 'tblcust', 'Customer/organization table - stores customer companies and organizations'),
        ('dbo', 'tblLinkCustContact', 'Customer-contact link table - links contacts to customer organizations (many-to-many)'),
        ('dbo', 'tblSalesper', 'Salespersons table - stores sales representative information'),
        ('dbo', 'tblinvmas', 'Products/inventory master table - master list of all products, packages, and equipment'),
        ('dbo', 'tbltask', 'Labour tasks table - defines labour task types (setup, pack down, operate, etc.)'),
        ('dbo', 'tblInvmas_Labour_Rates', 'Labour rates table - stores labour rates for products by location')
    ]

    print(f"\n🔍 ANALYZING KEY TABLES:")
    print("=" * 80)

    results = []
    for schema, table, description in key_tables:
        try:
            table_data = analyze_table(cursor, schema, table, description)
            results.append(table_data)
        except Exception as e:
            print(f"\n❌ ERROR analyzing {schema}.{table}: {e}")

    # Save to JSON
    with open('database_structure.json', 'w') as f:
        json.dump(results, f, indent=2, default=str)

    print(f"\n✅ Database analysis completed at {datetime.now()}")
    print("📄 Results saved to database_structure.json"
    conn.close()

if __name__ == "__main__":
    main()
