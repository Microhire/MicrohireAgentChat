-- Fix NOT NULL columns in tblbookings that block new booking creation
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\fix_nulls.sql

-- Add default values for all NOT NULL columns without defaults
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_ProjectManager DEFAULT '' FOR ProjectManager;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_dtExpReturnDate DEFAULT GETDATE() FOR dtExpected_ReturnDate;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_vcExpReturnTime DEFAULT '' FOR vcExpected_ReturnTime;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_vcTruckOutTime DEFAULT '' FOR vcTruckOutTime;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_vcTruckInTime DEFAULT '' FOR vcTruckInTime;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_CustID DEFAULT 0 FOR CustID;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_VenueID DEFAULT 0 FOR VenueID;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_LateCharges DEFAULT 0 FOR LateChargesApplied;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_shortagesTransf DEFAULT 0 FOR shortagesAreTransfered;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_DeprepOn DEFAULT 0 FOR DeprepOn;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_DeliveryDateOn DEFAULT 0 FOR DeliveryDateOn;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_PickupDateOn DEFAULT 0 FOR PickupDateOn;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_TaxabPCT DEFAULT 0 FOR TaxabPCT;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_UntaxPCT DEFAULT 0 FOR UntaxPCT;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_Tax1PCT DEFAULT 0 FOR Tax1PCT;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_Tax2PCT DEFAULT 0 FOR Tax2PCT;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_crew_cnt DEFAULT 0 FOR crew_cnt;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_rTargetMargin DEFAULT 0 FOR rTargetMargin;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_rProfitMargin DEFAULT 0 FOR rProfitMargin;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_SyncType DEFAULT 0 FOR SyncType;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_HasQT DEFAULT 0 FOR HasQT;
ALTER TABLE tblbookings ADD CONSTRAINT DF_bookings_HasDAT DEFAULT 0 FOR HasDAT;

PRINT 'All default constraints added to tblbookings - booking creation should work now.'
