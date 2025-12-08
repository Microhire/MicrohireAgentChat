import json
import os
from pathlib import Path

# Load the large JSON file
try:
    with open('database_structure.json', 'r') as f:
        tables_data = json.load(f)
except FileNotFoundError:
    print("❌ database_structure.json not found!")
    print("Run db_explore.py first to generate the database structure file.")
    exit(1)

# Create output directory
output_dir = 'database_schema'
os.makedirs(output_dir, exist_ok=True)

print(f"📂 Creating individual table files in '{output_dir}/' directory...\n")

# Split each table into its own file
for table in tables_data:
    table_name = table['name']
    filename = f"{output_dir}/{table_name}.json"

    with open(filename, 'w') as f:
        json.dump(table, f, indent=2)

    print(f"✓ Created: {filename}")
    print(f"  - Columns: {len(table['columns'])}")
    try:
        row_count = int(table['row_count'])
        print(f"  - Row count: {row_count:,}")
    except (ValueError, TypeError):
        print(f"  - Row count: {table['row_count']}")
    print()

# Create a summary/index file
summary = {
    'database': 'AITESTDB',
    'server': '116.90.5.144:41383',
    'total_tables': len(tables_data),
    'tables': []
}

for table in tables_data:
    summary['tables'].append({
        'name': table['name'],
        'description': table['description'],
        'row_count': table['row_count'],
        'column_count': len(table['columns']),
        'has_primary_key': any(col.get('IS_PRIMARY_KEY') == 'YES' for col in table['columns']),
        'has_foreign_keys': any(col.get('IS_FOREIGN_KEY') == 'YES' for col in table['columns']),
        'file': f"{table['name']}.json"
    })

# Save summary
summary_file = f"{output_dir}/README.json"
with open(summary_file, 'w') as f:
    json.dump(summary, f, indent=2)

print(f"✓ Created: {summary_file}")
print(f"\n✅ Successfully split {len(tables_data)} tables into individual files!")
print(f"📋 See {summary_file} for a quick overview of all tables.")
