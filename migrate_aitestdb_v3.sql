USE AITESTDB;
GO
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO
EXEC sp_MSforeachtable 'ALTER TABLE ? DISABLE TRIGGER ALL';
GO

IF (SELECT COUNT(*) FROM [tblActivityResult]) = 0
BEGIN
    PRINT 'Importing tblActivityResult (1 rows)...';
    SET IDENTITY_INSERT [tblActivityResult] ON;
    INSERT INTO [tblActivityResult] ([ID], [ActivityResult], [FirstActivityID], [SecondActivityID], [ThirdActivityID], [FirstActive], [SecondActive], [ThirdActive], [FirstDays], [SecondDays], [ThirdDays], [FirstHours], [SecondHours], [ThirdHours])
    SELECT [ID], [ActivityResult], [FirstActivityID], [SecondActivityID], [ThirdActivityID], [FirstActive], [SecondActive], [ThirdActive], [FirstDays], [SecondDays], [ThirdDays], [FirstHours], [SecondHours], [ThirdHours]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblActivityResult]');
    SET IDENTITY_INSERT [tblActivityResult] OFF;
END
ELSE PRINT 'Skipping tblActivityResult (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblBatch]) = 0
BEGIN
    PRINT 'Importing tblBatch (1 rows)...';
    INSERT INTO [tblBatch] ([CashReceiptBatchNo])
    SELECT [CashReceiptBatchNo]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblBatch]');
END
ELSE PRINT 'Skipping tblBatch (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCreditNoteNumber]) = 0
BEGIN
    PRINT 'Importing tblCreditNoteNumber (1 rows)...';
    SET IDENTITY_INSERT [tblCreditNoteNumber] ON;
    INSERT INTO [tblCreditNoteNumber] ([ID], [NextCreditNoteNumber])
    SELECT [ID], [NextCreditNoteNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCreditNoteNumber]');
    SET IDENTITY_INSERT [tblCreditNoteNumber] OFF;
END
ELSE PRINT 'Skipping tblCreditNoteNumber (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblDBVersion]) = 0
BEGIN
    PRINT 'Importing tblDBVersion (1 rows)...';
    SET IDENTITY_INSERT [tblDBVersion] ON;
    INSERT INTO [tblDBVersion] ([ID], [VersionNumber])
    SELECT [ID], [VersionNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblDBVersion]');
    SET IDENTITY_INSERT [tblDBVersion] OFF;
END
ELSE PRINT 'Skipping tblDBVersion (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblGLAccounts]) = 0
BEGIN
    PRINT 'Importing tblGLAccounts (1 rows)...';
    SET IDENTITY_INSERT [tblGLAccounts] ON;
    INSERT INTO [tblGLAccounts] ([ID], [LocationNum], [RentalAcctNum], [SaleAcctNum], [LossAcctNum], [DeliveryAcctNum], [LabourAcctNum], [SundryAcctNum], [InsuranceAcctNum], [StampAcctNum], [VatHoldAcctNum], [VatAcctNum], [SalesTaxAcctNum], [BankAcctNum], [ControlAcctNum], [SaleOfAssetAcctNum], [DiscountAcctNum], [AccPacSource], [ProdIncomeAcctNum], [Exp_To_v71], [SageExportPath], [accpac_integration_level], [invoice_exp_filename], [credit_exp_filename], [ExportBatch_Number], [ExportPay_Number], [cust_exp_filenameV2], [sage_breakdown], [sa_nominal_ac], [sa_dept_code], [sa_tax_code], [sybiz_period_number], [sybiz_sales_an_code], [state_sales_tax_authority], [county_tax_authority], [UseLongYear], [import_auto], [accpac_path], [accpac_filename], [accpac_param], [Is_Peachtree_LineExp], [Def_Peach_RevCode], [accpac_terms_code_cash], [accpac_terms_code_7], [accpac_terms_code_30], [accpac_applic], [myobTax1], [myobTax2], [myobDisc], [myobCharge], [stampduty_tax_Authority], [ExtraTaxCode], [ExportQBAccountNum], [ExportQBDiscounts], [CreditSurchargeAccNum], [SpecialPrefixAccount], [EventManagementAccNum], [QBOnlineFormat], [QBSandboxID], [QBSandboxSecret], [QBProductionID], [QBProductionSecret], [QBTaxNumber])
    SELECT [ID], [LocationNum], [RentalAcctNum], [SaleAcctNum], [LossAcctNum], [DeliveryAcctNum], [LabourAcctNum], [SundryAcctNum], [InsuranceAcctNum], [StampAcctNum], [VatHoldAcctNum], [VatAcctNum], [SalesTaxAcctNum], [BankAcctNum], [ControlAcctNum], [SaleOfAssetAcctNum], [DiscountAcctNum], [AccPacSource], [ProdIncomeAcctNum], [Exp_To_v71], [SageExportPath], [accpac_integration_level], [invoice_exp_filename], [credit_exp_filename], [ExportBatch_Number], [ExportPay_Number], [cust_exp_filenameV2], [sage_breakdown], [sa_nominal_ac], [sa_dept_code], [sa_tax_code], [sybiz_period_number], [sybiz_sales_an_code], [state_sales_tax_authority], [county_tax_authority], [UseLongYear], [import_auto], [accpac_path], [accpac_filename], [accpac_param], [Is_Peachtree_LineExp], [Def_Peach_RevCode], [accpac_terms_code_cash], [accpac_terms_code_7], [accpac_terms_code_30], [accpac_applic], [myobTax1], [myobTax2], [myobDisc], [myobCharge], [stampduty_tax_Authority], [ExtraTaxCode], [ExportQBAccountNum], [ExportQBDiscounts], [CreditSurchargeAccNum], [SpecialPrefixAccount], [EventManagementAccNum], [QBOnlineFormat], [QBSandboxID], [QBSandboxSecret], [QBProductionID], [QBProductionSecret], [QBTaxNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblGLAccounts]');
    SET IDENTITY_INSERT [tblGLAccounts] OFF;
END
ELSE PRINT 'Skipping tblGLAccounts (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblHistbks]) = 0
BEGIN
    PRINT 'Importing tblHistbks (1 rows)...';
    SET IDENTITY_INSERT [tblHistbks] ON;
    INSERT INTO [tblHistbks] ([ID], [booking_no], [order_no], [payment_type], [deposit_quoted_v50], [price_quoted], [docs_produced], [hire_price], [booking_type_v32], [status], [delivery], [percent_disc], [delivery_viav71], [delivery_time], [pickup_viaV71], [pickup_time], [invoiced], [labour], [invoice_no], [event_code], [discount_rate], [same_address], [insurance_v5], [days_using], [un_disc_amount], [del_time_h], [del_time_m], [ret_time_h], [ret_time_m], [Item_cnt], [sales_discount_rate], [sales_amount], [tax1], [division], [contact_nameV6], [sales_tax_no], [last_modified_by], [delivery_address_exist], [sales_percent_disc], [pricing_scheme_used], [days_charged_v51], [sale_of_asset], [From_locn], [return_to_locn], [retail_value], [perm_casual], [setupTimeV61], [RehearsalTime], [StrikeTime], [Trans_to_locn], [showStartTime], [ShowEndTime], [transferNo], [currencyStr], [BookingProgressStatus], [ConfirmedBy], [ConfirmedDocRef], [VenueRoom], [expAttendees], [HourBooked], [MinBooked], [SecBooked], [TaxAuthority1], [TaxAuthority2], [HorCCroom], [subrooms], [truckOut], [truckIn], [tripOut], [tripIn], [showName], [freightServiceDel], [freightServiceRet], [DelZone], [RetZone], [OurNumberDel], [OurNumberRet], [DatesAndTimesEnabled], [Government], [prep_time_h], [prep_entered], [prep_time_m], [sales_undisc_amount], [losses], [half_day_aplic], [ContactLoadedIntoVenue], [Assigned_to_v61], [sundry_total], [OrganizationV6], [Salesperson], [order_date], [dDate], [rDate], [Inv_date], [ShowSdate], [ShowEdate], [SetDate], [ADelDate], [SDate], [RehDate], [ConDate], [TOutDate], [TInDate], [PreDate], [ConByDate], [bookingPrinted], [CustCode], [ExtendedFrom], [last_operators], [operatorsID], [PotPercent], [Referral], [EventType], [Priority], [InvoiceStage], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardHolder], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [PickupRetDate], [rent_invd_too_date], [MaxBookingValue], [UsesPriceTable], [DateToInvoice], [TwoWkDisc], [ThreeWkDisc], [ServCont], [RentalType], [PrintedPayTerm], [PaymentOptions], [UseBillSchedule], [Tax2], [ContactID], [ShortHours], [ProjectManager], [dtExpected_ReturnDate], [vcExpected_ReturnTime], [vcTruckOutTime], [vcTruckInTime], [CustID], [VenueID], [LateChargesApplied], [shortagesAreTransfered], [VenueContactID], [VenueContact], [VenueContactPhoneID], [LTBillingOption], [DressCode], [Collection], [FuelSurchargeRate], [FreightLocked], [LabourLocked], [RentalLocked], [PriceLocked], [insurance_type], [EntryDate], [CreditSurchargeRate], [CreditSurchargeAmount], [DisableTreeOrder], [ConfirmationFinancials], [EventManagementRate], [EventManagementAmount], [EquipmentModified], [CrewStatusColumn], [LoadDateTime], [UnloadDateTime], [DeprepDateTime], [DeprepOn], [DeliveryDateOn], [PickupDateOn], [ScheduleDatesOn], [bBookingIsComplete], [DiscountOverride], [MasterBillingID], [MasterBillingMethod], [schedHeadEquipSpan], [TaxabPCT], [UntaxPCT], [Tax1PCT], [Tax2PCT], [PaymentContactID], [sale_of_asset_undisc_amt], [LockedForScanning], [OldAssignedTo], [DateLastModified], [crew_cnt], [rTargetMargin], [rProfitMargin], [ContractNo], [SyncType], [AllLocnAvail], [HasQT], [HasDAT], [AllHeadingsDaysOverride], [BayNo], [Paymethod])
    SELECT [ID], [booking_no], [order_no], [payment_type], [deposit_quoted_v50], [price_quoted], [docs_produced], [hire_price], [booking_type_v32], [status], [delivery], [percent_disc], [delivery_viav71], [delivery_time], [pickup_viaV71], [pickup_time], [invoiced], [labour], [invoice_no], [event_code], [discount_rate], [same_address], [insurance_v5], [days_using], [un_disc_amount], [del_time_h], [del_time_m], [ret_time_h], [ret_time_m], [Item_cnt], [sales_discount_rate], [sales_amount], [tax1], [division], [contact_nameV6], [sales_tax_no], [last_modified_by], [delivery_address_exist], [sales_percent_disc], [pricing_scheme_used], [days_charged_v51], [sale_of_asset], [From_locn], [return_to_locn], [retail_value], [perm_casual], [setupTimeV61], [RehearsalTime], [StrikeTime], [Trans_to_locn], [showStartTime], [ShowEndTime], [transferNo], [currencyStr], [BookingProgressStatus], [ConfirmedBy], [ConfirmedDocRef], [VenueRoom], [expAttendees], [HourBooked], [MinBooked], [SecBooked], [TaxAuthority1], [TaxAuthority2], [HorCCroom], [subrooms], [truckOut], [truckIn], [tripOut], [tripIn], [showName], [freightServiceDel], [freightServiceRet], [DelZone], [RetZone], [OurNumberDel], [OurNumberRet], [DatesAndTimesEnabled], [Government], [prep_time_h], [prep_entered], [prep_time_m], [sales_undisc_amount], [losses], [half_day_aplic], [ContactLoadedIntoVenue], [Assigned_to_v61], [sundry_total], [OrganizationV6], [Salesperson], [order_date], [dDate], [rDate], [Inv_date], [ShowSdate], [ShowEdate], [SetDate], [ADelDate], [SDate], [RehDate], [ConDate], [TOutDate], [TInDate], [PreDate], [ConByDate], [bookingPrinted], [CustCode], [ExtendedFrom], [last_operators], [operatorsID], [PotPercent], [Referral], [EventType], [Priority], [InvoiceStage], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardHolder], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [PickupRetDate], [rent_invd_too_date], [MaxBookingValue], [UsesPriceTable], [DateToInvoice], [TwoWkDisc], [ThreeWkDisc], [ServCont], [RentalType], [PrintedPayTerm], [PaymentOptions], [UseBillSchedule], [Tax2], [ContactID], [ShortHours], [ProjectManager], [dtExpected_ReturnDate], [vcExpected_ReturnTime], [vcTruckOutTime], [vcTruckInTime], [CustID], [VenueID], [LateChargesApplied], [shortagesAreTransfered], [VenueContactID], [VenueContact], [VenueContactPhoneID], [LTBillingOption], [DressCode], [Collection], [FuelSurchargeRate], [FreightLocked], [LabourLocked], [RentalLocked], [PriceLocked], [insurance_type], [EntryDate], [CreditSurchargeRate], [CreditSurchargeAmount], [DisableTreeOrder], [ConfirmationFinancials], [EventManagementRate], [EventManagementAmount], [EquipmentModified], [CrewStatusColumn], [LoadDateTime], [UnloadDateTime], [DeprepDateTime], [DeprepOn], [DeliveryDateOn], [PickupDateOn], [ScheduleDatesOn], [bBookingIsComplete], [DiscountOverride], [MasterBillingID], [MasterBillingMethod], [schedHeadEquipSpan], [TaxabPCT], [UntaxPCT], [Tax1PCT], [Tax2PCT], [PaymentContactID], [sale_of_asset_undisc_amt], [LockedForScanning], [OldAssignedTo], [DateLastModified], [crew_cnt], [rTargetMargin], [rProfitMargin], [ContractNo], [SyncType], [AllLocnAvail], [HasQT], [HasDAT], [AllHeadingsDaysOverride], [BayNo], [Paymethod]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblHistbks]');
    SET IDENTITY_INSERT [tblHistbks] OFF;
END
ELSE PRINT 'Skipping tblHistbks (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblHourlyRate]) = 0
BEGIN
    PRINT 'Importing tblHourlyRate (1 rows)...';
    SET IDENTITY_INSERT [tblHourlyRate] ON;
    INSERT INTO [tblHourlyRate] ([ID], [HourlyRateName])
    SELECT [ID], [HourlyRateName]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblHourlyRate]');
    SET IDENTITY_INSERT [tblHourlyRate] OFF;
END
ELSE PRINT 'Skipping tblHourlyRate (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLangTerms]) = 0
BEGIN
    PRINT 'Importing tblLangTerms (1 rows)...';
    SET IDENTITY_INSERT [tblLangTerms] ON;
    INSERT INTO [tblLangTerms] ([ID], [TermCell], [TermPhone1], [TermPhone2], [TermFax])
    SELECT [ID], [TermCell], [TermPhone1], [TermPhone2], [TermFax]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLangTerms]');
    SET IDENTITY_INSERT [tblLangTerms] OFF;
END
ELSE PRINT 'Skipping tblLangTerms (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblMasterBillingProductType]) = 0
BEGIN
    PRINT 'Importing tblMasterBillingProductType (1 rows)...';
    SET IDENTITY_INSERT [tblMasterBillingProductType] ON;
    INSERT INTO [tblMasterBillingProductType] ([ID], [MBProdTypeName], [MBPTActive], [MBPTNotes])
    SELECT [ID], [MBProdTypeName], [MBPTActive], [MBPTNotes]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblMasterBillingProductType]');
    SET IDENTITY_INSERT [tblMasterBillingProductType] OFF;
END
ELSE PRINT 'Skipping tblMasterBillingProductType (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblMessage]) = 0
BEGIN
    PRINT 'Importing tblMessage (1 rows)...';
    SET IDENTITY_INSERT [tblMessage] ON;
    INSERT INTO [tblMessage] ([ID], [MsgDate], [MsgTime], [ContactID], [ActivityTypeID], [Message], [FromID], [ToID], [Resolved], [Urgent], [StatusID], [MsgRead], [Subject])
    SELECT [ID], [MsgDate], [MsgTime], [ContactID], [ActivityTypeID], [Message], [FromID], [ToID], [Resolved], [Urgent], [StatusID], [MsgRead], [Subject]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblMessage]');
    SET IDENTITY_INSERT [tblMessage] OFF;
