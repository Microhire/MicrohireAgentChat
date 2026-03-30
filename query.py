import pyodbc
from time import time

conn_str = (
    r"DRIVER={ODBC Driver 18 for SQL Server};"
    r"SERVER=116.90.5.144,41383;"
    r"DATABASE=AITESTDB;"
    r"UID=PowerBI-Consult;"
    r"PWD=2tW@ostq3a3_9oV3m-TBQu3w;"
    r"TrustServerCertificate=yes;"
)

try:
    print("Connecting...")
    start = time()
    conn = pyodbc.connect(conn_str, timeout=10)
    print(f"Connected in {time() - start:.2f}s")
    
    cursor = conn.cursor()
    cursor.execute("SELECT TOP 5 RTRIM(product_code), RTRIM(descriptionV6) FROM tblinvmas")
    for row in cursor:
        print(row)
    
except Exception as e:
    print("Error:", e)
