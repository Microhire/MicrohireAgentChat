import pymssql

conn = pymssql.connect(
    server='116.90.5.144',
    port=41383,
    user='PowerBI-Consult',
    password='2tW@ostq3a3_9oV3m-TBQu3w',
    database='AITESTDB',
    as_dict=True
)
cursor = conn.cursor()

print("=== TESTING PROJECTOR SEARCH ===")
cursor.execute('''
    SELECT TOP(10) [t].[product_code], COALESCE([t].[descriptionv6], [t].[PrintedDesc]) AS [description], [t].[category], [t].[groupFld] AS [group]
    FROM [tblInvmas] AS [t]
    WHERE [t].[category] = 'PROJECTR'
    ORDER BY [t].[product_code]
''')

projectors = cursor.fetchall()
print(f"Found {len(projectors)} projectors:")
for p in projectors:
    print(f"  {p['product_code']:<15} | {p['description'][:50]}")

print("\n=== TESTING SPEAKER SEARCH ===")
cursor.execute('''
    SELECT TOP(10) [t].[product_code], COALESCE([t].[descriptionv6], [t].[PrintedDesc]) AS [description], [t].[category], [t].[groupFld] AS [group]
    FROM [tblInvmas] AS [t]
    WHERE [t].[category] = 'SPEAKER'
    ORDER BY [t].[product_code]
''')

speakers = cursor.fetchall()
print(f"Found {len(speakers)} speakers:")
for s in speakers:
    print(f"  {s['product_code']:<15} | {s['description'][:50]}")

conn.close()