END
ELSE PRINT 'Skipping tblMessage (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblOptionGroups]) = 0
BEGIN
    PRINT 'Importing tblOptionGroups (1 rows)...';
    INSERT INTO [tblOptionGroups] ([optg_id], [optg_name])
    SELECT [optg_id], [optg_name]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblOptionGroups]');
END
ELSE PRINT 'Skipping tblOptionGroups (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPo_no]) = 0
BEGIN
    PRINT 'Importing tblPo_no (1 rows)...';
    SET IDENTITY_INSERT [tblPo_no] ON;
    INSERT INTO [tblPo_no] ([ID], [PO_number], [TurnOnCheckoutSp])
    SELECT [ID], [PO_number], [TurnOnCheckoutSp]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPo_no]');
    SET IDENTITY_INSERT [tblPo_no] OFF;
END
ELSE PRINT 'Skipping tblPo_no (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPriceovr]) = 0
BEGIN
    PRINT 'Importing tblPriceovr (1 rows)...';
    SET IDENTITY_INSERT [tblPriceovr] ON;
    INSERT INTO [tblPriceovr] ([ID], [booking_no], [product_code], [quantity], [old_price], [new_price], [timeH], [timeM], [reason], [DateF], [operators], [ItemTranID], [PrOFieldIndicator])
    SELECT [ID], [booking_no], [product_code], [quantity], [old_price], [new_price], [timeH], [timeM], [reason], [DateF], [operators], [ItemTranID], [PrOFieldIndicator]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPriceovr]');
    SET IDENTITY_INSERT [tblPriceovr] OFF;
END
ELSE PRINT 'Skipping tblPriceovr (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblTransno]) = 0
BEGIN
    PRINT 'Importing tblTransno (1 rows)...';
    SET IDENTITY_INSERT [tblTransno] ON;
    INSERT INTO [tblTransno] ([ID], [TransferNo], [NextProjectNo])
    SELECT [ID], [TransferNo], [NextProjectNo]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblTransno]');
    SET IDENTITY_INSERT [tblTransno] OFF;
END
ELSE PRINT 'Skipping tblTransno (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblTrucks]) = 0
BEGIN
    PRINT 'Importing tblTrucks (1 rows)...';
    SET IDENTITY_INSERT [tblTrucks] ON;
    INSERT INTO [tblTrucks] ([ID], [truck_number], [Truck_name], [CapacityWeight], [CapacityCubic], [RegionNumber], [LicensePlate], [LicensePlateExpiry], [FuelCostPerUnit], [FuelType], [Active])
    SELECT [ID], [truck_number], [Truck_name], [CapacityWeight], [CapacityCubic], [RegionNumber], [LicensePlate], [LicensePlateExpiry], [FuelCostPerUnit], [FuelType], [Active]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblTrucks]');
    SET IDENTITY_INSERT [tblTrucks] OFF;
END
ELSE PRINT 'Skipping tblTrucks (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCustom]) = 0
BEGIN
    PRINT 'Importing tblCustom (2 rows)...';
    SET IDENTITY_INSERT [tblCustom] ON;
    INSERT INTO [tblCustom] ([ID], [Field1Name], [Field2Name], [Field3Name], [Field4Name], [Field5Name], [Field6Name], [Field7Name], [Field8Name], [Field9Name], [Field10Name], [Field11Name], [Field12Name], [Field13Name], [Field14Name], [Field15Name], [Field16Name], [Field17Name], [Field18Name], [Field19Name], [Field20Name], [Field21Name], [Field22Name], [Field23Name], [Field24Name], [Field25Name], [Field26Name], [Field27Name], [Field28Name], [Field29Name], [Field30Name], [OperatorID], [CustID], [Field31Name], [Field32Name])
    SELECT [ID], [Field1Name], [Field2Name], [Field3Name], [Field4Name], [Field5Name], [Field6Name], [Field7Name], [Field8Name], [Field9Name], [Field10Name], [Field11Name], [Field12Name], [Field13Name], [Field14Name], [Field15Name], [Field16Name], [Field17Name], [Field18Name], [Field19Name], [Field20Name], [Field21Name], [Field22Name], [Field23Name], [Field24Name], [Field25Name], [Field26Name], [Field27Name], [Field28Name], [Field29Name], [Field30Name], [OperatorID], [CustID], [Field31Name], [Field32Name]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCustom]');
    SET IDENTITY_INSERT [tblCustom] OFF;
END
ELSE PRINT 'Skipping tblCustom (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblOperatorTelemetry]) = 0
BEGIN
    PRINT 'Importing tblOperatorTelemetry (3 rows)...';
    SET IDENTITY_INSERT [tblOperatorTelemetry] ON;
    INSERT INTO [tblOperatorTelemetry] ([ID], [OperatorID], [LogonDate], [LogoffDate], [SessionID], [IPAddress], [Device], [LastTabOpened], [loggedAt])
    SELECT [ID], [OperatorID], [LogonDate], [LogoffDate], [SessionID], [IPAddress], [Device], [LastTabOpened], [loggedAt]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblOperatorTelemetry]');
    SET IDENTITY_INSERT [tblOperatorTelemetry] OFF;
END
ELSE PRINT 'Skipping tblOperatorTelemetry (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblOptionItems]) = 0
BEGIN
    PRINT 'Importing tblOptionItems (3 rows)...';
    INSERT INTO [tblOptionItems] ([opt_id], [opt_code], [opt_name], [opt_type], [opt_encrypted], [opt_group])
    SELECT [opt_id], [opt_code], [opt_name], [opt_type], [opt_encrypted], [opt_group]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblOptionItems]');
END
ELSE PRINT 'Skipping tblOptionItems (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPayTerms]) = 0
BEGIN
    PRINT 'Importing tblPayTerms (3 rows)...';
    SET IDENTITY_INSERT [tblPayTerms] ON;
    INSERT INTO [tblPayTerms] ([ID], [Booking_no], [Customer_Code], [NoOfStages])
    SELECT [ID], [Booking_no], [Customer_Code], [NoOfStages]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPayTerms]');
    SET IDENTITY_INSERT [tblPayTerms] OFF;
END
ELSE PRINT 'Skipping tblPayTerms (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLabourHours]) = 0
BEGIN
    PRINT 'Importing tblLabourHours (4 rows)...';
    SET IDENTITY_INSERT [tblLabourHours] ON;
    INSERT INTO [tblLabourHours] ([ID], [HourlyRateID], [DayType], [StraightTime], [OverTime], [DoubleTime])
    SELECT [ID], [HourlyRateID], [DayType], [StraightTime], [OverTime], [DoubleTime]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLabourHours]');
    SET IDENTITY_INSERT [tblLabourHours] OFF;
END
ELSE PRINT 'Skipping tblLabourHours (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblStatus]) = 0
BEGIN
    PRINT 'Importing tblStatus (4 rows)...';
    SET IDENTITY_INSERT [tblStatus] ON;
    INSERT INTO [tblStatus] ([ID], [Status])
    SELECT [ID], [Status]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblStatus]');
    SET IDENTITY_INSERT [tblStatus] OFF;
END
ELSE PRINT 'Skipping tblStatus (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblUpdateCount]) = 0
BEGIN
    PRINT 'Importing tblUpdateCount (4 rows)...';
    SET IDENTITY_INSERT [tblUpdateCount] ON;
    INSERT INTO [tblUpdateCount] ([tblName], [updCount], [ID])
    SELECT [tblName], [updCount], [ID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblUpdateCount]');
    SET IDENTITY_INSERT [tblUpdateCount] OFF;
END
ELSE PRINT 'Skipping tblUpdateCount (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblExcelQuery]) = 0
BEGIN
    PRINT 'Importing tblExcelQuery (5 rows)...';
    INSERT INTO [tblExcelQuery] ([sqlName], [sqlText])
    SELECT [sqlName], [sqlText]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblExcelQuery]');
END
ELSE PRINT 'Skipping tblExcelQuery (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLinkShowCompany]) = 0
BEGIN
    PRINT 'Importing tblLinkShowCompany (5 rows)...';
    SET IDENTITY_INSERT [tblLinkShowCompany] ON;
    INSERT INTO [tblLinkShowCompany] ([ID], [ShowID], [CustID])
    SELECT [ID], [ShowID], [CustID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLinkShowCompany]');
    SET IDENTITY_INSERT [tblLinkShowCompany] OFF;
END
ELSE PRINT 'Skipping tblLinkShowCompany (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPayTermNames]) = 0
BEGIN
    PRINT 'Importing tblPayTermNames (5 rows)...';
    SET IDENTITY_INSERT [tblPayTermNames] ON;
    INSERT INTO [tblPayTermNames] ([ID], [TermNo], [CashOnly], [NetDays], [PayTermName])
    SELECT [ID], [TermNo], [CashOnly], [NetDays], [PayTermName]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPayTermNames]');
    SET IDENTITY_INSERT [tblPayTermNames] OFF;
END
ELSE PRINT 'Skipping tblPayTermNames (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblpriceFactors]) = 0
BEGIN
    PRINT 'Importing tblpriceFactors (5 rows)...';
    INSERT INTO [tblpriceFactors] ([PFTName], [TableFactorsOn], [DaysToCharge], [TableNo])
    SELECT [PFTName], [TableFactorsOn], [DaysToCharge], [TableNo]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblpriceFactors]');
END
ELSE PRINT 'Skipping tblpriceFactors (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblTax]) = 0
BEGIN
    PRINT 'Importing tblTax (5 rows)...';
    SET IDENTITY_INSERT [tblTax] ON;
    INSERT INTO [tblTax] ([ID], [tax_auth_no], [Tax_authority], [tax_name], [ceiling], [taxrental], [taxsale], [taxlabour], [taxdelivery], [taxsundry], [taxinsurance], [default1], [default2], [GLHolding], [GLOutput], [Piggybacktax], [State], [Disabled], [taxCreditSurcharge], [taxEventManagement])
    SELECT [ID], [tax_auth_no], [Tax_authority], [tax_name], [ceiling], [taxrental], [taxsale], [taxlabour], [taxdelivery], [taxsundry], [taxinsurance], [default1], [default2], [GLHolding], [GLOutput], [Piggybacktax], [State], [Disabled], [taxCreditSurcharge], [taxEventManagement]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblTax]');
    SET IDENTITY_INSERT [tblTax] OFF;
END
ELSE PRINT 'Skipping tblTax (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblFreight]) = 0
BEGIN
    PRINT 'Importing tblFreight (6 rows)...';
    SET IDENTITY_INSERT [tblFreight] ON;
    INSERT INTO [tblFreight] ([ID], [freightDesc], [Service], [Ourtruck], [Zone], [BaseRate], [FreightNo], [Region], [Location], [Disabled], [BaseCost])
    SELECT [ID], [freightDesc], [Service], [Ourtruck], [Zone], [BaseRate], [FreightNo], [Region], [Location], [Disabled], [BaseCost]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblFreight]');
    SET IDENTITY_INSERT [tblFreight] OFF;
END
ELSE PRINT 'Skipping tblFreight (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblGenNextId]) = 0
BEGIN
    PRINT 'Importing tblGenNextId (6 rows)...';
    INSERT INTO [tblGenNextId] ([gen_name], [gen_value])
    SELECT [gen_name], [gen_value]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblGenNextId]');
END
ELSE PRINT 'Skipping tblGenNextId (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPaymentMethods]) = 0
BEGIN
    PRINT 'Importing tblPaymentMethods (6 rows)...';
    SET IDENTITY_INSERT [tblPaymentMethods] ON;
    INSERT INTO [tblPaymentMethods] ([ID], [MethodName], [SurchargeRate])
    SELECT [ID], [MethodName], [SurchargeRate]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPaymentMethods]');
    SET IDENTITY_INSERT [tblPaymentMethods] OFF;
END
ELSE PRINT 'Skipping tblPaymentMethods (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCRMenu]) = 0
BEGIN
    PRINT 'Importing tblCRMenu (7 rows)...';
    SET IDENTITY_INSERT [tblCRMenu] ON;
    INSERT INTO [tblCRMenu] ([CRID], [Index_num], [MenuName], [MenuFile], [MenuParams], [Location])
    SELECT [CRID], [Index_num], [MenuName], [MenuFile], [MenuParams], [Location]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCRMenu]');
    SET IDENTITY_INSERT [tblCRMenu] OFF;
END
ELSE PRINT 'Skipping tblCRMenu (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblDivlist]) = 0
BEGIN
    PRINT 'Importing tblDivlist (7 rows)...';
    SET IDENTITY_INSERT [tblDivlist] ON;
    INSERT INTO [tblDivlist] ([ID], [div_number], [div_name])
    SELECT [ID], [div_number], [div_name]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblDivlist]');
    SET IDENTITY_INSERT [tblDivlist] OFF;
END
ELSE PRINT 'Skipping tblDivlist (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblHistitm]) = 0
BEGIN
    PRINT 'Importing tblHistitm (8 rows)...';
    SET IDENTITY_INSERT [tblHistitm] ON;
    INSERT INTO [tblHistitm] ([ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [trans_type_v41], [product_code_v42], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [price], [item_type], [days_using], [sub_hire_qtyV61], [From_locn], [Trans_to_locn], [return_to_locn], [bit_field_v41], [TimeBookedH], [TimeBookedM], [TimeBookedS], [QtyReturned], [QtyCheckedOut], [techRateorDaysCharged], [TechPay], [unitRate], [prep_on], [Comment_desc_v42], [AssignTo], [FirstDate], [RetnDate], [BookDate], [PDate], [PTimeH], [PTimeM], [DayWeekRate], [QtyReserved], [AddedAtCheckout], [GroupSeqNo], [SubRentalLinkID], [AssignType], [QtyShort], [QtyAvailable], [PackageLevel], [BeforeDiscountAmount], [QuickTurnAroundQty], [InRack], [CostPrice], [NodeCollapsed], [AvailRecFlag], [booking_id], [Undisc_amt], [View_Logi], [View_client], [Logi_HeadingNo], [Logi_GroupSeqNo], [Logi_Seq_No], [Logi_Sub_Seq_no], [ParentCode], [EstSubRentalCost], [EstSubRentalDays], [VendorID], [Notes], [UseEstSubHireOverride], [Estimated_sub_hire_v5], [resolvedDiscrep], [QTBookingNo], [QTSource], [warehouseMutedPerOER], [techrateIsHourorDay])
    SELECT [ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [trans_type_v41], [product_code_v42], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [price], [item_type], [days_using], [sub_hire_qtyV61], [From_locn], [Trans_to_locn], [return_to_locn], [bit_field_v41], [TimeBookedH], [TimeBookedM], [TimeBookedS], [QtyReturned], [QtyCheckedOut], [techRateorDaysCharged], [TechPay], [unitRate], [prep_on], [Comment_desc_v42], [AssignTo], [FirstDate], [RetnDate], [BookDate], [PDate], [PTimeH], [PTimeM], [DayWeekRate], [QtyReserved], [AddedAtCheckout], [GroupSeqNo], [SubRentalLinkID], [AssignType], [QtyShort], [QtyAvailable], [PackageLevel], [BeforeDiscountAmount], [QuickTurnAroundQty], [InRack], [CostPrice], [NodeCollapsed], [AvailRecFlag], [booking_id], [Undisc_amt], [View_Logi], [View_client], [Logi_HeadingNo], [Logi_GroupSeqNo], [Logi_Seq_No], [Logi_Sub_Seq_no], [ParentCode], [EstSubRentalCost], [EstSubRentalDays], [VendorID], [Notes], [UseEstSubHireOverride], [Estimated_sub_hire_v5], [resolvedDiscrep], [QTBookingNo], [QTSource], [warehouseMutedPerOER], [techrateIsHourorDay]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblHistitm]');
    SET IDENTITY_INSERT [tblHistitm] OFF;
END
ELSE PRINT 'Skipping tblHistitm (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblRegions]) = 0
BEGIN
    PRINT 'Importing tblRegions (8 rows)...';
    SET IDENTITY_INSERT [tblRegions] ON;
    INSERT INTO [tblRegions] ([ID], [RegionNumber], [RegionName], [RegionGLCode], [BatchNumber])
    SELECT [ID], [RegionNumber], [RegionName], [RegionGLCode], [BatchNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblRegions]');
    SET IDENTITY_INSERT [tblRegions] OFF;
END
ELSE PRINT 'Skipping tblRegions (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblTermStages]) = 0
BEGIN
    PRINT 'Importing tblTermStages (8 rows)...';
    SET IDENTITY_INSERT [tblTermStages] ON;
    INSERT INTO [tblTermStages] ([ID], [InvStageName], [Percentage], [StageNo], [PayTermID], [dueDate])
    SELECT [ID], [InvStageName], [Percentage], [StageNo], [PayTermID], [dueDate]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblTermStages]');
    SET IDENTITY_INSERT [tblTermStages] OFF;
