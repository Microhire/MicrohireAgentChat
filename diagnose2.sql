-- Deep diagnostic for missing bookings
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\diagnose2.sql

PRINT '=== 1. Views count ==='
SELECT COUNT(*) AS view_count FROM sys.views;

PRINT ''
PRINT '=== 2. Booking-related views ==='
SELECT name FROM sys.views WHERE name LIKE '%book%' OR name LIKE '%Book%' ORDER BY name;

PRINT ''
PRINT '=== 3. Test vwBookingGrid ==='
BEGIN TRY
    DECLARE @c1 int;
    EXEC sp_executesql N'SELECT @c = COUNT(*) FROM vwBookingGrid', N'@c int OUTPUT', @c1 OUTPUT;
    PRINT 'vwBookingGrid rows: ' + CAST(@c1 AS varchar);
END TRY
BEGIN CATCH
    PRINT 'vwBookingGrid ERROR: ' + ERROR_MESSAGE();
END CATCH

PRINT ''
PRINT '=== 4. Test vwBookingGrid with location 20 ==='
BEGIN TRY
    DECLARE @c2 int;
    EXEC sp_executesql N'SELECT @c = COUNT(*) FROM vwBookingGrid WHERE Trans_to_locn = 20', N'@c int OUTPUT', @c2 OUTPUT;
    PRINT 'vwBookingGrid rows at locn 20: ' + CAST(@c2 AS varchar);
END TRY
BEGIN CATCH
    PRINT 'vwBookingGrid locn20 ERROR: ' + ERROR_MESSAGE();
END CATCH

PRINT ''
PRINT '=== 5. vwBookingGrid definition (first 4000 chars) ==='
SELECT LEFT(m.definition, 4000) AS def
FROM sys.views v
JOIN sys.sql_modules m ON v.object_id = m.object_id
WHERE v.name = 'vwBookingGrid';

PRINT ''
PRINT '=== 6. tblbookings raw data at location 20 ==='
SELECT TOP 5 ID, showName, SDate, rDate, From_locn, Trans_to_locn, status FROM tblbookings WHERE From_locn = 20 ORDER BY ID DESC;

PRINT ''
PRINT '=== 7. tblbookings ALL locations count ==='
SELECT From_locn, COUNT(*) AS cnt FROM tblbookings GROUP BY From_locn ORDER BY cnt DESC;

PRINT ''
PRINT '=== 8. Stored procs count ==='
SELECT COUNT(*) AS proc_count FROM sys.procedures;

PRINT ''
PRINT '=== 9. Functions count ==='
SELECT COUNT(*) AS func_count FROM sys.objects WHERE type IN ('FN','IF','TF');

PRINT ''
PRINT '=== 10. Check tblHeading (booking headers) ==='
SELECT TOP 3 * FROM tblHeading ORDER BY ID DESC;

PRINT ''
PRINT '=== 11. Check if tblCust has data ==='
SELECT COUNT(*) AS cust_count FROM tblCust;

PRINT ''
PRINT '=== 12. Operator permissions table ==='
SELECT name FROM sys.tables WHERE name LIKE '%perm%' OR name LIKE '%Perm%' OR name LIKE '%access%' OR name LIKE '%Access%' OR name LIKE '%right%' OR name LIKE '%Right%' OR name LIKE '%security%' OR name LIKE '%Security%' ORDER BY name;

PRINT ''
PRINT '=== 13. OperatorGroups ==='
SELECT name FROM sys.tables WHERE name LIKE '%OperatorGroup%' OR name LIKE '%opgroup%' ORDER BY name;
