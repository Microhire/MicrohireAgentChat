-- Drop stored procs that reference msdb (not available in Azure SQL)
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\Users\root\fix_for_azure.sql

-- Find and drop all procs that reference msdb
DECLARE @name NVARCHAR(128);
DECLARE @sql NVARCHAR(MAX);

DECLARE proc_cursor CURSOR FOR
SELECT p.name
FROM sys.procedures p
JOIN sys.sql_modules m ON p.object_id = m.object_id
WHERE m.definition LIKE '%msdb%';

OPEN proc_cursor;
FETCH NEXT FROM proc_cursor INTO @name;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = 'DROP PROCEDURE IF EXISTS [' + @name + ']';
    PRINT 'Dropping: ' + @name;
    EXEC sp_executesql @sql;
    FETCH NEXT FROM proc_cursor INTO @name;
END

CLOSE proc_cursor;
DEALLOCATE proc_cursor;

PRINT 'Done - all msdb-referencing procs dropped.';
