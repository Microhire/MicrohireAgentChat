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

DECLARE @OrgIds TABLE (id DECIMAL(10,0));
INSERT INTO @OrgIds SELECT DISTINCT b.CustID FROM dbo.tblbookings b
  INNER JOIN @ContactIds c ON b.ContactID = c.id WHERE b.CustID IS NOT NULL;
INSERT INTO @OrgIds SELECT DISTINCT ID FROM dbo.tblcust WHERE iLink_ContactID IN (SELECT id FROM @ContactIds);
INSERT INTO @OrgIds SELECT DISTINCT c.ID FROM dbo.tblLinkCustContact l
  INNER JOIN dbo.tblcust c ON c.Customer_code IS NOT NULL AND l.Customer_Code IS NOT NULL AND l.Customer_Code = c.Customer_code
  WHERE l.ContactID IN (SELECT id FROM @ContactIds);

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

/* Organisations tied to this user: no remaining bookings, no contact links, safe primary */
DELETE c
FROM dbo.tblcust c
WHERE c.ID IN (SELECT id FROM @OrgIds)
  AND NOT EXISTS (SELECT 1 FROM dbo.tblbookings b WHERE b.CustID = c.ID)
  AND NOT EXISTS (
    SELECT 1 FROM dbo.tblLinkCustContact l
    WHERE c.Customer_code IS NOT NULL AND l.Customer_Code IS NOT NULL AND l.Customer_Code = c.Customer_code
  )
  AND (
    c.iLink_ContactID IN (SELECT id FROM @ContactIds)
    OR c.iLink_ContactID IS NULL
    OR c.iLink_ContactID = 0
  );

DELETE FROM dbo.tblContact WHERE ID IN (SELECT id FROM @ContactIds);

IF OBJECT_ID('dbo.AgentThreads','U') IS NOT NULL
  DELETE FROM dbo.AgentThreads WHERE LOWER(LTRIM(UserKey)) = LOWER(@TargetEmail);

COMMIT TRANSACTION;

PRINT 'AITESTDB: done for email in @TargetEmail';
