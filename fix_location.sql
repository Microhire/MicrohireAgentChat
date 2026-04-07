-- Fix: Jenny's DefaultLocation=0 (Melbourne) but bookings are at location 20 (Westin Brisbane)
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\fix_location.sql

-- Option 1: Set Jenny to Westin Brisbane location (where all bookings are)
UPDATE tblOperators SET DefaultLocation = 20 WHERE ID = 260;
PRINT 'Updated Jenny DefaultLocation to 20 (Westin Brisbane)';

-- Verify bookings exist at location 20
SELECT TOP 5 ID, showName, SDate, From_locn, status FROM tblbookings WHERE From_locn = 20 ORDER BY ID DESC;

-- Count bookings per location
PRINT ''
PRINT '=== Bookings by location ==='
SELECT From_locn, COUNT(*) AS cnt FROM tblbookings GROUP BY From_locn ORDER BY cnt DESC;

-- Check if vwBookingGrid view exists and works
PRINT ''
PRINT '=== Testing vwBookingGrid ==='
BEGIN TRY
    DECLARE @c int;
    SELECT @c = COUNT(*) FROM vwBookingGrid;
    PRINT 'vwBookingGrid has ' + CAST(@c AS varchar) + ' rows - OK';
END TRY
BEGIN CATCH
    PRINT 'vwBookingGrid ERROR: ' + ERROR_MESSAGE();
END CATCH

PRINT ''
PRINT 'DONE - Restart RentalPoint completely (close and reopen hirepnt.exe)'