END
ELSE PRINT 'Skipping tblTermStages (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblActivityType]) = 0
BEGIN
    PRINT 'Importing tblActivityType (10 rows)...';
    SET IDENTITY_INSERT [tblActivityType] ON;
    INSERT INTO [tblActivityType] ([ID], [Description], [Points], [Colour], [CustomActivityType], [RpwsType], [RPWSTypes])
    SELECT [ID], [Description], [Points], [Colour], [CustomActivityType], [RpwsType], [RPWSTypes]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblActivityType]');
    SET IDENTITY_INSERT [tblActivityType] OFF;
END
ELSE PRINT 'Skipping tblActivityType (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCampaign]) = 0
BEGIN
    PRINT 'Importing tblCampaign (11 rows)...';
    SET IDENTITY_INSERT [tblCampaign] ON;
    INSERT INTO [tblCampaign] ([ID], [SourceName], [Cost], [DateStarted])
    SELECT [ID], [SourceName], [Cost], [DateStarted]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCampaign]');
    SET IDENTITY_INSERT [tblCampaign] OFF;
END
ELSE PRINT 'Skipping tblCampaign (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblOperatorTelemetryHist]) = 0
BEGIN
    PRINT 'Importing tblOperatorTelemetryHist (12 rows)...';
    SET IDENTITY_INSERT [tblOperatorTelemetryHist] ON;
    INSERT INTO [tblOperatorTelemetryHist] ([ID], [OperatorID], [LogonDate], [LogoffDate], [SessionID], [IPAddress], [Device], [LastTabOpened], [loggedAt])
    SELECT [ID], [OperatorID], [LogonDate], [LogoffDate], [SessionID], [IPAddress], [Device], [LastTabOpened], [loggedAt]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblOperatorTelemetryHist]');
    SET IDENTITY_INSERT [tblOperatorTelemetryHist] OFF;
END
ELSE PRINT 'Skipping tblOperatorTelemetryHist (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblListHead]) = 0
BEGIN
    PRINT 'Importing tblListHead (14 rows)...';
    SET IDENTITY_INSERT [tblListHead] ON;
    INSERT INTO [tblListHead] ([ID], [CallListName], [MultipleDays], [SkipWeekends], [ActivitiesPerDay], [ScriptID], [IsDistributionList], [HTMLFile], [IsProductList], [BookingProgressOpts], [CompanyTypes], [BookingTypes], [LowestQty], [HighestQty], [StartDate], [EndDate], [Industry], [ShowName], [EventType], [Source], [ByCustomer], [StartValue], [EndValue])
    SELECT [ID], [CallListName], [MultipleDays], [SkipWeekends], [ActivitiesPerDay], [ScriptID], [IsDistributionList], [HTMLFile], [IsProductList], [BookingProgressOpts], [CompanyTypes], [BookingTypes], [LowestQty], [HighestQty], [StartDate], [EndDate], [Industry], [ShowName], [EventType], [Source], [ByCustomer], [StartValue], [EndValue]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblListHead]');
    SET IDENTITY_INSERT [tblListHead] OFF;
END
ELSE PRINT 'Skipping tblListHead (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblShow]) = 0
BEGIN
    PRINT 'Importing tblShow (14 rows)...';
    SET IDENTITY_INSERT [tblShow] ON;
    INSERT INTO [tblShow] ([ID], [ShowName], [StartDate], [EndDate], [Venue], [DecisionDate], [Equipment], [Coordinator], [CustID], [Crew], [Competitors], [Budget])
    SELECT [ID], [ShowName], [StartDate], [EndDate], [Venue], [DecisionDate], [Equipment], [Coordinator], [CustID], [Crew], [Competitors], [Budget]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblShow]');
    SET IDENTITY_INSERT [tblShow] OFF;
END
ELSE PRINT 'Skipping tblShow (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblSettings]) = 0
BEGIN
    PRINT 'Importing tblSettings (15 rows)...';
    INSERT INTO [tblSettings] ([Name], [Value])
    SELECT [Name], [Value]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblSettings]');
END
ELSE PRINT 'Skipping tblSettings (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblOperatorgroup]) = 0
BEGIN
    PRINT 'Importing tblOperatorgroup (20 rows)...';
    SET IDENTITY_INSERT [tblOperatorgroup] ON;
    INSERT INTO [tblOperatorgroup] ([ID], [Groupname], [Accessprivilage], [Description], [AccessPrivilage1], [AccessPrivilage2], [DefaultPanel])
    SELECT [ID], [Groupname], [Accessprivilage], [Description], [AccessPrivilage1], [AccessPrivilage2], [DefaultPanel]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblOperatorgroup]');
    SET IDENTITY_INSERT [tblOperatorgroup] OFF;
END
ELSE PRINT 'Skipping tblOperatorgroup (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblGroup]) = 0
BEGIN
    PRINT 'Importing tblGroup (22 rows)...';
    SET IDENTITY_INSERT [tblGroup] ON;
    INSERT INTO [tblGroup] ([ID], [Group_code], [group_descV6], [days_table], [company], [seqNo], [GroupProductType], [AllowDiscount], [DefaultVendorCode], [pricingscheme], [DisplayColour], [OLGroupDesc], [OverrideMultiRateWithPFT], [CustomIcon], [MasterBillingProductType])
    SELECT [ID], [Group_code], [group_descV6], [days_table], [company], [seqNo], [GroupProductType], [AllowDiscount], [DefaultVendorCode], [pricingscheme], [DisplayColour], [OLGroupDesc], [OverrideMultiRateWithPFT], [CustomIcon], [MasterBillingProductType]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblGroup]');
    SET IDENTITY_INSERT [tblGroup] OFF;
END
ELSE PRINT 'Skipping tblGroup (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblMasterBilling]) = 0
BEGIN
    PRINT 'Importing tblMasterBilling (23 rows)...';
    SET IDENTITY_INSERT [tblMasterBilling] ON;
    INSERT INTO [tblMasterBilling] ([ID], [MBName], [CustomerID], [CommSalesHotel], [CommSalesAV], [CommRentalHotel], [CommRentalAV], [CommCrewHotel], [CommCrewAV], [CommInsur], [CommCCSurcharge], [CommOnLossesHotel], [CommonLossAV], [CommOnSundry], [CommOnEventManagementFee], [CommOnCrossRentals], [PriceSet], [TaxAuthority1], [TaxAuthority2], [MBActive], [Location], [Notes], [Scenario])
    SELECT [ID], [MBName], [CustomerID], [CommSalesHotel], [CommSalesAV], [CommRentalHotel], [CommRentalAV], [CommCrewHotel], [CommCrewAV], [CommInsur], [CommCCSurcharge], [CommOnLossesHotel], [CommonLossAV], [CommOnSundry], [CommOnEventManagementFee], [CommOnCrossRentals], [PriceSet], [TaxAuthority1], [TaxAuthority2], [MBActive], [Location], [Notes], [Scenario]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblMasterBilling]');
    SET IDENTITY_INSERT [tblMasterBilling] OFF;
END
ELSE PRINT 'Skipping tblMasterBilling (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblActivity]) = 0
BEGIN
    PRINT 'Importing tblActivity (25 rows)...';
    SET IDENTITY_INSERT [tblActivity] ON;
    INSERT INTO [tblActivity] ([ID], [StartDate], [EndDate], [StartTime], [EndTime], [TypeID], [OperatorID], [Description], [Notes], [Completed], [CompletedBy], [DateCompleted], [TimeCompleted], [ActivityResultID], [Scheduled], [ActualDuration], [CallListID], [AlarmDate], [AlarmTime], [AlarmSet], [ContactID], [AlarmMessage], [Booking_no], [ProjectCode], [LastContactedDateTime], [ActivitySource])
    SELECT [ID], [StartDate], [EndDate], [StartTime], [EndTime], [TypeID], [OperatorID], [Description], [Notes], [Completed], [CompletedBy], [DateCompleted], [TimeCompleted], [ActivityResultID], [Scheduled], [ActualDuration], [CallListID], [AlarmDate], [AlarmTime], [AlarmSet], [ContactID], [AlarmMessage], [Booking_no], [ProjectCode], [LastContactedDateTime], [ActivitySource]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblActivity]');
    SET IDENTITY_INSERT [tblActivity] OFF;
END
ELSE PRINT 'Skipping tblActivity (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblFreightRates]) = 0
BEGIN
    PRINT 'Importing tblFreightRates (25 rows)...';
    SET IDENTITY_INSERT [tblFreightRates] ON;
    INSERT INTO [tblFreightRates] ([ID], [tblFreightID], [rate_no], [fromweight], [Rate], [Cost])
    SELECT [ID], [tblFreightID], [rate_no], [fromweight], [Rate], [Cost]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblFreightRates]');
    SET IDENTITY_INSERT [tblFreightRates] OFF;
END
ELSE PRINT 'Skipping tblFreightRates (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblTask]) = 0
BEGIN
    PRINT 'Importing tblTask (26 rows)...';
    SET IDENTITY_INSERT [tblTask] ON;
    INSERT INTO [tblTask] ([ID], [task_number], [DefDateTime], [defaultDateandTime], [task_name], [TaskType])
    SELECT [ID], [task_number], [DefDateTime], [defaultDateandTime], [task_name], [TaskType]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblTask]');
    SET IDENTITY_INSERT [tblTask] OFF;
END
ELSE PRINT 'Skipping tblTask (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblReservedAssets]) = 0
BEGIN
    PRINT 'Importing tblReservedAssets (28 rows)...';
    SET IDENTITY_INSERT [tblReservedAssets] ON;
    INSERT INTO [tblReservedAssets] ([ID], [Booking_no], [Product_code], [Stock_number])
    SELECT [ID], [Booking_no], [Product_code], [Stock_number]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblReservedAssets]');
    SET IDENTITY_INSERT [tblReservedAssets] OFF;
END
ELSE PRINT 'Skipping tblReservedAssets (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLocnlist]) = 0
BEGIN
    PRINT 'Importing tblLocnlist (35 rows)...';
    SET IDENTITY_INSERT [tblLocnlist] ON;
    INSERT INTO [tblLocnlist] ([ID], [Locn_number], [Locn_name], [Lname], [LAdr1], [LAdr2], [Ladr3], [Lphone], [Lfax], [AutoTransfer], [DefaultGLCode], [AcctFileLocn], [NextInv_NO], [Locked], [LockDate], [LockTimeH], [LockTimeM], [LockedBy], [State], [Country], [PostCode], [TaxAuthority1], [TaxAuthority2], [TaxNumber], [NextPoNumber], [Phone1ID], [FaxID], [RegionNumber], [IsMainLocn], [LastExport], [LocnGlCode], [DefaultPriceSet], [BatchNumber], [SMTPAddress], [SMTPPort], [DefaultStandardTextID], [PhoneCountryCode], [PhoneAreaCode], [PhoneExt], [FaxCountryCode], [FaxAreaCode], [DefaultDeliveryID], [DefaultReturnID], [MapAddress1], [MapAddress2], [MapCity], [MapState], [MapCountry], [MapPostCode], [DefaultCrewDelivery], [DefaultCrewPickup], [Phone2ID], [CellID], [NextCreditNoteNumber], [SMTPEncryption], [TemplateDirectory], [SMTPReqAuth], [SMTPUserName], [POPrefix], [ContractNoPrefix], [StripePubKey], [StripeSecKey], [QBProductionID], [QBProductionSecret], [QBSandboxID], [QBSandboxSecret], [QBTaxName], [QBTaxNumber], [QBSandboxProduction], [realmID], [access_token], [refresh_token], [access_token_expire], [refresh_token_expire])
    SELECT [ID], [Locn_number], [Locn_name], [Lname], [LAdr1], [LAdr2], [Ladr3], [Lphone], [Lfax], [AutoTransfer], [DefaultGLCode], [AcctFileLocn], [NextInv_NO], [Locked], [LockDate], [LockTimeH], [LockTimeM], [LockedBy], [State], [Country], [PostCode], [TaxAuthority1], [TaxAuthority2], [TaxNumber], [NextPoNumber], [Phone1ID], [FaxID], [RegionNumber], [IsMainLocn], [LastExport], [LocnGlCode], [DefaultPriceSet], [BatchNumber], [SMTPAddress], [SMTPPort], [DefaultStandardTextID], [PhoneCountryCode], [PhoneAreaCode], [PhoneExt], [FaxCountryCode], [FaxAreaCode], [DefaultDeliveryID], [DefaultReturnID], [MapAddress1], [MapAddress2], [MapCity], [MapState], [MapCountry], [MapPostCode], [DefaultCrewDelivery], [DefaultCrewPickup], [Phone2ID], [CellID], [NextCreditNoteNumber], [SMTPEncryption], [TemplateDirectory], [SMTPReqAuth], [SMTPUserName], [POPrefix], [ContractNoPrefix], [StripePubKey], [StripeSecKey], [QBProductionID], [QBProductionSecret], [QBSandboxID], [QBSandboxSecret], [QBTaxName], [QBTaxNumber], [QBSandboxProduction], [realmID], [access_token], [refresh_token], [access_token_expire], [refresh_token_expire]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLocnlist]');
    SET IDENTITY_INSERT [tblLocnlist] OFF;
END
ELSE PRINT 'Skipping tblLocnlist (already has data)';
GO

IF (SELECT COUNT(*) FROM [tbloperatorPreference]) = 0
BEGIN
    PRINT 'Importing tbloperatorPreference (39 rows)...';
    SET IDENTITY_INSERT [tbloperatorPreference] ON;
    INSERT INTO [tbloperatorPreference] ([ID], [OperatorID], [OperatorName], [prefName], [PrefValue])
    SELECT [ID], [OperatorID], [OperatorName], [prefName], [PrefValue]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tbloperatorPreference]');
    SET IDENTITY_INSERT [tbloperatorPreference] OFF;
END
ELSE PRINT 'Skipping tbloperatorPreference (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblInvAudit]) = 0
BEGIN
    PRINT 'Importing tblInvAudit (40 rows)...';
    SET IDENTITY_INSERT [tblInvAudit] ON;
    INSERT INTO [tblInvAudit] ([AuditID], [TableName], [ActionType], [ChangedBy], [ChangeDate], [Notes])
    SELECT [AuditID], [TableName], [ActionType], [ChangedBy], [ChangeDate], [Notes]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblInvAudit]');
    SET IDENTITY_INSERT [tblInvAudit] OFF;
END
ELSE PRINT 'Skipping tblInvAudit (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblInvAuditDetail]) = 0
BEGIN
    PRINT 'Importing tblInvAuditDetail (47 rows)...';
    SET IDENTITY_INSERT [tblInvAuditDetail] ON;
    INSERT INTO [tblInvAuditDetail] ([DetailID], [AuditID], [ColumnName], [Finalized], [OldValue], [NewValue], [RecordID])
    SELECT [DetailID], [AuditID], [ColumnName], [Finalized], [OldValue], [NewValue], [RecordID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblInvAuditDetail]');
    SET IDENTITY_INSERT [tblInvAuditDetail] OFF;
END
ELSE PRINT 'Skipping tblInvAuditDetail (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblContactNote]) = 0
BEGIN
    PRINT 'Importing tblContactNote (51 rows)...';
    SET IDENTITY_INSERT [tblContactNote] ON;
    INSERT INTO [tblContactNote] ([ID], [ContactID], [LineNumber], [LineText])
    SELECT [ID], [ContactID], [LineNumber], [LineText]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblContactNote]');
    SET IDENTITY_INSERT [tblContactNote] OFF;
END
ELSE PRINT 'Skipping tblContactNote (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCountries]) = 0
BEGIN
    PRINT 'Importing tblCountries (51 rows)...';
    INSERT INTO [tblCountries] ([ID], [cname])
    SELECT [ID], [cname]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCountries]');
