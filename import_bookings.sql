-- Import tblItemtran and tblProdstat (booking line items) from remote AITESTDB
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\import_bookings.sql
-- This will take a while (2.5M total rows) - do NOT close the window

PRINT '=== Starting booking data import ==='
PRINT 'Time: ' + CONVERT(varchar, GETDATE(), 120)

-- Disable FK constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'
PRINT 'FK constraints disabled'

-- ============================================
-- tblItemtran (1.8M rows - booking line items)
-- ============================================
PRINT ''
PRINT '>>> Importing tblItemtran (1.8M rows) - this will take several minutes...'
PRINT 'Start: ' + CONVERT(varchar, GETDATE(), 120)

SET IDENTITY_INSERT tblItemtran ON;

INSERT INTO tblItemtran ([ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [trans_type_v41], [product_code_v42], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [price], [item_type], [days_using], [sub_hire_qtyV61], [From_locn], [Trans_to_locn], [return_to_locn], [bit_field_v41], [TimeBookedH], [TimeBookedM], [TimeBookedS], [QtyReturned], [QtyCheckedOut], [techRateorDaysCharged], [TechPay], [unitRate], [prep_on], [Comment_desc_v42], [AssignTo], [FirstDate], [RetnDate], [BookDate], [PDate], [PTimeH], [PTimeM], [DayWeekRate], [QtyReserved], [AddedAtCheckout], [GroupSeqNo], [SubRentalLinkID], [AssignType], [QtyShort], [QtyAvailable], [PackageLevel], [BeforeDiscountAmount], [QuickTurnAroundQty], [InRack], [CostPrice], [NodeCollapsed], [AvailRecFlag], [booking_id], [Undisc_amt], [View_Logi], [View_client], [Logi_HeadingNo], [Logi_GroupSeqNo], [Logi_Seq_No], [Logi_Sub_Seq_no], [ParentCode], [EstSubRentalCost], [EstSubRentalDays], [VendorID], [Notes], [UseEstSubHireOverride], [Estimated_sub_hire_v5], [resolvedDiscrep], [QTBookingNo], [QTSource], [warehouseMutedPerOER], [techrateIsHourorDay], [OriginalBookNo])
SELECT [ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [trans_type_v41], [product_code_v42], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [price], [item_type], [days_using], [sub_hire_qtyV61], [From_locn], [Trans_to_locn], [return_to_locn], [bit_field_v41], [TimeBookedH], [TimeBookedM], [TimeBookedS], [QtyReturned], [QtyCheckedOut], [techRateorDaysCharged], [TechPay], [unitRate], [prep_on], [Comment_desc_v42], [AssignTo], [FirstDate], [RetnDate], [BookDate], [PDate], [PTimeH], [PTimeM], [DayWeekRate], [QtyReserved], [AddedAtCheckout], [GroupSeqNo], [SubRentalLinkID], [AssignType], [QtyShort], [QtyAvailable], [PackageLevel], [BeforeDiscountAmount], [QuickTurnAroundQty], [InRack], [CostPrice], [NodeCollapsed], [AvailRecFlag], [booking_id], [Undisc_amt], [View_Logi], [View_client], [Logi_HeadingNo], [Logi_GroupSeqNo], [Logi_Seq_No], [Logi_Sub_Seq_no], [ParentCode], [EstSubRentalCost], [EstSubRentalDays], [VendorID], [Notes], [UseEstSubHireOverride], [Estimated_sub_hire_v5], [resolvedDiscrep], [QTBookingNo], [QTSource], [warehouseMutedPerOER], [techrateIsHourorDay], [OriginalBookNo]
FROM OPENROWSET(
    'MSOLEDBSQL',
    'Server=116.90.5.144\SQLEXPRESS,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
    'SELECT [ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [trans_type_v41], [product_code_v42], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [price], [item_type], [days_using], [sub_hire_qtyV61], [From_locn], [Trans_to_locn], [return_to_locn], [bit_field_v41], [TimeBookedH], [TimeBookedM], [TimeBookedS], [QtyReturned], [QtyCheckedOut], [techRateorDaysCharged], [TechPay], [unitRate], [prep_on], [Comment_desc_v42], [AssignTo], [FirstDate], [RetnDate], [BookDate], [PDate], [PTimeH], [PTimeM], [DayWeekRate], [QtyReserved], [AddedAtCheckout], [GroupSeqNo], [SubRentalLinkID], [AssignType], [QtyShort], [QtyAvailable], [PackageLevel], [BeforeDiscountAmount], [QuickTurnAroundQty], [InRack], [CostPrice], [NodeCollapsed], [AvailRecFlag], [booking_id], [Undisc_amt], [View_Logi], [View_client], [Logi_HeadingNo], [Logi_GroupSeqNo], [Logi_Seq_No], [Logi_Sub_Seq_no], [ParentCode], [EstSubRentalCost], [EstSubRentalDays], [VendorID], [Notes], [UseEstSubHireOverride], [Estimated_sub_hire_v5], [resolvedDiscrep], [QTBookingNo], [QTSource], [warehouseMutedPerOER], [techrateIsHourorDay], [OriginalBookNo] FROM tblItemtran'
);

SET IDENTITY_INSERT tblItemtran OFF;

PRINT 'tblItemtran DONE: ' + CONVERT(varchar, GETDATE(), 120)

-- ============================================
-- tblProdstat (710K rows - product status)
-- ============================================
PRINT ''
PRINT '>>> Importing tblProdstat (710K rows)...'
PRINT 'Start: ' + CONVERT(varchar, GETDATE(), 120)

SET IDENTITY_INSERT tblProdstat ON;

INSERT INTO tblProdstat ([ID], [product_code], [quantity], [DateF], [avail], [Location])
SELECT [ID], [product_code], [quantity], [DateF], [avail], [Location]
FROM OPENROWSET(
    'MSOLEDBSQL',
    'Server=116.90.5.144\SQLEXPRESS,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
    'SELECT [ID], [product_code], [quantity], [DateF], [avail], [Location] FROM tblProdstat'
);

SET IDENTITY_INSERT tblProdstat OFF;

PRINT 'tblProdstat DONE: ' + CONVERT(varchar, GETDATE(), 120)

-- Re-enable FK constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'
PRINT ''
PRINT 'FK constraints re-enabled'

-- Verify
PRINT ''
PRINT '=== VERIFICATION ==='
SELECT 'tblbookings' AS tbl, COUNT(*) AS cnt FROM tblbookings
UNION ALL SELECT 'tblItemtran', COUNT(*) FROM tblItemtran
UNION ALL SELECT 'tblProdstat', COUNT(*) FROM tblProdstat;

PRINT ''
PRINT '=== ALL DONE ==='
PRINT 'Time: ' + CONVERT(varchar, GETDATE(), 120)
PRINT 'Close this window and refresh RentalPoint (click Refresh button) to see bookings.'
