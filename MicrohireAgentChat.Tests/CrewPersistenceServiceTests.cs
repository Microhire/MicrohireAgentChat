using System.Text.Json;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class CrewPersistenceServiceTests
{
    private static BookingDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    [Fact]
    public async Task InsertCrewRowsAsync_UsesStructuredSelectedLabor_WithMixedCodesAndTaskDurations()
    {
        await using var db = CreateDb(nameof(InsertCrewRowsAsync_UsesStructuredSelectedLabor_WithMixedCodesAndTaskDurations));
        var service = new CrewPersistenceService(db, NullLogger<CrewPersistenceService>.Instance);

        const string selectedLaborJson = """
        [
          {
            "productCode": "AXTECH",
            "description": "Audio Technician",
            "task": "Rehearsal",
            "quantity": 1,
            "hours": 0,
            "minutes": 30
          },
          {
            "productCode": "VXTECH",
            "description": "Vision Technician",
            "task": "Operate",
            "quantity": 1,
            "hours": 0,
            "minutes": 0
          }
        ]
        """;

        var facts = new Dictionary<string, string>
        {
            ["selected_labor"] = selectedLaborJson,
            ["setup_time"] = "0830",
            ["show_start_time"] = "0900",
            ["show_end_time"] = "1100"
        };

        await service.InsertCrewRowsAsync("B-100", facts, CancellationToken.None);

        var rows = await db.TblCrews
            .Where(r => r.BookingNoV32 == "B-100")
            .ToListAsync();

        Assert.Equal(2, rows.Count);

        var audio = Assert.Single(rows, r => r.ProductCodeV42 == "AXTECH");
        Assert.Equal((byte)7, audio.Task); // rehearsal
        Assert.Equal((byte)0, audio.Hours);
        Assert.Equal((byte)30, audio.Minutes);
        Assert.Equal((byte)8, audio.DelTimeHour);
        Assert.Equal((byte)30, audio.DelTimeMin);
        // 30-minute rehearsal from setup 08:30 ends 09:00 (not +1h)
        Assert.Equal((byte)9, audio.ReturnTimeHour);
        Assert.Equal((byte)0, audio.ReturnTimeMin);

        var vision = Assert.Single(rows, r => r.ProductCodeV42 == "VXTECH");
        Assert.Equal((byte)3, vision.Task); // operate
        Assert.Equal((byte)2, vision.Hours); // 09:00 - 11:00 duration
        Assert.Equal((byte)0, vision.Minutes);
        Assert.Equal((byte)9, vision.DelTimeHour);
        Assert.Equal((byte)0, vision.DelTimeMin);
        Assert.Equal((byte)11, vision.ReturnTimeHour);
        Assert.Equal((byte)0, vision.ReturnTimeMin);
    }

    [Fact]
    public async Task InsertCrewRowsAsync_UsesCodeSpecificFallbackRate_WhenDatabaseRatesUnavailable()
    {
        await using var db = CreateDb(nameof(InsertCrewRowsAsync_UsesCodeSpecificFallbackRate_WhenDatabaseRatesUnavailable));
        var service = new CrewPersistenceService(db, NullLogger<CrewPersistenceService>.Instance);

        var selectedLabor = new List<SelectedLaborItem>
        {
            new()
            {
                ProductCode = "SAVTECH",
                Description = "Senior AV Technician",
                Task = "Setup",
                Quantity = 1,
                Hours = 1
            }
        };

        var facts = new Dictionary<string, string>
        {
            ["selected_labor"] = JsonSerializer.Serialize(selectedLabor),
            ["setup_time"] = "0800"
        };

        await service.InsertCrewRowsAsync("B-200", facts, CancellationToken.None);

        var row = Assert.Single(db.TblCrews.Where(r => r.BookingNoV32 == "B-200"));
        Assert.Equal("SAVTECH", row.ProductCodeV42);
        Assert.Equal(125d, row.UnitRate);
    }

    [Fact]
    public async Task InsertCrewRowsAsync_ParsesSelectedLabor_WithCaseInsensitiveJsonPayload()
    {
        await using var db = CreateDb(nameof(InsertCrewRowsAsync_ParsesSelectedLabor_WithCaseInsensitiveJsonPayload));
        var service = new CrewPersistenceService(db, NullLogger<CrewPersistenceService>.Instance);

        const string selectedLaborJson = """
        [
          {
            "productcode": "axtech",
            "description": "Audio Technician",
            "task": "Setup",
            "quantity": 1,
            "hours": 1.5,
            "minutes": 0
          }
        ]
        """;

        var facts = new Dictionary<string, string>
        {
            ["selected_labor"] = selectedLaborJson,
            ["setup_time"] = "0830"
        };

        await service.InsertCrewRowsAsync("B-300", facts, CancellationToken.None);

        var row = Assert.Single(db.TblCrews.Where(r => r.BookingNoV32 == "B-300"));
        Assert.Equal("AXTECH", row.ProductCodeV42);
        Assert.Equal((byte)2, row.Task); // setup (TryParseTask: "setup" -> 2)
    }

    [Fact]
    public async Task InsertCrewRowsAsync_FallsBackToLaborSummary_WhenSelectedLaborJsonIsInvalid()
    {
        await using var db = CreateDb(nameof(InsertCrewRowsAsync_FallsBackToLaborSummary_WhenSelectedLaborJsonIsInvalid));
        var service = new CrewPersistenceService(db, NullLogger<CrewPersistenceService>.Instance);

        var facts = new Dictionary<string, string>
        {
            ["selected_labor"] = "{bad json}",
            ["labor_summary"] = "1x Technician @ 2 hours",
            ["setup_time"] = "0900"
        };

        await service.InsertCrewRowsAsync("B-400", facts, CancellationToken.None);

        var row = Assert.Single(db.TblCrews.Where(r => r.BookingNoV32 == "B-400"));
        Assert.Equal(1, row.TransQty);
        Assert.Equal((byte)2, row.Hours);
    }
}
