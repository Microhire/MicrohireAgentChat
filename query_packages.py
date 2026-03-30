import pymssql

codes = [
    'WSB','WSBAX','WSBVX','WSBAV','FP PACKS','PROMOAV',
    'WSBELEV','ELEVIND','ELEVSCSS','ELEVPROJ','ELEVAVP','ELEVSAVP','ELEVCSS',
    'WSBBALL','WBSBCSS','WBDPROJ','WBSPROJ','WBSNPROJ','WBSSPROJ','WBSAVP','WBAVP','WBFBCSS','WBIND',
    'WSBTHRV','THRVPROJ','THRVAVP','THRVCSS','THRVIND',
    'CYB'
]

try:
    print("Connecting to AITESTDB...")
    conn = pymssql.connect(
        server='116.90.5.144',
        port=41383,
        user='PowerBI-Consult',
        password='2tW@ostq3a3_9oV3m-TBQu3w',
        database='AITESTDB'
    )
    print("Connected!")
    cursor = conn.cursor()

    placeholders = ','.join(["'%s'" % c for c in codes])
    sql = f"""
    SELECT 
      RTRIM(LTRIM(product_code)) AS product_code, 
      RTRIM(LTRIM(descriptionV6)) AS description,
      RTRIM(LTRIM(category)) AS category,
      RTRIM(LTRIM(groupFld)) AS groupFld,
      product_Config,
      product_type_v41
    FROM tblinvmas 
    WHERE RTRIM(LTRIM(product_code)) IN ({placeholders})
    ORDER BY product_code
    """
    cursor.execute(sql)
    
    print(f"\n{'CODE':<12} {'DESCRIPTION':<55} {'CATEGORY':<12} {'GROUP':<12} {'CONFIG':<8} {'TYPE'}")
    print("-" * 155)
    
    found = []
    for row in cursor:
        code, desc, cat, grp, cfg, typ = row
        found.append(code)
        print(f"{(code or ''):<12} {(desc or ''):<55} {(cat or ''):<12} {(grp or ''):<12} {(str(cfg) if cfg is not None else ''):<8} {typ if typ is not None else ''}")
    
    missing = [c for c in codes if c not in found]
    if missing:
        print(f"\nNOT FOUND in DB: {missing}")
    
    print(f"\nTotal found: {len(found)} / {len(codes)}")
    conn.close()

except Exception as e:
    print(f"Error: {e}")
