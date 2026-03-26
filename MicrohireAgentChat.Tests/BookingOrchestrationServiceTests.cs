using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Orchestration;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MicrohireAgentChat.Tests;

public sealed class BookingOrchestrationServiceTests
{
    private static BookingDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task SaveContactAndOrganizationAsync_DoesNotCreateContact_WhenNameMissing()
    {
        await using var db = CreateDb(nameof(SaveContactAndOrganizationAsync_DoesNotCreateContact_WhenNameMissing));

        var extractor = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var contactService = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);
        var orgService = new OrganizationPersistenceService(db, NullLogger<OrganizationPersistenceService>.Instance);
        var resolution = new ContactResolutionService(contactService, orgService, NullLogger<ContactResolutionService>.Instance);

        var orchestration = new BookingOrchestrationService(
            db,
            extractor,
            resolution,
            null!,
            null!,
            null!,
            NullLogger<BookingOrchestrationService>.Instance);

        var now = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new DisplayMessage("assistant", now.AddMinutes(-1), new[] { "Can I get your email?" }),
            new DisplayMessage("user", now, new[] { "My email is dhan@dhancorp.com" })
        };

        var (contactId, _) = await orchestration.SaveContactAndOrganizationAsync(messages, CancellationToken.None);

        Assert.Null(contactId);
        Assert.Empty(db.Contacts);
    }

    [Fact]
    public async Task ProcessConversationAsync_PersistsCrew_WhenOnlySelectedLaborIsProvided()
    {
        await using var db = CreateDb(nameof(ProcessConversationAsync_PersistsCrew_WhenOnlySelectedLaborIsProvided));

        var extractor = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);
        var chatExtractor = new ChatExtractionService(NullLogger<ChatExtractionService>.Instance);
        var contactService = new ContactPersistenceService(db, NullLogger<ContactPersistenceService>.Instance);
        var orgService = new OrganizationPersistenceService(db, NullLogger<OrganizationPersistenceService>.Instance);
        var resolution = new ContactResolutionService(contactService, orgService, NullLogger<ContactResolutionService>.Instance);
        var itemService = new ItemPersistenceService(db, NullLogger<ItemPersistenceService>.Instance);
        var crewService = new CrewPersistenceService(db, NullLogger<CrewPersistenceService>.Instance);
        var bookingService = new BookingPersistenceService(
            db,
            chatExtractor,
            itemService,
            crewService,
            resolution,
            Options.Create(new RentalPointDefaultsOptions()),
            NullLogger<BookingPersistenceService>.Instance);

        var orchestration = new BookingOrchestrationService(
            db,
            extractor,
            resolution,
            bookingService,
            itemService,
            crewService,
            NullLogger<BookingOrchestrationService>.Instance);

        var messages = new[]
        {
            new DisplayMessage("user", DateTimeOffset.UtcNow, new[] { "Please create a quote." })
        };

        var selectedLabor = JsonSerializer.Serialize(new List<SelectedLaborItem>
        {
            new()
            {
                ProductCode = "VXTECH",
                Task = "Operate",
                Quantity = 1,
                Hours = 2,
                Minutes = 0
            }
        });

        var additionalFacts = new Dictionary<string, string>
        {
            ["selected_labor"] = selectedLabor,
            ["event_date"] = "2026-05-08",
            ["show_start_time"] = "0900",
            ["show_end_time"] = "1100",
            ["setup_time"] = "0800"
        };

        var result = await orchestration.ProcessConversationAsync(messages, null, CancellationToken.None, additionalFacts);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.BookingNo));
        Assert.NotEmpty(db.TblCrews.Where(c => c.BookingNoV32 == result.BookingNo));
    }
}
