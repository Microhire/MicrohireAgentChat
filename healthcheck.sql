-- DB Health Check: compare local vs expected remote counts
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\healthcheck.sql

PRINT '=== LOCAL DB HEALTH CHECK ==='
PRINT ''

-- Object counts
PRINT '--- Object Counts (Local vs Remote) ---'
SELECT
  (SELECT COUNT(*) FROM sys.tables) AS [Local Tables],
  207 AS [Remote Tables],
  (SELECT COUNT(*) FROM sys.views) AS [Local Views],
  102 AS [Remote Views],
  (SELECT COUNT(*) FROM sys.procedures) AS [Local Procs],
  (SELECT COUNT(*) FROM sys.objects WHERE type IN ('FN','IF','TF')) AS [Local Funcs];

PRINT ''
PRINT '--- Key Table Row Counts (Local vs Remote) ---'
SELECT 'tblbookings' AS tbl, COUNT(*) AS [Local], 69370 AS [Remote] FROM tblbookings
UNION ALL SELECT 'tblItemtran', COUNT(*), 1829911 FROM tblItemtran
UNION ALL SELECT 'tblProdstat', COUNT(*), 710072 FROM tblProdstat
UNION ALL SELECT 'tblCust', COUNT(*), 14795 FROM tblCust
UNION ALL SELECT 'tblHeading', COUNT(*), 52990 FROM tblHeading
UNION ALL SELECT 'tblContact', COUNT(*), 21268 FROM tblContact
UNION ALL SELECT 'tblLocnlist', COUNT(*), 34 FROM tblLocnlist
UNION ALL SELECT 'tblLocnqty', COUNT(*), 86806 FROM tblLocnqty
UNION ALL SELECT 'tblOperators', COUNT(*), 260 FROM tblOperators
UNION ALL SELECT 'tblRatetbl', COUNT(*), 36800 FROM tblRatetbl
UNION ALL SELECT 'tblCrew', COUNT(*), 195841 FROM tblCrew;

PRINT ''
PRINT '--- vwBookingGrid Test ---'
BEGIN TRY
    DECLARE @c int;
    SELECT @c = COUNT(*) FROM vwBookingGrid;
    PRINT 'vwBookingGrid: ' + CAST(@c AS varchar) + ' rows - OK';
END TRY
BEGIN CATCH
    PRINT 'vwBookingGrid ERROR: ' + ERROR_MESSAGE();
END CATCH

PRINT ''
PRINT '=== DONE ==='
