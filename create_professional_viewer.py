#!/usr/bin/env python3
"""
Create a professional database schema viewer with dark mode
"""

import json
import os
from pathlib import Path

def load_json_files():
    """Load all JSON files from database_schema directory"""
    schema_dir = Path("database_schema")
    json_data = {}

    if not schema_dir.exists():
        print("Error: database_schema directory not found!")
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
                print(f"Loaded: {table_name}")
        except Exception as e:
            print(f"Error loading {json_file.name}: {e}")
            continue

    return json_data

def create_professional_html(json_data):
    """Create professional HTML viewer with JSON syntax highlighting"""

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
            color: #e2e8f0;
            background: linear-gradient(135deg, #0f0f23 0%, #1a1a2e 50%, #16213e 100%);
            min-height: 100vh;
            margin: 0;
        }

        .container {
            max-width: 1600px;
            margin: 0 auto;
            padding: 40px 20px;
        }

        .header {
            background: linear-gradient(135deg, rgba(30, 41, 59, 0.95) 0%, rgba(15, 23, 42, 0.95) 100%);
            backdrop-filter: blur(20px);
            border-radius: 16px;
            padding: 40px;
            margin-bottom: 30px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3), 0 0 0 1px rgba(255, 255, 255, 0.1);
            border: 1px solid rgba(255, 255, 255, 0.1);
            position: relative;
        }

        .header::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 2px;
            background: linear-gradient(90deg, #3b82f6, #8b5cf6, #ec4899);
            border-radius: 16px 16px 0 0;
        }

        .header h1 {
            color: #f1f5f9;
            font-size: 2.5rem;
            margin-bottom: 10px;
            font-weight: 700;
            text-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
        }

        .header p {
            color: #94a3b8;
            font-size: 1.1rem;
            opacity: 0.9;
        }

        .db-info {
            display: inline-block;
            margin-top: 20px;
            padding: 10px 20px;
            background: linear-gradient(135deg, rgba(59, 130, 246, 0.2) 0%, rgba(139, 92, 246, 0.2) 100%);
            border: 1px solid rgba(59, 130, 246, 0.3);
            border-radius: 8px;
            font-family: 'JetBrains Mono', 'Monaco', 'Menlo', monospace;
            font-size: 0.9rem;
            color: #60a5fa;
            font-weight: 500;
        }

        .stats-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: linear-gradient(135deg, rgba(30, 41, 59, 0.9) 0%, rgba(15, 23, 42, 0.9) 100%);
            backdrop-filter: blur(20px);
            border-radius: 12px;
            padding: 28px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3), 0 0 0 1px rgba(255, 255, 255, 0.1);
            border: 1px solid rgba(255, 255, 255, 0.1);
            transition: all 0.3s ease;
            position: relative;
            overflow: hidden;
        }

        .stat-card::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 3px;
            background: linear-gradient(90deg, #3b82f6, #8b5cf6, #ec4899);
        }

        .stat-card:hover {
            transform: translateY(-4px);
            box-shadow: 0 12px 40px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(59, 130, 246, 0.3);
        }

        .stat-label {
            font-size: 0.85rem;
            color: #94a3b8;
            text-transform: uppercase;
            letter-spacing: 1px;
            margin-bottom: 12px;
            font-weight: 600;
            opacity: 0.8;
        }

        .stat-value {
            font-size: 2.2rem;
            font-weight: 700;
            color: #f1f5f9;
            text-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
        }

        .search-box {
            padding: 30px;
            background: linear-gradient(135deg, rgba(30, 41, 59, 0.95) 0%, rgba(15, 23, 42, 0.95) 100%);
            backdrop-filter: blur(20px);
            border-radius: 12px;
            margin-bottom: 30px;
            box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
            border: 1px solid rgba(255, 255, 255, 0.1);
        }

        .search-input {
            width: 100%;
            padding: 16px 20px;
            background: rgba(15, 23, 42, 0.8);
            border: 2px solid rgba(59, 130, 246, 0.3);
            border-radius: 8px;
            font-size: 1rem;
            color: #f1f5f9;
            transition: all 0.3s ease;
            font-family: inherit;
        }

        .search-input:focus {
            outline: none;
            border-color: #3b82f6;
            box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.2);
            background: rgba(30, 41, 59, 0.9);
        }

        .search-input::placeholder {
            color: #64748b;
        }

        .tables-list {
            background: linear-gradient(135deg, rgba(30, 41, 59, 0.95) 0%, rgba(15, 23, 42, 0.95) 100%);
            backdrop-filter: blur(20px);
            border-radius: 16px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3), 0 0 0 1px rgba(255, 255, 255, 0.1);
            border: 1px solid rgba(255, 255, 255, 0.1);
            overflow: hidden;
        }

        .table-item {
            padding: 24px 30px;
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            justify-content: space-between;
            align-items: center;
            position: relative;
        }

        .table-item:hover {
            background: linear-gradient(135deg, rgba(59, 130, 246, 0.1) 0%, rgba(139, 92, 246, 0.1) 100%);
            transform: translateX(4px);
        }

        .table-item:hover::before {
            content: '';
            position: absolute;
            left: 0;
            top: 0;
            bottom: 0;
            width: 4px;
            background: linear-gradient(180deg, #3b82f6, #8b5cf6);
        }

        .table-item:last-child {
            border-bottom: none;
        }

        .table-name {
            font-size: 1.2rem;
            font-weight: 600;
            color: #f1f5f9;
            margin-bottom: 8px;
            font-family: 'JetBrains Mono', 'Monaco', 'Menlo', monospace;
            text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
        }

        .table-type {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 6px;
            font-size: 0.75rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 1px;
            margin-right: 12px;
            border: 1px solid rgba(255, 255, 255, 0.2);
        }

        .table-type.table {
            background: linear-gradient(135deg, rgba(34, 197, 94, 0.2) 0%, rgba(22, 163, 74, 0.2) 100%);
            color: #4ade80;
            border-color: rgba(34, 197, 94, 0.3);
        }

        .table-type.view {
            background: linear-gradient(135deg, rgba(251, 191, 36, 0.2) 0%, rgba(245, 158, 11, 0.2) 100%);
            color: #fbbf24;
            border-color: rgba(251, 191, 36, 0.3);
        }

        .table-description {
            color: #cbd5e1;
            font-size: 0.95rem;
            margin-bottom: 12px;
            line-height: 1.5;
        }

        .table-meta {
            display: flex;
            gap: 24px;
            font-size: 0.85rem;
            color: #94a3b8;
            opacity: 0.9;
        }

        .btn {
            padding: 10px 20px;
            border: 1px solid rgba(255, 255, 255, 0.2);
            background: linear-gradient(135deg, rgba(59, 130, 246, 0.1) 0%, rgba(139, 92, 246, 0.1) 100%);
            color: #e2e8f0;
            border-radius: 8px;
            cursor: pointer;
            font-size: 0.9rem;
            font-weight: 600;
            transition: all 0.3s ease;
            backdrop-filter: blur(10px);
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .btn:hover {
            background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
            border-color: rgba(59, 130, 246, 0.5);
            transform: translateY(-2px);
            box-shadow: 0 4px 16px rgba(59, 130, 246, 0.3);
        }

        .btn-primary {
            background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
            color: white;
            border-color: rgba(59, 130, 246, 0.5);
            box-shadow: 0 2px 8px rgba(59, 130, 246, 0.2);
        }

        .btn-primary:hover {
            background: linear-gradient(135deg, #2563eb 0%, #7c3aed 100%);
            box-shadow: 0 4px 16px rgba(59, 130, 246, 0.4);
        }

        .modal {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: linear-gradient(135deg, rgba(15, 23, 42, 0.95) 0%, rgba(0, 0, 0, 0.8) 100%);
            backdrop-filter: blur(20px);
            z-index: 1000;
        }

        .modal.active {
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .modal-content {
            background: linear-gradient(135deg, rgba(30, 41, 59, 0.98) 0%, rgba(15, 23, 42, 0.98) 100%);
            backdrop-filter: blur(20px);
            width: 95%;
            max-width: 1600px;
            max-height: 90vh;
            border-radius: 16px;
            overflow: hidden;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.1);
            border: 1px solid rgba(255, 255, 255, 0.1);
            display: flex;
            flex-direction: column;
        }

        .modal-header {
            padding: 28px 36px;
            background: linear-gradient(135deg, rgba(15, 23, 42, 0.95) 0%, rgba(0, 0, 0, 0.95) 100%);
            color: white;
            display: flex;
            justify-content: space-between;
            align-items: center;
            border-bottom: 1px solid rgba(255, 255, 255, 0.1);
            position: relative;
        }

        .modal-header::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 2px;
            background: linear-gradient(90deg, #3b82f6, #8b5cf6, #ec4899);
        }

        .modal-title {
            font-size: 1.5rem;
            font-weight: 700;
            font-family: 'JetBrains Mono', 'Monaco', 'Menlo', monospace;
            color: #f1f5f9;
            text-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
        }

        .modal-toolbar {
            display: flex;
            gap: 12px;
            align-items: center;
        }

        .close-btn {
            background: rgba(239, 68, 68, 0.1);
            border: 1px solid rgba(239, 68, 68, 0.3);
            color: #fca5a5;
            font-size: 1.4rem;
            cursor: pointer;
            padding: 8px;
            width: 40px;
            height: 40px;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 8px;
            transition: all 0.3s ease;
            font-weight: bold;
        }

        .close-btn:hover {
            background: rgba(239, 68, 68, 0.2);
            border-color: rgba(239, 68, 68, 0.5);
            transform: scale(1.05);
        }

        .modal-body {
            flex: 1;
            overflow-y: auto;
            background: linear-gradient(135deg, #0f0f23 0%, #1a1a2e 100%);
            position: relative;
        }

        .json-content {
            padding: 36px;
            font-family: 'JetBrains Mono', 'Monaco', 'Menlo', 'Ubuntu Mono', 'Courier New', monospace;
            font-size: 0.9rem;
            line-height: 1.7;
            white-space: pre;
            color: #e2e8f0;
            overflow-x: auto;
            background: linear-gradient(135deg, rgba(15, 23, 42, 0.8) 0%, rgba(30, 41, 59, 0.8) 100%);
            border-radius: 0 0 16px 16px;
            position: relative;
        }

        .json-content::before {
            content: '';
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            height: 1px;
            background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.1), transparent);
        }

        /* JSON Syntax Highlighting - Dark Theme */
        .json-key {
            color: #60a5fa;
            font-weight: 500;
        }

        .json-string {
            color: #34d399;
        }

        .json-number {
            color: #fbbf24;
            font-weight: 500;
        }

        .json-boolean {
            color: #f87171;
            font-weight: 600;
        }

        .json-null {
            color: #94a3b8;
            font-style: italic;
        }

        .json-bracket {
            color: #e879f9;
            font-weight: 600;
        }

        .copy-btn {
            position: sticky;
            top: 20px;
            float: right;
            margin: 16px;
            padding: 12px 20px;
            background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
            color: white;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            font-size: 0.9rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            box-shadow: 0 4px 16px rgba(59, 130, 246, 0.3);
            transition: all 0.3s ease;
            z-index: 10;
        }

        .copy-btn:hover {
            background: linear-gradient(135deg, #2563eb 0%, #7c3aed 100%);
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(59, 130, 246, 0.4);
        }

        .copy-btn.copied {
            background: linear-gradient(135deg, #16a34a 0%, #15803d 100%);
            box-shadow: 0 4px 16px rgba(22, 163, 74, 0.3);
        }

        .footer {
            text-align: center;
            padding: 40px 20px;
            color: #94a3b8;
            font-size: 0.9rem;
            opacity: 0.8;
        }

        @keyframes fadeInUp {
            from {
                opacity: 0;
                transform: translateY(30px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        @keyframes fadeIn {
            from {
                opacity: 0;
            }
            to {
                opacity: 1;
            }
        }

        @keyframes slideIn {
            from {
                opacity: 0;
                transform: translateX(-30px);
            }
            to {
                opacity: 1;
                transform: translateX(0);
            }
        }

        @media (max-width: 768px) {
            .container {
                padding: 20px 15px;
            }

            .header {
                padding: 24px;
            }

            .header h1 {
                font-size: 2rem;
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
        let searchTimeout;
        document.getElementById('searchInput').addEventListener('input', function(e) {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                const searchTerm = e.target.value.toLowerCase().trim();
                const items = document.querySelectorAll('.table-item');
                let visibleCount = 0;

                items.forEach(item => {
                    const text = item.textContent.toLowerCase();
                    const tableName = item.querySelector('.table-name').textContent.toLowerCase();
                    const description = item.querySelector('.table-description').textContent.toLowerCase();

                    if (searchTerm === '' || text.includes(searchTerm)) {
                        item.style.display = 'flex';
                        item.style.animation = 'slideIn 0.3s ease-out';
                        visibleCount++;

                        if (searchTerm) {
                            highlightText(item, searchTerm);
                        } else {
                            removeHighlights(item);
                        }
                    } else {
                        item.style.display = 'none';
                        removeHighlights(item);
                    }
                });

                updateSearchResults(searchTerm, visibleCount, items.length);
            }, 300);
        });

        function highlightText(element, searchTerm) {
            const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT, null, false);
            let node;
            while (node = walker.nextNode()) {
                const text = node.textContent;
                const index = text.toLowerCase().indexOf(searchTerm.toLowerCase());
                if (index !== -1) {
                    const before = text.substring(0, index);
                    const match = text.substring(index, index + searchTerm.length);
                    const after = text.substring(index + searchTerm.length);

                    const span = document.createElement('span');
                    span.innerHTML = `${before}<mark style="background: rgba(59, 130, 246, 0.3); padding: 2px 4px; border-radius: 3px;">${match}</mark>${after}`;
                    node.parentNode.replaceChild(span, node);
                }
            }
        }

        function removeHighlights(element) {
            const marks = element.querySelectorAll('mark');
            marks.forEach(mark => {
                mark.outerHTML = mark.innerHTML;
            });
        }

        function updateSearchResults(searchTerm, visibleCount, totalCount) {
            let searchStatus = document.getElementById('searchStatus');
            if (!searchStatus) {
                searchStatus = document.createElement('div');
                searchStatus.id = 'searchStatus';
                searchStatus.style.cssText = `
                    margin-top: 10px;
                    font-size: 0.85rem;
                    color: #94a3b8;
                    text-align: center;
                `;
                document.querySelector('.search-box').appendChild(searchStatus);
            }

            if (searchTerm) {
                searchStatus.textContent = `Found ${visibleCount} of ${totalCount} tables matching "${searchTerm}"`;
            } else {
                searchStatus.textContent = '';
            }
        }

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

    output_file = "database_schema_professional.html"
    with open(output_file, 'w') as f:
        f.write(html_content)

    print(f"\nCreated professional viewer: {output_file}")
    return True

def main():
    print("Creating Professional Database Schema Viewer")
    print("=" * 50)

    json_data = load_json_files()
    if not json_data:
        return

    success = create_professional_html(json_data)
    if success:
        print("\nSuccess! Open 'database_schema_professional.html' in your browser")
        print("Features:")
        print("  - Dark mode with glassmorphism effects")
        print("  - JSON syntax highlighting")
        print("  - Advanced search with highlighting")
        print("  - Copy and download JSON")
        print("  - Professional animations")

if __name__ == "__main__":
    main()
