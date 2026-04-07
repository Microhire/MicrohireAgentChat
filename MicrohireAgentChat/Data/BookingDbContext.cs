using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MicrohireAgentChat.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public DbSet<TblContact> Contacts => Set<TblContact>();
    public DbSet<TblBooking> TblBookings => Set<TblBooking>();
    public DbSet<TblInvmas> TblInvmas => Set<TblInvmas>();
    public DbSet<TblItemtran> TblItemtrans => Set<TblItemtran>();
    public DbSet<TblRatetbl> TblRatetbls => Set<TblRatetbl>();
    public DbSet<TblCrew> TblCrews => Set<TblCrew>();
    public DbSet<TblCust> TblCusts => Set<TblCust>();
    public DbSet<TblLinkCustContact> TblLinkCustContacts => Set<TblLinkCustContact>();
    public DbSet<TblBooknote> TblBooknotes => Set<TblBooknote>();
    public DbSet<TblVenue> TblVenues => Set<TblVenue>();
    public DbSet<VwProdsComponents> VwProdsComponents => Set<VwProdsComponents>();
    public DbSet<AgentThread> AgentThreads => Set<AgentThread>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- tblbookings ---
        var e = modelBuilder.Entity<TblBooking>();
        e.ToTable("tblbookings");
        e.HasKey(x => x.ID);
        e.Property(x => x.ID).ValueGeneratedOnAdd();
        e.Property(x => x.ID).HasColumnName("ID").HasColumnType("decimal(10,0)");

        e.Property(x => x.booking_no).HasMaxLength(35);
        e.Property(x => x.order_no).HasMaxLength(25);

        e.Property(x => x.status).HasColumnName("status");
        e.Property(x => x.bBookingIsComplete).HasColumnName("bBookingIsComplete");

        e.Property(x => x.VenueID).HasColumnName("VenueID");
        e.Property(x => x.VenueRoom).HasColumnName("VenueRoom").HasMaxLength(35);

        e.Property(x => x.SDate).HasColumnName("SDate");
        e.Property(x => x.rDate).HasColumnName("rDate");
        e.Property(x => x.order_date).HasColumnName("order_date");
        e.Property(x => x.ShowSDate).HasColumnName("ShowSDate");

        // NEW date columns
        e.Property(x => x.SetDate).HasColumnName("SetDate");
        e.Property(x => x.RehDate).HasColumnName("RehDate");

        // Times (varchar(4) - HHmm format)
        e.Property(x => x.showStartTime).HasColumnName("showStartTime").HasMaxLength(4);
        e.Property(x => x.ShowEndTime).HasColumnName("ShowEndTime").HasMaxLength(4);
        e.Property(x => x.setupTimeV61).HasColumnName("setupTimeV61").HasMaxLength(4);
        e.Property(x => x.RehearsalTime).HasColumnName("RehearsalTime").HasMaxLength(4);
        e.Property(x => x.StrikeTime).HasColumnName("StrikeTime").HasMaxLength(4);

        // Event type (varchar)
        e.Property(x => x.EventType).HasColumnName("EventType").HasMaxLength(20);

        // Money / numbers (limited set)
        e.Property(x => x.price_quoted).HasColumnName("price_quoted");
        e.Property(x => x.hire_price).HasColumnName("hire_price");
        e.Property(x => x.labour).HasColumnName("labour");
        e.Property(x => x.sundry_total).HasColumnName("sundry_total");
        e.Property(x => x.insurance_type).HasColumnName("insurance_type");
        e.Property(x => x.Tax2).HasColumnName("Tax2");
        // Booking meta (limited set)
        e.Property(x => x.booking_type_v32).HasColumnName("booking_type_v32");
        e.Property(x => x.BookingProgressStatus).HasColumnName("BookingProgressStatus");

        // Text fields
        e.Property(x => x.contact_nameV6).HasColumnName("contact_nameV6").HasMaxLength(35);
        e.Property(x => x.showName).HasColumnName("showName").HasMaxLength(50);
        e.Property(x => x.OrganizationV6).HasColumnName("OrganizationV6").HasMaxLength(50);
        e.Property(x => x.Salesperson).HasColumnName("Salesperson").HasMaxLength(30);
        e.Property(x => x.CustID).HasColumnName("CustID").HasColumnType("decimal(10,0)");
        e.Property(x => x.CustCode).HasColumnName("CustCode").HasMaxLength(30);
        e.Property(x => x.EntryDate).HasColumnName("EntryDate");

        // Contact FK link (no FK constraint here unless you add it)
        e.Property(x => x.ContactID).HasColumnName("ContactID").HasColumnType("decimal(10,0)");

        // helpful composite index for range queries
        e.HasIndex(x => new { x.VenueID, x.SDate, x.rDate });

        // --- tblInvmas ---
        var p = modelBuilder.Entity<TblInvmas>();
        p.ToTable("tblInvmas");
        p.HasKey(x => x.product_code);
        p.Property(x => x.product_code).HasColumnName("product_code");
        p.Property(x => x.PictureFileName).HasColumnName("PictureFileName");
        p.Property(x => x.category).HasColumnName("category");
        p.Property(x => x.descriptionv6).HasColumnName("descriptionv6");
        p.Property(x => x.PrintedDesc).HasColumnName("PrintedDesc");
        p.Property(x => x.groupFld).HasColumnName("groupFld");
        // --- tblContact ---
        var c = modelBuilder.Entity<TblContact>();
        c.ToTable("tblContact");
        c.HasKey(x => x.Id);

        c.Property(x => x.Id).HasColumnName("ID")
                             .HasColumnType("decimal(10, 0)")
                             .ValueGeneratedOnAdd();

        c.Property(x => x.Contactname).HasColumnName("Contactname").HasMaxLength(35);
        c.Property(x => x.Firstname).HasColumnName("firstname").HasMaxLength(25);
        c.Property(x => x.MidName).HasColumnName("MidName").HasMaxLength(35);
        c.Property(x => x.Surname).HasColumnName("surname").HasMaxLength(35);
        c.Property(x => x.Position).HasColumnName("position").HasMaxLength(35);
        c.Property(x => x.Email).HasColumnName("Email").HasMaxLength(80);
        c.Property(x => x.Cell).HasColumnName("Cell").HasMaxLength(16);
        c.Property(x => x.Phone1).HasColumnName("Phone1").HasMaxLength(16);

        c.Property(x => x.Active).HasColumnName("Active").HasMaxLength(1);
        c.Property(x => x.CreateDate).HasColumnName("CreateDate");
        c.Property(x => x.LastUpdate).HasColumnName("LastUpdate");
        c.Property(x => x.LastContact).HasColumnName("LastContact");
        c.Property(x => x.LastAttempt).HasColumnName("LastAttempt");

        c.HasIndex(x => x.Email)
         .HasDatabaseName("IX_tblContact_Email")
         .IsUnique(false);

        // ---- tblItemtrans ----------------------------------------------------------
        var it = modelBuilder.Entity<TblItemtran>();
        it.ToTable("tblItemtran");

        // PK
        it.HasKey(x => x.ID);
        it.Property(x => x.ID)
          .HasColumnName("ID")
          .HasColumnType("decimal(10,0)")
          .ValueGeneratedOnAdd();

        // Booking / identifiers
        it.Property(x => x.BookingNoV32).HasColumnName("booking_no_v32").HasMaxLength(35);
        it.Property(x => x.HeadingNo).HasColumnName("heading_no").HasColumnType("tinyint");

        // seq_no is decimal(19,0) in SQL. Keep long? in the model and convert.
        var decLongConv = new ValueConverter<long?, decimal?>(v => v.HasValue ? (decimal?)v.Value : null,
                                                              v => v.HasValue ? (long?)v.Value : null);

        it.Property(x => x.SeqNo)
          .HasColumnName("seq_no")
          .HasColumnType("decimal(19,0)")
          .HasConversion(decLongConv);

        it.Property(x => x.SubSeqNo).HasColumnName("sub_seq_no");

        // Core fields
        it.Property(x => x.TransTypeV41).HasColumnName("trans_type_v41").HasColumnType("tinyint");
        it.Property(x => x.ProductCodeV42).HasColumnName("product_code_v42").HasMaxLength(30);
        it.Property(x => x.DelTimeHour).HasColumnName("del_time_hour").HasColumnType("tinyint");
        it.Property(x => x.DelTimeMin).HasColumnName("del_time_min").HasColumnType("tinyint");
        it.Property(x => x.ReturnTimeHour).HasColumnName("return_time_hour").HasColumnType("tinyint");
        it.Property(x => x.ReturnTimeMin).HasColumnName("return_time_min").HasColumnType("tinyint");

        it.Property(x => x.TransQty).HasColumnName("trans_qty").HasColumnType("decimal(19,0)");
        it.Property(x => x.Price).HasColumnName("price");
        it.Property(x => x.ItemType).HasColumnName("item_type").HasColumnType("tinyint");
        it.Property(x => x.DaysUsing).HasColumnName("days_using");
        it.Property(x => x.SubHireQtyV61).HasColumnName("sub_hire_qtyV61").HasColumnType("decimal(19,0)");

        // Locations
        it.Property(x => x.FromLocn).HasColumnName("From_locn");
        it.Property(x => x.TransToLocn).HasColumnName("Trans_to_locn");
        it.Property(x => x.ReturnToLocn).HasColumnName("return_to_locn");

        // Bit field
        it.Property(x => x.BitFieldV41).HasColumnName("bit_field_v41").HasColumnType("tinyint");

        // Time booked
        it.Property(x => x.TimeBookedH).HasColumnName("TimeBookedH").HasColumnType("tinyint");
        it.Property(x => x.TimeBookedM).HasColumnName("TimeBookedM").HasColumnType("tinyint");
        it.Property(x => x.TimeBookedS).HasColumnName("TimeBookedS").HasColumnType("tinyint");

        // Qty / rates
        it.Property(x => x.QtyReturned).HasColumnName("QtyReturned").HasColumnType("decimal(19,0)");
        it.Property(x => x.QtyCheckedOut).HasColumnName("QtyCheckedOut").HasColumnType("decimal(19,0)");
        it.Property(x => x.TechRateOrDaysCharged).HasColumnName("techRateorDaysCharged");
        it.Property(x => x.TechPay).HasColumnName("TechPay");
        it.Property(x => x.UnitRate).HasColumnName("unitRate");

        // Flags / misc
        it.Property(x => x.PrepOn).HasColumnName("prep_on");
        it.Property(x => x.CommentDescV42).HasColumnName("Comment_desc_v42").HasMaxLength(70);
        it.Property(x => x.AssignTo).HasColumnName("AssignTo");

        // Dates / times
        it.Property(x => x.FirstDate).HasColumnName("FirstDate");
        it.Property(x => x.RetnDate).HasColumnName("RetnDate");
        it.Property(x => x.BookDate).HasColumnName("BookDate");
        it.Property(x => x.PDate).HasColumnName("PDate");
        it.Property(x => x.PTimeH).HasColumnName("PTimeH").HasColumnType("tinyint");
        it.Property(x => x.PTimeM).HasColumnName("PTimeM").HasColumnType("tinyint");

        // Booking economics
        it.Property(x => x.DayWeekRate).HasColumnName("DayWeekRate");
        it.Property(x => x.QtyReserved).HasColumnName("QtyReserved");
        it.Property(x => x.AddedAtCheckout).HasColumnName("AddedAtCheckout");
        it.Property(x => x.GroupSeqNo).HasColumnName("GroupSeqNo");
        it.Property(x => x.SubRentalLinkID).HasColumnName("SubRentalLinkID");
        it.Property(x => x.AssignType).HasColumnName("AssignType").HasColumnType("tinyint");
        it.Property(x => x.QtyShort).HasColumnName("QtyShort");
        it.Property(x => x.QtyAvailable).HasColumnName("QtyAvailable");
        it.Property(x => x.PackageLevel).HasColumnName("PackageLevel");
        it.Property(x => x.BeforeDiscountAmount).HasColumnName("BeforeDiscountAmount");
        it.Property(x => x.QuickTurnAroundQty).HasColumnName("QuickTurnAroundQty");
        it.Property(x => x.InRack).HasColumnName("InRack");
        it.Property(x => x.CostPrice).HasColumnName("CostPrice");
        it.Property(x => x.NodeCollapsed).HasColumnName("NodeCollapsed");
        it.Property(x => x.AvailRecFlag).HasColumnName("AvailRecFlag");

        // Links / FK-ish
        it.Property(x => x.BookingId).HasColumnName("booking_id").HasColumnType("int");
        it.Property(x => x.UndiscAmt).HasColumnName("Undisc_amt");

        // Logistics / visibility
        it.Property(x => x.ViewLogi).HasColumnName("View_Logi");
        it.Property(x => x.ViewClient).HasColumnName("View_client");
        it.Property(x => x.LogiHeadingNo).HasColumnName("Logi_HeadingNo");
        it.Property(x => x.LogiGroupSeqNo).HasColumnName("Logi_GroupSeqNo");
        it.Property(x => x.LogiSeqNo).HasColumnName("Logi_Seq_No");
        it.Property(x => x.LogiSubSeqNo).HasColumnName("Logi_Sub_Seq_no");

        // Parent link
        it.Property(x => x.ParentCode).HasColumnName("ParentCode").HasMaxLength(30);

        // --- tblRatetbl ---
        var rt = modelBuilder.Entity<TblRatetbl>();
        rt.ToTable("tblRatetbl");
        rt.HasKey(x => x.ID);
        rt.Property(x => x.ID).HasColumnName("ID").HasColumnType("decimal(10,0)");
        rt.Property(x => x.product_code).HasColumnName("ProductCode").HasMaxLength(30);
        rt.Property(x => x.TableNo).HasColumnName("tableNo").HasColumnType("tinyint");
        rt.Property(x => x.rate_1st_day).HasColumnName("rate_1st_day");
        rt.Property(x => x.rate_extra_days).HasColumnName("rate_extra_days");
        rt.Property(x => x.hourly_rate).HasColumnName("hourly_rate");
        rt.Property(x => x.half_day).HasColumnName("half_day");
        
        // Index for efficient lookups
        rt.HasIndex(x => new { x.product_code, x.TableNo }).HasDatabaseName("IX_tblRatetbl_ProductCode_TableNo");

        // --- tblCrew ---
        // --- TblCrew ---
        var tc = modelBuilder.Entity<TblCrew>();

        // Table name: use the exact DB object name ("TblCrew" vs "tblCrew")
        tc.ToTable("TblCrew");

        tc.HasKey(x => x.ID);
        tc.Property(x => x.ID)
          .HasColumnName("ID")
          .HasColumnType("decimal(10,0)")
          .ValueGeneratedOnAdd();

        tc.Property(x => x.BookingNoV32)
          .HasColumnName("booking_no_v32")
          .HasMaxLength(35);

        tc.Property(x => x.HeadingNo)
          .HasColumnName("heading_no")
          .HasColumnType("tinyint");

        tc.Property(x => x.SeqNo)
          .HasColumnName("seq_no")
          .HasColumnType("decimal(19,0)");

        tc.Property(x => x.SubSeqNo)
          .HasColumnName("sub_seq_no");

        // codes / description
        tc.Property(x => x.ProductCodeV42)
          .HasColumnName("product_code_v42")
          .HasMaxLength(30);

        // times (tinyint)
        tc.Property(x => x.DelTimeHour)
          .HasColumnName("del_time_hour")
          .HasColumnType("tinyint");

        tc.Property(x => x.DelTimeMin)
          .HasColumnName("del_time_min")
          .HasColumnType("tinyint");

        tc.Property(x => x.ReturnTimeHour)
          .HasColumnName("return_time_hour")
          .HasColumnType("tinyint");

        tc.Property(x => x.ReturnTimeMin)
          .HasColumnName("return_time_min")
          .HasColumnType("tinyint");

        // qty / price
        tc.Property(x => x.TransQty)
          .HasColumnName("trans_qty"); // int

        tc.Property(x => x.Price)
          .HasColumnName("price")
          .HasColumnType("float");

        tc.Property(x => x.UnitRate)
          .HasColumnName("unitRate")
          .HasColumnType("float");

        // duration fields
        tc.Property(x => x.Hours)
          .HasColumnName("hours")
          .HasColumnType("tinyint");

        tc.Property(x => x.Minutes)
          .HasColumnName("Minutes")
          .HasColumnType("tinyint");

        // person / task
        tc.Property(x => x.Person)
          .HasColumnName("person")
          .HasMaxLength(30)       // char(30)
          .IsFixedLength();

        tc.Property(x => x.Task)
          .HasColumnName("task")
          .HasColumnType("tinyint");

        // hour/day flag
        tc.Property(x => x.TechrateIsHourOrDay)
          .HasColumnName("techrateIsHourorDay")
          .HasMaxLength(1)        // char(1)
          .IsFixedLength();

        // dates
        tc.Property(x => x.FirstDate)
          .HasColumnName("FirstDate");   // datetime

        tc.Property(x => x.RetnDate)
          .HasColumnName("RetnDate");    // datetime

        // misc
        tc.Property(x => x.GroupSeqNo)
          .HasColumnName("GroupSeqNo");  // int

        tc.Property(x => x.StraightTime)
          .HasColumnName("StraightTime")
          .HasColumnType("float");       // <-- not bit

        tc.Property(x => x.TechIsConfirmed)
          .HasColumnName("TechIsConfirmed")
          .HasColumnType("bit")
          .IsRequired();

        tc.Property(x => x.MeetTechOnSite)
          .HasColumnName("MeetTechOnSite")
          .HasColumnType("bit")
          .IsRequired();

        // HourlyRateID - decimal(10,0) NOT NULL in DB
        tc.Property(x => x.HourlyRateID)
          .HasColumnName("HourlyRateID")
          .HasColumnType("decimal(10,0)")
          .IsRequired();


        // --- tblcust ---
        var tcust = modelBuilder.Entity<TblCust>();
        tcust.ToTable("tblcust");
        tcust.HasKey(x => x.ID);

        tcust.Property(x => x.ID)
             .HasColumnName("ID")
             .HasColumnType("decimal(10,0)")
             .ValueGeneratedOnAdd();

        tcust.Property(x => x.OrganisationV6)
             .HasColumnName("OrganisationV6")
             .HasMaxLength(120)
             .IsUnicode(false);

        tcust.Property(x => x.Address_l1V6)
             .HasColumnName("Address_l1V6")
             .HasMaxLength(200)
             .IsUnicode(false);

        tcust.Property(x => x.Customer_code)
             .HasColumnName("Customer_code")
             .HasMaxLength(35)
             .IsUnicode(false);

        // --- tblLinkCustContact (NEW) ---
        var link = modelBuilder.Entity<TblLinkCustContact>();
        link.ToTable("tblLinkCustContact");
        link.HasKey(x => x.ID);

        link.Property(x => x.ID)
            .HasColumnName("ID")
            .HasColumnType("decimal(10,0)")
            .ValueGeneratedOnAdd();

        link.Property(x => x.Customer_Code)
            .HasColumnName("Customer_Code")
            .HasMaxLength(30)
            .IsUnicode(false);

        link.Property(x => x.ContactID)
            .HasColumnName("ContactID")
            .HasColumnType("decimal(10,0)");

        link.HasIndex(x => new { x.Customer_Code, x.ContactID })
            .HasDatabaseName("IX_tblLinkCustContact_Code_Contact")
            .IsUnique(false);

        var bn = modelBuilder.Entity<TblBooknote>();
        bn.ToTable("tblbooknote");
        bn.HasKey(x => x.Id);

        bn.Property(x => x.Id)
          .HasColumnName("ID")
          .HasColumnType("decimal(10,0)")
          .ValueGeneratedOnAdd();

        bn.Property(x => x.BookingNo)
          .HasColumnName("bookingNo")
          .HasMaxLength(35)
          .IsUnicode(false);

        bn.Property(x => x.LineNo)
          .HasColumnName("line_no");

        bn.Property(x => x.TextLine)
          .HasColumnName("text_line")
          .IsUnicode(false);                 // varchar(max) (non-Unicode)

        bn.Property(x => x.NoteType)
          .HasColumnName("NoteType");        // 1=user, 2=assistant

        bn.Property(x => x.OperatorId)
          .HasColumnName("OperatorID")
          .HasColumnType("decimal(10,0)");

        // helpful ordering/index per booking
        bn.HasIndex(x => new { x.BookingNo, x.LineNo })
          .HasDatabaseName("IX_tblbooknote_Booking_Line");

        // --- vwProdsComponents (VIEW, keyless) ---
        modelBuilder.Entity<VwProdsComponents>(v =>
        {
            v.HasNoKey();
            v.ToView("vwProdsComponents");
            v.Property(x => x.ParentCode).HasColumnName("parent_code");
            v.Property(x => x.ProductCode).HasColumnName("product_code");
            v.Property(x => x.VariablePart).HasColumnName("variable_part");
            v.Property(x => x.Qty).HasColumnName("qty_v5");
            v.Property(x => x.SubSeqNo).HasColumnName("sub_seq_no");
        });

        // --- tblVenues ---
        var venue = modelBuilder.Entity<TblVenue>();
        venue.ToTable("tblVenues");
        venue.HasKey(x => x.ID);

        venue.Property(x => x.ID)
             .HasColumnName("ID")
             .HasColumnType("decimal(10,0)")
             .ValueGeneratedOnAdd();

        venue.Property(x => x.VenueName).HasColumnName("VenueName").HasMaxLength(100);
        venue.Property(x => x.ContactName).HasColumnName("ContactName").HasMaxLength(50);
        venue.Property(x => x.ContactID).HasColumnName("ContactID").HasColumnType("decimal(10,0)");
        venue.Property(x => x.WebPage).HasColumnName("WebPage").HasMaxLength(200);
        
        // Address fields
        venue.Property(x => x.Address1).HasColumnName("Address1").HasMaxLength(100);
        venue.Property(x => x.Address2).HasColumnName("Address2").HasMaxLength(100);
        venue.Property(x => x.City).HasColumnName("City").HasMaxLength(50);
        venue.Property(x => x.State).HasColumnName("State").HasMaxLength(25);
        venue.Property(x => x.Country).HasColumnName("Country").HasMaxLength(50);
        venue.Property(x => x.ZipCode).HasColumnName("ZipCode").HasMaxLength(15);
        
        // Phone fields
        venue.Property(x => x.Phone1CountryCode).HasColumnName("Phone1CountryCode").HasMaxLength(6);
        venue.Property(x => x.Phone1AreaCode).HasColumnName("Phone1AreaCode").HasMaxLength(6);
        venue.Property(x => x.Phone1Digits).HasColumnName("Phone1Digits").HasMaxLength(12);
        venue.Property(x => x.Phone1Ext).HasColumnName("Phone1Ext").HasMaxLength(10);
        
        venue.Property(x => x.Phone2CountryCode).HasColumnName("Phone2CountryCode").HasMaxLength(6);
        venue.Property(x => x.Phone2AreaCode).HasColumnName("Phone2AreaCode").HasMaxLength(6);
        venue.Property(x => x.Phone2Digits).HasColumnName("Phone2Digits").HasMaxLength(12);
        venue.Property(x => x.Phone2Ext).HasColumnName("Phone2Ext").HasMaxLength(10);
        
        venue.Property(x => x.FaxCountryCode).HasColumnName("FaxCountryCode").HasMaxLength(6);
        venue.Property(x => x.FaxAreaCode).HasColumnName("FaxAreaCode").HasMaxLength(6);
        venue.Property(x => x.FaxDigits).HasColumnName("FaxDigits").HasMaxLength(12);
        
        venue.Property(x => x.CellCountryCode).HasColumnName("CellCountryCode").HasMaxLength(6);
        venue.Property(x => x.CellAreaCode).HasColumnName("CellAreaCode").HasMaxLength(6);
        venue.Property(x => x.CellDigits).HasColumnName("CellDigits").HasMaxLength(12);
        
        // Type and metadata
        venue.Property(x => x.Type).HasColumnName("Type");
        venue.Property(x => x.BookingNo).HasColumnName("BookingNo").HasMaxLength(35);
        venue.Property(x => x.VenueNickname).HasColumnName("VenueNickname").HasMaxLength(50);
        venue.Property(x => x.VenueTextType).HasColumnName("VenueTextType").HasMaxLength(50);
        venue.Property(x => x.DefaultFolder).HasColumnName("DefaultFolder").HasMaxLength(200);

        venue.HasIndex(x => x.VenueName).HasDatabaseName("IX_tblVenues_Name");

        // --- AgentThreads (thread persistence) ---
        var at = modelBuilder.Entity<AgentThread>();
        at.ToTable("AgentThreads");
        at.HasKey(x => x.Id);

        at.Property(x => x.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();

        at.Property(x => x.UserKey)
            .HasColumnName("UserKey")
            .HasMaxLength(200)
            .IsRequired();

        at.Property(x => x.ThreadId)
            .HasColumnName("ThreadId")
            .HasMaxLength(200)
            .IsRequired();

        at.Property(x => x.CreatedUtc)
            .HasColumnName("CreatedUtc")
            .IsRequired();

        at.Property(x => x.LastSeenUtc)
            .HasColumnName("LastSeenUtc")
            .IsRequired();

        at.HasIndex(x => x.UserKey)
            .HasDatabaseName("IX_AgentThreads_UserKey")
            .IsUnique();

        at.HasIndex(x => x.ThreadId)
            .HasDatabaseName("IX_AgentThreads_ThreadId")
            .IsUnique();
    }
}