END
ELSE PRINT 'Skipping tblCountries (already has data)';
GO

IF (SELECT COUNT(*) FROM [SC_ImportEvent]) = 0
BEGIN
    PRINT 'Importing SC_ImportEvent (52 rows)...';
    INSERT INTO [SC_ImportEvent] ([ID], [event_code], [event_desc], [deltime], [rettime], [ShowTerm], [showdaysCharged], [DelvDate], [ShowSDate], [PrepDateTime], [Start_Date], [End_Date], [RetnDate], [ShowEDate], [DeprepDateTime], [Locn], [Mbscenario], [MBID], [invoiced], [Invoice_no], [Invoice_amount], [BookingsInvoiced], [Salesperson], [coordinator], [rentalDisc], [SalesDisc])
    SELECT [ID], [event_code], [event_desc], [deltime], [rettime], [ShowTerm], [showdaysCharged], [DelvDate], [ShowSDate], [PrepDateTime], [Start_Date], [End_Date], [RetnDate], [ShowEDate], [DeprepDateTime], [Locn], [Mbscenario], [MBID], [invoiced], [Invoice_no], [Invoice_amount], [BookingsInvoiced], [Salesperson], [coordinator], [rentalDisc], [SalesDisc]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [SC_ImportEvent]');
END
ELSE PRINT 'Skipping SC_ImportEvent (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblDeletedAssets]) = 0
BEGIN
    PRINT 'Importing tblDeletedAssets (53 rows)...';
    SET IDENTITY_INSERT [tblDeletedAssets] ON;
    INSERT INTO [tblDeletedAssets] ([ID], [InvMasID], [StockNumber])
    SELECT [ID], [InvMasID], [StockNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblDeletedAssets]');
    SET IDENTITY_INSERT [tblDeletedAssets] OFF;
END
ELSE PRINT 'Skipping tblDeletedAssets (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblFastReportTypes]) = 0
BEGIN
    PRINT 'Importing tblFastReportTypes (56 rows)...';
    INSERT INTO [tblFastReportTypes] ([frt_id], [frt_name])
    SELECT [frt_id], [frt_name]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblFastReportTypes]');
END
ELSE PRINT 'Skipping tblFastReportTypes (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblFRTypesRPMVC]) = 0
BEGIN
    PRINT 'Importing tblFRTypesRPMVC (56 rows)...';
    INSERT INTO [tblFRTypesRPMVC] ([frt_id], [frt_name])
    SELECT [frt_id], [frt_name]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblFRTypesRPMVC]');
END
ELSE PRINT 'Skipping tblFRTypesRPMVC (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCultureCode]) = 0
BEGIN
    PRINT 'Importing tblCultureCode (58 rows)...';
    INSERT INTO [tblCultureCode] ([ID], [spec_cult], [cult_name])
    SELECT [ID], [spec_cult], [cult_name]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCultureCode]');
END
ELSE PRINT 'Skipping tblCultureCode (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblMarginTargets]) = 0
BEGIN
    PRINT 'Importing tblMarginTargets (80 rows)...';
    SET IDENTITY_INSERT [tblMarginTargets] ON;
    INSERT INTO [tblMarginTargets] ([ID], [Margintype], [seqNo], [FromAmt], [ToAmt], [MarginTargetPerc])
    SELECT [ID], [Margintype], [seqNo], [FromAmt], [ToAmt], [MarginTargetPerc]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblMarginTargets]');
    SET IDENTITY_INSERT [tblMarginTargets] OFF;
END
ELSE PRINT 'Skipping tblMarginTargets (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblProdmx05]) = 0
BEGIN
    PRINT 'Importing tblProdmx05 (89 rows)...';
    SET IDENTITY_INSERT [tblProdmx05] ON;
    INSERT INTO [tblProdmx05] ([ID], [OpID], [LicTime], [IntKey], [SIKey], [LOCATION])
    SELECT [ID], [OpID], [LicTime], [IntKey], [SIKey], [LOCATION]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblProdmx05]');
    SET IDENTITY_INSERT [tblProdmx05] OFF;
END
ELSE PRINT 'Skipping tblProdmx05 (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblAgents]) = 0
BEGIN
    PRINT 'Importing tblAgents (109 rows)...';
    SET IDENTITY_INSERT [tblAgents] ON;
    INSERT INTO [tblAgents] ([ID], [agent_code], [Auth_agentV6], [address_l1V6], [address_l2V6], [address_l3V6], [phone], [fax], [what_type], [contactname], [ContactID], [State], [Country], [PostCode], [Phone1ID], [Phone2ID], [CellID], [FaxID])
    SELECT [ID], [agent_code], [Auth_agentV6], [address_l1V6], [address_l2V6], [address_l3V6], [phone], [fax], [what_type], [contactname], [ContactID], [State], [Country], [PostCode], [Phone1ID], [Phone2ID], [CellID], [FaxID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblAgents]');
    SET IDENTITY_INSERT [tblAgents] OFF;
END
ELSE PRINT 'Skipping tblAgents (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCurrencyName]) = 0
BEGIN
    PRINT 'Importing tblCurrencyName (138 rows)...';
    INSERT INTO [tblCurrencyName] ([ID], [CurrCode], [CurrName])
    SELECT [ID], [CurrCode], [CurrName]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCurrencyName]');
END
ELSE PRINT 'Skipping tblCurrencyName (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblDbMaintenance]) = 0
BEGIN
    PRINT 'Importing tblDbMaintenance (141 rows)...';
    SET IDENTITY_INSERT [tblDbMaintenance] ON;
    INSERT INTO [tblDbMaintenance] ([ID], [ActionName], [ActionErrorCode], [ActionDateTime], [Note], [OperatorId])
    SELECT [ID], [ActionName], [ActionErrorCode], [ActionDateTime], [Note], [OperatorId]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblDbMaintenance]');
    SET IDENTITY_INSERT [tblDbMaintenance] OFF;
END
ELSE PRINT 'Skipping tblDbMaintenance (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblInstruct]) = 0
BEGIN
    PRINT 'Importing tblInstruct (164 rows)...';
    SET IDENTITY_INSERT [tblInstruct] ON;
    INSERT INTO [tblInstruct] ([ID], [booking_no], [inst_instru])
    SELECT [ID], [booking_no], [inst_instru]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblInstruct]');
    SET IDENTITY_INSERT [tblInstruct] OFF;
END
ELSE PRINT 'Skipping tblInstruct (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblEmailNotes]) = 0
BEGIN
    PRINT 'Importing tblEmailNotes (177 rows)...';
    SET IDENTITY_INSERT [tblEmailNotes] ON;
    INSERT INTO [tblEmailNotes] ([ID], [OperatorID], [LocationNumber], [LineNumber], [Notes], [NoteType], [SignatureName])
    SELECT [ID], [OperatorID], [LocationNumber], [LineNumber], [Notes], [NoteType], [SignatureName]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblEmailNotes]');
    SET IDENTITY_INSERT [tblEmailNotes] OFF;
END
ELSE PRINT 'Skipping tblEmailNotes (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblSalesper]) = 0
BEGIN
    PRINT 'Importing tblSalesper (197 rows)...';
    SET IDENTITY_INSERT [tblSalesper] ON;
    INSERT INTO [tblSalesper] ([ID], [salesperson_code], [Salesperson_name], [assigned_for], [MonthsAssignedToContact], [ContactID], [bDisabled], [CommSales], [CommRental], [CommCrew], [CommInsur], [CommCCSurcharge], [CommOnLosses], [CommOnSundry], [CommOnEventManagementFee], [CommOnCrossRentals], [CommOnCrossCrew])
    SELECT [ID], [salesperson_code], [Salesperson_name], [assigned_for], [MonthsAssignedToContact], [ContactID], [bDisabled], [CommSales], [CommRental], [CommCrew], [CommInsur], [CommCCSurcharge], [CommOnLosses], [CommOnSundry], [CommOnEventManagementFee], [CommOnCrossRentals], [CommOnCrossCrew]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblSalesper]');
    SET IDENTITY_INSERT [tblSalesper] OFF;
END
ELSE PRINT 'Skipping tblSalesper (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblHolidays]) = 0
BEGIN
    PRINT 'Importing tblHolidays (227 rows)...';
    SET IDENTITY_INSERT [tblHolidays] ON;
    INSERT INTO [tblHolidays] ([ID], [DateF], [Description], [HolidayRegion], [HolidayLocation])
    SELECT [ID], [DateF], [Description], [HolidayRegion], [HolidayLocation]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblHolidays]');
    SET IDENTITY_INSERT [tblHolidays] OFF;
END
ELSE PRINT 'Skipping tblHolidays (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblWpformat]) = 0
BEGIN
    PRINT 'Importing tblWpformat (279 rows)...';
    SET IDENTITY_INSERT [tblWpformat] ON;
    INSERT INTO [tblWpformat] ([ID], [file_name], [desc], [Locn], [wp_type], [CustomerCode])
    SELECT [ID], [file_name], [desc], [Locn], [wp_type], [CustomerCode]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblWpformat]');
    SET IDENTITY_INSERT [tblWpformat] OFF;
END
ELSE PRINT 'Skipping tblWpformat (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblOperators]) = 0
BEGIN
    PRINT 'Importing tblOperators (280 rows)...';
    SET IDENTITY_INSERT [tblOperators] ON;
    INSERT INTO [tblOperators] ([ID], [FirstName], [LastName], [BelongsToGroup], [Loginname], [Password], [Email], [FullName], [StartWorkDay], [EndWorkDay], [WorkDays], [TimeInc], [CustomFieldsID], [PrivateMode], [CheckMessages], [RecallDays], [AutoEmail], [DefaultDurationH], [DefaultDurationM], [LoginAllowed], [DefaultLocation], [TreeViewAlwaysOpen], [MaxPOAmount], [AssignBookingToPO], [DefaultDivision], [DefaultRegion], [ReceiveBookingStatusMessage], [SMTPAddress], [SMTPPort], [DefaultSalesperson], [DefaultProjectManager], [DefaultSignatureID], [SysAdmin], [MaxRentalDiscount], [MaxSalesDiscount], [AutoEmailing], [MaxCRAmount], [bIsEnglishUser], [useCloud], [cUserName], [cPassword], [cCloudType], [cViewerType], [SMTPEncryption], [SMTPReqAuth], [SMTPType], [ColorSettings], [RFIDName], [OverrideEmailing], [EmailAll], [EmailSP], [EmailPM], [RecoveryCode], [PasswordWeb], [MobileInterface], [CultureInt], [Phone], [TOTP_KEY], [MFAEnabled], [MFASms], [MFAEmail], [AlwaysRequire2fa], [RecoveryCodes], [Mandatory2FA], [RecoveryUntil])
    SELECT [ID], [FirstName], [LastName], [BelongsToGroup], [Loginname], [Password], [Email], [FullName], [StartWorkDay], [EndWorkDay], [WorkDays], [TimeInc], [CustomFieldsID], [PrivateMode], [CheckMessages], [RecallDays], [AutoEmail], [DefaultDurationH], [DefaultDurationM], [LoginAllowed], [DefaultLocation], [TreeViewAlwaysOpen], [MaxPOAmount], [AssignBookingToPO], [DefaultDivision], [DefaultRegion], [ReceiveBookingStatusMessage], [SMTPAddress], [SMTPPort], [DefaultSalesperson], [DefaultProjectManager], [DefaultSignatureID], [SysAdmin], [MaxRentalDiscount], [MaxSalesDiscount], [AutoEmailing], [MaxCRAmount], [bIsEnglishUser], [useCloud], [cUserName], [cPassword], [cCloudType], [cViewerType], [SMTPEncryption], [SMTPReqAuth], [SMTPType], [ColorSettings], [RFIDName], [OverrideEmailing], [EmailAll], [EmailSP], [EmailPM], [RecoveryCode], [PasswordWeb], [MobileInterface], [CultureInt], [Phone], [TOTP_KEY], [MFAEnabled], [MFASms], [MFAEmail], [AlwaysRequire2fa], [RecoveryCodes], [Mandatory2FA], [RecoveryUntil]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblOperators]');
    SET IDENTITY_INSERT [tblOperators] OFF;
END
ELSE PRINT 'Skipping tblOperators (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCustnote]) = 0
BEGIN
    PRINT 'Importing tblCustnote (425 rows)...';
    SET IDENTITY_INSERT [tblCustnote] ON;
    INSERT INTO [tblCustnote] ([ID], [customer_code], [line_no], [text_line], [OperatorID])
    SELECT [ID], [customer_code], [line_no], [text_line], [OperatorID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCustnote]');
    SET IDENTITY_INSERT [tblCustnote] OFF;
END
ELSE PRINT 'Skipping tblCustnote (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCallList]) = 0
BEGIN
    PRINT 'Importing tblCallList (483 rows)...';
    SET IDENTITY_INSERT [tblCallList] ON;
    INSERT INTO [tblCallList] ([ID], [CallListID], [ContactID], [Completed], [ActivityID])
    SELECT [ID], [CallListID], [ContactID], [Completed], [ActivityID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCallList]');
    SET IDENTITY_INSERT [tblCallList] OFF;
END
ELSE PRINT 'Skipping tblCallList (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblVendnote]) = 0
BEGIN
    PRINT 'Importing tblVendnote (527 rows)...';
    SET IDENTITY_INSERT [tblVendnote] ON;
    INSERT INTO [tblVendnote] ([ID], [code], [line_no], [text_line])
    SELECT [ID], [code], [line_no], [text_line]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblVendnote]');
    SET IDENTITY_INSERT [tblVendnote] OFF;
END
ELSE PRINT 'Skipping tblVendnote (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLinkSaleCostAssetran]) = 0
BEGIN
    PRINT 'Importing tblLinkSaleCostAssetran (604 rows)...';
    SET IDENTITY_INSERT [tblLinkSaleCostAssetran] ON;
    INSERT INTO [tblLinkSaleCostAssetran] ([ID], [SaleCostID], [AssetranID])
    SELECT [ID], [SaleCostID], [AssetranID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLinkSaleCostAssetran]');
    SET IDENTITY_INSERT [tblLinkSaleCostAssetran] OFF;
END
ELSE PRINT 'Skipping tblLinkSaleCostAssetran (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCategory]) = 0
BEGIN
    PRINT 'Importing tblCategory (791 rows)...';
    SET IDENTITY_INSERT [tblCategory] ON;
    INSERT INTO [tblCategory] ([ID], [category_code], [cat_descV6], [DisplayColour], [DisplayBold], [CategoryType], [StandardCostPercentage], [GLRevenueCode], [GLCrossRentExpenseCode], [Group_code], [ParentCategoryCode], [OLCatDesc], [CustomIcon])
    SELECT [ID], [category_code], [cat_descV6], [DisplayColour], [DisplayBold], [CategoryType], [StandardCostPercentage], [GLRevenueCode], [GLCrossRentExpenseCode], [Group_code], [ParentCategoryCode], [OLCatDesc], [CustomIcon]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCategory]');
    SET IDENTITY_INSERT [tblCategory] OFF;
END
ELSE PRINT 'Skipping tblCategory (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblSundry]) = 0
BEGIN
    PRINT 'Importing tblSundry (794 rows)...';
    SET IDENTITY_INSERT [tblSundry] ON;
    INSERT INTO [tblSundry] ([ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [sundry_desc], [sundry_cost], [sundry_price], [GroupSeqNo], [Discount], [trans_qty], [restock_charge], [RevenueCode], [sundry_markup_percentage], [view_client], [view_logi], [Logi_HeadingNo], [Logi_GroupSeqNo], [Logi_Seq_No], [Logi_Sub_Seq_no], [SundryType])
    SELECT [ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [sundry_desc], [sundry_cost], [sundry_price], [GroupSeqNo], [Discount], [trans_qty], [restock_charge], [RevenueCode], [sundry_markup_percentage], [view_client], [view_logi], [Logi_HeadingNo], [Logi_GroupSeqNo], [Logi_Seq_No], [Logi_Sub_Seq_no], [SundryType]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblSundry]');
    SET IDENTITY_INSERT [tblSundry] OFF;
END
ELSE PRINT 'Skipping tblSundry (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblRoadcase]) = 0
BEGIN
    PRINT 'Importing tblRoadcase (820 rows)...';
    SET IDENTITY_INSERT [tblRoadcase] ON;
    INSERT INTO [tblRoadcase] ([ID], [parent_basecode], [asset_barcode], [CaseType], [Qty], [NonBarProductCode], [Booking_no], [DateTimePacked], [PackedBy], [NonBarLocn], [parent_basecode_ID], [asset_barcode_ID], [Floating])
    SELECT [ID], [parent_basecode], [asset_barcode], [CaseType], [Qty], [NonBarProductCode], [Booking_no], [DateTimePacked], [PackedBy], [NonBarLocn], [parent_basecode_ID], [asset_barcode_ID], [Floating]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblRoadcase]');
    SET IDENTITY_INSERT [tblRoadcase] OFF;
