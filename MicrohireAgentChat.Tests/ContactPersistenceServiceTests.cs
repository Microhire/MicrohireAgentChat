using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class ContactPersistenceServiceTests
{
    private static BookingDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task UpsertContactAsync_RepairsAssistantArtifacts_OnEmailMatch()
    {
        await using var db = CreateDb(nameof(UpsertContactAsync_RepairsAssistantArtifacts_OnEmailMatch));
        db.Contacts.Add(new TblContact
        {
            Id = 1001,
            Email = "dhan@dhancorp.com",
            Contactname = "Isla from Microhire",
            Firstname = "Isla",
            MidName = "from",
            Surname = "Microhire",
            Active = "Y"
        });
        await db.SaveChangesAsync();

        var service = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);

        var contactRes = await service.UpsertContactAsync(
            "Dhan Manalo",
            "dhan@dhancorp.com",
            "+61411111111",
            null,
            CancellationToken.None);

        Assert.Equal(1001, (int?)contactRes.Id);
        Assert.Equal("updated", contactRes.Action);

        var saved = await db.Contacts.SingleAsync(c => c.Email == "dhan@dhancorp.com");
        Assert.Equal("Dhan Manalo", saved.Contactname);
        Assert.Equal("Dhan", saved.Firstname);
        Assert.Null(saved.MidName);
        Assert.Equal("Manalo", saved.Surname);
    }

    [Fact]
    public async Task UpsertContactAsync_PersistsValidSplitNames()
    {
        await using var db = CreateDb(nameof(UpsertContactAsync_PersistsValidSplitNames));
        db.Contacts.Add(new TblContact
        {
            Id = 2001,
            Email = "dhan@dhancorp.com",
            Contactname = null,
            Firstname = null,
            MidName = null,
            Surname = null,
            Active = "Y"
        });
        await db.SaveChangesAsync();

        var service = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);

        var contactRes = await service.UpsertContactAsync(
            "Dhan Manalo",
            "dhan@dhancorp.com",
            null,
            "Director",
            CancellationToken.None);

        Assert.Equal(2001, (int?)contactRes.Id);
        Assert.Equal("updated", contactRes.Action);

        var saved = await db.Contacts.SingleAsync(c => c.Email == "dhan@dhancorp.com");
        Assert.Equal("Dhan", saved.Firstname);
        Assert.Equal("Manalo", saved.Surname);
    }

    [Fact]
    public async Task UpsertContactAsync_DoesNotPersistAssistantNameTokens()
    {
        await using var db = CreateDb(nameof(UpsertContactAsync_DoesNotPersistAssistantNameTokens));
        db.Contacts.Add(new TblContact
        {
            Id = 3001,
            Email = "client@example.com",
            Contactname = null,
            Firstname = null,
            MidName = null,
            Surname = null,
            Active = "Y"
        });
        await db.SaveChangesAsync();

        var service = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);

        var contactRes = await service.UpsertContactAsync(
            "Isla from Microhire",
            "client@example.com",
            null,
            null,
            CancellationToken.None);

        Assert.Equal(3001, (int?)contactRes.Id);
        Assert.Equal("updated", contactRes.Action);

        var saved = await db.Contacts.SingleAsync(c => c.Email == "client@example.com");
        Assert.Null(saved.Contactname);
        Assert.Null(saved.Firstname);
        Assert.Null(saved.MidName);
        Assert.Null(saved.Surname);
    }
}
