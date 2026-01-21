#!/usr/bin/env python3
"""
Execute CreateAgentThreadsTable.sql on the production database
"""
import pymssql
import sys

# Connection parameters - Azure SQL Server (IntentTestDB for thread persistence)
SERVER = "intenttest.database.windows.net"
PORT = 1433
DATABASE = "IntentTestDB"
USERNAME = "azadmin"
PASSWORD = "Intent@2024!Secure"

# SQL Script
SQL_SCRIPT = """
-- Create AgentThreads table for persisting conversation threads
-- This allows conversations to resume after session loss, app restarts, or Azure scaling

IF OBJECT_ID('dbo.AgentThreads', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgentThreads (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserKey NVARCHAR(200) NOT NULL,
        ThreadId NVARCHAR(200) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        LastSeenUtc DATETIME2 NOT NULL
    );
    
    CREATE UNIQUE INDEX IX_AgentThreads_UserKey ON dbo.AgentThreads(UserKey);
    CREATE UNIQUE INDEX IX_AgentThreads_ThreadId ON dbo.AgentThreads(ThreadId);
    
    PRINT 'AgentThreads table created successfully.';
END
ELSE
BEGIN
    PRINT 'AgentThreads table already exists.';
END
"""

def main():
    print(f"Connecting to SQL Server: {SERVER}:{PORT}")
    print(f"Database: {DATABASE}")
    print(f"User: {USERNAME}")
    print("-" * 60)
    
    try:
        # Connect to SQL Server
        conn = pymssql.connect(
            server=SERVER,
            port=PORT,
            user=USERNAME,
            password=PASSWORD,
            database=DATABASE,
            timeout=30
        )
        
        print("✅ Connected successfully!")
        print()
        
        # Execute the SQL script
        cursor = conn.cursor()
        print("Executing SQL script...")
        print("-" * 60)
        
        # Split by GO statements and execute each batch
        batches = [batch.strip() for batch in SQL_SCRIPT.split('GO') if batch.strip()]
        
        for batch in batches:
            if batch:
                cursor.execute(batch)
        
        # Commit the transaction
        conn.commit()
        
        print("✅ SQL script executed successfully!")
        print()
        
        # Verify the table was created
        print("Verifying table creation...")
        cursor.execute("""
            SELECT 
                TABLE_NAME,
                TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = 'AgentThreads'
        """)
        
        result = cursor.fetchone()
        if result:
            print(f"✅ Table verified: {result[0]} ({result[1]})")
        else:
            print("⚠️  Warning: Table not found after creation")
        
        print()
        
        # Show indexes
        print("Checking indexes...")
        cursor.execute("""
            SELECT 
                i.name AS IndexName,
                i.is_unique AS IsUnique
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.name = 'AgentThreads'
            AND i.name IS NOT NULL
        """)
        
        indexes = cursor.fetchall()
        for idx in indexes:
            unique_str = "UNIQUE" if idx[1] else "NON-UNIQUE"
            print(f"  ✅ {idx[0]} ({unique_str})")
        
        print()
        print("=" * 60)
        print("✅ THREAD PERSISTENCE TABLE SETUP COMPLETE!")
        print("=" * 60)
        print()
        print("The application can now persist conversation threads.")
        print("Users' conversations will survive:")
        print("  - Session loss")
        print("  - App restarts")
        print("  - Azure scaling")
        print("  - Cookie clearing")
        
        # Close connection
        cursor.close()
        conn.close()
        
        return 0
        
    except pymssql.OperationalError as e:
        print(f"❌ Connection Error: {e}")
        print()
        print("Possible issues:")
        print("  1. SQL Server not accessible from this IP")
        print("  2. Firewall blocking port 41383")
        print("  3. SQL Server instance not running")
        print()
        print("Solutions:")
        print("  - Whitelist your IP in Azure SQL firewall")
        print("  - Check network connectivity")
        print("  - Verify SQL Server is running")
        return 1
        
    except Exception as e:
        print(f"❌ Error: {e}")
        return 1

if __name__ == "__main__":
    sys.exit(main())
