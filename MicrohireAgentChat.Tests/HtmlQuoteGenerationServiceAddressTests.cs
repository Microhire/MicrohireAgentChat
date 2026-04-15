using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace MicrohireAgentChat.Tests;

/// <summary>
/// Regression tests for the sales-portal-address-in-quote bug: when a lead is submitted with a
/// new organisation address but the tblcust row is a reused existing org (Jenny's no-update rule
/// keeps Address_l1V6 stale), the quote must still show the admin-entered value from WestinLead.
/// </summary>
public sealed class HtmlQuoteGenerationServiceAddressTests : IDisposable
{
    private const string BookingNo = "C99999-TEST";
    private const string StaleTblCustAddress = "Shop 3, 18 Orchid Avenue, Surfers Paradise QLD 4217";
    private const string LeadFormAddress = "123 Change Address, Test";
    private const string SessionOverrideAddress = "999 Edited In Chat, Live";

    private readonly string _tempWebRoot;

    public HtmlQuoteGenerationServiceAddressTests()
    {
        _tempWebRoot = Path.Combine(Path.GetTempPath(), "mh-quote-addr-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_tempWebRoot, "files", "quotes"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempWebRoot, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public async Task Quote_UsesLeadAddress_WhenSessionEmpty_AndTblCustStale()
    {
        await using var bookingDb = CreateBookingDb(nameof(Quote_UsesLeadAddress_WhenSessionEmpty_AndTblCustStale));
        await using var appDb = CreateAppDb(nameof(Quote_UsesLeadAddress_WhenSessionEmpty_AndTblCustStale));

        await SeedBookingAsync(bookingDb, StaleTblCustAddress);
        await SeedLeadAsync(appDb, LeadFormAddress);

        var svc = new HtmlQuoteGenerationService(bookingDb, appDb, new TestEnv(_tempWebRoot), NullLogger<HtmlQuoteGenerationService>.Instance);
        var (success, url, error) = await svc.GenerateHtmlQuoteForBookingAsync(BookingNo, session: null);

        Assert.True(success, error);
        Assert.NotNull(url);
        var html = await File.ReadAllTextAsync(ResolveHtmlPath(url!));
        Assert.Contains(LeadFormAddress, html);
        Assert.DoesNotContain(StaleTblCustAddress, html);
    }

    [Fact]
    public async Task Quote_UsesTblCustAddress_WhenNoLead_AndSessionEmpty()
    {
        await using var bookingDb = CreateBookingDb(nameof(Quote_UsesTblCustAddress_WhenNoLead_AndSessionEmpty));
        await using var appDb = CreateAppDb(nameof(Quote_UsesTblCustAddress_WhenNoLead_AndSessionEmpty));

        await SeedBookingAsync(bookingDb, StaleTblCustAddress);
        // No lead record.

        var svc = new HtmlQuoteGenerationService(bookingDb, appDb, new TestEnv(_tempWebRoot), NullLogger<HtmlQuoteGenerationService>.Instance);
        var (success, url, _) = await svc.GenerateHtmlQuoteForBookingAsync(BookingNo, session: null);

        Assert.True(success);
        var html = await File.ReadAllTextAsync(ResolveHtmlPath(url!));
        Assert.Contains(StaleTblCustAddress, html);
    }

    [Fact]
    public async Task Quote_PrefersSessionAddress_OverLeadAndTblCust()
    {
        await using var bookingDb = CreateBookingDb(nameof(Quote_PrefersSessionAddress_OverLeadAndTblCust));
        await using var appDb = CreateAppDb(nameof(Quote_PrefersSessionAddress_OverLeadAndTblCust));

        await SeedBookingAsync(bookingDb, StaleTblCustAddress);
        await SeedLeadAsync(appDb, LeadFormAddress);

        var session = new InMemSession();
        session.SetString("Draft:OrganisationAddress", SessionOverrideAddress);

        var svc = new HtmlQuoteGenerationService(bookingDb, appDb, new TestEnv(_tempWebRoot), NullLogger<HtmlQuoteGenerationService>.Instance);
        var (success, url, _) = await svc.GenerateHtmlQuoteForBookingAsync(BookingNo, session: session);

        Assert.True(success);
        var html = await File.ReadAllTextAsync(ResolveHtmlPath(url!));
        Assert.Contains(SessionOverrideAddress, html);
        Assert.DoesNotContain(LeadFormAddress, html);
        Assert.DoesNotContain(StaleTblCustAddress, html);
    }

    private string ResolveHtmlPath(string url)
    {
        var rel = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_tempWebRoot, rel);
    }

    private static async Task SeedBookingAsync(BookingDbContext db, string tblCustAddress)
    {
        var cust = new TblCust
        {
            ID = 99999m,
            OrganisationV6 = "Coastal Trade Solutions Pty Ltd",
            Address_l1V6 = tblCustAddress,
            Customer_code = "C99999",
            CustCDate = DateTime.UtcNow,
        };
        db.TblCusts.Add(cust);

        var venue = new TblVenue
        {
            ID = 20,
            VenueName = "The Westin Brisbane",
        };
        db.TblVenues.Add(venue);

        var contact = new TblContact
        {
            Id = 12345m,
            Contactname = "Benjamin Clarke",
            Email = "ben@example.test",
            Phone1 = "0400000000",
        };
        db.Contacts.Add(contact);

        var booking = new TblBooking
        {
            ID = 777m,
            booking_no = BookingNo,
            order_no = BookingNo,
            CustID = cust.ID,
            CustCode = cust.Customer_code,
            ContactID = contact.Id,
            contact_nameV6 = contact.Contactname,
            OrganizationV6 = cust.OrganisationV6,
            VenueID = (int)venue.ID,
            VenueRoom = "Westin Ballroom 1",
            dDate = new DateTime(2026, 4, 18),
            rDate = new DateTime(2026, 4, 18),
            SDate = new DateTime(2026, 4, 18),
            ShowSDate = new DateTime(2026, 4, 18),
            ShowEdate = new DateTime(2026, 4, 18),
            SetDate = new DateTime(2026, 4, 18),
            RehDate = new DateTime(2026, 4, 18),
            order_date = new DateTime(2026, 4, 15),
            EntryDate = new DateTime(2026, 4, 15),
            status = 0,
            BookingProgressStatus = 1,
            booking_type_v32 = 2,
            Salesperson = "JEJ",
            invoiced = "N",
        };
        db.TblBookings.Add(booking);
        await db.SaveChangesAsync();
    }

    private static async Task SeedLeadAsync(AppDbContext db, string leadAddress)
    {
        db.WestinLeads.Add(new WestinLead
        {
            Token = Guid.NewGuid(),
            Organisation = "Coastal Trade Solutions Pty Ltd",
            OrganisationAddress = leadAddress,
            FirstName = "Benjamin",
            LastName = "Clarke",
            Email = "ben@example.test",
            PhoneNumber = "0400000000",
            EventStartDate = "2026-04-18",
            EventEndDate = "2026-04-18",
            Venue = "The Westin Brisbane",
            Room = "Westin Ballroom 1",
            Attendees = "50",
            CreatedUtc = DateTime.UtcNow,
            BookingNo = BookingNo,
        });
        await db.SaveChangesAsync();
    }

    private static BookingDbContext CreateBookingDb(string name)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(name + "-booking-" + Guid.NewGuid().ToString("N")[..8])
            .Options;
        return new BookingDbContext(options);
    }

    private static AppDbContext CreateAppDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + "-app-" + Guid.NewGuid().ToString("N")[..8])
            .Options;
        return new AppDbContext(options);
    }

    private sealed class TestEnv : IWebHostEnvironment
    {
        public TestEnv(string root)
        {
            ContentRootPath = root;
            WebRootPath = root;
            ContentRootFileProvider = new PhysicalFileProvider(root);
            WebRootFileProvider = new PhysicalFileProvider(root);
        }
        public string ApplicationName { get; set; } = "MicrohireAgentChat.Tests";
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
    }

    private sealed class InMemSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);
        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