END
ELSE PRINT 'Skipping tblRoadcase (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblRoadcasePackList]) = 0
BEGIN
    PRINT 'Importing tblRoadcasePackList (1,154 rows)...';
    SET IDENTITY_INSERT [tblRoadcasePackList] ON;
    INSERT INTO [tblRoadcasePackList] ([ID], [Parent_BaseCode_id], [Asset_Barcode_ID], [AssetDescription], [CaseType], [Qty], [NonBarProductCode], [Booking_no], [dateTimePacked], [PackedBy], [NonBarLocn], [Floating], [ListType])
    SELECT [ID], [Parent_BaseCode_id], [Asset_Barcode_ID], [AssetDescription], [CaseType], [Qty], [NonBarProductCode], [Booking_no], [dateTimePacked], [PackedBy], [NonBarLocn], [Floating], [ListType]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblRoadcasePackList]');
    SET IDENTITY_INSERT [tblRoadcasePackList] OFF;
END
ELSE PRINT 'Skipping tblRoadcasePackList (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblVendor]) = 0
BEGIN
    PRINT 'Importing tblVendor (1,171 rows)...';
    SET IDENTITY_INSERT [tblVendor] ON;
    INSERT INTO [tblVendor] ([ID], [VendorCode], [VendorContact], [VendorName], [Vadr1], [Vadr2], [Vadr3], [Vpostcode], [Vphone1], [Vphone2], [Vfax], [Vemail], [Vwebpage], [Vcurrency], [Vaccno], [AreaCode], [CountryCode], [CallType], [UseAreaCode], [FaxExt], [Phone1Ext], [Phone2Ext], [Country], [State], [TaxAuthority1], [TaxAuthority2], [MinPOAmount], [Phone1ID], [Phone2ID], [CellID], [FaxID], [DefaultDiscount], [PaymentTerms], [DateCreated], [LastBookingSeq], [Disabled], [VendTypeForExporting], [CellCountryCode], [CellAreaCode], [CellDigits], [VendorReferenceNumber])
    SELECT [ID], [VendorCode], [VendorContact], [VendorName], [Vadr1], [Vadr2], [Vadr3], [Vpostcode], [Vphone1], [Vphone2], [Vfax], [Vemail], [Vwebpage], [Vcurrency], [Vaccno], [AreaCode], [CountryCode], [CallType], [UseAreaCode], [FaxExt], [Phone1Ext], [Phone2Ext], [Country], [State], [TaxAuthority1], [TaxAuthority2], [MinPOAmount], [Phone1ID], [Phone2ID], [CellID], [FaxID], [DefaultDiscount], [PaymentTerms], [DateCreated], [LastBookingSeq], [Disabled], [VendTypeForExporting], [CellCountryCode], [CellAreaCode], [CellDigits], [VendorReferenceNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblVendor]');
    SET IDENTITY_INSERT [tblVendor] OFF;
END
ELSE PRINT 'Skipping tblVendor (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblContactLinks]) = 0
BEGIN
    PRINT 'Importing tblContactLinks (1,739 rows)...';
    SET IDENTITY_INSERT [tblContactLinks] ON;
    INSERT INTO [tblContactLinks] ([ID], [ContactID], [OperatorID])
    SELECT [ID], [ContactID], [OperatorID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblContactLinks]');
    SET IDENTITY_INSERT [tblContactLinks] OFF;
END
ELSE PRINT 'Skipping tblContactLinks (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblVenunote]) = 0
BEGIN
    PRINT 'Importing tblVenunote (1,884 rows)...';
    SET IDENTITY_INSERT [tblVenunote] ON;
    INSERT INTO [tblVenunote] ([ID], [VenueName], [line_no], [text_line], [VenueID], [NoteType])
    SELECT [ID], [VenueName], [line_no], [text_line], [VenueID], [NoteType]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblVenunote]');
    SET IDENTITY_INSERT [tblVenunote] OFF;
END
ELSE PRINT 'Skipping tblVenunote (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblParameters]) = 0
BEGIN
    PRINT 'Importing tblParameters (1,933 rows)...';
    INSERT INTO [tblParameters] ([Name], [Value])
    SELECT [Name], [Value]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblParameters]');
END
ELSE PRINT 'Skipping tblParameters (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblStockTakHistory]) = 0
BEGIN
    PRINT 'Importing tblStockTakHistory (1,936 rows)...';
    SET IDENTITY_INSERT [tblStockTakHistory] ON;
    INSERT INTO [tblStockTakHistory] ([ID], [Product_code], [Stock_Number], [EntryDateTime], [Qty], [Locn], [OperatorID])
    SELECT [ID], [Product_code], [Stock_Number], [EntryDateTime], [Qty], [Locn], [OperatorID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblStockTakHistory]');
    SET IDENTITY_INSERT [tblStockTakHistory] OFF;
END
ELSE PRINT 'Skipping tblStockTakHistory (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLinkVenueContact]) = 0
BEGIN
    PRINT 'Importing tblLinkVenueContact (1,996 rows)...';
    SET IDENTITY_INSERT [tblLinkVenueContact] ON;
    INSERT INTO [tblLinkVenueContact] ([ID], [VenueID], [ContactID])
    SELECT [ID], [VenueID], [ContactID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLinkVenueContact]');
    SET IDENTITY_INSERT [tblLinkVenueContact] OFF;
END
ELSE PRINT 'Skipping tblLinkVenueContact (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPayment]) = 0
BEGIN
    PRINT 'Importing tblPayment (2,051 rows)...';
    SET IDENTITY_INSERT [tblPayment] ON;
    INSERT INTO [tblPayment] ([ID], [customer_code], [booking_seq_no], [invoice_no], [amount], [comment_line], [tax2], [currencyStr], [tax1], [ReceiptNo], [taxauthority1], [DateF], [taxauthority2], [LinkedPaymentID], [FromPrepayment], [Booking_no], [Archived], [QBO_ID], [XeroId])
    SELECT [ID], [customer_code], [booking_seq_no], [invoice_no], [amount], [comment_line], [tax2], [currencyStr], [tax1], [ReceiptNo], [taxauthority1], [DateF], [taxauthority2], [LinkedPaymentID], [FromPrepayment], [Booking_no], [Archived], [QBO_ID], [XeroId]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPayment]');
    SET IDENTITY_INSERT [tblPayment] OFF;
END
ELSE PRINT 'Skipping tblPayment (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblReceipt]) = 0
BEGIN
    PRINT 'Importing tblReceipt (2,053 rows)...';
    SET IDENTITY_INSERT [tblReceipt] ON;
    INSERT INTO [tblReceipt] ([ID], [receiptNo], [booking_no], [amount], [Invoice_no], [division], [tax1], [tax2], [taxauthority1], [taxauthority2], [drawer], [bank], [branch], [cheque_no], [Card_name], [Card_no], [DateF], [cash_type], [CustomerCode], [includes_vat_gstfiller], [BatchNo])
    SELECT [ID], [receiptNo], [booking_no], [amount], [Invoice_no], [division], [tax1], [tax2], [taxauthority1], [taxauthority2], [drawer], [bank], [branch], [cheque_no], [Card_name], [Card_no], [DateF], [cash_type], [CustomerCode], [includes_vat_gstfiller], [BatchNo]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblReceipt]');
    SET IDENTITY_INSERT [tblReceipt] OFF;
END
ELSE PRINT 'Skipping tblReceipt (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblSaleCosts]) = 0
BEGIN
    PRINT 'Importing tblSaleCosts (2,299 rows)...';
    SET IDENTITY_INSERT [tblSaleCosts] ON;
    INSERT INTO [tblSaleCosts] ([ID], [Product_code], [QtyReceived], [UnitCost], [DateReceived], [Locn], [QtyUnsold], [PoNumber])
    SELECT [ID], [Product_code], [QtyReceived], [UnitCost], [DateReceived], [Locn], [QtyUnsold], [PoNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblSaleCosts]');
    SET IDENTITY_INSERT [tblSaleCosts] OFF;
END
ELSE PRINT 'Skipping tblSaleCosts (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblInvmas_PriceBreak]) = 0
BEGIN
    PRINT 'Importing tblInvmas_PriceBreak (2,424 rows)...';
    SET IDENTITY_INSERT [tblInvmas_PriceBreak] ON;
    INSERT INTO [tblInvmas_PriceBreak] ([ID], [tblInvmasID], [break_no], [break_qty], [unit_price])
    SELECT [ID], [tblInvmasID], [break_no], [break_qty], [unit_price]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblInvmas_PriceBreak]');
    SET IDENTITY_INSERT [tblInvmas_PriceBreak] OFF;
END
ELSE PRINT 'Skipping tblInvmas_PriceBreak (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblEvent]) = 0
BEGIN
    PRINT 'Importing tblEvent (2,919 rows)...';
    SET IDENTITY_INSERT [tblEvent] ON;
    INSERT INTO [tblEvent] ([ID], [event_code], [event_desc], [deltime], [rettime], [ShowStartTime], [ShowEndTime], [ShowTerm], [showdaysCharged], [VenueDesc], [venueAdr1], [venueAdr2], [venueAdr3], [VenuePhone1], [VenuePhone2], [VenueFax], [Invoiced], [Invoice_no], [Invoice_amount], [BookingsInvoiced], [Attendees], [Salesperson], [coordinator], [rentalDIsc], [SalesDisc], [WeeklyAdjAmount], [FAPAdjAmount], [FAPApplies], [DelvDate], [RetnDate], [ShowSDate], [ShowEDate], [WeeklyAdjApplies], [DayWeekRate], [State], [Country], [PostCode], [Phone1ID], [Phone2ID], [CellID], [FaxID], [Locn], [VenueContactID], [VenueContactName], [Phone1CountryCode], [Phone1AreaCode], [Phone1Ext], [Phone2CountryCode], [Phone2AreaCode], [Phone2Ext], [FaxCountryCode], [FaxAreaCode], [VenueID], [VenueType], [MBscenario], [MBID], [PrepDateTime], [DeprepDateTime], [UseOptimalEquip])
    SELECT [ID], [event_code], [event_desc], [deltime], [rettime], [ShowStartTime], [ShowEndTime], [ShowTerm], [showdaysCharged], [VenueDesc], [venueAdr1], [venueAdr2], [venueAdr3], [VenuePhone1], [VenuePhone2], [VenueFax], [Invoiced], [Invoice_no], [Invoice_amount], [BookingsInvoiced], [Attendees], [Salesperson], [coordinator], [rentalDIsc], [SalesDisc], [WeeklyAdjAmount], [FAPAdjAmount], [FAPApplies], [DelvDate], [RetnDate], [ShowSDate], [ShowEDate], [WeeklyAdjApplies], [DayWeekRate], [State], [Country], [PostCode], [Phone1ID], [Phone2ID], [CellID], [FaxID], [Locn], [VenueContactID], [VenueContactName], [Phone1CountryCode], [Phone1AreaCode], [Phone1Ext], [Phone2CountryCode], [Phone2AreaCode], [Phone2Ext], [FaxCountryCode], [FaxAreaCode], [VenueID], [VenueType], [MBscenario], [MBID], [PrepDateTime], [DeprepDateTime], [UseOptimalEquip]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblEvent]');
    SET IDENTITY_INSERT [tblEvent] OFF;
END
ELSE PRINT 'Skipping tblEvent (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblFastReportRegionLocationLink]) = 0
BEGIN
    PRINT 'Importing tblFastReportRegionLocationLink (3,317 rows)...';
    INSERT INTO [tblFastReportRegionLocationLink] ([rl_type], [rep_id], [rl_id])
    SELECT [rl_type], [rep_id], [rl_id]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblFastReportRegionLocationLink]');
END
ELSE PRINT 'Skipping tblFastReportRegionLocationLink (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLocking]) = 0
BEGIN
    PRINT 'Importing tblLocking (3,596 rows)...';
    SET IDENTITY_INSERT [tblLocking] ON;
    INSERT INTO [tblLocking] ([ID], [Booking_no], [LockOpsID], [LockOpsDate], [LockOpsTime], [LockType], [Locn], [AssignTo_Lock], [CrewID], [EntryDateTime])
    SELECT [ID], [Booking_no], [LockOpsID], [LockOpsDate], [LockOpsTime], [LockType], [Locn], [AssignTo_Lock], [CrewID], [EntryDateTime]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLocking]');
    SET IDENTITY_INSERT [tblLocking] OFF;
END
ELSE PRINT 'Skipping tblLocking (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblInvmas]) = 0
BEGIN
    PRINT 'Importing tblInvmas (5,053 rows)...';
    SET IDENTITY_INSERT [tblInvmas] ON;
    INSERT INTO [tblInvmas] ([ID], [seq_no], [product_code], [groupFld], [category], [descriptionV6], [first_trans], [product_Config], [product_type_v41], [indiv_hire_sale], [on_hand], [Ord_unit], [re_ord_level], [lead_time], [quantity_on_order], [sales_tax_rate], [cost_price], [retail_price], [wholesale_price], [trade_price], [webCatalog], [unit_weight], [unit_volume], [suppress_from_del_sch], [revenue_code], [components_del], [components_inv], [components_quote], [asset_track], [Notes_exist], [notes_on_quote], [notes_on_del], [notes_on_inv], [DisallowDisc], [PictureFilename], [CountryOfOrigin], [IsInTrashCan], [prodRoadCase], [person_required], [ContactID], [GLCode], [UseWeeklyRate], [isGenericItem], [MfctPartNumber], [NonTrackedBarcode], [DefaultDiscount], [PrintedDesc], [VendorCode], [DefaultDayRateID], [DefaultHourlyRateID], [SubCategory], [lastPurchasePrice], [RegionNumber], [zColor], [rLength], [rWidth], [rHeight], [zModelNo], [cyTurnCosts], [bCustomPrintouts], [CheckoutDoc], [bTestEveryUnit], [UnavailableUntilTested], [TestRequired], [DisallowTransfer], [Location], [EntryDate], [EnforceMinHours], [MinimumHours], [bDisallowRegionTransfer], [DontPrintOnCrossRentPO], [RFIDTag], [OLInternalDesc], [OLExternalDesc], [DefaultVendorCode], [iPictureSize], [LastUpdate], [DefaultRateForUnassigned], [bExpandWhenAdded], [BinLocation], [bExcludeFromDataExport], [iMonthsToDepreciate], [rDailyCostOfOwning], [CustomIcon], [On_handInRack], [MBowner], [taxableExempt], [bAutoCheckout], [ImportTestResultsFrom], [WarehouseActive], [OverridePriceChangeRestriction], [bProductIsFreight], [BasedOnPurchCost], [CAPEXGLCode], [CrossHireGLCode], [HSCode], [AveKwHusage])
    SELECT [ID], [seq_no], [product_code], [groupFld], [category], [descriptionV6], [first_trans], [product_Config], [product_type_v41], [indiv_hire_sale], [on_hand], [Ord_unit], [re_ord_level], [lead_time], [quantity_on_order], [sales_tax_rate], [cost_price], [retail_price], [wholesale_price], [trade_price], [webCatalog], [unit_weight], [unit_volume], [suppress_from_del_sch], [revenue_code], [components_del], [components_inv], [components_quote], [asset_track], [Notes_exist], [notes_on_quote], [notes_on_del], [notes_on_inv], [DisallowDisc], [PictureFilename], [CountryOfOrigin], [IsInTrashCan], [prodRoadCase], [person_required], [ContactID], [GLCode], [UseWeeklyRate], [isGenericItem], [MfctPartNumber], [NonTrackedBarcode], [DefaultDiscount], [PrintedDesc], [VendorCode], [DefaultDayRateID], [DefaultHourlyRateID], [SubCategory], [lastPurchasePrice], [RegionNumber], [zColor], [rLength], [rWidth], [rHeight], [zModelNo], [cyTurnCosts], [bCustomPrintouts], [CheckoutDoc], [bTestEveryUnit], [UnavailableUntilTested], [TestRequired], [DisallowTransfer], [Location], [EntryDate], [EnforceMinHours], [MinimumHours], [bDisallowRegionTransfer], [DontPrintOnCrossRentPO], [RFIDTag], [OLInternalDesc], [OLExternalDesc], [DefaultVendorCode], [iPictureSize], [LastUpdate], [DefaultRateForUnassigned], [bExpandWhenAdded], [BinLocation], [bExcludeFromDataExport], [iMonthsToDepreciate], [rDailyCostOfOwning], [CustomIcon], [On_handInRack], [MBowner], [taxableExempt], [bAutoCheckout], [ImportTestResultsFrom], [WarehouseActive], [OverridePriceChangeRestriction], [bProductIsFreight], [BasedOnPurchCost], [CAPEXGLCode], [CrossHireGLCode], [HSCode], [AveKwHusage]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblInvmas]');
    SET IDENTITY_INSERT [tblInvmas] OFF;
