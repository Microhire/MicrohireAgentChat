-- Diagnose empty booking columns in RentalPoint
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\diagnose_bookings.sql

PRINT '=== 1. tblbookings sample ==='
SELECT TOP 3 ID, showName, SDate, rDate, showSdate, showEdate, status FROM tblbookings ORDER BY ID DESC;

PRINT ''
PRINT '=== 2. All views in DB ==='
SELECT name FROM sys.views ORDER BY name;

PRINT ''
PRINT '=== 3. Try vwBookingGrid ==='
BEGIN TRY
    EXEC('SELECT TOP 1 * FROM vwBookingGrid');
    PRINT 'vwBookingGrid OK';
END TRY
BEGIN CATCH
    PRINT 'vwBookingGrid ERROR: ' + ERROR_MESSAGE();
END CATCH

PRINT ''
PRINT '=== 4. Try vwCustomerGrid ==='
BEGIN TRY
    EXEC('SELECT TOP 1 * FROM vwCustomerGrid');
    PRINT 'vwCustomerGrid OK';
END TRY
BEGIN CATCH
    PRINT 'vwCustomerGrid ERROR: ' + ERROR_MESSAGE();
END CATCH

PRINT ''
PRINT '=== 5. Operator location ==='
SELECT ID, FirstName, LastName, DefaultLocation, SysAdmin FROM tblOperators WHERE ID = 260;

PRINT ''
PRINT '=== 6. Stored procs count ==='
SELECT COUNT(*) AS proc_count FROM sys.procedures;

PRINT ''
PRINT '=== 7. Functions count ==='
SELECT COUNT(*) AS func_count FROM sys.objects WHERE type IN ('FN','IF','TF');

PRINT ''
PRINT '=== 8. Check key tables row counts ==='
SELECT 'tblbookings' AS tbl, COUNT(*) AS cnt FROM tblbookings
UNION ALL SELECT 'tblItemtran', COUNT(*) FROM tblItemtran
UNION ALL SELECT 'tblProdstat', COUNT(*) FROM tblProdstat
UNION ALL SELECT 'tblCust', COUNT(*) FROM tblCust
UNION ALL SELECT 'tblHeading', COUNT(*) FROM tblHeading
UNION ALL SELECT 'tblLocnqty', COUNT(*) FROM tblLocnqty;
