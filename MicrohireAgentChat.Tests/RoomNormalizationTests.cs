using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Tests;

/// <summary>
/// Verifies that projector-area labels are never embedded in VenueRoom when persisting
/// bookings, and that legacy values containing them are stripped at quote render time.
/// </summary>
public sealed class RoomNormalizationTests
{
    private static BookingDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new BookingDbContext(opts);
    }

    private static BookingPersistenceService CreateBookingService(BookingDbContext db)
    {
        var chatExtractor = new ChatExtractionService(NullLogger<ChatExtractionService>.Instance);
        var contactService = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);
        var orgService = new OrganizationPersistenceService(db, NullLogger<OrganizationPersistenceService>.Instance);
        var itemService = new ItemPersistenceService(db, NullLogger<ItemPersistenceService>.Instance);
        var crewService = new CrewPersistenceService(db, NullLogger<CrewPersistenceService>.Instance);
        return new BookingPersistenceService(
            db,
            chatExtractor,
            itemService,
            crewService,
            contactService,
            orgService,
            Options.Create(new RentalPointDefaultsOptions()),
            NullLogger<BookingPersistenceService>.Instance);
    }

    private static readonly Regex ProjectorSuffixPattern = new(
        @"\s*\(Proj(?:ector)?\s+[A-F](?:/[A-F])*\)$|\s*-\s*Projector\s+Area",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Verifies that updating an existing booking with projector area facts writes a clean room name.
    /// The pre-existing record simulates a legacy booking previously saved with the old suffix format.
    /// (In-memory EF cannot auto-generate decimal PKs, so only update paths can be integration-tested this way.)
    /// </summary>
    [Fact]
    public async Task SaveBookingAsync_StripsPreviouslyStoredProjectorSuffix_WhenRoomIsUpdated()
    {
        await using var db = CreateDb(nameof(SaveBookingAsync_StripsPreviouslyStoredProjectorSuffix_WhenRoomIsUpdated));
        var service = CreateBookingService(db);

        // Simulate a pre-existing booking that was saved with the old suffix logic.
        var legacyBookingNo = "TEST-LEGACY-001";
        db.TblBookings.Add(new MicrohireAgentChat.Models.TblBooking
        {
            ID         = 9001m,
            booking_no = legacyBookingNo,
            VenueRoom  = "Westin Ballroom (Proj A/D)",
            CustID     = 1
        });
        await db.SaveChangesAsync();

        // Update via facts — new logic should write a clean room name.
        var facts = new Dictionary<string, string>
        {
            ["venue_room"]      = "Westin Ballroom (Proj A/D)",  // agent might still pass the old value
            ["projector_areas"] = "A,D",
            ["event_date"]      = "2027-01-20"
        };

        await service.SaveBookingAsync(legacyBookingNo, facts, null, null, null, null, null, CancellationToken.None);

        db.ChangeTracker.Clear();
        var updated = db.TblBookings.Single(b => b.booking_no == legacyBookingNo);
        Assert.False(ProjectorSuffixPattern.IsMatch(updated.VenueRoom ?? ""),
            $"Updated VenueRoom should be clean, but was: '{updated.VenueRoom}'");
        Assert.Equal("Westin Ballroom", updated.VenueRoom);
    }

    [Theory]
    [InlineData("Westin Ballroom (Proj A)", "Westin Ballroom")]
    [InlineData("Westin Ballroom (Proj A/D)", "Westin Ballroom")]
    [InlineData("Westin Ballroom (Projector A)", "Westin Ballroom")]
    [InlineData("Westin Ballroom - Projector Area", "Westin Ballroom")]
    [InlineData("Westin Ballroom - Projector Area C", "Westin Ballroom")]
    [InlineData("Westin Ballroom - Projector Areas", "Westin Ballroom")]
    [InlineData("Westin Ballroom", "Westin Ballroom")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void StripProjectorAreaSuffix_RemovesAllKnownPatterns(string? input, string? expected)
    {
        var result = StripProjectorAreaSuffixHelper(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Mirrors the private static helper in the quote services so the logic can be tested directly.
    /// </summary>
    private static string? StripProjectorAreaSuffixHelper(string? venueRoom)
    {
        if (string.IsNullOrWhiteSpace(venueRoom)) return null;
        var s = venueRoom.Trim();
        s = Regex.Replace(s, @"\s*-\s*Projector\s+Area(?:s)?\s*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*-\s*Projector\s+Area(?:s)?\s+[A-F](?:/[A-F])*$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s*\(Proj(?:ector)?\s+[A-F](?:/[A-F])*\)$", "", RegexOptions.IgnoreCase);
        return s.Trim();
    }
}
