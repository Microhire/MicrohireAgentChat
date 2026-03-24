using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class ContactPersistenceServicePhoneLookupTests
{
    private static BookingDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task UpsertContactAsync_MatchesByPhoneWhenEmailDifferent_UpdatesSameRow()
    {
        await using var db = CreateDb(nameof(UpsertContactAsync_MatchesByPhoneWhenEmailDifferent_UpdatesSameRow));
        db.Contacts.Add(new TblContact
        {
            Id = 5001,
            Email = "old@example.com",
            Cell = "0411999888",
            Contactname = "Pat Lee",
            Active = "Y",
        });
        await db.SaveChangesAsync();

        var service = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);

        var res = await service.UpsertContactAsync(
            "Pat Lee",
            "newemail@example.com",
            "0411999888",
            null,
            CancellationToken.None);

        Assert.Equal(5001m, res.Id);
        Assert.Equal("updated", res.Action);

        var saved = await db.Contacts.SingleAsync(c => c.Id == 5001);
        Assert.Equal("newemail@example.com", saved.Email);
    }
}
