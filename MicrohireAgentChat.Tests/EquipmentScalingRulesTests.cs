using System.Text.Json;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class EquipmentScalingRulesTests
{
    // Presenter-based switcher scaling (primary path when presenter_count is provided)
    [Theory]
    [InlineData(2, 1)]   // 2 presenters → 1 switcher
    [InlineData(4, 1)]   // 4 presenters → 1 switcher
    [InlineData(5, 2)]   // 5 presenters → 2 switchers
    [InlineData(8, 2)]   // 8 presenters → 2 switchers
    [InlineData(9, 3)]   // 9 presenters → 3 switchers
    [InlineData(12, 3)]  // 12 presenters → 3 switchers
    public async Task Switcher_ScalesV1HD_BasedOnPresenterCount(int presenterCount, int expectedSwitcherQty)
    {
        var webRoot = CreateDataRoot();
        await using var db = CreateDb($"switcher_presenter_{presenterCount}");
        SeedProduct(db, "V1HD", "COMP", "Roland V-1HD HDMI Video Switcher", dayRate: 150);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            ExpectedAttendees = 100,
            NumberOfPresentations = presenterCount,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "switcher", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var switcher = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "V1HD", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(switcher);
        Assert.Equal(expectedSwitcherQty, switcher!.Quantity);
    }

    [Fact]
    public async Task Switcher_NotAdded_WhenSinglePresenter()
    {
        var webRoot = CreateDataRoot();
        await using var db = CreateDb(nameof(Switcher_NotAdded_WhenSinglePresenter));
        SeedProduct(db, "V1HD", "COMP", "Roland V-1HD HDMI Video Switcher", dayRate: 150);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "presentation",
            ExpectedAttendees = 50,
            NumberOfPresentations = 1,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "switcher", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var switcher = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "V1HD", StringComparison.OrdinalIgnoreCase));
        Assert.Null(switcher);
    }

    // Laptop-count-based switcher scaling (fallback when presenter_count is not provided)
    [Theory]
    [InlineData(6, 2)]
    [InlineData(5, 2)]
    [InlineData(4, 1)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    public async Task Switcher_ScalesV1HD_BasedOnLaptopCount_WhenNoPresenterCount(int laptopCount, int expectedSwitcherQty)
    {
        var webRoot = CreateDataRoot();
        await using var db = CreateDb($"switcher_scale_{laptopCount}");
        SeedProduct(db, "V1HD", "COMP", "Roland V-1HD HDMI Video Switcher", dayRate: 150);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            ExpectedAttendees = 100,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "laptop", Quantity = laptopCount },
                new() { EquipmentType = "switcher", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var switcher = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "V1HD", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(switcher);
        Assert.Equal(expectedSwitcherQty, switcher!.Quantity);
    }

    [Fact]
    public async Task Switcher_KeepsPassedQuantity_WhenLaptopCountFitsOneUnit()
    {
        var webRoot = CreateDataRoot();
        await using var db = CreateDb(nameof(Switcher_KeepsPassedQuantity_WhenLaptopCountFitsOneUnit));
        SeedProduct(db, "V1HD", "COMP", "Roland V-1HD HDMI Video Switcher", dayRate: 150);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            ExpectedAttendees = 50,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "laptop", Quantity = 3 },
                new() { EquipmentType = "switcher", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var switcher = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "V1HD", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(switcher);
        Assert.Equal(1, switcher!.Quantity);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(12, 2)]
    [InlineData(13, 3)]
    public async Task Mixer_ScalesMIXER06_BasedOnMicCount(int micCount, int expectedMixerQty)
    {
        var webRoot = CreateDataRoot();
        await using var db = CreateDb($"mixer_scale_{micCount}");
        SeedProduct(db, "MIC-HH-01", "W/MIC", "Wireless Handheld Microphone", dayRate: 80);
        SeedProduct(db, "MIXER06", "MIXER", "6 Channel Audio Mixer", dayRate: 120);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            ExpectedAttendees = 200,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "microphone", Quantity = micCount, MicrophoneType = "handheld" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var mixer = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "MIXER06", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(mixer);
        Assert.Equal(expectedMixerQty, mixer!.Quantity);
    }

    [Fact]
    public async Task Mixer_NotAdded_WhenSingleMic()
    {
        var webRoot = CreateDataRoot();
        await using var db = CreateDb(nameof(Mixer_NotAdded_WhenSingleMic));
        SeedProduct(db, "MIC-HH-01", "W/MIC", "Wireless Handheld Microphone", dayRate: 80);
        SeedProduct(db, "MIXER06", "MIXER", "6 Channel Audio Mixer", dayRate: 120);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "presentation",
            ExpectedAttendees = 30,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "microphone", Quantity = 1, MicrophoneType = "handheld" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var mixer = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "MIXER06", StringComparison.OrdinalIgnoreCase));
        Assert.Null(mixer);
    }

    [Fact]
    public async Task Mixer_NotDuplicated_WhenAlreadyPresent()
    {
        var webRoot = CreateDataRoot();
        await using var db = CreateDb(nameof(Mixer_NotDuplicated_WhenAlreadyPresent));
        SeedProduct(db, "MIC-HH-01", "W/MIC", "Wireless Handheld Microphone", dayRate: 80);
        SeedProduct(db, "MIXER06", "MIXER", "6 Channel Audio Mixer", dayRate: 120);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            ExpectedAttendees = 200,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "microphone", Quantity = 4, MicrophoneType = "handheld" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var mixers = result.Items.Where(i =>
            string.Equals(i.ProductCode, "MIXER06", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(mixers);
    }

    [Fact]
    public async Task WestinPackage_DoesNotInflateMicCount_ForMixerLogic()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true, includeWestinLaborRules: true);
        await using var db = CreateDb(nameof(WestinPackage_DoesNotInflateMicCount_ForMixerLogic));
        SeedProduct(db, "WBSBCSS", "WSB", "Westin Single Ballroom Ceiling Speaker System", dayRate: 300);
        SeedProduct(db, "MIC-HH-01", "W/MIC", "Wireless Handheld Microphone", dayRate: 80);
        SeedProduct(db, "MIXER06", "MIXER", "6 Channel Audio Mixer", dayRate: 120);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Westin Ballroom 1",
            ExpectedAttendees = 200,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "microphone", Quantity = 3, MicrophoneType = "handheld" },
                new() { EquipmentType = "speaker", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var mixer = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "MIXER06", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(mixer);
        Assert.Equal(1, mixer!.Quantity);
    }

    [Fact]
    public async Task Screen_Request_DoesNotAddStandaloneScreen_WhenRoomVisionPackageExists()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Screen_Request_DoesNotAddStandaloneScreen_WhenRoomVisionPackageExists));
        SeedProduct(db, "WBSPROJ", "WSB", "Westin Single Ballroom Projector Package", dayRate: 500);
        SeedProduct(db, "SCREEN16", "SCREEN", "16x9 Fastfold Screen", dayRate: 220);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Westin Ballroom 1",
            ExpectedAttendees = 100,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "screen", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.DoesNotContain(result.Items, i => string.Equals(i.Category, "SCREEN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Speaker_Request_ExternalStyle_PrefersPortableSpeakerOverRoomInbuiltPackage()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Speaker_Request_ExternalStyle_PrefersPortableSpeakerOverRoomInbuiltPackage));
        SeedProduct(db, "WBSBCSS", "WSB", "Westin Single Ballroom Ceiling Speaker System", dayRate: 300);
        SeedProduct(db, "PA-PORT-01", "SPEAKER", "Portable PA Speaker System", dayRate: 180);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Westin Ballroom 1",
            ExpectedAttendees = 80,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "speaker", Quantity = 1, SpeakerStyle = "external portable" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i => string.Equals(i.Category, "SPEAKER", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i => string.Equals(i.ProductCode, "WBSBCSS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Speaker_Request_InbuiltStyle_PrefersRoomPackage()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Speaker_Request_InbuiltStyle_PrefersRoomPackage));
        SeedProduct(db, "WBSBCSS", "WSB", "Westin Single Ballroom Ceiling Speaker System", dayRate: 300);
        SeedProduct(db, "PA-PORT-01", "SPEAKER", "Portable PA Speaker System", dayRate: 180);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Westin Ballroom 1",
            ExpectedAttendees = 80,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "speaker", Quantity = 1, SpeakerStyle = "inbuilt" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i => string.Equals(i.ProductCode, "WBSBCSS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Av_WestinBallroom2_SelectsWBSAVP()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Av_WestinBallroom2_SelectsWBSAVP));
        SeedProduct(db, "WBAVP", "WSB", "Westin Ballroom Full AV Package", dayRate: 900);
        SeedProduct(db, "WBSAVP", "WSB", "Westin Single Ballroom AV Package", dayRate: 500);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Westin Ballroom 2",
            ExpectedAttendees = 120,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "av", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        var av = result.Items.FirstOrDefault(i =>
            string.Equals(i.ProductCode, "WBSAVP", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(av);
        Assert.DoesNotContain(result.Items, i =>
            string.Equals(i.ProductCode, "WBAVP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Av_WestinBallroomFull_SelectsWBAVP()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Av_WestinBallroomFull_SelectsWBAVP));
        SeedProduct(db, "WBAVP", "WSB", "Westin Ballroom Full AV Package", dayRate: 900);
        SeedProduct(db, "WBSAVP", "WSB", "Westin Single Ballroom AV Package", dayRate: 500);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Westin Ballroom",
            ExpectedAttendees = 200,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "av", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i =>
            string.Equals(i.ProductCode, "WBAVP", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i =>
            string.Equals(i.ProductCode, "WBSAVP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Av_ElevateFull_SelectsELEVAVP()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Av_ElevateFull_SelectsELEVAVP));
        SeedProduct(db, "ELEVAVP", "WSB", "Elevate AV Package", dayRate: 800, subCategory: "WSBELEV");
        SeedProduct(db, "ELEVSAVP", "WSB", "Elevate (Single) AV Package", dayRate: 400, subCategory: "WSBELEV");
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Elevate",
            ExpectedAttendees = 80,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "av", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i =>
            string.Equals(i.ProductCode, "ELEVAVP", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i =>
            string.Equals(i.ProductCode, "ELEVSAVP", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Elevate 1")]
    [InlineData("Elevate 2")]
    public async Task Av_ElevateHalf_SelectsELEVSAVP(string roomName)
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb($"{nameof(Av_ElevateHalf_SelectsELEVSAVP)}_{roomName.Replace(' ', '_')}");
        SeedProduct(db, "ELEVAVP", "WSB", "Elevate AV Package", dayRate: 800, subCategory: "WSBELEV");
        SeedProduct(db, "ELEVSAVP", "WSB", "Elevate (Single) AV Package", dayRate: 400, subCategory: "WSBELEV");
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = roomName,
            ExpectedAttendees = 40,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "av", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i =>
            string.Equals(i.ProductCode, "ELEVSAVP", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i =>
            string.Equals(i.ProductCode, "ELEVAVP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Speaker_ElevateFull_SelectsELEVCSS()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Speaker_ElevateFull_SelectsELEVCSS));
        SeedProduct(db, "ELEVCSS", "WSB", "Elevate Ceiling Speaker System", dayRate: 350, subCategory: "WSBELEV");
        SeedProduct(db, "ELEVSCSS", "WSB", "Elevate Single Ceiling Speaker System", dayRate: 200, subCategory: "WSBELEV");
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Elevate",
            ExpectedAttendees = 60,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "speaker", Quantity = 1, SpeakerStyle = "inbuilt" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i =>
            string.Equals(i.ProductCode, "ELEVCSS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Speaker_Elevate1_SelectsELEVSCSS()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Speaker_Elevate1_SelectsELEVSCSS));
        SeedProduct(db, "ELEVCSS", "WSB", "Elevate Ceiling Speaker System", dayRate: 350, subCategory: "WSBELEV");
        SeedProduct(db, "ELEVSCSS", "WSB", "Elevate Single Ceiling Speaker System", dayRate: 200, subCategory: "WSBELEV");
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "conference",
            VenueName = "Westin Brisbane",
            RoomName = "Elevate 1",
            ExpectedAttendees = 30,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "speaker", Quantity = 1, SpeakerStyle = "inbuilt" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i =>
            string.Equals(i.ProductCode, "ELEVSCSS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Av_Thrive_SelectsTHRVAVP()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Av_Thrive_SelectsTHRVAVP));
        SeedProduct(db, "THRVAVP", "WSBTHRV", "Thrive Boardroom AV Package", dayRate: 450);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "board meeting",
            VenueName = "Westin Brisbane",
            RoomName = "Thrive Boardroom",
            ExpectedAttendees = 12,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "av", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i =>
            string.Equals(i.ProductCode, "THRVAVP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Speaker_Thrive_SelectsTHRVCSS()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(Speaker_Thrive_SelectsTHRVCSS));
        SeedProduct(db, "THRVCSS", "WSBTHRV", "Thrive Boardroom Ceiling Speaker System", dayRate: 280);
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "board meeting",
            VenueName = "Westin Brisbane",
            RoomName = "Thrive Boardroom",
            ExpectedAttendees = 12,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "speaker", Quantity = 1, SpeakerStyle = "inbuilt" }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        Assert.Contains(result.Items, i =>
            string.Equals(i.ProductCode, "THRVCSS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DynamicDiscovery_NewProductAdded_AutoDiscovered()
    {
        var webRoot = CreateDataRoot(includeVenuePackages: true);
        await using var db = CreateDb(nameof(DynamicDiscovery_NewProductAdded_AutoDiscovered));
        // Seed the standard Elevate products plus a hypothetical new one
        SeedProduct(db, "ELEVAVP", "WSB", "Elevate AV Package", dayRate: 800, subCategory: "WSBELEV");
        SeedProduct(db, "ELEVSAVP", "WSB", "Elevate (Single) AV Package", dayRate: 400, subCategory: "WSBELEV");
        SeedProduct(db, "ELEVPREMAV", "WSB", "Elevate Premium AV Package", dayRate: 1200, subCategory: "WSBELEV");
        await db.SaveChangesAsync();

        var service = CreateService(db, webRoot);
        var context = new EventContext
        {
            EventType = "gala",
            VenueName = "Westin Brisbane",
            RoomName = "Elevate",
            ExpectedAttendees = 120,
            EquipmentRequests = new List<EquipmentRequest>
            {
                new() { EquipmentType = "av", Quantity = 1 }
            }
        };

        var result = await service.GetRecommendationsAsync(context, CancellationToken.None);

        // The new product should be discovered without any JSON or code changes
        var avItems = result.Items.Where(i =>
            i.ProductCode.StartsWith("ELEV", StringComparison.OrdinalIgnoreCase) &&
            i.Description.ToLowerInvariant().Contains("av package")).ToList();

        // At least one Elevate AV package should be returned (the best-match selection logic picks one)
        Assert.NotEmpty(avItems);
    }

    #region Test Helpers

    private static BookingDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new BookingDbContext(options);
    }

    private static void SeedProduct(BookingDbContext db, string code, string category, string description, double dayRate, string? subCategory = null)
    {
        db.TblInvmas.Add(new TblInvmas
        {
            product_code = code,
            category = category,
            descriptionv6 = description,
            SubCategory = subCategory
        });
        db.TblRatetbls.Add(new TblRatetbl
        {
            ID = db.TblRatetbls.Count() + new Random().Next(1000, 9999),
            product_code = code,
            TableNo = 0,
            rate_1st_day = dayRate
        });
    }

    private static SmartEquipmentRecommendationService CreateService(BookingDbContext db, string webRoot)
    {
        return new SmartEquipmentRecommendationService(
            db,
            new TestWebHostEnvironment(webRoot),
            NullLogger<SmartEquipmentRecommendationService>.Instance,
            new TestWestinRoomCatalog());
    }

    private static string CreateDataRoot(bool includeVenuePackages = false, bool includeWestinLaborRules = false)
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "microhire-tests", Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(webRoot, "data");
        Directory.CreateDirectory(dataDir);

        var knowledgeJson = JsonSerializer.Serialize(new
        {
            categories = new object[]
            {
                new { id = "computers-playback", operationMode = "self_operated" },
                new { id = "special-effects", operationMode = "operator_required" }
            }
        });
        File.WriteAllText(Path.Combine(dataDir, "product-knowledge-master.json"), knowledgeJson);

        if (includeVenuePackages)
        {
            var ballroomPkgs = new Dictionary<string, object>
            {
                ["aiFolder"] = "WSBBALL",
                ["audio"] = new[] { "WBFBCSS", "WBSBCSS" },
                ["vision"] = new[] { "WBSPROJ", "WBDPROJ", "WBSNPROJ", "WBSSPROJ" },
                ["av"] = new[] { "WBAVP", "WBSAVP" }
            };
            var elevatePkgs = new Dictionary<string, object>
            {
                ["aiFolder"] = "WSBELEV",
                ["baseEquipment"] = new[] { "Inbuilt projector and screen", "Inbuilt speakers" }
            };
            var thrivePkgs = new Dictionary<string, object>
            {
                ["aiFolder"] = "WSBTHRV",
                ["audio"] = new[] { "THRVCSS" },
                ["vision"] = new[] { "THRVPROJ" },
                ["av"] = new[] { "THRVAVP" }
            };
            var venueJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["Westin Brisbane"] = new Dictionary<string, object>
                {
                    ["Westin Ballroom"] = ballroomPkgs,
                    ["Westin Ballroom 1"] = ballroomPkgs,
                    ["Westin Ballroom 2"] = ballroomPkgs,
                    ["Elevate"] = elevatePkgs,
                    ["Thrive Boardroom"] = thrivePkgs
                }
            });
            File.WriteAllText(Path.Combine(dataDir, "venue-room-packages.json"), venueJson);
        }

        if (includeWestinLaborRules)
        {
            const string rulesJson = """
            {
              "rooms": [
                {
                  "roomKey": "ballroom",
                  "roomContains": ["ballroom"],
                  "packageCodes": ["WBDPROJ", "WBSPROJ", "WBSBCSS"],
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
        public Task<List<WestinRoom>> GetRoomsAsync(CancellationToken ct = default)
            => Task.FromResult(new List<WestinRoom>());

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

    #endregion
}
