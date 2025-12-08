-- Quick check for recent bookings created by the chat system
-- Run this in SQL Server Management Studio or similar tool

USE AITESTDB;
GO

PRINT '=== RECENT BOOKINGS (Last 24 hours) ===';
SELECT TOP 5
    booking_no,
    FORMAT(SDate, 'yyyy-MM-dd') as EventDate,
    contact_nameV6 as Contact,
    OrganizationV6 as Organization,
    CAST(price_quoted as decimal(10,2)) as QuoteAmount,
    expAttendees as Attendees,
    FORMAT(order_date, 'yyyy-MM-dd HH:mm') as Created
FROM tblbookings
WHERE order_date >= DATEADD(DAY, -1, GETDATE())
ORDER BY order_date DESC;

PRINT CHAR(13) + CHAR(10) + '=== MICHAEL KNIGHT BOOKINGS ===';
SELECT
    booking_no,
    FORMAT(SDate, 'yyyy-MM-dd') as EventDate,
    contact_nameV6 as Contact,
    OrganizationV6 as Organization,
    CAST(price_quoted as decimal(10,2)) as QuoteAmount
FROM tblbookings
WHERE contact_nameV6 LIKE '%Michael%' OR contact_nameV6 LIKE '%Knight%'
ORDER BY ID DESC;

PRINT CHAR(13) + CHAR(10) + '=== YES 100 ATTENDEES BOOKINGS ===';
SELECT
    booking_no,
    FORMAT(SDate, 'yyyy-MM-dd') as EventDate,
    OrganizationV6 as Organization,
    contact_nameV6 as Contact
FROM tblbookings
WHERE OrganizationV6 LIKE '%Yes 100%' OR OrganizationV6 LIKE '%Attendees%'
ORDER BY ID DESC;

PRINT CHAR(13) + CHAR(10) + '=== CONTACT RECORDS ===';
SELECT TOP 3
    ID,
    Contactname,
    Email,
    FORMAT(CreateDate, 'yyyy-MM-dd HH:mm') as Created,
    FORMAT(LastUpdate, 'yyyy-MM-dd HH:mm') as LastUpdated
FROM tblContact
WHERE Email LIKE '%yes100attendees%' OR Contactname LIKE '%Michael%'
ORDER BY ID DESC;

PRINT CHAR(13) + CHAR(10) + '=== TRANSCRIPT RECORDS ===';
SELECT TOP 3
    BookingNo,
    NoteType,
    FORMAT(CreateDate, 'yyyy-MM-dd HH:mm') as Created
FROM tblbooknote
WHERE BookingNo LIKE '25%' AND NoteType = 1 -- 1 = transcript
ORDER BY CreateDate DESC;
