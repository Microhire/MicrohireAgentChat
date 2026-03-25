/*
  Standalone template: replace @TargetEmail with the address to purge, then run with sqlcmd.

  Example (AITESTDB):
    sqlcmd -S tcp:HOST,PORT -d AITESTDB -U USER -P PASS -C -i Scripts/delete-email-data.sql

  Or use delete-email-data.sh which substitutes the email safely.
*/

SET NOCOUNT ON;

DECLARE @TargetEmail NVARCHAR(200) = N'nith@intent.do'; /* <-- edit */

BEGIN TRANSACTION;

DECLARE @ContactIds TABLE (id DECIMAL(10,0));
INSERT INTO @ContactIds
SELECT ID FROM dbo.tblContact WHERE LOWER(LTRIM(Email)) = LOWER(@TargetEmail);

/* Booking line items */
DELETE it
FROM dbo.tblitemtran it
WHERE it.booking_no_v32 IN (
    SELECT b.booking_no FROM dbo.tblbookings b WHERE b.ContactID IN (SELECT id FROM @ContactIds)
);

DELETE it
FROM dbo.tblitemtran it
WHERE EXISTS (
    SELECT 1 FROM dbo.tblbookings b
    WHERE b.ContactID IN (SELECT id FROM @ContactIds)
      AND b.ID IS NOT NULL AND it.booking_id = CAST(b.ID AS int)
);

DELETE FROM dbo.TblCrew
WHERE booking_no_v32 IN (
    SELECT booking_no FROM dbo.tblbookings WHERE ContactID IN (SELECT id FROM @ContactIds)
);

DELETE FROM dbo.tblbooknote
WHERE bookingNo IN (
    SELECT booking_no FROM dbo.tblbookings WHERE ContactID IN (SELECT id FROM @ContactIds)
);

DELETE FROM dbo.tblbookings WHERE ContactID IN (SELECT id FROM @ContactIds);

DELETE FROM dbo.tblLinkCustContact WHERE ContactID IN (SELECT id FROM @ContactIds);

/* Customers linked only to this contact and with no remaining bookings */
DELETE c
FROM dbo.tblcust c
WHERE c.iLink_ContactID IN (SELECT id FROM @ContactIds)
  AND NOT EXISTS (SELECT 1 FROM dbo.tblbookings b WHERE b.CustID = c.ID);

DELETE FROM dbo.tblContact WHERE ID IN (SELECT id FROM @ContactIds);

COMMIT TRANSACTION;

PRINT 'AITESTDB: done for email in @TargetEmail';
