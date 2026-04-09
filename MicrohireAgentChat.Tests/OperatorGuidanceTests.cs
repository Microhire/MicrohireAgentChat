using System.Reflection;
using System.Text.Json;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class OperatorGuidanceTests
{
    [Fact]
    public void FormatProductKnowledgeJson_IncludesOperatorRequirementSection()
    {
        const string json = """
        {
          "description": "Test scope",
          "categories": [
            {
              "id": "computers-playback",
              "name": "Computers & Playback Systems",
              "operationalNotes": "Basic playback notes.",
              "operationMode": "self_operated",
              "operationGuidance": "Can be self-operated after setup."
            }
          ]
        }
        """;

        var method = typeof(AgentToolHandlerService).GetMethod(
            "FormatProductKnowledgeJson",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var formatted = (string?)method!.Invoke(null, new object?[] { json, null, "Test Scope" });
        Assert.NotNull(formatted);
        Assert.Contains("**Operator requirement**", formatted);
        Assert.Contains("- Mode: Self-operated", formatted);
        Assert.Contains("- Guidance: Can be self-operated after setup.", formatted);
    }

    [Fact]
    public async Task RecommendLaborForEquipmentAsync_DoesNotAddDefaultLabor_ForSelfOperatedOnlySimpleSetup()
    {
        var webRoot = CreateKnowledgeDataRoot();
        await using var db = CreateDb(nameof(RecommendLaborForEquipmentAsync_DoesNotAddDefaultLabor_ForSelfOperatedOnlySimpleSetup));

        var service = new SmartEquipmentRecommendationService(
            db,
            new TestWebHostEnvironment(webRoot),
            NullLogger<SmartEquipmentRecommendationService>.Instance,
            new TestWestinRoomCatalog());

        var labor = await service.RecommendLaborForEquipmentAsync(
            new[]
            {
                new EquipmentItemForLabor
                {
                    ProductCode = "",
                    Description = "Macbook Pro 13\" - Show Laptop",
                    Quantity = 1
                }
            },
            new EventContext
            {
                EventType = "presentation",
                ExpectedAttendees = 80
            },
            CancellationToken.None);

        Assert.Empty(labor);
    }

    [Fact]
    public async Task RecommendLaborForEquipmentAsync_AddsLabor_ForOperatorRequiredCategory()
    {
        var webRoot = CreateKnowledgeDataRoot();
        await using var db = CreateDb(nameof(RecommendLaborForEquipmentAsync_AddsLabor_ForOperatorRequiredCategory));

        var service = new SmartEquipmentRecommendationService(
            db,
            new TestWebHostEnvironment(webRoot),
            NullLogger<SmartEquipmentRecommendationService>.Instance,
            new TestWestinRoomCatalog());

        var labor = await service.RecommendLaborForEquipmentAsync(
            new[]
            {
                new EquipmentItemForLabor
                {
                    ProductCode = "",
                    Description = "CO2 jet effect package",
                    Quantity = 1
                }
            },
            new EventContext
            {
                EventType = "launch",
                ExpectedAttendees = 10
            },
            CancellationToken.None);

        var requiredLabor = Assert.Single(labor);
        Assert.Equal("AV Technician", requiredLabor.Description);
        Assert.Contains("Operator-required equipment selected", requiredLabor.RecommendationReason);
    }

    [Fact]
    public async Task GetRecommendationsAsync_Prefers_LOGISPOT_Clicker_WhenAvailable()
    {
        var webRoot = CreateKnowledgeDataRoot();
        await using var db = CreateDb(nameof(GetRecommendationsAsync_Prefers_LOGISPOT_Clicker_WhenAvailable));

        db.TblInvmas.AddRange(
            new TblInvmas
            {
                product_code = "LOGISPOT",
                category = "COMP",
                descriptionv6 = "Logitech Spotlight Presentation Remote"
            },
            new TblInvmas
            {
                product_code = "WIRPRES",
                category = "COMP",
                descriptionv6 = "Logitech Wireless Presenter"
            });

        db.TblRatetbls.AddRange(
            new TblRatetbl
            {
                ID = 1,
                product_code = "LOGISPOT",
                TableNo = 0,
                rate_1st_day = 45
            },
            new TblRatetbl
            {
                ID = 2,
                product_code = "WIRPRES",
                TableNo = 0,
                rate_1st_day = 30
            });

        await db.SaveChangesAsync();

        var service = new SmartEquipmentRecommendationService(
            db,
            new TestWebHostEnvironment(webRoot),
            NullLogger<SmartEquipmentRecommendationService>.Instance,
            new TestWestinRoomCatalog());

        var context = new EventContext
        {
            EventType = "presentation",
            ExpectedAttendees = 50,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "clicker", Quantity = 1 }
            }
        };

        var recommendation = await service.GetRecommendationsAsync(context, CancellationToken.None);
        var clicker = Assert.Single(recommendation.Items);
        Assert.Equal("LOGISPOT", clicker.ProductCode);
    }

    [Fact]
    public async Task RecommendLaborForEquipmentAsync_UsesSingleAvTechnician_WhenMixerAndSwitcherPresent()
    {
        var webRoot = CreateKnowledgeDataRoot();
        await using var db = CreateDb(nameof(RecommendLaborForEquipmentAsync_UsesSingleAvTechnician_WhenMixerAndSwitcherPresent));

        var service = new SmartEquipmentRecommendationService(
            db,
            new TestWebHostEnvironment(webRoot),
            NullLogger<SmartEquipmentRecommendationService>.Instance,
            new TestWestinRoomCatalog());

        var labor = await service.RecommendLaborForEquipmentAsync(
            new[]
            {
                new EquipmentItemForLabor
                {
                    ProductCode = "V1HD",
                    Description = "HDMI switcher",
                    Quantity = 1
                },
                new EquipmentItemForLabor
                {
                    ProductCode = "MIXER06",
                    Description = "6-channel mixer",
                    Quantity = 1
                }
            },
            new EventContext
            {
                EventType = "presentation",
                ExpectedAttendees = 120
            },
            CancellationToken.None);

        var avTech = Assert.Single(labor);
        Assert.Equal("AV Technician", avTech.Description);
        Assert.Contains("MIXER06 with V1HD", avTech.RecommendationReason);
    }

    [Fact]
    public async Task RecommendLaborForEquipmentAsync_AppliesWestinRoomPackageRules_WhenPackageAndSwitcherPresent()
    {
        var webRoot = CreateKnowledgeDataRoot(includeWestinLaborRules: true);
        await using var db = CreateDb(nameof(RecommendLaborForEquipmentAsync_AppliesWestinRoomPackageRules_WhenPackageAndSwitcherPresent));

        var service = new SmartEquipmentRecommendationService(
            db,
            new TestWebHostEnvironment(webRoot),
            NullLogger<SmartEquipmentRecommendationService>.Instance,
            new TestWestinRoomCatalog());

        var labor = await service.RecommendLaborForEquipmentAsync(
            new[]
            {
                new EquipmentItemForLabor
                {
                    ProductCode = "WBDPROJ",
                    Description = "Westin Ballroom Dual Projector Package",
                    Quantity = 1
                },
                new EquipmentItemForLabor
                {
                    ProductCode = "V1HD",
                    Description = "HDMI switcher",
                    Quantity = 1
                }
            },
            new EventContext
            {
                EventType = "presentation",
                VenueName = "Westin Brisbane",
                RoomName = "Westin Ballroom 1",
                ExpectedAttendees = 120
            },
            CancellationToken.None);

        Assert.Contains(labor, l => l.ProductCode == "AVTECH" && l.Task == "Setup" && l.Minutes >= 90);
        // V1HD no longer removes baseline T&C — it is kept alongside VXTECH Rehearsal
        Assert.Contains(labor, l => l.ProductCode == "AVTECH" && l.Task == "Test & Connect" && l.Minutes >= 30);
        Assert.Contains(labor, l => l.ProductCode == "AVTECH" && l.Task == "Packdown" && l.Minutes >= 30);
        Assert.Contains(labor, l => l.ProductCode == "VXTECH" && l.Task == "Rehearsal" && l.Minutes == 30);
        Assert.Contains(labor, l => l.ProductCode == "VXTECH" && l.Task == "Operate");
    }

    private static BookingDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    private static string CreateKnowledgeDataRoot(bool includeWestinLaborRules = false)
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "microhire-tests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(webRoot, "data");
        Directory.CreateDirectory(dataDir);

        var root = new
        {
            categories = new object[]
            {
                new { id = "computers-playback", operationMode = "self_operated" },
                new { id = "special-effects", operationMode = "operator_required" }
            }
        };

        var json = JsonSerializer.Serialize(root);
        File.WriteAllText(Path.Combine(dataDir, "product-knowledge-master.json"), json);

        if (includeWestinLaborRules)
        {
            const string rulesJson = """
            {
              "rooms": [
                {
                  "roomKey": "ballroom",
                  "roomContains": ["ballroom"],
                  "packageCodes": ["WBDPROJ"],
                  "baselineLaborCode": "AVTECH",
                  "visionSpecialistCode": "VXTECH",
                  "audioSpecialistCode": "AXTECH",
                  "microphoneOperatorThreshold": 2
                }
              ],
              "specialRules": {
                "switcherCode": "V1HD",
                "mixerCode": "MIXER06",
                "flipchartCode": "NATFLIPC",
                "videoConferenceCode": "LOG4kCAM",
                "lecternCodes": ["LECT1", "SHURE418"]
              }
            }
            """;
            File.WriteAllText(Path.Combine(dataDir, "westin-labor-rules.json"), rulesJson);
        }

        return webRoot;
    }

    private sealed class TestWestinRoomCatalog : IWestinRoomCatalog
    {
        public Task<List<WestinRoom>> GetRoomsAsync(CancellationToken ct = default) => Task.FromResult(new List<WestinRoom>());

        public IReadOnlyList<(string Slug, string Name)> GetVenueConfirmRoomOptions() =>
            Array.Empty<(string, string)>();
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
            ContentRootPath = webRootPath;
            WebRootFileProvider = new NullFileProvider();
            ContentRootFileProvider = new NullFileProvider();
        }

        public string ApplicationName { get; set; } = "MicrohireAgentChat.Tests";
        public IFileProvider WebRootFileProvider { get; set; }
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