END
ELSE PRINT 'Skipping tblInvmas (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblVenuroom]) = 0
BEGIN
    PRINT 'Importing tblVenuroom (5,852 rows)...';
    SET IDENTITY_INSERT [tblVenuroom] ON;
    INSERT INTO [tblVenuroom] ([ID], [VenueName], [Roomname], [floorplanfilename], [VenueID], [RoomNumber], [MaxCapacity], [CeilingHeight], [FloorNumber])
    SELECT [ID], [VenueName], [Roomname], [floorplanfilename], [VenueID], [RoomNumber], [MaxCapacity], [CeilingHeight], [FloorNumber]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblVenuroom]');
    SET IDENTITY_INSERT [tblVenuroom] OFF;
END
ELSE PRINT 'Skipping tblVenuroom (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblBill]) = 0
BEGIN
    PRINT 'Importing tblBill (6,663 rows)...';
    SET IDENTITY_INSERT [tblBill] ON;
    INSERT INTO [tblBill] ([ID], [parent_code], [product_code], [qty_v5], [sub_seq_no], [variable_part], [ContactID], [SelectComp], [AccessoryDiscount], [AutoResolve], [nestedCompAcc])
    SELECT [ID], [parent_code], [product_code], [qty_v5], [sub_seq_no], [variable_part], [ContactID], [SelectComp], [AccessoryDiscount], [AutoResolve], [nestedCompAcc]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblBill]');
    SET IDENTITY_INSERT [tblBill] OFF;
END
ELSE PRINT 'Skipping tblBill (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblProdnote]) = 0
BEGIN
    PRINT 'Importing tblProdnote (8,443 rows)...';
    SET IDENTITY_INSERT [tblProdnote] ON;
    INSERT INTO [tblProdnote] ([ID], [product_code], [line_no], [text_line], [Notetype], [stock_number])
    SELECT [ID], [product_code], [line_no], [text_line], [Notetype], [stock_number]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblProdnote]');
    SET IDENTITY_INSERT [tblProdnote] OFF;
END
ELSE PRINT 'Skipping tblProdnote (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblBookingAssetHist]) = 0
BEGIN
    PRINT 'Importing tblBookingAssetHist (8,852 rows)...';
    SET IDENTITY_INSERT [tblBookingAssetHist] ON;
    INSERT INTO [tblBookingAssetHist] ([ID], [OpDate], [OpCode], [SoftwareTypeCode], [Description], [Barcode], [OperatorID], [Quantity], [Product_Code], [Subst_Product_Code], [Stock_Number], [BookingNo], [ErrorCode], [AssetTracked], [Pending], [Executed], [Deleted], [SubstitutionType], [ItemTranId], [OperatorNote], [CheckoutSessionId], [ReturnSessionId], [Roadcase_Product_Code], [Roadcase_Stock_Number], [Roadcase_barcode])
    SELECT [ID], [OpDate], [OpCode], [SoftwareTypeCode], [Description], [Barcode], [OperatorID], [Quantity], [Product_Code], [Subst_Product_Code], [Stock_Number], [BookingNo], [ErrorCode], [AssetTracked], [Pending], [Executed], [Deleted], [SubstitutionType], [ItemTranId], [OperatorNote], [CheckoutSessionId], [ReturnSessionId], [Roadcase_Product_Code], [Roadcase_Stock_Number], [Roadcase_barcode]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblBookingAssetHist]');
    SET IDENTITY_INSERT [tblBookingAssetHist] OFF;
END
ELSE PRINT 'Skipping tblBookingAssetHist (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblMaint]) = 0
BEGIN
    PRINT 'Importing tblMaint (8,876 rows)...';
    SET IDENTITY_INSERT [tblMaint] ON;
    INSERT INTO [tblMaint] ([ID], [Product_code], [Serial_number], [Reference], [Repair_details], [Labour], [Material], [Supplier_code], [DateF], [Stock_Number], [OutDate], [ReturnDate], [DamagedFaulty], [IncludeOnreport], [Booking_no], [OutTime], [ReturnTime], [AssetStatus], [bIsHistoryItem], [EntryLocn], [OperatorID], [ReturnOperatorID], [parent_id], [ReturnProcess], [LastModByOpID])
    SELECT [ID], [Product_code], [Serial_number], [Reference], [Repair_details], [Labour], [Material], [Supplier_code], [DateF], [Stock_Number], [OutDate], [ReturnDate], [DamagedFaulty], [IncludeOnreport], [Booking_no], [OutTime], [ReturnTime], [AssetStatus], [bIsHistoryItem], [EntryLocn], [OperatorID], [ReturnOperatorID], [parent_id], [ReturnProcess], [LastModByOpID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblMaint]');
    SET IDENTITY_INSERT [tblMaint] OFF;
END
ELSE PRINT 'Skipping tblMaint (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPo]) = 0
BEGIN
    PRINT 'Importing tblPo (11,332 rows)...';
    SET IDENTITY_INSERT [tblPo] ON;
    INSERT INTO [tblPo] ([ID], [PVendorCode], [PPONumber], [PpostedToOnOrd], [PReceived], [Pdelto], [PdeliveryAdr1], [PdeliveryAdr2], [PdeliveryAdr3], [PPostcode], [PdeliverVia], [PtotalAmount], [PtaxAmount], [PtaxTitle], [ProjectCode], [PrintNotesOnPO], [Otherdesc], [freight], [TheirOurNumber], [Archaived], [POBooking_no], [PaymentTermsDays], [DiscountPerc], [OrderDate], [ExpectedDate], [CrossRental], [State], [Country], [Phone], [Fax], [Contact], [OrderedBy], [RequestedBy], [Location], [TaxAuthority1], [TaxAuthority2], [Tax1Value], [Tax2Value], [Approved], [AppByOpID], [ApprovalDate], [ApprovedAmount], [InvoiceStatus], [CrewAdded], [Phone1ID], [FaxID], [AirBill], [Description], [UndiscountedAmount], [DiscountedAmount], [PhoneCountryCode], [PhoneAreaCode], [PhoneExt], [FaxCountryCode], [FaxAreaCode], [bReviewStatus], [ActualPOCurrency], [bIncludeOnSchedule], [zPOPickupTime], [Phone2ID], [CellID], [MonthYearFilter], [POPrefix])
    SELECT [ID], [PVendorCode], [PPONumber], [PpostedToOnOrd], [PReceived], [Pdelto], [PdeliveryAdr1], [PdeliveryAdr2], [PdeliveryAdr3], [PPostcode], [PdeliverVia], [PtotalAmount], [PtaxAmount], [PtaxTitle], [ProjectCode], [PrintNotesOnPO], [Otherdesc], [freight], [TheirOurNumber], [Archaived], [POBooking_no], [PaymentTermsDays], [DiscountPerc], [OrderDate], [ExpectedDate], [CrossRental], [State], [Country], [Phone], [Fax], [Contact], [OrderedBy], [RequestedBy], [Location], [TaxAuthority1], [TaxAuthority2], [Tax1Value], [Tax2Value], [Approved], [AppByOpID], [ApprovalDate], [ApprovedAmount], [InvoiceStatus], [CrewAdded], [Phone1ID], [FaxID], [AirBill], [Description], [UndiscountedAmount], [DiscountedAmount], [PhoneCountryCode], [PhoneAreaCode], [PhoneExt], [FaxCountryCode], [FaxAreaCode], [bReviewStatus], [ActualPOCurrency], [bIncludeOnSchedule], [zPOPickupTime], [Phone2ID], [CellID], [MonthYearFilter], [POPrefix]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPo]');
    SET IDENTITY_INSERT [tblPo] OFF;
END
ELSE PRINT 'Skipping tblPo (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPoline]) = 0
BEGIN
    PRINT 'Importing tblPoline (12,060 rows)...';
    SET IDENTITY_INSERT [tblPoline] ON;
    INSERT INTO [tblPoline] ([ID], [LPOnumber], [LProductCode], [LlineType], [LFFtext], [Lquantity], [LunitPrice], [Lprice], [UnitMessurement], [LquantityReceived], [LineDiscount], [PONumber], [LineNumber], [PartNumber], [RevenueCode], [bit_field_v41], [item_type], [PackageLevel], [sub_seq_no])
    SELECT [ID], [LPOnumber], [LProductCode], [LlineType], [LFFtext], [Lquantity], [LunitPrice], [Lprice], [UnitMessurement], [LquantityReceived], [LineDiscount], [PONumber], [LineNumber], [PartNumber], [RevenueCode], [bit_field_v41], [item_type], [PackageLevel], [sub_seq_no]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPoline]');
    SET IDENTITY_INSERT [tblPoline] OFF;
END
ELSE PRINT 'Skipping tblPoline (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblMaintenanceNotes]) = 0
BEGIN
    PRINT 'Importing tblMaintenanceNotes (15,939 rows)...';
    SET IDENTITY_INSERT [tblMaintenanceNotes] ON;
    INSERT INTO [tblMaintenanceNotes] ([ID], [MaintenanceID], [LineNumber], [TextLine], [NoteType])
    SELECT [ID], [MaintenanceID], [LineNumber], [TextLine], [NoteType]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblMaintenanceNotes]');
    SET IDENTITY_INSERT [tblMaintenanceNotes] OFF;
END
ELSE PRINT 'Skipping tblMaintenanceNotes (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCust]) = 0
BEGIN
    PRINT 'Importing tblCust (16,199 rows)...';
    SET IDENTITY_INSERT [tblCust] ON;
    INSERT INTO [tblCust] ([ID], [Customer_code], [PostalAddress1], [PostalAddress2], [PostalAddress3], [postalPostCode], [currencyStr], [UsesPriceTableV71], [post_code], [sales_tax_no], [Account_type], [industry_type], [insurance_type], [hire_tax_exempt], [TaxAuthority1], [Price_customer_pays], [customer_number], [stop_credit], [Last_bk_seq], [Credit_limit], [Current], [Seven_days], [Fourteen_days], [Twenty_one_days], [payments_mtd], [discount_rate], [last_pmt_amt], [account_is_zero], [Monthly_cycle_billing_basis], [salesperson], [taxAuthority2], [contactV6], [OrganisationV6], [Address_l1V6], [Address_l2V6], [Address_l3V6], [webAddress], [emailAddress], [Paymethod], [lastTranDate], [lastPmtDate], [lastBalupDate], [firstUnpayInvDate], [SalesAssignEndDate], [CustCDate], [FirstInvDate], [DisabledCust], [AcctMgr], [IndustryDescription], [Field1], [Field2], [Field3], [Field4], [Field5], [Field6], [Field7], [Field8], [Field9], [Field10], [Field11], [Field12], [Field13], [Field14], [Field15], [Field16], [Field17], [Field18], [Field19], [Field20], [Field21], [Field22], [Field23], [Field24], [Field25], [Field26], [Field27], [Field28], [Field29], [Field30], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardHolder], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [Field31], [Field32], [Phone2Ext], [TwoWkDisc], [ThreeWkDisc], [StreetCountry], [StreetState], [PostalCountry], [PostalState], [InsuranceCertificate], [InsuredAmount], [InsuredFromDate], [InsuredToDate], [iLink_ContactID], [Phone1ID], [Phone2ID], [FaxID], [bPONumRequired], [CustomerType], [CampaignID], [DefaultCustomerDivision], [FaxCallType], [FaxCountryCode], [FaxAreaCode], [FaxDigits], [FaxDialAreaCode], [Phone1CountryCode], [Phone1AreaCode], [Phone1Digits], [Phone1Ext], [Phone2CountryCode], [Phone2AreaCode], [Phone2Digits], [EnteredByOpID], [bCustomTemplateList], [AREmailAddress], [CustTypeForExporting], [Phone], [fax], [CellCountryCode], [CellAreaCode], [CellDigits], [PaymentContactID], [QBO_id], [StripeID], [isVendor], [MinPOAmount], [Vaccno], [freightzone], [DefaultBookingContactID], [XeroId])
    SELECT [ID], [Customer_code], [PostalAddress1], [PostalAddress2], [PostalAddress3], [postalPostCode], [currencyStr], [UsesPriceTableV71], [post_code], [sales_tax_no], [Account_type], [industry_type], [insurance_type], [hire_tax_exempt], [TaxAuthority1], [Price_customer_pays], [customer_number], [stop_credit], [Last_bk_seq], [Credit_limit], [Current], [Seven_days], [Fourteen_days], [Twenty_one_days], [payments_mtd], [discount_rate], [last_pmt_amt], [account_is_zero], [Monthly_cycle_billing_basis], [salesperson], [taxAuthority2], [contactV6], [OrganisationV6], [Address_l1V6], [Address_l2V6], [Address_l3V6], [webAddress], [emailAddress], [Paymethod], [lastTranDate], [lastPmtDate], [lastBalupDate], [firstUnpayInvDate], [SalesAssignEndDate], [CustCDate], [FirstInvDate], [DisabledCust], [AcctMgr], [IndustryDescription], [Field1], [Field2], [Field3], [Field4], [Field5], [Field6], [Field7], [Field8], [Field9], [Field10], [Field11], [Field12], [Field13], [Field14], [Field15], [Field16], [Field17], [Field18], [Field19], [Field20], [Field21], [Field22], [Field23], [Field24], [Field25], [Field26], [Field27], [Field28], [Field29], [Field30], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardHolder], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [Field31], [Field32], [Phone2Ext], [TwoWkDisc], [ThreeWkDisc], [StreetCountry], [StreetState], [PostalCountry], [PostalState], [InsuranceCertificate], [InsuredAmount], [InsuredFromDate], [InsuredToDate], [iLink_ContactID], [Phone1ID], [Phone2ID], [FaxID], [bPONumRequired], [CustomerType], [CampaignID], [DefaultCustomerDivision], [FaxCallType], [FaxCountryCode], [FaxAreaCode], [FaxDigits], [FaxDialAreaCode], [Phone1CountryCode], [Phone1AreaCode], [Phone1Digits], [Phone1Ext], [Phone2CountryCode], [Phone2AreaCode], [Phone2Digits], [EnteredByOpID], [bCustomTemplateList], [AREmailAddress], [CustTypeForExporting], [Phone], [fax], [CellCountryCode], [CellAreaCode], [CellDigits], [PaymentContactID], [QBO_id], [StripeID], [isVendor], [MinPOAmount], [Vaccno], [freightzone], [DefaultBookingContactID], [XeroId]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCust]');
    SET IDENTITY_INSERT [tblCust] OFF;
END
ELSE PRINT 'Skipping tblCust (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCancelBR]) = 0
BEGIN
    PRINT 'Importing tblCancelBR (18,755 rows)...';
    SET IDENTITY_INSERT [tblCancelBR] ON;
    INSERT INTO [tblCancelBR] ([ID], [Reason], [booking_no], [StatusBeforeCan], [price_quoted])
    SELECT [ID], [Reason], [booking_no], [StatusBeforeCan], [price_quoted]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCancelBR]');
    SET IDENTITY_INSERT [tblCancelBR] OFF;
