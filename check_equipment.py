import pyodbc
import json

conn_str = "Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=MONACIP_PROD;TrustServerCertificate=yes;UID=sa;PWD=YourStrong@Passw0rd"
conn = pyodbc.connect(conn_str)
cur = conn.cursor()

# Get unique groups and categories for equipment
print("=== Equipment Groups ===")
cur.execute("SELECT DISTINCT groupFld FROM tblInvmas WHERE groupFld IS NOT NULL AND groupFld != '' ORDER BY groupFld")
for row in cur.fetchall():
    print(f"  {row[0]}")

print("\n=== Computers/Laptops ===")
cur.execute("""
    SELECT TOP 20 product_code, groupFld, category, SubCategory, descriptionV6, PrintedDesc, retail_price
    FROM tblInvmas 
    WHERE (descriptionV6 LIKE '%laptop%' OR descriptionV6 LIKE '%computer%' 
           OR descriptionV6 LIKE '%mac%' OR descriptionV6 LIKE '%pc %'
           OR category LIKE '%computer%' OR category LIKE '%laptop%'
           OR product_code LIKE '%MAC%' OR product_code LIKE '%PC%' OR product_code LIKE '%LAP%')
    AND IsInTrashCan = 'N'
    ORDER BY product_code
""")
for row in cur.fetchall():
    print(f"  {row[0].strip()}: {row[4]} | Group: {row[1]} | Cat: {row[2]} | Sub: {row[3]} | Price: ${row[6] or 0:.2f}")

print("\n=== Projectors ===")
cur.execute("""
    SELECT TOP 15 product_code, groupFld, category, SubCategory, descriptionV6, PrintedDesc, retail_price
    FROM tblInvmas 
    WHERE (descriptionV6 LIKE '%projector%' OR category LIKE '%projector%'
           OR product_code LIKE '%PROJ%' OR product_code LIKE '%EB%')
    AND IsInTrashCan = 'N'
    ORDER BY product_code
""")
for row in cur.fetchall():
    print(f"  {row[0].strip()}: {row[4]} | Group: {row[1]} | Cat: {row[2]} | Price: ${row[6] or 0:.2f}")

print("\n=== Microphones ===")
cur.execute("""
    SELECT TOP 15 product_code, groupFld, category, SubCategory, descriptionV6, PrintedDesc, retail_price
    FROM tblInvmas 
    WHERE (descriptionV6 LIKE '%mic%' OR category LIKE '%mic%' OR product_code LIKE '%MIC%')
    AND IsInTrashCan = 'N'
    ORDER BY product_code
""")
for row in cur.fetchall():
    print(f"  {row[0].strip()}: {row[4]} | Group: {row[1]} | Cat: {row[2]} | Price: ${row[6] or 0:.2f}")

print("\n=== Screens ===")
cur.execute("""
    SELECT TOP 15 product_code, groupFld, category, SubCategory, descriptionV6, PrintedDesc, retail_price
    FROM tblInvmas 
    WHERE (descriptionV6 LIKE '%screen%' OR category LIKE '%screen%' OR product_code LIKE '%SCR%')
    AND IsInTrashCan = 'N'
    ORDER BY product_code
""")
for row in cur.fetchall():
    print(f"  {row[0].strip()}: {row[4]} | Group: {row[1]} | Cat: {row[2]} | Price: ${row[6] or 0:.2f}")

conn.close()
