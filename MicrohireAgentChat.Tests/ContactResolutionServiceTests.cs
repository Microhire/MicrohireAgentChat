using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicrohireAgentChat.Tests;

public sealed class ContactResolutionServiceTests
{
    private class TestBookingDbContext : BookingDbContext
    {
        public TestBookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Add value generators for decimal IDs since InMemory provider doesn't support them automatically
            modelBuilder.Entity<TblContact>().Property(x => x.Id).HasValueGenerator<DecimalIdGenerator>();
            modelBuilder.Entity<TblCust>().Property(x => x.ID).HasValueGenerator<DecimalIdGenerator>();
            modelBuilder.Entity<TblLinkCustContact>().Property(x => x.ID).HasValueGenerator<DecimalIdGenerator>();
        }
    }

    private class DecimalIdGenerator : ValueGenerator<decimal>
    {
        private long _current = 1000;
        public override bool GeneratesTemporaryValues => false;
        public override decimal Next(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry) 
            => System.Threading.Interlocked.Increment(ref _current);
    }

    private static BookingDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TestBookingDbContext(options);
    }

    [Fact]
    public async Task ResolveAsync_ReusesContact_WhenLinkedToOrg()
    {
        // Arrange
        await using var db = CreateDb(nameof(ResolveAsync_ReusesContact_WhenLinkedToOrg));
        
        // Existing contact
        var contact = new TblContact
        {
            Id = 100m,
            Email = "test@example.com",
            Contactname = "Existing Contact",
            Active = "Y"
        };
        db.Contacts.Add(contact);

        // Existing org
        var org = new TblCust
        {
            ID = 500m,
            OrganisationV6 = "Test Org",
            Customer_code = "C00500"
        };
        db.TblCusts.Add(org);

        // Link them
        db.TblLinkCustContacts.Add(new TblLinkCustContact
        {
            ID = 1m,
            Customer_Code = "C00500",
            ContactID = 100m
        });

        await db.SaveChangesAsync();

        var contactService = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);
        var orgService = new OrganizationPersistenceService(db, NullLogger<OrganizationPersistenceService>.Instance);
        var resolutionService = new ContactResolutionService(contactService, orgService, NullLogger<ContactResolutionService>.Instance);

        // Act
        var res = await resolutionService.ResolveAsync(
            "New Name", // Submitted name is different
            "test@example.com",
            null,
            null,
            "Test Org",
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal(100m, res.contactId);
        Assert.Equal("reused", res.contactAction);
        Assert.Equal(500m, res.orgId);
        Assert.Equal("reused", res.orgAction);

        // Verify contact details were NOT updated
        var savedContact = await db.Contacts.FindAsync(100m);
        Assert.Equal("Existing Contact", savedContact.Contactname);
    }

    [Fact]
    public async Task ResolveAsync_CreatesNewContact_WhenNotLinkedToOrg()
    {
        // Arrange
        await using var db = CreateDb(nameof(ResolveAsync_CreatesNewContact_WhenNotLinkedToOrg));
        
        // Existing contact (but not linked to this org)
        var contact = new TblContact
        {
            Id = 200m,
            Email = "user@other.com",
            Contactname = "Old Contact",
            Active = "Y"
        };
        db.Contacts.Add(contact);

        // Existing org
        var org = new TblCust
        {
            ID = 600m,
            OrganisationV6 = "Target Org",
            Customer_code = "C00600"
        };
        db.TblCusts.Add(org);

        await db.SaveChangesAsync();

        var contactService = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);
        var orgService = new OrganizationPersistenceService(db, NullLogger<OrganizationPersistenceService>.Instance);
        var resolutionService = new ContactResolutionService(contactService, orgService, NullLogger<ContactResolutionService>.Instance);

        // Act
        var res = await resolutionService.ResolveAsync(
            "New Contact",
            "user@other.com",
            null,
            null,
            "Target Org",
            null,
            CancellationToken.None);

        // Assert
        Assert.NotNull(res.contactId);
        Assert.NotEqual(200m, res.contactId); // Should be new
        Assert.Equal("created", res.contactAction);
        Assert.Equal(600m, res.orgId);
        Assert.Equal("reused", res.orgAction);

        // Verify link was created
        var linkExists = await db.TblLinkCustContacts.AnyAsync(l => l.Customer_Code == "C00600" && l.ContactID == res.contactId);
        Assert.True(linkExists);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotUpdateExistingOrgFields()
    {
        // Arrange
        await using var db = CreateDb(nameof(ResolveAsync_DoesNotUpdateExistingOrgFields));
        
        // Existing org
        var org = new TblCust
        {
            ID = 700m,
            OrganisationV6 = "Original Name",
            Address_l1V6 = "Old Address",
            Customer_code = "C00700"
        };
        db.TblCusts.Add(org);

        await db.SaveChangesAsync();

        var contactService = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);
        var orgService = new OrganizationPersistenceService(db, NullLogger<OrganizationPersistenceService>.Instance);
        var resolutionService = new ContactResolutionService(contactService, orgService, NullLogger<ContactResolutionService>.Instance);

        // Act
        var res = await resolutionService.ResolveAsync(
            "Contact Name",
            "contact@example.com",
            null,
            null,
            "Original Name",
            "New Address Attempt",
            CancellationToken.None);

        // Assert
        Assert.Equal(700m, res.orgId);
        Assert.Equal("reused", res.orgAction);

        // Verify org fields were NOT updated
        var savedOrg = await db.TblCusts.FindAsync(700m);
        Assert.Equal("Original Name", savedOrg.OrganisationV6);
        Assert.Equal("Old Address", savedOrg.Address_l1V6);
    }
}
