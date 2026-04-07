-- Fix: Drop fake "view tables" so DatabaseWizard can create real views
-- Run on VM: sqlcmd -S .\SQLEXPRESS -E -C -d AITESTDB -i C:\fix_views.sql

PRINT '=== Dropping 102 tables that should be views ==='

DROP TABLE IF EXISTS [vwActivityInformation];
DROP TABLE IF EXISTS [vwAllVenues];
DROP TABLE IF EXISTS [vwArchivedInventory];
DROP TABLE IF EXISTS [vwAssetPlusParent];
DROP TABLE IF EXISTS [vwAssetsOutForService];
DROP TABLE IF EXISTS [vwAssetsOutForServicePAT];
DROP TABLE IF EXISTS [vwAssignedPOList];
DROP TABLE IF EXISTS [vwAudit];
DROP TABLE IF EXISTS [vwBookAndHist];
DROP TABLE IF EXISTS [vwBookAndHistAllfieldsV11_0_0_0];
DROP TABLE IF EXISTS [vwBookingGrid];
DROP TABLE IF EXISTS [vwBookingGrid_v2];
DROP TABLE IF EXISTS [vwBookingGridHint];
DROP TABLE IF EXISTS [vwBookingsToBeReinvoiced];
DROP TABLE IF EXISTS [vwCarnetBookingItems];
DROP TABLE IF EXISTS [vwCarnetItems];
DROP TABLE IF EXISTS [vwCheckAvail];
DROP TABLE IF EXISTS [vwCheckAvailIDXV2];
DROP TABLE IF EXISTS [vwComments];
DROP TABLE IF EXISTS [vwContactManageGrid];
DROP TABLE IF EXISTS [vwContactPhones];
DROP TABLE IF EXISTS [vwCrewAndHist];
DROP TABLE IF EXISTS [vwCrewItems];
DROP TABLE IF EXISTS [vwCrewSplitByDate];
DROP TABLE IF EXISTS [vwCrossRentalList];
DROP TABLE IF EXISTS [vwCurrentAndDeletedAssets];
DROP TABLE IF EXISTS [vwCurrentAssets];
DROP TABLE IF EXISTS [vwCustAndVend];
DROP TABLE IF EXISTS [vwCustomer];
DROP TABLE IF EXISTS [vwCustomerGrid];
DROP TABLE IF EXISTS [vwCustomerPhones];
DROP TABLE IF EXISTS [vwDocuSign];
DROP TABLE IF EXISTS [vwDocuSignDS];
DROP TABLE IF EXISTS [vwDocuSignRP];
DROP TABLE IF EXISTS [vwDuplicateBarcodes];
DROP TABLE IF EXISTS [vwGroupEmails];
DROP TABLE IF EXISTS [vwGroupEmailsProducts];
DROP TABLE IF EXISTS [vwGroupTotals];
DROP TABLE IF EXISTS [vwHeadingSubTotals];
DROP TABLE IF EXISTS [vwHistBookGrid];
DROP TABLE IF EXISTS [vwHistBookGrid_v2];
DROP TABLE IF EXISTS [vwHistCrewItems];
DROP TABLE IF EXISTS [vwInsuranceCoverage];
DROP TABLE IF EXISTS [vwInsuredBookings];
DROP TABLE IF EXISTS [vwInvCommentItems];
DROP TABLE IF EXISTS [vwInventoryItems];
DROP TABLE IF EXISTS [vwItemAndHist];
DROP TABLE IF EXISTS [vwLoadSubRental];
DROP TABLE IF EXISTS [vwLoadSubRental_v2];
DROP TABLE IF EXISTS [vwLoadSubRentalHist];
DROP TABLE IF EXISTS [vwLoadTruckSchedule];
DROP TABLE IF EXISTS [vwLocnQtyV2];
DROP TABLE IF EXISTS [vwMinPOAmount];
DROP TABLE IF EXISTS [vwOpenBooking_v2];
DROP TABLE IF EXISTS [vwOpenBookingRPMVC];
DROP TABLE IF EXISTS [vwOrphSubrentals];
DROP TABLE IF EXISTS [vwPlotCrewActivities];
DROP TABLE IF EXISTS [vwPlotCrewItems];
DROP TABLE IF EXISTS [vwPlotCrewItems_v2];
DROP TABLE IF EXISTS [vwPlotItems];
DROP TABLE IF EXISTS [vwPlotItems_v2];
DROP TABLE IF EXISTS [vwPlotItemsPAT];
DROP TABLE IF EXISTS [vwPlotItemsPAT_v2];
DROP TABLE IF EXISTS [vwPOApproval];
DROP TABLE IF EXISTS [vwPOItems];
DROP TABLE IF EXISTS [vwPOLine];
DROP TABLE IF EXISTS [vwPrepDeprepBookings];
DROP TABLE IF EXISTS [vwPriceOverrideReport_NoBookingOverrides];
DROP TABLE IF EXISTS [vwPriceOverrideReport_WithBookingOverrides];
DROP TABLE IF EXISTS [vwProdsComponents];
DROP TABLE IF EXISTS [vwProdsInGroup];
DROP TABLE IF EXISTS [vwQuoteComments];
DROP TABLE IF EXISTS [vwQuoteCrew];
DROP TABLE IF EXISTS [vwQuoteCrew_SC];
DROP TABLE IF EXISTS [vwQuoteRentalEquipV2];
DROP TABLE IF EXISTS [vwQuoteRentalEquipV3];
DROP TABLE IF EXISTS [vwQuoteRentalEquipV3_SC];
DROP TABLE IF EXISTS [vwQuoteUSundryItems_V2];
DROP TABLE IF EXISTS [vwRackedAssets];
DROP TABLE IF EXISTS [vwReadInventory];
DROP TABLE IF EXISTS [vwRegionLocations];
DROP TABLE IF EXISTS [vwRentalSalesSundry_HeadingTotals];
DROP TABLE IF EXISTS [vwReportBooking_SC];
DROP TABLE IF EXISTS [vwReportBooking_v1];
DROP TABLE IF EXISTS [vwRevenueRep];
DROP TABLE IF EXISTS [vwRoadcase];
DROP TABLE IF EXISTS [vwRoadcasePickList];
DROP TABLE IF EXISTS [vwRoadcases];
DROP TABLE IF EXISTS [vwRPWebContact];
DROP TABLE IF EXISTS [vwScheduleGridByBooking];
DROP TABLE IF EXISTS [vwScheduleGridByHeading];
DROP TABLE IF EXISTS [vwShortCrew];
DROP TABLE IF EXISTS [vwShortCrewActivities];
DROP TABLE IF EXISTS [vwShortCrewBooking];
DROP TABLE IF EXISTS [vwShortCrewItems];
DROP TABLE IF EXISTS [vwShortCrewNames];
DROP TABLE IF EXISTS [vwSundryAndHist];
DROP TABLE IF EXISTS [vwTechnicianLabour];
DROP TABLE IF EXISTS [vwTechnicianPO];
DROP TABLE IF EXISTS [vwVendors];
DROP TABLE IF EXISTS [vwVendorsAndCust];
DROP TABLE IF EXISTS [vwWebPayData];

-- Also drop any existing (broken) views with these names
DROP VIEW IF EXISTS [vwBookingGrid];
DROP VIEW IF EXISTS [vwCustomerGrid];

PRINT '=== Done dropping fake tables ==='
PRINT ''
PRINT 'View count before: should be very low'
SELECT COUNT(*) AS views_remaining FROM sys.views;
PRINT ''
PRINT 'Table count:'
SELECT COUNT(*) AS tables_remaining FROM sys.tables;
PRINT ''
PRINT '*** NOW RUN DatabaseWizard.exe with JENNY JUNKEER / jj ***'
PRINT '*** It will recreate all 102 views properly ***'
