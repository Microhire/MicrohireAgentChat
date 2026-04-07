-- ============================================
-- AITESTDB Schema Creation Script (v2 - fixed)
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'AITESTDB')
    CREATE DATABASE AITESTDB;
GO
USE AITESTDB;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AggregatedCounter')
BEGIN
CREATE TABLE [AggregatedCounter] (
    [Key] nvarchar(100) NOT NULL,
    [Value] bigint NOT NULL,
    [ExpireAt] datetime NULL,
    CONSTRAINT [PK_HangFire_CounterAggregated] PRIMARY KEY CLUSTERED ([Key])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Counter')
BEGIN
CREATE TABLE [Counter] (
    [Key] nvarchar(100) NOT NULL,
    [Value] int NOT NULL,
    [ExpireAt] datetime NULL,
    [Id] bigint IDENTITY(1,1) NOT NULL,
    CONSTRAINT [PK_HangFire_Counter] PRIMARY KEY CLUSTERED ([Key], [Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Hash')
BEGIN
CREATE TABLE [Hash] (
    [Key] nvarchar(100) NOT NULL,
    [Field] nvarchar(100) NOT NULL,
    [Value] nvarchar(MAX) NULL,
    [ExpireAt] datetime2 NULL,
    CONSTRAINT [PK_HangFire_Hash] PRIMARY KEY CLUSTERED ([Key], [Field])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Job')
BEGIN
CREATE TABLE [Job] (
    [Id] bigint IDENTITY(1,1) NOT NULL,
    [StateId] bigint NULL,
    [StateName] nvarchar(20) NULL,
    [InvocationData] nvarchar(MAX) NOT NULL,
    [Arguments] nvarchar(MAX) NOT NULL,
    [CreatedAt] datetime NOT NULL,
    [ExpireAt] datetime NULL,
    CONSTRAINT [PK_HangFire_Job] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobParameter')
BEGIN
CREATE TABLE [JobParameter] (
    [JobId] bigint NOT NULL,
    [Name] nvarchar(40) NOT NULL,
    [Value] nvarchar(MAX) NULL,
    CONSTRAINT [PK_HangFire_JobParameter] PRIMARY KEY CLUSTERED ([JobId], [Name])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'JobQueue')
BEGIN
CREATE TABLE [JobQueue] (
    [Id] bigint IDENTITY(1,1) NOT NULL,
    [JobId] bigint NOT NULL,
    [Queue] nvarchar(50) NOT NULL,
    [FetchedAt] datetime NULL,
    CONSTRAINT [PK_HangFire_JobQueue] PRIMARY KEY CLUSTERED ([Queue], [Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'List')
BEGIN
CREATE TABLE [List] (
    [Id] bigint IDENTITY(1,1) NOT NULL,
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(MAX) NULL,
    [ExpireAt] datetime NULL,
    CONSTRAINT [PK_HangFire_List] PRIMARY KEY CLUSTERED ([Key], [Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SC_ImportEvent')
BEGIN
CREATE TABLE [SC_ImportEvent] (
    [ID] numeric(10,0) NOT NULL,
    [event_code] char(30) NULL,
    [event_desc] char(32) NULL,
    [deltime] int NULL,
    [rettime] int NULL,
    [ShowTerm] int NULL,
    [showdaysCharged] float NULL,
    [DelvDate] datetime NULL,
    [ShowSDate] datetime NULL,
    [PrepDateTime] datetime NULL,
    [Start_Date] datetime NULL,
    [End_Date] datetime NULL,
    [RetnDate] datetime NULL,
    [ShowEDate] datetime NULL,
    [DeprepDateTime] datetime NULL,
    [Locn] int NULL,
    [Mbscenario] tinyint NULL,
    [MBID] int NULL,
    [invoiced] char(1) NULL,
    [Invoice_no] decimal(19,0) NULL,
    [Invoice_amount] float NULL,
    [BookingsInvoiced] tinyint NULL,
    [Salesperson] varchar(30) NULL,
    [coordinator] varchar(8) NULL,
    [rentalDisc] float NULL,
    [SalesDisc] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SC_ImportHolidays')
BEGIN
CREATE TABLE [SC_ImportHolidays] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [DateF] datetime NULL,
    [Description] varchar(35) NULL,
    [HolidayRegion] int NULL,
    [HolidayLocation] int NULL,
    CONSTRAINT [PK__SC_Impor__3214EC27CF0F9869] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Schema')
BEGIN
CREATE TABLE [Schema] (
    [Version] int NOT NULL,
    CONSTRAINT [PK_HangFire_Schema] PRIMARY KEY CLUSTERED ([Version])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Server')
BEGIN
CREATE TABLE [Server] (
    [Id] nvarchar(200) NOT NULL,
    [Data] nvarchar(MAX) NULL,
    [LastHeartbeat] datetime NOT NULL,
    CONSTRAINT [PK_HangFire_Server] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Set')
BEGIN
CREATE TABLE [Set] (
    [Key] nvarchar(100) NOT NULL,
    [Score] float NOT NULL,
    [Value] nvarchar(256) NOT NULL,
    [ExpireAt] datetime NULL,
    CONSTRAINT [PK_HangFire_Set] PRIMARY KEY CLUSTERED ([Key], [Value])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'State')
BEGIN
CREATE TABLE [State] (
    [Id] bigint IDENTITY(1,1) NOT NULL,
    [JobId] bigint NOT NULL,
    [Name] nvarchar(20) NOT NULL,
    [Reason] nvarchar(100) NULL,
    [CreatedAt] datetime NOT NULL,
    [Data] nvarchar(MAX) NULL,
    CONSTRAINT [PK_HangFire_State] PRIMARY KEY CLUSTERED ([JobId], [Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAccountExportTransactions')
BEGIN
CREATE TABLE [tblAccountExportTransactions] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Status] int NOT NULL,
    [TransactionType] int NOT NULL,
    [BookingID] decimal(10,0) NULL,
    [ContactID] decimal(10,0) NULL,
    [CustomerID] decimal(10,0) NULL,
    [PayrollCategory] varchar(31) NULL,
    [PayrollUnits] float NULL,
    [PayrollStartStop] float NULL,
    [Notes] varchar(254) NULL,
    [TransactionDate] datetime NULL,
    CONSTRAINT [PK_tblAccountExportTransactions] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblActivity')
BEGIN
CREATE TABLE [tblActivity] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [StartDate] datetime NULL,
    [EndDate] datetime NULL,
    [StartTime] varchar(4) NULL,
    [EndTime] varchar(4) NULL,
    [TypeID] decimal(10,0) NULL,
    [OperatorID] decimal(10,0) NULL,
    [Description] varchar(50) NULL,
    [Notes] text NULL,
    [Completed] char(1) NULL,
    [CompletedBy] decimal(10,0) NULL,
    [DateCompleted] datetime NULL,
    [TimeCompleted] varchar(4) NULL,
    [ActivityResultID] decimal(10,0) NULL,
    [Scheduled] char(1) NULL,
    [ActualDuration] int NULL,
    [CallListID] decimal(10,0) NULL,
    [AlarmDate] datetime NULL,
    [AlarmTime] varchar(4) NULL,
    [AlarmSet] char(1) NULL,
    [ContactID] decimal(10,0) NULL,
    [AlarmMessage] varchar(30) NULL,
    [Booking_no] varchar(35) NULL,
    [ProjectCode] varchar(30) NULL,
    [LastContactedDateTime] datetime NULL,
    [ActivitySource] int NULL,
    CONSTRAINT [PK_tblActivity] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblActivityResult')
BEGIN
CREATE TABLE [tblActivityResult] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ActivityResult] varchar(50) NULL,
    [FirstActivityID] decimal(10,0) NULL,
    [SecondActivityID] decimal(10,0) NULL,
    [ThirdActivityID] decimal(10,0) NULL,
    [FirstActive] char(1) NULL,
    [SecondActive] char(1) NULL,
    [ThirdActive] char(1) NULL,
    [FirstDays] int NULL,
    [SecondDays] int NULL,
    [ThirdDays] int NULL,
    [FirstHours] int NULL,
    [SecondHours] int NULL,
    [ThirdHours] int NULL,
    CONSTRAINT [PK_tblActivityResult] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblActivityType')
BEGIN
CREATE TABLE [tblActivityType] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Description] varchar(20) NULL,
    [Points] int NULL,
    [Colour] int NULL,
    [CustomActivityType] decimal(19,0) NULL,
    [RpwsType] bit NULL,
    [RPWSTypes] bit NULL,
    CONSTRAINT [PK_tblActivityType] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAddTechSkill')
BEGIN
CREATE TABLE [tblAddTechSkill] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [SeqNo] int NULL,
    [SkillName] varchar(80) NULL,
    [SkillNotes] varchar(255) NULL,
    [hasExpiryDate] bit NULL,
    [hasLicense] bit NULL,
    CONSTRAINT [PK_tbltblAddTechSkill] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAgents')
BEGIN
CREATE TABLE [tblAgents] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [agent_code] varchar(13) NULL,
    [Auth_agentV6] varchar(50) NULL,
    [address_l1V6] varchar(50) NULL,
    [address_l2V6] varchar(50) NULL,
    [address_l3V6] varchar(50) NULL,
    [phone] varchar(32) NULL,
    [fax] varchar(16) NULL,
    [what_type] tinyint NULL,
    [contactname] varchar(35) NULL,
    [ContactID] decimal(10,0) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [PostCode] varchar(12) NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    CONSTRAINT [PK_tblAgents] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblApikeys')
BEGIN
CREATE TABLE [tblApikeys] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Description] varchar(50) NOT NULL,
    [Key] varchar(50) NOT NULL,
    [AllowedCidr] varchar(MAX) NOT NULL,
    CONSTRAINT [PK__tblApike__3214EC272DF5B24E] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblArcInstruct')
BEGIN
CREATE TABLE [tblArcInstruct] (
    [booking_no] varchar(35) NULL,
    [inst_instru] text NULL,
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    CONSTRAINT [PK_tblArcInstruct] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAsks')
BEGIN
CREATE TABLE [tblAsks] (
    [ID] decimal(18,0) IDENTITY(1,1) NOT NULL,
    [fields] varchar(MAX) NULL,
    [askName] varchar(100) NULL,
    [emailAddress] varchar(50) NULL,
    [phoneNumber] varchar(50) NULL,
    [export] bit NULL,
    [createDateTime] datetime NULL,
    [archived] bit NULL,
    CONSTRAINT [PK_tblAsks] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAsset01')
BEGIN
CREATE TABLE [tblAsset01] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ASSET_CODE] char(30) NULL,
    [DESCRIPTION] varchar(50) NULL,
    [PRODUCT_COde] char(30) NULL,
    [STOCK_NUMBER] int NULL,
    [SERIAL_NO] varchar(25) NULL,
    [COST] float NULL,
    [EST_RESALE] float NULL,
    [Disposal_AMT] float NULL,
    [REVAL_TD] float NULL,
    [INSURER] char(6) NULL,
    [INSURED_VAL] float NULL,
    [BOOKING_NO] varchar(35) NULL,
    [DEL_TIME_H] tinyint NULL,
    [DEL_TIME_M] tinyint NULL,
    [RET_TIME_H] tinyint NULL,
    [RET_TIME_M] tinyint NULL,
    [TIMES_HIRE] int NULL,
    [AMOUNT_LTD] float NULL,
    [days_IN_Service] int NULL,
    [days_REQ_service] int NULL,
    [METHOD_TAX] tinyint NULL,
    [DEPN_RATE_tax] float NULL,
    [ACCUM_DEPN_tax] float NULL,
    [YTD_DEPN_Tax] float NULL,
    [DEPN_LY_TAx] float NULL,
    [WRTN_DOWN_val_tax] float NULL,
    [warehouse_time_h] tinyint NULL,
    [wareHouse_time_m] tinyint NULL,
    [times_hired_1_4td] int NULL,
    [current_1_4] tinyint NULL,
    [locn] int NULL,
    [modelNumber] varchar(25) NULL,
    [PurDate] datetime NULL,
    [StartDate] datetime NULL,
    [DisDate] datetime NULL,
    [DelDate] datetime NULL,
    [RetDate] datetime NULL,
    [LastTaxDate] datetime NULL,
    [WareDate] datetime NULL,
    [PONumber] int NULL,
    [ReturnFromservice] datetime NULL,
    [ServiceStatus] tinyint NULL,
    [VendorV8] varchar(30) NULL,
    [KeepStatus] tinyint NULL,
    [NextTestDate] datetime NULL,
    [LastTestDate] datetime NULL,
    [OperationalStatus] tinyint NULL,
    [TestFrequencyDays] int NULL,
    [Financier] varchar(50) NULL,
    [FinanceStartDate] datetime NULL,
    [FinanceEndDate] datetime NULL,
    [ContractNo] varchar(20) NULL,
    [RepayAmount] float NULL,
    [FinanceType] varchar(50) NULL,
    [FinanceTotal] float NULL,
    [RFIDTag] varchar(50) NULL,
    [RevCenterLocn] int NULL,
    [iDisposalType] int NULL,
    [SeqNo] int NULL,
    [LastTestResultsImportedFrom] int NULL,
    [HomeLocn] int NULL,
    [PCode] varchar(30) NULL,
    [NavCode] varchar(30) NULL,
    [PackStatus] bit NOT NULL,
    [latitude] float NULL,
    [longitude] float NULL,
    [LOCATION] varchar(100) NULL,
    CONSTRAINT [PK_tblAsset01] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAssetMovement')
BEGIN
CREATE TABLE [tblAssetMovement] (
    [ID] decimal(18,0) IDENTITY(1,1) NOT NULL,
    [ProductCode] char(30) NULL,
    [StockNumber] int NOT NULL,
    [Quantity] int NULL,
    [BookingNo] varchar(35) NULL,
    [TransType] tinyint NOT NULL,
    [Note] nvarchar(MAX) NULL,
    [SubStituteProductCode] varchar(30) NULL,
    [itemIsSubtitue] bit NULL,
    [OpID] decimal(10,0) NULL,
    [asDateTime] datetime NULL,
    [ItemTranID] decimal(10,0) NULL,
    [PackType] tinyint NULL,
    [CompleteStatus] tinyint NOT NULL,
    [ParentBC] varchar(30) NULL,
    CONSTRAINT [PK_tblAssetMovement] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAssetran')
BEGIN
CREATE TABLE [tblAssetran] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [product_code] varchar(30) NULL,
    [stock_number] int NULL,
    [price] float NULL,
    [act_time_out_h] tinyint NULL,
    [act_time_out_m] tinyint NULL,
    [act_time_in_h] tinyint NULL,
    [act_time_in_m] tinyint NULL,
    [checkoutNo] int NULL,
    [qtycheckedOut] int NULL,
    [ActOutDate] datetime NULL,
    [ActInDate] datetime NULL,
    [Qtyreturned] int NULL,
    [ReturnNo] int NULL,
    [RecentUpdate] bit NOT NULL,
    [QtyCrossRented] int NOT NULL,
    [ItemTranID] int NOT NULL,
    [ReturnType] tinyint NOT NULL,
    [OperatorID] int NOT NULL,
    [ReturnOperatorID] int NULL,
    [quantity] int NULL,
    CONSTRAINT [PK_tblAssetran] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAssetTrail')
BEGIN
CREATE TABLE [tblAssetTrail] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [audit_date] datetime NULL,
    [asset_id] decimal(10,0) NULL,
    [asset_code] varchar(30) NULL,
    [operator_ID] decimal(10,0) NULL,
    [locn_number] int NULL,
    [audit_action] tinyint NULL,
    [product_code] varchar(30) NULL,
    [new_state] varchar(16) NULL,
    [old_state] varchar(16) NULL,
    CONSTRAINT [PK_tblAssetTrail] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAttach')
BEGIN
CREATE TABLE [tblAttach] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [project_code] varchar(30) NULL,
    [filename] varchar(255) NULL,
    [aType] tinyint NOT NULL,
    [OperatorID] decimal(10,0) NOT NULL,
    [DateCreated] datetime NOT NULL,
    [TimeCreated] varchar(4) NOT NULL,
    [OtherCode] varchar(36) NULL,
    [PrintAtCheckout] bit NULL,
    [CustomerCode] varchar(30) NULL,
    [EmailDateAndTime] datetime NULL,
    [PONumber] int NULL,
    [cID] varchar(64) NULL,
    [cFile] bit NULL,
    [cPath] varchar(255) NULL,
    [cName] varchar(127) NULL,
    [cEditLink] varchar(512) NULL,
    [cViewLink] varchar(50) NULL,
    [cType] tinyint NULL,
    CONSTRAINT [PK_tblAttach] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAttachments')
BEGIN
CREATE TABLE [tblAttachments] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ActivityID] decimal(10,0) NULL,
    [Path] varchar(100) NULL,
    [IsCoverLetter] char(1) NULL,
    CONSTRAINT [PK_tblAttachments] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAudit')
BEGIN
CREATE TABLE [tblAudit] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [time_h] tinyint NULL,
    [time_m] tinyint NULL,
    [audit_type] tinyint NULL,
    [invoice_no] decimal(19,0) NULL,
    [value] real NULL,
    [DateF] datetime NULL,
    [status] tinyint NULL,
    [reason] varchar(50) NULL,
    [operators] varchar(50) NULL,
    [CustomCreditNoteNumber] decimal(19,0) NULL,
    [version_no] smallint NULL,
    CONSTRAINT [PK_tblAudit] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAuditTrailGeneral')
BEGIN
CREATE TABLE [tblAuditTrailGeneral] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [TableName] varchar(50) NULL,
    [FieldName] varchar(50) NULL,
    [OperatorID] decimal(10,0) NULL,
    [ValueBefore] varchar(300) NULL,
    [ValueAfter] varchar(300) NULL,
    [ChangeDate] datetime NULL,
    [Note] varchar(300) NULL,
    CONSTRAINT [PK_tblAuditTrailGeneral_ID] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAutoEmailGroupContacts')
BEGIN
CREATE TABLE [tblAutoEmailGroupContacts] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [GroupID] decimal(10,0) NULL,
    [ContactID] decimal(10,0) NULL,
    [BookingSavedInConfirmed] bit NULL,
    [EquipAddToConfirmed] bit NULL,
    [EquipEditConfirmed] bit NULL,
    [EquipDeleteConfirmed] bit NULL,
    CONSTRAINT [PK_tblAutoEmailGroupContacts] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblAutoGenerate')
BEGIN
CREATE TABLE [tblAutoGenerate] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [NextBarcodeNumber] int NULL,
    [BarcodePrefix] varchar(1) NULL,
    CONSTRAINT [PK_tblAutoGenerate] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblBackup')
BEGIN
CREATE TABLE [tblBackup] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [BackupFolder] varchar(255) NOT NULL,
    [Sunday] bit NOT NULL,
    [Monday] bit NOT NULL,
    [Tuesday] bit NOT NULL,
    [Wednesday] bit NOT NULL,
    [Thursday] bit NOT NULL,
    [Friday] bit NOT NULL,
    [Saturday] bit NOT NULL,
    [BackupTime] tinyint NOT NULL,
    [LastBackupDate] datetime NOT NULL,
    [LastBackupTime] varchar(4) NOT NULL,
    CONSTRAINT [PK_tblBackup] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblBarcodeStaging')
BEGIN
CREATE TABLE [tblBarcodeStaging] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [SessionID] uniqueidentifier NOT NULL,
    [booking_no] varchar(35) NULL,
    [Product_code] varchar(30) NULL,
    [Barcode] varchar(30) NULL,
    [Asset_code] varchar(30) NULL,
    [Stock_number] int NULL,
    [Asset_track] char(1) NULL,
    [Qty] int NULL,
    [OverQty] int NULL,
    [InvDesc] varchar(50) NULL,
    [ADesc] varchar(50) NULL,
    [MaintID] decimal(10,0) NULL,
    [MaintReturnDate] datetime NULL,
    [DisDate] datetime NULL,
    [Locn] int NULL,
    [ActOutDate] datetime NULL,
    [act_time_out_h] tinyint NULL,
    [act_time_out_m] tinyint NULL,
    [OperationalStatus] tinyint NULL,
    [NextTestDate] datetime NULL,
    [LastTestDate] datetime NULL,
    [TestFrequencyDays] int NULL,
    [TestRequired] bit NULL,
    [ProConfig] tinyint NULL,
    [ProdRoadCase] tinyint NULL,
    [IsRoadcase] bit NULL,
    [InRack] bit NULL,
    [product_type_v41] tinyint NULL,
    [indiv_hire_sale] char(1) NULL,
    [Floating] bit NULL,
    CONSTRAINT [PK_tblBarcodeStaging] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblBatch')
BEGIN
CREATE TABLE [tblBatch] (
    [CashReceiptBatchNo] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblBill')
BEGIN
CREATE TABLE [tblBill] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [parent_code] char(30) NULL,
    [product_code] char(30) NULL,
    [qty_v5] float NULL,
    [sub_seq_no] tinyint NULL,
    [variable_part] tinyint NULL,
    [ContactID] decimal(10,0) NULL,
    [SelectComp] char(1) NULL,
    [AccessoryDiscount] float NULL,
    [AutoResolve] bit NULL,
    [nestedCompAcc] bit NULL,
    CONSTRAINT [PK_tblBill] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblBookingAssetHist')
BEGIN
CREATE TABLE [tblBookingAssetHist] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [OpDate] datetime NOT NULL,
    [OpCode] int NOT NULL,
    [SoftwareTypeCode] int NOT NULL,
    [Description] varchar(100) NULL,
    [Barcode] varchar(50) NULL,
    [OperatorID] decimal(10,0) NOT NULL,
    [Quantity] int NOT NULL,
    [Product_Code] varchar(30) NULL,
    [Subst_Product_Code] varchar(30) NULL,
    [Stock_Number] int NULL,
    [BookingNo] varchar(35) NULL,
    [ErrorCode] int NULL,
    [AssetTracked] bit NOT NULL,
    [Pending] bit NOT NULL,
    [Executed] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [SubstitutionType] int NULL,
    [ItemTranId] int NULL,
    [OperatorNote] varchar(200) NULL,
    [CheckoutSessionId] int NULL,
    [ReturnSessionId] int NULL,
    [Roadcase_Product_Code] varchar(30) NULL,
    [Roadcase_Stock_Number] int NULL,
    [Roadcase_barcode] varchar(50) NULL,
    CONSTRAINT [PK_tblBookingAssetHist] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblBookingCrewInfo')
BEGIN
CREATE TABLE [tblBookingCrewInfo] (
    [ID] decimal(18,0) IDENTITY(1,1) NOT NULL,
    [Booking_no] varchar(35) NULL,
    [CrewChief] varchar(50) NULL,
    [CrewChiefID] decimal(10,0) NULL,
    [CustomListField] varchar(100) NULL,
    [GeneralLocation] varchar(100) NULL,
    [CustomInt] int NULL,
    [DressCode] varchar(50) NULL,
    [CustomReal] float NULL,
    [CustomDateTime] datetime NULL,
    [CustomString] varchar(512) NULL,
    CONSTRAINT [PK_tblBookingCrewInfo] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblbookings')
BEGIN
CREATE TABLE [tblbookings] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viaV71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [Item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_nameV6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setupTimeV61] varchar(4) NULL,
    [RehearsalTime] varchar(4) NULL,
    [StrikeTime] varchar(4) NULL,
    [Trans_to_locn] int NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEndTime] varchar(4) NULL,
    [transferNo] decimal(19,0) NULL,
    [currencyStr] varchar(5) NULL,
    [BookingProgressStatus] tinyint NULL,
    [ConfirmedBy] varchar(35) NULL,
    [ConfirmedDocRef] varchar(50) NULL,
    [VenueRoom] varchar(35) NULL,
    [expAttendees] int NULL,
    [HourBooked] tinyint NULL,
    [MinBooked] tinyint NULL,
    [SecBooked] tinyint NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [HorCCroom] int NULL,
    [subrooms] char(12) NULL,
    [truckOut] int NULL,
    [truckIn] int NULL,
    [tripOut] tinyint NULL,
    [tripIn] tinyint NULL,
    [showName] varchar(50) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [OurNumberDel] char(1) NULL,
    [OurNumberRet] char(1) NULL,
    [DatesAndTimesEnabled] char(1) NULL,
    [Government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [ContactLoadedIntoVenue] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [OrganizationV6] varchar(50) NULL,
    [Salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Inv_date] datetime NULL,
    [ShowSdate] datetime NULL,
    [ShowEdate] datetime NULL,
    [SetDate] datetime NULL,
    [ADelDate] datetime NULL,
    [SDate] datetime NULL,
    [RehDate] datetime NULL,
    [ConDate] datetime NULL,
    [TOutDate] datetime NULL,
    [TInDate] datetime NULL,
    [PreDate] datetime NULL,
    [ConByDate] datetime NULL,
    [bookingPrinted] char(1) NULL,
    [CustCode] varchar(30) NULL,
    [ExtendedFrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [PotPercent] float NULL,
    [Referral] varchar(50) NULL,
    [EventType] varchar(20) NULL,
    [Priority] int NULL,
    [InvoiceStage] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [PickupRetDate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [MaxBookingValue] float NULL,
    [UsesPriceTable] int NULL,
    [DateToInvoice] datetime NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [ServCont] char(1) NULL,
    [PaymentOptions] tinyint NULL,
    [PrintedPayTerm] varchar(40) NULL,
    [RentalType] tinyint NULL,
    [UseBillSchedule] char(1) NULL,
    [Tax2] float NULL,
    [ContactID] decimal(9,0) NULL,
    [ShortHours] tinyint NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [dtExpected_ReturnDate] datetime NOT NULL,
    [vcExpected_ReturnTime] varchar(4) NOT NULL,
    [vcTruckOutTime] varchar(4) NOT NULL,
    [vcTruckInTime] varchar(4) NOT NULL,
    [CustID] decimal(10,0) NOT NULL,
    [VenueID] int NOT NULL,
    [LateChargesApplied] bit NOT NULL,
    [shortagesAreTransfered] bit NOT NULL,
    [VenueContactID] int NULL,
    [VenueContact] varchar(50) NULL,
    [VenueContactPhoneID] int NULL,
    [LTBillingOption] tinyint NULL,
    [DressCode] varchar(35) NULL,
    [Collection] float NULL,
    [FuelSurchargeRate] float NULL,
    [FreightLocked] bit NULL,
    [LabourLocked] bit NULL,
    [RentalLocked] bit NULL,
    [PriceLocked] bit NULL,
    [insurance_type] tinyint NULL,
    [EntryDate] datetime NULL,
    [CreditSurchargeRate] float NULL,
    [CreditSurchargeAmount] float NULL,
    [DisableTreeOrder] bit NULL,
    [ConfirmationFinancials] varchar(30) NULL,
    [EventManagementRate] float NULL,
    [EventManagementAmount] float NULL,
    [EquipmentModified] bit NULL,
    [CrewStatusColumn] tinyint NULL,
    [LoadDateTime] datetime NULL,
    [UnloadDateTime] datetime NULL,
    [DeprepDateTime] datetime NULL,
    [DeprepOn] bit NOT NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [ScheduleDatesOn] varchar(10) NULL,
    [bBookingIsComplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingID] int NULL,
    [MasterBillingMethod] tinyint NULL,
    [schedHeadEquipSpan] tinyint NULL,
    [TaxabPCT] float NOT NULL,
    [UntaxPCT] float NOT NULL,
    [Tax1PCT] float NOT NULL,
    [Tax2PCT] float NOT NULL,
    [PaymentContactID] int NULL,
    [sale_of_asset_undisc_amt] float NULL,
    [LockedForScanning] bit NULL,
    [OldAssignedTo] varchar(35) NULL,
    [DateLastModified] datetime NULL,
    [crew_cnt] int NOT NULL,
    [rTargetMargin] float NOT NULL,
    [rProfitMargin] float NOT NULL,
    [ContractNo] varchar(18) NULL,
    [SyncType] tinyint NOT NULL,
    [AllLocnAvail] bit NULL,
    [HasQT] bit NOT NULL,
    [HasDAT] bit NOT NULL,
    [AllHeadingsDaysOverride] bit NULL,
    [printedDate] datetime NULL,
    [BayNo] int NULL,
    [Paymethod] varchar(25) NULL,
    CONSTRAINT [PK_tblbookings] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblbooknote')
BEGIN
CREATE TABLE [tblbooknote] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [bookingNo] varchar(35) NULL,
    [line_no] tinyint NULL,
    [text_line] varchar(MAX) NULL,
    [NoteType] tinyint NULL,
    [OperatorID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblbooknote] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCalendarAccount')
BEGIN
CREATE TABLE [tblCalendarAccount] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [ServerAccountType] int NOT NULL,
    [AccountName] varchar(MAX) NULL,
    [UserId] varchar(MAX) NULL,
    [Password] varchar(MAX) NULL,
    [ServerName] varchar(MAX) NULL,
    [ServerOption] varchar(MAX) NULL,
    [Active] bit NOT NULL,
    [CalendarDataType] int NOT NULL,
    [ContactEmail] varchar(MAX) NULL,
    [ContactName] varchar(MAX) NULL,
    [ContactPhone] varchar(50) NULL,
    [OwnerContactId] int NULL,
    [OwnerName] varchar(MAX) NULL,
    [RefreshToken] varchar(MAX) NULL,
    [AccessToken] varchar(MAX) NULL,
    [AuthToken] varchar(MAX) NULL,
    [ClientID] varchar(512) NULL,
    [ClientSecret] varchar(512) NULL,
    [DatesType] int NULL,
    [SendEmail] bit NULL,
    CONSTRAINT [PK_tblCalendarAccount] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCalendarAccountLink')
BEGIN
CREATE TABLE [tblCalendarAccountLink] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [CalAccountID] int NOT NULL,
    [CalendarName] varchar(200) NULL,
    [Active] bit NOT NULL,
    [AllCustomerFlag] bit NULL,
    [TechID] int NULL,
    [LeaderID] int NULL,
    [CustomerID] int NULL,
    [AllTechFlag] bit NULL,
    [CategoryCode] varchar(30) NULL,
    [EventCode] varchar(30) NULL,
    [ShopFlag] bit NULL,
    [LeaderFlag] bit NULL,
    [TechFlag] bit NULL,
    [CalendarID] varchar(255) NULL,
    CONSTRAINT [PK_tblCalendarAccountLink] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCalendarAccountLinkSync')
BEGIN
CREATE TABLE [tblCalendarAccountLinkSync] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [DeletedFlag] bit NOT NULL,
    [SyncFlag] bit NOT NULL,
    [ErrorFlag] bit NOT NULL,
    [FirstSync] datetime NOT NULL,
    [MostRecentSync] datetime NOT NULL,
    [CalendarAccountId] int NOT NULL,
    [CalendarAccountLinkId] int NOT NULL,
    [LinkedItemId] int NULL,
    [LinkedItemType] int NULL,
    [EventID] varchar(255) NULL,
    [CalendarID] varchar(255) NULL,
    [techId] varchar(30) NULL,
    CONSTRAINT [PK_tblCalendarAccountLinkSync] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCalendarSyncAccount')
BEGIN
CREATE TABLE [tblCalendarSyncAccount] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [ServerType] int NOT NULL,
    [AccountName] varchar(300) NULL,
    [AccountMail] varchar(100) NULL,
    [UserId] varchar(100) NULL,
    [UserPass] varchar(100) NULL,
    [ServerName] varchar(100) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_tblCalendarSyncAccount] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCalendarSyncItem')
BEGIN
CREATE TABLE [tblCalendarSyncItem] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [SyncTechID] int NOT NULL,
    [RpItemID] int NOT NULL,
    [RpItemType] smallint NOT NULL,
    [AppointmentID] varchar(255) NULL,
    CONSTRAINT [PK_tblCalendarSyncItem] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCalendarSyncLog')
BEGIN
CREATE TABLE [tblCalendarSyncLog] (
    [log_date] datetime NULL,
    [log_type] smallint NULL,
    [log_message] varchar(500) NULL,
    [ID] int IDENTITY(1,1) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCalendarSyncTech')
BEGIN
CREATE TABLE [tblCalendarSyncTech] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [AccountID] int NOT NULL,
    [Active] bit NOT NULL,
    [TechID] int NULL,
    [CalendarID] varchar(255) NULL,
    CONSTRAINT [PK_tblCalendarSyncTech] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCallList')
BEGIN
CREATE TABLE [tblCallList] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [CallListID] decimal(10,0) NULL,
    [ContactID] decimal(10,0) NULL,
    [Completed] decimal(10,0) NULL,
    [ActivityID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblCallList] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCampaign')
BEGIN
CREATE TABLE [tblCampaign] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [SourceName] varchar(50) NULL,
    [Cost] float NULL,
    [DateStarted] datetime NULL,
    CONSTRAINT [PK_tblCampaign] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCancelBR')
BEGIN
CREATE TABLE [tblCancelBR] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Reason] varchar(80) NULL,
    [booking_no] varchar(35) NULL,
    [StatusBeforeCan] tinyint NULL,
    [price_quoted] real NULL,
    CONSTRAINT [PK_tblCancelBR] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCarnet')
BEGIN
CREATE TABLE [tblCarnet] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [cDescription] varchar(80) NULL,
    [Booking_no] varchar(35) NULL,
    [EventCode] varchar(13) NULL,
    [Box_Name] varchar(80) NULL,
    [Height] float NULL,
    [Width] float NULL,
    [rLength] float NULL,
    [Depth] float NULL,
    [HwdUnit] varchar(20) NULL,
    [rWeight] float NULL,
    [WeightUnit] varchar(20) NULL,
    [OffsetPerc] float NULL,
    [BoxSeqNo] int NULL,
    [HeadingNo] int NULL,
    CONSTRAINT [PK_tblCarnet] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCarnetHeading')
BEGIN
CREATE TABLE [tblCarnetHeading] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [HeadingNo] int NULL,
    [HeadingDesc] varchar(50) NULL,
    [EventCode] varchar(30) NULL,
    [BookingNo] varchar(35) NULL,
    [Exclude] bit NULL,
    CONSTRAINT [PK_tblCarnetHeading] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCarnetItems')
BEGIN
CREATE TABLE [tblCarnetItems] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [CarnetID] decimal(10,0) NULL,
    [Qty] int NULL,
    [ItemTranID] decimal(10,0) NULL,
    [AssetTranID] decimal(10,0) NULL,
    [HeadingID] decimal(10,0) NULL,
    [SeqNo] int NULL,
    [HSCode] varchar(20) NULL,
    [CustomDesc] varchar(80) NULL,
    [CustomWeight] float NULL,
    [CustomValue] float NULL,
    [useVolume] bit NULL,
    [SerialNo] varchar(25) NULL,
    [Origin] varchar(25) NULL,
    [CustomHeight] float NULL,
    [CustomWidth] float NULL,
    [CustomLength] float NULL,
    [Exclude] bit NULL,
    CONSTRAINT [PK_tblCarnetItems] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCategory')
BEGIN
CREATE TABLE [tblCategory] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [category_code] char(30) NULL,
    [cat_descV6] char(50) NULL,
    [DisplayColour] varchar(7) NULL,
    [DisplayBold] char(1) NULL,
    [CategoryType] tinyint NOT NULL,
    [StandardCostPercentage] float NULL,
    [GLRevenueCode] varchar(50) NULL,
    [GLCrossRentExpenseCode] varchar(30) NULL,
    [Group_code] varchar(30) NULL,
    [ParentCategoryCode] varchar(30) NULL,
    [OLCatDesc] varchar(50) NULL,
    [CustomIcon] varchar(60) NULL,
    CONSTRAINT [PK_tblCategory] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblContact')
BEGIN
CREATE TABLE [tblContact] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [CustCodeLink] varchar(30) NULL,
    [Contactname] varchar(35) NULL,
    [firstname] varchar(25) NULL,
    [surname] varchar(35) NULL,
    [nameKeyField] varchar(11) NULL,
    [position] varchar(50) NULL,
    [busname] varchar(50) NULL,
    [Adr1] varchar(50) NULL,
    [Adr2] varchar(50) NULL,
    [Adr3] varchar(50) NULL,
    [Postcode] varchar(12) NULL,
    [Phone1] varchar(16) NULL,
    [Phone2] varchar(16) NULL,
    [Fax] varchar(16) NULL,
    [Webpage] varchar(80) NULL,
    [Email] varchar(80) NULL,
    [driversLicNo] varchar(20) NULL,
    [OtherID] varchar(30) NULL,
    [specialty] varchar(60) NULL,
    [PictureDatafile] varchar(240) NULL,
    [lastBookDate] datetime NULL,
    [MidName] varchar(35) NULL,
    [Cell] varchar(16) NULL,
    [Ext1] varchar(8) NULL,
    [Ext2] varchar(8) NULL,
    [Active] char(1) NULL,
    [MailList] char(1) NULL,
    [DecMaker] char(1) NULL,
    [LastContact] datetime NULL,
    [LastAttempt] datetime NULL,
    [Department] varchar(50) NULL,
    [SourceID] decimal(10,0) NULL,
    [CreateDate] datetime NULL,
    [LastUpdate] datetime NULL,
    [ReferralName] varchar(50) NULL,
    [Field1] varchar(50) NULL,
    [Field2] varchar(50) NULL,
    [Field3] varchar(50) NULL,
    [Field4] varchar(50) NULL,
    [Field5] varchar(50) NULL,
    [Field6] varchar(50) NULL,
    [Field7] datetime NULL,
    [Field8] datetime NULL,
    [AskFor] varchar(20) NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [Sendme_faxes] char(1) NULL,
    [Sendme_emails] char(1) NULL,
    [CardHolder_Name] varchar(250) NULL,
    [SalesPerson_Code] varchar(30) NULL,
    [SalesAssignEndDate] datetime NULL,
    [Country] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [bDriver] bit NOT NULL,
    [bFreeLanceContact] bit NOT NULL,
    [JobTitle] varchar(15) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [FaxDialAreaCode] bit NULL,
    [FaxCallType] tinyint NULL,
    [SubRentalVendor] varchar(30) NULL,
    [AgencyContact] bit NULL,
    [UpdateVendorContact] bit NULL,
    [username] varchar(50) NULL,
    [password] varchar(256) NULL,
    [TimeZone] varchar(30) NULL,
    [RPwebservicesActive] bit NOT NULL,
    [RPWSDefaultOpID] int NULL,
    [CultureInt] int NULL,
    [ProjectManager] bit NULL,
    [Utc] smallint NULL,
    [field9] varchar(50) NULL,
    [field10] varchar(50) NULL,
    [field11] varchar(50) NULL,
    [field12] varchar(50) NULL,
    [field13] varchar(50) NULL,
    [field14] varchar(50) NULL,
    [field15] datetime NULL,
    [RPWSCrewManager] bit NULL,
    CONSTRAINT [PK_tblContact] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblContactLinks')
BEGIN
CREATE TABLE [tblContactLinks] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ContactID] decimal(10,0) NULL,
    [OperatorID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblContactLinks] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblContactNote')
BEGIN
CREATE TABLE [tblContactNote] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ContactID] decimal(10,0) NULL,
    [LineNumber] tinyint NULL,
    [LineText] varchar(253) NULL,
    CONSTRAINT [PK_tblContactNote] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblContactSecondarySkills')
BEGIN
CREATE TABLE [tblContactSecondarySkills] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ContactID] int NULL,
    [SkillID] int NULL,
    [hasLicense] bit NULL,
    [startDate] datetime NULL,
    [expirydate] datetime NULL,
    [licensepathFilename] varchar(255) NULL,
    [skillNotes] varchar(255) NULL,
    CONSTRAINT [PK_tblContactSecondarySkills] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCountries')
BEGIN
CREATE TABLE [tblCountries] (
    [ID] smallint NOT NULL,
    [cname] varchar(25) NULL,
    CONSTRAINT [tblCountries_pk] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCreditNoteNumber')
BEGIN
CREATE TABLE [tblCreditNoteNumber] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [NextCreditNoteNumber] decimal(19,0) NULL,
    CONSTRAINT [PK_tblCreditNoteNumber] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCrew')
BEGIN
CREATE TABLE [tblCrew] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] int NULL,
    [price] float NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [person] char(30) NULL,
    [task] tinyint NULL,
    [TechRate] float NULL,
    [TechPay] float NULL,
    [unitRate] float NULL,
    [techrateIsHourorDay] char(1) NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [GroupSeqNo] int NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [bit_field_v41] tinyint NULL,
    [SubrentalLinkID] int NULL,
    [AssignTo] varchar(35) NULL,
    [days_using] float NULL,
    [MinimumHours] float NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [Notes] varchar(512) NULL,
    [AdmModifiedNoteDate] datetime NULL,
    [JobTimeZone] tinyint NULL,
    [TechTimezone] tinyint NULL,
    [JobOffered] bit NULL,
    [JobOffereddate] datetime NULL,
    [JobAccepted] bit NULL,
    [JobAcceptedDate] datetime NULL,
    [Conflict] bit NULL,
    [PrintOnQuote] bit NULL,
    [PrintOnInvoice] bit NULL,
    [JobTechOfferStatus] tinyint NULL,
    [JobTechOfferDate] datetime NULL,
    [JobTechNotes] varchar(512) NULL,
    [CrewClientNotes] varchar(40) NULL,
    CONSTRAINT [PK_tblCrew] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCRMenu')
BEGIN
CREATE TABLE [tblCRMenu] (
    [CRID] int IDENTITY(1,1) NOT NULL,
    [Index_num] int NULL,
    [MenuName] nvarchar(63) NULL,
    [MenuFile] nvarchar(127) NULL,
    [MenuParams] nvarchar(255) NULL,
    [Location] tinyint NULL,
    CONSTRAINT [PK_tblCRMenu] PRIMARY KEY CLUSTERED ([CRID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCultureCode')
BEGIN
CREATE TABLE [tblCultureCode] (
    [ID] int NOT NULL,
    [spec_cult] nvarchar(10) NULL,
    [cult_name] nvarchar(50) NULL,
    CONSTRAINT [PK_tblCultureCode] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCurrate')
BEGIN
CREATE TABLE [tblCurrate] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [cur_string] char(5) NULL,
    [DateF] datetime NULL,
    [rate] float NULL,
    CONSTRAINT [PK_tblCurrate] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCurrency')
BEGIN
CREATE TABLE [tblCurrency] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [cur_string] varchar(5) NULL,
    [cur_name] varchar(30) NULL,
    [fixed] char(1) NULL,
    [FixedRate] float NULL,
    [bIsDomestic] bit NULL,
    [decimalSep] varchar(1) NULL,
    [thousandSep] varchar(1) NULL,
    [cur_code] int NULL,
    CONSTRAINT [PK_tblCurrency] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCurrencyName')
BEGIN
CREATE TABLE [tblCurrencyName] (
    [ID] bigint NOT NULL,
    [CurrCode] varchar(30) NULL,
    [CurrName] varchar(50) NULL,
    CONSTRAINT [PK__tblCurre__3214EC2715F45F55] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCust')
BEGIN
CREATE TABLE [tblCust] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Customer_code] varchar(30) NULL,
    [PostalAddress1] char(50) NULL,
    [PostalAddress2] char(50) NULL,
    [PostalAddress3] char(50) NULL,
    [postalPostCode] char(12) NULL,
    [currencyStr] varchar(5) NULL,
    [UsesPriceTableV71] tinyint NULL,
    [post_code] char(12) NULL,
    [sales_tax_no] char(25) NULL,
    [Account_type] tinyint NULL,
    [industry_type] varchar(8) NULL,
    [insurance_type] tinyint NULL,
    [hire_tax_exempt] char(1) NULL,
    [TaxAuthority1] int NULL,
    [Price_customer_pays] tinyint NULL,
    [customer_number] char(6) NULL,
    [stop_credit] tinyint NULL,
    [Last_bk_seq] varchar(5) NULL,
    [Credit_limit] float NULL,
    [Current] float NULL,
    [Seven_days] float NULL,
    [Fourteen_days] float NULL,
    [Twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [discount_rate] float NULL,
    [last_pmt_amt] float NULL,
    [account_is_zero] char(1) NULL,
    [Monthly_cycle_billing_basis] tinyint NULL,
    [salesperson] char(30) NULL,
    [taxAuthority2] int NULL,
    [contactV6] varchar(35) NULL,
    [OrganisationV6] varchar(50) NULL,
    [Address_l1V6] char(50) NULL,
    [Address_l2V6] char(50) NULL,
    [Address_l3V6] char(50) NULL,
    [webAddress] varchar(80) NULL,
    [emailAddress] varchar(80) NULL,
    [Paymethod] varchar(16) NULL,
    [lastTranDate] datetime NULL,
    [lastPmtDate] datetime NULL,
    [lastBalupDate] datetime NULL,
    [firstUnpayInvDate] datetime NULL,
    [SalesAssignEndDate] datetime NULL,
    [CustCDate] datetime NULL,
    [FirstInvDate] datetime NULL,
    [DisabledCust] char(1) NULL,
    [AcctMgr] varchar(30) NULL,
    [IndustryDescription] varchar(35) NULL,
    [Field1] varchar(25) NULL,
    [Field2] varchar(25) NULL,
    [Field3] varchar(25) NULL,
    [Field4] varchar(25) NULL,
    [Field5] varchar(25) NULL,
    [Field6] varchar(25) NULL,
    [Field7] varchar(25) NULL,
    [Field8] varchar(25) NULL,
    [Field9] varchar(25) NULL,
    [Field10] varchar(25) NULL,
    [Field11] varchar(25) NULL,
    [Field12] varchar(25) NULL,
    [Field13] varchar(25) NULL,
    [Field14] varchar(25) NULL,
    [Field15] datetime NULL,
    [Field16] datetime NULL,
    [Field17] char(1) NULL,
    [Field18] char(1) NULL,
    [Field19] char(1) NULL,
    [Field20] char(1) NULL,
    [Field21] char(1) NULL,
    [Field22] char(1) NULL,
    [Field23] char(1) NULL,
    [Field24] char(1) NULL,
    [Field25] char(1) NULL,
    [Field26] char(1) NULL,
    [Field27] int NULL,
    [Field28] int NULL,
    [Field29] int NULL,
    [Field30] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [Field31] varchar(120) NULL,
    [Field32] varchar(120) NULL,
    [Phone2Ext] varchar(8) NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [StreetCountry] varchar(50) NULL,
    [StreetState] varchar(50) NULL,
    [PostalCountry] varchar(50) NULL,
    [PostalState] varchar(50) NULL,
    [InsuranceCertificate] varchar(25) NULL,
    [InsuredAmount] float NULL,
    [InsuredFromDate] datetime NULL,
    [InsuredToDate] datetime NULL,
    [iLink_ContactID] decimal(10,0) NOT NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [bPONumRequired] bit NULL,
    [CustomerType] tinyint NULL,
    [CampaignID] int NULL,
    [DefaultCustomerDivision] tinyint NULL,
    [FaxCallType] tinyint NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [FaxDialAreaCode] bit NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [EnteredByOpID] decimal(10,0) NULL,
    [bCustomTemplateList] bit NULL,
    [AREmailAddress] varchar(80) NULL,
    [CustTypeForExporting] int NULL,
    [Phone] char(32) NULL,
    [fax] char(16) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [PaymentContactID] int NULL,
    [QBO_id] varchar(10) NULL,
    [StripeID] varchar(24) NULL,
    [isVendor] bit NULL,
    [MinPOAmount] float NULL,
    [Vaccno] varchar(30) NULL,
    [freightzone] int NULL,
    [DefaultBookingContactID] decimal(10,0) NULL,
    [XeroId] varchar(36) NULL,
    CONSTRAINT [PK_tblCust] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCustnote')
BEGIN
CREATE TABLE [tblCustnote] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [customer_code] char(30) NULL,
    [line_no] smallint NULL,
    [text_line] varchar(253) NULL,
    [OperatorID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblCustnote] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblCustom')
BEGIN
CREATE TABLE [tblCustom] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Field1Name] varchar(25) NULL,
    [Field2Name] varchar(25) NULL,
    [Field3Name] varchar(25) NULL,
    [Field4Name] varchar(25) NULL,
    [Field5Name] varchar(25) NULL,
    [Field6Name] varchar(25) NULL,
    [Field7Name] varchar(25) NULL,
    [Field8Name] varchar(25) NULL,
    [Field9Name] varchar(25) NULL,
    [Field10Name] varchar(25) NULL,
    [Field11Name] varchar(25) NULL,
    [Field12Name] varchar(25) NULL,
    [Field13Name] varchar(25) NULL,
    [Field14Name] varchar(25) NULL,
    [Field15Name] varchar(25) NULL,
    [Field16Name] varchar(25) NULL,
    [Field17Name] varchar(25) NULL,
    [Field18Name] varchar(25) NULL,
    [Field19Name] varchar(25) NULL,
    [Field20Name] varchar(25) NULL,
    [Field21Name] varchar(25) NULL,
    [Field22Name] varchar(25) NULL,
    [Field23Name] varchar(25) NULL,
    [Field24Name] varchar(25) NULL,
    [Field25Name] varchar(25) NULL,
    [Field26Name] varchar(25) NULL,
    [Field27Name] varchar(25) NULL,
    [Field28Name] varchar(25) NULL,
    [Field29Name] varchar(25) NULL,
    [Field30Name] varchar(25) NULL,
    [OperatorID] decimal(25,0) NULL,
    [CustID] decimal(10,0) NULL,
    [Field31Name] varchar(25) NULL,
    [Field32Name] varchar(25) NULL,
    CONSTRAINT [PK_tblCustom] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDamagedAssets')
BEGIN
CREATE TABLE [tblDamagedAssets] (
    [Id] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_no] varchar(35) NULL,
    [ProductCode] char(30) NULL,
    [Image] varbinary(MAX) NULL,
    [Message] varchar(MAX) NULL,
    CONSTRAINT [PK_tblDamagedAssets] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDbMaintenance')
BEGIN
CREATE TABLE [tblDbMaintenance] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [ActionName] nvarchar(80) NOT NULL,
    [ActionErrorCode] int NOT NULL,
    [ActionDateTime] datetime NOT NULL,
    [Note] nvarchar(255) NOT NULL,
    [OperatorId] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDBVersion')
BEGIN
CREATE TABLE [tblDBVersion] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VersionNumber] varchar(20) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDeletedAssets')
BEGIN
CREATE TABLE [tblDeletedAssets] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [InvMasID] decimal(10,0) NOT NULL,
    [StockNumber] int NOT NULL,
    CONSTRAINT [PK_tblDeletedAssets] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDeliveryNoticeSignatures')
BEGIN
CREATE TABLE [tblDeliveryNoticeSignatures] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [DelDate] datetime NULL,
    [booking_no] varchar(35) NULL,
    [Receiver] nvarchar(200) NULL,
    [PagePDF] varbinary(MAX) NULL,
    [SignatureImage] varbinary(MAX) NULL,
    [Latitude] decimal(9,6) NULL,
    [Longitude] decimal(9,6) NULL,
    [Address] varchar(MAX) NULL,
    CONSTRAINT [PK_tblDeliveryNoticeSignatures] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDeposit')
BEGIN
CREATE TABLE [tblDeposit] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [receipt_time_h] tinyint NULL,
    [receipt_time_m] tinyint NULL,
    [refund_time_h] tinyint NULL,
    [refund_time_m] tinyint NULL,
    [amount] float NULL,
    [receiptNo] int NULL,
    [cash_type] tinyint NULL,
    [drawer] varchar(30) NULL,
    [bank] varchar(10) NULL,
    [branch] varchar(15) NULL,
    [cheque_no] varchar(10) NULL,
    [Card_name] varchar(128) NULL,
    [Card_no] varchar(128) NULL,
    [RecpDate] datetime NULL,
    [RefundDate] datetime NULL,
    [refund_operators] varchar(50) NULL,
    [receipt_operators] varchar(50) NULL,
    CONSTRAINT [PK_tblDeposit] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDivlist')
BEGIN
CREATE TABLE [tblDivlist] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [div_number] tinyint NULL,
    [div_name] varchar(30) NULL,
    CONSTRAINT [PK_tblDivlist] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDocuSign')
BEGIN
CREATE TABLE [tblDocuSign] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_no] varchar(35) NULL,
    [NameofReceiver] varchar(50) NULL,
    [dateTimeofSig] datetime NULL,
    [GPScoOrdsLon] decimal(9,6) NULL,
    [GPScoOrdsLat] decimal(9,6) NULL,
    [AttachID] decimal(10,0) NULL,
    [Sig] varbinary(MAX) NULL,
    [dsStatus] tinyint NULL,
    [RP2Exported] bit NULL,
    [RP2Imported] bit NULL,
    [SignServiceDocumentId] int NULL,
    CONSTRAINT [PK_tblDocuSign] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblDocuSignDocuments')
BEGIN
CREATE TABLE [tblDocuSignDocuments] (
    [ID] decimal(18,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [EnvID] varchar(50) NULL,
    [EnvURI] varchar(100) NULL,
    [StatDate] datetime NULL,
    [zStatus] varchar(20) NULL,
    [dsStatus] tinyint NULL,
    [ReportType] smallint NULL,
    [AttachID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblDocuSignDocuments] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblEmailNotes')
BEGIN
CREATE TABLE [tblEmailNotes] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [OperatorID] decimal(10,0) NULL,
    [LocationNumber] int NULL,
    [LineNumber] tinyint NULL,
    [Notes] varchar(253) NULL,
    [NoteType] tinyint NULL,
    [SignatureName] varchar(50) NULL,
    CONSTRAINT [PK_tblEmailNotes] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblEvent')
BEGIN
CREATE TABLE [tblEvent] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [event_code] char(30) NULL,
    [event_desc] char(32) NULL,
    [deltime] int NULL,
    [rettime] int NULL,
    [ShowStartTime] int NULL,
    [ShowEndTime] int NULL,
    [ShowTerm] int NULL,
    [showdaysCharged] float NULL,
    [VenueDesc] varchar(50) NULL,
    [venueAdr1] varchar(50) NULL,
    [venueAdr2] varchar(50) NULL,
    [venueAdr3] varchar(50) NULL,
    [VenuePhone1] varchar(16) NULL,
    [VenuePhone2] varchar(16) NULL,
    [VenueFax] varchar(16) NULL,
    [Invoiced] char(1) NULL,
    [Invoice_no] decimal(19,0) NULL,
    [Invoice_amount] float NULL,
    [BookingsInvoiced] tinyint NULL,
    [Attendees] int NULL,
    [Salesperson] varchar(30) NULL,
    [coordinator] varchar(8) NULL,
    [rentalDIsc] float NULL,
    [SalesDisc] float NULL,
    [WeeklyAdjAmount] float NULL,
    [FAPAdjAmount] float NULL,
    [FAPApplies] char(1) NULL,
    [DelvDate] datetime NULL,
    [RetnDate] datetime NULL,
    [ShowSDate] datetime NULL,
    [ShowEDate] datetime NULL,
    [WeeklyAdjApplies] char(1) NULL,
    [DayWeekRate] float NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [PostCode] varchar(12) NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [Locn] int NULL,
    [VenueContactID] decimal(10,0) NULL,
    [VenueContactName] varchar(35) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Ext] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [VenueID] decimal(10,0) NULL,
    [VenueType] int NULL,
    [MBscenario] tinyint NULL,
    [MBID] int NULL,
    [PrepDateTime] datetime NULL,
    [DeprepDateTime] datetime NULL,
    [UseOptimalEquip] bit NULL,
    CONSTRAINT [PK_tblEvent] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblExcelQuery')
BEGIN
CREATE TABLE [tblExcelQuery] (
    [sqlName] varchar(255) NOT NULL,
    [sqlText] text NULL,
    CONSTRAINT [PK_tblExcelQuery] PRIMARY KEY CLUSTERED ([sqlName])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblExpenseCodes')
BEGIN
CREATE TABLE [tblExpenseCodes] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ExpenseCode] varchar(30) NULL,
    [ExpenseName] varchar(50) NULL,
    [DefaultMarkup] float NULL,
    [DefaultRate] float NULL,
    CONSTRAINT [PK_tblExpenseCodes] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblExpenses')
BEGIN
CREATE TABLE [tblExpenses] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_No] varchar(35) NULL,
    [ExpenseCodeID] decimal(18,0) NULL,
    [Description] varchar(50) NULL,
    [TechnicianCode] varchar(30) NULL,
    [Quantity] int NULL,
    [UnitRate] float NULL,
    [Markup] float NULL,
    [Discount] float NULL,
    [ExtendedPrice] float NULL,
    [Billed] bit NULL,
    [InvoiceNumber] decimal(18,0) NULL,
    [HeadingNo] tinyint NULL,
    [PrintExpenseAtEnd] bit NULL,
    [PriceLocked] bit NULL,
    CONSTRAINT [PK_tblExpenses] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFap')
BEGIN
CREATE TABLE [tblFap] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ProdCode] varchar(30) NULL,
    [attendees] int NULL,
    [Quantity] int NULL,
    CONSTRAINT [PK_tblFap] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFastReportProperties')
BEGIN
CREATE TABLE [tblFastReportProperties] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [FastReportName] varchar(120) NULL,
    [FastReportCode] varchar(50) NULL,
    [FR_type] tinyint NOT NULL,
    [PreferenceStr] varchar(MAX) NULL,
    [StoredProcedureName] varchar(50) NULL,
    [ReportTemplate] image NULL,
    [ReportTemplateDsNames] varchar(512) NULL,
    [FR_status] tinyint NULL,
    [SP_DateTime] datetime NULL,
    [ReportDisabled] bit NULL,
    [RPMVCTemplate] image NULL,
    [RPMVCType] tinyint NULL,
    CONSTRAINT [PK_tblfastReportProperties] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFastReportRegionLocationLink')
BEGIN
CREATE TABLE [tblFastReportRegionLocationLink] (
    [rl_type] tinyint NOT NULL,
    [rep_id] decimal(10,0) NOT NULL,
    [rl_id] decimal(10,0) NOT NULL,
    CONSTRAINT [tblFastReportRegionLocationLink_pk] PRIMARY KEY CLUSTERED ([rl_type], [rep_id], [rl_id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFastReportSettings')
BEGIN
CREATE TABLE [tblFastReportSettings] (
    [rep_id] decimal(10,0) NOT NULL,
    [set_code] varchar(50) NOT NULL,
    [set_name] varchar(100) NULL,
    [set_status] tinyint NULL,
    CONSTRAINT [tblFastReportPrefsLink_pk] PRIMARY KEY CLUSTERED ([rep_id], [set_code])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFastReportTypes')
BEGIN
CREATE TABLE [tblFastReportTypes] (
    [frt_id] smallint NOT NULL,
    [frt_name] varchar(100) NULL,
    CONSTRAINT [tblFastReportTypes_pk] PRIMARY KEY CLUSTERED ([frt_id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFreight')
BEGIN
CREATE TABLE [tblFreight] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [freightDesc] varchar(30) NULL,
    [Service] varchar(30) NULL,
    [Ourtruck] char(1) NULL,
    [Zone] int NULL,
    [BaseRate] float NULL,
    [FreightNo] int NULL,
    [Region] int NULL,
    [Location] int NULL,
    [Disabled] bit NULL,
    [BaseCost] float NULL,
    CONSTRAINT [PK_tblFreight] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFreightRates')
BEGIN
CREATE TABLE [tblFreightRates] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [tblFreightID] decimal(10,0) NULL,
    [rate_no] int NULL,
    [fromweight] float NULL,
    [Rate] float NULL,
    [Cost] float NULL,
    CONSTRAINT [PK_tblFreightRates] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblFRTypesRPMVC')
BEGIN
CREATE TABLE [tblFRTypesRPMVC] (
    [frt_id] smallint NOT NULL,
    [frt_name] varchar(100) NULL,
    CONSTRAINT [tblFRTypesRPMVC_pk] PRIMARY KEY CLUSTERED ([frt_id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblGenNextId')
BEGIN
CREATE TABLE [tblGenNextId] (
    [gen_name] varchar(50) NULL,
    [gen_value] decimal(18,0) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblGLAccounts')
BEGIN
CREATE TABLE [tblGLAccounts] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [LocationNum] int NULL,
    [RentalAcctNum] varchar(25) NULL,
    [SaleAcctNum] varchar(25) NULL,
    [LossAcctNum] varchar(25) NULL,
    [DeliveryAcctNum] varchar(25) NULL,
    [LabourAcctNum] varchar(25) NULL,
    [SundryAcctNum] varchar(25) NULL,
    [InsuranceAcctNum] varchar(25) NULL,
    [StampAcctNum] varchar(25) NULL,
    [VatHoldAcctNum] varchar(25) NULL,
    [VatAcctNum] varchar(25) NULL,
    [SalesTaxAcctNum] varchar(25) NULL,
    [BankAcctNum] varchar(25) NULL,
    [ControlAcctNum] varchar(25) NULL,
    [SaleOfAssetAcctNum] varchar(25) NULL,
    [DiscountAcctNum] varchar(25) NULL,
    [AccPacSource] varchar(25) NULL,
    [ProdIncomeAcctNum] varchar(25) NULL,
    [Exp_To_v71] int NULL,
    [SageExportPath] varchar(79) NULL,
    [accpac_integration_level] int NULL,
    [invoice_exp_filename] varchar(40) NULL,
    [credit_exp_filename] varchar(40) NULL,
    [ExportBatch_Number] decimal(18,0) NULL,
    [ExportPay_Number] decimal(18,0) NULL,
    [cust_exp_filenameV2] varchar(40) NULL,
    [sage_breakdown] int NULL,
    [sa_nominal_ac] char(8) NULL,
    [sa_dept_code] smallint NULL,
    [sa_tax_code] char(2) NULL,
    [sybiz_period_number] tinyint NULL,
    [sybiz_sales_an_code] tinyint NULL,
    [state_sales_tax_authority] varchar(8) NULL,
    [county_tax_authority] varchar(8) NULL,
    [UseLongYear] bit NULL,
    [import_auto] bit NULL,
    [accpac_path] varchar(30) NULL,
    [accpac_filename] varchar(12) NULL,
    [accpac_param] varchar(9) NULL,
    [Is_Peachtree_LineExp] bit NULL,
    [Def_Peach_RevCode] char(6) NULL,
    [accpac_terms_code_cash] varchar(2) NULL,
    [accpac_terms_code_7] varchar(2) NULL,
    [accpac_terms_code_30] varchar(2) NULL,
    [accpac_applic] varchar(2) NULL,
    [myobTax1] varchar(3) NULL,
    [myobTax2] varchar(3) NULL,
    [myobDisc] varchar(6) NOT NULL,
    [myobCharge] varchar(6) NOT NULL,
    [stampduty_tax_Authority] int NOT NULL,
    [ExtraTaxCode] varchar(25) NULL,
    [ExportQBAccountNum] bit NULL,
    [ExportQBDiscounts] bit NULL,
    [CreditSurchargeAccNum] varchar(30) NULL,
    [SpecialPrefixAccount] varchar(25) NULL,
    [EventManagementAccNum] varchar(30) NULL,
    [QBOnlineFormat] tinyint NULL,
    [QBSandboxID] varchar(255) NULL,
    [QBSandboxSecret] varchar(255) NULL,
    [QBProductionID] varchar(255) NULL,
    [QBProductionSecret] varchar(255) NULL,
    [QBTaxNumber] int NULL,
    CONSTRAINT [PK_tblGLAccounts] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblGroup')
BEGIN
CREATE TABLE [tblGroup] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Group_code] char(30) NULL,
    [group_descV6] char(50) NULL,
    [days_table] tinyint NULL,
    [company] tinyint NULL,
    [seqNo] decimal(19,0) NULL,
    [GroupProductType] tinyint NULL,
    [AllowDiscount] char(1) NULL,
    [DefaultVendorCode] varchar(30) NULL,
    [pricingscheme] tinyint NULL,
    [DisplayColour] varchar(7) NULL,
    [OLGroupDesc] varchar(50) NULL,
    [OverrideMultiRateWithPFT] bit NULL,
    [CustomIcon] varchar(60) NULL,
    [MasterBillingProductType] int NULL,
    CONSTRAINT [PK_tblGroup] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHccrooms')
BEGIN
CREATE TABLE [tblHccrooms] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [locn] int NULL,
    [RoomName] varchar(50) NULL,
    [kit] varchar(8) NULL,
    [floorplanfilename] varchar(120) NULL,
    [subrooms] tinyint NULL,
    CONSTRAINT [PK_tblHccrooms] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHeadarch')
BEGIN
CREATE TABLE [tblHeadarch] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [del_date] int NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_date] int NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [days_using] int NULL,
    [days_charged_filler] int NULL,
    [heading_desc] varchar(79) NULL,
    [Own_equip] char(1) NULL,
    [equip_hire_tot] float NULL,
    [labour_tot] float NULL,
    [sales_tot] float NULL,
    [hire_disc_perc] float NULL,
    [sale_disc_perc] float NULL,
    [hire_disc_amt] float NULL,
    [sale_disc_amt] float NULL,
    [sundry_tot] float NULL,
    [sundry_cost_tot] float NULL,
    [hire_undisc_amt] float NULL,
    [duty] float NULL,
    [tax1_tot] float NULL,
    [losses] float NULL,
    [DelvDate] datetime NULL,
    [RetnDate] datetime NULL,
    [days_charged_v51] float NULL,
    [NodeCollapsed] bit NULL,
    [venueroomID] int NULL,
    [VenueType] int NULL,
    [BookedDateTime] datetime NULL,
    [BookedTimeH] tinyint NULL,
    [BookedTimeM] tinyint NULL,
    [BookedTimeS] tinyint NULL,
    [view_client] bit NULL,
    [view_logi] bit NULL,
    [Logi_HeadingNo] tinyint NULL,
    [hasDates] bit NULL,
    [BayNo] int NULL,
    [sales_undisc_amt] float NULL,
    [RentalLineDiscountAmt] float NULL,
    [SalesLineDiscountAmt] float NULL,
    CONSTRAINT [PK_tblHeadarch] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHeading')
BEGIN
CREATE TABLE [tblHeading] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [del_date] int NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_date] int NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [days_using] int NULL,
    [days_charged_filler] int NULL,
    [heading_desc] varchar(79) NULL,
    [Own_equip] char(1) NULL,
    [equip_hire_tot] float NULL,
    [labour_tot] float NULL,
    [sales_tot] float NULL,
    [hire_disc_perc] float NULL,
    [sale_disc_perc] float NULL,
    [hire_disc_amt] float NULL,
    [sale_disc_amt] float NULL,
    [sundry_tot] float NULL,
    [sundry_cost_tot] float NULL,
    [hire_undisc_amt] float NULL,
    [duty] float NULL,
    [tax1_tot] float NULL,
    [losses] float NULL,
    [DelvDate] datetime NULL,
    [RetnDate] datetime NULL,
    [days_charged_v51] float NULL,
    [NodeCollapsed] bit NULL,
    [venueroomID] int NULL,
    [VenueType] int NULL,
    [BookedDateTime] datetime NULL,
    [BookedTimeH] tinyint NULL,
    [BookedTimeM] tinyint NULL,
    [BookedTimeS] tinyint NULL,
    [view_client] bit NULL,
    [view_logi] bit NULL,
    [Logi_HeadingNo] tinyint NULL,
    [hasDates] bit NULL,
    [BayNo] int NULL,
    [sales_undisc_amt] float NULL,
    [RentalLineDiscountAmt] float NULL,
    [SalesLineDiscountAmt] float NULL,
    CONSTRAINT [PK_tblHeading] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHistbks')
BEGIN
CREATE TABLE [tblHistbks] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viaV71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [Item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_nameV6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setupTimeV61] varchar(4) NULL,
    [RehearsalTime] varchar(4) NULL,
    [StrikeTime] varchar(4) NULL,
    [Trans_to_locn] int NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEndTime] varchar(4) NULL,
    [transferNo] decimal(19,0) NULL,
    [currencyStr] varchar(5) NULL,
    [BookingProgressStatus] tinyint NULL,
    [ConfirmedBy] varchar(35) NULL,
    [ConfirmedDocRef] varchar(50) NULL,
    [VenueRoom] varchar(35) NULL,
    [expAttendees] int NULL,
    [HourBooked] tinyint NULL,
    [MinBooked] tinyint NULL,
    [SecBooked] tinyint NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [HorCCroom] int NULL,
    [subrooms] char(12) NULL,
    [truckOut] int NULL,
    [truckIn] int NULL,
    [tripOut] tinyint NULL,
    [tripIn] tinyint NULL,
    [showName] varchar(50) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [OurNumberDel] char(1) NULL,
    [OurNumberRet] char(1) NULL,
    [DatesAndTimesEnabled] char(1) NULL,
    [Government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [ContactLoadedIntoVenue] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [OrganizationV6] varchar(50) NULL,
    [Salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Inv_date] datetime NULL,
    [ShowSdate] datetime NULL,
    [ShowEdate] datetime NULL,
    [SetDate] datetime NULL,
    [ADelDate] datetime NULL,
    [SDate] datetime NULL,
    [RehDate] datetime NULL,
    [ConDate] datetime NULL,
    [TOutDate] datetime NULL,
    [TInDate] datetime NULL,
    [PreDate] datetime NULL,
    [ConByDate] datetime NULL,
    [bookingPrinted] char(1) NULL,
    [CustCode] varchar(30) NULL,
    [ExtendedFrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [PotPercent] float NULL,
    [Referral] varchar(50) NULL,
    [EventType] varchar(20) NULL,
    [Priority] int NULL,
    [InvoiceStage] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [PickupRetDate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [MaxBookingValue] float NULL,
    [UsesPriceTable] int NULL,
    [DateToInvoice] datetime NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [ServCont] char(1) NULL,
    [RentalType] tinyint NULL,
    [PrintedPayTerm] varchar(40) NULL,
    [PaymentOptions] tinyint NULL,
    [UseBillSchedule] char(1) NULL,
    [Tax2] float NULL,
    [ContactID] decimal(9,0) NULL,
    [ShortHours] tinyint NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [dtExpected_ReturnDate] datetime NOT NULL,
    [vcExpected_ReturnTime] varchar(4) NOT NULL,
    [vcTruckOutTime] varchar(4) NOT NULL,
    [vcTruckInTime] varchar(4) NOT NULL,
    [CustID] decimal(10,0) NOT NULL,
    [VenueID] int NOT NULL,
    [LateChargesApplied] bit NULL,
    [shortagesAreTransfered] bit NULL,
    [VenueContactID] int NULL,
    [VenueContact] varchar(50) NULL,
    [VenueContactPhoneID] int NULL,
    [LTBillingOption] tinyint NULL,
    [DressCode] varchar(35) NULL,
    [Collection] float NULL,
    [FuelSurchargeRate] float NULL,
    [FreightLocked] bit NULL,
    [LabourLocked] bit NULL,
    [RentalLocked] bit NULL,
    [PriceLocked] bit NULL,
    [insurance_type] tinyint NULL,
    [EntryDate] datetime NULL,
    [CreditSurchargeRate] float NULL,
    [CreditSurchargeAmount] float NULL,
    [DisableTreeOrder] bit NULL,
    [ConfirmationFinancials] varchar(30) NULL,
    [EventManagementRate] float NULL,
    [EventManagementAmount] float NULL,
    [EquipmentModified] bit NULL,
    [CrewStatusColumn] tinyint NULL,
    [LoadDateTime] datetime NULL,
    [UnloadDateTime] datetime NULL,
    [DeprepDateTime] datetime NULL,
    [DeprepOn] bit NOT NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [ScheduleDatesOn] varchar(10) NULL,
    [bBookingIsComplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingID] int NULL,
    [MasterBillingMethod] tinyint NULL,
    [schedHeadEquipSpan] tinyint NULL,
    [TaxabPCT] float NOT NULL,
    [UntaxPCT] float NOT NULL,
    [Tax1PCT] float NOT NULL,
    [Tax2PCT] float NOT NULL,
    [PaymentContactID] int NULL,
    [sale_of_asset_undisc_amt] float NULL,
    [LockedForScanning] bit NULL,
    [OldAssignedTo] varchar(35) NULL,
    [DateLastModified] datetime NULL,
    [crew_cnt] int NOT NULL,
    [rTargetMargin] float NOT NULL,
    [rProfitMargin] float NOT NULL,
    [ContractNo] varchar(18) NULL,
    [SyncType] tinyint NOT NULL,
    [AllLocnAvail] bit NULL,
    [HasQT] bit NOT NULL,
    [HasDAT] bit NOT NULL,
    [AllHeadingsDaysOverride] bit NULL,
    [BayNo] int NULL,
    [Paymethod] varchar(25) NULL,
    CONSTRAINT [PK_tblHistbks] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHistCrew')
BEGIN
CREATE TABLE [tblHistCrew] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] int NULL,
    [price] float NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [person] char(30) NULL,
    [task] tinyint NULL,
    [TechRate] float NULL,
    [TechPay] float NULL,
    [unitRate] float NULL,
    [techrateIsHourorDay] char(1) NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [GroupSeqNo] int NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [bit_field_v41] tinyint NULL,
    [SubrentalLinkID] int NULL,
    [AssignTo] varchar(35) NULL,
    [days_using] float NULL,
    [MinimumHours] float NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [Notes] varchar(512) NULL,
    [AdmModifiedNoteDate] datetime NULL,
    [JobTimeZone] tinyint NULL,
    [TechTimezone] tinyint NULL,
    [JobOffered] bit NULL,
    [JobOffereddate] datetime NULL,
    [JobAccepted] bit NULL,
    [JobAcceptedDate] datetime NULL,
    [Conflict] bit NULL,
    [PrintOnQuote] bit NULL,
    [PrintOnInvoice] bit NULL,
    [JobTechOfferStatus] tinyint NULL,
    [JobTechOfferDate] datetime NULL,
    [JobTechNotes] varchar(512) NULL,
    [CrewClientNotes] varchar(40) NULL,
    CONSTRAINT [PK_tblHistCrew] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHistitm')
BEGIN
CREATE TABLE [tblHistitm] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [trans_type_v41] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [From_locn] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [bit_field_v41] tinyint NULL,
    [TimeBookedH] tinyint NULL,
    [TimeBookedM] tinyint NULL,
    [TimeBookedS] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [techRateorDaysCharged] float NULL,
    [TechPay] float NULL,
    [unitRate] float NULL,
    [prep_on] char(1) NULL,
    [Comment_desc_v42] char(70) NULL,
    [AssignTo] varchar(255) NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [BookDate] datetime NULL,
    [PDate] datetime NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [DayWeekRate] float NULL,
    [QtyReserved] int NULL,
    [AddedAtCheckout] bit NULL,
    [GroupSeqNo] int NULL,
    [SubRentalLinkID] int NOT NULL,
    [AssignType] tinyint NOT NULL,
    [QtyShort] int NOT NULL,
    [QtyAvailable] int NULL,
    [PackageLevel] smallint NULL,
    [BeforeDiscountAmount] float NULL,
    [QuickTurnAroundQty] int NULL,
    [InRack] bit NULL,
    [CostPrice] float NULL,
    [NodeCollapsed] bit NULL,
    [AvailRecFlag] bit NOT NULL,
    [booking_id] int NULL,
    [Undisc_amt] float NULL,
    [View_Logi] bit NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [ParentCode] varchar(30) NULL,
    [EstSubRentalCost] float NULL,
    [EstSubRentalDays] smallint NULL,
    [VendorID] int NULL,
    [Notes] varchar(MAX) NULL,
    [UseEstSubHireOverride] bit NULL,
    [Estimated_sub_hire_v5] float NULL,
    [resolvedDiscrep] bit NOT NULL,
    [QTBookingNo] varchar(35) NULL,
    [QTSource] tinyint NULL,
    [warehouseMutedPerOER] bit NULL,
    [techrateIsHourorDay] char(1) NULL,
    CONSTRAINT [PK_tblHistitm] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHistSundry')
BEGIN
CREATE TABLE [tblHistSundry] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] int NULL,
    [sub_seq_no] int NULL,
    [sundry_desc] varchar(50) NULL,
    [sundry_cost] float NULL,
    [sundry_price] float NULL,
    [GroupSeqNo] int NULL,
    [Discount] float NULL,
    [trans_qty] int NULL,
    [restock_charge] tinyint NULL,
    [RevenueCode] varchar(50) NULL,
    [sundry_markup_percentage] float NULL,
    [view_client] bit NULL,
    [view_logi] bit NULL,
    [Logi_HeadingNo] tinyint NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [SundryType] tinyint NULL,
    CONSTRAINT [PK_tblHistSundry] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHolidays')
BEGIN
CREATE TABLE [tblHolidays] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [DateF] datetime NULL,
    [Description] varchar(35) NULL,
    [HolidayRegion] int NULL,
    [HolidayLocation] int NULL,
    CONSTRAINT [PK_tblHolidays] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblHourlyRate')
BEGIN
CREATE TABLE [tblHourlyRate] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [HourlyRateName] varchar(50) NULL,
    CONSTRAINT [PK_tblHourlyRate] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInstruct')
BEGIN
CREATE TABLE [tblInstruct] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [inst_instru] text NULL,
    CONSTRAINT [PK_tblInstruct] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvAudit')
BEGIN
CREATE TABLE [tblInvAudit] (
    [AuditID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [TableName] nvarchar(128) NOT NULL,
    [ActionType] tinyint NOT NULL,
    [ChangedBy] decimal(10,0) NOT NULL,
    [ChangeDate] datetime2 NOT NULL,
    [Notes] varchar(50) NULL,
    CONSTRAINT [PK__tblInvAu__A17F23B859AB8FFD] PRIMARY KEY CLUSTERED ([AuditID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvAuditDetail')
BEGIN
CREATE TABLE [tblInvAuditDetail] (
    [DetailID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [AuditID] decimal(10,0) NOT NULL,
    [ColumnName] nvarchar(128) NOT NULL,
    [Finalized] bit NULL,
    [OldValue] nvarchar(MAX) NULL,
    [NewValue] nvarchar(MAX) NULL,
    [RecordID] decimal(10,0) NULL,
    CONSTRAINT [PK__tblInvAu__135C314D66956916] PRIMARY KEY CLUSTERED ([DetailID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvdet')
BEGIN
CREATE TABLE [tblInvdet] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] int NULL,
    [sub_seq_no] tinyint NULL,
    [Product_code] varchar(30) NULL,
    [Invoice_date] int NULL,
    [trans_type] tinyint NULL,
    [first_date] int NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_date] int NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [Quantity] int NULL,
    [Price] float NULL,
    [item_type] tinyint NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [days_using] int NULL,
    [person] varchar(2) NULL,
    [bit_field] tinyint NULL,
    [sundry_desc] varchar(50) NULL,
    [sundry_cost] float NULL,
    [InvDate] datetime NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [sundry_price] float NULL,
    [RevCenterLocn] int NULL,
    [GLAccount] varchar(25) NULL,
    [Customer_Code] varchar(30) NULL,
    [Invoice_Cred_No] decimal(19,0) NULL,
    [Discount_Value] float NULL,
    [Invoice_Amount] float NULL,
    [Division] tinyint NULL,
    [Description] varchar(30) NULL,
    [GroupSeqNo] int NULL,
    [ManualEntryID] decimal(19,0) NULL,
    CONSTRAINT [PK_tblInvdet] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvhead')
BEGIN
CREATE TABLE [tblInvhead] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Invoice_cred_no] decimal(19,0) NULL,
    [Customer_code] char(30) NULL,
    [inv_cred_note] tinyint NULL,
    [Taxauthority1] int NULL,
    [Taxauthority2] int NULL,
    [Booking_seq_no] varchar(5) NULL,
    [Invoice_amount] float NULL,
    [payment_type] tinyint NULL,
    [hire_price] float NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [labour] float NULL,
    [discount_rate] float NULL,
    [insurance_v5] float NULL,
    [un_disc_amount] float NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [sundry_total] float NULL,
    [Discount_value] float NULL,
    [Sales_undisc_amt] float NULL,
    [division] tinyint NULL,
    [booking_type] tinyint NULL,
    [losses] float NULL,
    [first_month] char(1) NULL,
    [credit_amount] float NULL,
    [tax2] float NULL,
    [Government] char(1) NULL,
    [comment_line] varchar(30) NULL,
    [event_code] varchar(30) NULL,
    [sale_of_asset] float NULL,
    [currencyStr] char(5) NULL,
    [InvDate] datetime NULL,
    [RenInvSDate] datetime NULL,
    [RenInvEDate] datetime NULL,
    [CredDate] datetime NULL,
    [StageNo] int NULL,
    [Inv_ToDate] datetime NULL,
    [Booking_No] varchar(35) NULL,
    [CreditSurchargeAmount] float NULL,
    [EventManagementAmount] float NULL,
    [Location] int NULL,
    [CustomCreditNoteNumber] decimal(19,0) NULL,
    [TaxabPCT] float NOT NULL,
    [UntaxPCT] float NOT NULL,
    [Tax1PCT] float NOT NULL,
    [Tax2PCT] float NOT NULL,
    [Booking_amount] float NULL,
    [webcode] varchar(16) NULL,
    [TaxExemptLabour] float NULL,
    [Archived] bit NOT NULL,
    [SubRentPrice] float NOT NULL,
    [exportedToAcc] bit NULL,
    [exportedDateTime] datetime NULL,
    [expLinkfield] varchar(1024) NULL,
    [Paid] bit NULL,
    [InvoiceCreationDate] datetime NULL,
    [DepositInvoice] bit NULL,
    [XeroId] varchar(36) NULL,
    CONSTRAINT [PK_tblInvhead] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvmas')
BEGIN
CREATE TABLE [tblInvmas] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [seq_no] decimal(19,0) NULL,
    [product_code] char(30) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [first_trans] int NULL,
    [product_Config] tinyint NULL,
    [product_type_v41] tinyint NULL,
    [indiv_hire_sale] char(1) NULL,
    [on_hand] float NULL,
    [Ord_unit] varchar(4) NULL,
    [re_ord_level] float NULL,
    [lead_time] float NULL,
    [quantity_on_order] float NULL,
    [sales_tax_rate] tinyint NULL,
    [cost_price] float NULL,
    [retail_price] float NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [webCatalog] char(1) NULL,
    [unit_weight] float NULL,
    [unit_volume] float NULL,
    [suppress_from_del_sch] char(1) NULL,
    [revenue_code] varchar(6) NULL,
    [components_del] char(1) NULL,
    [components_inv] char(1) NULL,
    [components_quote] char(1) NULL,
    [asset_track] char(1) NULL,
    [Notes_exist] char(1) NULL,
    [notes_on_quote] char(1) NULL,
    [notes_on_del] char(1) NULL,
    [notes_on_inv] char(1) NULL,
    [DisallowDisc] char(1) NULL,
    [PictureFilename] varchar(MAX) NULL,
    [CountryOfOrigin] varchar(25) NULL,
    [IsInTrashCan] char(1) NULL,
    [prodRoadCase] tinyint NULL,
    [person_required] char(1) NULL,
    [ContactID] decimal(10,0) NULL,
    [GLCode] varchar(25) NULL,
    [UseWeeklyRate] char(1) NULL,
    [isGenericItem] char(1) NULL,
    [MfctPartNumber] varchar(30) NULL,
    [NonTrackedBarcode] varchar(30) NULL,
    [DefaultDiscount] float NULL,
    [PrintedDesc] varchar(50) NULL,
    [VendorCode] varchar(30) NULL,
    [DefaultDayRateID] tinyint NOT NULL,
    [DefaultHourlyRateID] decimal(18,0) NOT NULL,
    [SubCategory] varchar(30) NULL,
    [lastPurchasePrice] float NULL,
    [RegionNumber] int NOT NULL,
    [zColor] varchar(25) NULL,
    [rLength] float NOT NULL,
    [rWidth] float NOT NULL,
    [rHeight] float NOT NULL,
    [zModelNo] varchar(25) NULL,
    [cyTurnCosts] money NOT NULL,
    [bCustomPrintouts] bit NOT NULL,
    [CheckoutDoc] bit NOT NULL,
    [bTestEveryUnit] bit NULL,
    [UnavailableUntilTested] bit NULL,
    [TestRequired] bit NULL,
    [DisallowTransfer] bit NULL,
    [Location] int NOT NULL,
    [EntryDate] datetime NULL,
    [EnforceMinHours] bit NULL,
    [MinimumHours] float NULL,
    [bDisallowRegionTransfer] bit NULL,
    [DontPrintOnCrossRentPO] bit NOT NULL,
    [RFIDTag] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [OLExternalDesc] varchar(50) NULL,
    [DefaultVendorCode] varchar(30) NULL,
    [iPictureSize] int NULL,
    [LastUpdate] datetime NULL,
    [DefaultRateForUnassigned] float NULL,
    [bExpandWhenAdded] bit NULL,
    [BinLocation] varchar(100) NULL,
    [bExcludeFromDataExport] bit NULL,
    [iMonthsToDepreciate] int NULL,
    [rDailyCostOfOwning] float NULL,
    [CustomIcon] varchar(60) NULL,
    [On_handInRack] int NULL,
    [MBowner] tinyint NULL,
    [taxableExempt] bit NULL,
    [bAutoCheckout] bit NULL,
    [ImportTestResultsFrom] int NULL,
    [WarehouseActive] bit NULL,
    [OverridePriceChangeRestriction] smallint NOT NULL,
    [bProductIsFreight] bit NOT NULL,
    [BasedOnPurchCost] float NOT NULL,
    [CAPEXGLCode] varchar(14) NULL,
    [CrossHireGLCode] varchar(14) NULL,
    [HSCode] varchar(20) NULL,
    [AveKwHusage] float NULL,
    CONSTRAINT [PK_tblInvmas] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvmas_Labour_Rates')
BEGIN
CREATE TABLE [tblInvmas_Labour_Rates] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [tblInvmasID] decimal(10,0) NULL,
    [rate_no] int NULL,
    [Labour_rate] float NULL,
    [Locn] int NULL,
    [IsDefault] bit NULL,
    [DefaultRate] bit NULL,
    CONSTRAINT [PK_tblInvmas_Labour_Rates] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvmas_PriceBreak')
BEGIN
CREATE TABLE [tblInvmas_PriceBreak] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [tblInvmasID] decimal(10,0) NULL,
    [break_no] int NULL,
    [break_qty] int NULL,
    [unit_price] float NULL,
    CONSTRAINT [PK_tblInvmas_PriceBreak] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblInvoiceNo')
BEGIN
CREATE TABLE [tblInvoiceNo] (
    [InvNoID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [next_invoice_no] decimal(10,0) NULL,
    [Locked] char(1) NULL,
    [LockDate] datetime NULL,
    [LockTimeH] smallint NULL,
    [LockTimeM] smallint NULL,
    [LockedBy] int NULL,
    [LastExport] datetime NULL,
    [MonthClosedDate] datetime NULL,
    CONSTRAINT [PK_tblInvoiceNo] PRIMARY KEY CLUSTERED ([InvNoID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblItemAdj')
BEGIN
CREATE TABLE [tblItemAdj] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ProjectCode] varchar(30) NULL,
    [product_code] varchar(30) NULL,
    [quantity] int NULL,
    [amount] float NULL,
    [from_locn] int NULL,
    [trans_to_locn] int NULL,
    [OutsDate] datetime NULL,
    [InsDate] datetime NULL,
    [ret_to_locn] int NULL,
    CONSTRAINT [PK_tblItemAdj] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblItemtran')
BEGIN
CREATE TABLE [tblItemtran] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [trans_type_v41] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [From_locn] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [bit_field_v41] tinyint NULL,
    [TimeBookedH] tinyint NULL,
    [TimeBookedM] tinyint NULL,
    [TimeBookedS] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [techRateorDaysCharged] float NULL,
    [TechPay] float NULL,
    [unitRate] float NULL,
    [prep_on] char(1) NULL,
    [Comment_desc_v42] char(70) NULL,
    [AssignTo] varchar(255) NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [BookDate] datetime NULL,
    [PDate] datetime NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [DayWeekRate] float NULL,
    [QtyReserved] int NULL,
    [AddedAtCheckout] bit NULL,
    [GroupSeqNo] int NULL,
    [SubRentalLinkID] int NOT NULL,
    [AssignType] tinyint NOT NULL,
    [QtyShort] int NOT NULL,
    [QtyAvailable] int NULL,
    [PackageLevel] smallint NULL,
    [BeforeDiscountAmount] float NULL,
    [QuickTurnAroundQty] int NULL,
    [InRack] bit NULL,
    [CostPrice] float NULL,
    [NodeCollapsed] bit NULL,
    [AvailRecFlag] bit NOT NULL,
    [booking_id] int NULL,
    [Undisc_amt] float NULL,
    [View_Logi] bit NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [ParentCode] varchar(30) NULL,
    [EstSubRentalCost] float NULL,
    [EstSubRentalDays] smallint NULL,
    [VendorID] int NULL,
    [Notes] varchar(MAX) NULL,
    [UseEstSubHireOverride] bit NULL,
    [Estimated_sub_hire_v5] float NULL,
    [resolvedDiscrep] bit NOT NULL,
    [QTBookingNo] varchar(35) NULL,
    [QTSource] tinyint NULL,
    [warehouseMutedPerOER] bit NULL,
    [techrateIsHourorDay] char(1) NULL,
    [OriginalBookNo] varchar(35) NULL,
    CONSTRAINT [PK_tblItemtran] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblItemTranExtended')
BEGIN
CREATE TABLE [tblItemTranExtended] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_no_v32] varchar(35) NULL,
    [mutedOnFinalCheckout] bit NULL,
    [IT_SubRentalLinkID] decimal(10,0) NULL,
    CONSTRAINT [PK__tblItemT__3214EC27BB9F314D] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblItemTranNextLinkID')
BEGIN
CREATE TABLE [tblItemTranNextLinkID] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_no] varchar(35) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblItemtranSnapshot')
BEGIN
CREATE TABLE [tblItemtranSnapshot] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [trans_type_v41] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [From_locn] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [bit_field_v41] tinyint NULL,
    [TimeBookedH] tinyint NULL,
    [TimeBookedM] tinyint NULL,
    [TimeBookedS] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [techRateorDaysCharged] float NULL,
    [TechPay] float NULL,
    [unitRate] float NULL,
    [prep_on] char(1) NULL,
    [Comment_desc_v42] char(70) NULL,
    [AssignTo] varchar(255) NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [BookDate] datetime NULL,
    [PDate] datetime NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [DayWeekRate] float NULL,
    [QtyReserved] int NULL,
    [AddedAtCheckout] bit NULL,
    [GroupSeqNo] int NULL,
    [SubRentalLinkID] int NOT NULL,
    [AssignType] tinyint NOT NULL,
    [QtyShort] int NOT NULL,
    [QtyAvailable] int NULL,
    [PackageLevel] smallint NULL,
    [BeforeDiscountAmount] float NULL,
    [QuickTurnAroundQty] int NULL,
    [InRack] bit NULL,
    [CostPrice] float NULL,
    [NodeCollapsed] bit NULL,
    [AvailRecFlag] bit NOT NULL,
    [booking_id] int NULL,
    [Undisc_amt] float NULL,
    [View_Logi] bit NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [ParentCode] varchar(30) NULL,
    [EstSubRentalCost] float NULL,
    [EstSubRentalDays] smallint NULL,
    [VendorID] int NULL,
    [Notes] varchar(MAX) NULL,
    [UseEstSubHireOverride] bit NULL,
    [Estimated_sub_hire_v5] float NULL,
    [resolvedDiscrep] bit NOT NULL,
    [QTBookingNo] varchar(35) NULL,
    [QTSource] tinyint NULL,
    [warehouseMutedPerOER] bit NULL,
    [techrateIsHourorDay] char(1) NULL,
    [OriginalBookNo] varchar(35) NULL,
    [ItemTranID] decimal(10,0) NULL,
    [TimeStamp] datetime NULL,
    [LogMsg] varchar(1000) NULL,
    CONSTRAINT [PK_tblItemtranSnapShot] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLabourHours')
BEGIN
CREATE TABLE [tblLabourHours] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [HourlyRateID] decimal(10,0) NULL,
    [DayType] tinyint NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    CONSTRAINT [PK_tblLabourHours] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLabourRates')
BEGIN
CREATE TABLE [tblLabourRates] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ContactID] decimal(18,0) NULL,
    [ProductCode] char(30) NULL,
    [RateType] tinyint NULL,
    [HourlyRate] float NULL,
    [DailyRate] float NULL,
    [HalfDayRate] float NOT NULL,
    [RateApproved] bit NULL,
    [DateRateConfirmed] datetime NULL,
    [RateConfirmedBy] int NULL,
    [SkillLevel] tinyint NULL,
    CONSTRAINT [PK_tblLabourRates] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLangTerms')
BEGIN
CREATE TABLE [tblLangTerms] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [TermCell] varchar(8) NULL,
    [TermPhone1] varchar(25) NULL,
    [TermPhone2] varchar(25) NULL,
    [TermFax] varchar(25) NULL,
    CONSTRAINT [PK_tblLangTerms] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLinkBookingActivity')
BEGIN
CREATE TABLE [tblLinkBookingActivity] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [BookingID] decimal(10,0) NULL,
    [ActivityID] decimal(10,0) NULL,
    [IsProject] char(1) NULL,
    [BookProjCode] varchar(35) NULL,
    CONSTRAINT [PK_tblLinkBookingActivity] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLinkCustContact')
BEGIN
CREATE TABLE [tblLinkCustContact] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Customer_Code] varchar(30) NULL,
    [ContactID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblLinkCustContact] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLinkSaleCostAssetran')
BEGIN
CREATE TABLE [tblLinkSaleCostAssetran] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [SaleCostID] decimal(18,0) NOT NULL,
    [AssetranID] decimal(18,0) NOT NULL,
    CONSTRAINT [PK_tblLinkSaleCostAssetran] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLinkShowCompany')
BEGIN
CREATE TABLE [tblLinkShowCompany] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ShowID] decimal(10,0) NULL,
    [CustID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblLinkShowCompany] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLinkVenueContact')
BEGIN
CREATE TABLE [tblLinkVenueContact] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VenueID] decimal(10,0) NULL,
    [ContactID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblLinkVenueContacts] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblListHead')
BEGIN
CREATE TABLE [tblListHead] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [CallListName] varchar(50) NULL,
    [MultipleDays] char(1) NULL,
    [SkipWeekends] char(1) NULL,
    [ActivitiesPerDay] int NULL,
    [ScriptID] decimal(10,0) NULL,
    [IsDistributionList] char(1) NULL,
    [HTMLFile] varchar(100) NULL,
    [IsProductList] char(1) NULL,
    [BookingProgressOpts] char(5) NULL,
    [CompanyTypes] char(4) NULL,
    [BookingTypes] char(3) NULL,
    [LowestQty] int NULL,
    [HighestQty] int NULL,
    [StartDate] datetime NULL,
    [EndDate] datetime NULL,
    [Industry] varchar(35) NULL,
    [ShowName] varchar(50) NULL,
    [EventType] varchar(20) NULL,
    [Source] varchar(50) NULL,
    [ByCustomer] char(1) NULL,
    [StartValue] float NULL,
    [EndValue] float NULL,
    CONSTRAINT [PK_tblListHead] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLocking')
BEGIN
CREATE TABLE [tblLocking] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_no] varchar(35) NULL,
    [LockOpsID] decimal(10,0) NULL,
    [LockOpsDate] datetime NULL,
    [LockOpsTime] varchar(4) NULL,
    [LockType] smallint NULL,
    [Locn] int NOT NULL,
    [AssignTo_Lock] varchar(35) NULL,
    [CrewID] decimal(10,0) NULL,
    [EntryDateTime] datetime NULL,
    CONSTRAINT [PK_tblLocking] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLocnlist')
BEGIN
CREATE TABLE [tblLocnlist] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Locn_number] int NULL,
    [Locn_name] varchar(50) NULL,
    [Lname] varchar(40) NULL,
    [LAdr1] varchar(40) NULL,
    [LAdr2] varchar(40) NULL,
    [Ladr3] varchar(40) NULL,
    [Lphone] varchar(16) NULL,
    [Lfax] varchar(16) NULL,
    [AutoTransfer] char(1) NULL,
    [DefaultGLCode] varchar(25) NULL,
    [AcctFileLocn] varchar(40) NULL,
    [NextInv_NO] decimal(18,0) NULL,
    [Locked] char(1) NULL,
    [LockDate] datetime NULL,
    [LockTimeH] smallint NULL,
    [LockTimeM] smallint NULL,
    [LockedBy] int NULL,
    [State] varchar(40) NULL,
    [Country] varchar(40) NULL,
    [PostCode] varchar(40) NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [TaxNumber] varchar(40) NULL,
    [NextPoNumber] decimal(18,0) NOT NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [RegionNumber] int NOT NULL,
    [IsMainLocn] bit NOT NULL,
    [LastExport] datetime NULL,
    [LocnGlCode] varchar(10) NULL,
    [DefaultPriceSet] tinyint NULL,
    [BatchNumber] int NULL,
    [SMTPAddress] varchar(50) NULL,
    [SMTPPort] varchar(20) NULL,
    [DefaultStandardTextID] decimal(10,0) NULL,
    [PhoneCountryCode] varchar(10) NULL,
    [PhoneAreaCode] varchar(10) NULL,
    [PhoneExt] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [DefaultDeliveryID] int NULL,
    [DefaultReturnID] int NULL,
    [MapAddress1] varchar(50) NULL,
    [MapAddress2] varchar(50) NULL,
    [MapCity] varchar(50) NULL,
    [MapState] varchar(50) NULL,
    [MapCountry] varchar(50) NULL,
    [MapPostCode] varchar(50) NULL,
    [DefaultCrewDelivery] varchar(8) NULL,
    [DefaultCrewPickup] varchar(8) NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [NextCreditNoteNumber] decimal(19,0) NULL,
    [SMTPEncryption] tinyint NULL,
    [TemplateDirectory] varchar(255) NULL,
    [SMTPReqAuth] bit NULL,
    [SMTPUserName] varchar(50) NULL,
    [POPrefix] varchar(3) NULL,
    [ContractNoPrefix] varchar(3) NULL,
    [StripePubKey] varchar(250) NULL,
    [StripeSecKey] varchar(250) NULL,
    [QBProductionID] varchar(384) NULL,
    [QBProductionSecret] varchar(384) NULL,
    [QBSandboxID] varchar(384) NULL,
    [QBSandboxSecret] varchar(384) NULL,
    [QBTaxName] varchar(128) NULL,
    [QBTaxNumber] int NULL,
    [QBSandboxProduction] varchar(128) NULL,
    [realmID] varchar(128) NULL,
    [access_token] varchar(2048) NULL,
    [refresh_token] varchar(384) NULL,
    [access_token_expire] datetime NULL,
    [refresh_token_expire] datetime NULL,
    CONSTRAINT [PK_tblLocnlist] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblLocnqty')
BEGIN
CREATE TABLE [tblLocnqty] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [product_code] char(30) NULL,
    [Locn] int NULL,
    [qty] float NULL,
    [GLCode] varchar(25) NULL,
    [QtyInRack] int NULL,
    [BinLocation] varchar(100) NULL,
    CONSTRAINT [PK_tblLocnqty] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMaint')
BEGIN
CREATE TABLE [tblMaint] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Product_code] char(30) NULL,
    [Serial_number] varchar(25) NULL,
    [Reference] varchar(10) NULL,
    [Repair_details] text NULL,
    [Labour] float NULL,
    [Material] float NULL,
    [Supplier_code] varchar(30) NULL,
    [DateF] datetime NULL,
    [Stock_Number] int NULL,
    [OutDate] datetime NULL,
    [ReturnDate] datetime NULL,
    [DamagedFaulty] char(1) NULL,
    [IncludeOnreport] char(1) NULL,
    [Booking_no] varchar(35) NULL,
    [OutTime] varchar(4) NOT NULL,
    [ReturnTime] varchar(4) NOT NULL,
    [AssetStatus] tinyint NULL,
    [bIsHistoryItem] bit NOT NULL,
    [EntryLocn] int NULL,
    [OperatorID] int NULL,
    [ReturnOperatorID] int NULL,
    [parent_id] decimal(10,0) NULL,
    [ReturnProcess] varchar(200) NULL,
    [LastModByOpID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblMaint] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMaintenanceNotes')
BEGIN
CREATE TABLE [tblMaintenanceNotes] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [MaintenanceID] decimal(10,0) NULL,
    [LineNumber] tinyint NULL,
    [TextLine] varchar(253) NULL,
    [NoteType] tinyint NULL,
    CONSTRAINT [PK_tblMaintenanceNotes] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMarginTargets')
BEGIN
CREATE TABLE [tblMarginTargets] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Margintype] tinyint NOT NULL,
    [seqNo] tinyint NOT NULL,
    [FromAmt] float NOT NULL,
    [ToAmt] float NOT NULL,
    [MarginTargetPerc] float NOT NULL,
    CONSTRAINT [PK_tblMarginTargets] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMasterBilling')
BEGIN
CREATE TABLE [tblMasterBilling] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [MBName] varchar(50) NULL,
    [CustomerID] int NULL,
    [CommSalesHotel] float NULL,
    [CommSalesAV] float NULL,
    [CommRentalHotel] float NULL,
    [CommRentalAV] float NULL,
    [CommCrewHotel] float NULL,
    [CommCrewAV] float NULL,
    [CommInsur] float NULL,
    [CommCCSurcharge] float NULL,
    [CommOnLossesHotel] float NULL,
    [CommonLossAV] float NULL,
    [CommOnSundry] float NULL,
    [CommOnEventManagementFee] float NULL,
    [CommOnCrossRentals] float NULL,
    [PriceSet] tinyint NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [MBActive] bit NULL,
    [Location] int NULL,
    [Notes] varchar(512) NULL,
    [Scenario] tinyint NULL,
    CONSTRAINT [PK_tblMasterBilling] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMasterBillingProductType')
BEGIN
CREATE TABLE [tblMasterBillingProductType] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [MBProdTypeName] varchar(50) NULL,
    [MBPTActive] bit NULL,
    [MBPTNotes] varchar(512) NULL,
    CONSTRAINT [PK_tblMasterBillingProductType] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMasterBillingProductTypeComm')
BEGIN
CREATE TABLE [tblMasterBillingProductTypeComm] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [MBNID] int NULL,
    [MBID] int NULL,
    [CommPercentageHotel] float NULL,
    [CommPercentageAV] float NULL,
    [CommissionServiceChargePerc] float NULL,
    CONSTRAINT [PK_tblMasterBillingProductTypeComm] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMessage')
BEGIN
CREATE TABLE [tblMessage] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [MsgDate] datetime NULL,
    [MsgTime] varchar(4) NULL,
    [ContactID] decimal(10,0) NULL,
    [ActivityTypeID] decimal(10,0) NULL,
    [Message] text NULL,
    [FromID] decimal(10,0) NULL,
    [ToID] decimal(10,0) NULL,
    [Resolved] char(1) NULL,
    [Urgent] char(1) NULL,
    [StatusID] decimal(10,0) NULL,
    [MsgRead] char(1) NULL,
    [Subject] varchar(50) NULL,
    CONSTRAINT [PK_tblMessage] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblMiscCosts')
BEGIN
CREATE TABLE [tblMiscCosts] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [CostType] tinyint NOT NULL,
    [Description] varchar(50) NOT NULL,
    [Category_code] varchar(30) NULL,
    [Cost] float NOT NULL,
    [SeqNo] int NOT NULL,
    [CatSeqNo] int NOT NULL,
    CONSTRAINT [PK_tblMiscCosts] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblNonAssetTrackedProductTagList')
BEGIN
CREATE TABLE [tblNonAssetTrackedProductTagList] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ProductID] decimal(10,0) NULL,
    [barcodeNumber] varchar(30) NULL,
    [RFIDTag] varchar(50) NULL,
    CONSTRAINT [PK_tblNonAssetTrackedProductTagList] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOperatorgroup')
BEGIN
CREATE TABLE [tblOperatorgroup] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Groupname] varchar(35) NOT NULL,
    [Accessprivilage] varchar(200) NULL,
    [Description] varchar(100) NULL,
    [AccessPrivilage1] varchar(200) NULL,
    [AccessPrivilage2] varchar(200) NULL,
    [DefaultPanel] int NULL,
    CONSTRAINT [PK_tblOperatorgroup] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbloperatorPreference')
BEGIN
CREATE TABLE [tbloperatorPreference] (
    [ID] decimal(18,0) IDENTITY(1,1) NOT NULL,
    [OperatorID] decimal(18,0) NOT NULL,
    [OperatorName] varchar(50) NULL,
    [prefName] varchar(50) NULL,
    [PrefValue] varchar(500) NULL,
    CONSTRAINT [PK_tbloperatorPreference] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOperators')
BEGIN
CREATE TABLE [tblOperators] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [FirstName] varchar(35) NULL,
    [LastName] varchar(35) NULL,
    [BelongsToGroup] varchar(35) NULL,
    [Loginname] varchar(50) NULL,
    [Password] varchar(128) NULL,
    [Email] varchar(80) NULL,
    [FullName] varchar(70) NULL,
    [StartWorkDay] varchar(4) NULL,
    [EndWorkDay] varchar(4) NULL,
    [WorkDays] char(7) NULL,
    [TimeInc] int NULL,
    [CustomFieldsID] decimal(10,0) NULL,
    [PrivateMode] char(1) NULL,
    [CheckMessages] int NULL,
    [RecallDays] int NULL,
    [AutoEmail] char(1) NULL,
    [DefaultDurationH] int NULL,
    [DefaultDurationM] int NULL,
    [LoginAllowed] char(1) NULL,
    [DefaultLocation] int NULL,
    [TreeViewAlwaysOpen] int NULL,
    [MaxPOAmount] float NULL,
    [AssignBookingToPO] bit NOT NULL,
    [DefaultDivision] int NOT NULL,
    [DefaultRegion] int NOT NULL,
    [ReceiveBookingStatusMessage] bit NOT NULL,
    [SMTPAddress] varchar(50) NULL,
    [SMTPPort] varchar(20) NULL,
    [DefaultSalesperson] varchar(30) NULL,
    [DefaultProjectManager] varchar(8) NULL,
    [DefaultSignatureID] decimal(10,0) NULL,
    [SysAdmin] bit NULL,
    [MaxRentalDiscount] float NULL,
    [MaxSalesDiscount] float NULL,
    [AutoEmailing] varchar(80) NULL,
    [MaxCRAmount] float NULL,
    [bIsEnglishUser] bit NULL,
    [useCloud] bit NULL,
    [cUserName] varchar(100) NULL,
    [cPassword] varchar(100) NULL,
    [cCloudType] tinyint NULL,
    [cViewerType] tinyint NULL,
    [SMTPEncryption] tinyint NULL,
    [SMTPReqAuth] bit NULL,
    [SMTPType] tinyint NOT NULL,
    [ColorSettings] text NOT NULL,
    [RFIDName] varchar(100) NULL,
    [OverrideEmailing] bit NULL,
    [EmailAll] bit NULL,
    [EmailSP] bit NULL,
    [EmailPM] bit NULL,
    [RecoveryCode] varchar(200) NULL,
    [PasswordWeb] varchar(200) NULL,
    [MobileInterface] bit NULL,
    [CultureInt] int NOT NULL,
    [Phone] varchar(20) NULL,
    [TOTP_KEY] varchar(50) NULL,
    [MFAEnabled] bit NULL,
    [MFASms] bit NULL,
    [MFAEmail] bit NULL,
    [AlwaysRequire2fa] bit NULL,
    [RecoveryCodes] varchar(90) NULL,
    [Mandatory2FA] bit NULL,
    [RecoveryUntil] datetime NULL,
    CONSTRAINT [PK_tblOperators] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOperatorsLocationGroups')
BEGIN
CREATE TABLE [tblOperatorsLocationGroups] (
    [ID] decimal(19,0) IDENTITY(1,1) NOT NULL,
    [LocnID] decimal(10,0) NULL,
    [OpGroupID] decimal(10,0) NULL,
    [OperatorID] decimal(10,0) NULL,
    CONSTRAINT [PK__tblOpera__3214EC279579196C] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOperatorTelemetry')
BEGIN
CREATE TABLE [tblOperatorTelemetry] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [OperatorID] decimal(10,0) NOT NULL,
    [LogonDate] datetime NULL,
    [LogoffDate] datetime NULL,
    [SessionID] nvarchar(50) NULL,
    [IPAddress] nvarchar(20) NULL,
    [Device] nvarchar(100) NULL,
    [LastTabOpened] nvarchar(50) NULL,
    [loggedAt] datetime NULL,
    CONSTRAINT [PK__tblOpera__3214EC279E8EA425] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOperatorTelemetryHist')
BEGIN
CREATE TABLE [tblOperatorTelemetryHist] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [OperatorID] decimal(10,0) NOT NULL,
    [LogonDate] datetime NULL,
    [LogoffDate] datetime NULL,
    [SessionID] nvarchar(50) NULL,
    [IPAddress] nvarchar(20) NULL,
    [Device] nvarchar(100) NULL,
    [LastTabOpened] nvarchar(50) NULL,
    [loggedAt] datetime NULL,
    CONSTRAINT [PK__tblOpera__3214EC2791D769B6] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOptionGroups')
BEGIN
CREATE TABLE [tblOptionGroups] (
    [optg_id] smallint NOT NULL,
    [optg_name] varchar(100) NULL,
    CONSTRAINT [tblOptionGroups_pk] PRIMARY KEY CLUSTERED ([optg_id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOptionItems')
BEGIN
CREATE TABLE [tblOptionItems] (
    [opt_id] int NOT NULL,
    [opt_code] varchar(100) NOT NULL,
    [opt_name] varchar(300) NULL,
    [opt_type] smallint NULL,
    [opt_encrypted] bit NULL,
    [opt_group] smallint NULL,
    CONSTRAINT [tblOptions_pk] PRIMARY KEY CLUSTERED ([opt_id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblOptionValues')
BEGIN
CREATE TABLE [tblOptionValues] (
    [opt_id] int NOT NULL,
    [op_id] decimal(10,0) NOT NULL,
    [opt_value] varchar(MAX) NULL,
    CONSTRAINT [tblOptionValues_pk] PRIMARY KEY CLUSTERED ([opt_id], [op_id])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblParameters')
BEGIN
CREATE TABLE [tblParameters] (
    [Name] varchar(100) NOT NULL,
    [Value] varchar(MAX) NULL,
    CONSTRAINT [PK__tblParam__737584F7C3F27194] PRIMARY KEY CLUSTERED ([Name])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPayment')
BEGIN
CREATE TABLE [tblPayment] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [customer_code] varchar(30) NULL,
    [booking_seq_no] varchar(5) NULL,
    [invoice_no] decimal(19,0) NULL,
    [amount] float NULL,
    [comment_line] varchar(30) NULL,
    [tax2] float NULL,
    [currencyStr] varchar(5) NULL,
    [tax1] float NULL,
    [ReceiptNo] int NULL,
    [taxauthority1] int NULL,
    [DateF] datetime NULL,
    [taxauthority2] int NULL,
    [LinkedPaymentID] decimal(10,0) NULL,
    [FromPrepayment] char(1) NULL,
    [Booking_no] varchar(35) NULL,
    [Archived] bit NOT NULL,
    [QBO_ID] decimal(10,0) NULL,
    [XeroId] varchar(36) NULL,
    CONSTRAINT [PK_tblPayment] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPaymentMethods')
BEGIN
CREATE TABLE [tblPaymentMethods] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [MethodName] varchar(25) NULL,
    [SurchargeRate] float NULL,
    CONSTRAINT [PK_tblPaymentMethods] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPayroll')
BEGIN
CREATE TABLE [tblPayroll] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_No] varchar(35) NULL,
    [LabourProductCode] varchar(30) NULL,
    [StartDate] datetime NULL,
    [StartTime] varchar(4) NULL,
    [EndDate] datetime NULL,
    [EndTime] varchar(4) NULL,
    [Days] float NULL,
    [Hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [StraightTime] float NULL,
    [Overtime] float NULL,
    [DoubleTime] float NULL,
    [TechnicianProductCode] varchar(30) NULL,
    [TechnicianRate] float NULL,
    [HourOrDay] char(1) NULL,
    [TechnicianPrice] float NULL,
    [HourlyRateID] decimal(18,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMinutes] int NOT NULL,
    [Details] varchar(50) NOT NULL,
    [Approved] bit NOT NULL,
    [ApprovedBy] decimal(10,0) NOT NULL,
    [ApprovedDate] datetime NOT NULL,
    [PayrollType] int NOT NULL,
    [CrewID] decimal(10,0) NOT NULL,
    [Finished] tinyint NULL,
    [StartLongitude] decimal(9,6) NULL,
    [FinishLongitude] decimal(9,6) NULL,
    [StartLatitude] decimal(9,6) NULL,
    [FinishLatitude] decimal(9,6) NULL,
    [StartAdress] varchar(MAX) NULL,
    [FinishAdress] varchar(MAX) NULL,
    [AssignTo] varchar(35) NULL,
    [SubrentalLinkID] int NULL,
    [TaskNo] int NULL,
    CONSTRAINT [PK_tblPayroll] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPayTermNames')
BEGIN
CREATE TABLE [tblPayTermNames] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [TermNo] smallint NULL,
    [CashOnly] char(1) NULL,
    [NetDays] int NULL,
    [PayTermName] varchar(40) NULL,
    CONSTRAINT [PK_tblPayTermNames] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPayTerms')
BEGIN
CREATE TABLE [tblPayTerms] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_no] varchar(35) NULL,
    [Customer_Code] varchar(30) NULL,
    [NoOfStages] int NULL,
    CONSTRAINT [PK_tblPayTerms] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPhones')
BEGIN
CREATE TABLE [tblPhones] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [PhoneType] tinyint NOT NULL,
    [CallType] tinyint NOT NULL,
    [DialAreaCode] bit NOT NULL,
    [CountryCode] varchar(10) NOT NULL,
    [AreaCode] varchar(10) NOT NULL,
    [Digits] varchar(16) NOT NULL,
    [Extension] varchar(8) NOT NULL,
    [PhoneCode] varchar(35) NULL,
    [PType] tinyint NULL,
    CONSTRAINT [PK_tblPhones] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPo')
BEGIN
CREATE TABLE [tblPo] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [PVendorCode] varchar(30) NULL,
    [PPONumber] decimal(19,0) NULL,
    [PpostedToOnOrd] char(1) NULL,
    [PReceived] char(1) NULL,
    [Pdelto] varchar(50) NULL,
    [PdeliveryAdr1] varchar(50) NULL,
    [PdeliveryAdr2] varchar(50) NULL,
    [PdeliveryAdr3] varchar(50) NULL,
    [PPostcode] varchar(12) NULL,
    [PdeliverVia] int NULL,
    [PtotalAmount] float NULL,
    [PtaxAmount] float NULL,
    [PtaxTitle] varchar(30) NULL,
    [ProjectCode] varchar(30) NULL,
    [PrintNotesOnPO] char(1) NULL,
    [Otherdesc] varchar(20) NULL,
    [freight] float NULL,
    [TheirOurNumber] tinyint NULL,
    [Archaived] char(1) NULL,
    [POBooking_no] varchar(35) NULL,
    [PaymentTermsDays] int NULL,
    [DiscountPerc] float NULL,
    [OrderDate] datetime NULL,
    [ExpectedDate] datetime NULL,
    [CrossRental] char(1) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [Phone] varchar(16) NULL,
    [Fax] varchar(16) NULL,
    [Contact] varchar(50) NULL,
    [OrderedBy] varchar(50) NULL,
    [RequestedBy] varchar(50) NULL,
    [Location] int NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [Tax1Value] float NULL,
    [Tax2Value] float NULL,
    [Approved] bit NULL,
    [AppByOpID] decimal(18,0) NULL,
    [ApprovalDate] datetime NULL,
    [ApprovedAmount] float NOT NULL,
    [InvoiceStatus] tinyint NOT NULL,
    [CrewAdded] bit NOT NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [AirBill] varchar(50) NOT NULL,
    [Description] varchar(50) NULL,
    [UndiscountedAmount] float NULL,
    [DiscountedAmount] float NULL,
    [PhoneCountryCode] varchar(10) NULL,
    [PhoneAreaCode] varchar(10) NULL,
    [PhoneExt] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [bReviewStatus] bit NULL,
    [ActualPOCurrency] varchar(5) NULL,
    [bIncludeOnSchedule] bit NULL,
    [zPOPickupTime] varchar(4) NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [MonthYearFilter] datetime NULL,
    [POPrefix] varchar(3) NULL,
    CONSTRAINT [PK_tblPo] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPo_no')
BEGIN
CREATE TABLE [tblPo_no] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [PO_number] decimal(19,0) NULL,
    [TurnOnCheckoutSp] bit NOT NULL,
    CONSTRAINT [PK_tblPo_no] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPoline')
BEGIN
CREATE TABLE [tblPoline] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [LPOnumber] varchar(13) NULL,
    [LProductCode] varchar(30) NULL,
    [LlineType] tinyint NULL,
    [LFFtext] varchar(60) NULL,
    [Lquantity] float NULL,
    [LunitPrice] float NULL,
    [Lprice] float NULL,
    [UnitMessurement] varchar(8) NULL,
    [LquantityReceived] int NULL,
    [LineDiscount] float NULL,
    [PONumber] int NOT NULL,
    [LineNumber] int NOT NULL,
    [PartNumber] varchar(30) NULL,
    [RevenueCode] varchar(50) NULL,
    [bit_field_v41] tinyint NULL,
    [item_type] tinyint NULL,
    [PackageLevel] int NULL,
    [sub_seq_no] int NULL,
    CONSTRAINT [PK_tblPoline] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPonote')
BEGIN
CREATE TABLE [tblPonote] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [code] char(30) NULL,
    [line_no] tinyint NULL,
    [text_line] varchar(253) NULL,
    CONSTRAINT [PK_tblPonote] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblpriceFactors')
BEGIN
CREATE TABLE [tblpriceFactors] (
    [PFTName] varchar(100) NULL,
    [TableFactorsOn] int NULL,
    [DaysToCharge] varchar(MAX) NULL,
    [TableNo] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblPriceovr')
BEGIN
CREATE TABLE [tblPriceovr] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no] varchar(35) NULL,
    [product_code] varchar(30) NULL,
    [quantity] smallint NULL,
    [old_price] float NULL,
    [new_price] float NULL,
    [timeH] tinyint NULL,
    [timeM] tinyint NULL,
    [reason] varchar(50) NULL,
    [DateF] datetime NULL,
    [operators] varchar(50) NULL,
    [ItemTranID] int NULL,
    [PrOFieldIndicator] tinyint NULL,
    CONSTRAINT [PK_tblPriceovr] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblProdList')
BEGIN
CREATE TABLE [tblProdList] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Code] varchar(30) NULL,
    [CodeType] smallint NULL,
    [CallListID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblProdList] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblProdmx05')
BEGIN
CREATE TABLE [tblProdmx05] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [OpID] int NULL,
    [LicTime] smalldatetime NULL,
    [IntKey] int NULL,
    [SIKey] int NULL,
    [LOCATION] varchar(255) NULL,
    CONSTRAINT [PK_tblProdmx05] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblProdnote')
BEGIN
CREATE TABLE [tblProdnote] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [product_code] char(30) NULL,
    [line_no] tinyint NULL,
    [text_line] varchar(253) NULL,
    [Notetype] tinyint NULL,
    [stock_number] int NOT NULL,
    CONSTRAINT [PK_tblProdnote] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblProdstat')
BEGIN
CREATE TABLE [tblProdstat] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [product_code] char(30) NULL,
    [quantity] smallint NULL,
    [DateF] datetime NULL,
    [avail] smallint NULL,
    [Location] int NULL,
    CONSTRAINT [PK_tblProdstat] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblProductionExchange')
BEGIN
CREATE TABLE [tblProductionExchange] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [InvMasID] int NOT NULL,
    [eCode] varchar(50) NOT NULL,
    [eCatCode] varchar(50) NULL,
    [CapMaxQty] int NULL,
    CONSTRAINT [PK_tblProductionExchange] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblProfile')
BEGIN
CREATE TABLE [tblProfile] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Name] varchar(30) NULL,
    [Description] varchar(50) NULL,
    [ContactID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblProfile] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblQBExport')
BEGIN
CREATE TABLE [tblQBExport] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [QBType] tinyint NOT NULL,
    [QBObject] varchar(MAX) NOT NULL,
    [QBDate] datetime NOT NULL,
    [location] int NULL,
    [err_export] bit NULL,
    [QBVersion] int NULL,
    [InvheadID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblQBExport] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblQBExportHist')
BEGIN
CREATE TABLE [tblQBExportHist] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [QBType] tinyint NOT NULL,
    [QBObject] varchar(MAX) NOT NULL,
    [QBDate] datetime NOT NULL,
    [location] int NULL,
    [ExportDate] datetime NOT NULL,
    [QBVersion] int NULL,
    [InvheadID] decimal(10,0) NULL,
    [QBO_ID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblQBExportHist] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblQBWebHooks')
BEGIN
CREATE TABLE [tblQBWebHooks] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [realmID] varchar(128) NULL,
    [operationID] varchar(128) NULL,
    [operation] varchar(128) NULL,
    [operationName] varchar(128) NULL,
    [lastUpdatedRaw] varchar(64) NULL,
    [lastUpdated] datetime NULL,
    [Imported] bit NULL,
    [ImportDate] datetime NULL,
    [ImportError] bit NULL,
    [ErrorDescription] varchar(512) NULL,
    CONSTRAINT [PK_tblQBWebHooks] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblRatetbl')
BEGIN
CREATE TABLE [tblRatetbl] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ProductCode] char(30) NULL,
    [tableNo] tinyint NULL,
    [hourly_rate] float NULL,
    [half_day] float NULL,
    [rate_1st_day] float NULL,
    [rate_extra_days] float NULL,
    [rate_week] float NULL,
    [rate_long_term] float NULL,
    [deposit] float NULL,
    [damage_waiver_rate] float NULL,
    [DayWeekRate] float NULL,
    [MinimumRental] float NULL,
    [ReplacementValue] float NULL,
    [Rate3rdDay] float NULL,
    [Rate4thDay] float NULL,
    [Rate2ndWeek] float NULL,
    [Rate3rdWeek] float NULL,
    [RateAdditionalMonth] float NULL,
    [RatePrep] float NULL,
    [RateWrap] float NULL,
    [RatePreLight] float NULL,
    CONSTRAINT [PK_tblRatetbl] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblReceipt')
BEGIN
CREATE TABLE [tblReceipt] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [receiptNo] int NULL,
    [booking_no] varchar(35) NULL,
    [amount] float NULL,
    [Invoice_no] decimal(19,0) NULL,
    [division] tinyint NULL,
    [tax1] float NULL,
    [tax2] float NULL,
    [taxauthority1] int NULL,
    [taxauthority2] int NULL,
    [drawer] varchar(30) NULL,
    [bank] varchar(10) NULL,
    [branch] varchar(15) NULL,
    [cheque_no] varchar(10) NULL,
    [Card_name] varchar(128) NULL,
    [Card_no] varchar(128) NULL,
    [DateF] datetime NULL,
    [cash_type] tinyint NULL,
    [CustomerCode] varchar(30) NULL,
    [includes_vat_gstfiller] char(1) NULL,
    [BatchNo] int NULL,
    CONSTRAINT [PK_tblReceipt] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblRegions')
BEGIN
CREATE TABLE [tblRegions] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [RegionNumber] int NOT NULL,
    [RegionName] varchar(50) NOT NULL,
    [RegionGLCode] varchar(10) NULL,
    [BatchNumber] int NULL,
    CONSTRAINT [PK_tblRegions] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblRentalPointServerLog')
BEGIN
CREATE TABLE [tblRentalPointServerLog] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [DateTime] datetime NOT NULL,
    [Service] varchar(50) NOT NULL,
    [Message] varchar(200) NOT NULL,
    [ErrorFlag] bit NOT NULL,
    [AccountId] int NULL,
    [EventType] int NOT NULL,
    [EventSubType] int NOT NULL,
    [BookingNo] varchar(35) NULL,
    [OperatorId] varchar(50) NULL,
    CONSTRAINT [PK_tblRentalPointServerLog] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblReservedAssets')
BEGIN
CREATE TABLE [tblReservedAssets] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Booking_no] varchar(35) NULL,
    [Product_code] varchar(30) NULL,
    [Stock_number] int NULL,
    CONSTRAINT [PK_tblReservedAssets] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblResults')
BEGIN
CREATE TABLE [tblResults] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ResponseID] decimal(10,0) NULL,
    [QuestionID] decimal(10,0) NULL,
    [ScriptID] decimal(10,0) NULL,
    [ContactID] decimal(10,0) NULL,
    [ResultDate] datetime NULL,
    CONSTRAINT [PK_tblResults] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblReturnsSignatures')
BEGIN
CREATE TABLE [tblReturnsSignatures] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Date] datetime NOT NULL,
    [booking_no] varchar(35) NULL,
    [SignerName] nvarchar(200) NOT NULL,
    [PDF] varbinary(MAX) NOT NULL,
    [SignatureImage] varbinary(MAX) NOT NULL,
    CONSTRAINT [PK_tblReturnsSignatures] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblRFIDEvents')
BEGIN
CREATE TABLE [tblRFIDEvents] (
    [ID] decimal(18,0) IDENTITY(1,1) NOT NULL,
    [RFIDTag] varchar(50) NULL,
    [RFIDTimeStamp] datetime NULL,
    [EventCode] tinyint NULL,
    [ReadPointList] varchar(50) NULL,
    [UserID] varchar(50) NULL,
    [EventRule] varchar(50) NULL,
    [DownloadedBy] decimal(10,0) NULL,
    [Booking_no] varchar(35) NULL,
    [DateLoaded] datetime NULL,
    [Direction] int NULL,
    [SessionNo] int NULL,
    [SessionOp] decimal(10,0) NULL,
    [ReaderName] varchar(100) NULL,
    CONSTRAINT [PK_tblRFIDEvents] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblRoadcase')
BEGIN
CREATE TABLE [tblRoadcase] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [parent_basecode] char(30) NULL,
    [asset_barcode] char(30) NULL,
    [CaseType] tinyint NULL,
    [Qty] int NULL,
    [NonBarProductCode] varchar(30) NULL,
    [Booking_no] varchar(35) NULL,
    [DateTimePacked] datetime NULL,
    [PackedBy] decimal(10,0) NULL,
    [NonBarLocn] int NULL,
    [parent_basecode_ID] int NULL,
    [asset_barcode_ID] int NULL,
    [Floating] bit NULL,
    CONSTRAINT [PK_tblRoadcase] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblRoadcasePackList')
BEGIN
CREATE TABLE [tblRoadcasePackList] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Parent_BaseCode_id] int NOT NULL,
    [Asset_Barcode_ID] int NULL,
    [AssetDescription] varchar(50) NULL,
    [CaseType] tinyint NULL,
    [Qty] int NULL,
    [NonBarProductCode] varchar(30) NULL,
    [Booking_no] varchar(35) NULL,
    [dateTimePacked] datetime NULL,
    [PackedBy] decimal(10,0) NULL,
    [NonBarLocn] int NULL,
    [Floating] bit NULL,
    [ListType] tinyint NULL,
    CONSTRAINT [PK_tblRoadcasePackList] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSage_DGTtemp')
BEGIN
CREATE TABLE [tblSage_DGTtemp] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Invoice_no] varchar(10) NULL,
    [sage_acnt_no] varchar(10) NULL,
    [amount] float NULL,
    [Category_code] varchar(30) NULL,
    [ProductType] tinyint NULL,
    [Group_code] varchar(30) NULL,
    [booking_no] varchar(35) NULL,
    CONSTRAINT [PK_tblSage_DGTtemp] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSaleCosts')
BEGIN
CREATE TABLE [tblSaleCosts] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Product_code] varchar(30) NULL,
    [QtyReceived] int NULL,
    [UnitCost] float NULL,
    [DateReceived] datetime NOT NULL,
    [Locn] int NULL,
    [QtyUnsold] int NULL,
    [PoNumber] decimal(18,0) NULL,
    CONSTRAINT [PK_tblSaleCosts] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSalesper')
BEGIN
CREATE TABLE [tblSalesper] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [salesperson_code] varchar(30) NULL,
    [Salesperson_name] varchar(45) NULL,
    [assigned_for] smallint NULL,
    [MonthsAssignedToContact] int NULL,
    [ContactID] decimal(10,0) NULL,
    [bDisabled] bit NULL,
    [CommSales] float NULL,
    [CommRental] float NULL,
    [CommCrew] float NULL,
    [CommInsur] float NULL,
    [CommCCSurcharge] float NULL,
    [CommOnLosses] float NULL,
    [CommOnSundry] float NULL,
    [CommOnEventManagementFee] float NULL,
    [CommOnCrossRentals] float NULL,
    [CommOnCrossCrew] float NULL,
    CONSTRAINT [PK_tblSalesper] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblScript')
BEGIN
CREATE TABLE [tblScript] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Name] varchar(20) NULL,
    CONSTRAINT [PK_tblScript] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSettings')
BEGIN
CREATE TABLE [tblSettings] (
    [Name] varchar(100) NOT NULL,
    [Value] varchar(MAX) NULL,
    CONSTRAINT [PK__tblSetti__737584F757025DFE] PRIMARY KEY CLUSTERED ([Name])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblShortages')
BEGIN
CREATE TABLE [tblShortages] (
    [ItemTranID] decimal(10,0) NOT NULL,
    [ShortQty] int NULL,
    [LastUpdate] datetime NULL,
    CONSTRAINT [PK_tblShortages] PRIMARY KEY CLUSTERED ([ItemTranID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblShow')
BEGIN
CREATE TABLE [tblShow] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ShowName] varchar(50) NULL,
    [StartDate] datetime NULL,
    [EndDate] datetime NULL,
    [Venue] varchar(25) NULL,
    [DecisionDate] datetime NULL,
    [Equipment] text NULL,
    [Coordinator] varchar(30) NULL,
    [CustID] decimal(10,0) NULL,
    [Crew] text NULL,
    [Competitors] varchar(70) NULL,
    [Budget] varchar(40) NULL,
    CONSTRAINT [PK_tblShow] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSQuestion')
BEGIN
CREATE TABLE [tblSQuestion] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ScriptID] decimal(10,0) NULL,
    [Number] int NULL,
    [Title] varchar(20) NULL,
    [Question] text NULL,
    [Instructions] text NULL,
    CONSTRAINT [PK_tblSQuestion] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSResponses')
BEGIN
CREATE TABLE [tblSResponses] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [QuestionID] decimal(10,0) NULL,
    [Number] int NULL,
    [ScriptID] decimal(10,0) NULL,
    [GotoQuestion] int NULL,
    [Response] varchar(40) NULL,
    [Points] int NULL,
    CONSTRAINT [PK_tblSResponses] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblStatus')
BEGIN
CREATE TABLE [tblStatus] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Status] varchar(20) NULL,
    CONSTRAINT [PK_tblStatus] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblStocktak')
BEGIN
CREATE TABLE [tblStocktak] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [bars] varchar(15) NULL,
    [dayno] int NULL,
    [Bhour] tinyint NULL,
    [Bminute] tinyint NULL,
    [qty] smallint NULL,
    [Locn] int NULL,
    CONSTRAINT [PK_tblStocktak] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblStockTakHistory')
BEGIN
CREATE TABLE [tblStockTakHistory] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [Product_code] varchar(30) NULL,
    [Stock_Number] int NULL,
    [EntryDateTime] datetime NULL,
    [Qty] int NULL,
    [Locn] int NULL,
    [OperatorID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblStockTakHistory] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblStripeSessions')
BEGIN
CREATE TABLE [tblStripeSessions] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [invoice_id] decimal(10,0) NULL,
    [session_id] varchar(128) NULL,
    CONSTRAINT [PK_tblStripeSessions] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSundry')
BEGIN
CREATE TABLE [tblSundry] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] int NULL,
    [sub_seq_no] int NULL,
    [sundry_desc] varchar(50) NULL,
    [sundry_cost] float NULL,
    [sundry_price] float NULL,
    [GroupSeqNo] int NULL,
    [Discount] float NULL,
    [trans_qty] float NULL,
    [restock_charge] tinyint NULL,
    [RevenueCode] varchar(50) NULL,
    [sundry_markup_percentage] float NULL,
    [view_client] bit NULL,
    [view_logi] bit NULL,
    [Logi_HeadingNo] tinyint NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [SundryType] tinyint NULL,
    CONSTRAINT [PK_tblSundry] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTask')
BEGIN
CREATE TABLE [tblTask] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [task_number] int NULL,
    [DefDateTime] datetime NULL,
    [defaultDateandTime] tinyint NULL,
    [task_name] varchar(30) NULL,
    [TaskType] tinyint NULL,
    CONSTRAINT [PK_tblTask] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTax')
BEGIN
CREATE TABLE [tblTax] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [tax_auth_no] int NULL,
    [Tax_authority] varchar(50) NULL,
    [tax_name] varchar(24) NULL,
    [ceiling] float NULL,
    [taxrental] float NULL,
    [taxsale] float NULL,
    [taxlabour] float NULL,
    [taxdelivery] float NULL,
    [taxsundry] float NULL,
    [taxinsurance] float NULL,
    [default1] char(1) NULL,
    [default2] char(1) NULL,
    [GLHolding] varchar(30) NULL,
    [GLOutput] varchar(30) NULL,
    [Piggybacktax] char(1) NULL,
    [State] varchar(50) NULL,
    [Disabled] bit NULL,
    [taxCreditSurcharge] float NULL,
    [taxEventManagement] float NULL,
    CONSTRAINT [PK_tblTax] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTaxExemptGroups')
BEGIN
CREATE TABLE [tblTaxExemptGroups] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [GroupID] decimal(10,0) NOT NULL,
    [TaxID] decimal(10,0) NULL,
    CONSTRAINT [PK_tblTaxExemptGroups] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTermStages')
BEGIN
CREATE TABLE [tblTermStages] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [InvStageName] varchar(20) NULL,
    [Percentage] float NULL,
    [StageNo] int NULL,
    [PayTermID] decimal(10,0) NULL,
    [dueDate] date NULL,
    CONSTRAINT [PK_tblTermStages] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTracking')
BEGIN
CREATE TABLE [tblTracking] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [OperatorID] decimal(10,0) NULL,
    [InDate] datetime NULL,
    [OutDate] datetime NULL,
    [InTime] varchar(4) NULL,
    [OutTime] varchar(4) NULL,
    [Whereabouts] varchar(50) NULL,
    [LeaveDeskDate] datetime NULL,
    [LeaveDeskTime] varchar(4) NULL,
    [Duration] int NULL,
    CONSTRAINT [PK_tblTracking] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTransno')
BEGIN
CREATE TABLE [tblTransno] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [TransferNo] decimal(19,0) NULL,
    [NextProjectNo] int NULL,
    CONSTRAINT [PK_tblTransno] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTrip')
BEGIN
CREATE TABLE [tblTrip] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [truckNo] int NULL,
    [tripNo] tinyint NULL,
    [Driver] varchar(8) NULL,
    [DateF] datetime NULL,
    [outIn] tinyint NULL,
    [ArrivalDateTime] datetime NULL,
    [ActivityID] decimal(19,0) NULL,
    CONSTRAINT [PK_tblTrip] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTruckLoadList')
BEGIN
CREATE TABLE [tblTruckLoadList] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [BookingID] decimal(10,0) NOT NULL,
    [TripID] decimal(10,0) NOT NULL,
    [BookingLoadSequence] int NULL,
    [Direction] tinyint NULL,
    [BookingDelOrRetTime] varchar(4) NULL,
    [HeadingNumber] int NULL,
    CONSTRAINT [PK_tblTruckLoadList] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblTrucks')
BEGIN
CREATE TABLE [tblTrucks] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [truck_number] int NULL,
    [Truck_name] varchar(30) NULL,
    [CapacityWeight] float NULL,
    [CapacityCubic] float NULL,
    [RegionNumber] int NOT NULL,
    [LicensePlate] varchar(30) NULL,
    [LicensePlateExpiry] datetime NULL,
    [FuelCostPerUnit] float NULL,
    [FuelType] varchar(50) NULL,
    [Active] bit NOT NULL,
    CONSTRAINT [PK_tblTrucks] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblUpdateCount')
BEGIN
CREATE TABLE [tblUpdateCount] (
    [tblName] varchar(20) NOT NULL,
    [updCount] int NOT NULL,
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    CONSTRAINT [PK_tblUpdateCount] PRIMARY KEY CLUSTERED ([tblName])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVendnote')
BEGIN
CREATE TABLE [tblVendnote] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [code] char(30) NULL,
    [line_no] tinyint NULL,
    [text_line] varchar(253) NULL,
    CONSTRAINT [PK_tblVendnote] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVendor')
BEGIN
CREATE TABLE [tblVendor] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VendorCode] varchar(30) NULL,
    [VendorContact] varchar(35) NULL,
    [VendorName] varchar(50) NULL,
    [Vadr1] varchar(50) NULL,
    [Vadr2] varchar(50) NULL,
    [Vadr3] varchar(50) NULL,
    [Vpostcode] varchar(12) NULL,
    [Vphone1] varchar(16) NULL,
    [Vphone2] varchar(16) NULL,
    [Vfax] varchar(16) NULL,
    [Vemail] varchar(80) NULL,
    [Vwebpage] varchar(80) NULL,
    [Vcurrency] varchar(5) NULL,
    [Vaccno] varchar(30) NULL,
    [AreaCode] varchar(10) NULL,
    [CountryCode] varchar(10) NULL,
    [CallType] tinyint NULL,
    [UseAreaCode] char(1) NULL,
    [FaxExt] varchar(8) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2Ext] varchar(8) NULL,
    [Country] varchar(50) NULL,
    [State] varchar(50) NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [MinPOAmount] float NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [DefaultDiscount] float NOT NULL,
    [PaymentTerms] int NOT NULL,
    [DateCreated] datetime NULL,
    [LastBookingSeq] varchar(5) NULL,
    [Disabled] bit NULL,
    [VendTypeForExporting] int NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [VendorReferenceNumber] varchar(8) NULL,
    CONSTRAINT [PK_tblVendor] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVendorRates')
BEGIN
CREATE TABLE [tblVendorRates] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [ProductCode] varchar(30) NULL,
    [VendorCode] varchar(30) NULL,
    [VendorRefNo] varchar(35) NULL,
    [HourlyRate] float NULL,
    [HalfDayRate] float NULL,
    [FirstDayRate] float NULL,
    [ExtraDayRate] float NULL,
    [WeekRate] float NULL,
    [LongTermRate] float NULL,
    [bIsHistoryEntry] bit NULL,
    [DateModified] datetime NULL,
    [ModifiedByOperator] int NULL,
    CONSTRAINT [PK_tblVendorRates] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVenueAddress')
BEGIN
CREATE TABLE [tblVenueAddress] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VenueID] int NOT NULL,
    [Address1] varchar(50) NOT NULL,
    [Address2] varchar(50) NOT NULL,
    [City] varchar(50) NOT NULL,
    [State] varchar(50) NOT NULL,
    [Country] varchar(50) NOT NULL,
    [ZipCode] varchar(12) NOT NULL,
    CONSTRAINT [PK_tblVenueAddress] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVenuePhone')
BEGIN
CREATE TABLE [tblVenuePhone] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VenueID] int NOT NULL,
    [CountryCode] varchar(10) NOT NULL,
    [AreaCode] varchar(10) NOT NULL,
    [Digits] varchar(16) NOT NULL,
    [Extension] varchar(8) NOT NULL,
    [PhoneType] tinyint NOT NULL,
    CONSTRAINT [PK_tblVenuePhone] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVenues')
BEGIN
CREATE TABLE [tblVenues] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VenueName] varchar(50) NOT NULL,
    [ContactName] varchar(50) NOT NULL,
    [ContactID] decimal(10,0) NULL,
    [WebPage] varchar(80) NULL,
    [Address1] varchar(50) NULL,
    [Address2] varchar(50) NULL,
    [City] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [ZipCode] varchar(12) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [Phone2Ext] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [Type] tinyint NOT NULL,
    [BookingNo] varchar(35) NULL,
    [VenueNickname] varchar(50) NULL,
    [VenueTextType] varchar(50) NULL,
    [DefaultFolder] varchar(255) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    CONSTRAINT [PK_tblVenues] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVenunote')
BEGIN
CREATE TABLE [tblVenunote] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VenueName] char(50) NULL,
    [line_no] tinyint NULL,
    [text_line] varchar(253) NULL,
    [VenueID] decimal(18,0) NOT NULL,
    [NoteType] tinyint NOT NULL,
    CONSTRAINT [PK_tblVenunote] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblVenuroom')
BEGIN
CREATE TABLE [tblVenuroom] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [VenueName] char(50) NULL,
    [Roomname] char(35) NULL,
    [floorplanfilename] varchar(120) NULL,
    [VenueID] bigint NOT NULL,
    [RoomNumber] smallint NOT NULL,
    [MaxCapacity] int NULL,
    [CeilingHeight] varchar(20) NULL,
    [FloorNumber] varchar(20) NULL,
    CONSTRAINT [PK_tblVenuroom] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblWebPaylog')
BEGIN
CREATE TABLE [tblWebPaylog] (
    [ID] int IDENTITY(1,1) NOT NULL,
    [created] datetime NULL,
    [description] varchar(20) NULL,
    [amount] money NULL,
    [currency] varchar(3) NULL,
    [paid] bit NULL,
    [payment] varchar(50) NULL,
    [trans] varchar(50) NULL,
    [InvID] int NULL,
    [cardId] varchar(50) NULL,
    [last4] varchar(4) NULL,
    [brand] varchar(32) NULL,
    [exp_month] int NULL,
    [exp_year] int NULL,
    [country] varchar(4) NULL,
    [name] varchar(50) NULL,
    [status] varchar(16) NULL,
    [invoiceid] int NULL,
    CONSTRAINT [PK_tblWebPaylog] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblWorkflow')
BEGIN
CREATE TABLE [tblWorkflow] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [WorkflowName] varchar(100) NOT NULL,
    [Enabled] bit NOT NULL,
    [WorkflowShort] varchar(10) NOT NULL,
    [WorkflowType] tinyint NULL,
    CONSTRAINT [PK_tblWorkflow] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblWorkflowBookingLink')
BEGIN
CREATE TABLE [tblWorkflowBookingLink] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [WorkflowID] decimal(10,0) NOT NULL,
    [WorkflowStepID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    CONSTRAINT [PK_tblWorkflowBookingLink] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblWorkflowOperatorLink')
BEGIN
CREATE TABLE [tblWorkflowOperatorLink] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [WorkflowID] decimal(10,0) NOT NULL,
    [OperatorID] decimal(10,0) NOT NULL,
    [PrimaryWorkflow] bit NOT NULL,
    CONSTRAINT [PK_tblWorkflowOperatorLink] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblWorkflowStep')
BEGIN
CREATE TABLE [tblWorkflowStep] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [WorkflowID] decimal(10,0) NOT NULL,
    [StepName] varchar(100) NOT NULL,
    [StepColour] int NULL,
    [Enabled] bit NOT NULL,
    [Sequence] int NULL,
    [StepShort] varchar(10) NOT NULL,
    CONSTRAINT [PK_tblWorkflowStep] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblWpformat')
BEGIN
CREATE TABLE [tblWpformat] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [file_name] varchar(12) NULL,
    [desc] varchar(58) NULL,
    [Locn] int NULL,
    [wp_type] tinyint NULL,
    [CustomerCode] varchar(30) NULL,
    CONSTRAINT [PK_tblWpformat] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblXeroBKExp')
BEGIN
CREATE TABLE [tblXeroBKExp] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [XType] tinyint NOT NULL,
    [XObject] varchar(MAX) NOT NULL,
    [XDate] datetime NOT NULL,
    [InvheadID] decimal(10,0) NULL,
    [location] int NULL,
    [err_export] bit NULL,
    CONSTRAINT [PK_tblXeroBKExp] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblXeroExportHist')
BEGIN
CREATE TABLE [tblXeroExportHist] (
    [ID] decimal(10,0) IDENTITY(1,1) NOT NULL,
    [XType] tinyint NOT NULL,
    [XObject] varchar(MAX) NOT NULL,
    [ExportDate] datetime NOT NULL,
    [InvheadID] decimal(10,0) NULL,
    [XeroId] varchar(50) NULL,
    [OperatorId] decimal(10,0) NOT NULL,
    CONSTRAINT [PK_tblXeroExportHist] PRIMARY KEY CLUSTERED ([ID])
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwActivityInformation')
BEGIN
CREATE TABLE [vwActivityInformation] (
    [Act_ID] decimal(10,0) NOT NULL,
    [Inv_ID] decimal(10,0) NOT NULL,
    [Description] varchar(50) NULL,
    [StartDate] datetime NULL,
    [EndDate] datetime NULL,
    [StartTime] varchar(4) NULL,
    [EndTime] varchar(4) NULL,
    [Notes] text NULL,
    [Completed] char(1) NULL,
    [DateCompleted] datetime NULL,
    [TimeCompleted] varchar(4) NULL,
    [del_time_h] varchar(2) NULL,
    [del_time_m] varchar(2) NULL,
    [ret_time_h] varchar(2) NULL,
    [ret_time_m] varchar(2) NULL,
    [person] char(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwAllVenues')
BEGIN
CREATE TABLE [vwAllVenues] (
    [ID] decimal(10,0) NULL,
    [Auth_agentv6] varchar(50) NULL,
    [agent_code] varchar(13) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwArchivedInventory')
BEGIN
CREATE TABLE [vwArchivedInventory] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [trans_type_v41] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [bit_field_v41] tinyint NULL,
    [TimeBookedH] tinyint NULL,
    [TimeBookedM] tinyint NULL,
    [TimeBookedS] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [techRateorDaysCharged] float NULL,
    [unitRate] float NULL,
    [prep_on] char(1) NULL,
    [Comment_desc_v42] char(70) NULL,
    [AssignTo] varchar(255) NULL,
    [QtyReserved] int NULL,
    [AddedAtCheckout] bit NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [BookDate] datetime NULL,
    [PDate] datetime NULL,
    [DayWeekRate] float NULL,
    [TechPay] float NULL,
    [GroupSeqNo] int NULL,
    [SubRentalLinkID] int NOT NULL,
    [AssignType] tinyint NOT NULL,
    [QtyShort] int NOT NULL,
    [QtyAvailable] int NULL,
    [BeforeDiscountAmount] float NULL,
    [PackageLevel] smallint NULL,
    [QuickTurnaroundQty] int NULL,
    [InRack] bit NULL,
    [CostPrice] float NULL,
    [NodeCollapsed] bit NULL,
    [resolvedDiscrep] bit NOT NULL,
    [InvID] decimal(10,0) NOT NULL,
    [iseq_no] decimal(19,0) NULL,
    [descriptionV6] varchar(50) NULL,
    [product_type_v41] tinyint NULL,
    [retail_price] float NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [cost_price] float NULL,
    [unit_weight] float NULL,
    [unit_volume] float NULL,
    [prodRoadCase] tinyint NULL,
    [DisallowDisc] char(1) NULL,
    [product_Config] tinyint NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [isGenericItem] char(1) NULL,
    [UseWeeklyRate] char(1) NULL,
    [Indiv_hire_sale] char(1) NULL,
    [on_hand] float NULL,
    [DisallowTransfer] bit NULL,
    [cyTurnCosts] money NOT NULL,
    [bDisallowRegionTransfer] bit NULL,
    [IsInTrashCan] char(1) NULL,
    [LastUpdate] datetime NULL,
    [OLInternalDesc] varchar(50) NULL,
    [asset_track] char(1) NULL,
    [components_quote] char(1) NULL,
    [WarehouseActive] bit NULL,
    [bCustomPrintouts] bit NOT NULL,
    [CommissionServiceChargePerc] float NULL,
    [OverridePriceChangeRestriction] smallint NOT NULL,
    [BasedOnPurchCost] float NOT NULL,
    [MfctPartNumber] varchar(30) NULL,
    [bProductIsFreight] bit NOT NULL,
    [DisplayColour] varchar(7) NULL,
    [DisplayBold] char(1) NULL,
    [Undisc_amt] float NULL,
    [View_Logi] bit NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [parentCode] varchar(30) NULL,
    [EstSubRentalCost] float NULL,
    [EstSubRentalDays] smallint NULL,
    [VendorID] int NULL,
    [Notes] varchar(MAX) NULL,
    [UseEstSubHireOverride] bit NULL,
    [QTSource] tinyint NULL,
    [QTBookingNo] varchar(35) NULL,
    [POPrefix] varchar(3) NOT NULL,
    [CrossRental] char(1) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwAssetPlusParent')
BEGIN
CREATE TABLE [vwAssetPlusParent] (
    [PLAID] decimal(10,0) NULL,
    [dateTimePacked] datetime NULL,
    [PARENT_CODE] char(30) NULL,
    [showName] varchar(50) NULL,
    [ID] decimal(10,0) NOT NULL,
    [ASSET_CODE] char(30) NULL,
    [DESCRIPTION] varchar(50) NULL,
    [PRODUCT_COde] char(30) NULL,
    [STOCK_NUMBER] int NULL,
    [SERIAL_NO] varchar(25) NULL,
    [COST] float NULL,
    [EST_RESALE] float NULL,
    [Disposal_AMT] float NULL,
    [REVAL_TD] float NULL,
    [INSURER] char(6) NULL,
    [INSURED_VAL] float NULL,
    [BOOKING_NO] varchar(35) NULL,
    [DEL_TIME_H] tinyint NULL,
    [DEL_TIME_M] tinyint NULL,
    [RET_TIME_H] tinyint NULL,
    [RET_TIME_M] tinyint NULL,
    [TIMES_HIRE] int NULL,
    [AMOUNT_LTD] float NULL,
    [days_IN_Service] int NULL,
    [days_REQ_service] int NULL,
    [METHOD_TAX] tinyint NULL,
    [DEPN_RATE_tax] float NULL,
    [ACCUM_DEPN_tax] float NULL,
    [YTD_DEPN_Tax] float NULL,
    [DEPN_LY_TAx] float NULL,
    [WRTN_DOWN_val_tax] float NULL,
    [warehouse_time_h] tinyint NULL,
    [wareHouse_time_m] tinyint NULL,
    [times_hired_1_4td] int NULL,
    [current_1_4] tinyint NULL,
    [locn] int NULL,
    [modelNumber] varchar(25) NULL,
    [PurDate] datetime NULL,
    [StartDate] datetime NULL,
    [DisDate] datetime NULL,
    [DelDate] datetime NULL,
    [RetDate] datetime NULL,
    [LastTaxDate] datetime NULL,
    [WareDate] datetime NULL,
    [PONumber] int NULL,
    [ReturnFromservice] datetime NULL,
    [ServiceStatus] tinyint NULL,
    [VendorV8] varchar(30) NULL,
    [KeepStatus] tinyint NULL,
    [NextTestDate] datetime NULL,
    [LastTestDate] datetime NULL,
    [OperationalStatus] tinyint NULL,
    [TestFrequencyDays] int NULL,
    [Financier] varchar(50) NULL,
    [FinanceStartDate] datetime NULL,
    [FinanceEndDate] datetime NULL,
    [ContractNo] varchar(20) NULL,
    [RepayAmount] float NULL,
    [FinanceType] varchar(50) NULL,
    [FinanceTotal] float NULL,
    [RFIDTag] varchar(50) NULL,
    [RevCenterLocn] int NULL,
    [iDisposalType] int NULL,
    [SeqNo] int NULL,
    [LastTestResultsImportedFrom] int NULL,
    [HomeLocn] int NULL,
    [PCode] varchar(30) NULL,
    [NavCode] varchar(30) NULL,
    [PackStatus] bit NOT NULL,
    [latitude] float NULL,
    [longitude] float NULL,
    [LOCATION] varchar(100) NULL,
    [BookingNumber] varchar(35) NOT NULL,
    [TestRequired] bit NULL,
    [AssetStatus] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwAssetsOutForService')
BEGIN
CREATE TABLE [vwAssetsOutForService] (
    [ID] decimal(10,0) NOT NULL,
    [ReturnDate] datetime NULL,
    [ReturnTime] varchar(4) NOT NULL,
    [Product_Code] char(30) NULL,
    [Locn] int NULL,
    [OutDate] datetime NULL,
    [OutTime] varchar(4) NOT NULL,
    [Stock_number] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwAssetsOutForServicePAT')
BEGIN
CREATE TABLE [vwAssetsOutForServicePAT] (
    [ID] decimal(10,0) NOT NULL,
    [ReturnDate] datetime NULL,
    [ReturnTime] varchar(4) NOT NULL,
    [Product_Code] char(30) NULL,
    [Locn] int NULL,
    [OutDate] datetime NULL,
    [OutTime] varchar(4) NOT NULL,
    [Stock_number] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwAssignedPOList')
BEGIN
CREATE TABLE [vwAssignedPOList] (
    [LineType] int NOT NULL,
    [BookingNo] decimal(19,0) NULL,
    [PONumber] decimal(19,0) NULL,
    [LProductCode] varchar(30) NULL,
    [LFFtext] varchar(60) NULL,
    [Lquantity] float NULL,
    [LunitPrice] float NULL,
    [LineDiscount] float NULL,
    [Lprice] float NULL,
    [DiscountPerc] float NULL,
    [ActualPOCurrency] varchar(5) NULL,
    [BookingCurrency] varchar(5) NULL,
    [POPrefix] varchar(3) NULL,
    [Approved] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwAudit')
BEGIN
CREATE TABLE [vwAudit] (
    [ID] decimal(10,0) IDENTITY(NULL,NULL) NOT NULL,
    [booking_no] varchar(35) NULL,
    [time_h] tinyint NULL,
    [time_m] tinyint NULL,
    [audit_type] tinyint NULL,
    [invoice_no] decimal(19,0) NULL,
    [value] real NULL,
    [DateF] datetime NULL,
    [status] tinyint NULL,
    [reason] varchar(50) NULL,
    [operators] varchar(50) NULL,
    [CustomCreditNoteNumber] decimal(19,0) NULL,
    [version_no] smallint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwBookAndHist')
BEGIN
CREATE TABLE [vwBookAndHist] (
    [ID] decimal(10,0) NOT NULL,
    [CustID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viaV71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [Item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_nameV6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setupTimeV61] varchar(4) NULL,
    [RehearsalTime] varchar(4) NULL,
    [StrikeTime] varchar(4) NULL,
    [Trans_to_locn] int NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEndTime] varchar(4) NULL,
    [transferNo] decimal(19,0) NULL,
    [currencyStr] varchar(5) NULL,
    [BookingProgressStatus] tinyint NULL,
    [ConfirmedBy] varchar(35) NULL,
    [ConfirmedDocRef] varchar(50) NULL,
    [VenueRoom] varchar(35) NULL,
    [expAttendees] int NULL,
    [HourBooked] tinyint NULL,
    [MinBooked] tinyint NULL,
    [SecBooked] tinyint NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [HorCCroom] int NULL,
    [subrooms] char(12) NULL,
    [truckOut] int NULL,
    [truckIn] int NULL,
    [tripOut] tinyint NULL,
    [tripIn] tinyint NULL,
    [showName] varchar(50) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [OurNumberDel] char(1) NULL,
    [OurNumberRet] char(1) NULL,
    [DatesAndTimesEnabled] char(1) NULL,
    [Paymethod] varchar(25) NULL,
    [Government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [ContactLoadedIntoVenue] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [OrganizationV6] varchar(50) NULL,
    [Salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Inv_date] datetime NULL,
    [ShowSdate] datetime NULL,
    [ShowEdate] datetime NULL,
    [SetDate] datetime NULL,
    [ADelDate] datetime NULL,
    [SDate] datetime NULL,
    [RehDate] datetime NULL,
    [ConDate] datetime NULL,
    [TOutDate] datetime NULL,
    [TInDate] datetime NULL,
    [PreDate] datetime NULL,
    [ConByDate] datetime NULL,
    [bookingPrinted] char(1) NULL,
    [CustCode] varchar(30) NULL,
    [ExtendedFrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [PotPercent] float NULL,
    [Referral] varchar(50) NULL,
    [EventType] varchar(20) NULL,
    [Priority] int NULL,
    [InvoiceStage] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [PickupRetDate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [MaxBookingValue] float NULL,
    [UsesPriceTable] int NULL,
    [DateToInvoice] datetime NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [ServCont] char(1) NULL,
    [PrintedPayTerm] varchar(40) NULL,
    [PaymentOptions] tinyint NULL,
    [RentalType] tinyint NULL,
    [UseBillSchedule] char(1) NULL,
    [Tax2] float NULL,
    [ContactID] decimal(9,0) NULL,
    [dtExpected_ReturnDate] datetime NOT NULL,
    [vcExpected_ReturnTime] varchar(4) NOT NULL,
    [vcTruckOutTime] varchar(4) NOT NULL,
    [vcTruckInTime] varchar(4) NOT NULL,
    [VenueID] int NOT NULL,
    [ConfirmationFinancials] varchar(30) NULL,
    [EquipmentModified] bit NULL,
    [LoadDateTime] datetime NULL,
    [UnloadDateTime] datetime NULL,
    [DeprepDateTime] datetime NULL,
    [DeprepOn] bit NOT NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [ScheduleDatesOn] varchar(10) NULL,
    [bBookingIsComplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingID] int NULL,
    [MasterBillingMethod] tinyint NULL,
    [schedHeadEquipSpan] tinyint NULL,
    [TaxabPCT] float NOT NULL,
    [UntaxPCT] float NOT NULL,
    [Tax1PCT] float NOT NULL,
    [Tax2PCT] float NOT NULL,
    [PaymentContactID] int NULL,
    [Collection] float NULL,
    [FuelSurchargeRate] float NULL,
    [EntryDate] datetime NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [sale_of_asset_undisc_amt] float NULL,
    [LockedForScanning] bit NULL,
    [Archived] int NOT NULL,
    [ContractNo] varchar(18) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwBookAndHistAllfieldsV11_0_0_0')
BEGIN
CREATE TABLE [vwBookAndHistAllfieldsV11_0_0_0] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viaV71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [Item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_nameV6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setupTimeV61] varchar(4) NULL,
    [RehearsalTime] varchar(4) NULL,
    [StrikeTime] varchar(4) NULL,
    [Trans_to_locn] int NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEndTime] varchar(4) NULL,
    [transferNo] decimal(19,0) NULL,
    [currencyStr] varchar(5) NULL,
    [BookingProgressStatus] tinyint NULL,
    [ConfirmedBy] varchar(35) NULL,
    [ConfirmedDocRef] varchar(50) NULL,
    [VenueRoom] varchar(35) NULL,
    [expAttendees] int NULL,
    [HourBooked] tinyint NULL,
    [MinBooked] tinyint NULL,
    [SecBooked] tinyint NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [HorCCroom] int NULL,
    [subrooms] char(12) NULL,
    [truckOut] int NULL,
    [truckIn] int NULL,
    [tripOut] tinyint NULL,
    [tripIn] tinyint NULL,
    [showName] varchar(50) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [OurNumberDel] char(1) NULL,
    [OurNumberRet] char(1) NULL,
    [DatesAndTimesEnabled] char(1) NULL,
    [Paymethod] varchar(25) NULL,
    [Government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [ContactLoadedIntoVenue] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [OrganizationV6] varchar(50) NULL,
    [Salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Inv_date] datetime NULL,
    [ShowSdate] datetime NULL,
    [ShowEdate] datetime NULL,
    [SetDate] datetime NULL,
    [ADelDate] datetime NULL,
    [SDate] datetime NULL,
    [RehDate] datetime NULL,
    [ConDate] datetime NULL,
    [TOutDate] datetime NULL,
    [TInDate] datetime NULL,
    [PreDate] datetime NULL,
    [ConByDate] datetime NULL,
    [bookingPrinted] char(1) NULL,
    [CustCode] varchar(30) NULL,
    [ExtendedFrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [PotPercent] float NULL,
    [Referral] varchar(50) NULL,
    [EventType] varchar(20) NULL,
    [Priority] int NULL,
    [InvoiceStage] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [PickupRetDate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [MaxBookingValue] float NULL,
    [UsesPriceTable] int NULL,
    [DateToInvoice] datetime NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [ServCont] char(1) NULL,
    [PaymentOptions] tinyint NULL,
    [PrintedPayTerm] varchar(40) NULL,
    [RentalType] tinyint NULL,
    [UseBillSchedule] char(1) NULL,
    [Tax2] float NULL,
    [ContactID] decimal(9,0) NULL,
    [ShortHours] tinyint NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [dtExpected_ReturnDate] datetime NOT NULL,
    [vcExpected_ReturnTime] varchar(4) NOT NULL,
    [vcTruckOutTime] varchar(4) NOT NULL,
    [vcTruckInTime] varchar(4) NOT NULL,
    [CustID] decimal(10,0) NOT NULL,
    [VenueID] int NOT NULL,
    [LateChargesApplied] bit NULL,
    [shortagesAreTransfered] bit NULL,
    [VenueContactID] int NULL,
    [VenueContact] varchar(50) NULL,
    [VenueContactPhoneID] int NULL,
    [LTBillingOption] tinyint NULL,
    [DressCode] varchar(35) NULL,
    [Collection] float NULL,
    [FuelSurchargeRate] float NULL,
    [FreightLocked] bit NULL,
    [LabourLocked] bit NULL,
    [RentalLocked] bit NULL,
    [PriceLocked] bit NULL,
    [insurance_type] tinyint NULL,
    [EntryDate] datetime NULL,
    [CreditSurchargeRate] float NULL,
    [CreditSurchargeAmount] float NULL,
    [DisableTreeOrder] bit NULL,
    [ConfirmationFinancials] varchar(30) NULL,
    [EventManagementRate] float NULL,
    [EventManagementAmount] float NULL,
    [EquipmentModified] bit NULL,
    [CrewStatusColumn] tinyint NULL,
    [LoadDateTime] datetime NULL,
    [UnloadDateTime] datetime NULL,
    [DeprepDateTime] datetime NULL,
    [DeprepOn] bit NOT NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [ScheduleDatesOn] varchar(10) NULL,
    [bBookingIsComplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingID] int NULL,
    [MasterBillingMethod] tinyint NULL,
    [schedHeadEquipSpan] tinyint NULL,
    [sale_of_asset_undisc_amt] float NULL,
    [ContractNo] varchar(18) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwBookingGrid')
BEGIN
CREATE TABLE [vwBookingGrid] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [OrganizationV6] varchar(50) NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [booking_type_v32] tinyint NULL,
    [Trans_to_locn] int NULL,
    [order_no] varchar(25) NULL,
    [VenueRoom] varchar(35) NULL,
    [delivery_address_exist] char(1) NULL,
    [HorCCroom] int NULL,
    [InvoiceStage] int NULL,
    [invoiced] char(1) NULL,
    [invoice_no] decimal(19,0) NULL,
    [status] tinyint NULL,
    [BookingProgressStatus] tinyint NULL,
    [ExtendedFrom] varchar(5) NULL,
    [rDate] datetime NULL,
    [dDate] datetime NULL,
    [docs_produced] tinyint NULL,
    [event_code] char(30) NULL,
    [showName] varchar(50) NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [operatorsID] decimal(19,0) NULL,
    [Salesperson] varchar(30) NULL,
    [auth_agentv6] varchar(50) NULL,
    [subrooms] char(12) NULL,
    [Agent_code] varchar(5) NOT NULL,
    [Price_Quoted] float NULL,
    [CSalesperson] char(30) NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [ConDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [currencyStr] varchar(5) NULL,
    [CrewStatusColumn] tinyint NULL,
    [bBookingIsComplete] bit NULL,
    [Vendorname] varchar(50) NULL,
    [Division] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwBookingGrid_v2')
BEGIN
CREATE TABLE [vwBookingGrid_v2] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [OrganizationV6] varchar(50) NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [booking_type_v32] tinyint NULL,
    [Trans_to_locn] int NULL,
    [order_no] varchar(25) NULL,
    [VenueRoom] varchar(35) NULL,
    [delivery_address_exist] char(1) NULL,
    [HorCCroom] int NULL,
    [InvoiceStage] int NULL,
    [invoiced] char(1) NULL,
    [invoice_no] decimal(19,0) NULL,
    [status] tinyint NULL,
    [BookingProgressStatus] tinyint NULL,
    [ExtendedFrom] varchar(5) NULL,
    [rDate] datetime NULL,
    [dDate] datetime NULL,
    [docs_produced] tinyint NULL,
    [event_code] char(30) NULL,
    [showName] varchar(50) NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [operatorsID] decimal(19,0) NULL,
    [Salesperson] varchar(30) NULL,
    [auth_agentv6] varchar(50) NULL,
    [subrooms] char(12) NULL,
    [Agent_code] varchar(5) NOT NULL,
    [Price_Quoted] float NULL,
    [CSalesperson] char(30) NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [ConDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [currencyStr] varchar(5) NULL,
    [CrewStatusColumn] tinyint NULL,
    [bBookingIsComplete] bit NULL,
    [VendorName] varchar(50) NULL,
    [division] tinyint NULL,
    [Tax1] float NULL,
    [Tax2] float NULL,
    [LockedForScanning] bit NULL,
    [reason] varchar(1) NOT NULL,
    [entrydate] datetime NULL,
    [eventtype] varchar(20) NULL,
    [bLocked] bit NULL,
    [STAGE_xml] nvarchar(MAX) NULL,
    [SyncType] tinyint NOT NULL,
    [ContractNo] varchar(18) NULL,
    [Customer_code] varchar(30) NULL,
    [PrintedPayTerm] varchar(40) NULL,
    [HasQT] bit NOT NULL,
    [HasDAT] bit NOT NULL,
    [ShowSdate] datetime NULL,
    [ShowStartTime] varchar(4) NULL,
    [ShowEDate] datetime NULL,
    [ShowEndTime] varchar(4) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwBookingGridHint')
BEGIN
CREATE TABLE [vwBookingGridHint] (
    [product_code_v42] char(30) NULL,
    [comment_desc_v42] char(70) NULL,
    [trans_qty] decimal(19,0) NULL,
    [trans_type_v41] tinyint NULL,
    [item_type] tinyint NULL,
    [booking_no_v32] varchar(35) NULL,
    [Heading_no] tinyint NULL,
    [groupseqno] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwBookingsToBeReinvoiced')
BEGIN
CREATE TABLE [vwBookingsToBeReinvoiced] (
    [BookingID] decimal(10,0) NOT NULL,
    [BookingNumber] varchar(35) NULL,
    [Showname] varchar(50) NULL,
    [CustomerName] varchar(50) NULL,
    [InvoiceNumber] decimal(19,0) NULL,
    [InvoiceDate] datetime NULL,
    [From_locn] int NULL,
    [InvoiceAmount] float NULL,
    [LastOpToModify] varchar(50) NULL,
    [OriginalOperator] varchar(50) NULL,
    [OperatorsID] decimal(19,0) NULL,
    [NewBookingValue] float NULL,
    [CurrencyStr] varchar(5) NOT NULL,
    [ProjectCode] char(30) NULL,
    [ProjectDescription] char(32) NULL,
    [ProjectManager] varchar(45) NULL,
    [BookingsInvoiced] tinyint NULL,
    [MasterBillingMethod] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCarnetBookingItems')
BEGIN
CREATE TABLE [vwCarnetBookingItems] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [product_code_v42] char(30) NULL,
    [stock_number] int NULL,
    [AssetTranID] decimal(10,0) NULL,
    [UnitValue] float NULL,
    [ASSET_CODE] char(30) NULL,
    [QtyOut] int NULL,
    [GroupFld] varchar(30) NULL,
    [QtyRet] int NULL,
    [AssignTo] varchar(255) NULL,
    [Heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [descriptionV6] char(70) NULL,
    [heading_desc] varchar(79) NULL,
    [item_type] tinyint NULL,
    [prodRoadCase] tinyint NULL,
    [isGenericItem] char(1) NULL,
    [PackageLevel] smallint NULL,
    [parent_basecode_ID] int NULL,
    [parent_basecode] char(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCarnetItems')
BEGIN
CREATE TABLE [vwCarnetItems] (
    [ID] decimal(10,0) NOT NULL,
    [CarnetID] decimal(10,0) NULL,
    [Qty] int NULL,
    [ItemTranID] decimal(10,0) NULL,
    [AssetTranID] decimal(10,0) NULL,
    [HeadingID] decimal(10,0) NULL,
    [SeqNo] int NULL,
    [HSCode] varchar(20) NULL,
    [CustomDesc] varchar(80) NULL,
    [CustomWeight] float NULL,
    [CustomValue] float NULL,
    [useVolume] bit NULL,
    [SerialNo] varchar(25) NULL,
    [Origin] varchar(25) NULL,
    [CustomHeight] float NULL,
    [CustomWidth] float NULL,
    [CustomLength] float NULL,
    [Exclude] bit NULL,
    [HeadingDesc] varchar(50) NULL,
    [isGenericItem] char(1) NULL,
    [Description] char(70) NULL,
    [item_type] tinyint NULL,
    [AssignTo] varchar(255) NULL,
    [ProdCode] char(30) NULL,
    [Barcode] char(30) NULL,
    [UnitValue] float NULL,
    [ProdOnlyUnitValue] float NULL,
    [parent_basecode] char(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCheckAvail')
BEGIN
CREATE TABLE [vwCheckAvail] (
    [bkno] varchar(35) NULL,
    [trantype] tinyint NULL,
    [Assignbk] varchar(35) NULL,
    [dateo] datetime NULL,
    [HBooked] tinyint NULL,
    [MBooked] tinyint NULL,
    [SBooked] tinyint NULL,
    [Pon] char(1) NULL,
    [Flocn] int NULL,
    [TTlocn] int NULL,
    [PrDate] datetime NULL,
    [PrTimeH] tinyint NULL,
    [PrTimeM] tinyint NULL,
    [RDatev] datetime NULL,
    [bitField] tinyint NULL,
    [FDatev] datetime NULL,
    [DBooked] datetime NULL,
    [TBookH] tinyint NULL,
    [TBookM] tinyint NULL,
    [TBookS] tinyint NULL,
    [dth] tinyint NULL,
    [dtm] tinyint NULL,
    [rtlocn] int NULL,
    [rth] tinyint NULL,
    [rtm] tinyint NULL,
    [trqty] decimal(20,0) NULL,
    [MyQtycheckedout] decimal(19,0) NULL,
    [retqty] decimal(19,0) NULL,
    [ID] decimal(10,0) NOT NULL,
    [bkProg] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [return_to_locn] int NULL,
    [AssignTo] varchar(255) NULL,
    [AssignType] tinyint NOT NULL,
    [QtyReserved] int NULL,
    [DeprepOn] bit NOT NULL,
    [DeprepDateTime] datetime NULL,
    [QuickTurnaroundQty] int NULL,
    [InRack] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCheckAvailIDXV2')
BEGIN
CREATE TABLE [vwCheckAvailIDXV2] (
    [AvailRecFlag] bit NOT NULL,
    [bkno] varchar(35) NULL,
    [trantype] tinyint NULL,
    [Assignbk] varchar(35) NULL,
    [dateo] datetime NULL,
    [HBooked] tinyint NULL,
    [MBooked] tinyint NULL,
    [SBooked] tinyint NULL,
    [Pon] char(1) NULL,
    [Flocn] int NULL,
    [TTlocn] int NULL,
    [PrDate] datetime NULL,
    [PrTimeH] tinyint NULL,
    [PrTimeM] tinyint NULL,
    [RDatev] datetime NULL,
    [bitField] tinyint NULL,
    [FDatev] datetime NULL,
    [DBooked] datetime NULL,
    [TBookH] tinyint NULL,
    [TBookM] tinyint NULL,
    [TBookS] tinyint NULL,
    [dth] tinyint NULL,
    [dtm] tinyint NULL,
    [rtlocn] int NULL,
    [rth] tinyint NULL,
    [rtm] tinyint NULL,
    [trqty] decimal(20,0) NULL,
    [MyQtycheckedout] decimal(19,0) NULL,
    [retqty] decimal(19,0) NULL,
    [ID] decimal(10,0) NOT NULL,
    [bkProg] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [return_to_locn] int NULL,
    [AssignTo] varchar(255) NULL,
    [AssignType] tinyint NOT NULL,
    [QtyReserved] int NULL,
    [DeprepOn] bit NOT NULL,
    [DeprepDateTime] datetime NULL,
    [QuickTurnaroundQty] int NULL,
    [InRack] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwComments')
BEGIN
CREATE TABLE [vwComments] (
    [ID] decimal(10,0) IDENTITY(NULL,NULL) NOT NULL,
    [heading_no] tinyint NULL,
    [sub_seq_no] int NULL,
    [comment_desc_v42] char(70) NULL,
    [Booking_no_v32] varchar(35) NULL,
    [seq_no] decimal(19,0) NULL,
    [GroupSeqNo] int NULL,
    [SubRentalLinkID] int NOT NULL,
    [view_logi] bit NULL,
    [view_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwContactManageGrid')
BEGIN
CREATE TABLE [vwContactManageGrid] (
    [ID] decimal(10,0) NOT NULL,
    [linkid] decimal(10,0) NULL,
    [DisabledCust] char(1) NOT NULL,
    [CustomerType] tinyint NOT NULL,
    [SurName] varchar(35) NULL,
    [contactname] varchar(35) NULL,
    [Adr1] varchar(50) NULL,
    [Adr2] varchar(50) NULL,
    [Adr3] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [isTech] bit NULL,
    [TechCode] char(30) NULL,
    [Sendme_emails] char(1) NULL,
    [AgencyContact] bit NULL,
    [bDriver] bit NOT NULL,
    [bFreeLanceContact] bit NOT NULL,
    [isVendor] bit NULL,
    [FirstName] varchar(25) NULL,
    [projectmanager] bit NULL,
    [OrganisationV6] varchar(50) NULL,
    [Phone1] varchar(47) NOT NULL,
    [ext1] varchar(8) NULL,
    [SalesPerson_Code] varchar(30) NULL,
    [desig] nvarchar(MAX) NULL,
    [Phone2] varchar(47) NOT NULL,
    [ext2] varchar(8) NULL,
    [Cell] varchar(38) NOT NULL,
    [ConAdr] varchar(267) NULL,
    [webpage] varchar(80) NULL,
    [LastContact] datetime NULL,
    [ConField1] varchar(50) NULL,
    [PostCode] varchar(12) NULL,
    [Position] varchar(50) NULL,
    [CreateDate] datetime NULL,
    [LastUpdate] datetime NULL,
    [p1cc] varchar(10) NULL,
    [p1ac] varchar(10) NULL,
    [p1d] varchar(16) NULL,
    [p1e] varchar(8) NULL,
    [p2cc] varchar(10) NULL,
    [p2ac] varchar(10) NULL,
    [p2d] varchar(16) NULL,
    [p2e] varchar(8) NULL,
    [cellcc] varchar(10) NULL,
    [cellac] varchar(10) NULL,
    [celld] varchar(16) NULL,
    [fcc] varchar(10) NULL,
    [fac] varchar(10) NULL,
    [fd] varchar(16) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [Phone2Ext] varchar(8) NULL,
    [IndustryDescription] varchar(35) NULL,
    [email] varchar(80) NULL,
    [PostalAddress1] char(50) NULL,
    [PostalAddress2] char(50) NULL,
    [PostalAddress3] char(50) NULL,
    [PostalPostCode] char(12) NULL,
    [PostalState] varchar(50) NULL,
    [PostalCountry] varchar(50) NULL,
    [PostalAddress] varchar(272) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwContactPhones')
BEGIN
CREATE TABLE [vwContactPhones] (
    [ID] decimal(10,0) IDENTITY(NULL,NULL) NOT NULL,
    [CustCodeLink] varchar(30) NULL,
    [Contactname] varchar(35) NULL,
    [firstname] varchar(25) NULL,
    [surname] varchar(35) NULL,
    [nameKeyField] varchar(11) NULL,
    [position] varchar(50) NULL,
    [busname] varchar(50) NULL,
    [Adr1] varchar(50) NULL,
    [Adr2] varchar(50) NULL,
    [Adr3] varchar(50) NULL,
    [Postcode] varchar(12) NULL,
    [Phone1] varchar(16) NULL,
    [Phone2] varchar(16) NULL,
    [Fax] varchar(16) NULL,
    [Webpage] varchar(80) NULL,
    [Email] varchar(80) NULL,
    [driversLicNo] varchar(20) NULL,
    [OtherID] varchar(30) NULL,
    [specialty] varchar(60) NULL,
    [PictureDatafile] varchar(240) NULL,
    [lastBookDate] datetime NULL,
    [MidName] varchar(35) NULL,
    [Cell] varchar(16) NULL,
    [Ext1] varchar(8) NULL,
    [Ext2] varchar(8) NULL,
    [Active] char(1) NULL,
    [MailList] char(1) NULL,
    [DecMaker] char(1) NULL,
    [LastContact] datetime NULL,
    [LastAttempt] datetime NULL,
    [Department] varchar(50) NULL,
    [SourceID] decimal(10,0) NULL,
    [CreateDate] datetime NULL,
    [LastUpdate] datetime NULL,
    [ReferralName] varchar(50) NULL,
    [Field1] varchar(50) NULL,
    [Field2] varchar(50) NULL,
    [Field3] varchar(50) NULL,
    [Field4] varchar(50) NULL,
    [Field5] varchar(50) NULL,
    [Field6] varchar(50) NULL,
    [Field7] datetime NULL,
    [Field8] datetime NULL,
    [AskFor] varchar(20) NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [Sendme_faxes] char(1) NULL,
    [Sendme_emails] char(1) NULL,
    [CardHolder_Name] varchar(250) NULL,
    [SalesPerson_Code] varchar(30) NULL,
    [SalesAssignEndDate] datetime NULL,
    [Country] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [bDriver] bit NOT NULL,
    [bFreeLanceContact] bit NOT NULL,
    [JobTitle] varchar(15) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [FaxDialAreaCode] bit NULL,
    [FaxCallType] tinyint NULL,
    [SubRentalVendor] varchar(30) NULL,
    [AgencyContact] bit NULL,
    [UpdateVendorContact] bit NULL,
    [username] varchar(50) NULL,
    [password] varchar(256) NULL,
    [TimeZone] varchar(30) NULL,
    [RPwebservicesActive] bit NOT NULL,
    [RPWSDefaultOpID] int NULL,
    [CultureInt] int NULL,
    [ProjectManager] bit NULL,
    [Utc] smallint NULL,
    [field9] varchar(50) NULL,
    [field10] varchar(50) NULL,
    [field11] varchar(50) NULL,
    [field12] varchar(50) NULL,
    [field13] varchar(50) NULL,
    [field14] varchar(50) NULL,
    [field15] datetime NULL,
    [RPWSCrewManager] bit NULL,
    [ContactPhone1] varchar(47) NOT NULL,
    [ContactPhone2] varchar(47) NOT NULL,
    [ContactCell] varchar(38) NOT NULL,
    [ContactFax] varchar(38) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCrewAndHist')
BEGIN
CREATE TABLE [vwCrewAndHist] (
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] int NULL,
    [price] float NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [person] char(30) NULL,
    [task] tinyint NULL,
    [TechRate] float NULL,
    [TechPay] float NULL,
    [unitRate] float NULL,
    [techrateIsHourorDay] char(1) NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [GroupSeqNo] int NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [bit_field_v41] tinyint NULL,
    [SubrentalLinkID] int NULL,
    [AssignTo] varchar(35) NULL,
    [days_using] float NULL,
    [MinimumHours] float NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [ID] decimal(10,0) NOT NULL,
    [CrewClientNotes] varchar(40) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCrewItems')
BEGIN
CREATE TABLE [vwCrewItems] (
    [ID] decimal(10,0) NOT NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [trans_qty] int NULL,
    [bit_field_v41] tinyint NULL,
    [price] float NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [days_using] float NULL,
    [GroupSeqNo] int NULL,
    [descriptionV6] varchar(50) NULL,
    [product_type_v41] tinyint NULL,
    [EnforceMinHours] bit NULL,
    [MinimumHours] float NULL,
    [person] char(30) NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [task] tinyint NULL,
    [person_required] char(1) NULL,
    [unitRate] float NULL,
    [TechRate] float NULL,
    [techrateIsHourorDay] char(1) NULL,
    [TechPay] float NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [booking_no_v32] varchar(35) NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [AssignTo] varchar(35) NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [SubrentalLinkID] int NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [PPONumber] decimal(19,0) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [Notes] varchar(512) NULL,
    [AdmModifiedNoteDate] datetime NULL,
    [JobTimeZone] tinyint NULL,
    [TechTimezone] tinyint NULL,
    [JobOffered] bit NULL,
    [JobOffereddate] datetime NULL,
    [JobAccepted] bit NULL,
    [JobAcceptedDate] datetime NULL,
    [Conflict] bit NULL,
    [PrintOnInvoice] bit NULL,
    [PrintOnQuote] bit NULL,
    [JobTechOfferDate] datetime NULL,
    [JobTechOfferStatus] tinyint NULL,
    [JobTechNotes] varchar(512) NULL,
    [CrewClientNotes] varchar(40) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCrewSplitByDate')
BEGIN
CREATE TABLE [vwCrewSplitByDate] (
    [dayDate] datetime NULL,
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] int NULL,
    [price] float NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [person] char(30) NULL,
    [task] tinyint NULL,
    [TechRate] float NULL,
    [TechPay] float NULL,
    [unitRate] float NULL,
    [techrateIsHourorDay] char(1) NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [GroupSeqNo] int NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [bit_field_v41] tinyint NULL,
    [SubrentalLinkID] int NULL,
    [AssignTo] varchar(35) NULL,
    [days_using] float NULL,
    [MinimumHours] float NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [Notes] varchar(512) NULL,
    [AdmModifiedNoteDate] datetime NULL,
    [JobTimeZone] tinyint NULL,
    [TechTimezone] tinyint NULL,
    [JobOffered] bit NULL,
    [JobOffereddate] datetime NULL,
    [JobAccepted] bit NULL,
    [JobAcceptedDate] datetime NULL,
    [Conflict] bit NULL,
    [PrintOnQuote] bit NULL,
    [PrintOnInvoice] bit NULL,
    [JobTechOfferStatus] tinyint NULL,
    [JobTechOfferDate] datetime NULL,
    [JobTechNotes] varchar(512) NULL,
    [CrewClientNotes] varchar(40) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCrossRentalList')
BEGIN
CREATE TABLE [vwCrossRentalList] (
    [LineType] int NOT NULL,
    [BookingNo] varchar(35) NULL,
    [ProductCode] varchar(30) NULL,
    [ProductDescription] varchar(70) NULL,
    [Price] float NULL,
    [LineDiscount] float NULL,
    [BookingDiscount] float NULL,
    [Amount] float NULL,
    [UseEstSubHireOverride] int NOT NULL,
    [EstSubRentalCost] float NOT NULL,
    [ViewClient] int NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [ActualPOCurrency] varchar(5) NULL,
    [Approved] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCurrentAndDeletedAssets')
BEGIN
CREATE TABLE [vwCurrentAndDeletedAssets] (
    [ProductCode] char(30) NULL,
    [StockNumber] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCurrentAssets')
BEGIN
CREATE TABLE [vwCurrentAssets] (
    [ID] decimal(10,0) NOT NULL,
    [product_code] char(30) NULL,
    [locn] int NULL,
    [stock_number] int NULL,
    [InRack] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCustAndVend')
BEGIN
CREATE TABLE [vwCustAndVend] (
    [ID] decimal(10,0) NOT NULL,
    [VCCode] varchar(30) NULL,
    [VCType] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCustomer')
BEGIN
CREATE TABLE [vwCustomer] (
    [ID] decimal(10,0) IDENTITY(NULL,NULL) NOT NULL,
    [Customer_code] varchar(30) NULL,
    [PostalAddress1] char(50) NULL,
    [PostalAddress2] char(50) NULL,
    [PostalAddress3] char(50) NULL,
    [postalPostCode] char(12) NULL,
    [currencyStr] varchar(5) NULL,
    [UsesPriceTableV71] tinyint NULL,
    [post_code] char(12) NULL,
    [sales_tax_no] char(25) NULL,
    [Account_type] tinyint NULL,
    [industry_type] varchar(8) NULL,
    [insurance_type] tinyint NULL,
    [hire_tax_exempt] char(1) NULL,
    [TaxAuthority1] int NULL,
    [Price_customer_pays] tinyint NULL,
    [customer_number] char(6) NULL,
    [stop_credit] tinyint NULL,
    [Last_bk_seq] varchar(5) NULL,
    [Credit_limit] float NULL,
    [Current] float NULL,
    [Seven_days] float NULL,
    [Fourteen_days] float NULL,
    [Twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [discount_rate] float NULL,
    [last_pmt_amt] float NULL,
    [account_is_zero] char(1) NULL,
    [Monthly_cycle_billing_basis] tinyint NULL,
    [salesperson] char(30) NULL,
    [taxAuthority2] int NULL,
    [contactV6] varchar(35) NULL,
    [OrganisationV6] varchar(50) NULL,
    [Address_l1V6] char(50) NULL,
    [Address_l2V6] char(50) NULL,
    [Address_l3V6] char(50) NULL,
    [webAddress] varchar(80) NULL,
    [emailAddress] varchar(80) NULL,
    [Paymethod] varchar(16) NULL,
    [lastTranDate] datetime NULL,
    [lastPmtDate] datetime NULL,
    [lastBalupDate] datetime NULL,
    [firstUnpayInvDate] datetime NULL,
    [SalesAssignEndDate] datetime NULL,
    [CustCDate] datetime NULL,
    [FirstInvDate] datetime NULL,
    [DisabledCust] char(1) NULL,
    [AcctMgr] varchar(30) NULL,
    [IndustryDescription] varchar(35) NULL,
    [Field1] varchar(25) NULL,
    [Field2] varchar(25) NULL,
    [Field3] varchar(25) NULL,
    [Field4] varchar(25) NULL,
    [Field5] varchar(25) NULL,
    [Field6] varchar(25) NULL,
    [Field7] varchar(25) NULL,
    [Field8] varchar(25) NULL,
    [Field9] varchar(25) NULL,
    [Field10] varchar(25) NULL,
    [Field11] varchar(25) NULL,
    [Field12] varchar(25) NULL,
    [Field13] varchar(25) NULL,
    [Field14] varchar(25) NULL,
    [Field15] datetime NULL,
    [Field16] datetime NULL,
    [Field17] char(1) NULL,
    [Field18] char(1) NULL,
    [Field19] char(1) NULL,
    [Field20] char(1) NULL,
    [Field21] char(1) NULL,
    [Field22] char(1) NULL,
    [Field23] char(1) NULL,
    [Field24] char(1) NULL,
    [Field25] char(1) NULL,
    [Field26] char(1) NULL,
    [Field27] int NULL,
    [Field28] int NULL,
    [Field29] int NULL,
    [Field30] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [Field31] varchar(120) NULL,
    [Field32] varchar(120) NULL,
    [Phone2Ext] varchar(8) NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [StreetCountry] varchar(50) NULL,
    [StreetState] varchar(50) NULL,
    [PostalCountry] varchar(50) NULL,
    [PostalState] varchar(50) NULL,
    [InsuranceCertificate] varchar(25) NULL,
    [InsuredAmount] float NULL,
    [InsuredFromDate] datetime NULL,
    [InsuredToDate] datetime NULL,
    [iLink_ContactID] decimal(10,0) NOT NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [bPONumRequired] bit NULL,
    [CustomerType] tinyint NULL,
    [CampaignID] int NULL,
    [DefaultCustomerDivision] tinyint NULL,
    [FaxCallType] tinyint NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [FaxDialAreaCode] bit NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [EnteredByOpID] decimal(10,0) NULL,
    [bCustomTemplateList] bit NULL,
    [AREmailAddress] varchar(80) NULL,
    [CustTypeForExporting] int NULL,
    [Phone] char(32) NULL,
    [fax] char(16) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [PaymentContactID] int NULL,
    [QBO_id] varchar(10) NULL,
    [StripeID] varchar(24) NULL,
    [isVendor] bit NULL,
    [MinPOAmount] float NULL,
    [Vaccno] varchar(30) NULL,
    [freightzone] int NULL,
    [DefaultBookingContactID] decimal(10,0) NULL,
    [XeroId] varchar(36) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCustomerGrid')
BEGIN
CREATE TABLE [vwCustomerGrid] (
    [ID] decimal(10,0) IDENTITY(NULL,NULL) NOT NULL,
    [Customer_code] varchar(30) NULL,
    [DisabledCust] char(1) NULL,
    [InsuredFromDate] datetime NULL,
    [InsuredToDate] datetime NULL,
    [CustomerType] tinyint NULL,
    [contactV6] varchar(35) NULL,
    [OrganisationV6] varchar(50) NULL,
    [PostalAddress1] char(50) NULL,
    [PostalAddress2] char(50) NULL,
    [PostalAddress3] char(50) NULL,
    [PostalPostCode] char(12) NULL,
    [PostalState] varchar(50) NULL,
    [PostalCountry] varchar(50) NULL,
    [Address_l1V6] char(50) NULL,
    [Address_l2V6] char(50) NULL,
    [Address_l3V6] char(50) NULL,
    [StreetState] varchar(50) NULL,
    [StreetCountry] varchar(50) NULL,
    [post_code] char(12) NULL,
    [industry_type] varchar(8) NULL,
    [customer_number] char(6) NULL,
    [Credit_limit] float NULL,
    [Current] float NULL,
    [TotalBal] float NULL,
    [Seven_days] float NULL,
    [Fourteen_days] float NULL,
    [Twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [discount_rate] float NULL,
    [last_pmt_amt] float NULL,
    [salesperson] char(30) NULL,
    [webAddress] varchar(80) NULL,
    [emailAddress] varchar(80) NULL,
    [Paymethod] varchar(16) NULL,
    [lastTranDate] datetime NULL,
    [lastPmtDate] datetime NULL,
    [lastBalupDate] datetime NULL,
    [firstUnpayInvDate] datetime NULL,
    [SalesAssignEndDate] datetime NULL,
    [CustCDate] datetime NULL,
    [FirstInvDate] datetime NULL,
    [AcctMgr] varchar(30) NULL,
    [isVendor] bit NULL,
    [IndustryDescription] varchar(35) NULL,
    [CField1] varchar(25) NULL,
    [CField2] varchar(25) NULL,
    [CField3] varchar(25) NULL,
    [CField4] varchar(25) NULL,
    [CField5] varchar(25) NULL,
    [CField6] varchar(25) NULL,
    [CField7] varchar(25) NULL,
    [CField8] varchar(25) NULL,
    [CField9] varchar(25) NULL,
    [CField10] varchar(25) NULL,
    [CField11] varchar(25) NULL,
    [CField12] varchar(25) NULL,
    [CField13] varchar(25) NULL,
    [CField14] varchar(25) NULL,
    [CField15] datetime NULL,
    [CField16] datetime NULL,
    [CField17] char(1) NULL,
    [CField18] char(1) NULL,
    [CField19] char(1) NULL,
    [CField20] char(1) NULL,
    [CField21] char(1) NULL,
    [CField22] char(1) NULL,
    [CField23] char(1) NULL,
    [CField24] char(1) NULL,
    [CField25] char(1) NULL,
    [CField26] char(1) NULL,
    [CField27] int NULL,
    [CField28] int NULL,
    [CField29] int NULL,
    [CField30] int NULL,
    [CField31] varchar(120) NULL,
    [CField32] varchar(120) NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [AREmailAddress] varchar(80) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [CampaignID] int NULL,
    [Fax] varchar(37) NULL,
    [Phone] varchar(47) NULL,
    [Phone2] varchar(47) NULL,
    [CellNumber] varchar(38) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [Phone2Ext] varchar(8) NULL,
    [bCustomTemplateList] bit NULL,
    [PostalAddress] varchar(272) NULL,
    [StreetAddress] varchar(271) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwCustomerPhones')
BEGIN
CREATE TABLE [vwCustomerPhones] (
    [ID] decimal(10,0) IDENTITY(NULL,NULL) NOT NULL,
    [Customer_code] varchar(30) NULL,
    [PostalAddress1] char(50) NULL,
    [PostalAddress2] char(50) NULL,
    [PostalAddress3] char(50) NULL,
    [postalPostCode] char(12) NULL,
    [currencyStr] varchar(5) NULL,
    [UsesPriceTableV71] tinyint NULL,
    [post_code] char(12) NULL,
    [sales_tax_no] char(25) NULL,
    [Account_type] tinyint NULL,
    [industry_type] varchar(8) NULL,
    [insurance_type] tinyint NULL,
    [hire_tax_exempt] char(1) NULL,
    [TaxAuthority1] int NULL,
    [Price_customer_pays] tinyint NULL,
    [customer_number] char(6) NULL,
    [stop_credit] tinyint NULL,
    [Last_bk_seq] varchar(5) NULL,
    [Credit_limit] float NULL,
    [Current] float NULL,
    [Seven_days] float NULL,
    [Fourteen_days] float NULL,
    [Twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [discount_rate] float NULL,
    [last_pmt_amt] float NULL,
    [account_is_zero] char(1) NULL,
    [Monthly_cycle_billing_basis] tinyint NULL,
    [salesperson] char(30) NULL,
    [taxAuthority2] int NULL,
    [contactV6] varchar(35) NULL,
    [OrganisationV6] varchar(50) NULL,
    [Address_l1V6] char(50) NULL,
    [Address_l2V6] char(50) NULL,
    [Address_l3V6] char(50) NULL,
    [webAddress] varchar(80) NULL,
    [emailAddress] varchar(80) NULL,
    [Paymethod] varchar(16) NULL,
    [lastTranDate] datetime NULL,
    [lastPmtDate] datetime NULL,
    [lastBalupDate] datetime NULL,
    [firstUnpayInvDate] datetime NULL,
    [SalesAssignEndDate] datetime NULL,
    [CustCDate] datetime NULL,
    [FirstInvDate] datetime NULL,
    [DisabledCust] char(1) NULL,
    [AcctMgr] varchar(30) NULL,
    [IndustryDescription] varchar(35) NULL,
    [Field1] varchar(25) NULL,
    [Field2] varchar(25) NULL,
    [Field3] varchar(25) NULL,
    [Field4] varchar(25) NULL,
    [Field5] varchar(25) NULL,
    [Field6] varchar(25) NULL,
    [Field7] varchar(25) NULL,
    [Field8] varchar(25) NULL,
    [Field9] varchar(25) NULL,
    [Field10] varchar(25) NULL,
    [Field11] varchar(25) NULL,
    [Field12] varchar(25) NULL,
    [Field13] varchar(25) NULL,
    [Field14] varchar(25) NULL,
    [Field15] datetime NULL,
    [Field16] datetime NULL,
    [Field17] char(1) NULL,
    [Field18] char(1) NULL,
    [Field19] char(1) NULL,
    [Field20] char(1) NULL,
    [Field21] char(1) NULL,
    [Field22] char(1) NULL,
    [Field23] char(1) NULL,
    [Field24] char(1) NULL,
    [Field25] char(1) NULL,
    [Field26] char(1) NULL,
    [Field27] int NULL,
    [Field28] int NULL,
    [Field29] int NULL,
    [Field30] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [Field31] varchar(120) NULL,
    [Field32] varchar(120) NULL,
    [Phone2Ext] varchar(8) NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [StreetCountry] varchar(50) NULL,
    [StreetState] varchar(50) NULL,
    [PostalCountry] varchar(50) NULL,
    [PostalState] varchar(50) NULL,
    [InsuranceCertificate] varchar(25) NULL,
    [InsuredAmount] float NULL,
    [InsuredFromDate] datetime NULL,
    [InsuredToDate] datetime NULL,
    [iLink_ContactID] decimal(10,0) NOT NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [bPONumRequired] bit NULL,
    [CustomerType] tinyint NULL,
    [CampaignID] int NULL,
    [DefaultCustomerDivision] tinyint NULL,
    [FaxCallType] tinyint NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [FaxDialAreaCode] bit NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [EnteredByOpID] decimal(10,0) NULL,
    [bCustomTemplateList] bit NULL,
    [AREmailAddress] varchar(80) NULL,
    [CustTypeForExporting] int NULL,
    [Phone] char(32) NULL,
    [fax] char(16) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [PaymentContactID] int NULL,
    [QBO_id] varchar(10) NULL,
    [StripeID] varchar(24) NULL,
    [isVendor] bit NULL,
    [MinPOAmount] float NULL,
    [Vaccno] varchar(30) NULL,
    [freightzone] int NULL,
    [DefaultBookingContactID] decimal(10,0) NULL,
    [XeroId] varchar(36) NULL,
    [CPhone1] varchar(47) NULL,
    [CPhone2] varchar(47) NULL,
    [CFax] varchar(38) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwDocuSign')
BEGIN
CREATE TABLE [vwDocuSign] (
    [ID] decimal(10,0) NOT NULL,
    [BookingNo] varchar(35) NULL,
    [showName] varchar(50) NULL,
    [Company] varchar(50) NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Salesperson] varchar(30) NULL,
    [Amount] float NULL,
    [BookingProgressStatus] tinyint NULL,
    [From_locn] int NULL,
    [zStatus] varchar(20) NULL,
    [StatDate] datetime NULL,
    [dsStatus] tinyint NULL,
    [EnvID] varchar(50) NULL,
    [AttachID] decimal(10,0) NULL,
    [dsType] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwDocuSignDS')
BEGIN
CREATE TABLE [vwDocuSignDS] (
    [ID] decimal(10,0) NOT NULL,
    [BookingNo] varchar(35) NULL,
    [showName] varchar(50) NULL,
    [Company] varchar(50) NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Salesperson] varchar(30) NULL,
    [Amount] float NULL,
    [BookingProgressStatus] tinyint NULL,
    [From_locn] int NULL,
    [zStatus] varchar(20) NULL,
    [StatDate] datetime NULL,
    [dsStatus] tinyint NULL,
    [EnvID] varchar(50) NULL,
    [AttachID] decimal(10,0) NULL,
    [dsType] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwDocuSignRP')
BEGIN
CREATE TABLE [vwDocuSignRP] (
    [ID] decimal(10,0) NOT NULL,
    [BookingNo] varchar(35) NULL,
    [showName] varchar(50) NULL,
    [Company] varchar(50) NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Salesperson] varchar(30) NULL,
    [Amount] float NULL,
    [BookingProgressStatus] tinyint NULL,
    [From_locn] int NULL,
    [zStatus] varchar(8) NULL,
    [StatDate] datetime NULL,
    [dsStatus] tinyint NULL,
    [EnvID] varchar(10) NULL,
    [AttachID] decimal(10,0) NOT NULL,
    [dsType] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwDuplicateBarcodes')
BEGIN
CREATE TABLE [vwDuplicateBarcodes] (
    [ID] decimal(10,0) NOT NULL,
    [Product_Code] char(30) NULL,
    [Barcode] varchar(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwGroupEmails')
BEGIN
CREATE TABLE [vwGroupEmails] (
    [GrpID] decimal(10,0) NOT NULL,
    [Group_code] varchar(30) NULL,
    [AegID] decimal(10,0) NOT NULL,
    [GroupID] decimal(10,0) NULL,
    [ContactID] decimal(10,0) NULL,
    [BookingSavedInConfirmed] bit NULL,
    [EquipAddToConfirmed] bit NULL,
    [EquipEditConfirmed] bit NULL,
    [EquipDeleteConfirmed] bit NULL,
    [firstname] varchar(25) NULL,
    [surname] varchar(35) NULL,
    [Email] varchar(80) NULL,
    [Sendme_emails] char(1) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwGroupEmailsProducts')
BEGIN
CREATE TABLE [vwGroupEmailsProducts] (
    [GrpID] decimal(10,0) NOT NULL,
    [Group_code] varchar(30) NULL,
    [GroupID] decimal(10,0) NULL,
    [AegID] decimal(10,0) NOT NULL,
    [ContactID] decimal(10,0) NULL,
    [BookingSavedInConfirmed] bit NULL,
    [EquipAddToConfirmed] bit NULL,
    [EquipEditConfirmed] bit NULL,
    [EquipDeleteConfirmed] bit NULL,
    [firstname] varchar(25) NULL,
    [surname] varchar(35) NULL,
    [Email] varchar(80) NULL,
    [Sendme_emails] char(1) NULL,
    [product_code] char(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwGroupTotals')
BEGIN
CREATE TABLE [vwGroupTotals] (
    [Price] float NULL,
    [booking_no_v32] varchar(35) NULL,
    [product_code_v42] varchar(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwHeadingSubTotals')
BEGIN
CREATE TABLE [vwHeadingSubTotals] (
    [booking_no] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [DelvDate] datetime NULL,
    [Retndate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [days_using] int NULL,
    [HRental] float NULL,
    [HRental_discounted] float NULL,
    [HRentalDisc] float NULL,
    [HLabour] float NULL,
    [HSales] float NULL,
    [HSalesDiscAmount] float NULL,
    [HRentalDisc_perc] float NULL,
    [HSalesDisc_perc] float NULL,
    [HSundry] float NULL,
    [HRentalUndiscRentalAmount] float NULL,
    [HTax1] float NULL,
    [INCALL_RentalSalesSundryLabourBeforeDiscount] float NULL,
    [INCALL_RentalSalesSundryLabourAfterDiscount] float NULL,
    [HDiscountTotal] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwHistBookGrid')
BEGIN
CREATE TABLE [vwHistBookGrid] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [OrganizationV6] varchar(50) NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [booking_type_v32] tinyint NULL,
    [Trans_to_locn] int NULL,
    [order_no] varchar(25) NULL,
    [VenueRoom] varchar(35) NULL,
    [delivery_address_exist] char(1) NULL,
    [HorCCroom] int NULL,
    [InvoiceStage] int NULL,
    [invoiced] char(1) NULL,
    [invoice_no] decimal(19,0) NULL,
    [status] tinyint NULL,
    [BookingProgressStatus] tinyint NULL,
    [ExtendedFrom] varchar(5) NULL,
    [rDate] datetime NULL,
    [dDate] datetime NULL,
    [docs_produced] tinyint NULL,
    [event_code] char(30) NULL,
    [showName] varchar(50) NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [operatorsID] decimal(19,0) NULL,
    [Salesperson] varchar(30) NULL,
    [auth_agentv6] varchar(50) NULL,
    [address_l1V6] varchar(50) NULL,
    [address_l2V6] varchar(50) NULL,
    [address_l3V6] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [PostCode] varchar(12) NULL,
    [subrooms] char(12) NULL,
    [Agent_code] varchar(5) NOT NULL,
    [Price_Quoted] float NULL,
    [CSalesperson] char(30) NULL,
    [BkSName] varchar(45) NULL,
    [Salesperson_name] varchar(45) NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [ConDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [ProjectManagerName] varchar(45) NULL,
    [currencyStr] varchar(5) NULL,
    [VendorName] varchar(50) NULL,
    [CrewStatusColumn] tinyint NULL,
    [OriginalOperator] varchar(50) NULL,
    [bBookingIsComplete] bit NULL,
    [division] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwHistBookGrid_v2')
BEGIN
CREATE TABLE [vwHistBookGrid_v2] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [OrganizationV6] varchar(50) NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [booking_type_v32] tinyint NULL,
    [Trans_to_locn] int NULL,
    [order_no] varchar(25) NULL,
    [VenueRoom] varchar(35) NULL,
    [delivery_address_exist] char(1) NULL,
    [HorCCroom] int NULL,
    [InvoiceStage] int NULL,
    [invoiced] char(1) NULL,
    [invoice_no] decimal(19,0) NULL,
    [status] tinyint NULL,
    [BookingProgressStatus] tinyint NULL,
    [ExtendedFrom] varchar(5) NULL,
    [rDate] datetime NULL,
    [dDate] datetime NULL,
    [docs_produced] tinyint NULL,
    [event_code] char(30) NULL,
    [showName] varchar(50) NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [operatorsID] decimal(19,0) NULL,
    [Salesperson] varchar(30) NULL,
    [auth_agentv6] varchar(50) NULL,
    [address_l1V6] varchar(50) NULL,
    [address_l2V6] varchar(50) NULL,
    [address_l3V6] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [PostCode] varchar(12) NULL,
    [subrooms] char(12) NULL,
    [Agent_code] varchar(5) NOT NULL,
    [Price_Quoted] float NULL,
    [CSalesperson] char(30) NULL,
    [BkSName] varchar(45) NULL,
    [Salesperson_name] varchar(45) NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [ConDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [ProjectManagerName] varchar(45) NULL,
    [currencyStr] varchar(5) NULL,
    [VendorName] varchar(50) NULL,
    [CrewStatusColumn] tinyint NULL,
    [OriginalOperator] varchar(50) NULL,
    [bBookingIsComplete] bit NULL,
    [division] tinyint NULL,
    [Tax1] float NULL,
    [Tax2] float NULL,
    [LockedForScanning] bit NULL,
    [Reason] varchar(80) NOT NULL,
    [entrydate] datetime NULL,
    [eventtype] varchar(20) NULL,
    [bLocked] bit NULL,
    [STAGE_xml] nvarchar(MAX) NULL,
    [SyncType] tinyint NOT NULL,
    [ContractNo] varchar(18) NULL,
    [Customer_code] varchar(30) NULL,
    [PrintedPayTerm] varchar(40) NULL,
    [HasQT] bit NOT NULL,
    [HasDAT] bit NOT NULL,
    [ShowSdate] datetime NULL,
    [ShowStartTime] varchar(4) NULL,
    [ShowEDate] datetime NULL,
    [ShowEndTime] varchar(4) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwHistCrewItems')
BEGIN
CREATE TABLE [vwHistCrewItems] (
    [ID] decimal(10,0) NOT NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [trans_qty] int NULL,
    [bit_field_v41] tinyint NULL,
    [price] float NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [days_using] float NULL,
    [GroupSeqNo] int NULL,
    [descriptionV6] varchar(50) NULL,
    [product_type_v41] tinyint NULL,
    [EnforceMinHours] bit NULL,
    [MinimumHours] float NULL,
    [person] char(30) NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [task] tinyint NULL,
    [person_required] char(1) NULL,
    [unitRate] float NULL,
    [TechRate] float NULL,
    [techrateIsHourorDay] char(1) NULL,
    [TechPay] float NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [booking_no_v32] varchar(35) NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [AssignTo] varchar(35) NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [SubrentalLinkID] int NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [PPONumber] decimal(19,0) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [Notes] varchar(512) NULL,
    [AdmModifiedNoteDate] datetime NULL,
    [JobTimeZone] tinyint NULL,
    [TechTimezone] tinyint NULL,
    [JobOffered] bit NULL,
    [JobOffereddate] datetime NULL,
    [JobAccepted] bit NULL,
    [JobAcceptedDate] datetime NULL,
    [Conflict] bit NULL,
    [PrintOnInvoice] bit NULL,
    [PrintOnQuote] bit NULL,
    [JobTechOfferDate] datetime NULL,
    [JobTechOfferStatus] tinyint NULL,
    [JobTechNotes] varchar(512) NULL,
    [CrewClientNotes] varchar(40) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwInsuranceCoverage')
BEGIN
CREATE TABLE [vwInsuranceCoverage] (
    [Customer_code] varchar(30) NULL,
    [OrganisationV6] varchar(50) NULL,
    [InsuredAmount] float NULL,
    [CostPrice] float NULL,
    [RetailPrice] float NULL,
    [WholesalePrice] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwInsuredBookings')
BEGIN
CREATE TABLE [vwInsuredBookings] (
    [booking_no] varchar(35) NULL,
    [OrganizationV6] varchar(50) NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [status] tinyint NULL,
    [CostPrice] float NULL,
    [retailPrice] float NULL,
    [WholeSalePrice] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwInvCommentItems')
BEGIN
CREATE TABLE [vwInvCommentItems] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [trans_type_v41] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [bit_field_v41] tinyint NULL,
    [TimeBookedH] tinyint NULL,
    [TimeBookedM] tinyint NULL,
    [TimeBookedS] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [techRateorDaysCharged] float NULL,
    [unitRate] float NULL,
    [prep_on] char(1) NULL,
    [Comment_desc_v42] char(70) NULL,
    [AssignTo] varchar(255) NULL,
    [QtyReserved] int NULL,
    [AddedAtCheckout] bit NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [BookDate] datetime NULL,
    [PDate] datetime NULL,
    [DayWeekRate] float NULL,
    [TechPay] float NULL,
    [GroupSeqNo] int NULL,
    [SubRentalLinkID] int NOT NULL,
    [AssignType] tinyint NOT NULL,
    [QtyShort] int NOT NULL,
    [QtyAvailable] int NULL,
    [BeforeDiscountAmount] float NULL,
    [PackageLevel] smallint NULL,
    [QuickTurnaroundQty] int NULL,
    [InRack] bit NULL,
    [CostPrice] float NULL,
    [NodeCollapsed] bit NULL,
    [resolvedDiscrep] bit NOT NULL,
    [iseq_no] decimal(19,0) NULL,
    [descriptionV6] varchar(50) NULL,
    [product_type_v41] tinyint NULL,
    [retail_price] float NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [cost_price] float NULL,
    [unit_weight] float NULL,
    [unit_volume] float NULL,
    [prodRoadCase] tinyint NULL,
    [DisallowDisc] char(1) NULL,
    [product_Config] tinyint NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [isGenericItem] char(1) NULL,
    [UseWeeklyRate] char(1) NULL,
    [Indiv_hire_sale] char(1) NULL,
    [on_hand] float NULL,
    [DisallowTransfer] bit NULL,
    [cyTurnCosts] money NULL,
    [bDisallowRegionTransfer] bit NULL,
    [IsInTrashCan] char(1) NULL,
    [LastUpdate] datetime NULL,
    [OLInternalDesc] varchar(50) NULL,
    [asset_track] char(1) NULL,
    [components_quote] char(1) NULL,
    [WarehouseActive] bit NULL,
    [bCustomPrintouts] bit NULL,
    [OverridePriceChangeRestriction] smallint NULL,
    [BasedOnPurchCost] float NULL,
    [MfctPartNumber] varchar(30) NULL,
    [bProductIsFreight] bit NULL,
    [revenue_code] varchar(6) NULL,
    [PrintedDesc] varchar(50) NULL,
    [DisplayColour] varchar(7) NULL,
    [DisplayBold] char(1) NULL,
    [Undisc_amt] float NULL,
    [View_Logi] bit NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [parentCode] varchar(30) NULL,
    [EstSubRentalCost] float NULL,
    [EstSubRentalDays] smallint NULL,
    [VendorID] int NULL,
    [Notes] varchar(MAX) NULL,
    [UseEstSubHireOverride] bit NULL,
    [QTSource] tinyint NULL,
    [QTBookingNo] varchar(35) NULL,
    [POPrefix] varchar(3) NOT NULL,
    [CrossRental] char(1) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwInventoryItems')
BEGIN
CREATE TABLE [vwInventoryItems] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [trans_type_v41] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [bit_field_v41] tinyint NULL,
    [TimeBookedH] tinyint NULL,
    [TimeBookedM] tinyint NULL,
    [TimeBookedS] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [techRateorDaysCharged] float NULL,
    [unitRate] float NULL,
    [prep_on] char(1) NULL,
    [Comment_desc_v42] char(70) NULL,
    [AssignTo] varchar(255) NULL,
    [QtyReserved] int NULL,
    [AddedAtCheckout] bit NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [BookDate] datetime NULL,
    [PDate] datetime NULL,
    [DayWeekRate] float NULL,
    [TechPay] float NULL,
    [GroupSeqNo] int NULL,
    [SubRentalLinkID] int NOT NULL,
    [AssignType] tinyint NOT NULL,
    [QtyShort] int NOT NULL,
    [QtyAvailable] int NULL,
    [BeforeDiscountAmount] float NULL,
    [PackageLevel] smallint NULL,
    [QuickTurnaroundQty] int NULL,
    [InRack] bit NULL,
    [CostPrice] float NULL,
    [NodeCollapsed] bit NULL,
    [resolvedDiscrep] bit NOT NULL,
    [InvID] decimal(10,0) NOT NULL,
    [iseq_no] decimal(19,0) NULL,
    [descriptionV6] varchar(50) NULL,
    [product_type_v41] tinyint NULL,
    [retail_price] float NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [cost_price] float NULL,
    [unit_weight] float NULL,
    [unit_volume] float NULL,
    [prodRoadCase] tinyint NULL,
    [DisallowDisc] char(1) NULL,
    [product_Config] tinyint NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [isGenericItem] char(1) NULL,
    [UseWeeklyRate] char(1) NULL,
    [Indiv_hire_sale] char(1) NULL,
    [on_hand] float NULL,
    [DisallowTransfer] bit NULL,
    [cyTurnCosts] money NOT NULL,
    [bDisallowRegionTransfer] bit NULL,
    [IsInTrashCan] char(1) NULL,
    [LastUpdate] datetime NULL,
    [OLInternalDesc] varchar(50) NULL,
    [asset_track] char(1) NULL,
    [components_quote] char(1) NULL,
    [WarehouseActive] bit NULL,
    [bCustomPrintouts] bit NOT NULL,
    [OverridePriceChangeRestriction] smallint NOT NULL,
    [BasedOnPurchCost] float NOT NULL,
    [MfctPartNumber] varchar(30) NULL,
    [bProductIsFreight] bit NOT NULL,
    [revenue_code] varchar(6) NULL,
    [PrintedDesc] varchar(50) NULL,
    [DisplayColour] varchar(7) NULL,
    [DisplayBold] char(1) NULL,
    [Undisc_amt] float NULL,
    [View_Logi] bit NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [parentCode] varchar(30) NULL,
    [EstSubRentalCost] float NULL,
    [EstSubRentalDays] smallint NULL,
    [VendorID] int NULL,
    [Notes] varchar(MAX) NULL,
    [UseEstSubHireOverride] bit NULL,
    [QTSource] tinyint NULL,
    [QTBookingNo] varchar(35) NULL,
    [POPrefix] varchar(3) NOT NULL,
    [CrossRental] char(1) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwItemAndHist')
BEGIN
CREATE TABLE [vwItemAndHist] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [trans_type_v41] tinyint NULL,
    [product_code_v42] char(30) NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [From_locn] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [bit_field_v41] tinyint NULL,
    [TimeBookedH] tinyint NULL,
    [TimeBookedM] tinyint NULL,
    [TimeBookedS] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [techRateorDaysCharged] float NULL,
    [unitRate] float NULL,
    [prep_on] char(1) NULL,
    [Comment_desc_v42] char(70) NULL,
    [GroupSeqNo] int NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [BookDate] datetime NULL,
    [PDate] datetime NULL,
    [DayWeekRate] float NULL,
    [TechPay] float NULL,
    [AssignTo] varchar(255) NULL,
    [QtyReserved] int NULL,
    [AddedAtCheckout] bit NULL,
    [AssignType] tinyint NOT NULL,
    [QtyShort] int NOT NULL,
    [SubRentalLinkID] int NOT NULL,
    [BeforeDiscountAmount] float NULL,
    [CostPrice] float NULL,
    [Undisc_amt] float NULL,
    [view_client] bit NULL,
    [view_logi] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwLoadSubRental')
BEGIN
CREATE TABLE [vwLoadSubRental] (
    [ID] decimal(10,0) NOT NULL,
    [SubRentalLinkID] int NOT NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [del_time_hour] int NULL,
    [del_time_min] int NULL,
    [return_time_hour] int NULL,
    [return_time_min] int NULL,
    [techRateorDaysCharged] float NULL,
    [product_code_v42] varchar(30) NULL,
    [Comment_desc_v42] varchar(70) NULL,
    [trans_qty] float NULL,
    [unitRate] float NULL,
    [TechPay] float NULL,
    [price] float NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [item_type] int NULL,
    [DayWeekRate] float NULL,
    [trans_type_v41] int NULL,
    [sundry_cost] float NULL,
    [sundry_desc] varchar(50) NULL,
    [sundry_price] float NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [booking_no_v32] varchar(35) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [BookDate] datetime NULL,
    [TimeBookedH] int NULL,
    [TimeBookedM] int NULL,
    [TimeBookedS] int NULL,
    [InRack] int NULL,
    [heading_desc] varchar(79) NULL,
    [PackageLevel] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwLoadSubRental_v2')
BEGIN
CREATE TABLE [vwLoadSubRental_v2] (
    [ID] decimal(10,0) NOT NULL,
    [SubRentalLinkID] int NOT NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [del_time_hour] int NULL,
    [del_time_min] int NULL,
    [return_time_hour] int NULL,
    [return_time_min] int NULL,
    [techrateordayscharged] float NULL,
    [product_code_v42] varchar(30) NULL,
    [comment_desc_v42] varchar(70) NULL,
    [trans_qty] float NULL,
    [unitrate] float NULL,
    [TechPay] float NULL,
    [Price] float NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [item_type] int NULL,
    [Dayweekrate] float NULL,
    [trans_type_v41] int NULL,
    [sundry_cost] float NULL,
    [sundry_desc] varchar(50) NULL,
    [sundry_price] float NULL,
    [heading_no] tinyint NULL,
    [groupseqno] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [booking_no_v32] varchar(35) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [BookDate] datetime NULL,
    [TimeBookedH] int NULL,
    [TimeBookedM] int NULL,
    [TimeBookedS] int NULL,
    [InRack] int NULL,
    [RevenueCode] varchar(50) NULL,
    [bit_field_v41] int NULL,
    [PackageLevel] int NULL,
    [DontPrintOnCrossRentPO] int NULL,
    [SundryType] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwLoadSubRentalHist')
BEGIN
CREATE TABLE [vwLoadSubRentalHist] (
    [ID] decimal(10,0) NOT NULL,
    [SubRentalLinkID] int NOT NULL,
    [FirstDate] datetime NULL,
    [RetnDate] datetime NULL,
    [del_time_hour] int NULL,
    [del_time_min] int NULL,
    [return_time_hour] int NULL,
    [return_time_min] int NULL,
    [techRateorDaysCharged] float NULL,
    [product_code_v42] varchar(30) NULL,
    [Comment_desc_v42] varchar(70) NULL,
    [trans_qty] decimal(19,0) NULL,
    [unitRate] float NULL,
    [TechPay] float NULL,
    [price] float NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [item_type] int NULL,
    [DayWeekRate] float NULL,
    [trans_type_v41] int NULL,
    [sundry_cost] float NULL,
    [sundry_desc] varchar(50) NULL,
    [sundry_price] float NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [booking_no_v32] varchar(35) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [BookDate] datetime NULL,
    [TimeBookedH] int NULL,
    [TimeBookedM] int NULL,
    [TimeBookedS] int NULL,
    [InRack] int NULL,
    [heading_desc] varchar(79) NULL,
    [PackageLevel] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwLoadTruckSchedule')
BEGIN
CREATE TABLE [vwLoadTruckSchedule] (
    [ID] decimal(10,0) NOT NULL,
    [BookingID] decimal(10,0) NOT NULL,
    [booking_type_v32] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [showStartTime] varchar(4) NULL,
    [From_locn] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [VenueRoom] varchar(35) NULL,
    [Field1] varchar(25) NULL,
    [delivery_viav71] int NULL,
    [pickup_viaV71] int NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [showName] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [Salesperson] varchar(30) NULL,
    [BookingProgressStatus] tinyint NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [status] tinyint NULL,
    [SDate] datetime NULL,
    [ADelDate] datetime NULL,
    [PickupRetDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [booking_no] varchar(35) NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [Auth_agentv6] varchar(50) NULL,
    [address_l1V6] varchar(50) NULL,
    [address_l2V6] varchar(50) NULL,
    [address_l3V6] varchar(50) NULL,
    [event_desc] char(32) NULL,
    [OrganisationV6] varchar(50) NULL,
    [Cadr1] char(50) NULL,
    [Cadr2] char(50) NULL,
    [Cadr3] char(50) NULL,
    [StreetState] varchar(50) NULL,
    [StreetCountry] varchar(50) NULL,
    [Vadr1] varchar(50) NULL,
    [Vadr2] varchar(50) NULL,
    [Vadr3] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [Vfax] varchar(16) NULL,
    [VCntryCode] varchar(10) NULL,
    [VArCode] varchar(10) NULL,
    [Vphone1] varchar(16) NULL,
    [subrooms] char(12) NULL,
    [HorCCroom] int NULL,
    [VendorName] varchar(50) NULL,
    [bookingPrinted] char(1) NULL,
    [pickup_time] char(6) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [delivery_time] char(6) NULL,
    [ShowEdate] datetime NULL,
    [ShowSdate] datetime NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [Priority] int NULL,
    [P1CC] varchar(10) NULL,
    [P1AC] varchar(10) NULL,
    [P1Digits] varchar(16) NULL,
    [P1Ext] varchar(8) NULL,
    [P2CC] varchar(10) NULL,
    [P2AC] varchar(10) NULL,
    [P2Digits] varchar(16) NULL,
    [P2Ext] varchar(8) NULL,
    [FaxCC] varchar(10) NULL,
    [FaxAC] varchar(10) NULL,
    [AFaxDigits] varchar(16) NULL,
    [AState] varchar(50) NULL,
    [ACountry] varchar(50) NULL,
    [dtExpected_ReturnDate] datetime NOT NULL,
    [vcExpected_ReturnTime] varchar(4) NOT NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [Phone2Ext] varchar(8) NULL,
    [VendPhone1ID] decimal(18,0) NULL,
    [VendPhone2ID] decimal(18,0) NULL,
    [VendFAxID] decimal(18,0) NULL,
    [EquipmentModified] bit NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [Direction] tinyint NULL,
    [TruckNo] int NULL,
    [TripNo] tinyint NULL,
    [TripDate] datetime NULL,
    [TripID] decimal(10,0) NULL,
    [BookingLoadSequence] int NULL,
    [HeadingNumber] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwLocnQtyV2')
BEGIN
CREATE TABLE [vwLocnQtyV2] (
    [Product_code] char(30) NULL,
    [Locn] int NULL,
    [Qty] float NULL,
    [QtyInRack] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwMinPOAmount')
BEGIN
CREATE TABLE [vwMinPOAmount] (
    [PPONumber] decimal(19,0) NULL,
    [POPrefix] varchar(3) NULL,
    [PtotalAmount] float NULL,
    [CrossRental] char(1) NULL,
    [VendorName] varchar(50) NULL,
    [MinPOAmount] float NULL,
    [Remaining] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwOpenBooking_v2')
BEGIN
CREATE TABLE [vwOpenBooking_v2] (
    [id] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viav71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_namev6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [from_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setuptimev61] varchar(4) NULL,
    [rehearsaltime] varchar(4) NULL,
    [striketime] varchar(4) NULL,
    [trans_to_locn] int NULL,
    [showstarttime] varchar(4) NULL,
    [showendtime] varchar(4) NULL,
    [transferno] decimal(19,0) NULL,
    [currencystr] varchar(5) NULL,
    [bookingprogressstatus] tinyint NULL,
    [confirmedby] varchar(35) NULL,
    [confirmeddocref] varchar(50) NULL,
    [venueroom] varchar(35) NULL,
    [expattendees] int NULL,
    [hourbooked] tinyint NULL,
    [minbooked] tinyint NULL,
    [secbooked] tinyint NULL,
    [taxauthority1] int NULL,
    [taxauthority2] int NULL,
    [horccroom] int NULL,
    [subrooms] char(12) NULL,
    [truckout] int NULL,
    [truckin] int NULL,
    [tripout] tinyint NULL,
    [tripin] tinyint NULL,
    [showname] varchar(50) NULL,
    [freightservicedel] tinyint NULL,
    [freightserviceret] tinyint NULL,
    [delzone] int NULL,
    [retzone] int NULL,
    [ournumberdel] char(1) NULL,
    [ournumberret] char(1) NULL,
    [datesandtimesenabled] char(1) NULL,
    [paymethod] varchar(25) NULL,
    [government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [contactloadedintovenue] tinyint NULL,
    [assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [organizationv6] varchar(50) NULL,
    [salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [ddate] datetime NULL,
    [rdate] datetime NULL,
    [inv_date] datetime NULL,
    [showsdate] datetime NULL,
    [showedate] datetime NULL,
    [setdate] datetime NULL,
    [adeldate] datetime NULL,
    [sdate] datetime NULL,
    [rehdate] datetime NULL,
    [condate] datetime NULL,
    [toutdate] datetime NULL,
    [tindate] datetime NULL,
    [predate] datetime NULL,
    [conbydate] datetime NULL,
    [bookingprinted] char(1) NULL,
    [custcode] varchar(30) NULL,
    [extendedfrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsid] decimal(19,0) NULL,
    [potpercent] float NULL,
    [referral] varchar(50) NULL,
    [eventtype] varchar(20) NULL,
    [priority] int NULL,
    [invoicestage] int NULL,
    [creditcardname] varchar(20) NULL,
    [creditcardnumber] varchar(250) NULL,
    [expmonth] varchar(250) NULL,
    [expyear] varchar(250) NULL,
    [cardholder] varchar(250) NULL,
    [cardstreet1] varchar(250) NULL,
    [cardstreet2] varchar(250) NULL,
    [cardcity] varchar(250) NULL,
    [cardstate] varchar(250) NULL,
    [cardpostcode] varchar(250) NULL,
    [creditcardidno] varchar(250) NULL,
    [pickupretdate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [maxbookingvalue] float NULL,
    [usespricetable] int NULL,
    [datetoinvoice] datetime NULL,
    [twowkdisc] float NULL,
    [threewkdisc] float NULL,
    [servcont] char(1) NULL,
    [paymentoptions] tinyint NULL,
    [printedpayterm] varchar(40) NULL,
    [rentaltype] tinyint NULL,
    [usebillschedule] char(1) NULL,
    [tax2] float NULL,
    [contactid] decimal(9,0) NULL,
    [shorthours] tinyint NULL,
    [projectmanager] varchar(8) NOT NULL,
    [dtexpected_returndate] datetime NOT NULL,
    [vcexpected_returntime] varchar(4) NOT NULL,
    [vctruckouttime] varchar(4) NOT NULL,
    [vctruckintime] varchar(4) NOT NULL,
    [custid] decimal(10,0) NOT NULL,
    [venueid] int NOT NULL,
    [latechargesapplied] bit NOT NULL,
    [shortagesaretransfered] bit NOT NULL,
    [venuecontactid] int NULL,
    [venuecontact] varchar(50) NULL,
    [venuecontactphoneid] int NULL,
    [ltbillingoption] tinyint NULL,
    [collection] float NULL,
    [fuelsurchargerate] float NULL,
    [freightlocked] bit NULL,
    [labourlocked] bit NULL,
    [rentallocked] bit NULL,
    [pricelocked] bit NULL,
    [insurance_type] tinyint NULL,
    [entrydate] datetime NULL,
    [creditsurchargerate] float NULL,
    [creditsurchargeamount] float NULL,
    [disabletreeorder] bit NULL,
    [loaddatetime] datetime NULL,
    [unloaddatetime] datetime NULL,
    [deprepdatetime] datetime NULL,
    [deprepon] bit NOT NULL,
    [deliverydateon] bit NOT NULL,
    [pickupdateon] bit NOT NULL,
    [scheduledateson] varchar(10) NULL,
    [confirmationfinancials] varchar(30) NULL,
    [eventmanagementrate] float NULL,
    [eventmanagementamount] float NULL,
    [equipmentmodified] bit NULL,
    [crewstatuscolumn] tinyint NULL,
    [bbookingiscomplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingMethod] tinyint NULL,
    [MasterBillingID] int NULL,
    [schedHeadEquipSpan] tinyint NULL,
    [TaxabPCT] float NOT NULL,
    [UntaxPCT] float NOT NULL,
    [Tax1PCT] float NOT NULL,
    [Tax2PCT] float NOT NULL,
    [PaymentContactID] int NULL,
    [LockedForScanning] bit NULL,
    [OldAssignedTo] varchar(35) NULL,
    [DateLastModified] datetime NULL,
    [crew_cnt] int NOT NULL,
    [rTargetMargin] float NOT NULL,
    [rProfitMargin] float NOT NULL,
    [ContractNo] varchar(18) NULL,
    [AllLocnAvail] bit NULL,
    [vvenuename] varchar(50) NULL,
    [vcontactname] varchar(50) NULL,
    [vcontactid] decimal(10,0) NULL,
    [address1] varchar(50) NULL,
    [address2] varchar(50) NULL,
    [city] varchar(50) NULL,
    [state] varchar(50) NULL,
    [country] varchar(50) NULL,
    [zipcode] varchar(12) NULL,
    [venuetype] tinyint NULL,
    [vp1cc] varchar(10) NULL,
    [vp1ac] varchar(10) NULL,
    [vp1number] varchar(16) NULL,
    [vp1extension] varchar(8) NULL,
    [vp2cc] varchar(10) NULL,
    [vp2ac] varchar(10) NULL,
    [vp2number] varchar(16) NULL,
    [vp2extension] varchar(8) NULL,
    [vp3cc] varchar(10) NULL,
    [vp3ac] varchar(10) NULL,
    [vp3number] varchar(16) NULL,
    [defaultfolder] varchar(255) NULL,
    [webpage] varchar(80) NULL,
    [cid] decimal(10,0) NOT NULL,
    [customer_code] varchar(30) NULL,
    [postaladdress1] char(50) NULL,
    [postaladdress2] char(50) NULL,
    [postaladdress3] char(50) NULL,
    [postalpostcode] char(12) NULL,
    [ccurrencystr] varchar(5) NULL,
    [usespricetablev71] tinyint NULL,
    [post_code] char(12) NULL,
    [csales_tax_no] char(25) NULL,
    [account_type] tinyint NULL,
    [industry_type] varchar(8) NULL,
    [defaultinsurance_type] tinyint NULL,
    [hire_tax_exempt] char(1) NULL,
    [price_customer_pays] tinyint NULL,
    [customer_number] char(6) NULL,
    [stop_credit] tinyint NULL,
    [last_bk_seq] varchar(5) NULL,
    [credit_limit] float NULL,
    [CURRENT] float NULL,
    [seven_days] float NULL,
    [fourteen_days] float NULL,
    [twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [lastpmtdate] datetime NULL,
    [last_pmt_amt] float NULL,
    [monthly_cycle_billing_basis] tinyint NULL,
    [csalesperson] char(30) NULL,
    [contactv6] varchar(35) NULL,
    [organisationv6] varchar(50) NULL,
    [address_l1v6] char(50) NULL,
    [address_l2v6] char(50) NULL,
    [address_l3v6] char(50) NULL,
    [webaddress] varchar(80) NULL,
    [emailaddress] varchar(80) NULL,
    [cpaymethod] varchar(16) NULL,
    [lastbalupdate] datetime NULL,
    [custcdate] datetime NULL,
    [streetstate] varchar(50) NULL,
    [streetcountry] varchar(50) NULL,
    [postalstate] varchar(50) NULL,
    [postalcountry] varchar(50) NULL,
    [insuredfromdate] datetime NULL,
    [insuredtodate] datetime NULL,
    [field1] varchar(25) NULL,
    [bponumrequired] bit NULL,
    [faxcalltype] tinyint NULL,
    [faxdialareacode] bit NULL,
    [faxcountrycode] varchar(10) NULL,
    [faxareacode] varchar(10) NULL,
    [faxdigits] varchar(16) NULL,
    [phone1countrycode] varchar(10) NULL,
    [phone1areacode] varchar(10) NULL,
    [phone1digits] varchar(16) NULL,
    [phone1ext] varchar(8) NULL,
    [phone2countrycode] varchar(10) NULL,
    [phone2areacode] varchar(10) NULL,
    [phone2digits] varchar(16) NULL,
    [phone2ext] varchar(8) NULL,
    [campaignid] int NULL,
    [ilink_contactid] decimal(10,0) NULL,
    [CustomerDiscountRate] float NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [CustomerPaymentContactID] int NULL,
    [firstinvdate] datetime NULL,
    [AcctMgr] varchar(30) NULL,
    [CustomerType] tinyint NULL,
    [isVendor] bit NULL,
    [AREmailAddress] varchar(80) NULL,
    [DisabledCust] char(1) NULL,
    [inst_instru] text NULL,
    [bksname] varchar(45) NULL,
    [salesperson_name] varchar(45) NULL,
    [projectmanagername] varchar(45) NULL,
    [event_desc] char(32) NULL,
    [sale_of_asset_undisc_amt] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwOpenBookingRPMVC')
BEGIN
CREATE TABLE [vwOpenBookingRPMVC] (
    [id] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viav71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_namev6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [from_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setuptimev61] varchar(4) NULL,
    [rehearsaltime] varchar(4) NULL,
    [striketime] varchar(4) NULL,
    [trans_to_locn] int NULL,
    [showstarttime] varchar(4) NULL,
    [showendtime] varchar(4) NULL,
    [transferno] decimal(19,0) NULL,
    [currencystr] varchar(5) NULL,
    [bookingprogressstatus] tinyint NULL,
    [confirmedby] varchar(35) NULL,
    [confirmeddocref] varchar(50) NULL,
    [venueroom] varchar(35) NULL,
    [expattendees] int NULL,
    [hourbooked] tinyint NULL,
    [minbooked] tinyint NULL,
    [secbooked] tinyint NULL,
    [taxauthority1] int NULL,
    [taxauthority2] int NULL,
    [horccroom] int NULL,
    [subrooms] char(12) NULL,
    [truckout] int NULL,
    [truckin] int NULL,
    [tripout] tinyint NULL,
    [tripin] tinyint NULL,
    [showname] varchar(50) NULL,
    [freightservicedel] tinyint NULL,
    [freightserviceret] tinyint NULL,
    [delzone] int NULL,
    [retzone] int NULL,
    [ournumberdel] char(1) NULL,
    [ournumberret] char(1) NULL,
    [datesandtimesenabled] char(1) NULL,
    [paymethod] varchar(25) NULL,
    [government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [contactloadedintovenue] tinyint NULL,
    [assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [organizationv6] varchar(50) NULL,
    [salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [ddate] datetime NULL,
    [rdate] datetime NULL,
    [inv_date] datetime NULL,
    [showsdate] datetime NULL,
    [showedate] datetime NULL,
    [setdate] datetime NULL,
    [adeldate] datetime NULL,
    [sdate] datetime NULL,
    [rehdate] datetime NULL,
    [condate] datetime NULL,
    [toutdate] datetime NULL,
    [tindate] datetime NULL,
    [predate] datetime NULL,
    [conbydate] datetime NULL,
    [bookingprinted] char(1) NULL,
    [custcode] varchar(30) NULL,
    [extendedfrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsid] decimal(19,0) NULL,
    [potpercent] float NULL,
    [referral] varchar(50) NULL,
    [eventtype] varchar(20) NULL,
    [priority] int NULL,
    [invoicestage] int NULL,
    [creditcardname] varchar(20) NULL,
    [creditcardnumber] varchar(250) NULL,
    [expmonth] varchar(250) NULL,
    [expyear] varchar(250) NULL,
    [cardholder] varchar(250) NULL,
    [cardstreet1] varchar(250) NULL,
    [cardstreet2] varchar(250) NULL,
    [cardcity] varchar(250) NULL,
    [cardstate] varchar(250) NULL,
    [cardpostcode] varchar(250) NULL,
    [creditcardidno] varchar(250) NULL,
    [pickupretdate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [maxbookingvalue] float NULL,
    [usespricetable] int NULL,
    [datetoinvoice] datetime NULL,
    [twowkdisc] float NULL,
    [threewkdisc] float NULL,
    [servcont] char(1) NULL,
    [paymentoptions] tinyint NULL,
    [printedpayterm] varchar(40) NULL,
    [rentaltype] tinyint NULL,
    [usebillschedule] char(1) NULL,
    [tax2] float NULL,
    [contactid] decimal(9,0) NULL,
    [shorthours] tinyint NULL,
    [projectmanager] varchar(8) NOT NULL,
    [dtexpected_returndate] datetime NOT NULL,
    [vcexpected_returntime] varchar(4) NOT NULL,
    [vctruckouttime] varchar(4) NOT NULL,
    [vctruckintime] varchar(4) NOT NULL,
    [custid] decimal(10,0) NOT NULL,
    [venueid] int NOT NULL,
    [latechargesapplied] bit NOT NULL,
    [shortagesaretransfered] bit NOT NULL,
    [venuecontactid] int NULL,
    [venuecontact] varchar(50) NULL,
    [venuecontactphoneid] int NULL,
    [ltbillingoption] tinyint NULL,
    [collection] float NULL,
    [fuelsurchargerate] float NULL,
    [freightlocked] bit NULL,
    [labourlocked] bit NULL,
    [rentallocked] bit NULL,
    [pricelocked] bit NULL,
    [insurance_type] tinyint NULL,
    [entrydate] datetime NULL,
    [creditsurchargerate] float NULL,
    [creditsurchargeamount] float NULL,
    [disabletreeorder] bit NULL,
    [loaddatetime] datetime NULL,
    [unloaddatetime] datetime NULL,
    [deprepdatetime] datetime NULL,
    [deprepon] bit NOT NULL,
    [deliverydateon] bit NOT NULL,
    [pickupdateon] bit NOT NULL,
    [scheduledateson] varchar(10) NULL,
    [confirmationfinancials] varchar(30) NULL,
    [eventmanagementrate] float NULL,
    [eventmanagementamount] float NULL,
    [equipmentmodified] bit NULL,
    [crewstatuscolumn] tinyint NULL,
    [bbookingiscomplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingMethod] tinyint NULL,
    [MasterBillingID] int NULL,
    [schedHeadEquipSpan] tinyint NULL,
    [TaxabPCT] float NOT NULL,
    [UntaxPCT] float NOT NULL,
    [Tax1PCT] float NOT NULL,
    [Tax2PCT] float NOT NULL,
    [PaymentContactID] int NULL,
    [LockedForScanning] bit NULL,
    [OldAssignedTo] varchar(35) NULL,
    [DateLastModified] datetime NULL,
    [crew_cnt] int NOT NULL,
    [rTargetMargin] float NOT NULL,
    [rProfitMargin] float NOT NULL,
    [ContractNo] varchar(18) NULL,
    [AllLocnAvail] bit NULL,
    [AllHeadingsDaysOverride] bit NULL,
    [printedDate] datetime NULL,
    [cid] decimal(10,0) NOT NULL,
    [customer_code] varchar(30) NULL,
    [ccurrencystr] varchar(5) NULL,
    [usespricetablev71] tinyint NULL,
    [csales_tax_no] char(25) NULL,
    [account_type] tinyint NULL,
    [industry_type] varchar(8) NULL,
    [defaultinsurance_type] tinyint NULL,
    [hire_tax_exempt] char(1) NULL,
    [price_customer_pays] tinyint NULL,
    [customer_number] char(6) NULL,
    [stop_credit] tinyint NULL,
    [last_bk_seq] varchar(5) NULL,
    [credit_limit] float NULL,
    [CURRENT] float NULL,
    [seven_days] float NULL,
    [fourteen_days] float NULL,
    [twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [lastpmtdate] datetime NULL,
    [last_pmt_amt] float NULL,
    [csalesperson] char(30) NULL,
    [contactv6] varchar(35) NULL,
    [organisationv6] varchar(50) NULL,
    [cpaymethod] varchar(16) NULL,
    [lastbalupdate] datetime NULL,
    [custcdate] datetime NULL,
    [insuredfromdate] datetime NULL,
    [insuredtodate] datetime NULL,
    [bponumrequired] bit NULL,
    [campaignid] int NULL,
    [ilink_contactid] decimal(10,0) NULL,
    [CustomerDiscountRate] float NULL,
    [CustomerPaymentContactID] int NULL,
    [firstinvdate] datetime NULL,
    [AcctMgr] varchar(30) NULL,
    [CustomerType] tinyint NULL,
    [StreetState] varchar(50) NULL,
    [bksname] varchar(45) NULL,
    [salesperson_name] varchar(45) NULL,
    [projectmanagername] varchar(45) NULL,
    [event_desc] char(32) NULL,
    [sale_of_asset_undisc_amt] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwOrphSubrentals')
BEGIN
CREATE TABLE [vwOrphSubrentals] (
    [BookingNo] varchar(35) NULL,
    [SubrentalNo] varchar(35) NULL,
    [ProdCode] char(30) NULL,
    [SubrentalQty] decimal(38,0) NULL,
    [BookingQty] decimal(38,0) NOT NULL,
    [From_locn] int NULL,
    [BookingType] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPlotCrewActivities')
BEGIN
CREATE TABLE [vwPlotCrewActivities] (
    [ID] decimal(10,0) NOT NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] varchar(2) NULL,
    [del_time_min] varchar(2) NULL,
    [From_locn] int NOT NULL,
    [return_time_hour] varchar(2) NULL,
    [return_time_min] varchar(2) NULL,
    [Trans_to_locn] int NOT NULL,
    [return_to_locn] int NOT NULL,
    [trans_qty] int NOT NULL,
    [person] char(30) NULL,
    [RetnDate] datetime NULL,
    [booking_no_v32] varchar(8) NOT NULL,
    [Booking_type_v32] int NOT NULL,
    [BookingProgressStatus] int NOT NULL,
    [showName] varchar(8) NOT NULL,
    [bit_field_v41] int NOT NULL,
    [Description] varchar(50) NULL,
    [AgencyContact] bit NULL,
    [CustomActivityType] decimal(19,0) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPlotCrewItems')
BEGIN
CREATE TABLE [vwPlotCrewItems] (
    [ID] decimal(10,0) NOT NULL,
    [FirstDate] datetime NULL,
    [Del_time_hour] tinyint NULL,
    [Del_time_min] tinyint NULL,
    [From_locn] int NULL,
    [Return_time_hour] tinyint NULL,
    [Return_time_min] tinyint NULL,
    [Trans_to_locn] int NULL,
    [Return_to_locn] int NULL,
    [Trans_qty] int NULL,
    [Person] char(30) NULL,
    [RetnDate] datetime NULL,
    [booking_no_v32] varchar(35) NULL,
    [Booking_type_v32] int NULL,
    [BookingProgressStatus] tinyint NULL,
    [Showname] varchar(50) NULL,
    [Bit_field_v41] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPlotCrewItems_v2')
BEGIN
CREATE TABLE [vwPlotCrewItems_v2] (
    [ID] decimal(10,0) NOT NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [From_locn] int NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [trans_qty] int NULL,
    [person] char(30) NULL,
    [RetnDate] datetime NULL,
    [booking_no_v32] varchar(35) NULL,
    [Booking_type_v32] int NULL,
    [BookingProgressStatus] int NULL,
    [showName] varchar(50) NULL,
    [bit_field_v41] int NULL,
    [Description] varchar(50) NULL,
    [AgencyContact] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPlotItems')
BEGIN
CREATE TABLE [vwPlotItems] (
    [ID] decimal(10,0) NOT NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] int NULL,
    [del_time_min] int NULL,
    [From_locn] int NULL,
    [trans_type_v41] int NULL,
    [return_time_min] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] int NULL,
    [booking_no_v32] varchar(35) NULL,
    [trans_qty] decimal(19,0) NULL,
    [prep_on] varchar(1) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [bit_field_v41] int NULL,
    [PreDate] datetime NULL,
    [prep_time_h] int NULL,
    [prep_time_m] int NULL,
    [BookingProgressStatus] int NULL,
    [Product_code_v42] char(30) NULL,
    [BookDate] datetime NULL,
    [TimeBookedH] int NULL,
    [TimeBookedM] int NULL,
    [TimeBookedS] int NULL,
    [DeprepOn] int NOT NULL,
    [DeprepDateTime] datetime NULL,
    [Asset_code] varchar(30) NULL,
    [InRack] int NULL,
    [Showname] varchar(50) NULL,
    [AssignTo] varchar(255) NULL,
    [AssignType] int NOT NULL,
    [view_logi] int NULL,
    [QuickTurnAroundQty] int NULL,
    [QTSource] int NULL,
    [QTBookingNo] varchar(35) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPlotItems_v2')
BEGIN
CREATE TABLE [vwPlotItems_v2] (
    [ID] decimal(10,0) NOT NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] int NULL,
    [del_time_min] int NULL,
    [From_locn] int NULL,
    [trans_type_v41] int NULL,
    [return_time_min] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] int NULL,
    [booking_no_v32] varchar(35) NULL,
    [trans_qty] decimal(19,0) NULL,
    [prep_on] varchar(1) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [bit_field_v41] int NULL,
    [PreDate] datetime NULL,
    [prep_time_h] int NULL,
    [prep_time_m] int NULL,
    [BookingProgressStatus] int NULL,
    [Product_code_v42] char(30) NULL,
    [BookDate] datetime NULL,
    [TimeBookedH] int NULL,
    [TimeBookedM] int NULL,
    [TimeBookedS] int NULL,
    [DeprepOn] int NOT NULL,
    [DeprepDateTime] datetime NULL,
    [Asset_code] varchar(30) NULL,
    [InRack] int NULL,
    [Showname] varchar(50) NULL,
    [AssignTo] varchar(255) NULL,
    [view_logi] int NULL,
    [QuickTurnAroundQty] int NULL,
    [QTSource] int NULL,
    [QTBookingNo] varchar(35) NULL,
    [LoadDateTime] datetime NULL,
    [ADelDate] datetime NULL,
    [delivery_time] varchar(6) NULL,
    [SetDate] datetime NULL,
    [SetupTimeV61] varchar(4) NULL,
    [RehDate] datetime NULL,
    [RehearsalTime] varchar(4) NULL,
    [ShowSdate] datetime NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEdate] datetime NULL,
    [ShowEndTime] varchar(4) NULL,
    [Sdate] datetime NULL,
    [StrikeTime] varchar(4) NULL,
    [PickupRetDate] datetime NULL,
    [pickup_time] varchar(6) NULL,
    [UnloadDateTime] datetime NULL,
    [CustID] decimal(10,0) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPlotItemsPAT')
BEGIN
CREATE TABLE [vwPlotItemsPAT] (
    [ID] decimal(10,0) NOT NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] int NULL,
    [del_time_min] int NULL,
    [From_locn] int NULL,
    [trans_type_v41] int NULL,
    [return_time_min] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] int NULL,
    [booking_no_v32] varchar(35) NULL,
    [trans_qty] decimal(19,0) NULL,
    [prep_on] varchar(1) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [bit_field_v41] int NULL,
    [PreDate] datetime NULL,
    [prep_time_h] int NULL,
    [prep_time_m] int NULL,
    [BookingProgressStatus] int NULL,
    [Product_code_v42] char(30) NULL,
    [BookDate] datetime NULL,
    [TimeBookedH] int NULL,
    [TimeBookedM] int NULL,
    [TimeBookedS] int NULL,
    [DeprepOn] int NOT NULL,
    [DeprepDateTime] datetime NULL,
    [asset_code] varchar(30) NULL,
    [InRack] int NULL,
    [Showname] varchar(50) NULL,
    [AssignTo] varchar(255) NULL,
    [AssignType] int NOT NULL,
    [view_logi] int NULL,
    [QuickTurnAroundQty] int NULL,
    [QTSource] int NULL,
    [QTBookingNo] varchar(35) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPlotItemsPAT_v2')
BEGIN
CREATE TABLE [vwPlotItemsPAT_v2] (
    [ID] decimal(10,0) NOT NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] int NULL,
    [del_time_min] int NULL,
    [From_locn] int NULL,
    [trans_type_v41] int NULL,
    [return_time_min] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] int NULL,
    [booking_no_v32] varchar(35) NULL,
    [trans_qty] decimal(19,0) NULL,
    [prep_on] varchar(1) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [bit_field_v41] int NULL,
    [PreDate] datetime NULL,
    [prep_time_h] int NULL,
    [prep_time_m] int NULL,
    [BookingProgressStatus] int NULL,
    [Product_code_v42] char(30) NULL,
    [BookDate] datetime NULL,
    [TimeBookedH] int NULL,
    [TimeBookedM] int NULL,
    [TimeBookedS] int NULL,
    [DeprepOn] int NOT NULL,
    [DeprepDateTime] datetime NULL,
    [asset_code] varchar(30) NULL,
    [InRack] int NULL,
    [Showname] varchar(50) NULL,
    [AssignTo] varchar(255) NULL,
    [view_logi] int NULL,
    [QuickTurnAroundQty] int NULL,
    [QTSource] int NULL,
    [QTBookingNo] varchar(35) NULL,
    [LoadDateTime] datetime NULL,
    [ADelDate] datetime NULL,
    [delivery_time] varchar(6) NULL,
    [SetDate] datetime NULL,
    [SetupTimeV61] varchar(4) NULL,
    [RehDate] datetime NULL,
    [RehearsalTime] varchar(4) NULL,
    [ShowSdate] datetime NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEdate] datetime NULL,
    [ShowEndTime] varchar(4) NULL,
    [Sdate] datetime NULL,
    [StrikeTime] varchar(4) NULL,
    [PickupRetDate] datetime NULL,
    [pickup_time] varchar(6) NULL,
    [UnloadDateTime] datetime NULL,
    [CustID] decimal(10,0) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPOApproval')
BEGIN
CREATE TABLE [vwPOApproval] (
    [PPONumber] decimal(19,0) NULL,
    [POPrefix] varchar(3) NULL,
    [PtotalAmount] float NULL,
    [CrossRental] char(1) NULL,
    [VendorName] varchar(50) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPOItems')
BEGIN
CREATE TABLE [vwPOItems] (
    [ExpectedDate] datetime NULL,
    [UnReceivedQty] float NULL,
    [LProductcode] varchar(30) NULL,
    [Location] int NULL,
    [PPONumber] decimal(19,0) NULL,
    [POPrefix] varchar(3) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPOLine')
BEGIN
CREATE TABLE [vwPOLine] (
    [PONumber] int NOT NULL,
    [LineNumber] int NOT NULL,
    [LProductCode] varchar(30) NULL,
    [LQuantity] float NULL,
    [LQuantityReceived] int NULL,
    [LFFText] varchar(60) NULL,
    [LPrice] float NULL,
    [LUnitPrice] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPrepDeprepBookings')
BEGIN
CREATE TABLE [vwPrepDeprepBookings] (
    [Booking_no] varchar(35) NULL,
    [DDate] datetime NULL,
    [RDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [PreDate] datetime NULL,
    [prep_time_h] tinyint NULL,
    [prep_time_m] tinyint NULL,
    [DeprepDateTime] datetime NULL,
    [StartDateTime] datetime NULL,
    [EndDateTime] datetime NULL,
    [Booking_type_v32] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPriceOverrideReport_NoBookingOverrides')
BEGIN
CREATE TABLE [vwPriceOverrideReport_NoBookingOverrides] (
    [BookingNumber] varchar(35) NULL,
    [ProductCode] varchar(30) NULL,
    [Qty] smallint NULL,
    [OldPrice] float NULL,
    [NewPrice] float NULL,
    [TimeH] tinyint NULL,
    [TimeM] tinyint NULL,
    [Reason] varchar(50) NULL,
    [AuditDate] datetime NULL,
    [Operators] varchar(50) NULL,
    [BookingSalesperson] varchar(30) NULL,
    [CustomerSalesperson] char(30) NULL,
    [CustomerCode] varchar(30) NULL,
    [FieldIndicator] tinyint NULL,
    [BookingType] tinyint NULL,
    [AssignedTo] varchar(35) NULL,
    [AuditType] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwPriceOverrideReport_WithBookingOverrides')
BEGIN
CREATE TABLE [vwPriceOverrideReport_WithBookingOverrides] (
    [BookingNumber] varchar(35) NULL,
    [ProductCode] varchar(30) NULL,
    [Qty] int NULL,
    [OldPrice] float NULL,
    [NewPrice] float NULL,
    [TimeH] tinyint NULL,
    [TimeM] tinyint NULL,
    [Reason] varchar(50) NULL,
    [AuditDate] datetime NULL,
    [Operators] varchar(50) NULL,
    [BookingSalesperson] varchar(30) NULL,
    [CustomerSalesperson] char(30) NULL,
    [CustomerCode] varchar(30) NULL,
    [FieldIndicator] int NULL,
    [BookingType] tinyint NULL,
    [AssignedTo] varchar(35) NULL,
    [AuditType] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwProdsComponents')
BEGIN
CREATE TABLE [vwProdsComponents] (
    [ID] decimal(10,0) NOT NULL,
    [parent_code] char(30) NULL,
    [product_code] char(30) NULL,
    [qty_v5] float NULL,
    [sub_seq_no] tinyint NULL,
    [variable_part] tinyint NULL,
    [ContactID] decimal(10,0) NULL,
    [SelectComp] char(1) NULL,
    [AccessoryDiscount] float NULL,
    [AutoResolve] bit NULL,
    [nestedCompAcc] bit NULL,
    [CustomIcon] varchar(60) NULL,
    [product_type_v41] tinyint NULL,
    [DescriptionV6] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [CATID] decimal(10,0) NULL,
    [OLCatDesc] varchar(50) NULL,
    [cat_descv6] char(50) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwProdsInGroup')
BEGIN
CREATE TABLE [vwProdsInGroup] (
    [ID] decimal(10,0) NOT NULL,
    [seq_no] decimal(19,0) NULL,
    [product_code] char(30) NULL,
    [CustomIcon] varchar(60) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [product_Config] tinyint NULL,
    [ProdRoadcase] tinyint NULL,
    [product_type_v41] tinyint NULL,
    [IsInTrashCan] char(1) NULL,
    [SubCategory] varchar(30) NULL,
    [ContactID] decimal(10,0) NULL,
    [isGenericItem] char(1) NULL,
    [RegionNumber] int NOT NULL,
    [MfctPartNumber] varchar(30) NULL,
    [Location] int NOT NULL,
    [PictureFilename] varchar(MAX) NULL,
    [iPictureSize] int NULL,
    [indiv_hire_sale] char(1) NULL,
    [cat_descV6] char(50) NULL,
    [CategoryCustomIcon] varchar(60) NULL,
    [OLCatDesc] varchar(50) NULL,
    [SubCatDesc] char(50) NULL,
    [SubCatEnglishDesc] varchar(50) NULL,
    [SubCategoryCustomIcon] varchar(60) NULL,
    [assetCnt] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwQuoteComments')
BEGIN
CREATE TABLE [vwQuoteComments] (
    [ID] decimal(10,0) NOT NULL,
    [heading_no] tinyint NULL,
    [sub_seq_no] int NULL,
    [seq_no] decimal(19,0) NULL,
    [GroupSeqNo] int NULL,
    [comment_desc_v42] char(70) NULL,
    [Booking_no_v32] varchar(35) NULL,
    [SubRentalLinkID] int NOT NULL,
    [view_client] bit NULL,
    [view_logi] bit NULL,
    [days_charged_v51] float NULL,
    [Hheading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [DelvDate] datetime NULL,
    [Del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [HRetnDate] datetime NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwQuoteCrew')
BEGIN
CREATE TABLE [vwQuoteCrew] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] int NULL,
    [price] float NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [minutes] tinyint NULL,
    [days_using] float NULL,
    [person] char(30) NULL,
    [bit_field_V41] tinyint NULL,
    [task] tinyint NULL,
    [techRate] float NULL,
    [TechPay] float NULL,
    [techrateIsHourOrDay] char(1) NULL,
    [UnitRate] float NULL,
    [StraightTime] float NULL,
    [Overtime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [group_descV6] char(50) NULL,
    [GroupProductType] tinyint NULL,
    [days_table] tinyint NULL,
    [MID] decimal(10,0) NOT NULL,
    [product_code] char(30) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [product_Config] tinyint NULL,
    [product_type_v41] tinyint NULL,
    [components_inv] char(1) NULL,
    [components_quote] char(1) NULL,
    [notes_on_quote] char(1) NULL,
    [notes_on_inv] char(1) NULL,
    [isGenericItem] char(1) NULL,
    [PrintedDesc] varchar(50) NULL,
    [SubCategory] varchar(30) NULL,
    [bCustomPrintouts] bit NOT NULL,
    [days_charged_v51] float NULL,
    [Hheading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [DelvDate] datetime NULL,
    [Del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [HRetnDate] datetime NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [DisallowDisc] char(1) NULL,
    [EnforceMinHours] bit NULL,
    [MinimumHours] float NULL,
    [OLGroupDesc] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [OLExternalDesc] varchar(50) NULL,
    [PrintOnInvoice] bit NULL,
    [PrintOnQuote] bit NULL,
    [booking_type_v32] tinyint NULL,
    [taxableExempt] bit NULL,
    [CrewClientNotes] varchar(40) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwQuoteCrew_SC')
BEGIN
CREATE TABLE [vwQuoteCrew_SC] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [product_code_v42] varchar(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] int NULL,
    [price] float NULL,
    [rate_selected] tinyint NULL,
    [hours] tinyint NULL,
    [Minutes] tinyint NULL,
    [days_using] float NULL,
    [person] char(30) NULL,
    [bit_field_v41] tinyint NULL,
    [task] tinyint NULL,
    [TechRate] float NULL,
    [TechPay] float NULL,
    [techrateIsHourorDay] char(1) NULL,
    [unitRate] float NULL,
    [StraightTime] float NULL,
    [OverTime] float NULL,
    [DoubleTime] float NULL,
    [UseCustomRate] bit NULL,
    [CustomRate] float NULL,
    [HourOrDay] char(1) NULL,
    [ShortTurnaround] bit NULL,
    [HourlyRateID] decimal(10,0) NOT NULL,
    [UnpaidHours] int NOT NULL,
    [UnpaidMins] int NOT NULL,
    [ConfirmationLevel] tinyint NULL,
    [JobDescription] varchar(160) NULL,
    [TechIsConfirmed] bit NOT NULL,
    [MeetTechOnSite] bit NOT NULL,
    [group_descV6] char(50) NULL,
    [GroupProductType] tinyint NULL,
    [days_table] tinyint NULL,
    [MID] decimal(10,0) NOT NULL,
    [product_code] char(30) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [product_Config] tinyint NULL,
    [product_type_v41] tinyint NULL,
    [components_inv] char(1) NULL,
    [components_quote] char(1) NULL,
    [notes_on_quote] char(1) NULL,
    [notes_on_inv] char(1) NULL,
    [isGenericItem] char(1) NULL,
    [PrintedDesc] varchar(50) NULL,
    [SubCategory] varchar(30) NULL,
    [bCustomPrintouts] bit NOT NULL,
    [days_charged_v51] float NULL,
    [Hheading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [DelvDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [HRetnDate] datetime NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [DisallowDisc] char(1) NULL,
    [EnforceMinHours] bit NULL,
    [MinimumHours] float NULL,
    [OLGroupDesc] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [OLExternalDesc] varchar(50) NULL,
    [PrintOnInvoice] bit NULL,
    [PrintOnQuote] bit NULL,
    [booking_type_v32] tinyint NULL,
    [taxableExempt] bit NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [ShowSdate] datetime NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEdate] datetime NULL,
    [ShowEndTime] varchar(4) NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viaV71] int NULL,
    [pickup_time] char(6) NULL,
    [RehearsalTime] varchar(4) NULL,
    [StrikeTime] varchar(4) NULL,
    [VenueRoom] varchar(35) NULL,
    [SetDate] datetime NULL,
    [ADelDate] datetime NULL,
    [SDate] datetime NULL,
    [RehDate] datetime NULL,
    [ConDate] datetime NULL,
    [CustID] decimal(10,0) NOT NULL,
    [event_code] char(30) NULL,
    [task_name] varchar(30) NULL,
    [TechName] varchar(50) NULL,
    [TechCell] varchar(16) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwQuoteRentalEquipV2')
BEGIN
CREATE TABLE [vwQuoteRentalEquipV2] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [trans_type_v41] tinyint NULL,
    [PDate] datetime NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [DayWeekRate] float NULL,
    [product_code_v42] char(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [bit_field_V41] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [AssignType] tinyint NOT NULL,
    [AssignTo] varchar(255) NULL,
    [techRateorDaysCharged] float NULL,
    [UnitRate] float NULL,
    [comment_desc_v42] char(70) NULL,
    [TechPay] float NULL,
    [InRack] bit NULL,
    [tableNo] tinyint NULL,
    [hourly_rate] float NULL,
    [half_day] float NULL,
    [rate_1st_day] float NULL,
    [rate_extra_days] float NULL,
    [rate_week] float NULL,
    [rate_long_term] float NULL,
    [RDayWeekRate] float NULL,
    [deposit] float NULL,
    [damage_waiver_rate] float NULL,
    [group_descV6] char(50) NULL,
    [GroupProductType] tinyint NULL,
    [days_table] tinyint NULL,
    [MID] decimal(10,0) NOT NULL,
    [product_code] char(30) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [product_Config] tinyint NULL,
    [product_type_v41] tinyint NOT NULL,
    [Ord_unit] varchar(4) NULL,
    [cost_price] float NULL,
    [retail_price] float NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [unit_weight] float NULL,
    [unit_volume] float NULL,
    [components_inv] char(1) NOT NULL,
    [components_quote] char(1) NOT NULL,
    [asset_track] char(1) NOT NULL,
    [notes_on_quote] char(1) NOT NULL,
    [notes_on_inv] char(1) NOT NULL,
    [CountryOfOrigin] varchar(25) NULL,
    [prodRoadCase] tinyint NULL,
    [UseWeeklyRate] char(1) NULL,
    [isGenericItem] char(1) NULL,
    [PrintedDesc] varchar(50) NULL,
    [SubCategory] varchar(30) NULL,
    [zColor] varchar(25) NULL,
    [rLength] float NOT NULL,
    [rWidth] float NOT NULL,
    [rHeight] float NOT NULL,
    [zModelNo] varchar(25) NULL,
    [bCustomPrintouts] bit NOT NULL,
    [DontPrintOnCrossRentPO] bit NOT NULL,
    [days_charged_v51] float NULL,
    [Hheading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [DelvDate] datetime NULL,
    [Del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [HRetnDate] datetime NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [DisallowDisc] char(1) NULL,
    [MfctPartNumber] varchar(30) NULL,
    [OLGroupDesc] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [OLExternalDesc] varchar(50) NULL,
    [BeforeDiscountAmount] float NULL,
    [NonTrackedBarcode] varchar(30) NULL,
    [ReplacementValue] float NULL,
    [BinLocation] varchar(100) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwQuoteRentalEquipV3')
BEGIN
CREATE TABLE [vwQuoteRentalEquipV3] (
    [ID] decimal(10,0) NOT NULL,
    [product_code_v42] char(30) NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [View_Logi] bit NULL,
    [trans_type_v41] tinyint NULL,
    [item_type] tinyint NULL,
    [PDate] datetime NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [UnitRate] float NULL,
    [Undisc_amt] float NULL,
    [BeforeDiscountAmount] float NULL,
    [price] float NULL,
    [days_using] int NULL,
    [DayWeekRate] float NULL,
    [AssignType] tinyint NOT NULL,
    [AssignTo] varchar(255) NULL,
    [techRateorDaysCharged] float NULL,
    [TechPay] float NULL,
    [comment_desc_v42] char(70) NULL,
    [InRack] bit NULL,
    [bit_field_V41] tinyint NULL,
    [AddedAtCheckout] bit NULL,
    [PackageLevel] smallint NULL,
    [EstSubRentalCost] float NULL,
    [EstSubRentalDays] smallint NULL,
    [VendorID] int NULL,
    [Notes] varchar(MAX) NULL,
    [UseEstSubHireOverride] bit NULL,
    [QTBookingNo] varchar(35) NULL,
    [tableNo] tinyint NULL,
    [hourly_rate] float NULL,
    [half_day] float NULL,
    [RDayWeekRate] float NULL,
    [rate_1st_day] float NULL,
    [rate_extra_days] float NULL,
    [rate_week] float NULL,
    [rate_long_term] float NULL,
    [deposit] float NULL,
    [damage_waiver_rate] float NULL,
    [ReplacementValue] float NULL,
    [group_descV6] char(50) NULL,
    [GroupProductType] tinyint NULL,
    [days_table] tinyint NULL,
    [OLGroupDesc] varchar(50) NULL,
    [MID] decimal(10,0) NOT NULL,
    [product_code] char(30) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [product_Config] tinyint NULL,
    [product_type_v41] tinyint NOT NULL,
    [Ord_unit] varchar(4) NULL,
    [cost_price] float NULL,
    [retail_price] float NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [unit_weight] float NULL,
    [unit_volume] float NULL,
    [components_inv] char(1) NOT NULL,
    [components_quote] char(1) NOT NULL,
    [asset_track] char(1) NOT NULL,
    [notes_on_quote] char(1) NOT NULL,
    [notes_on_inv] char(1) NOT NULL,
    [CountryOfOrigin] varchar(25) NULL,
    [prodRoadCase] tinyint NULL,
    [UseWeeklyRate] char(1) NULL,
    [isGenericItem] char(1) NULL,
    [PrintedDesc] varchar(50) NULL,
    [SubCategory] varchar(30) NULL,
    [zColor] varchar(25) NULL,
    [rLength] float NOT NULL,
    [rWidth] float NOT NULL,
    [rHeight] float NOT NULL,
    [zModelNo] varchar(25) NULL,
    [bCustomPrintouts] bit NOT NULL,
    [DontPrintOnCrossRentPO] bit NOT NULL,
    [DisallowDisc] char(1) NULL,
    [MfctPartNumber] varchar(30) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [OLExternalDesc] varchar(50) NULL,
    [NonTrackedBarcode] varchar(30) NULL,
    [BinLocation] varchar(100) NULL,
    [AveKwHusage] float NULL,
    [Hheading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [days_charged_v51] float NULL,
    [DelvDate] datetime NULL,
    [Del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [HRetnDate] datetime NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwQuoteRentalEquipV3_SC')
BEGIN
CREATE TABLE [vwQuoteRentalEquipV3_SC] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [seq_no] decimal(19,0) NULL,
    [sub_seq_no] int NULL,
    [trans_type_v41] tinyint NULL,
    [PDate] datetime NULL,
    [PTimeH] tinyint NULL,
    [PTimeM] tinyint NULL,
    [DayWeekRate] float NULL,
    [product_code_v42] char(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [trans_qty] decimal(19,0) NULL,
    [price] float NULL,
    [item_type] tinyint NULL,
    [days_using] int NULL,
    [sub_hire_qtyV61] decimal(19,0) NULL,
    [bit_field_v41] tinyint NULL,
    [QtyReturned] decimal(19,0) NULL,
    [QtyCheckedOut] decimal(19,0) NULL,
    [AssignType] tinyint NOT NULL,
    [AssignTo] varchar(255) NULL,
    [techRateorDaysCharged] float NULL,
    [unitRate] float NULL,
    [Comment_desc_v42] char(70) NOT NULL,
    [TechPay] float NULL,
    [InRack] bit NULL,
    [tableNo] tinyint NULL,
    [hourly_rate] float NULL,
    [half_day] float NULL,
    [rate_1st_day] float NULL,
    [rate_extra_days] float NULL,
    [rate_week] float NULL,
    [rate_long_term] float NULL,
    [RDayWeekRate] float NULL,
    [deposit] float NULL,
    [damage_waiver_rate] float NULL,
    [group_descV6] char(50) NULL,
    [GroupProductType] tinyint NULL,
    [days_table] tinyint NULL,
    [MID] decimal(10,0) NULL,
    [product_code] char(30) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [descriptionV6] varchar(50) NOT NULL,
    [product_Config] tinyint NULL,
    [product_type_v41] tinyint NOT NULL,
    [Ord_unit] varchar(4) NULL,
    [cost_price] float NULL,
    [retail_price] float NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [unit_weight] float NULL,
    [unit_volume] float NULL,
    [components_inv] char(1) NOT NULL,
    [components_quote] char(1) NOT NULL,
    [asset_track] char(1) NOT NULL,
    [notes_on_quote] char(1) NOT NULL,
    [notes_on_inv] char(1) NOT NULL,
    [CountryOfOrigin] varchar(25) NULL,
    [prodRoadCase] tinyint NULL,
    [UseWeeklyRate] char(1) NULL,
    [isGenericItem] char(1) NULL,
    [PrintedDesc] varchar(50) NOT NULL,
    [SubCategory] varchar(30) NULL,
    [zColor] varchar(25) NULL,
    [rLength] float NULL,
    [rWidth] float NULL,
    [rHeight] float NULL,
    [zModelNo] varchar(25) NULL,
    [bCustomPrintouts] bit NULL,
    [DontPrintOnCrossRentPO] bit NULL,
    [days_charged_v51] float NULL,
    [Hheading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [DelvDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [HRetnDate] datetime NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [DisallowDisc] char(1) NULL,
    [MfctPartNumber] varchar(30) NULL,
    [OLGroupDesc] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [OLExternalDesc] varchar(50) NULL,
    [BeforeDiscountAmount] float NULL,
    [NonTrackedBarcode] varchar(30) NULL,
    [ReplacementValue] float NULL,
    [BinLocation] varchar(100) NULL,
    [AddedAtCheckout] bit NULL,
    [Undisc_amt] float NULL,
    [View_Logi] bit NULL,
    [View_client] bit NULL,
    [Logi_HeadingNo] int NULL,
    [Logi_GroupSeqNo] int NULL,
    [Logi_Seq_No] int NULL,
    [Logi_Sub_Seq_no] int NULL,
    [NodeCollapsed] bit NULL,
    [PackageLevel] smallint NULL,
    [HeadingNodeCollapsed] bit NULL,
    [Tree_GroupSeqNo] decimal(19,0) NULL,
    [tree_InvmasSeqNo] decimal(19,0) NULL,
    [percent_disc] float NULL,
    [discount_rate] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwQuoteUSundryItems_V2')
BEGIN
CREATE TABLE [vwQuoteUSundryItems_V2] (
    [ID] decimal(10,0) NOT NULL,
    [Booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [GroupSeqNo] int NULL,
    [Seq_no] int NULL,
    [sub_seq_no] int NULL,
    [view_client] bit NULL,
    [View_logi] bit NULL,
    [RevenueCode] varchar(50) NULL,
    [sundry_price] float NULL,
    [Sundry_cost] float NULL,
    [restock_charge] tinyint NULL,
    [sundry_desc] varchar(50) NULL,
    [days_charged_v51] float NULL,
    [Hheading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [DelvDate] datetime NULL,
    [Del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [HRetnDate] datetime NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRackedAssets')
BEGIN
CREATE TABLE [vwRackedAssets] (
    [ID] decimal(10,0) NOT NULL,
    [PRODUCT_COde] char(30) NULL,
    [STOCK_NUMBER] int NULL,
    [asset_barcode] char(30) NULL,
    [locn] int NULL,
    [parent_basecode] char(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwReadInventory')
BEGIN
CREATE TABLE [vwReadInventory] (
    [ID] decimal(10,0) NOT NULL,
    [seq_no] decimal(19,0) NULL,
    [product_code] char(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [groupFld] varchar(30) NULL,
    [category] varchar(30) NULL,
    [product_Config] tinyint NULL,
    [product_type_v41] tinyint NULL,
    [on_hand] float NULL,
    [cost_price] float NULL,
    [retail_price] float NULL,
    [wholesale_price] float NULL,
    [trade_price] float NULL,
    [DisallowDisc] char(1) NULL,
    [person_required] char(1) NULL,
    [isGenericItem] char(1) NULL,
    [UseWeeklyRate] char(1) NULL,
    [DefaultDayRateID] tinyint NOT NULL,
    [DefaultHourlyRateID] decimal(18,0) NOT NULL,
    [seqNo] decimal(19,0) NULL,
    [ContactID] decimal(10,0) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRegionLocations')
BEGIN
CREATE TABLE [vwRegionLocations] (
    [RegionNumber] int NOT NULL,
    [RegionName] varchar(50) NOT NULL,
    [Locn_number] int NULL,
    [Locn_name] varchar(50) NULL,
    [booking_no] varchar(35) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRentalSalesSundry_HeadingTotals')
BEGIN
CREATE TABLE [vwRentalSalesSundry_HeadingTotals] (
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [PriceSum] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRentalSalesSundry_HeadingTotals_SC')
BEGIN
CREATE TABLE [vwRentalSalesSundry_HeadingTotals_SC] (
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [heading_desc] varchar(79) NULL,
    [PriceSum] float NULL,
    [product_code_v42] char(30) NULL,
    [comment_desc_v42] varchar(70) NULL,
    [DiscAmt] float NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwReportBooking_SC')
BEGIN
CREATE TABLE [vwReportBooking_SC] (
    [ID] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viaV71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [Item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_nameV6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setupTimeV61] varchar(4) NULL,
    [RehearsalTime] varchar(4) NULL,
    [StrikeTime] varchar(4) NULL,
    [Trans_to_locn] int NULL,
    [showStartTime] varchar(4) NULL,
    [ShowEndTime] varchar(4) NULL,
    [transferNo] decimal(19,0) NULL,
    [currencyStr] varchar(5) NULL,
    [BookingProgressStatus] tinyint NULL,
    [ConfirmedBy] varchar(35) NULL,
    [ConfirmedDocRef] varchar(50) NULL,
    [VenueRoom] varchar(35) NULL,
    [expAttendees] int NULL,
    [HourBooked] tinyint NULL,
    [MinBooked] tinyint NULL,
    [SecBooked] tinyint NULL,
    [TaxAuthority1] int NULL,
    [TaxAuthority2] int NULL,
    [HorCCroom] int NULL,
    [subrooms] char(12) NULL,
    [truckOut] int NULL,
    [truckIn] int NULL,
    [tripOut] tinyint NULL,
    [tripIn] tinyint NULL,
    [showName] varchar(50) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [OurNumberDel] char(1) NULL,
    [OurNumberRet] char(1) NULL,
    [DatesAndTimesEnabled] char(1) NULL,
    [Paymethod] varchar(25) NULL,
    [Government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [ContactLoadedIntoVenue] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [OrganizationV6] varchar(50) NULL,
    [Salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [Inv_date] datetime NULL,
    [ShowSdate] datetime NULL,
    [ShowEdate] datetime NULL,
    [SetDate] datetime NULL,
    [ADelDate] datetime NULL,
    [SDate] datetime NULL,
    [RehDate] datetime NULL,
    [ConDate] datetime NULL,
    [TOutDate] datetime NULL,
    [TInDate] datetime NULL,
    [PreDate] datetime NULL,
    [ConByDate] datetime NULL,
    [bookingPrinted] char(1) NULL,
    [CustCode] varchar(30) NULL,
    [ExtendedFrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [PotPercent] float NULL,
    [Referral] varchar(50) NULL,
    [EventType] varchar(20) NULL,
    [Priority] int NULL,
    [InvoiceStage] int NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardHolder] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [PickupRetDate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [MaxBookingValue] float NULL,
    [UsesPriceTable] int NULL,
    [DateToInvoice] datetime NULL,
    [TwoWkDisc] float NULL,
    [ThreeWkDisc] float NULL,
    [ServCont] char(1) NULL,
    [PaymentOptions] tinyint NULL,
    [PrintedPayTerm] varchar(40) NULL,
    [RentalType] tinyint NULL,
    [UseBillSchedule] char(1) NULL,
    [Tax2] float NULL,
    [ContactID] decimal(9,0) NULL,
    [ShortHours] tinyint NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [dtExpected_ReturnDate] datetime NOT NULL,
    [vcExpected_ReturnTime] varchar(4) NOT NULL,
    [vcTruckOutTime] varchar(4) NOT NULL,
    [vcTruckInTime] varchar(4) NOT NULL,
    [CustID] decimal(10,0) NOT NULL,
    [VenueID] int NOT NULL,
    [LateChargesApplied] bit NOT NULL,
    [shortagesAreTransfered] bit NOT NULL,
    [VenueContactID] int NULL,
    [VenueContact] varchar(50) NULL,
    [VenueContactPhoneID] int NULL,
    [LTBillingOption] tinyint NULL,
    [Collection] float NULL,
    [FuelSurchargeRate] float NULL,
    [FreightLocked] bit NULL,
    [LabourLocked] bit NULL,
    [RentalLocked] bit NULL,
    [PriceLocked] bit NULL,
    [insurance_type] tinyint NULL,
    [EntryDate] datetime NULL,
    [CreditSurchargeRate] float NULL,
    [CreditSurchargeAmount] float NULL,
    [DisableTreeOrder] bit NULL,
    [LoadDateTime] datetime NULL,
    [UnloadDateTime] datetime NULL,
    [DeprepDateTime] datetime NULL,
    [DeprepOn] bit NOT NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [ScheduleDatesOn] varchar(10) NULL,
    [ConfirmationFinancials] varchar(30) NULL,
    [EventManagementRate] float NULL,
    [EventManagementAmount] float NULL,
    [EquipmentModified] bit NULL,
    [CrewStatusColumn] tinyint NULL,
    [bBookingIsComplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingID] int NULL,
    [DateLastModified] datetime NULL,
    [vvenuename] varchar(50) NULL,
    [vcontactname] varchar(50) NULL,
    [vcontactid] decimal(10,0) NULL,
    [Address1] varchar(50) NULL,
    [Address2] varchar(50) NULL,
    [City] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [ZipCode] varchar(12) NULL,
    [venuetype] tinyint NULL,
    [vp1cc] varchar(10) NULL,
    [vp1ac] varchar(10) NULL,
    [vp1number] varchar(16) NULL,
    [vp2cc] varchar(10) NULL,
    [vp2ac] varchar(10) NULL,
    [vp2number] varchar(16) NULL,
    [vp2extension] varchar(8) NULL,
    [vp3cc] varchar(10) NULL,
    [vp3ac] varchar(10) NULL,
    [vp3number] varchar(16) NULL,
    [DefaultFolder] varchar(255) NULL,
    [WebPage] varchar(80) NULL,
    [cid] decimal(10,0) NOT NULL,
    [Customer_code] varchar(30) NULL,
    [PostalAddress1] char(50) NULL,
    [PostalAddress2] char(50) NULL,
    [PostalAddress3] char(50) NULL,
    [PostalState] varchar(50) NULL,
    [postalPostCode] char(12) NULL,
    [PostalCountry] varchar(50) NULL,
    [ccurrencystr] varchar(5) NULL,
    [UsesPriceTableV71] tinyint NULL,
    [csales_tax_no] char(25) NULL,
    [Account_type] tinyint NULL,
    [industry_type] varchar(8) NULL,
    [defaultinsurance_type] tinyint NULL,
    [hire_tax_exempt] char(1) NULL,
    [Price_customer_pays] tinyint NULL,
    [customer_number] char(6) NULL,
    [stop_credit] tinyint NULL,
    [Last_bk_seq] varchar(5) NULL,
    [Credit_limit] float NULL,
    [Current] float NULL,
    [Seven_days] float NULL,
    [Fourteen_days] float NULL,
    [Twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [lastPmtDate] datetime NULL,
    [last_pmt_amt] float NULL,
    [Monthly_cycle_billing_basis] tinyint NULL,
    [csalesperson] char(30) NULL,
    [Cust_Contact] varchar(35) NULL,
    [CustOrganization] varchar(50) NULL,
    [Address_l1V6] varchar(50) NULL,
    [Address_l2V6] varchar(50) NULL,
    [Address_l3V6] varchar(50) NULL,
    [Cust_StreetState] varchar(50) NULL,
    [Cust_StreetPostCode] varchar(12) NULL,
    [Cust_StreetCountry] varchar(50) NULL,
    [webAddress] varchar(80) NULL,
    [emailAddress] varchar(80) NULL,
    [cpaymethod] varchar(16) NULL,
    [lastBalupDate] datetime NULL,
    [CustCDate] datetime NULL,
    [InsuredFromDate] datetime NULL,
    [InsuredToDate] datetime NULL,
    [Field1] varchar(25) NULL,
    [bPONumRequired] bit NULL,
    [FaxCallType] tinyint NULL,
    [FaxDialAreaCode] bit NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [Phone2Ext] varchar(8) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [CampaignID] int NULL,
    [iLink_ContactID] decimal(10,0) NULL,
    [CustomerDiscountRate] float NULL,
    [Event_Description] varchar(32) NULL,
    [showdaysCharged] float NULL,
    [Contact_ContactName] varchar(35) NULL,
    [Contact_ContactFirstname] varchar(25) NULL,
    [Contact_Contactsurname] varchar(35) NULL,
    [Contact_Ph1] varchar(16) NULL,
    [Contact_CountryCodePh1] varchar(10) NULL,
    [Contact_AreaCodePh1] varchar(10) NULL,
    [Contact_Ph2] varchar(16) NULL,
    [Contact_CountryCodePh2] varchar(10) NULL,
    [Contact_AreaCodePh2] varchar(10) NULL,
    [Contact_FaxAreaCode] varchar(10) NULL,
    [Contact_Fax] varchar(16) NULL,
    [Contact_FaxCountryCode] varchar(10) NULL,
    [Contact_Cell] varchar(16) NULL,
    [Contact_CellAreaCode] varchar(10) NULL,
    [Contact_CellCountryCode] varchar(10) NULL,
    [Contact_email] varchar(80) NULL,
    [Contact_Adr1] varchar(50) NULL,
    [Contact_Adr2] varchar(50) NULL,
    [Contact_Adr3] varchar(50) NULL,
    [Contact_State] varchar(50) NULL,
    [Contact_Postcode] varchar(12) NULL,
    [Contact_Country] varchar(50) NULL,
    [Contact_Postion] varchar(50) NULL,
    [billcust_contactV6] varchar(35) NULL,
    [billcust_Address_l1V6] char(50) NULL,
    [billcust_Address_l2V6] char(50) NULL,
    [billcust_Address_l3V6] char(50) NULL,
    [billcust_organisationv6] varchar(50) NULL,
    [billcust_post_code] char(12) NULL,
    [billcust_state] varchar(50) NULL,
    [billcust_country] varchar(50) NULL,
    [billcust_Phone1CountryCode] varchar(10) NULL,
    [billcust_Phone1AreaCode] varchar(10) NULL,
    [billcust_Phone1Digits] varchar(16) NULL,
    [billcust_emailAddress] varchar(80) NULL,
    [billcust_PaymentTermsName] varchar(40) NULL,
    [status_text] varchar(9) NULL,
    [BookingProgressStatus_text] varchar(12) NULL,
    [PrintedPayTerm_text] varchar(68) NULL,
    [VenueRoomListText] varchar(300) NULL,
    [Instruct_text] text NULL,
    [TotalFreightCharge] float NULL,
    [TaxAuthority1_name] varchar(24) NULL,
    [TaxAuthority2_name] varchar(24) NULL,
    [Salesperson_name] varchar(45) NULL,
    [Salesperson_Email] varchar(80) NULL,
    [Salesperson_Position] varchar(50) NULL,
    [Salesperson_CellCountryCode] varchar(10) NULL,
    [Salesperson_CellAreaCode] varchar(10) NULL,
    [Salesperson_Cell] varchar(16) NULL,
    [Salesperson_Ph1Country] varchar(10) NULL,
    [Salesperson_Ph1Area] varchar(10) NULL,
    [Salesperson_Ph1] varchar(16) NULL,
    [Salesperson_FaxCountry] varchar(10) NULL,
    [Salesperson_FaxAreaCode] varchar(10) NULL,
    [Salesperson_Fax] varchar(16) NULL,
    [Salesperson_Adr1] varchar(50) NULL,
    [Salesperson_Adr2] varchar(50) NULL,
    [Salesperson_Adr3] varchar(50) NULL,
    [Salesperson_State] varchar(50) NULL,
    [Salesperson_Postcode] varchar(12) NULL,
    [Salesperson_Country] varchar(50) NULL,
    [ProjectMan_Name] varchar(45) NULL,
    [ProjectMan_Email] varchar(80) NULL,
    [ProjectMan_Position] varchar(50) NULL,
    [ProjectMan_CellCountryCode] varchar(10) NULL,
    [ProjectMan_CellAreaCode] varchar(10) NULL,
    [ProjectMan_Cell] varchar(16) NULL,
    [PayTermName] varchar(40) NULL,
    [InvStageName] varchar(20) NULL,
    [MasterBillingName] varchar(50) NULL,
    [Locn_number] int NULL,
    [LocnIntName] varchar(50) NULL,
    [LocnExtName] varchar(40) NULL,
    [LAdr1] varchar(40) NULL,
    [LAdr2] varchar(40) NULL,
    [Ladr3] varchar(40) NULL,
    [LocnState] varchar(40) NULL,
    [PostCode] varchar(40) NULL,
    [LocnCountry] varchar(40) NULL,
    [RegionNumber] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwReportBooking_v1')
BEGIN
CREATE TABLE [vwReportBooking_v1] (
    [id] decimal(10,0) NOT NULL,
    [booking_no] varchar(35) NULL,
    [order_no] varchar(25) NULL,
    [payment_type] tinyint NULL,
    [deposit_quoted_v50] float NULL,
    [price_quoted] float NULL,
    [docs_produced] tinyint NULL,
    [hire_price] float NULL,
    [booking_type_v32] tinyint NULL,
    [status] tinyint NULL,
    [delivery] float NULL,
    [percent_disc] float NULL,
    [delivery_viav71] int NULL,
    [delivery_time] char(6) NULL,
    [pickup_viav71] int NULL,
    [pickup_time] char(6) NULL,
    [invoiced] char(1) NULL,
    [labour] float NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [discount_rate] float NULL,
    [same_address] char(1) NULL,
    [insurance_v5] float NULL,
    [days_using] int NULL,
    [un_disc_amount] float NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [item_cnt] int NULL,
    [sales_discount_rate] float NULL,
    [sales_amount] float NULL,
    [tax1] float NULL,
    [division] tinyint NULL,
    [contact_namev6] varchar(35) NULL,
    [sales_tax_no] char(25) NULL,
    [last_modified_by] char(2) NULL,
    [delivery_address_exist] char(1) NULL,
    [sales_percent_disc] float NULL,
    [pricing_scheme_used] tinyint NULL,
    [days_charged_v51] float NULL,
    [sale_of_asset] float NULL,
    [from_locn] int NULL,
    [return_to_locn] int NULL,
    [retail_value] float NULL,
    [perm_casual] char(1) NULL,
    [setuptimev61] varchar(4) NULL,
    [rehearsaltime] varchar(4) NULL,
    [striketime] varchar(4) NULL,
    [trans_to_locn] int NULL,
    [showstarttime] varchar(4) NULL,
    [showendtime] varchar(4) NULL,
    [transferno] decimal(19,0) NULL,
    [currencystr] varchar(5) NULL,
    [bookingprogressstatus] tinyint NULL,
    [confirmedby] varchar(35) NULL,
    [confirmeddocref] varchar(50) NULL,
    [venueroom] varchar(35) NULL,
    [expattendees] int NULL,
    [hourbooked] tinyint NULL,
    [minbooked] tinyint NULL,
    [secbooked] tinyint NULL,
    [taxauthority1] int NULL,
    [taxauthority2] int NULL,
    [horccroom] int NULL,
    [subrooms] char(12) NULL,
    [truckout] int NULL,
    [truckin] int NULL,
    [tripout] tinyint NULL,
    [tripin] tinyint NULL,
    [showname] varchar(50) NULL,
    [freightservicedel] tinyint NULL,
    [freightserviceret] tinyint NULL,
    [delzone] int NULL,
    [retzone] int NULL,
    [ournumberdel] char(1) NULL,
    [ournumberret] char(1) NULL,
    [datesandtimesenabled] char(1) NULL,
    [paymethod] varchar(25) NULL,
    [government] char(1) NULL,
    [prep_time_h] tinyint NULL,
    [prep_entered] char(1) NULL,
    [prep_time_m] tinyint NULL,
    [sales_undisc_amount] float NULL,
    [losses] float NULL,
    [half_day_aplic] char(1) NULL,
    [contactloadedintovenue] tinyint NULL,
    [assigned_to_v61] varchar(35) NULL,
    [sundry_total] float NULL,
    [organizationv6] varchar(50) NULL,
    [salesperson] varchar(30) NULL,
    [order_date] datetime NULL,
    [ddate] datetime NULL,
    [rdate] datetime NULL,
    [inv_date] datetime NULL,
    [showsdate] datetime NULL,
    [showedate] datetime NULL,
    [setdate] datetime NULL,
    [adeldate] datetime NULL,
    [sdate] datetime NULL,
    [rehdate] datetime NULL,
    [condate] datetime NULL,
    [toutdate] datetime NULL,
    [tindate] datetime NULL,
    [predate] datetime NULL,
    [conbydate] datetime NULL,
    [bookingprinted] char(1) NULL,
    [custcode] varchar(30) NULL,
    [extendedfrom] varchar(5) NULL,
    [last_operators] varchar(50) NULL,
    [operatorsid] decimal(19,0) NULL,
    [potpercent] float NULL,
    [referral] varchar(50) NULL,
    [eventtype] varchar(20) NULL,
    [priority] int NULL,
    [invoicestage] int NULL,
    [creditcardname] varchar(20) NULL,
    [creditcardnumber] varchar(250) NULL,
    [expmonth] varchar(250) NULL,
    [expyear] varchar(250) NULL,
    [cardholder] varchar(250) NULL,
    [cardstreet1] varchar(250) NULL,
    [cardstreet2] varchar(250) NULL,
    [cardcity] varchar(250) NULL,
    [cardstate] varchar(250) NULL,
    [cardpostcode] varchar(250) NULL,
    [creditcardidno] varchar(250) NULL,
    [pickupretdate] datetime NULL,
    [rent_invd_too_date] datetime NULL,
    [maxbookingvalue] float NULL,
    [usespricetable] int NULL,
    [datetoinvoice] datetime NULL,
    [twowkdisc] float NULL,
    [threewkdisc] float NULL,
    [servcont] char(1) NULL,
    [paymentoptions] tinyint NULL,
    [printedpayterm] varchar(40) NULL,
    [rentaltype] tinyint NULL,
    [usebillschedule] char(1) NULL,
    [tax2] float NULL,
    [contactid] decimal(9,0) NULL,
    [shorthours] tinyint NULL,
    [projectmanager] varchar(8) NOT NULL,
    [dtexpected_returndate] datetime NOT NULL,
    [vcexpected_returntime] varchar(4) NOT NULL,
    [vctruckouttime] varchar(4) NOT NULL,
    [vctruckintime] varchar(4) NOT NULL,
    [custid] decimal(10,0) NOT NULL,
    [venueid] int NOT NULL,
    [latechargesapplied] bit NOT NULL,
    [shortagesaretransfered] bit NOT NULL,
    [venuecontactid] int NULL,
    [venuecontact] varchar(50) NULL,
    [venuecontactphoneid] int NULL,
    [ltbillingoption] tinyint NULL,
    [collection] float NULL,
    [fuelsurchargerate] float NULL,
    [freightlocked] bit NULL,
    [labourlocked] bit NULL,
    [rentallocked] bit NULL,
    [pricelocked] bit NULL,
    [insurance_type] tinyint NULL,
    [entrydate] datetime NULL,
    [creditsurchargerate] float NULL,
    [creditsurchargeamount] float NULL,
    [disabletreeorder] bit NULL,
    [loaddatetime] datetime NULL,
    [unloaddatetime] datetime NULL,
    [deprepdatetime] datetime NULL,
    [deprepon] bit NOT NULL,
    [deliverydateon] bit NOT NULL,
    [pickupdateon] bit NOT NULL,
    [scheduledateson] varchar(10) NULL,
    [confirmationfinancials] varchar(30) NULL,
    [eventmanagementrate] float NULL,
    [eventmanagementamount] float NULL,
    [equipmentmodified] bit NULL,
    [crewstatuscolumn] tinyint NULL,
    [bbookingiscomplete] bit NULL,
    [DiscountOverride] bit NULL,
    [MasterBillingID] int NULL,
    [DateLastModified] datetime NULL,
    [vvenuename] varchar(50) NULL,
    [vcontactname] varchar(50) NULL,
    [vcontactid] decimal(10,0) NULL,
    [address1] varchar(50) NULL,
    [address2] varchar(50) NULL,
    [city] varchar(50) NULL,
    [state] varchar(50) NULL,
    [country] varchar(50) NULL,
    [zipcode] varchar(12) NULL,
    [venuetype] tinyint NULL,
    [vp1cc] varchar(10) NULL,
    [vp1ac] varchar(10) NULL,
    [vp1number] varchar(16) NULL,
    [vp2cc] varchar(10) NULL,
    [vp2ac] varchar(10) NULL,
    [vp2number] varchar(16) NULL,
    [vp2extension] varchar(8) NULL,
    [vp3cc] varchar(10) NULL,
    [vp3ac] varchar(10) NULL,
    [vp3number] varchar(16) NULL,
    [defaultfolder] varchar(255) NULL,
    [webpage] varchar(80) NULL,
    [cid] decimal(10,0) NOT NULL,
    [customer_code] varchar(30) NULL,
    [postaladdress1] char(50) NULL,
    [postaladdress2] char(50) NULL,
    [postaladdress3] char(50) NULL,
    [postalpostcode] char(12) NULL,
    [ccurrencystr] varchar(5) NULL,
    [usespricetablev71] tinyint NULL,
    [post_code] char(12) NULL,
    [csales_tax_no] char(25) NULL,
    [account_type] tinyint NULL,
    [industry_type] varchar(8) NULL,
    [defaultinsurance_type] tinyint NULL,
    [hire_tax_exempt] char(1) NULL,
    [price_customer_pays] tinyint NULL,
    [customer_number] char(6) NULL,
    [stop_credit] tinyint NULL,
    [last_bk_seq] varchar(5) NULL,
    [credit_limit] float NULL,
    [CURRENT] float NULL,
    [seven_days] float NULL,
    [fourteen_days] float NULL,
    [twenty_one_days] float NULL,
    [payments_mtd] float NULL,
    [lastpmtdate] datetime NULL,
    [last_pmt_amt] float NULL,
    [monthly_cycle_billing_basis] tinyint NULL,
    [csalesperson] char(30) NULL,
    [contactv6] varchar(35) NULL,
    [organisationv6] varchar(50) NULL,
    [address_l1v6] char(50) NULL,
    [address_l2v6] char(50) NULL,
    [address_l3v6] char(50) NULL,
    [address_l3v6_ws] varchar(8000) NULL,
    [webaddress] varchar(80) NULL,
    [emailaddress] varchar(80) NULL,
    [cpaymethod] varchar(16) NULL,
    [lastbalupdate] datetime NULL,
    [custcdate] datetime NULL,
    [streetstate] varchar(50) NULL,
    [streetcountry] varchar(50) NULL,
    [postalstate] varchar(50) NULL,
    [postalcountry] varchar(50) NULL,
    [insuredfromdate] datetime NULL,
    [insuredtodate] datetime NULL,
    [field1] varchar(25) NULL,
    [bponumrequired] bit NULL,
    [faxcalltype] tinyint NULL,
    [faxdialareacode] bit NULL,
    [faxcountrycode] varchar(10) NULL,
    [faxareacode] varchar(10) NULL,
    [faxdigits] varchar(16) NULL,
    [phone1countrycode] varchar(10) NULL,
    [phone1areacode] varchar(10) NULL,
    [phone1digits] varchar(16) NULL,
    [phone1ext] varchar(8) NULL,
    [phone2countrycode] varchar(10) NULL,
    [phone2areacode] varchar(10) NULL,
    [phone2digits] varchar(16) NULL,
    [phone2ext] varchar(8) NULL,
    [campaignid] int NULL,
    [ilink_contactid] decimal(10,0) NULL,
    [CustomerDiscountRate] float NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [event_desc] char(32) NULL,
    [showdaysCharged] float NULL,
    [Contact_ContactName] varchar(35) NULL,
    [Contact_ContactFirstname] varchar(25) NULL,
    [Contact_Contactsurname] varchar(35) NULL,
    [Contact_FaxAreaCode] varchar(10) NULL,
    [Contact_Fax] varchar(16) NULL,
    [Contact_FaxCountryCode] varchar(10) NULL,
    [Contact_Cell] varchar(16) NULL,
    [Contact_CellAreaCode] varchar(10) NULL,
    [Contact_CellCountryCode] varchar(10) NULL,
    [Contact_email] varchar(80) NULL,
    [billcust_contactV6] varchar(35) NULL,
    [billcust_Address_l1V6] char(50) NULL,
    [billcust_Address_l2V6] char(50) NULL,
    [billcust_Address_l3V6] char(50) NULL,
    [billcust_organisationv6] varchar(50) NULL,
    [billcust_post_code] char(12) NULL,
    [billcust_Phone1CountryCode] varchar(10) NULL,
    [billcust_Phone1AreaCode] varchar(10) NULL,
    [billcust_Phone1Digits] varchar(16) NULL,
    [billcust_emailAddress] varchar(80) NULL,
    [status_text] varchar(9) NULL,
    [BookingProgressStatus_text] varchar(12) NULL,
    [PrintedPayTerm_text] varchar(68) NULL,
    [VenueRoomListText] varchar(300) NULL,
    [Instruct_text] text NULL,
    [TotalFreightCharge] float NULL,
    [TaxAuthority1_name] varchar(24) NULL,
    [TaxAuthority2_name] varchar(24) NULL,
    [Salesperson_Name] varchar(45) NULL,
    [Salesperson_Email] varchar(80) NULL,
    [Salesperson_Position] varchar(50) NULL,
    [Salesperson_CellCountryCode] varchar(10) NULL,
    [Salesperson_CellAreaCode] varchar(10) NULL,
    [Salesperson_Cell] varchar(16) NULL,
    [ProjectMan_Name] varchar(45) NULL,
    [ProjectMan_Email] varchar(80) NULL,
    [ProjectMan_Position] varchar(50) NULL,
    [ProjectMan_CellCountryCode] varchar(10) NULL,
    [ProjectMan_CellAreaCode] varchar(10) NULL,
    [ProjectMan_Cell] varchar(16) NULL,
    [PayTermName] varchar(40) NULL,
    [InvStageName] varchar(20) NULL,
    [MasterBillingName] varchar(50) NULL,
    [text_line] nvarchar(MAX) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRevenueRep')
BEGIN
CREATE TABLE [vwRevenueRep] (
    [booking_no] varchar(35) NULL,
    [OrganisationV6] varchar(50) NULL,
    [Customer_code] varchar(30) NULL,
    [rDate] datetime NULL,
    [dDate] datetime NULL,
    [ShowSdate] datetime NULL,
    [ShowEdate] datetime NULL,
    [BookingProgressStatus] tinyint NULL,
    [Inv_date] datetime NULL,
    [price_quoted] float NULL,
    [Salesperson] varchar(30) NULL,
    [CSalesperson] char(30) NULL,
    [invoice_no] decimal(19,0) NULL,
    [booking_type_v32] tinyint NULL,
    [showName] varchar(50) NULL,
    [invoiced] char(1) NULL,
    [InvoiceStage] int NULL,
    [division] tinyint NULL,
    [From_locn] int NULL,
    [return_to_locn] int NULL,
    [Trans_to_locn] int NULL,
    [currencyStr] varchar(5) NULL,
    [Referral] varchar(50) NULL,
    [EventType] varchar(20) NULL,
    [industry_type] varchar(8) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRoadcase')
BEGIN
CREATE TABLE [vwRoadcase] (
    [ID] decimal(10,0) NOT NULL,
    [parent_basecode] char(30) NULL,
    [asset_barcode] char(30) NULL,
    [NonBarLocn] int NULL,
    [PackedBy] decimal(10,0) NULL,
    [DateTimePacked] datetime NULL,
    [Booking_no] varchar(35) NULL,
    [Qty] int NULL,
    [CaseType] tinyint NULL,
    [NonBarProductCode] varchar(30) NULL,
    [asset_barcode_ID] int NULL,
    [parent_basecode_ID] int NULL,
    [Floating] bit NULL,
    [ParentProductCode] char(30) NULL,
    [AssetProductCode] char(30) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRoadcasePickList')
BEGIN
CREATE TABLE [vwRoadcasePickList] (
    [ID] decimal(10,0) NOT NULL,
    [parent_basecode] char(30) NULL,
    [asset_barcode] char(30) NULL,
    [NonBarLocn] int NULL,
    [PackedBy] decimal(10,0) NULL,
    [DateTimePacked] datetime NULL,
    [Booking_no] varchar(35) NULL,
    [Qty] int NULL,
    [CaseType] tinyint NULL,
    [NonBarProductCode] varchar(30) NULL,
    [asset_barcode_ID] int NULL,
    [parent_basecode_ID] int NOT NULL,
    [Floating] bit NULL,
    [ListType] tinyint NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRoadcases')
BEGIN
CREATE TABLE [vwRoadcases] (
    [ASSET_CODE] char(30) NULL,
    [product_code] char(30) NULL,
    [STOCK_NUMBER] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwRPWebContact')
BEGIN
CREATE TABLE [vwRPWebContact] (
    [ID] decimal(10,0) NOT NULL,
    [CustCodeLink] varchar(30) NULL,
    [Contactname] varchar(35) NULL,
    [firstname] varchar(25) NULL,
    [surname] varchar(35) NULL,
    [nameKeyField] varchar(11) NULL,
    [position] varchar(50) NULL,
    [busname] varchar(50) NULL,
    [Adr1] varchar(50) NULL,
    [Adr2] varchar(50) NULL,
    [Adr3] varchar(50) NULL,
    [Postcode] varchar(12) NULL,
    [Phone1] varchar(16) NULL,
    [Phone2] varchar(16) NULL,
    [Fax] varchar(16) NULL,
    [Webpage] varchar(80) NULL,
    [Email] varchar(80) NULL,
    [driversLicNo] varchar(20) NULL,
    [OtherID] varchar(30) NULL,
    [specialty] varchar(60) NULL,
    [PictureDatafile] varchar(240) NULL,
    [lastBookDate] datetime NULL,
    [MidName] varchar(35) NULL,
    [Cell] varchar(16) NULL,
    [Ext1] varchar(8) NULL,
    [Ext2] varchar(8) NULL,
    [Active] char(1) NULL,
    [MailList] char(1) NULL,
    [DecMaker] char(1) NULL,
    [LastContact] datetime NULL,
    [LastAttempt] datetime NULL,
    [Department] varchar(50) NULL,
    [SourceID] decimal(10,0) NULL,
    [CreateDate] datetime NULL,
    [LastUpdate] datetime NULL,
    [ReferralName] varchar(50) NULL,
    [Field1] varchar(50) NULL,
    [Field2] varchar(50) NULL,
    [Field3] varchar(50) NULL,
    [Field4] varchar(50) NULL,
    [Field5] varchar(50) NULL,
    [Field6] varchar(50) NULL,
    [Field7] datetime NULL,
    [Field8] datetime NULL,
    [AskFor] varchar(20) NULL,
    [CreditCardName] varchar(20) NULL,
    [CreditCardNumber] varchar(250) NULL,
    [expMonth] varchar(250) NULL,
    [expYear] varchar(250) NULL,
    [CardStreet1] varchar(250) NULL,
    [CardStreet2] varchar(250) NULL,
    [CardCity] varchar(250) NULL,
    [CardState] varchar(250) NULL,
    [CardPostCode] varchar(250) NULL,
    [CreditCardIdNo] varchar(250) NULL,
    [Sendme_faxes] char(1) NULL,
    [Sendme_emails] char(1) NULL,
    [CardHolder_Name] varchar(250) NULL,
    [SalesPerson_Code] varchar(30) NULL,
    [SalesAssignEndDate] datetime NULL,
    [Country] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] decimal(18,0) NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [bDriver] bit NOT NULL,
    [bFreeLanceContact] bit NOT NULL,
    [JobTitle] varchar(15) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [FaxDialAreaCode] bit NULL,
    [FaxCallType] tinyint NULL,
    [SubRentalVendor] varchar(30) NULL,
    [AgencyContact] bit NULL,
    [UpdateVendorContact] bit NULL,
    [username] varchar(50) NULL,
    [password] varchar(256) NULL,
    [TimeZone] varchar(30) NULL,
    [Utc] smallint NULL,
    [Culture] nvarchar(10) NOT NULL,
    [RPwebservicesActive] bit NOT NULL,
    [RPWSDefaultOpID] int NULL,
    [ProjectManager] bit NULL,
    [RPWSCrewManager] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwScheduleGridByBooking')
BEGIN
CREATE TABLE [vwScheduleGridByBooking] (
    [ID] decimal(10,0) NOT NULL,
    [BookingID] decimal(10,0) NOT NULL,
    [booking_type_v32] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [showStartTime] varchar(4) NULL,
    [From_locn] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [VenueRoom] varchar(35) NULL,
    [delivery_viav71] int NULL,
    [pickup_viaV71] int NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [showName] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [Salesperson] varchar(30) NULL,
    [BookingProgressStatus] tinyint NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [status] tinyint NULL,
    [SDate] datetime NULL,
    [ADelDate] datetime NULL,
    [PickupRetDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [booking_no] varchar(35) NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [subrooms] char(12) NULL,
    [HorCCroom] int NULL,
    [bookingPrinted] char(1) NULL,
    [pickup_time] varchar(6) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [LoadDateTime] datetime NULL,
    [UnLoadDateTime] datetime NULL,
    [BookBayNo] int NULL,
    [delivery_time] varchar(6) NULL,
    [ShowEdate] datetime NULL,
    [ShowSdate] datetime NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [Priority] int NULL,
    [dtExpected_ReturnDate] datetime NOT NULL,
    [vcExpected_ReturnTime] varchar(4) NOT NULL,
    [EquipmentModified] bit NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [DateLastModified] datetime NULL,
    [printedDate] datetime NULL,
    [Auth_agentv6] varchar(50) NULL,
    [address_l1V6] varchar(50) NULL,
    [address_l2V6] varchar(50) NULL,
    [address_l3V6] varchar(50) NULL,
    [P1CC] varchar(10) NULL,
    [P1AC] varchar(10) NULL,
    [P1Digits] varchar(16) NULL,
    [P1Ext] varchar(8) NULL,
    [P2CC] varchar(10) NULL,
    [P2AC] varchar(10) NULL,
    [P2Digits] varchar(16) NULL,
    [P2Ext] varchar(8) NULL,
    [FaxCC] varchar(10) NULL,
    [FaxAC] varchar(10) NULL,
    [AFaxDigits] varchar(16) NULL,
    [AState] varchar(50) NULL,
    [ACountry] varchar(50) NULL,
    [APostCode] varchar(12) NULL,
    [event_desc] char(32) NULL,
    [Field1] varchar(25) NULL,
    [OrganisationV6] varchar(50) NULL,
    [Cadr1] char(50) NULL,
    [Cadr2] char(50) NULL,
    [Cadr3] char(50) NULL,
    [StreetState] varchar(50) NULL,
    [StreetCountry] varchar(50) NULL,
    [CPostCode] char(12) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [Phone2Ext] varchar(8) NULL,
    [CustSalesperson] char(30) NULL,
    [Vadr1] varchar(50) NULL,
    [Vadr2] varchar(50) NULL,
    [Vadr3] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [Vfax] varchar(16) NULL,
    [VCntryCode] varchar(10) NULL,
    [VPostCode] varchar(12) NULL,
    [VArCode] varchar(10) NULL,
    [Vphone1] varchar(16) NULL,
    [VendorName] varchar(50) NULL,
    [VendPhone1ID] decimal(18,0) NULL,
    [VendPhone2ID] decimal(18,0) NULL,
    [VendFAxID] decimal(18,0) NULL,
    [VRroomname] varchar(1) NOT NULL,
    [HeadBayNo] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwScheduleGridByHeading')
BEGIN
CREATE TABLE [vwScheduleGridByHeading] (
    [ID] decimal(10,0) NOT NULL,
    [BookingID] decimal(10,0) NOT NULL,
    [booking_type_v32] tinyint NULL,
    [Assigned_to_v61] varchar(35) NULL,
    [showStartTime] varchar(4) NULL,
    [From_locn] int NULL,
    [Trans_to_locn] int NULL,
    [return_to_locn] int NULL,
    [VenueRoom] varchar(35) NULL,
    [delivery_viav71] int NULL,
    [pickup_viaV71] int NULL,
    [invoice_no] decimal(19,0) NULL,
    [event_code] char(30) NULL,
    [showName] varchar(50) NULL,
    [operatorsID] decimal(19,0) NULL,
    [Salesperson] varchar(30) NULL,
    [BookingProgressStatus] tinyint NULL,
    [dDate] datetime NULL,
    [rDate] datetime NULL,
    [status] tinyint NULL,
    [SDate] datetime NULL,
    [ADelDate] datetime NULL,
    [PickupRetDate] datetime NULL,
    [del_time_h] tinyint NULL,
    [del_time_m] tinyint NULL,
    [booking_no] varchar(35) NULL,
    [ret_time_h] tinyint NULL,
    [ret_time_m] tinyint NULL,
    [subrooms] char(12) NULL,
    [HorCCroom] int NULL,
    [bookingPrinted] char(1) NULL,
    [pickup_time] char(6) NULL,
    [freightServiceDel] tinyint NULL,
    [freightServiceRet] tinyint NULL,
    [DelZone] int NULL,
    [RetZone] int NULL,
    [delivery_time] char(6) NULL,
    [ShowEdate] datetime NULL,
    [ShowSdate] datetime NULL,
    [ProjectManager] varchar(8) NOT NULL,
    [Priority] int NULL,
    [EquipmentModified] bit NULL,
    [DeliveryDateOn] bit NOT NULL,
    [PickupDateOn] bit NOT NULL,
    [LoadDateTime] datetime NULL,
    [UnLoadDateTime] datetime NULL,
    [DateLastModified] datetime NULL,
    [printedDate] datetime NULL,
    [BookBayNo] int NULL,
    [headdh] tinyint NULL,
    [headdm] tinyint NULL,
    [headrh] tinyint NULL,
    [headrm] tinyint NULL,
    [headdd] datetime NULL,
    [headrd] datetime NULL,
    [headno] tinyint NULL,
    [headdes] varchar(79) NULL,
    [Heading_No] tinyint NULL,
    [venueroomID] int NULL,
    [HeadBayNo] int NULL,
    [Auth_agentV6] varchar(50) NULL,
    [address_l1V6] varchar(50) NULL,
    [address_l2V6] varchar(50) NULL,
    [address_l3V6] varchar(50) NULL,
    [P1CC] varchar(10) NULL,
    [P1AC] varchar(10) NULL,
    [P1Digits] varchar(16) NULL,
    [P1Ext] varchar(8) NULL,
    [P2CC] varchar(10) NULL,
    [P2AC] varchar(10) NULL,
    [P2Digits] varchar(16) NULL,
    [P2Ext] varchar(8) NULL,
    [FaxCC] varchar(10) NULL,
    [FaxAC] varchar(10) NULL,
    [AFaxDigits] varchar(16) NULL,
    [AState] varchar(50) NULL,
    [ACountry] varchar(50) NULL,
    [APostCode] varchar(12) NULL,
    [event_desc] char(32) NULL,
    [OrganisationV6] varchar(50) NULL,
    [Cadr1] char(50) NULL,
    [Cadr2] char(50) NULL,
    [Cadr3] char(50) NULL,
    [StreetState] varchar(50) NULL,
    [StreetCountry] varchar(50) NULL,
    [CPostCode] char(12) NULL,
    [Field1] varchar(25) NULL,
    [Phone1CountryCode] varchar(10) NULL,
    [Phone1AreaCode] varchar(10) NULL,
    [Phone1Digits] varchar(16) NULL,
    [Phone1Ext] varchar(8) NULL,
    [FaxCountryCode] varchar(10) NULL,
    [FaxAreaCode] varchar(10) NULL,
    [FaxDigits] varchar(16) NULL,
    [Phone2CountryCode] varchar(10) NULL,
    [Phone2AreaCode] varchar(10) NULL,
    [Phone2Digits] varchar(16) NULL,
    [Phone2Ext] varchar(8) NULL,
    [CustSalesperson] char(30) NULL,
    [Vadr1] varchar(50) NULL,
    [Vadr2] varchar(50) NULL,
    [Vadr3] varchar(50) NULL,
    [State] varchar(50) NULL,
    [Country] varchar(50) NULL,
    [Vfax] varchar(16) NULL,
    [VCntryCode] varchar(10) NULL,
    [VPostCode] varchar(12) NULL,
    [VArCode] varchar(10) NULL,
    [Vphone1] varchar(16) NULL,
    [VendorName] varchar(50) NULL,
    [VendPhone1ID] decimal(18,0) NULL,
    [VendPhone2ID] decimal(18,0) NULL,
    [VendFAxID] decimal(18,0) NULL,
    [VRroomname] char(35) NULL,
    [HDStatus] int NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwShortCrew')
BEGIN
CREATE TABLE [vwShortCrew] (
    [person] char(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [booking_no] varchar(50) NULL,
    [ShortType] int NOT NULL,
    [From_locn] int NULL,
    [Conflict] int NULL,
    [ID] decimal(10,0) NOT NULL,
    [showName] varchar(50) NULL,
    [CustomActivityType] decimal(19,0) NULL,
    [Declined] int NULL,
    [CrewCode] varchar(30) NULL,
    [CrewDescription] varchar(50) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwShortCrewActivities')
BEGIN
CREATE TABLE [vwShortCrewActivities] (
    [person] char(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] varchar(2) NULL,
    [del_time_min] varchar(2) NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] varchar(2) NULL,
    [return_time_min] varchar(2) NULL,
    [booking_no] varchar(50) NULL,
    [ShortType] int NOT NULL,
    [From_locn] int NOT NULL,
    [Conflict] int NOT NULL,
    [ID] decimal(10,0) NOT NULL,
    [showName] varchar(1) NOT NULL,
    [CustomActivityType] decimal(19,0) NULL,
    [Declined] int NOT NULL,
    [CrewCode] varchar(1) NOT NULL,
    [CrewDescription] varchar(1) NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwShortCrewBooking')
BEGIN
CREATE TABLE [vwShortCrewBooking] (
    [person] char(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [booking_no] varchar(35) NULL,
    [ShortType] int NOT NULL,
    [From_locn] int NULL,
    [Conflict] int NOT NULL,
    [ID] decimal(10,0) NOT NULL,
    [showName] varchar(50) NULL,
    [CustomActivityType] int NOT NULL,
    [Declined] int NOT NULL,
    [CrewCode] char(30) NULL,
    [CrewDescription] varchar(50) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwShortCrewItems')
BEGIN
CREATE TABLE [vwShortCrewItems] (
    [person] char(30) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [booking_no] varchar(35) NULL,
    [ShortType] int NOT NULL,
    [From_locn] int NULL,
    [Conflict] bit NULL,
    [ID] decimal(10,0) NOT NULL,
    [showName] varchar(50) NULL,
    [CustomActivityType] int NOT NULL,
    [Declined] tinyint NULL,
    [CrewCode] char(30) NULL,
    [CrewDescription] varchar(50) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwShortCrewNames')
BEGIN
CREATE TABLE [vwShortCrewNames] (
    [person] char(30) NULL,
    [firstname] varchar(25) NULL,
    [surname] varchar(35) NULL,
    [FirstDate] datetime NULL,
    [del_time_hour] tinyint NULL,
    [del_time_min] tinyint NULL,
    [RetnDate] datetime NULL,
    [return_time_hour] tinyint NULL,
    [return_time_min] tinyint NULL,
    [booking_no] varchar(50) NULL,
    [ShortType] int NOT NULL,
    [From_locn] int NULL,
    [Conflict] int NULL,
    [ID] decimal(10,0) NOT NULL,
    [showName] varchar(50) NULL,
    [CustomActivityType] decimal(19,0) NULL,
    [Declined] int NULL,
    [CrewCode] varchar(30) NULL,
    [CrewDescription] varchar(50) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwSundryAndHist')
BEGIN
CREATE TABLE [vwSundryAndHist] (
    [booking_no_v32] varchar(35) NULL,
    [heading_no] tinyint NULL,
    [seq_no] int NULL,
    [sub_seq_no] int NULL,
    [sundry_desc] varchar(50) NULL,
    [sundry_cost] float NULL,
    [sundry_price] float NULL,
    [GroupSeqNo] int NULL,
    [Discount] float NULL,
    [trans_qty] float NULL,
    [restock_charge] tinyint NULL,
    [RevenueCode] varchar(50) NULL,
    [view_client] bit NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwTechnicianLabour')
BEGIN
CREATE TABLE [vwTechnicianLabour] (
    [product_code] char(30) NULL,
    [descriptionV6] varchar(50) NULL,
    [OLInternalDesc] varchar(50) NULL,
    [ContactID] decimal(10,0) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwTechnicianPO')
BEGIN
CREATE TABLE [vwTechnicianPO] (
    [POBooking_no] varchar(35) NULL,
    [PPONumber] decimal(19,0) NULL,
    [POPrefix] varchar(3) NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwVendors')
BEGIN
CREATE TABLE [vwVendors] (
    [ID] decimal(10,0) NOT NULL,
    [VendorCode] varchar(30) NULL,
    [VendorContact] varchar(35) NULL,
    [VendorName] varchar(50) NULL,
    [Vadr1] char(50) NULL,
    [Vadr2] char(50) NULL,
    [Vadr3] char(50) NULL,
    [Vpostcode] char(12) NULL,
    [Vphone1] varchar(16) NULL,
    [Vphone2] varchar(16) NULL,
    [Vfax] varchar(16) NULL,
    [Vemail] varchar(80) NULL,
    [Vwebpage] varchar(80) NULL,
    [Vcurrency] varchar(5) NULL,
    [Vaccno] varchar(30) NULL,
    [AreaCode] varchar(10) NULL,
    [CountryCode] varchar(10) NULL,
    [CallType] tinyint NULL,
    [UseAreaCode] bit NULL,
    [FaxExt] varchar(1) NOT NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2Ext] varchar(8) NULL,
    [Country] varchar(50) NULL,
    [State] varchar(50) NULL,
    [TaxAuthority1] int NULL,
    [taxAuthority2] int NULL,
    [MinPOAmount] float NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] int NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [DefaultDiscount] float NULL,
    [PaymentTerms] int NULL,
    [DateCreated] datetime NULL,
    [LastBookingSeq] varchar(5) NULL,
    [DisabledCust] char(1) NULL,
    [VendTypeForExporting] int NOT NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [VendorReferenceNumber] char(6) NULL,
    [isCust] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwVendorsAndCust')
BEGIN
CREATE TABLE [vwVendorsAndCust] (
    [ID] decimal(10,0) IDENTITY(NULL,NULL) NOT NULL,
    [VendorCode] varchar(30) NULL,
    [VendorContact] varchar(35) NULL,
    [VendorName] varchar(50) NULL,
    [Vadr1] char(50) NULL,
    [Vadr2] char(50) NULL,
    [Vadr3] char(50) NULL,
    [Vpostcode] char(12) NULL,
    [Vphone1] varchar(16) NULL,
    [Vphone2] varchar(16) NULL,
    [Vfax] varchar(16) NULL,
    [Vemail] varchar(80) NULL,
    [Vwebpage] varchar(80) NULL,
    [Vcurrency] varchar(5) NULL,
    [Vaccno] varchar(30) NULL,
    [AreaCode] varchar(10) NULL,
    [CountryCode] varchar(10) NULL,
    [CallType] tinyint NULL,
    [UseAreaCode] bit NULL,
    [FaxExt] varchar(1) NOT NULL,
    [Phone1Ext] varchar(8) NULL,
    [Phone2Ext] varchar(8) NULL,
    [Country] varchar(50) NULL,
    [State] varchar(50) NULL,
    [TaxAuthority1] int NULL,
    [taxAuthority2] int NULL,
    [MinPOAmount] int NOT NULL,
    [Phone1ID] decimal(18,0) NOT NULL,
    [Phone2ID] decimal(18,0) NOT NULL,
    [CellID] int NOT NULL,
    [FaxID] decimal(18,0) NOT NULL,
    [DefaultDiscount] float NULL,
    [PaymentTerms] tinyint NULL,
    [DateCreated] datetime NULL,
    [LastBookingSeq] varchar(5) NULL,
    [DisabledCust] char(1) NULL,
    [VendTypeForExporting] int NOT NULL,
    [CellCountryCode] varchar(10) NULL,
    [CellAreaCode] varchar(10) NULL,
    [CellDigits] varchar(16) NULL,
    [VendorReferenceNumber] char(6) NULL,
    [isCust] int NOT NULL
);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vwWebPayData')
BEGIN
CREATE TABLE [vwWebPayData] (
    [ID] decimal(10,0) NOT NULL,
    [currencyStr] char(5) NULL,
    [price_quoted] float NULL,
    [Invoice_cred_no] decimal(19,0) NULL,
    [hire_price] float NULL,
    [webcode] varchar(16) NULL,
    [status] varchar(16) NULL,
    [brand] varchar(32) NULL,
    [created] datetime NULL,
    [last4] varchar(4) NULL,
    [emailAddress] varchar(80) NULL,
    [StripeID] varchar(24) NULL,
    [customer_code] varchar(30) NULL
);
END
GO

-- ============================================
-- Non-clustered Indexes
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_AggregatedCounter_ExpireAt' AND object_id = OBJECT_ID('AggregatedCounter'))
CREATE NONCLUSTERED INDEX [IX_HangFire_AggregatedCounter_ExpireAt] ON [AggregatedCounter] ([ExpireAt]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_Hash_ExpireAt' AND object_id = OBJECT_ID('Hash'))
CREATE NONCLUSTERED INDEX [IX_HangFire_Hash_ExpireAt] ON [Hash] ([ExpireAt]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_Job_ExpireAt' AND object_id = OBJECT_ID('Job'))
CREATE NONCLUSTERED INDEX [IX_HangFire_Job_ExpireAt] ON [Job] ([StateName], [ExpireAt]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_Job_StateName' AND object_id = OBJECT_ID('Job'))
CREATE NONCLUSTERED INDEX [IX_HangFire_Job_StateName] ON [Job] ([StateName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_List_ExpireAt' AND object_id = OBJECT_ID('List'))
CREATE NONCLUSTERED INDEX [IX_HangFire_List_ExpireAt] ON [List] ([ExpireAt]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_Server_LastHeartbeat' AND object_id = OBJECT_ID('Server'))
CREATE NONCLUSTERED INDEX [IX_HangFire_Server_LastHeartbeat] ON [Server] ([LastHeartbeat]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_Set_ExpireAt' AND object_id = OBJECT_ID('Set'))
CREATE NONCLUSTERED INDEX [IX_HangFire_Set_ExpireAt] ON [Set] ([ExpireAt]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_Set_Score' AND object_id = OBJECT_ID('Set'))
CREATE NONCLUSTERED INDEX [IX_HangFire_Set_Score] ON [Set] ([Key], [Score]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_HangFire_State_CreatedAt' AND object_id = OBJECT_ID('State'))
CREATE NONCLUSTERED INDEX [IX_HangFire_State_CreatedAt] ON [State] ([CreatedAt]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxActivityContactID' AND object_id = OBJECT_ID('tblActivity'))
CREATE NONCLUSTERED INDEX [idxActivityContactID] ON [tblActivity] ([ContactID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblActivityAlarmSet' AND object_id = OBJECT_ID('tblActivity'))
CREATE NONCLUSTERED INDEX [idxtblActivityAlarmSet] ON [tblActivity] ([AlarmSet]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblActivityResult' AND object_id = OBJECT_ID('tblActivityResult'))
CREATE NONCLUSTERED INDEX [ix_tblActivityResult] ON [tblActivityResult] ([ActivityResult]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblActivityType' AND object_id = OBJECT_ID('tblActivityType'))
CREATE NONCLUSTERED INDEX [ix_tblActivityType] ON [tblActivityType] ([Points]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblAgentsDesc' AND object_id = OBJECT_ID('tblAgents'))
CREATE NONCLUSTERED INDEX [IDXtblAgentsDesc] ON [tblAgents] ([Auth_agentV6]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxAsset01Locn' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [idxAsset01Locn] ON [tblAsset01] ([locn]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxAsset01SerialNo' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [idxAsset01SerialNo] ON [tblAsset01] ([SERIAL_NO]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxCAsset01AssetCode' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [idxCAsset01AssetCode] ON [tblAsset01] ([ASSET_CODE]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAsset01RFIDTag' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [idxtblAsset01RFIDTag] ON [tblAsset01] ([RFIDTag]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXttblAsset01ProductCode' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [IDXttblAsset01ProductCode] ON [tblAsset01] ([PRODUCT_COde]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXttblAsset01ProductCodeStkNo' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [IDXttblAsset01ProductCodeStkNo] ON [tblAsset01] ([PRODUCT_COde], [STOCK_NUMBER]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNC_Asset01AssetCode' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [ixNC_Asset01AssetCode] ON [tblAsset01] ([BOOKING_NO], [locn], [PRODUCT_COde], [iDisposalType], [TestFrequencyDays], [LastTestDate], [NextTestDate], [OperationalStatus], [DisDate], [ServiceStatus], [ASSET_CODE]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNC_Asset01ID' AND object_id = OBJECT_ID('tblAsset01'))
CREATE NONCLUSTERED INDEX [ixNC_Asset01ID] ON [tblAsset01] ([ASSET_CODE], [BOOKING_NO], [locn], [PRODUCT_COde], [iDisposalType], [TestFrequencyDays], [LastTestDate], [NextTestDate], [OperationalStatus], [DisDate], [ServiceStatus], [ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNC_tblAssetMovementProcessPrepIdx' AND object_id = OBJECT_ID('tblAssetMovement'))
CREATE NONCLUSTERED INDEX [ixNC_tblAssetMovementProcessPrepIdx] ON [tblAssetMovement] ([ID], [BookingNo], [ItemTranID], [TransType], [CompleteStatus], [ProductCode], [StockNumber]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAssetranActInDate' AND object_id = OBJECT_ID('tblAssetran'))
CREATE NONCLUSTERED INDEX [idxtblAssetranActInDate] ON [tblAssetran] ([ActInDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAssetranActOutDate' AND object_id = OBJECT_ID('tblAssetran'))
CREATE NONCLUSTERED INDEX [idxtblAssetranActOutDate] ON [tblAssetran] ([ActOutDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblAsseTranProductCodeStkNo' AND object_id = OBJECT_ID('tblAssetran'))
CREATE NONCLUSTERED INDEX [IDXtblAsseTranProductCodeStkNo] ON [tblAssetran] ([booking_no], [product_code], [stock_number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAssetranReturnNo' AND object_id = OBJECT_ID('tblAssetran'))
CREATE NONCLUSTERED INDEX [idxtblAssetranReturnNo] ON [tblAssetran] ([ReturnNo]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAssetranReturnType' AND object_id = OBJECT_ID('tblAssetran'))
CREATE NONCLUSTERED INDEX [idxtblAssetranReturnType] ON [tblAssetran] ([ReturnType]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAssetranStock_number' AND object_id = OBJECT_ID('tblAssetran'))
CREATE NONCLUSTERED INDEX [idxtblAssetranStock_number] ON [tblAssetran] ([stock_number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDX_TBLATTACH_CUSTOMERCODE' AND object_id = OBJECT_ID('tblAttach'))
CREATE NONCLUSTERED INDEX [IDX_TBLATTACH_CUSTOMERCODE] ON [tblAttach] ([CustomerCode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDX_TBLATTACH_FILENAME' AND object_id = OBJECT_ID('tblAttach'))
CREATE NONCLUSTERED INDEX [IDX_TBLATTACH_FILENAME] ON [tblAttach] ([filename]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxtblAttachBooking_no' AND object_id = OBJECT_ID('tblAttach'))
CREATE NONCLUSTERED INDEX [IdxtblAttachBooking_no] ON [tblAttach] ([booking_no], [filename]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAttachOtherCode' AND object_id = OBJECT_ID('tblAttach'))
CREATE NONCLUSTERED INDEX [idxtblAttachOtherCode] ON [tblAttach] ([OtherCode], [aType]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxtblAttachProjectCode' AND object_id = OBJECT_ID('tblAttach'))
CREATE NONCLUSTERED INDEX [IdxtblAttachProjectCode] ON [tblAttach] ([project_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblAttachments' AND object_id = OBJECT_ID('tblAttachments'))
CREATE NONCLUSTERED INDEX [ix_tblAttachments] ON [tblAttachments] ([Path]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'cidxAuditBookingNo' AND object_id = OBJECT_ID('tblAudit'))
CREATE NONCLUSTERED INDEX [cidxAuditBookingNo] ON [tblAudit] ([booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxAuditAuditType' AND object_id = OBJECT_ID('tblAudit'))
CREATE NONCLUSTERED INDEX [idxAuditAuditType] ON [tblAudit] ([audit_type]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblAuditInvoice_no' AND object_id = OBJECT_ID('tblAudit'))
CREATE NONCLUSTERED INDEX [idxtblAuditInvoice_no] ON [tblAudit] ([invoice_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblAuditDateF' AND object_id = OBJECT_ID('tblAudit'))
CREATE NONCLUSTERED INDEX [IX_tblAuditDateF] ON [tblAudit] ([DateF]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Billseq' AND object_id = OBJECT_ID('tblBill'))
CREATE NONCLUSTERED INDEX [idx_Billseq] ON [tblBill] ([parent_code], [sub_seq_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblBillProductCode' AND object_id = OBJECT_ID('tblBill'))
CREATE NONCLUSTERED INDEX [idxtblBillProductCode] ON [tblBill] ([product_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblBookingCrewInfo' AND object_id = OBJECT_ID('tblBookingCrewInfo'))
CREATE NONCLUSTERED INDEX [IX_tblBookingCrewInfo] ON [tblBookingCrewInfo] ([Booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'cstUniqueBookingno' AND object_id = OBJECT_ID('tblbookings'))
CREATE UNIQUE NONCLUSTERED INDEX [cstUniqueBookingno] ON [tblbookings] ([booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_BKinvnoc' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idx_BKinvnoc] ON [tblbookings] ([invoice_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_bkorderno' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idx_bkorderno] ON [tblbookings] ([order_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDX_BookingGridRefresh' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [IDX_BookingGridRefresh] ON [tblbookings] ([booking_type_v32], [BookingProgressStatus], [ID], [booking_no], [order_no], [price_quoted], [docs_produced], [status], [invoiced], [invoice_no], [event_code], [del_time_h], [del_time_m], [ret_time_h], [ret_time_m], [division]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Project' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idx_Project] ON [tblbookings] ([event_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_RetDate' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idx_RetDate] ON [tblbookings] ([rDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_showname' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idx_showname] ON [tblbookings] ([showName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsBookingId' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsBookingId] ON [tblbookings] ([HourBooked], [MinBooked], [SecBooked], [Assigned_to_v61], [order_date], [DeprepDateTime], [DeprepOn], [booking_no], [ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsBookingNo' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsBookingNo] ON [tblbookings] ([HourBooked], [MinBooked], [SecBooked], [Assigned_to_v61], [order_date], [DeprepDateTime], [DeprepOn], [ID], [booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsBookingProgressStatus' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsBookingProgressStatus] ON [tblbookings] ([BookingProgressStatus]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsBookingType' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsBookingType] ON [tblbookings] ([booking_type_v32], [Assigned_to_v61]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsConByDate' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsConByDate] ON [tblbookings] ([ConByDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsCustID' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsCustID] ON [tblbookings] ([CustID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsSalesperson' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsSalesperson] ON [tblbookings] ([Salesperson]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingsVenueID' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [idxBookingsVenueID] ON [tblbookings] ([VenueID], [booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblBookingsBKFromLocn' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [IDXtblBookingsBKFromLocn] ON [tblbookings] ([booking_no], [From_locn]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxtblBookingsDDate' AND object_id = OBJECT_ID('tblbookings'))
CREATE NONCLUSTERED INDEX [IdxtblBookingsDDate] ON [tblbookings] ([dDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxBookNoteBookingNo' AND object_id = OBJECT_ID('tblbooknote'))
CREATE NONCLUSTERED INDEX [IdxBookNoteBookingNo] ON [tblbooknote] ([bookingNo]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblCallList' AND object_id = OBJECT_ID('tblCallList'))
CREATE NONCLUSTERED INDEX [ix_tblCallList] ON [tblCallList] ([Completed]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblContactLinks' AND object_id = OBJECT_ID('tblContactLinks'))
CREATE NONCLUSTERED INDEX [ix_tblContactLinks] ON [tblContactLinks] ([ContactID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblContactNote' AND object_id = OBJECT_ID('tblContactNote'))
CREATE NONCLUSTERED INDEX [ix_tblContactNote] ON [tblContactNote] ([ContactID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ContactSkillIDX' AND object_id = OBJECT_ID('tblContactSecondarySkills'))
CREATE NONCLUSTERED INDEX [ContactSkillIDX] ON [tblContactSecondarySkills] ([ContactID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblCrew' AND object_id = OBJECT_ID('tblCrew'))
CREATE NONCLUSTERED INDEX [idxtblCrew] ON [tblCrew] ([product_code_v42]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblCrewBkSeq' AND object_id = OBJECT_ID('tblCrew'))
CREATE NONCLUSTERED INDEX [idxtblCrewBkSeq] ON [tblCrew] ([booking_no_v32], [heading_no], [GroupSeqNo], [seq_no], [sub_seq_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblCrewBookingNo' AND object_id = OBJECT_ID('tblCrew'))
CREATE NONCLUSTERED INDEX [idxtblCrewBookingNo] ON [tblCrew] ([booking_no_v32]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblCrewPerson' AND object_id = OBJECT_ID('tblCrew'))
CREATE NONCLUSTERED INDEX [idxtblCrewPerson] ON [tblCrew] ([person]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Currate' AND object_id = OBJECT_ID('tblCurrate'))
CREATE UNIQUE NONCLUSTERED INDEX [idx_Currate] ON [tblCurrate] ([cur_string], [DateF], [ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Currency' AND object_id = OBJECT_ID('tblCurrency'))
CREATE UNIQUE NONCLUSTERED INDEX [Currency] ON [tblCurrency] ([cur_string], [ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDX_CUSTCONTACT' AND object_id = OBJECT_ID('tblCust'))
CREATE NONCLUSTERED INDEX [IDX_CUSTCONTACT] ON [tblCust] ([contactV6]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblCustIndustryType' AND object_id = OBJECT_ID('tblCust'))
CREATE NONCLUSTERED INDEX [IDXtblCustIndustryType] ON [tblCust] ([industry_type]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblCustName' AND object_id = OBJECT_ID('tblCust'))
CREATE NONCLUSTERED INDEX [IDXtblCustName] ON [tblCust] ([OrganisationV6]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblCustPost_code' AND object_id = OBJECT_ID('tblCust'))
CREATE NONCLUSTERED INDEX [idxtblCustPost_code] ON [tblCust] ([post_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblCustStreetState' AND object_id = OBJECT_ID('tblCust'))
CREATE NONCLUSTERED INDEX [idxtblCustStreetState] ON [tblCust] ([StreetState]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Unique_CustomerCode' AND object_id = OBJECT_ID('tblCust'))
CREATE UNIQUE NONCLUSTERED INDEX [Unique_CustomerCode] ON [tblCust] ([Customer_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxCustNoteCustCode' AND object_id = OBJECT_ID('tblCustnote'))
CREATE NONCLUSTERED INDEX [IdxCustNoteCustCode] ON [tblCustnote] ([line_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblCustNoteCustC' AND object_id = OBJECT_ID('tblCustnote'))
CREATE NONCLUSTERED INDEX [idxtblCustNoteCustC] ON [tblCustnote] ([customer_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblCustom' AND object_id = OBJECT_ID('tblCustom'))
CREATE NONCLUSTERED INDEX [ix_tblCustom] ON [tblCustom] ([Field1Name]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Deposit' AND object_id = OBJECT_ID('tblDeposit'))
CREATE NONCLUSTERED INDEX [idx_Deposit] ON [tblDeposit] ([booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblEventDelDate' AND object_id = OBJECT_ID('tblEvent'))
CREATE NONCLUSTERED INDEX [IX_tblEventDelDate] ON [tblEvent] ([DelvDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblExpenseCodes' AND object_id = OBJECT_ID('tblExpenseCodes'))
CREATE NONCLUSTERED INDEX [IX_tblExpenseCodes] ON [tblExpenseCodes] ([ExpenseCode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblExpenses' AND object_id = OBJECT_ID('tblExpenses'))
CREATE NONCLUSTERED INDEX [IX_tblExpenses] ON [tblExpenses] ([Booking_No]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxGroupProductType' AND object_id = OBJECT_ID('tblGroup'))
CREATE NONCLUSTERED INDEX [idxGroupProductType] ON [tblGroup] ([GroupProductType]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblGroupSeqNo' AND object_id = OBJECT_ID('tblGroup'))
CREATE NONCLUSTERED INDEX [IDXtblGroupSeqNo] ON [tblGroup] ([seqNo]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Headarc' AND object_id = OBJECT_ID('tblHeadarch'))
CREATE NONCLUSTERED INDEX [idx_Headarc] ON [tblHeadarch] ([booking_no], [heading_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblHeading' AND object_id = OBJECT_ID('tblHeading'))
CREATE NONCLUSTERED INDEX [IDXtblHeading] ON [tblHeading] ([booking_no], [heading_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblHeadingsDelvDate' AND object_id = OBJECT_ID('tblHeading'))
CREATE NONCLUSTERED INDEX [idxtblHeadingsDelvDate] ON [tblHeading] ([DelvDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblHeadingsRetnDate' AND object_id = OBJECT_ID('tblHeading'))
CREATE NONCLUSTERED INDEX [idxtblHeadingsRetnDate] ON [tblHeading] ([RetnDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Arc-cont' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [idx_Arc-cont] ON [tblHistbks] ([contact_nameV6]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Arcddate' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [idx_Arcddate] ON [tblHistbks] ([dDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_ArcPoBk' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [idx_ArcPoBk] ON [tblHistbks] ([order_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Bkinvnoa' AND object_id = OBJECT_ID('tblHistbks'))
CREATE UNIQUE NONCLUSTERED INDEX [idx_Bkinvnoa] ON [tblHistbks] ([invoice_no], [ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Hist-bks' AND object_id = OBJECT_ID('tblHistbks'))
CREATE UNIQUE NONCLUSTERED INDEX [idx_Hist-bks] ON [tblHistbks] ([booking_no], [ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Org2' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [idx_Org2] ON [tblHistbks] ([OrganizationV6]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_RatDateID' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [idx_RatDateID] ON [tblHistbks] ([rDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxHistBksBookingNo' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [IdxHistBksBookingNo] ON [tblHistbks] ([booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxHistBksEventCode' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [IdxHistBksEventCode] ON [tblHistbks] ([event_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxHistBksRDate' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [IdxHistBksRDate] ON [tblHistbks] ([rDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxHistbksSalesperson' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [idxHistbksSalesperson] ON [tblHistbks] ([Salesperson]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxHistBksShowName' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [IdxHistBksShowName] ON [tblHistbks] ([showName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblHistBksBookingType' AND object_id = OBJECT_ID('tblHistbks'))
CREATE NONCLUSTERED INDEX [idxtblHistBksBookingType] ON [tblHistbks] ([booking_type_v32]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblHistCrew' AND object_id = OBJECT_ID('tblHistCrew'))
CREATE NONCLUSTERED INDEX [idxtblHistCrew] ON [tblHistCrew] ([product_code_v42]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblHistCrewBkSeq' AND object_id = OBJECT_ID('tblHistCrew'))
CREATE NONCLUSTERED INDEX [idxtblHistCrewBkSeq] ON [tblHistCrew] ([booking_no_v32], [heading_no], [GroupSeqNo], [seq_no], [sub_seq_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblHistCrewPerson' AND object_id = OBJECT_ID('tblHistCrew'))
CREATE NONCLUSTERED INDEX [idxtblHistCrewPerson] ON [tblHistCrew] ([person]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxhistitmBookingSubLinkID' AND object_id = OBJECT_ID('tblHistitm'))
CREATE NONCLUSTERED INDEX [idxhistitmBookingSubLinkID] ON [tblHistitm] ([booking_no_v32], [SubRentalLinkID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblHistItmBooking_no_v32' AND object_id = OBJECT_ID('tblHistitm'))
CREATE NONCLUSTERED INDEX [idxtblHistItmBooking_no_v32] ON [tblHistitm] ([booking_no_v32]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblHistitm' AND object_id = OBJECT_ID('tblHistitm'))
CREATE NONCLUSTERED INDEX [IX_tblHistitm] ON [tblHistitm] ([product_code_v42]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblHourlyRate' AND object_id = OBJECT_ID('tblHourlyRate'))
CREATE NONCLUSTERED INDEX [IX_tblHourlyRate] ON [tblHourlyRate] ([HourlyRateName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxCInvHeadInvoiceNo' AND object_id = OBJECT_ID('tblInvhead'))
CREATE NONCLUSTERED INDEX [idxCInvHeadInvoiceNo] ON [tblInvhead] ([Invoice_cred_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblInvHeadBookingNo' AND object_id = OBJECT_ID('tblInvhead'))
CREATE NONCLUSTERED INDEX [idxtblInvHeadBookingNo] ON [tblInvhead] ([Booking_No]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxInvMasContactID' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [idxInvMasContactID] ON [tblInvmas] ([ContactID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxInvMasNonTrackedBarcode' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [idxInvMasNonTrackedBarcode] ON [tblInvmas] ([NonTrackedBarcode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxInvMasRoadCaseAndProductCode' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [idxInvMasRoadCaseAndProductCode] ON [tblInvmas] ([prodRoadCase], [product_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblInvMasCategory' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [IDXtblInvMasCategory] ON [tblInvmas] ([category]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblInvMasDesc' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [IDXtblInvMasDesc] ON [tblInvmas] ([descriptionV6]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblInvMasGroup' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [IDXtblInvMasGroup] ON [tblInvmas] ([groupFld]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblInvMasGRoupPConfig' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [IDXtblInvMasGRoupPConfig] ON [tblInvmas] ([groupFld], [product_Config]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblInvMasGroupSeqNo' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [IDXtblInvMasGroupSeqNo] ON [tblInvmas] ([groupFld], [seq_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxtblInvmasPartNumber' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [IdxtblInvmasPartNumber] ON [tblInvmas] ([MfctPartNumber]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblInvMasProductType' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [IDXtblInvMasProductType] ON [tblInvmas] ([product_type_v41]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblInvMasRFIDTag' AND object_id = OBJECT_ID('tblInvmas'))
CREATE NONCLUSTERED INDEX [idxtblInvMasRFIDTag] ON [tblInvmas] ([RFIDTag]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Itemadj' AND object_id = OBJECT_ID('tblItemAdj'))
CREATE NONCLUSTERED INDEX [idx_Itemadj] ON [tblItemAdj] ([ProjectCode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Itemadj2' AND object_id = OBJECT_ID('tblItemAdj'))
CREATE NONCLUSTERED INDEX [idx_Itemadj2] ON [tblItemAdj] ([product_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxItemTran_CheckQtyAvail_OPTIMIZATION' AND object_id = OBJECT_ID('tblItemtran'))
CREATE NONCLUSTERED INDEX [idxItemTran_CheckQtyAvail_OPTIMIZATION] ON [tblItemtran] ([AvailRecFlag], [product_code_v42], [ID], [booking_no_v32], [trans_type_v41], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [From_locn], [Trans_to_locn], [return_to_locn], [bit_field_v41], [TimeBookedH], [TimeBookedM]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxItemTran_CheckQtyAvail_OPTIMIZATION2' AND object_id = OBJECT_ID('tblItemtran'))
CREATE NONCLUSTERED INDEX [idxItemTran_CheckQtyAvail_OPTIMIZATION2] ON [tblItemtran] ([product_code_v42], [AvailRecFlag], [ID], [booking_no_v32], [trans_type_v41], [del_time_hour], [del_time_min], [return_time_hour], [return_time_min], [trans_qty], [From_locn], [Trans_to_locn], [return_to_locn], [bit_field_v41], [TimeBookedH], [TimeBookedM]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxItemTranBookingSubLinkID' AND object_id = OBJECT_ID('tblItemtran'))
CREATE NONCLUSTERED INDEX [idxItemTranBookingSubLinkID] ON [tblItemtran] ([booking_no_v32], [SubRentalLinkID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblItemTranBKSeq' AND object_id = OBJECT_ID('tblItemtran'))
CREATE NONCLUSTERED INDEX [IDXtblItemTranBKSeq] ON [tblItemtran] ([booking_no_v32], [heading_no], [GroupSeqNo], [seq_no], [sub_seq_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblItemTranProductCode' AND object_id = OBJECT_ID('tblItemtran'))
CREATE NONCLUSTERED INDEX [IDXtblItemTranProductCode] ON [tblItemtran] ([product_code_v42]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_tblItemTranExtended_Booking_SubRental' AND object_id = OBJECT_ID('tblItemTranExtended'))
CREATE UNIQUE NONCLUSTERED INDEX [UX_tblItemTranExtended_Booking_SubRental] ON [tblItemTranExtended] ([Booking_no_v32], [IT_SubRentalLinkID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblLabourRates' AND object_id = OBJECT_ID('tblLabourRates'))
CREATE NONCLUSTERED INDEX [IX_tblLabourRates] ON [tblLabourRates] ([ContactID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblLinkCustContactID' AND object_id = OBJECT_ID('tblLinkCustContact'))
CREATE NONCLUSTERED INDEX [idxtblLinkCustContactID] ON [tblLinkCustContact] ([ContactID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblLinkCustContact' AND object_id = OBJECT_ID('tblLinkCustContact'))
CREATE NONCLUSTERED INDEX [ix_tblLinkCustContact] ON [tblLinkCustContact] ([Customer_Code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblLinkShowCompany' AND object_id = OBJECT_ID('tblLinkShowCompany'))
CREATE NONCLUSTERED INDEX [ix_tblLinkShowCompany] ON [tblLinkShowCompany] ([ShowID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblListHead' AND object_id = OBJECT_ID('tblListHead'))
CREATE NONCLUSTERED INDEX [ix_tblListHead] ON [tblListHead] ([CallListName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblLockingBookingNo' AND object_id = OBJECT_ID('tblLocking'))
CREATE NONCLUSTERED INDEX [idxtblLockingBookingNo] ON [tblLocking] ([Booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblLockingLocktype' AND object_id = OBJECT_ID('tblLocking'))
CREATE NONCLUSTERED INDEX [IX_tblLockingLocktype] ON [tblLocking] ([LockType]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblLockingOpsID' AND object_id = OBJECT_ID('tblLocking'))
CREATE NONCLUSTERED INDEX [IX_tblLockingOpsID] ON [tblLocking] ([LockOpsID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Locnlist' AND object_id = OBJECT_ID('tblLocnlist'))
CREATE UNIQUE NONCLUSTERED INDEX [idx_Locnlist] ON [tblLocnlist] ([Locn_number], [ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Locnlist_desc' AND object_id = OBJECT_ID('tblLocnlist'))
CREATE NONCLUSTERED INDEX [idx_Locnlist_desc] ON [tblLocnlist] ([Locn_number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblLocnQtyProduct_code' AND object_id = OBJECT_ID('tblLocnqty'))
CREATE NONCLUSTERED INDEX [idxtblLocnQtyProduct_code] ON [tblLocnqty] ([product_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblMaintBookingNo' AND object_id = OBJECT_ID('tblMaint'))
CREATE NONCLUSTERED INDEX [IDXtblMaintBookingNo] ON [tblMaint] ([Booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblMaintProductCode' AND object_id = OBJECT_ID('tblMaint'))
CREATE NONCLUSTERED INDEX [IDXtblMaintProductCode] ON [tblMaint] ([Product_code], [Stock_Number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblMaintReturnDate' AND object_id = OBJECT_ID('tblMaint'))
CREATE NONCLUSTERED INDEX [IDXtblMaintReturnDate] ON [tblMaint] ([ReturnDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblMaintSerialNo' AND object_id = OBJECT_ID('tblMaint'))
CREATE NONCLUSTERED INDEX [IDXtblMaintSerialNo] ON [tblMaint] ([Serial_number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNC_tblMaintProductCodeStkN' AND object_id = OBJECT_ID('tblMaint'))
CREATE NONCLUSTERED INDEX [ixNC_tblMaintProductCodeStkN] ON [tblMaint] ([OutDate], [ReturnDate], [bIsHistoryItem], [Product_code], [Stock_Number], [Serial_number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblMaintenanceNotesMaintenanceID' AND object_id = OBJECT_ID('tblMaintenanceNotes'))
CREATE NONCLUSTERED INDEX [idxtblMaintenanceNotesMaintenanceID] ON [tblMaintenanceNotes] ([MaintenanceID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblMessage' AND object_id = OBJECT_ID('tblMessage'))
CREATE NONCLUSTERED INDEX [ix_tblMessage] ON [tblMessage] ([MsgDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblNonAssetTrackedProductTagList_RFIDtag' AND object_id = OBJECT_ID('tblNonAssetTrackedProductTagList'))
CREATE NONCLUSTERED INDEX [idxtblNonAssetTrackedProductTagList_RFIDtag] ON [tblNonAssetTrackedProductTagList] ([ProductID], [barcodeNumber], [RFIDTag]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblOperatorsLoginName' AND object_id = OBJECT_ID('tblOperators'))
CREATE NONCLUSTERED INDEX [IDXtblOperatorsLoginName] ON [tblOperators] ([Loginname]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'tblOperatorsLocationGroups_uq' AND object_id = OBJECT_ID('tblOperatorsLocationGroups'))
CREATE UNIQUE NONCLUSTERED INDEX [tblOperatorsLocationGroups_uq] ON [tblOperatorsLocationGroups] ([LocnID], [OpGroupID], [OperatorID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'tblOptions_uq' AND object_id = OBJECT_ID('tblOptionItems'))
CREATE UNIQUE NONCLUSTERED INDEX [tblOptions_uq] ON [tblOptionItems] ([opt_code]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Payment' AND object_id = OBJECT_ID('tblPayment'))
CREATE NONCLUSTERED INDEX [idx_Payment] ON [tblPayment] ([invoice_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblPaymentBookingNo' AND object_id = OBJECT_ID('tblPayment'))
CREATE NONCLUSTERED INDEX [idxtblPaymentBookingNo] ON [tblPayment] ([Booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblPayroll' AND object_id = OBJECT_ID('tblPayroll'))
CREATE NONCLUSTERED INDEX [IX_tblPayroll] ON [tblPayroll] ([Booking_No]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Po' AND object_id = OBJECT_ID('tblPo'))
CREATE NONCLUSTERED INDEX [idx_Po] ON [tblPo] ([PPONumber]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_PoBookNo' AND object_id = OBJECT_ID('tblPo'))
CREATE NONCLUSTERED INDEX [idx_PoBookNo] ON [tblPo] ([POBooking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_PoProj' AND object_id = OBJECT_ID('tblPo'))
CREATE NONCLUSTERED INDEX [idx_PoProj] ON [tblPo] ([ProjectCode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblPOExpectedDate' AND object_id = OBJECT_ID('tblPo'))
CREATE NONCLUSTERED INDEX [IDXtblPOExpectedDate] ON [tblPo] ([ExpectedDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblPOExpectedOrders' AND object_id = OBJECT_ID('tblPo'))
CREATE NONCLUSTERED INDEX [IDXtblPOExpectedOrders] ON [tblPo] ([Archaived], [PpostedToOnOrd], [ExpectedDate], [Location]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblPOOrderDate' AND object_id = OBJECT_ID('tblPo'))
CREATE NONCLUSTERED INDEX [idxtblPOOrderDate] ON [tblPo] ([OrderDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblPOLineProduct_code' AND object_id = OBJECT_ID('tblPoline'))
CREATE NONCLUSTERED INDEX [IDXtblPOLineProduct_code] ON [tblPoline] ([LquantityReceived], [LProductCode], [Lquantity]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Ponote' AND object_id = OBJECT_ID('tblPonote'))
CREATE NONCLUSTERED INDEX [idx_Ponote] ON [tblPonote] ([line_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblProdMx05IntKey' AND object_id = OBJECT_ID('tblProdmx05'))
CREATE NONCLUSTERED INDEX [idxtblProdMx05IntKey] ON [tblProdmx05] ([IntKey]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblProdMx05LicTime' AND object_id = OBJECT_ID('tblProdmx05'))
CREATE NONCLUSTERED INDEX [idxtblProdMx05LicTime] ON [tblProdmx05] ([LicTime]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblProdNoteType' AND object_id = OBJECT_ID('tblProdnote'))
CREATE NONCLUSTERED INDEX [IDXtblProdNoteType] ON [tblProdnote] ([Notetype], [line_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tblProdstat_desc' AND object_id = OBJECT_ID('tblProdstat'))
CREATE UNIQUE NONCLUSTERED INDEX [idx_tblProdstat_desc] ON [tblProdstat] ([ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblProfile' AND object_id = OBJECT_ID('tblProfile'))
CREATE NONCLUSTERED INDEX [ix_tblProfile] ON [tblProfile] ([Name]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Receipt' AND object_id = OBJECT_ID('tblReceipt'))
CREATE NONCLUSTERED INDEX [idx_Receipt] ON [tblReceipt] ([receiptNo]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblReceiptDateF' AND object_id = OBJECT_ID('tblReceipt'))
CREATE NONCLUSTERED INDEX [IDXtblReceiptDateF] ON [tblReceipt] ([DateF]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNC_tblReservedAssetsPcStkNo' AND object_id = OBJECT_ID('tblReservedAssets'))
CREATE NONCLUSTERED INDEX [ixNC_tblReservedAssetsPcStkNo] ON [tblReservedAssets] ([Product_code], [Stock_number], [Booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblResults' AND object_id = OBJECT_ID('tblResults'))
CREATE NONCLUSTERED INDEX [ix_tblResults] ON [tblResults] ([ResultDate]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblRFIDEvents' AND object_id = OBJECT_ID('tblRFIDEvents'))
CREATE NONCLUSTERED INDEX [IX_tblRFIDEvents] ON [tblRFIDEvents] ([RFIDTag]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblRFIDEventsDateLoaded' AND object_id = OBJECT_ID('tblRFIDEvents'))
CREATE NONCLUSTERED INDEX [IX_tblRFIDEventsDateLoaded] ON [tblRFIDEvents] ([DateLoaded]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblRoadcase_Booking_no' AND object_id = OBJECT_ID('tblRoadcase'))
CREATE NONCLUSTERED INDEX [idxtblRoadcase_Booking_no] ON [tblRoadcase] ([Booking_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxtblRoadcaseAssetBarcode' AND object_id = OBJECT_ID('tblRoadcase'))
CREATE NONCLUSTERED INDEX [IdxtblRoadcaseAssetBarcode] ON [tblRoadcase] ([asset_barcode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblRoadcaseNonBarProductCode' AND object_id = OBJECT_ID('tblRoadcase'))
CREATE NONCLUSTERED INDEX [idxtblRoadcaseNonBarProductCode] ON [tblRoadcase] ([NonBarProductCode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxtblRoadcaseParentBasecode' AND object_id = OBJECT_ID('tblRoadcase'))
CREATE NONCLUSTERED INDEX [IdxtblRoadcaseParentBasecode] ON [tblRoadcase] ([parent_basecode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNB_tblRoadcaseNonBarProductCode' AND object_id = OBJECT_ID('tblRoadcase'))
CREATE NONCLUSTERED INDEX [ixNB_tblRoadcaseNonBarProductCode] ON [tblRoadcase] ([Qty], [NonBarLocn], [Booking_no], [NonBarProductCode], [parent_basecode_ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNC_tblRoadcaseAssetBarcodeID' AND object_id = OBJECT_ID('tblRoadcase'))
CREATE NONCLUSTERED INDEX [ixNC_tblRoadcaseAssetBarcodeID] ON [tblRoadcase] ([parent_basecode_ID], [Floating], [Booking_no], [CaseType], [asset_barcode_ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ixNC_tblRoadcaseParentbasecodeID' AND object_id = OBJECT_ID('tblRoadcase'))
CREATE NONCLUSTERED INDEX [ixNC_tblRoadcaseParentbasecodeID] ON [tblRoadcase] ([asset_barcode_ID], [Floating], [Booking_no], [CaseType], [parent_basecode_ID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblScript' AND object_id = OBJECT_ID('tblScript'))
CREATE NONCLUSTERED INDEX [ix_tblScript] ON [tblScript] ([Name]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblShow' AND object_id = OBJECT_ID('tblShow'))
CREATE NONCLUSTERED INDEX [ix_tblShow] ON [tblShow] ([ShowName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblSQuestion' AND object_id = OBJECT_ID('tblSQuestion'))
CREATE NONCLUSTERED INDEX [ix_tblSQuestion] ON [tblSQuestion] ([Number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblSResponses' AND object_id = OBJECT_ID('tblSResponses'))
CREATE NONCLUSTERED INDEX [ix_tblSResponses] ON [tblSResponses] ([Number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_tblStatus' AND object_id = OBJECT_ID('tblStatus'))
CREATE NONCLUSTERED INDEX [ix_tblStatus] ON [tblStatus] ([Status]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Tax' AND object_id = OBJECT_ID('tblTax'))
CREATE NONCLUSTERED INDEX [idx_Tax] ON [tblTax] ([tax_auth_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Trip' AND object_id = OBJECT_ID('tblTrip'))
CREATE NONCLUSTERED INDEX [idx_Trip] ON [tblTrip] ([truckNo], [DateF], [tripNo]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxBookingID' AND object_id = OBJECT_ID('tblTruckLoadList'))
CREATE NONCLUSTERED INDEX [idxBookingID] ON [tblTruckLoadList] ([BookingID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Trucks' AND object_id = OBJECT_ID('tblTrucks'))
CREATE NONCLUSTERED INDEX [idx_Trucks] ON [tblTrucks] ([truck_number]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Vendnote' AND object_id = OBJECT_ID('tblVendnote'))
CREATE NONCLUSTERED INDEX [idx_Vendnote] ON [tblVendnote] ([code], [line_no]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Vcontact' AND object_id = OBJECT_ID('tblVendor'))
CREATE NONCLUSTERED INDEX [idx_Vcontact] ON [tblVendor] ([VendorContact]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Vname' AND object_id = OBJECT_ID('tblVendor'))
CREATE NONCLUSTERED INDEX [idx_Vname] ON [tblVendor] ([VendorName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblVendorRates' AND object_id = OBJECT_ID('tblVendorRates'))
CREATE NONCLUSTERED INDEX [IX_tblVendorRates] ON [tblVendorRates] ([ProductCode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_tblVendorRatesVendorCode' AND object_id = OBJECT_ID('tblVendorRates'))
CREATE NONCLUSTERED INDEX [IX_tblVendorRatesVendorCode] ON [tblVendorRates] ([VendorCode]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblVenueAddressID' AND object_id = OBJECT_ID('tblVenueAddress'))
CREATE NONCLUSTERED INDEX [IDXtblVenueAddressID] ON [tblVenueAddress] ([VenueID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblVenuesPhoneID' AND object_id = OBJECT_ID('tblVenuePhone'))
CREATE NONCLUSTERED INDEX [IDXtblVenuesPhoneID] ON [tblVenuePhone] ([VenueID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblVenuesName' AND object_id = OBJECT_ID('tblVenues'))
CREATE NONCLUSTERED INDEX [IDXtblVenuesName] ON [tblVenues] ([VenueName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IdxtblVenuNoteVenueID' AND object_id = OBJECT_ID('tblVenunote'))
CREATE NONCLUSTERED INDEX [IdxtblVenuNoteVenueID] ON [tblVenunote] ([VenueID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_Venuroom' AND object_id = OBJECT_ID('tblVenuroom'))
CREATE NONCLUSTERED INDEX [idx_Venuroom] ON [tblVenuroom] ([VenueName], [Roomname]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IDXtblVenuRoomID' AND object_id = OBJECT_ID('tblVenuroom'))
CREATE NONCLUSTERED INDEX [IDXtblVenuRoomID] ON [tblVenuroom] ([VenueID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ__tblWorkf__DC0E2DEBFE4EA538' AND object_id = OBJECT_ID('tblWorkflow'))
CREATE UNIQUE NONCLUSTERED INDEX [UQ__tblWorkf__DC0E2DEBFE4EA538] ON [tblWorkflow] ([WorkflowName]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UC_WorkflowID_OperatorID' AND object_id = OBJECT_ID('tblWorkflowOperatorLink'))
CREATE UNIQUE NONCLUSTERED INDEX [UC_WorkflowID_OperatorID] ON [tblWorkflowOperatorLink] ([WorkflowID], [OperatorID]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idxtblWPFormatwp_type' AND object_id = OBJECT_ID('tblWpformat'))
CREATE NONCLUSTERED INDEX [idxtblWPFormatwp_type] ON [tblWpformat] ([wp_type]);
GO

PRINT 'Schema creation complete!';
GO