#!/usr/bin/env python3
"""
Create a standalone database schema viewer with embedded JSON data
No web server required - works from local file system
"""

import json
import os
from pathlib import Path

def load_json_files():
    """Load all JSON files from database_schema directory"""
    schema_dir = Path("database_schema")
    json_data = {}

    if not schema_dir.exists():
        print("❌ database_schema directory not found!")
        print("Run split_tables.py first to create individual table files.")
        return None

    # Load each JSON file
    for json_file in schema_dir.glob("*.json"):
        if json_file.name == "README.json":
            continue  # Skip the summary file

        try:
            with open(json_file, 'r') as f:
                data = json.load(f)
                table_name = json_file.stem  # Remove .json extension
                json_data[table_name] = data
                print(f"📄 Loaded: {table_name}")
        except Exception as e:
            print(f"❌ Error loading {json_file.name}: {e}")
            continue

    return json_data

def create_standalone_html(json_data):
    """Create HTML file with embedded JSON data"""

    json_js = json.dumps(json_data, indent=2)

    html_content = '''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Database Schema Documentation - AITESTDB</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Roboto', 'Oxygen', 'Ubuntu', 'Cantarell', sans-serif;
            line-height: 1.6;
            color: #2c3e50;
            background: #f5f7fa;
            min-height: 100vh;
        }

        .container {
            max-width: 1400px;
            margin: 0 auto;
            padding: 40px 20px;
        }

        .header {
            background: white;
            border-radius: 8px;
            padding: 40px;
            margin-bottom: 30px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
            border-left: 4px solid #2c3e50;
        }

        .header h1 {
            color: #2c3e50;
            font-size: 2.2rem;
            margin-bottom: 10px;
            font-weight: 600;
        }

        .header p {
            color: #7f8c8d;
            font-size: 1.05rem;
        }

        .db-info {
            display: inline-block;
            margin-top: 15px;
            padding: 8px 16px;
            background: #ecf0f1;
            border-radius: 4px;
            font-family: 'Monaco', 'Menlo', monospace;
            font-size: 0.9rem;
            color: #34495e;
        }

        .stats-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            border-radius: 8px;
            padding: 24px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
            border-top: 3px solid #3498db;
        }

        .stat-label {
            font-size: 0.85rem;
            color: #7f8c8d;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
            font-weight: 500;
        }

        .stat-value {
            font-size: 2rem;
            font-weight: 600;
            color: #2c3e50;
        }

        .tables-list {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.08);
            overflow: hidden;
        }

        .table-item {
            padding: 20px 30px;
            border-bottom: 1px solid #ecf0f1;
            cursor: pointer;
            transition: background-color 0.15s ease;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .table-item:hover {
            background-color: #f8f9fa;
        }

        .table-item:last-child {
            border-bottom: none;
        }

        .table-info {
            flex: 1;
        }

        .table-name {
            font-size: 1.15rem;
            font-weight: 600;
            color: #2c3e50;
            margin-bottom: 6px;
            font-family: 'Monaco', 'Menlo', monospace;
        }

        .table-type {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 3px;
            font-size: 0.7rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-right: 8px;
        }

        .table-type.table {
            background: #d4edda;
            color: #155724;
        }

        .table-type.view {
            background: #fff3cd;
            color: #856404;
        }

        .table-description {
            color: #7f8c8d;
            font-size: 0.95rem;
            margin-bottom: 8px;
        }

        .table-meta {
            display: flex;
            gap: 20px;
            font-size: 0.85rem;
            color: #95a5a6;
        }

        .meta-item {
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .table-actions {
            display: flex;
            gap: 10px;
        }

        .btn {
            padding: 8px 16px;
            border: 1px solid #dfe6e9;
            background: white;
            color: #2c3e50;
            border-radius: 4px;
            cursor: pointer;
            font-size: 0.9rem;
            font-weight: 500;
            transition: all 0.15s ease;
        }

        .btn:hover {
            background: #2c3e50;
            color: white;
            border-color: #2c3e50;
        }

        .btn-primary {
            background: #3498db;
            color: white;
            border-color: #3498db;
        }

        .btn-primary:hover {
            background: #2980b9;
            border-color: #2980b9;
        }

        .modal {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(44, 62, 80, 0.95);
            z-index: 1000;
        }

        .modal.active {
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .modal-content {
            background: white;
            width: 95%;
            max-width: 1400px;
            max-height: 90vh;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.3);
            display: flex;
            flex-direction: column;
        }

        .modal-header {
            padding: 24px 30px;
            background: #2c3e50;
            color: white;
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 1px solid #34495e;
        }

        .modal-title {
            font-size: 1.4rem;
            font-weight: 600;
            font-family: 'Monaco', 'Menlo', monospace;
        }

        .modal-toolbar {
            display: flex;
            gap: 10px;
            align-items: center;
        }

        .close-btn {
            background: none;
            border: none;
            color: white;
            font-size: 1.8rem;
            cursor: pointer;
            padding: 0;
            width: 32px;
            height: 32px;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 4px;
            transition: background 0.15s ease;
        }

        .close-btn:hover {
            background: rgba(255, 255, 255, 0.1);
        }

        .modal-body {
            flex: 1;
            overflow-y: auto;
            background: #1e1e1e;
        }

        .json-content {
            padding: 30px;
            font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', 'Courier New', monospace;
            font-size: 0.9rem;
            line-height: 1.6;
            white-space: pre;
            color: #d4d4d4;
            overflow-x: auto;
        }

        /* JSON Syntax Highlighting */
        .json-key {
            color: #9cdcfe;
        }

        .json-string {
            color: #ce9178;
        }

        .json-number {
            color: #b5cea8;
        }

        .json-boolean {
            color: #569cd6;
        }

        .json-null {
            color: #569cd6;
        }

        .json-bracket {
            color: #ffd700;
            font-weight: bold;
        }

        .copy-btn {
            position: sticky;
            top: 10px;
            float: right;
            margin: 10px;
            padding: 8px 16px;
            background: #3498db;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 0.9rem;
            font-weight: 500;
            z-index: 10;
        }

        .copy-btn:hover {
            background: #2980b9;
        }

        .copy-btn.copied {
            background: #27ae60;
        }

        .search-box {
            padding: 20px 30px;
            background: white;
            border-bottom: 1px solid #ecf0f1;
        }

        .search-input {
            width: 100%;
            padding: 12px 16px;
            border: 2px solid #dfe6e9;
            border-radius: 4px;
            font-size: 1rem;
            transition: border-color 0.15s ease;
        }

        .search-input:focus {
            outline: none;
            border-color: #3498db;
        }

        .footer {
            text-align: center;
            padding: 30px 20px;
            color: #7f8c8d;
            font-size: 0.9rem;
        }

        @media (max-width: 768px) {
            .container {
                padding: 20px 15px;
            }

            .header {
                padding: 24px;
            }

            .header h1 {
                font-size: 1.8rem;
            }

            .table-item {
                flex-direction: column;
                align-items: flex-start;
                gap: 15px;
            }

            .table-actions {
                width: 100%;
            }

            .btn {
                flex: 1;
            }

            .modal-content {
                width: 100%;
                max-height: 100vh;
                border-radius: 0;
            }
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Database Schema Documentation</h1>
            <p>Complete structure and column definitions for AITESTDB</p>
            <div class="db-info">Server: 116.90.5.144:41383 | Database: AITESTDB</div>
        </div>

        <div class="stats-row">
            <div class="stat-card">
                <div class="stat-label">Total Tables</div>
                <div class="stat-value">11</div>
            </div>
            <div class="stat-card">
                <div class="stat-label">Total Columns</div>
                <div class="stat-value">~700</div>
            </div>
            <div class="stat-card">
                <div class="stat-label">Total Records</div>
                <div class="stat-value">~2.2M</div>
            </div>
            <div class="stat-card">
                <div class="stat-label">Primary Keys</div>
                <div class="stat-value">10</div>
            </div>
        </div>

        <div class="search-box">
            <input type="text" class="search-input" id="searchInput" placeholder="Search tables by name or description...">
        </div>

        <div class="tables-list" id="tablesList">
            <!-- Tables will be loaded here -->
        </div>
    </div>

    <div class="footer">
        <p>Generated on November 20, 2025 | Database Schema Documentation</p>
    </div>

    <div class="modal" id="jsonModal">
        <div class="modal-content">
            <div class="modal-header">
                <h2 class="modal-title" id="modalTitle">Table Schema</h2>
                <div class="modal-toolbar">
                    <button class="btn" onclick="downloadJSON()">Download JSON</button>
                    <button class="close-btn" onclick="closeModal()">&times;</button>
                </div>
            </div>
            <div class="modal-body">
                <div class="json-container">
                    <button class="copy-btn" onclick="copyJSON()">Copy JSON</button>
                    <div class="json-content" id="jsonContent"></div>
                </div>
            </div>
        </div>
    </div>

    <script>
        // Embedded JSON data
        const jsonData = ''' + json_js + ''';

        const schemaData = {
            "tables": [
                {"name": "tblbookings", "description": "Main bookings table - stores all booking/order details including customer, venue, dates, and financial information", "row_count": 68966, "column_count": 204, "has_primary_key": true, "type": "table"},
                {"name": "vwProdsComponents", "description": "View showing product components - displays what items/components make up package products", "row_count": 6268, "column_count": 18, "has_primary_key": false, "type": "view"},
                {"name": "tblitemtran", "description": "Equipment transactions table - tracks all equipment items in bookings including quantities, rates, and dates", "row_count": 1826879, "column_count": 73, "has_primary_key": true, "type": "table"},
                {"name": "tblcrew", "description": "Labour/crew table - stores labour assignments for bookings with tasks, hours, and rates", "row_count": 195524, "column_count": 58, "has_primary_key": true, "type": "table"},
                {"name": "tblContact", "description": "Contact persons table - stores contact information for individuals", "row_count": 20738, "column_count": 98, "has_primary_key": true, "type": "table"},
                {"name": "tblcust", "description": "Customer/organization table - stores customer companies and organizations", "row_count": 14422, "column_count": 141, "has_primary_key": true, "type": "table"},
                {"name": "tblLinkCustContact", "description": "Customer-contact link table - links contacts to customer organizations (many-to-many)", "row_count": 20604, "column_count": 3, "has_primary_key": true, "type": "table"},
                {"name": "tblSalesper", "description": "Salespersons table - stores sales representative information", "row_count": 187, "column_count": 17, "has_primary_key": true, "type": "table"},
                {"name": "tblinvmas", "description": "Products/inventory master table - master list of all products, packages, and equipment", "row_count": 4795, "column_count": 97, "has_primary_key": true, "type": "table"},
                {"name": "tbltask", "description": "Labour tasks table - defines labour task types (setup, pack down, operate, etc.)", "row_count": 26, "column_count": 6, "has_primary_key": true, "type": "table"},
                {"name": "tblInvmas_Labour_Rates", "description": "Labour rates table - stores labour rates for products by location", "row_count": 21980, "column_count": 7, "has_primary_key": true, "type": "table"}
            ]
        };

        let currentTableData = null;
        let currentTableName = '';

        function formatNumber(num) {
            if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M';
            if (num >= 1000) return (num / 1000).toFixed(1) + 'K';
            return num.toString();
        }

        function syntaxHighlight(json) {
            if (typeof json != 'string') {
                json = JSON.stringify(json, null, 2);
            }
            json = json.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            return json.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?)/g, function (match) {
                var cls = 'json-number';
                if (/^"/.test(match)) {
                    if (/:$/.test(match)) {
                        cls = 'json-key';
                    } else {
                        cls = 'json-string';
                    }
                } else if (/true|false/.test(match)) {
                    cls = 'json-boolean';
                } else if (/null/.test(match)) {
                    cls = 'json-null';
                }
                return '<span class="' + cls + '">' + match + '</span>';
            });
        }

        function renderTables() {
            const list = document.getElementById('tablesList');
            list.innerHTML = '';

            schemaData.tables.forEach(table => {
                const item = document.createElement('div');
                item.className = 'table-item';

                item.innerHTML = `
                    <div class="table-info">
                        <div class="table-name">
                            <span class="table-type ${table.type}">${table.type}</span>
                            ${table.name}
                        </div>
                        <div class="table-description">${table.description}</div>
                        <div class="table-meta">
                            <div class="meta-item">
                                <span>Rows: ${formatNumber(table.row_count)}</span>
                            </div>
                            <div class="meta-item">
                                <span>Columns: ${table.column_count}</span>
                            </div>
                            ${table.has_primary_key ? '<div class="meta-item"><span>Primary Key</span></div>' : ''}
                        </div>
                    </div>
                    <div class="table-actions">
                        <button class="btn btn-primary" onclick="showTableSchema('${table.name}')">View Schema</button>
                    </div>
                `;

                item.onclick = () => showTableSchema(table.name);
                list.appendChild(item);
            });
        }

        function showTableSchema(tableName) {
            const data = jsonData[tableName];
            if (!data) {
                alert('Schema data not found for: ' + tableName);
                return;
            }

            currentTableData = data;
            currentTableName = tableName;

            document.getElementById('modalTitle').textContent = tableName;
            document.getElementById('jsonContent').innerHTML = syntaxHighlight(data);
            document.getElementById('jsonModal').classList.add('active');
        }

        function closeModal() {
            document.getElementById('jsonModal').classList.remove('active');
        }

        function copyJSON() {
            const jsonString = JSON.stringify(currentTableData, null, 2);
            navigator.clipboard.writeText(jsonString).then(() => {
                const btn = document.querySelector('.copy-btn');
                btn.textContent = 'Copied!';
                btn.classList.add('copied');
                setTimeout(() => {
                    btn.textContent = 'Copy JSON';
                    btn.classList.remove('copied');
                }, 2000);
            });
        }

        function downloadJSON() {
            const jsonString = JSON.stringify(currentTableData, null, 2);
            const blob = new Blob([jsonString], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${currentTableName}.json`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }

        // Search functionality
        document.getElementById('searchInput').addEventListener('input', function(e) {
            const searchTerm = e.target.value.toLowerCase();
            const items = document.querySelectorAll('.table-item');

            items.forEach(item => {
                const text = item.textContent.toLowerCase();
                if (text.includes(searchTerm)) {
                    item.style.display = 'flex';
                } else {
                    item.style.display = 'none';
                }
            });
        });

        // Close modal on escape key
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                closeModal();
            }
        });

        // Close modal when clicking outside
        document.getElementById('jsonModal').addEventListener('click', function(e) {
            if (e.target === this) {
                closeModal();
            }
        });

        // Initialize
        renderTables();
    </script>
</body>
</html>'''

    output_file = "database_schema_standalone.html"
    with open(output_file, 'w') as f:
        f.write(html_content)

    print(f"\nCreated standalone viewer: {output_file}")
    return True

def main():
    print("Creating Standalone Database Schema Viewer")
    print("=" * 50)

    json_data = load_json_files()
    if not json_data:
        return

    success = create_standalone_html(json_data)
    if success:
        print("\nSuccess! Open 'database_schema_standalone.html' in your browser")
        print("Features:")
        print("  - No web server required")
        print("  - Works from local file system")
        print("  - All data embedded in single file")
        print("  - JSON syntax highlighting")
        print("  - Search functionality")
        print("  - Copy and download JSON")

if __name__ == "__main__":
    main()
