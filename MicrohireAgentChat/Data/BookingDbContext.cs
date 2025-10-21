using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MicrohireAgentChat.Data;

public sealed class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }
    public DbSet<TblContact> Contacts => Set<TblContact>();
    public DbSet<TblBooking> TblBookings => Set<TblBooking>();
    public DbSet<TblInvmas> TblInvmas => Set<TblInvmas>();
    public DbSet<TblItemtran> TblItemtrans => Set<TblItemtran>();
    public DbSet<TblRatetbl> TblRatetbls => Set<TblRatetbl>();
    public DbSet<TblCrew> TblCrews => Set<TblCrew>();
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

        // Times (varchar(4))
        e.Property(x => x.showStartTime).HasColumnName("showStartTime").HasMaxLength(4);
        e.Property(x => x.ShowEndTime).HasColumnName("ShowEndTime").HasMaxLength(4);

        // Money / numbers (limited set)
        e.Property(x => x.price_quoted).HasColumnName("price_quoted");
        e.Property(x => x.hire_price).HasColumnName("hire_price");
        e.Property(x => x.labour).HasColumnName("labour");
        e.Property(x => x.sundry_total).HasColumnName("sundry_total");

        // Booking meta (limited set)
        e.Property(x => x.booking_type_v32).HasColumnName("booking_type_v32");
        e.Property(x => x.BookingProgressStatus).HasColumnName("BookingProgressStatus");

        // Text fields
        e.Property(x => x.contact_nameV6).HasColumnName("contact_nameV6").HasMaxLength(35);
        e.Property(x => x.showName).HasColumnName("showName").HasMaxLength(100);
        e.Property(x => x.OrganizationV6).HasColumnName("OrganizationV6").HasMaxLength(100);
        e.Property(x => x.Salesperson).HasColumnName("Salesperson").HasMaxLength(50);
        e.Property(x => x.CustID).HasColumnName("CustID").HasColumnType("decimal(10,0)");
        e.Property(x => x.CustCode).HasColumnName("CustCode").HasMaxLength(50);

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
        var decLongConv = new ValueConverter<long?, decimal?>(
            v => v.HasValue ? (decimal?)v.Value : null,
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
        it.Property(x => x.Price).HasColumnName("price");                       // float
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
        it.Property(x => x.TechRateOrDaysCharged).HasColumnName("techRateorDaysCharged"); // float
        it.Property(x => x.TechPay).HasColumnName("TechPay");                               // float
        it.Property(x => x.UnitRate).HasColumnName("unitRate");                             // float

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
        it.Property(x => x.DayWeekRate).HasColumnName("DayWeekRate");           // float
        it.Property(x => x.QtyReserved).HasColumnName("QtyReserved");           // int
        it.Property(x => x.AddedAtCheckout).HasColumnName("AddedAtCheckout");   // bit
        it.Property(x => x.GroupSeqNo).HasColumnName("GroupSeqNo");
        it.Property(x => x.SubRentalLinkID).HasColumnName("SubRentalLinkID");
        it.Property(x => x.AssignType).HasColumnName("AssignType").HasColumnType("tinyint");
        it.Property(x => x.QtyShort).HasColumnName("QtyShort");                 // int NOT NULL
        it.Property(x => x.QtyAvailable).HasColumnName("QtyAvailable");
        it.Property(x => x.PackageLevel).HasColumnName("PackageLevel");
        it.Property(x => x.BeforeDiscountAmount).HasColumnName("BeforeDiscountAmount"); // float
        it.Property(x => x.QuickTurnAroundQty).HasColumnName("QuickTurnAroundQty");     // float
        it.Property(x => x.InRack).HasColumnName("InRack");
        it.Property(x => x.CostPrice).HasColumnName("CostPrice");               // float
        it.Property(x => x.NodeCollapsed).HasColumnName("NodeCollapsed");
        it.Property(x => x.AvailRecFlag).HasColumnName("AvailRecFlag");         // bit NOT NULL

        // Links / FK-ish
        it.Property(x => x.BookingId).HasColumnName("booking_id");
        it.Property(x => x.UndiscAmt).HasColumnName("Undisc_amt");              // float

        // Logistics / visibility
        it.Property(x => x.ViewLogi).HasColumnName("View_Logi");
        it.Property(x => x.ViewClient).HasColumnName("View_client");
        it.Property(x => x.LogiHeadingNo).HasColumnName("Logi_HeadingNo");
        it.Property(x => x.LogiGroupSeqNo).HasColumnName("Logi_GroupSeqNo");
        it.Property(x => x.LogiSeqNo).HasColumnName("Logi_Seq_No");
        it.Property(x => x.LogiSubSeqNo).HasColumnName("Logi_Sub_Seq_no");

        // Parent link
        it.Property(x => x.ParentCode).HasColumnName("ParentCode").HasMaxLength(30);

        var rt = modelBuilder.Entity<TblRatetbl>();
        rt.ToTable("tblRatetbl");
        rt.HasKey(x => new { x.product_code, x.TableNo });

        rt.Property(x => x.product_code)
          .HasColumnName("ProductCode")
          .HasMaxLength(35);

        rt.Property(x => x.TableNo)
         .HasColumnName("TableNo")
         .HasColumnType("tinyint");

        rt.Property(x => x.rate_1st_day)
          .HasColumnName("rate_1st_day")
           .HasColumnType("float");

        var tc = modelBuilder.Entity<TblCrew>();
        tc.ToTable("tblCrew");
        tc.HasKey(x => x.ID);
        tc.Property(x => x.ID).HasColumnName("ID").HasColumnType("decimal(10,0)").ValueGeneratedOnAdd();

        tc.Property(x => x.BookingNoV32).HasColumnName("booking_no_v32").HasMaxLength(35);
        tc.Property(x => x.HeadingNo).HasColumnName("heading_no").HasColumnType("tinyint");
        tc.Property(x => x.SeqNo).HasColumnName("seq_no");
        tc.Property(x => x.SubSeqNo).HasColumnName("sub_seq_no");

        tc.Property(x => x.ProductCodeV42).HasColumnName("product_code_v42").HasMaxLength(30);
        tc.Property(x => x.DelTimeHour).HasColumnName("del_time_hour").HasColumnType("tinyint");
        tc.Property(x => x.DelTimeMin).HasColumnName("del_time_min").HasColumnType("tinyint");
        tc.Property(x => x.ReturnTimeHour).HasColumnName("return_time_hour");
        tc.Property(x => x.ReturnTimeMin).HasColumnName("return_time_min");

        tc.Property(x => x.TransQty).HasColumnName("trans_qty");
        tc.Property(x => x.Price).HasColumnName("price");
        tc.Property(x => x.UnitRate).HasColumnName("unitRate");

        tc.Property(x => x.Hours).HasColumnName("hours");
        tc.Property(x => x.Minutes).HasColumnName("Minutes");
        tc.Property(x => x.Person).HasColumnName("person");
        tc.Property(x => x.Task).HasColumnName("task");
        tc.Property(x => x.TechrateIsHourorDay).HasColumnName("techrateIsHourorDay"); // bit
        tc.Property(x => x.FirstDate).HasColumnName("FirstDate");
        tc.Property(x => x.RetnDate).HasColumnName("RetnDate");
        tc.Property(x => x.GroupSeqNo).HasColumnName("GroupSeqNo");
        tc.Property(x => x.StraightTime).HasColumnName("StraightTime"); // bit
        tc.Property(x => x.TechIsConfirmed).HasColumnName("TechIsConfirmed");
        tc.Property(x => x.MeetTechOnSite).HasColumnName("MeetTechOnSite");
    }
}