END
ELSE PRINT 'Skipping tblCancelBR (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblInvmas_Labour_Rates]) = 0
BEGIN
    PRINT 'Importing tblInvmas_Labour_Rates (22,000 rows)...';
    SET IDENTITY_INSERT [tblInvmas_Labour_Rates] ON;
    INSERT INTO [tblInvmas_Labour_Rates] ([ID], [tblInvmasID], [rate_no], [Labour_rate], [Locn], [IsDefault], [DefaultRate])
    SELECT [ID], [tblInvmasID], [rate_no], [Labour_rate], [Locn], [IsDefault], [DefaultRate]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblInvmas_Labour_Rates]');
    SET IDENTITY_INSERT [tblInvmas_Labour_Rates] OFF;
END
ELSE PRINT 'Skipping tblInvmas_Labour_Rates (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPhones]) = 0
BEGIN
    PRINT 'Importing tblPhones (23,440 rows)...';
    SET IDENTITY_INSERT [tblPhones] ON;
    INSERT INTO [tblPhones] ([ID], [PhoneType], [CallType], [DialAreaCode], [CountryCode], [AreaCode], [Digits], [Extension], [PhoneCode], [PType])
    SELECT [ID], [PhoneType], [CallType], [DialAreaCode], [CountryCode], [AreaCode], [Digits], [Extension], [PhoneCode], [PType]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPhones]');
    SET IDENTITY_INSERT [tblPhones] OFF;
END
ELSE PRINT 'Skipping tblPhones (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblVenues]) = 0
BEGIN
    PRINT 'Importing tblVenues (23,954 rows)...';
    SET IDENTITY_INSERT [tblVenues] ON;
    INSERT INTO [tblVenues] ([ID], [VenueName], [ContactName], [ContactID], [WebPage], [Address1], [Address2], [City], [State], [Country], [ZipCode], [Phone1CountryCode], [Phone1AreaCode], [Phone1Digits], [Phone1Ext], [Phone2CountryCode], [Phone2AreaCode], [Phone2Digits], [Phone2Ext], [FaxCountryCode], [FaxAreaCode], [FaxDigits], [Type], [BookingNo], [VenueNickname], [VenueTextType], [DefaultFolder], [CellCountryCode], [CellAreaCode], [CellDigits])
    SELECT [ID], [VenueName], [ContactName], [ContactID], [WebPage], [Address1], [Address2], [City], [State], [Country], [ZipCode], [Phone1CountryCode], [Phone1AreaCode], [Phone1Digits], [Phone1Ext], [Phone2CountryCode], [Phone2AreaCode], [Phone2Digits], [Phone2Ext], [FaxCountryCode], [FaxAreaCode], [FaxDigits], [Type], [BookingNo], [VenueNickname], [VenueTextType], [DefaultFolder], [CellCountryCode], [CellAreaCode], [CellDigits]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblVenues]');
    SET IDENTITY_INSERT [tblVenues] OFF;
END
ELSE PRINT 'Skipping tblVenues (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLinkCustContact]) = 0
BEGIN
    PRINT 'Importing tblLinkCustContact (24,504 rows)...';
    SET IDENTITY_INSERT [tblLinkCustContact] ON;
    INSERT INTO [tblLinkCustContact] ([ID], [Customer_Code], [ContactID])
    SELECT [ID], [Customer_Code], [ContactID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLinkCustContact]');
    SET IDENTITY_INSERT [tblLinkCustContact] OFF;
END
ELSE PRINT 'Skipping tblLinkCustContact (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblContact]) = 0
BEGIN
    PRINT 'Importing tblContact (24,609 rows)...';
    SET IDENTITY_INSERT [tblContact] ON;
    INSERT INTO [tblContact] ([ID], [CustCodeLink], [Contactname], [firstname], [surname], [nameKeyField], [position], [busname], [Adr1], [Adr2], [Adr3], [Postcode], [Phone1], [Phone2], [Fax], [Webpage], [Email], [driversLicNo], [OtherID], [specialty], [PictureDatafile], [lastBookDate], [MidName], [Cell], [Ext1], [Ext2], [Active], [MailList], [DecMaker], [LastContact], [LastAttempt], [Department], [SourceID], [CreateDate], [LastUpdate], [ReferralName], [Field1], [Field2], [Field3], [Field4], [Field5], [Field6], [Field7], [Field8], [AskFor], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [Sendme_faxes], [Sendme_emails], [CardHolder_Name], [SalesPerson_Code], [SalesAssignEndDate], [Country], [State], [Phone1ID], [Phone2ID], [CellID], [FaxID], [bDriver], [bFreeLanceContact], [JobTitle], [Phone1CountryCode], [Phone1AreaCode], [Phone2CountryCode], [Phone2AreaCode], [FaxCountryCode], [FaxAreaCode], [CellCountryCode], [CellAreaCode], [FaxDialAreaCode], [FaxCallType], [SubRentalVendor], [AgencyContact], [UpdateVendorContact], [username], [password], [TimeZone], [RPwebservicesActive], [RPWSDefaultOpID], [CultureInt], [ProjectManager], [Utc], [field9], [field10], [field11], [field12], [field13], [field14], [field15], [RPWSCrewManager])
    SELECT [ID], [CustCodeLink], [Contactname], [firstname], [surname], [nameKeyField], [position], [busname], [Adr1], [Adr2], [Adr3], [Postcode], [Phone1], [Phone2], [Fax], [Webpage], [Email], [driversLicNo], [OtherID], [specialty], [PictureDatafile], [lastBookDate], [MidName], [Cell], [Ext1], [Ext2], [Active], [MailList], [DecMaker], [LastContact], [LastAttempt], [Department], [SourceID], [CreateDate], [LastUpdate], [ReferralName], [Field1], [Field2], [Field3], [Field4], [Field5], [Field6], [Field7], [Field8], [AskFor], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [Sendme_faxes], [Sendme_emails], [CardHolder_Name], [SalesPerson_Code], [SalesAssignEndDate], [Country], [State], [Phone1ID], [Phone2ID], [CellID], [FaxID], [bDriver], [bFreeLanceContact], [JobTitle], [Phone1CountryCode], [Phone1AreaCode], [Phone2CountryCode], [Phone2AreaCode], [FaxCountryCode], [FaxAreaCode], [CellCountryCode], [CellAreaCode], [FaxDialAreaCode], [FaxCallType], [SubRentalVendor], [AgencyContact], [UpdateVendorContact], [username], [password], [TimeZone], [RPwebservicesActive], [RPWSDefaultOpID], [CultureInt], [ProjectManager], [Utc], [field9], [field10], [field11], [field12], [field13], [field14], [field15], [RPWSCrewManager]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblContact]');
    SET IDENTITY_INSERT [tblContact] OFF;
END
ELSE PRINT 'Skipping tblContact (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblPonote]) = 0
BEGIN
    PRINT 'Importing tblPonote (26,246 rows)...';
    SET IDENTITY_INSERT [tblPonote] ON;
    INSERT INTO [tblPonote] ([ID], [code], [line_no], [text_line])
    SELECT [ID], [code], [line_no], [text_line]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblPonote]');
    SET IDENTITY_INSERT [tblPonote] OFF;
END
ELSE PRINT 'Skipping tblPonote (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblRatetbl]) = 0
BEGIN
    PRINT 'Importing tblRatetbl (38,570 rows)...';
    SET IDENTITY_INSERT [tblRatetbl] ON;
    INSERT INTO [tblRatetbl] ([ID], [ProductCode], [tableNo], [hourly_rate], [half_day], [rate_1st_day], [rate_extra_days], [rate_week], [rate_long_term], [deposit], [damage_waiver_rate], [DayWeekRate], [MinimumRental], [ReplacementValue], [Rate3rdDay], [Rate4thDay], [Rate2ndWeek], [Rate3rdWeek], [RateAdditionalMonth], [RatePrep], [RateWrap], [RatePreLight])
    SELECT [ID], [ProductCode], [tableNo], [hourly_rate], [half_day], [rate_1st_day], [rate_extra_days], [rate_week], [rate_long_term], [deposit], [damage_waiver_rate], [DayWeekRate], [MinimumRental], [ReplacementValue], [Rate3rdDay], [Rate4thDay], [Rate2ndWeek], [Rate3rdWeek], [RateAdditionalMonth], [RatePrep], [RateWrap], [RatePreLight]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblRatetbl]');
    SET IDENTITY_INSERT [tblRatetbl] OFF;
END
ELSE PRINT 'Skipping tblRatetbl (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblInvhead]) = 0
BEGIN
    PRINT 'Importing tblInvhead (47,043 rows)...';
    SET IDENTITY_INSERT [tblInvhead] ON;
    INSERT INTO [tblInvhead] ([ID], [Invoice_cred_no], [Customer_code], [inv_cred_note], [Taxauthority1], [Taxauthority2], [Booking_seq_no], [Invoice_amount], [payment_type], [hire_price], [delivery], [percent_disc], [labour], [discount_rate], [insurance_v5], [un_disc_amount], [sales_discount_rate], [sales_amount], [tax1], [sundry_total], [Discount_value], [Sales_undisc_amt], [division], [booking_type], [losses], [first_month], [credit_amount], [tax2], [Government], [comment_line], [event_code], [sale_of_asset], [currencyStr], [InvDate], [RenInvSDate], [RenInvEDate], [CredDate], [StageNo], [Inv_ToDate], [Booking_No], [CreditSurchargeAmount], [EventManagementAmount], [Location], [CustomCreditNoteNumber], [TaxabPCT], [UntaxPCT], [Tax1PCT], [Tax2PCT], [Booking_amount], [webcode], [TaxExemptLabour], [Archived], [SubRentPrice], [exportedToAcc], [exportedDateTime], [expLinkfield], [Paid], [InvoiceCreationDate], [DepositInvoice], [XeroId])
    SELECT [ID], [Invoice_cred_no], [Customer_code], [inv_cred_note], [Taxauthority1], [Taxauthority2], [Booking_seq_no], [Invoice_amount], [payment_type], [hire_price], [delivery], [percent_disc], [labour], [discount_rate], [insurance_v5], [un_disc_amount], [sales_discount_rate], [sales_amount], [tax1], [sundry_total], [Discount_value], [Sales_undisc_amt], [division], [booking_type], [losses], [first_month], [credit_amount], [tax2], [Government], [comment_line], [event_code], [sale_of_asset], [currencyStr], [InvDate], [RenInvSDate], [RenInvEDate], [CredDate], [StageNo], [Inv_ToDate], [Booking_No], [CreditSurchargeAmount], [EventManagementAmount], [Location], [CustomCreditNoteNumber], [TaxabPCT], [UntaxPCT], [Tax1PCT], [Tax2PCT], [Booking_amount], [webcode], [TaxExemptLabour], [Archived], [SubRentPrice], [exportedToAcc], [exportedDateTime], [expLinkfield], [Paid], [InvoiceCreationDate], [DepositInvoice], [XeroId]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblInvhead]');
    SET IDENTITY_INSERT [tblInvhead] OFF;
END
ELSE PRINT 'Skipping tblInvhead (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblAsset01]) = 0
BEGIN
    PRINT 'Importing tblAsset01 (47,754 rows)...';
    SET IDENTITY_INSERT [tblAsset01] ON;
    INSERT INTO [tblAsset01] ([ID], [ASSET_CODE], [DESCRIPTION], [PRODUCT_COde], [STOCK_NUMBER], [SERIAL_NO], [COST], [EST_RESALE], [Disposal_AMT], [REVAL_TD], [INSURER], [INSURED_VAL], [BOOKING_NO], [DEL_TIME_H], [DEL_TIME_M], [RET_TIME_H], [RET_TIME_M], [TIMES_HIRE], [AMOUNT_LTD], [days_IN_Service], [days_REQ_service], [METHOD_TAX], [DEPN_RATE_tax], [ACCUM_DEPN_tax], [YTD_DEPN_Tax], [DEPN_LY_TAx], [WRTN_DOWN_val_tax], [warehouse_time_h], [wareHouse_time_m], [times_hired_1_4td], [current_1_4], [locn], [modelNumber], [PurDate], [StartDate], [DisDate], [DelDate], [RetDate], [LastTaxDate], [WareDate], [PONumber], [ReturnFromservice], [ServiceStatus], [VendorV8], [KeepStatus], [NextTestDate], [LastTestDate], [OperationalStatus], [TestFrequencyDays], [Financier], [FinanceStartDate], [FinanceEndDate], [ContractNo], [RepayAmount], [FinanceType], [FinanceTotal], [RFIDTag], [RevCenterLocn], [iDisposalType], [SeqNo], [LastTestResultsImportedFrom], [HomeLocn], [PCode], [NavCode], [PackStatus], [latitude], [longitude], [LOCATION])
    SELECT [ID], [ASSET_CODE], [DESCRIPTION], [PRODUCT_COde], [STOCK_NUMBER], [SERIAL_NO], [COST], [EST_RESALE], [Disposal_AMT], [REVAL_TD], [INSURER], [INSURED_VAL], [BOOKING_NO], [DEL_TIME_H], [DEL_TIME_M], [RET_TIME_H], [RET_TIME_M], [TIMES_HIRE], [AMOUNT_LTD], [days_IN_Service], [days_REQ_service], [METHOD_TAX], [DEPN_RATE_tax], [ACCUM_DEPN_tax], [YTD_DEPN_Tax], [DEPN_LY_TAx], [WRTN_DOWN_val_tax], [warehouse_time_h], [wareHouse_time_m], [times_hired_1_4td], [current_1_4], [locn], [modelNumber], [PurDate], [StartDate], [DisDate], [DelDate], [RetDate], [LastTaxDate], [WareDate], [PONumber], [ReturnFromservice], [ServiceStatus], [VendorV8], [KeepStatus], [NextTestDate], [LastTestDate], [OperationalStatus], [TestFrequencyDays], [Financier], [FinanceStartDate], [FinanceEndDate], [ContractNo], [RepayAmount], [FinanceType], [FinanceTotal], [RFIDTag], [RevCenterLocn], [iDisposalType], [SeqNo], [LastTestResultsImportedFrom], [HomeLocn], [PCode], [NavCode], [PackStatus], [latitude], [longitude], [LOCATION]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblAsset01]');
    SET IDENTITY_INSERT [tblAsset01] OFF;
END
ELSE PRINT 'Skipping tblAsset01 (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblBookingCrewInfo]) = 0
BEGIN
    PRINT 'Importing tblBookingCrewInfo (61,425 rows)...';
    SET IDENTITY_INSERT [tblBookingCrewInfo] ON;
    INSERT INTO [tblBookingCrewInfo] ([ID], [Booking_no], [CrewChief], [CrewChiefID], [CustomListField], [GeneralLocation], [CustomInt], [DressCode], [CustomReal], [CustomDateTime], [CustomString])
    SELECT [ID], [Booking_no], [CrewChief], [CrewChiefID], [CustomListField], [GeneralLocation], [CustomInt], [DressCode], [CustomReal], [CustomDateTime], [CustomString]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblBookingCrewInfo]');
    SET IDENTITY_INSERT [tblBookingCrewInfo] OFF;
END
ELSE PRINT 'Skipping tblBookingCrewInfo (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblHeading]) = 0
BEGIN
    PRINT 'Importing tblHeading (65,652 rows)...';
    SET IDENTITY_INSERT [tblHeading] ON;
    INSERT INTO [tblHeading] ([ID], [booking_no], [heading_no], [del_date], [del_time_h], [del_time_m], [ret_date], [ret_time_h], [ret_time_m], [days_using], [days_charged_filler], [heading_desc], [Own_equip], [equip_hire_tot], [labour_tot], [sales_tot], [hire_disc_perc], [sale_disc_perc], [hire_disc_amt], [sale_disc_amt], [sundry_tot], [sundry_cost_tot], [hire_undisc_amt], [duty], [tax1_tot], [losses], [DelvDate], [RetnDate], [days_charged_v51], [NodeCollapsed], [venueroomID], [VenueType], [BookedDateTime], [BookedTimeH], [BookedTimeM], [BookedTimeS], [view_client], [view_logi], [Logi_HeadingNo], [hasDates], [BayNo], [sales_undisc_amt], [RentalLineDiscountAmt], [SalesLineDiscountAmt])
    SELECT [ID], [booking_no], [heading_no], [del_date], [del_time_h], [del_time_m], [ret_date], [ret_time_h], [ret_time_m], [days_using], [days_charged_filler], [heading_desc], [Own_equip], [equip_hire_tot], [labour_tot], [sales_tot], [hire_disc_perc], [sale_disc_perc], [hire_disc_amt], [sale_disc_amt], [sundry_tot], [sundry_cost_tot], [hire_undisc_amt], [duty], [tax1_tot], [losses], [DelvDate], [RetnDate], [days_charged_v51], [NodeCollapsed], [venueroomID], [VenueType], [BookedDateTime], [BookedTimeH], [BookedTimeM], [BookedTimeS], [view_client], [view_logi], [Logi_HeadingNo], [hasDates], [BayNo], [sales_undisc_amt], [RentalLineDiscountAmt], [SalesLineDiscountAmt]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblHeading]');
    SET IDENTITY_INSERT [tblHeading] OFF;
