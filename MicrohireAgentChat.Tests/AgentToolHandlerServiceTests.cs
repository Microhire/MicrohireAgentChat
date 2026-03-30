using System.Linq;
using System.Text.Json;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicrohireAgentChat.Tests;

public sealed class AgentToolHandlerServiceTests
{
    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenEventTypeMissing()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp"
        });

        var argsJson = """
        {
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("missing required event type", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_UsesSessionEventTypeWhenMissingInArgs()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "24",
            ["Draft:SetupStyle"] = "theatre"
        });

        var argsJson = """
        {
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing required event type", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenAttendeesMissing()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:SetupStyle"] = "theatre"
        });

        var argsJson = """
        {
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("missing required attendee count", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenSetupStyleMissing()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "24"
        });

        var argsJson = """
        {
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("missing required room setup style", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRoomImages_ForThrive_ReturnsCoverOnlyBlock()
    {
        var rooms = new List<WestinRoom>
        {
            new(
                1,
                "Thrive Boardroom",
                "thrive-boardroom",
                "Level 1",
                "/images/thrive/cover.jpg",
                new List<RoomLayout>
                {
                    new("Theatre", 20, "/images/thrive/theatre.png"),
                    new("Boardroom", 10, "/images/thrive/boardroom.png")
                })
        };

        var service = CreateServiceWithSession(new Dictionary<string, string>(), rooms);

        var result = await service.HandleToolCallAsync(
            "get_room_images",
            """{ "room": "thrive-boardroom" }""",
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("Here is the Thrive Boardroom", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"fixedLayout\":\"boardroom\"", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Here are the room and setup options", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Theatre", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cabaret", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("thrive")]
    [InlineData("Thrive Boardroom")]
    [InlineData("thrive-boardroom")]
    public async Task GetRoomImages_ForThriveNameVariants_ReturnsCoverOnlyBlock(string roomArg)
    {
        var rooms = new List<WestinRoom>
        {
            new(
                1,
                "Thrive Boardroom",
                "thrive-boardroom",
                "Level 1",
                "/images/thrive/cover.jpg",
                new List<RoomLayout>
                {
                    new("Theatre", 20, "/images/thrive/theatre.png"),
                    new("Boardroom", 10, "/images/thrive/boardroom.png")
                })
        };

        var service = CreateServiceWithSession(new Dictionary<string, string>(), rooms);
        var argsJson = $$"""{"room": "{{roomArg}}"}""";

        var result = await service.HandleToolCallAsync(
            "get_room_images",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("Here is the Thrive Boardroom", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"fixedLayout\":\"boardroom\"", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Here are the room and setup options", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_AllowsThriveWithoutExplicitSetupStyle()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "workshop",
            ["Draft:ExpectedAttendees"] = "6"
        });

        var argsJson = """
        {
          "venue_name": "Demo Venue",
          "room_name": "Thrive Boardroom",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing required room setup style", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateEquipment_AllowsThriveWithoutExplicitSetupStyle()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:SelectedEquipment"] = """[{ "ProductCode": "ABC123", "Description": "Wireless microphone package", "Quantity": 2 }]""",
            ["Draft:EventType"] = "workshop",
            ["Draft:ExpectedAttendees"] = "6",
            ["Draft:RoomName"] = "Thrive Boardroom"
        });

        var result = await service.HandleToolCallAsync(
            "update_equipment",
            "{}",
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing required event details", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("room setup style", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRoomCapacity_ForThriveBoardroom_Returns10()
    {
        var rooms = new List<WestinRoom>
        {
            new(
                1,
                "Thrive Boardroom",
                "thrive-boardroom",
                "Level 1",
                "/images/thrive/cover.jpg",
                new List<RoomLayout>
                {
                    new("Boardroom", 10, "/images/thrive/boardroom.png"),
                    new("Theatre", 0, "/images/thrive/theatre.png")
                })
        };

        var service = CreateServiceWithSession(new Dictionary<string, string>(), rooms);

        var result = await service.HandleToolCallAsync(
            "get_room_capacity",
            """{"room_name": "thrive", "setup_type": "boardroom"}""",
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("10", result!);
        Assert.Contains("Thrive Boardroom", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_ForThrive_WithAllowedEquipment_DoesNotRequireSetupStyle()
    {
        // Verifies Thrive accepts allowed equipment types (laptop, clicker, flipchart) without setup style.
        // Equipment search may fail in unit test (empty DB); we assert Thrive-specific validation passes.
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "meeting",
            ["Draft:ExpectedAttendees"] = "6",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Thrive Boardroom",
            ["Draft:LaptopOwnershipAnswered"] = "1",
            ["Draft:NeedsProvidedLaptop"] = "1",
            ["Draft:LaptopPreference"] = "mac"
        });

        var argsJson = """
        {
          "venue_name": "Westin Brisbane",
          "room_name": "Thrive Boardroom",
          "equipment_requests": [
            { "equipment_type": "laptop", "quantity": 1, "preference": "mac" },
            { "equipment_type": "clicker", "quantity": 1 },
            { "equipment_type": "flipchart", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing required", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missing required room setup style", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateEquipment_UsesKnownEventDetailsWithoutFallbackDefaults()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:SelectedEquipment"] = """[{ "ProductCode": "ABC123", "Description": "Wireless microphone package", "Quantity": 2 }]""",
            ["Draft:EventType"] = "training",
            ["Draft:ExpectedAttendees"] = "30",
            ["Draft:SetupStyle"] = "classroom"
        });

        var result = await service.HandleToolCallAsync(
            "update_equipment",
            "{}",
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("**Event Type:** training", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("**Attendees:** 30", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("**Event Type:** meeting", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("**Attendees:** 8", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenWestinBallroomProjectorAreaMissing()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "80",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom 1"
        });

        var argsJson = """
        {
          "event_type": "presentation",
          "expected_attendees": 80,
          "setup_style": "theatre",
          "venue_name": "Westin Brisbane",
          "room_name": "Westin Ballroom 1",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("missing projector placement area", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenWestinBallroomSplitNotSpecified()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "120",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom"
        });

        var argsJson = """
        {
          "event_type": "presentation",
          "expected_attendees": 120,
          "setup_style": "theatre",
          "venue_name": "Westin Brisbane",
          "room_name": "Westin Ballroom",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("split not specified", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full Westin Ballroom, Westin Ballroom 1, or Westin Ballroom 2", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_DoesNotBlockForProjectorAreaWhenProjectionNotNeeded()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "meeting",
            ["Draft:ExpectedAttendees"] = "20",
            ["Draft:SetupStyle"] = "boardroom"
        });

        var argsJson = """
        {
          "event_type": "meeting",
          "expected_attendees": 20,
          "setup_style": "boardroom",
          "venue_name": "Westin Brisbane",
          "room_name": "Westin Ballroom 2",
          "equipment_requests": [
            { "equipment_type": "microphone", "quantity": 2 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing projector placement area", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_PreservesCapturedProjectorAreaAcrossNonProjectionReentry()
    {
        // Westin Ballroom 1 allows areas E, D, C — and requires 2 areas.
        // "A" is only valid for Ballroom 2, so use "E,D" here.
        var (service, session) = CreateServiceHarness(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "80",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom 1",
            ["Draft:ProjectorAreas"] = "E,D",
            ["Draft:ProjectorArea"] = "E",
            ["Draft:ProjectorPromptShown"] = "1",
            ["Draft:ProjectorPromptThreadId"] = "thread-test",
            ["Draft:ProjectorAreaCaptured"] = "1",
            ["Draft:ProjectorAreaThreadId"] = "thread-test"
        });

        var nonProjectionArgs = """
        {
          "event_type": "presentation",
          "expected_attendees": 80,
          "setup_style": "theatre",
          "equipment_requests": [
            { "equipment_type": "microphone", "quantity": 1 }
          ]
        }
        """;

        await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            nonProjectionArgs,
            "thread-test",
            CancellationToken.None);

        Assert.Equal("E", session.GetString("Draft:ProjectorArea"));
        Assert.Equal("E,D", session.GetString("Draft:ProjectorAreas"));

        var projectionArgs = """
        {
          "event_type": "presentation",
          "expected_attendees": 80,
          "setup_style": "theatre",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            projectionArgs,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing projector placement area", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_DoesNotRepeatProjectorPromptAfterTechnicianStagesAnswered()
    {
        // Regression: projector placement must not be re-asked on the turn where the user provides
        // technician stage coverage, even when the tool is called again to finalise the quote.
        // Scenario: user answered "A and D" (already captured in session), then answered "all stages"
        // for technician coverage.  The third call to recommend_equipment_for_event must not re-ask
        // for projector areas.
        var (service, session) = CreateServiceHarness(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Michael Hall",
            ["Draft:ContactEmail"] = "michael_hall@megasoft.com",
            ["Draft:Organisation"] = "MegaSoft Group",
            ["Draft:EventType"] = "conference",
            ["Draft:ExpectedAttendees"] = "80",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom",
            ["Draft:StartTime"] = "10:00",
            ["Draft:EventDate"] = "2026-03-27",
            // Projector areas already captured (simulates session state after user said "A and D")
            ["Draft:ProjectorAreas"] = "A,D",
            ["Draft:ProjectorArea"] = "A",
            ["Draft:ProjectorPromptShown"] = "1",
            ["Draft:ProjectorPromptThreadId"] = "thread-westin",
            ["Draft:ProjectorAreaCaptured"] = "1",
            ["Draft:ProjectorAreaThreadId"] = "thread-westin",
            // Technician coverage already captured (simulates session state after user said "all stages")
            ["Draft:TechnicianCoverage"] = """{"NoTechnicianSupport":false,"Setup":true,"Rehearsal":true,"Operate":true,"Packdown":true}"""
        });

        // Args that match the follow-up call after technician stages are answered;
        // the AI may only pass venue_name without room_name on a re-entry call.
        var argsJson = """
        {
          "event_type": "conference",
          "expected_attendees": 80,
          "setup_style": "theatre",
          "venue_name": "Westin Brisbane",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 2 },
            { "equipment_type": "screen",    "quantity": 2 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-westin",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing projector placement area", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_ProjectorStateSurvivesVenueOnlyArg()
    {
        // When venue_name is in args but room_name is absent, the clear guard must NOT fire.
        // Previously, HasExplicitVenueOrRoomInArgs returned true for venue-only args, which
        // could wipe captured projector areas on a follow-up tool call.
        var (service, session) = CreateServiceHarness(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "conference",
            ["Draft:ExpectedAttendees"] = "80",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom",
            ["Draft:ProjectorAreas"] = "A,D",
            ["Draft:ProjectorArea"] = "A",
            ["Draft:ProjectorPromptShown"] = "1",
            ["Draft:ProjectorPromptThreadId"] = "thread-test",
            ["Draft:ProjectorAreaCaptured"] = "1",
            ["Draft:ProjectorAreaThreadId"] = "thread-test"
        });

        // Call with only venue_name (no room_name) — the problematic case.
        var argsJson = """
        {
          "event_type": "conference",
          "expected_attendees": 80,
          "setup_style": "theatre",
          "venue_name": "Westin Brisbane",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 2 }
          ]
        }
        """;

        await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        // Projector areas must NOT have been cleared by the clear guard.
        Assert.Equal("A,D", session.GetString("Draft:ProjectorAreas"));
        Assert.Equal("A", session.GetString("Draft:ProjectorArea"));
        Assert.Equal("1", session.GetString("Draft:ProjectorAreaCaptured"));
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_UsesSessionLaptopPreferenceWhenAlreadyCaptured()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "40",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:LaptopOwnershipAnswered"] = "1",
            ["Draft:NeedsProvidedLaptop"] = "1",
            ["Draft:LaptopPreference"] = "mac"
        });

        var argsJson = """
        {
          "event_type": "presentation",
          "expected_attendees": 40,
          "setup_style": "theatre",
          "equipment_requests": [
            { "equipment_type": "laptop", "quantity": 2 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing laptop preference", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenLaptopOwnershipMissing_ForLaptopWorkflowAccessories()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "meeting",
            ["Draft:ExpectedAttendees"] = "10",
            ["Draft:SetupStyle"] = "boardroom",
            ["Draft:TechnicianCoverage"] = """{"NoTechnicianSupport":false,"Setup":true,"Rehearsal":true,"Operate":true,"Packdown":true}"""
        });

        var argsJson = """
        {
          "event_type": "meeting",
          "expected_attendees": 10,
          "setup_style": "boardroom",
          "equipment_requests": [
            { "equipment_type": "clicker", "quantity": 1 },
            { "equipment_type": "hdmi_adaptor", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("missing laptop ownership", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Are you bringing your own laptop or do you need one provided by us?", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_DoesNotBlockLaptopOwnership_WhenOwnLaptopAlreadyConfirmed()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "meeting",
            ["Draft:ExpectedAttendees"] = "10",
            ["Draft:SetupStyle"] = "boardroom",
            ["Draft:LaptopOwnershipAnswered"] = "1",
            ["Draft:NeedsProvidedLaptop"] = "0",
            ["Draft:TechnicianCoverage"] = """{"NoTechnicianSupport":false,"Setup":true,"Rehearsal":true,"Operate":true,"Packdown":true}"""
        });

        var argsJson = """
        {
          "event_type": "meeting",
          "expected_attendees": 10,
          "setup_style": "boardroom",
          "equipment_requests": [
            { "equipment_type": "clicker", "quantity": 1 },
            { "equipment_type": "hdmi_adaptor", "quantity": 1 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("missing laptop ownership", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateEquipment_SummaryIncludesProjectorPlacementArea()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:SelectedEquipment"] = """[{ "ProductCode": "ABC123", "Description": "Projector package", "Quantity": 1 }]""",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "40",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom",
            ["Draft:ProjectorArea"] = "C"
        });

        var result = await service.HandleToolCallAsync(
            "update_equipment",
            "{}",
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("Projector Placement Area", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateEquipment_HidesStaleProjectorPlacementArea_WhenProjectionNotRequested()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:SelectedEquipment"] = """[{ "ProductCode": "ABC123", "Description": "Wireless microphone package", "Quantity": 1 }]""",
            ["Draft:EventType"] = "meeting",
            ["Draft:ExpectedAttendees"] = "20",
            ["Draft:SetupStyle"] = "boardroom",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom",
            ["Draft:ProjectorAreas"] = "A,D",
            ["Draft:ProjectorArea"] = "A"
        });

        var result = await service.HandleToolCallAsync(
            "update_equipment",
            "{}",
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.DoesNotContain("Projector Placement Area", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Projector Placement Areas", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenMultipleProjectorsNeedMoreAreas()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "120",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:ProjectorAreas"] = "E",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom 1"
        });

        var argsJson = """
        {
          "event_type": "presentation",
          "expected_attendees": 120,
          "setup_style": "theatre",
          "venue_name": "Westin Brisbane",
          "room_name": "Westin Ballroom 1",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 2 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("missing projector placement area", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenAreasInvalidForBallroomRoomSplit()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:ContactName"] = "Gold Roger",
            ["Draft:ContactEmail"] = "gold@roger.com",
            ["Draft:Organisation"] = "One Piece Corp",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "120",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:ProjectorAreas"] = "E,D",
            ["Draft:VenueName"] = "Westin Brisbane",
            ["Draft:RoomName"] = "Westin Ballroom 2"
        });

        var argsJson = """
        {
          "event_type": "presentation",
          "expected_attendees": 120,
          "setup_style": "theatre",
          "venue_name": "Westin Brisbane",
          "room_name": "Westin Ballroom 2",
          "equipment_requests": [
            { "equipment_type": "projector", "quantity": 2 }
          ]
        }
        """;

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            argsJson,
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("missing projector placement area", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateEquipment_AcceptsSelectedEquipmentWithPackageMetadata()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:SelectedEquipment"] = """[{ "ProductCode":"PKG-1", "Description":"Westin Single Projector Package", "Quantity":1, "IsPackage":true, "ParentPackageCode":null }]""",
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "40",
            ["Draft:SetupStyle"] = "theatre"
        });

        var result = await service.HandleToolCallAsync(
            "update_equipment",
            "{}",
            "thread-test",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("\"success\":true", result!, StringComparison.OrdinalIgnoreCase);
    }

    #region Transcript Validation Tests

    [Fact]
    public void ExtractAttendeesFromUserMessages_ReturnsZero_WhenUserNeverMentionedAttendees()
    {
        var messages = new List<DisplayMessage>
        {
            new("assistant", DateTimeOffset.UtcNow, new[] { "How many attendees are you expecting?" }),
            new("user", DateTimeOffset.UtcNow, new[] { "I've selected this schedule: Setup 7AM; Start 10AM; End 5PM" }),
        };

        var result = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ExtractAttendeesFromUserMessages_ReturnsZero_WhenNextReplyIsChooseSchedulePayload()
    {
        var messages = new List<DisplayMessage>
        {
            new("assistant", DateTimeOffset.UtcNow, new[] { "How many attendees are you expecting?" }),
            new("user", DateTimeOffset.UtcNow, new[] { "Choose schedule: date=2026-05-05; setup=07:00; rehearsal=09:30; start=10:00; end=16:00; packup=18:00" }),
        };

        var result = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ExtractAttendeesFromUserMessages_ExtractsCount_WhenUserSaysDirectly()
    {
        var messages = new List<DisplayMessage>
        {
            new("assistant", DateTimeOffset.UtcNow, new[] { "How many attendees are you expecting?" }),
            new("user", DateTimeOffset.UtcNow, new[] { "about 50 people" }),
        };

        var result = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
        Assert.Equal(50, result);
    }

    [Fact]
    public void ExtractAttendeesFromUserMessages_ExtractsCount_WhenUserRespondsWithJustANumber()
    {
        var messages = new List<DisplayMessage>
        {
            new("assistant", DateTimeOffset.UtcNow, new[] { "How many attendees are you expecting?" }),
            new("user", DateTimeOffset.UtcNow, new[] { "75" }),
        };

        var result = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
        Assert.Equal(75, result);
    }

    [Fact]
    public void ExtractAttendeesFromUserMessages_ExtractsCount_WithExpectingPrefix()
    {
        var messages = new List<DisplayMessage>
        {
            new("user", DateTimeOffset.UtcNow, new[] { "We're expecting 120 for the conference" }),
        };

        var result = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
        Assert.Equal(120, result);
    }

    [Fact]
    public void ExtractAttendeesFromUserMessages_ReturnsZero_WhenNoUserMessages()
    {
        var messages = new List<DisplayMessage>
        {
            new("assistant", DateTimeOffset.UtcNow, new[] { "There will be 30 attendees" }),
        };

        var result = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ExtractSetupStyleFromUserMessages_ReturnsNull_WhenUserNeverMentionedStyle()
    {
        var messages = new List<DisplayMessage>
        {
            new("user", DateTimeOffset.UtcNow, new[] { "A team meeting at the westin brisbane" }),
            new("user", DateTimeOffset.UtcNow, new[] { "yes please" }),
        };

        var result = AgentToolHandlerService.ExtractSetupStyleFromUserMessages(messages);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractSetupStyleFromUserMessages_ExtractsStyle_WhenUserMentionsTheatre()
    {
        var messages = new List<DisplayMessage>
        {
            new("user", DateTimeOffset.UtcNow, new[] { "We need theatre style setup" }),
        };

        var result = AgentToolHandlerService.ExtractSetupStyleFromUserMessages(messages);
        Assert.Equal("theatre", result);
    }

    [Fact]
    public void ExtractSetupStyleFromUserMessages_ExtractsStyle_WhenUserMentionsBoardroom()
    {
        var messages = new List<DisplayMessage>
        {
            new("user", DateTimeOffset.UtcNow, new[] { "boardroom layout please" }),
        };

        var result = AgentToolHandlerService.ExtractSetupStyleFromUserMessages(messages);
        Assert.Equal("boardroom", result);
    }

    [Fact]
    public void ExtractSetupStyleFromUserMessages_IgnoresAssistantMessages()
    {
        var messages = new List<DisplayMessage>
        {
            new("assistant", DateTimeOffset.UtcNow, new[] { "I've noted theatre setup" }),
            new("user", DateTimeOffset.UtcNow, new[] { "yes that sounds good" }),
        };

        var result = AgentToolHandlerService.ExtractSetupStyleFromUserMessages(messages);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractSetupStyleFromUserMessages_UsesLatestUserStyle_WhenUserChangesMind()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("user", now.AddMinutes(-3), new[] { "boardroom setup please" }),
            new("assistant", now.AddMinutes(-2), new[] { "Would you like to stay with boardroom or move to theatre?" }),
            new("user", now.AddMinutes(-1), new[] { "yes theatre" }),
        };

        var result = AgentToolHandlerService.ExtractSetupStyleFromUserMessages(messages);
        Assert.Equal("theatre", result);
    }

    [Fact]
    public void ExtractRoomFromUserMessages_ReturnsNull_WhenOnlyAssistantSuggestsRoom()
    {
        var messages = new List<DisplayMessage>
        {
            new("assistant", DateTimeOffset.UtcNow, new[] { "Thrive Boardroom would work well for your meeting." }),
            new("user", DateTimeOffset.UtcNow, new[] { "Sounds good." }),
        };

        var result = AgentToolHandlerService.ExtractRoomFromUserMessages(messages);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractRoomFromUserMessages_ExtractsRoom_WhenUserStatesRoom()
    {
        var messages = new List<DisplayMessage>
        {
            new("user", DateTimeOffset.UtcNow, new[] { "Let's use Westin Brisbane Thrive Room." }),
        };

        var result = AgentToolHandlerService.ExtractRoomFromUserMessages(messages);
        Assert.Equal("Thrive Boardroom", result);
    }

    [Fact]
    public void ExtractRoomFromUserMessages_UsesLatestRoom_WhenUserChangesSelection()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("user", now.AddMinutes(-2), new[] { "Let's go with Westin Ballroom." }),
            new("user", now.AddMinutes(-1), new[] { "Actually choose room: elevate" }),
        };

        var result = AgentToolHandlerService.ExtractRoomFromUserMessages(messages);
        Assert.Equal("Elevate", result);
    }

    [Theory]
    [InlineData("thrive")]
    [InlineData("Thrive")]
    [InlineData("THRIVE")]
    [InlineData("thrive boardroom")]
    [InlineData("thrive room")]
    [InlineData("Thrive Boardroom")]
    [InlineData("I'd like the thrive boardroom please")]
    [InlineData("Let's use Westin Brisbane Thrive Room.")]
    public void ExtractRoomFromUserMessages_ResolvesThriveAliases_ToThriveBoardroom(string userMessage)
    {
        var messages = new List<DisplayMessage>
        {
            new("user", DateTimeOffset.UtcNow, new[] { userMessage }),
        };

        var result = AgentToolHandlerService.ExtractRoomFromUserMessages(messages);
        Assert.Equal("Thrive Boardroom", result);
    }

    [Fact]
    public void ExtractAttendeesFromUserMessages_ExtractsFromPaxKeyword()
    {
        var messages = new List<DisplayMessage>
        {
            new("user", DateTimeOffset.UtcNow, new[] { "We have 200 pax for the event" }),
        };

        var result = AgentToolHandlerService.ExtractAttendeesFromUserMessages(messages);
        Assert.Equal(200, result);
    }

    #endregion

    // =========================================================================
    // Technician coverage extraction — regression tests for the repeated-prompt
    // loop where the user had to give the same stage answer multiple times.
    // =========================================================================

    #region TechnicianCoverage

    [Fact]
    public void ExtractTechnicianCoveragePreference_AllStages_WithoutTechnicianKeyword()
    {
        // "all stages" should be accepted as full coverage even when the user
        // does not repeat the word "technician" in their reply.
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("assistant", now.AddMinutes(-2), new[]
            {
                "Which stages would you like technician support for: setup, rehearsal/test & connect, operate during the event, and/or pack down?"
            }),
            new("user", now, new[] { "all stages" }),
        };

        var result = AgentToolHandlerService.ExtractTechnicianCoveragePreference(messages);

        Assert.True(result.HasPreference);
        Assert.False(result.NoTechnicianSupport);
        Assert.True(result.Setup);
        Assert.True(result.Rehearsal);
        Assert.True(result.Operate);
        Assert.True(result.Packdown);
    }

    [Fact]
    public void ExtractTechnicianCoveragePreference_FullCoverage_Phrase()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("assistant", now.AddMinutes(-2), new[]
            {
                "Which stages would you like technician support for?"
            }),
            new("user", now, new[] { "full coverage please" }),
        };

        var result = AgentToolHandlerService.ExtractTechnicianCoveragePreference(messages);

        Assert.True(result.HasPreference);
        Assert.True(result.Setup);
        Assert.True(result.Operate);
        Assert.True(result.Packdown);
    }

    [Fact]
    public void ExtractTechnicianCoveragePreference_AffirmativeReplyToBinaryOperatorQuestion_TreatedAsFullCoverage()
    {
        // Regression: when user answers "yes" to the binary operator question,
        // we should not ask a second stage-selection question.
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("assistant", now.AddMinutes(-2), new[]
            {
                "Would you like a technical operator to assist you during the entire event or only during setup and rehearsal?"
            }),
            new("user", now, new[] { "yes" }),
        };

        var result = AgentToolHandlerService.ExtractTechnicianCoveragePreference(messages);

        Assert.True(result.HasPreference);
        Assert.False(result.NoTechnicianSupport);
        Assert.True(result.Setup);
        Assert.True(result.Rehearsal);
        Assert.True(result.Operate);
        Assert.True(result.Packdown);
    }

    [Fact]
    public void ExtractTechnicianCoveragePreference_SpecificStages_NotFilteredAsSchedule()
    {
        // A reply mentioning specific stage names like "setup, rehearsal and pack up"
        // must NOT be dropped by the schedule-message filter.
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("assistant", now.AddMinutes(-2), new[]
            {
                "Which stages would you like technician support for?"
            }),
            new("user", now, new[] { "setup, rehearsal and pack up please" }),
        };

        var result = AgentToolHandlerService.ExtractTechnicianCoveragePreference(messages);

        Assert.True(result.HasPreference);
        Assert.True(result.Setup);
        Assert.True(result.Rehearsal);
        Assert.True(result.Packdown);
    }

    [Fact]
    public void ExtractTechnicianCoveragePreference_SchedulePickerMessage_IsFiltered()
    {
        // A real time-picker message containing stage names + time values must be
        // ignored so it doesn't accidentally register as a stage preference.
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("user", now, new[] { "Choose schedule: Setup 07:00AM Rehearsal 08:30AM Start 09:00AM End 05:00PM Pack Up 06:00PM" }),
        };

        var result = AgentToolHandlerService.ExtractTechnicianCoveragePreference(messages);

        Assert.False(result.HasPreference);
    }

    [Fact]
    public void TryLoadTechnicianCoverageFromSession_DeserializesStoredPreference()
    {
        var session = new InMemorySession();
        session.SetString("Draft:TechnicianCoverage", """
            {"NoTechnicianSupport":false,"Setup":true,"Rehearsal":true,"Operate":false,"Packdown":true}
            """);

        var result = AgentToolHandlerService.TryLoadTechnicianCoverageFromSession(session);

        Assert.NotNull(result);
        Assert.True(result!.HasPreference);
        Assert.False(result.NoTechnicianSupport);
        Assert.True(result.Setup);
        Assert.True(result.Rehearsal);
        Assert.False(result.Operate);
        Assert.True(result.Packdown);
    }

    [Fact]
    public void TryLoadTechnicianCoverageFromSession_ReturnsNull_WhenSessionEmpty()
    {
        var session = new InMemorySession();
        var result = AgentToolHandlerService.TryLoadTechnicianCoverageFromSession(session);
        Assert.Null(result);
    }

    [Fact]
    public void TryInferTechnicianCoverageFromDraftSession_TechWholeEventYes_IsFullCoverage()
    {
        var session = new InMemorySession();
        session.SetString("Draft:TechWholeEvent", "yes");

        var result = AgentToolHandlerService.TryInferTechnicianCoverageFromDraftSession(session);

        Assert.NotNull(result);
        Assert.True(result!.HasPreference);
        Assert.False(result.NoTechnicianSupport);
        Assert.True(result.Setup);
        Assert.True(result.Rehearsal);
        Assert.True(result.Operate);
        Assert.True(result.Packdown);
    }

    [Fact]
    public void TryInferTechnicianCoverageFromDraftSession_TechWindowIsSet_IsFullCoverage()
    {
        var session = new InMemorySession();
        session.SetString("Draft:TechStartTime", "10:00");
        session.SetString("Draft:TechEndTime", "16:00");

        var result = AgentToolHandlerService.TryInferTechnicianCoverageFromDraftSession(session);

        Assert.NotNull(result);
        Assert.True(result!.HasPreference);
        Assert.False(result.NoTechnicianSupport);
    }

    [Fact]
    public void ExtractTechnicianCoveragePreference_WillNeedForWholeDuration_TreatedAsFullCoverage()
    {
        // Regression: user says "will need for whole duration" to canonical operator question;
        // extraction must recognize this and not trigger a second stage-selection question.
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("assistant", now.AddMinutes(-2), new[]
            {
                "Would you like a technician ONLY for setup, rehearsal/test & connect and pack down, or would you also like a technical operator present for the WHOLE duration of the event?"
            }),
            new("user", now, new[] { "will need for whole duration" }),
        };

        var result = AgentToolHandlerService.ExtractTechnicianCoveragePreference(messages);

        Assert.True(result.HasPreference);
        Assert.False(result.NoTechnicianSupport);
        Assert.True(result.Setup);
        Assert.True(result.Rehearsal);
        Assert.True(result.Operate);
        Assert.True(result.Packdown);
    }

    #endregion

    #region VideoConferenceConfirmation

    [Fact]
    public void HasExplicitVideoConferenceConfirmation_ZoomAndTeamsMention_ReturnsTrue()
    {
        // User proactively mentions Zoom/Teams in requirements; should count as confirmation
        // so the agent does not re-ask about video conferencing package.
        var now = DateTimeOffset.UtcNow;
        var messages = new List<DisplayMessage>
        {
            new("user", now, new[] { "sharing presentations with attendees in the room and others globally via zoom and teams" }),
        };

        var result = AgentToolHandlerService.HasExplicitVideoConferenceConfirmation(messages);

        Assert.True(result);
    }

    #endregion

    #region AffirmativeReply

    [Fact]
    public void IsLikelyAffirmativeReply_ThatsGood_ReturnsTrue()
    {
        Assert.True(AgentToolHandlerService.IsLikelyAffirmativeReply("that's good"));
        Assert.True(AgentToolHandlerService.IsLikelyAffirmativeReply("that is great"));
        Assert.True(AgentToolHandlerService.IsLikelyAffirmativeReply("yes this good"));
    }

    #endregion

    #region FormToolsAndWorkflow

    [Fact]
    public async Task BuildTimePicker_MultitimeIncludesSubmitLabelAndSessionPrefill()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:SetupTime"] = "06:00",
            ["Draft:RehearsalTime"] = "08:00",
            ["Draft:StartTime"] = "11:30",
            ["Draft:EndTime"] = "15:45",
            ["Draft:PackupTime"] = "17:00"
        });

        var result = await service.HandleToolCallAsync(
            "build_time_picker",
            """{"date":"2030-06-15","stepMinutes":30}""",
            "thread-forms",
            CancellationToken.None);

        Assert.NotNull(result);
        using var outer = JsonDocument.Parse(result!);
        var outputToUser = outer.RootElement.GetProperty("outputToUser").GetString();
        Assert.NotNull(outputToUser);
        Assert.Contains("Please confirm your schedule", outputToUser, StringComparison.OrdinalIgnoreCase);

        var jsonStart = outputToUser!.IndexOf('{');
        Assert.True(jsonStart >= 0);
        using var uiDoc = JsonDocument.Parse(outputToUser.Substring(jsonStart));
        var ui = uiDoc.RootElement.GetProperty("ui");
        Assert.Equal("multitime", ui.GetProperty("type").GetString());
        Assert.Equal("Submit", ui.GetProperty("submitLabel").GetString());

        var pickers = ui.GetProperty("pickers").EnumerateArray().ToList();
        Assert.Equal(5, pickers.Count);
        Assert.Equal("06:00", pickers.Single(p => p.GetProperty("name").GetString() == "setup").GetProperty("default").GetString());
        Assert.Equal("11:30", pickers.Single(p => p.GetProperty("name").GetString() == "start").GetProperty("default").GetString());
        Assert.Equal("15:45", pickers.Single(p => p.GetProperty("name").GetString() == "end").GetProperty("default").GetString());
    }

    [Fact]
    public async Task BuildContactForm_ReturnsContactFormUiWithSubmitButtonLabel()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>());

        var result = await service.HandleToolCallAsync(
            "build_contact_form",
            "{}",
            "thread-forms",
            CancellationToken.None);

        Assert.NotNull(result);
        using var outer = JsonDocument.Parse(result!);
        var outputToUser = outer.RootElement.GetProperty("outputToUser").GetString();
        Assert.NotNull(outputToUser);
        Assert.Contains("contact form", outputToUser, StringComparison.OrdinalIgnoreCase);

        var jsonStart = outputToUser!.IndexOf('{');
        using var uiDoc = JsonDocument.Parse(outputToUser.Substring(jsonStart));
        var ui = uiDoc.RootElement.GetProperty("ui");
        Assert.Equal("contactForm", ui.GetProperty("type").GetString());
        Assert.Equal("Send details", ui.GetProperty("submitLabel").GetString());
    }

    [Fact]
    public async Task BuildContactForm_CustomSubmitLabel_IsHonored()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>());

        var result = await service.HandleToolCallAsync(
            "build_contact_form",
            """{"title":"Your details","submitLabel":"Confirm and continue"}""",
            "thread-forms",
            CancellationToken.None);

        Assert.NotNull(result);
        using var outer = JsonDocument.Parse(result!);
        var outputToUser = outer.RootElement.GetProperty("outputToUser").GetString();
        var jsonStart = outputToUser!.IndexOf('{');
        using var uiDoc = JsonDocument.Parse(outputToUser.Substring(jsonStart));
        var ui = uiDoc.RootElement.GetProperty("ui");
        Assert.Equal("Confirm and continue", ui.GetProperty("submitLabel").GetString());
    }

    [Fact]
    public async Task BuildEventForm_PrefillsFromSessionAndIncludesSubmitLabel()
    {
        var venueRooms = new[] { ("thrive-boardroom", "Thrive Boardroom"), ("elevate", "Elevate") };
        var service = CreateServiceHarness(
            new Dictionary<string, string>
            {
                ["Draft:EventType"] = "product launch",
                ["Draft:ExpectedAttendees"] = "120",
                ["Draft:SetupStyle"] = "Theatre",
                ["Draft:EventDate"] = "2030-08-20",
                ["Draft:RoomName"] = "Thrive Boardroom",
                ["Draft:StartTime"] = "09:15",
                ["Draft:EndTime"] = "16:30"
            },
            venueConfirmRooms: venueRooms).Service;

        var result = await service.HandleToolCallAsync(
            "build_event_form",
            """{"submitLabel":"Confirm event details"}""",
            "thread-forms",
            CancellationToken.None);

        Assert.NotNull(result);
        using var outer = JsonDocument.Parse(result!);
        var outputToUser = outer.RootElement.GetProperty("outputToUser").GetString();
        Assert.NotNull(outputToUser);

        var jsonStart = outputToUser!.IndexOf('{');
        using var uiDoc = JsonDocument.Parse(outputToUser.Substring(jsonStart));
        var ui = uiDoc.RootElement.GetProperty("ui");
        Assert.Equal("eventForm", ui.GetProperty("type").GetString());
        Assert.Equal("Confirm event details", ui.GetProperty("submitLabel").GetString());
        Assert.Equal("product launch", ui.GetProperty("eventType").GetString());
        Assert.Equal("120", ui.GetProperty("attendees").GetString());
        Assert.Equal("Theatre", ui.GetProperty("setupStyle").GetString());
        Assert.Equal("2030-08-20", ui.GetProperty("eventDate").GetString());
        Assert.Equal("thrive-boardroom", ui.GetProperty("selectedRoomSlug").GetString());
        Assert.Equal("09:15", ui.GetProperty("schedule").GetProperty("start").GetString());
        Assert.Equal("16:30", ui.GetProperty("schedule").GetProperty("end").GetString());
    }

    [Fact]
    public async Task RecommendEquipmentForEvent_BlocksWhenCustomerContactMissing()
    {
        var service = CreateServiceWithSession(new Dictionary<string, string>
        {
            ["Draft:EventType"] = "presentation",
            ["Draft:ExpectedAttendees"] = "40",
            ["Draft:SetupStyle"] = "theatre",
            ["Draft:StartTime"] = "10:00",
            ["Draft:EndTime"] = "16:00",
            ["Draft:EventDate"] = "2030-01-10",
            ["Draft:DateConfirmed"] = "1"
        });

        var result = await service.HandleToolCallAsync(
            "recommend_equipment_for_event",
            """{"equipment_requests":[{"equipment_type":"projector","quantity":1}]}""",
            "thread-forms",
            CancellationToken.None);

        Assert.NotNull(result);
        using var doc = JsonDocument.Parse(result!);
        Assert.True(doc.RootElement.TryGetProperty("error", out var err));
        Assert.Contains("Cannot show quote summary", err.GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer information", err.GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(doc.RootElement.TryGetProperty("missingFields", out var mf));
        var fields = mf.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("customer name", fields!);
        Assert.Contains("contact email or phone number", fields!);
        Assert.Contains("organisation name", fields!);
        var instruction = doc.RootElement.GetProperty("instruction").GetString();
        Assert.Contains("collect", instruction, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    private static AgentToolHandlerService CreateServiceWithSession(
        Dictionary<string, string> sessionValues,
        List<WestinRoom>? rooms = null)
        => CreateServiceHarness(sessionValues, rooms).Service;

    private static (AgentToolHandlerService Service, InMemorySession Session) CreateServiceHarness(
        Dictionary<string, string> sessionValues,
        List<WestinRoom>? rooms = null,
        IReadOnlyList<(string Slug, string Name)>? venueConfirmRooms = null)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new BookingDbContext(options);

        var session = new InMemorySession();
        foreach (var kvp in sessionValues)
            session.SetString(kvp.Key, kvp.Value);

        var httpContext = new DefaultHttpContext
        {
            Session = session,
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };

        var httpAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var env = new TestWebHostEnvironment();
        var roomCatalog = new StubWestinRoomCatalog(rooms, venueConfirmRooms);
        var extraction = new ConversationExtractionService(NullLogger<ConversationExtractionService>.Instance);

        var equipmentSearch = new EquipmentSearchService(db, NullLogger<EquipmentSearchService>.Instance);
        var smartEquipment = new SmartEquipmentRecommendationService(
            db,
            env,
            NullLogger<SmartEquipmentRecommendationService>.Instance,
            roomCatalog);

        var service = new AgentToolHandlerService(
            db,
            roomCatalog,
            drafts: null,
            http: httpAccessor,
            env: env,
            logger: NullLogger<AgentToolHandlerService>.Instance,
            equipmentSearch: equipmentSearch,
            smartEquipment: smartEquipment,
            extraction: extraction);

        return (service, session);
    }

    private sealed class StubWestinRoomCatalog : IWestinRoomCatalog
    {
        private readonly List<WestinRoom> _rooms;
        private readonly IReadOnlyList<(string Slug, string Name)> _venueConfirmRooms;

        public StubWestinRoomCatalog(
            List<WestinRoom>? rooms = null,
            IReadOnlyList<(string Slug, string Name)>? venueConfirmRooms = null)
        {
            _rooms = rooms ?? new List<WestinRoom>();
            _venueConfirmRooms = venueConfirmRooms ?? Array.Empty<(string, string)>();
        }

        public Task<List<WestinRoom>> GetRoomsAsync(CancellationToken ct = default) =>
            Task.FromResult(_rooms);

        public IReadOnlyList<(string Slug, string Name)> GetVenueConfirmRoomOptions() => _venueConfirmRooms;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MicrohireAgentChat.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    }

    private sealed class InMemorySession : ISession
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