END
ELSE PRINT 'Skipping tblHeading (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblbooknote]) = 0
BEGIN
    PRINT 'Importing tblbooknote (72,842 rows)...';
    SET IDENTITY_INSERT [tblbooknote] ON;
    INSERT INTO [tblbooknote] ([ID], [bookingNo], [line_no], [text_line], [NoteType], [OperatorID])
    SELECT [ID], [bookingNo], [line_no], [text_line], [NoteType], [OperatorID]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblbooknote]');
    SET IDENTITY_INSERT [tblbooknote] OFF;
END
ELSE PRINT 'Skipping tblbooknote (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblbookings]) = 0
BEGIN
    PRINT 'Importing tblbookings (78,952 rows)...';
    SET IDENTITY_INSERT [tblbookings] ON;
    INSERT INTO [tblbookings] ([ID], [booking_no], [order_no], [payment_type], [deposit_quoted_v50], [price_quoted], [docs_produced], [hire_price], [booking_type_v32], [status], [delivery], [percent_disc], [delivery_viav71], [delivery_time], [pickup_viaV71], [pickup_time], [invoiced], [labour], [invoice_no], [event_code], [discount_rate], [same_address], [insurance_v5], [days_using], [un_disc_amount], [del_time_h], [del_time_m], [ret_time_h], [ret_time_m], [Item_cnt], [sales_discount_rate], [sales_amount], [tax1], [division], [contact_nameV6], [sales_tax_no], [last_modified_by], [delivery_address_exist], [sales_percent_disc], [pricing_scheme_used], [days_charged_v51], [sale_of_asset], [From_locn], [return_to_locn], [retail_value], [perm_casual], [setupTimeV61], [RehearsalTime], [StrikeTime], [Trans_to_locn], [showStartTime], [ShowEndTime], [transferNo], [currencyStr], [BookingProgressStatus], [ConfirmedBy], [ConfirmedDocRef], [VenueRoom], [expAttendees], [HourBooked], [MinBooked], [SecBooked], [TaxAuthority1], [TaxAuthority2], [HorCCroom], [subrooms], [truckOut], [truckIn], [tripOut], [tripIn], [showName], [freightServiceDel], [freightServiceRet], [DelZone], [RetZone], [OurNumberDel], [OurNumberRet], [DatesAndTimesEnabled], [Government], [prep_time_h], [prep_entered], [prep_time_m], [sales_undisc_amount], [losses], [half_day_aplic], [ContactLoadedIntoVenue], [Assigned_to_v61], [sundry_total], [OrganizationV6], [Salesperson], [order_date], [dDate], [rDate], [Inv_date], [ShowSdate], [ShowEdate], [SetDate], [ADelDate], [SDate], [RehDate], [ConDate], [TOutDate], [TInDate], [PreDate], [ConByDate], [bookingPrinted], [CustCode], [ExtendedFrom], [last_operators], [operatorsID], [PotPercent], [Referral], [EventType], [Priority], [InvoiceStage], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardHolder], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [PickupRetDate], [rent_invd_too_date], [MaxBookingValue], [UsesPriceTable], [DateToInvoice], [TwoWkDisc], [ThreeWkDisc], [ServCont], [PaymentOptions], [PrintedPayTerm], [RentalType], [UseBillSchedule], [Tax2], [ContactID], [ShortHours], [ProjectManager], [dtExpected_ReturnDate], [vcExpected_ReturnTime], [vcTruckOutTime], [vcTruckInTime], [CustID], [VenueID], [LateChargesApplied], [shortagesAreTransfered], [VenueContactID], [VenueContact], [VenueContactPhoneID], [LTBillingOption], [DressCode], [Collection], [FuelSurchargeRate], [FreightLocked], [LabourLocked], [RentalLocked], [PriceLocked], [insurance_type], [EntryDate], [CreditSurchargeRate], [CreditSurchargeAmount], [DisableTreeOrder], [ConfirmationFinancials], [EventManagementRate], [EventManagementAmount], [EquipmentModified], [CrewStatusColumn], [LoadDateTime], [UnloadDateTime], [DeprepDateTime], [DeprepOn], [DeliveryDateOn], [PickupDateOn], [ScheduleDatesOn], [bBookingIsComplete], [DiscountOverride], [MasterBillingID], [MasterBillingMethod], [schedHeadEquipSpan], [TaxabPCT], [UntaxPCT], [Tax1PCT], [Tax2PCT], [PaymentContactID], [sale_of_asset_undisc_amt], [LockedForScanning], [OldAssignedTo], [DateLastModified], [crew_cnt], [rTargetMargin], [rProfitMargin], [ContractNo], [SyncType], [AllLocnAvail], [HasQT], [HasDAT], [AllHeadingsDaysOverride], [printedDate], [BayNo], [Paymethod])
    SELECT [ID], [booking_no], [order_no], [payment_type], [deposit_quoted_v50], [price_quoted], [docs_produced], [hire_price], [booking_type_v32], [status], [delivery], [percent_disc], [delivery_viav71], [delivery_time], [pickup_viaV71], [pickup_time], [invoiced], [labour], [invoice_no], [event_code], [discount_rate], [same_address], [insurance_v5], [days_using], [un_disc_amount], [del_time_h], [del_time_m], [ret_time_h], [ret_time_m], [Item_cnt], [sales_discount_rate], [sales_amount], [tax1], [division], [contact_nameV6], [sales_tax_no], [last_modified_by], [delivery_address_exist], [sales_percent_disc], [pricing_scheme_used], [days_charged_v51], [sale_of_asset], [From_locn], [return_to_locn], [retail_value], [perm_casual], [setupTimeV61], [RehearsalTime], [StrikeTime], [Trans_to_locn], [showStartTime], [ShowEndTime], [transferNo], [currencyStr], [BookingProgressStatus], [ConfirmedBy], [ConfirmedDocRef], [VenueRoom], [expAttendees], [HourBooked], [MinBooked], [SecBooked], [TaxAuthority1], [TaxAuthority2], [HorCCroom], [subrooms], [truckOut], [truckIn], [tripOut], [tripIn], [showName], [freightServiceDel], [freightServiceRet], [DelZone], [RetZone], [OurNumberDel], [OurNumberRet], [DatesAndTimesEnabled], [Government], [prep_time_h], [prep_entered], [prep_time_m], [sales_undisc_amount], [losses], [half_day_aplic], [ContactLoadedIntoVenue], [Assigned_to_v61], [sundry_total], [OrganizationV6], [Salesperson], [order_date], [dDate], [rDate], [Inv_date], [ShowSdate], [ShowEdate], [SetDate], [ADelDate], [SDate], [RehDate], [ConDate], [TOutDate], [TInDate], [PreDate], [ConByDate], [bookingPrinted], [CustCode], [ExtendedFrom], [last_operators], [operatorsID], [PotPercent], [Referral], [EventType], [Priority], [InvoiceStage], [CreditCardName], [CreditCardNumber], [expMonth], [expYear], [CardHolder], [CardStreet1], [CardStreet2], [CardCity], [CardState], [CardPostCode], [CreditCardIdNo], [PickupRetDate], [rent_invd_too_date], [MaxBookingValue], [UsesPriceTable], [DateToInvoice], [TwoWkDisc], [ThreeWkDisc], [ServCont], [PaymentOptions], [PrintedPayTerm], [RentalType], [UseBillSchedule], [Tax2], [ContactID], [ShortHours], [ProjectManager], [dtExpected_ReturnDate], [vcExpected_ReturnTime], [vcTruckOutTime], [vcTruckInTime], [CustID], [VenueID], [LateChargesApplied], [shortagesAreTransfered], [VenueContactID], [VenueContact], [VenueContactPhoneID], [LTBillingOption], [DressCode], [Collection], [FuelSurchargeRate], [FreightLocked], [LabourLocked], [RentalLocked], [PriceLocked], [insurance_type], [EntryDate], [CreditSurchargeRate], [CreditSurchargeAmount], [DisableTreeOrder], [ConfirmationFinancials], [EventManagementRate], [EventManagementAmount], [EquipmentModified], [CrewStatusColumn], [LoadDateTime], [UnloadDateTime], [DeprepDateTime], [DeprepOn], [DeliveryDateOn], [PickupDateOn], [ScheduleDatesOn], [bBookingIsComplete], [DiscountOverride], [MasterBillingID], [MasterBillingMethod], [schedHeadEquipSpan], [TaxabPCT], [UntaxPCT], [Tax1PCT], [Tax2PCT], [PaymentContactID], [sale_of_asset_undisc_amt], [LockedForScanning], [OldAssignedTo], [DateLastModified], [crew_cnt], [rTargetMargin], [rProfitMargin], [ContractNo], [SyncType], [AllLocnAvail], [HasQT], [HasDAT], [AllHeadingsDaysOverride], [printedDate], [BayNo], [Paymethod]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblbookings]');
    SET IDENTITY_INSERT [tblbookings] OFF;
END
ELSE PRINT 'Skipping tblbookings (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblLocnqty]) = 0
BEGIN
    PRINT 'Importing tblLocnqty (95,002 rows)...';
    SET IDENTITY_INSERT [tblLocnqty] ON;
    INSERT INTO [tblLocnqty] ([ID], [product_code], [Locn], [qty], [GLCode], [QtyInRack], [BinLocation])
    SELECT [ID], [product_code], [Locn], [qty], [GLCode], [QtyInRack], [BinLocation]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblLocnqty]');
    SET IDENTITY_INSERT [tblLocnqty] OFF;
END
ELSE PRINT 'Skipping tblLocnqty (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblTracking]) = 0
BEGIN
    PRINT 'Importing tblTracking (101,209 rows)...';
    SET IDENTITY_INSERT [tblTracking] ON;
    INSERT INTO [tblTracking] ([ID], [OperatorID], [InDate], [OutDate], [InTime], [OutTime], [Whereabouts], [LeaveDeskDate], [LeaveDeskTime], [Duration])
    SELECT [ID], [OperatorID], [InDate], [OutDate], [InTime], [OutTime], [Whereabouts], [LeaveDeskDate], [LeaveDeskTime], [Duration]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblTracking]');
    SET IDENTITY_INSERT [tblTracking] OFF;
END
ELSE PRINT 'Skipping tblTracking (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblShortages]) = 0
BEGIN
    PRINT 'Importing tblShortages (101,494 rows)...';
    INSERT INTO [tblShortages] ([ItemTranID], [ShortQty], [LastUpdate])
    SELECT [ItemTranID], [ShortQty], [LastUpdate]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblShortages]');
END
ELSE PRINT 'Skipping tblShortages (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblAssetTrail]) = 0
BEGIN
    PRINT 'Importing tblAssetTrail (102,398 rows)...';
    SET IDENTITY_INSERT [tblAssetTrail] ON;
    INSERT INTO [tblAssetTrail] ([ID], [audit_date], [asset_id], [asset_code], [operator_ID], [locn_number], [audit_action], [product_code], [new_state], [old_state])
    SELECT [ID], [audit_date], [asset_id], [asset_code], [operator_ID], [locn_number], [audit_action], [product_code], [new_state], [old_state]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblAssetTrail]');
    SET IDENTITY_INSERT [tblAssetTrail] OFF;
END
ELSE PRINT 'Skipping tblAssetTrail (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblCrew]) = 0
BEGIN
    PRINT 'Importing tblCrew (231,763 rows)...';
    SET IDENTITY_INSERT [tblCrew] ON;
    INSERT INTO [tblCrew] ([ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [product_code_v42], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [price], [rate_selected], [hours], [Minutes], [person], [task], [TechRate], [TechPay], [unitRate], [techrateIsHourorDay], [FirstDate], [RetnDate], [GroupSeqNo], [StraightTime], [OverTime], [DoubleTime], [UseCustomRate], [CustomRate], [HourOrDay], [ShortTurnaround], [HourlyRateID], [UnpaidHours], [UnpaidMins], [TechIsConfirmed], [MeetTechOnSite], [bit_field_v41], [SubrentalLinkID], [AssignTo], [days_using], [MinimumHours], [ConfirmationLevel], [JobDescription], [Notes], [AdmModifiedNoteDate], [JobTimeZone], [TechTimezone], [JobOffered], [JobOffereddate], [JobAccepted], [JobAcceptedDate], [Conflict], [PrintOnQuote], [PrintOnInvoice], [JobTechOfferStatus], [JobTechOfferDate], [JobTechNotes], [CrewClientNotes])
    SELECT [ID], [booking_no_v32], [heading_no], [seq_no], [sub_seq_no], [product_code_v42], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [price], [rate_selected], [hours], [Minutes], [person], [task], [TechRate], [TechPay], [unitRate], [techrateIsHourorDay], [FirstDate], [RetnDate], [GroupSeqNo], [StraightTime], [OverTime], [DoubleTime], [UseCustomRate], [CustomRate], [HourOrDay], [ShortTurnaround], [HourlyRateID], [UnpaidHours], [UnpaidMins], [TechIsConfirmed], [MeetTechOnSite], [bit_field_v41], [SubrentalLinkID], [AssignTo], [days_using], [MinimumHours], [ConfirmationLevel], [JobDescription], [Notes], [AdmModifiedNoteDate], [JobTimeZone], [TechTimezone], [JobOffered], [JobOffereddate], [JobAccepted], [JobAcceptedDate], [Conflict], [PrintOnQuote], [PrintOnInvoice], [JobTechOfferStatus], [JobTechOfferDate], [JobTechNotes], [CrewClientNotes]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblCrew]');
    SET IDENTITY_INSERT [tblCrew] OFF;
END
ELSE PRINT 'Skipping tblCrew (already has data)';
GO

IF (SELECT COUNT(*) FROM [tblAssetran]) = 0
BEGIN
    PRINT 'Importing tblAssetran (493,890 rows)...';
    SET IDENTITY_INSERT [tblAssetran] ON;
    INSERT INTO [tblAssetran] ([ID], [booking_no], [product_code], [stock_number], [price], [act_time_out_h], [act_time_out_m], [act_time_in_h], [act_time_in_m], [checkoutNo], [qtycheckedOut], [ActOutDate], [ActInDate], [Qtyreturned], [ReturnNo], [RecentUpdate], [QtyCrossRented], [ItemTranID], [ReturnType], [OperatorID], [ReturnOperatorID], [quantity])
    SELECT [ID], [booking_no], [product_code], [stock_number], [price], [act_time_out_h], [act_time_out_m], [act_time_in_h], [act_time_in_m], [checkoutNo], [qtycheckedOut], [ActOutDate], [ActInDate], [Qtyreturned], [ReturnNo], [RecentUpdate], [QtyCrossRented], [ItemTranID], [ReturnType], [OperatorID], [ReturnOperatorID], [quantity]
    FROM OPENROWSET('MSOLEDBSQL',
        'Server=116.90.5.144,41383;Database=AITESTDB;UID=PowerBI-Consult;PWD=2tW@ostq3a3_9oV3m-TBQu3w;TrustServerCertificate=Yes;Encrypt=Optional;',
        'SELECT * FROM [tblAssetran]');
    SET IDENTITY_INSERT [tblAssetran] OFF;
END
ELSE PRINT 'Skipping tblAssetran (already has data)';
GO

EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO
EXEC sp_MSforeachtable 'ALTER TABLE ? ENABLE TRIGGER ALL';
GO
PRINT 'Migration complete!';
GO