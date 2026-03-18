using Azure.AI.Agents.Persistent;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

/// <summary>
/// Handles all Azure Agent tool calls - extracted from AzureAgentChatService
/// </summary>
public sealed partial class AgentToolHandlerService
{
    private readonly BookingDbContext _db;
    private readonly IWestinRoomCatalog _roomCatalog;
    private readonly IBookingDraftStore? _drafts;
    private readonly IHttpContextAccessor _http;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AgentToolHandlerService> _logger;
    private readonly EquipmentSearchService _equipmentSearch;
    private readonly SmartEquipmentRecommendationService _smartEquipment;
    private readonly ConversationExtractionService _extraction;

    public AgentToolHandlerService(
        BookingDbContext db,
        IWestinRoomCatalog roomCatalog,
        IBookingDraftStore? drafts,
        IHttpContextAccessor http,
        IWebHostEnvironment env,
        ILogger<AgentToolHandlerService> logger,
        EquipmentSearchService equipmentSearch,
        SmartEquipmentRecommendationService smartEquipment,
        ConversationExtractionService extraction)
    {
        _db = db;
        _roomCatalog = roomCatalog;
        _drafts = drafts;
        _http = http;
        _env = env;
        _logger = logger;
        _equipmentSearch = equipmentSearch;
        _smartEquipment = smartEquipment;
        _extraction = extraction;
    }

    /// <summary>
    /// Process a single tool call and return the result.
    /// Returns null if the tool is not handled here (should be handled by main service).
    /// </summary>
    public async Task<string?> HandleToolCallAsync(
        string toolName,
        string argsJson,
        string threadId,
        CancellationToken ct)
    {
        try
        {
            return toolName switch
            {
                "check_date_availability" => await HandleCheckAvailabilityAsync(argsJson, threadId, ct),
                "get_now_aest" => HandleGetNowAest(),
                "list_westin_rooms" => await HandleListRoomsAsync(ct),
                "build_time_picker" => HandleBuildTimePicker(argsJson, threadId),
                "get_room_images" => await HandleGetRoomImagesAsync(argsJson, ct),
                "get_product_info" => await HandleGetProductInfoAsync(argsJson, ct),
                "get_product_images" => await HandleGetProductImagesAsync(argsJson, ct),
                "build_equipment_picker" => HandleBuildEquipmentPicker(argsJson),
                "search_equipment" => await HandleSearchEquipmentAsync(argsJson, ct),
                "get_equipment_recommendations" => await HandleGetEquipmentRecommendationsAsync(argsJson, ct),
                "recommend_equipment_for_event" => await HandleSmartEquipmentRecommendationAsync(argsJson, threadId, ct),
                "update_equipment" => await HandleUpdateEquipmentAsync(argsJson, ct),
                "get_package_details" => await HandleGetPackageDetailsAsync(argsJson, ct),
                "show_equipment_alternatives" => await HandleShowEquipmentAlternativesAsync(argsJson, ct),
                "get_product_knowledge" => await HandleGetProductKnowledgeAsync(argsJson, ct),
                "get_westin_venue_guide" => await HandleGetWestinVenueGuideAsync(ct),
                "get_capacity_table" => await HandleGetCapacityTableAsync(ct),
                "get_room_capacity" => await HandleGetRoomCapacityAsync(argsJson, ct),
                "save_contact" => HandleSaveContact(argsJson),
                "update_quote" or "send_internal_followup" => HandleInternalFollowup(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool call failed: {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private string HandleSaveContact(string argsJson)
    {
        var session = _http.HttpContext?.Session;
        if (session != null && !string.IsNullOrWhiteSpace(argsJson))
        {
            try
            {
                using var contactDoc = JsonDocument.Parse(argsJson);
                var root = contactDoc.RootElement;
                string? orgName = null;
                string? orgLocation = null;

                if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    var contactName = CleanSessionValue(nameProp.GetString());
                    if (!string.IsNullOrWhiteSpace(contactName))
                        session.SetString("Draft:ContactName", contactName);
                }

                if (root.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
                {
                    var contactEmail = CleanSessionValue(emailProp.GetString());
                    if (!string.IsNullOrWhiteSpace(contactEmail))
                        session.SetString("Draft:ContactEmail", contactEmail);
                }

                if (root.TryGetProperty("phone", out var phoneProp) && phoneProp.ValueKind == JsonValueKind.String)
                {
                    var contactPhone = CleanSessionValue(phoneProp.GetString());
                    if (!string.IsNullOrWhiteSpace(contactPhone))
                        session.SetString("Draft:ContactPhone", contactPhone);
                }

                if (root.TryGetProperty("organisation", out var orgProp) && orgProp.ValueKind == JsonValueKind.String)
                {
                    orgName = CleanSessionValue(orgProp.GetString());
                }

                if (root.TryGetProperty("location", out var locProp) && locProp.ValueKind == JsonValueKind.String)
                {
                    orgLocation = CleanSessionValue(locProp.GetString());
                }

                if (!string.IsNullOrWhiteSpace(orgName) &&
                    string.IsNullOrWhiteSpace(orgLocation) &&
                    TrySplitOrganisationAndLocation(orgName, out var parsedOrg, out var parsedLocation))
                {
                    orgName = parsedOrg;
                    orgLocation = parsedLocation;
                }

                if (!string.IsNullOrWhiteSpace(orgName))
                {
                    session.SetString("Draft:Organisation", orgName);
                }

                if (!string.IsNullOrWhiteSpace(orgLocation))
                {
                    session.SetString("Draft:OrganisationAddress", orgLocation);
                }

                if (root.TryGetProperty("position", out var posProp) && posProp.ValueKind == JsonValueKind.String)
                {
                    var position = CleanSessionValue(posProp.GetString());
                    if (!string.IsNullOrWhiteSpace(position))
                        session.SetString("Draft:Position", position);
                }

                _logger.LogInformation("Saved contact info to session: Name={Name}, Email={Email}, Phone={Phone}, Org={Org}, OrgAddress={OrgAddress}",
                    session.GetString("Draft:ContactName"),
                    session.GetString("Draft:ContactEmail"),
                    session.GetString("Draft:ContactPhone"),
                    session.GetString("Draft:Organisation"),
                    session.GetString("Draft:OrganisationAddress"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse save_contact args");
            }
        }

        return JsonSerializer.Serialize(new { ok = true, message = "Contact information saved successfully." });
    }

    private static string? CleanSessionValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool TrySplitOrganisationAndLocation(string combined, out string organisation, out string location)
    {
        organisation = string.Empty;
        location = string.Empty;

        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        var text = combined.Trim().TrimEnd('.', ';');

        var m = Regex.Match(text, @"^(?<org>.+?),\s*(?<loc>.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var orgCandidate = m.Groups["org"].Value.Trim();
            var locationCandidate = m.Groups["loc"].Value.Trim();
            if (orgCandidate.Length >= 3 && LooksLikeLocation(locationCandidate))
            {
                organisation = orgCandidate;
                location = locationCandidate;
                return true;
            }
        }

        m = Regex.Match(text, @"^(?<org>.+?)\s+(?:located|based|situated|headquartered)\s+(?:in|at)\s+(?<loc>.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var orgCandidate = m.Groups["org"].Value.Trim();
            var locationCandidate = m.Groups["loc"].Value.Trim();
            if (orgCandidate.Length >= 3 && LooksLikeLocation(locationCandidate))
            {
                organisation = orgCandidate;
                location = locationCandidate;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeLocation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Any(char.IsDigit))
        {
            return true;
        }

        var lower = value.Trim().ToLowerInvariant();
        string[] tokens =
        {
            "brisbane", "sydney", "melbourne", "perth", "adelaide", "hobart", "darwin", "canberra",
            "qld", "nsw", "vic", "wa", "tas", "act", "australia",
            "street", " st ", "road", " rd ", "avenue", " ave ", "drive", " dr ", "lane", " ln "
        };

        return tokens.Any(t => lower.Contains(t, StringComparison.Ordinal));
    }

    private string HandleInternalFollowup()
    {
        var bookingNo = _http.HttpContext?.Session?.GetString("Draft:BookingNo") ?? "unknown";
        _logger.LogWarning("[INTERNAL_FOLLOWUP] HIGH PRIORITY: Booking {BookingNo} requires follow-up", bookingNo);
        return JsonSerializer.Serialize(new { ok = true });
    }

    private async Task<string> HandleCheckAvailabilityAsync(string argsJson, string threadId, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
                map[prop.Name] = prop.Value.GetString() ?? "";
            else if (prop.Value.ValueKind == JsonValueKind.Number && prop.Name.Equals("venueId", StringComparison.OrdinalIgnoreCase))
                map["venueId"] = prop.Value.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // Merge from draft if available
        var draft = _drafts?.TryGet(threadId);
        if (draft != null)
        {
            if (!map.ContainsKey("startTime") && draft.Start is TimeSpan s)
                map["startTime"] = $"{(int)s.TotalHours:00}{s.Minutes:00}";
            if (!map.ContainsKey("endTime") && draft.End is TimeSpan e)
                map["endTime"] = $"{(int)e.TotalHours:00}{e.Minutes:00}";
            if (!map.ContainsKey("date") && draft.Date is DateOnly d)
                map["date"] = d.ToString("yyyy-MM-dd");
        }

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var args = JsonSerializer.Deserialize<CheckDateArgs>(JsonSerializer.Serialize(map), opts)
                   ?? throw new InvalidOperationException("check_date_availability: missing/invalid args");

        var result = await CheckAvailabilityInternalAsync(args, ct);
        return JsonSerializer.Serialize(result);
    }

    private string HandleGetNowAest()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        
        return JsonSerializer.Serialize(new
        {
            currentDateTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
            timezone = "AEST",
            dayOfWeek = now.DayOfWeek.ToString()
        });
    }

    private async Task<string> HandleListRoomsAsync(CancellationToken ct)
    {
        var allRooms = FilterQuotableWestinRooms(await _roomCatalog.GetRoomsAsync(ct));
        var rooms = allRooms
            .Select(r =>
            {
                var capacities = r.Layouts
                    .Where(l => l.Capacity > 0)
                    .ToDictionary(l => l.Type, l => l.Capacity, StringComparer.OrdinalIgnoreCase);
                return new
                {
                    id = r.Id,
                    name = r.Name,
                    slug = r.Slug,
                    level = r.Level,
                    cover = ToAbsoluteUrl(r.Cover),
                    capacities = capacities.Count > 0 ? capacities : null
                };
            })
            .ToList();

        var roomCards = allRooms
            .Select(r =>
            {
                var maxCap = r.Layouts.Count > 0 ? r.Layouts.Max(l => l.Capacity) : (int?)null;
                return new IslaBlocks.RoomCard(
                    r.Id,
                    r.Name,
                    r.Slug,
                    ToAbsoluteUrl(r.Cover),
                    r.Level,
                    maxCap);
            })
            .ToList();
        var baseUrl = GetBaseUrl();
        var galleryBlock = IslaBlocks.BuildRoomsGalleryBlock(roomCards, baseUrl, max: 12, headerRoomList: "The Westin Brisbane");
        var outputToUser = "Here are the rooms at The Westin Brisbane:\n\n" + galleryBlock;
        var payload = new
        {
            rooms,
            outputToUser,
            instruction = "OUTPUT the 'outputToUser' value EXACTLY AS-IS so room images appear. Do not paraphrase."
        };
        return JsonSerializer.Serialize(payload);
    }

    private string HandleBuildTimePicker(string argsJson, string threadId)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string title = doc.RootElement.TryGetProperty("title", out var t)
            ? (t.GetString() ?? "Select start and end time")
            : "Select start and end time";
        string? dateIso = doc.RootElement.TryGetProperty("date", out var d) ? d.GetString() : null;
        DateTimeOffset? normalizedDate = null;

        // If date not provided by AI, try to get it from draft or extract from conversation
        if (string.IsNullOrEmpty(dateIso))
        {
            // First try to get from draft
            var draft = _drafts?.TryGet(threadId);
            if (draft != null && draft.Date is DateOnly draftDate)
            {
                var dto = new DateTimeOffset(draftDate.ToDateTime(TimeOnly.MinValue));
                normalizedDate = dto;
                dateIso = dto.ToString("yyyy-MM-dd");
            }
            else
            {
                // Try to extract from conversation
                var httpSession = _http.HttpContext?.Session;
                if (httpSession != null)
                {
                    var chatService = _http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService;
                    if (chatService != null)
                    {
                        var (_, messages) = chatService.GetTranscript(threadId);
                        var (dateDto, _) = _extraction.ExtractEventDate(messages);
                        if (dateDto.HasValue)
                        {
                            normalizedDate = dateDto.Value;
                            dateIso = dateDto.Value.ToString("yyyy-MM-dd");
                        }
                    }
                }
            }
        }

        // Normalize provided date string (including explicit dates in argsJson) and apply smart date logic
        if (normalizedDate is null && !string.IsNullOrWhiteSpace(dateIso))
        {
            if (DateTimeOffset.TryParse(dateIso, out var parsed))
            {
                normalizedDate = parsed;
                dateIso = parsed.ToString("yyyy-MM-dd");
            }
        }

        // Apply smart date detection: roll forward until the date is in the future
        // This handles cases where AI agent provides wrong year or past dates
        if (normalizedDate.HasValue)
        {
            var now = DateTimeOffset.Now;
            var adjustedDate = normalizedDate.Value;

            _logger.LogInformation("TIME PICKER TOOL: Initial date from AI agent: {InitialDate}, Current time: {Now}",
                adjustedDate, now);

            // Roll forward if the date is in the past
            while (adjustedDate.Date < now.Date)
            {
                var beforeRoll = adjustedDate;
                adjustedDate = adjustedDate.AddYears(1);
                _logger.LogInformation("TIME PICKER TOOL: Rolled forward from {Before} to {After}", beforeRoll.Date, adjustedDate.Date);
            }

            normalizedDate = adjustedDate;
            dateIso = adjustedDate.ToString("yyyy-MM-dd");

            _logger.LogInformation("TIME PICKER TOOL: Final date after smart detection: {FinalDate}", adjustedDate);
        }
        var titleDateText = normalizedDate.HasValue
            ? normalizedDate.Value.ToString("d MMMM yyyy")
            : (string.IsNullOrEmpty(dateIso) ? "your event" : dateIso);
        title = $"Confirm your schedule for {titleDateText}";
        string? defStart = doc.RootElement.TryGetProperty("defaultStart", out var ds) ? ds.GetString() : "09:00";
        string? defEnd = doc.RootElement.TryGetProperty("defaultEnd", out var de) ? de.GetString() : "10:00";
        int stepMinutes = doc.RootElement.TryGetProperty("stepMinutes", out var sm) && sm.ValueKind == JsonValueKind.Number
                            ? sm.GetInt32() : 30;

        // Try to pre-fill start/end times from the conversation if not already stored
        var session = _http.HttpContext?.Session;
        if (session != null)
        {
            var currentStart = session.GetString("Draft:StartTime");
            var currentEnd = session.GetString("Draft:EndTime");

            if (string.IsNullOrWhiteSpace(currentStart) || string.IsNullOrWhiteSpace(currentEnd))
            {
                var chatService = _http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService;
                if (chatService != null)
                {
                    var (_, messages) = chatService.GetTranscript(threadId);
                    var (startTime, endTime, matchedTimeText) = chatService.ExtractEventTime(messages);

                    if (startTime.HasValue)
                    {
                        session.SetString("Draft:StartTime", startTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER TOOL: Prefilled Draft:StartTime from conversation match '{Matched}' => {Start}", matchedTimeText, startTime);
                    }
                    if (endTime.HasValue)
                    {
                        session.SetString("Draft:EndTime", endTime.Value.ToString(@"hh\:mm"));
                        _logger.LogInformation("TIME PICKER TOOL: Prefilled Draft:EndTime from conversation match '{Matched}' => {End}", matchedTimeText, endTime);
                    }
                }
            }
        }

        // Read schedule times from session, fallback to defaults if not found
        var setupTime = session?.GetString("Draft:SetupTime") ?? "07:00";
        var rehearsalTime = session?.GetString("Draft:RehearsalTime") ?? "09:30";
        var sessionStartTime = session?.GetString("Draft:StartTime") ?? "10:00";
        var sessionEndTime = session?.GetString("Draft:EndTime") ?? "16:00";
        var packupTime = session?.GetString("Draft:PackupTime") ?? "18:00";

        // Build the UI JSON that needs to be embedded in the response
        var uiPayload = new
        {
            ui = new
            {
                type = "multitime",
                title = title,
                date = dateIso,
                pickers = new[]
                {
                    new { name = "setup", label = "Room setup by (optional)", @default = setupTime },
                    new { name = "rehearsal", label = "Rehearsal time (optional)", @default = rehearsalTime },
                    new { name = "start", label = "Event start time", @default = sessionStartTime },
                    new { name = "end", label = "Event end time", @default = sessionEndTime },
                    new { name = "packup", label = "Pack up time from (optional)", @default = packupTime }
                },
                stepMinutes = stepMinutes,
                submitLabel = "Submit"
            }
        };
        
        var jsonToEmbed = JsonSerializer.Serialize(uiPayload);

        var payload = new
        {
            success = true,
            outputToUser = $"Here's a time picker for you. Please confirm your schedule for {titleDateText}:\n\n{jsonToEmbed}",
            instruction = "OUTPUT THE 'outputToUser' VALUE EXACTLY AS-IS in your response. This creates a time picker widget for the user. DO NOT say you've 'generated' a time picker - just output the text and the picker will appear."
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task<string> HandleGetRoomImagesAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string roomKey = "";
        if (doc.RootElement.TryGetProperty("room", out var rProp) && rProp.ValueKind == JsonValueKind.String)
            roomKey = rProp.GetString() ?? "";

        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var roomNorm = roomKey.Trim().ToLowerInvariant();
        var roomNormSlug = roomNorm.Replace(" ", "-");
        var room = rooms.FirstOrDefault(r =>
                r.Name.Equals(roomKey, StringComparison.OrdinalIgnoreCase) ||
                r.Slug.Equals(roomKey, StringComparison.OrdinalIgnoreCase))
            ?? rooms.FirstOrDefault(r => r.Slug.Equals(roomNormSlug, StringComparison.OrdinalIgnoreCase))
            ?? (roomNorm.Contains("thrive") ? rooms.FirstOrDefault(r => r.Slug == "thrive-boardroom") : null)
            ?? (roomNorm == "elevate 1" ? rooms.FirstOrDefault(r => r.Slug == "elevate-1") : null)
            ?? (roomNorm == "elevate 2" ? rooms.FirstOrDefault(r => r.Slug == "elevate-2") : null)
            ?? (roomNorm == "elevate" ? rooms.FirstOrDefault(r => r.Slug == "elevate") : null)
            ?? (roomNorm == "ballroom 1" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-1") : null)
            ?? (roomNorm == "ballroom 2" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-2") : null)
            ?? (roomNorm == "ballroom" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom") : null)
            ?? rooms.FirstOrDefault(r => r.Name.Contains(roomKey, StringComparison.OrdinalIgnoreCase) || r.Slug.Contains(roomNormSlug, StringComparison.OrdinalIgnoreCase));

        if (room is null)
        {
            return JsonSerializer.Serialize(new { error = "room not found" });
        }

        var coverUrl = ToAbsoluteUrl(room.Cover);

        if (room.Slug == "thrive-boardroom")
        {
            var coverOnly = IslaBlocks.BuildCoverOnlyBlock(room.Name, coverUrl, GetBaseUrl());
            var fixedOutput = $"Here is the Thrive Boardroom:\n\n{coverOnly}";
            return JsonSerializer.Serialize(new
            {
                room = room.Name,
                cover = coverUrl,
                fixedLayout = "boardroom",
                outputToUser = fixedOutput,
                instruction = "OUTPUT the 'outputToUser' value EXACTLY AS-IS. Do NOT ask the user for room setup style -- automatically use 'boardroom' for Thrive Boardroom."
            });
        }

        var layouts = room.Layouts.Select(l => new
        {
            type = l.Type,
            capacity = l.Capacity,
            image = ToAbsoluteUrl(l.Image)
        }).ToList();

        var roomImagesDto = new IslaBlocks.RoomImagesDto(
            room.Name,
            coverUrl,
            room.Layouts.Select(l => new IslaBlocks.LayoutDto(l.Type, l.Capacity, ToAbsoluteUrl(l.Image))).ToList());
        var baseUrl = GetBaseUrl();
        var galleryBlock = IslaBlocks.BuildLayoutsGalleryBlock(roomImagesDto, baseUrl, includeCover: true);
        var outputToUser = $"Here are the room and setup options for {room.Name}:\n\n" + galleryBlock;

        var payload = new
        {
            room = room.Name,
            cover = coverUrl,
            layouts,
            outputToUser,
            instruction = "OUTPUT the 'outputToUser' value EXACTLY AS-IS so room and setup images appear."
        };

        return JsonSerializer.Serialize(payload);
    }

    private Task<object> CheckAvailabilityInternalAsync(CheckDateArgs args, CancellationToken ct)
    {
        // Date validation against RP database removed per requirement. Tool records schedule only.
        var result = new
        {
            Available = true,
            Message = "Schedule recorded for your event",
            Conflicts = (List<object>)new List<object>()
        };
        return Task.FromResult<object>(result);
    }

    private async Task<string> HandleGetProductInfoAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        
        string? keyword = null;
        string? productCode = null;
        int take = 12;

        if (doc.RootElement.TryGetProperty("keyword", out var kwProp))
            keyword = kwProp.GetString();
        if (doc.RootElement.TryGetProperty("product_code", out var pc))
            productCode = pc.GetString();
        if (doc.RootElement.TryGetProperty("take", out var t) && t.ValueKind == JsonValueKind.Number)
            take = Math.Clamp(t.GetInt32(), 1, 50);

        // If looking up by specific product code, do direct lookup
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var product = await _db.TblInvmas.AsNoTracking()
                .Where(p => (p.product_code ?? "").Trim() == productCode.Trim())
                .Select(p => new
                {
                    product_code = p.product_code,
                    description = p.descriptionv6 ?? p.PrintedDesc,
                    printed_desc = p.PrintedDesc,
                    category = p.category,
                    group = p.groupFld,
                    picture = p.PictureFileName
                })
                .FirstOrDefaultAsync(ct);

            if (product != null)
            {
                return JsonSerializer.Serialize(new { 
                    products = new[] { product },
                    count = 1,
                    searchInfo = $"Found product: {product.description}"
                });
            }
            
            return JsonSerializer.Serialize(new { 
                products = Array.Empty<object>(), 
                count = 0,
                searchInfo = $"No product found with code '{productCode}'"
            });
        }

        // Use intelligent equipment search for keyword searches
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var result = await _equipmentSearch.SearchEquipmentAsync(keyword, take, ct);

            if (!string.IsNullOrEmpty(result.Error))
            {
                return JsonSerializer.Serialize(new { error = result.Error });
            }

            var products = result.Items.Select(i => new
            {
                product_code = i.ProductCode,
                description = i.Description,
                printed_desc = i.PrintedDescription,
                category = i.Category,
                group = i.Group,
                picture = i.PictureFileName,
                day_rate = i.DayRate,
                is_package = i.IsPackage,
                part_of_packages = i.PartOfPackages?.Select(p => new
                {
                    package_code = p.PackageCode,
                    package_description = p.PackageDescription,
                    package_rate = p.DayRate
                }).ToList()
            }).ToList();

            // Build gallery HTML for visual picker
            string? galleryHtml = null;
            if (result.Items.Count > 0)
            {
                var equipmentItems = result.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Description))
                    .Select(i => new IslaBlocks.EquipmentItem(
                        i.ProductCode,
                        $"{i.Description} - ${i.DayRate:F0}/day",
                        i.Category,
                        !string.IsNullOrWhiteSpace(i.PictureFileName) 
                            ? ToAbsoluteUrl($"/images/products/{i.PictureFileName}") 
                            : null
                    ))
                    .ToList();

                if (equipmentItems.Count > 0)
                {
                    galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(
                        equipmentItems, 
                        $"Select a {result.CategoryName}", 
                        max: take
                    );
                }
            }

            // If we have products with a gallery, tell the agent to output the gallery block
            if (!string.IsNullOrEmpty(galleryHtml))
            {
                return JsonSerializer.Serialize(new { 
                    products,
                    count = products.Count,
                    category = result.CategoryName,
                    searchInfo = $"Found {products.Count} {result.CategoryName.ToLower()} with pricing",
                    outputToUser = galleryHtml,
                    instruction = "OUTPUT THE 'outputToUser' VALUE EXACTLY AS-IS in your response. This creates a visual picker for the user with prices."
                });
            }

            return JsonSerializer.Serialize(new { 
                products, 
                count = products.Count,
                category = result.CategoryName,
                searchInfo = products.Count > 0 
                    ? $"Found {products.Count} items" 
                    : $"No items found for '{keyword}'"
            });
        }

        return JsonSerializer.Serialize(new { 
            products = Array.Empty<object>(), 
            count = 0,
            searchInfo = "No search criteria provided"
        });
    }

    private async Task<string> HandleGetProductImagesAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        
        string? productCode = null;
        if (doc.RootElement.TryGetProperty("product_code", out var pc))
            productCode = pc.GetString();

        if (string.IsNullOrWhiteSpace(productCode))
        {
            return JsonSerializer.Serialize(new { 
                ui = new { images = Array.Empty<object>() }, 
                error = "product_code is required" 
            });
        }

        var product = await _db.TblInvmas.AsNoTracking()
            .Where(x => x.product_code == productCode.Trim())
            .Select(x => new { 
                x.product_code, 
                x.descriptionv6, 
                x.PrintedDesc, 
                x.PictureFileName 
            })
            .FirstOrDefaultAsync(ct);

        if (product == null || string.IsNullOrWhiteSpace(product.PictureFileName))
        {
            return JsonSerializer.Serialize(new { ui = new { images = Array.Empty<object>() } });
        }

        var imageUrl = ToAbsoluteUrl($"/images/products/{product.PictureFileName}");
        var images = new[] { new { url = imageUrl, description = product.descriptionv6 } };

        return JsonSerializer.Serialize(new { ui = new { images } });
    }

    private string HandleBuildEquipmentPicker(string argsJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string title = doc.RootElement.TryGetProperty("title", out var t) 
            ? (t.GetString() ?? "Choose equipment") 
            : "Choose equipment";

        var equipmentItems = new List<IslaBlocks.EquipmentItem>();

        if (doc.RootElement.TryGetProperty("products", out var productsArray) && 
            productsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in productsArray.EnumerateArray())
            {
                var productCode = item.TryGetProperty("product_code", out var pc) ? pc.GetString() : null;
                var description = item.TryGetProperty("description", out var desc) ? desc.GetString() : 
                                  item.TryGetProperty("printed_desc", out var pd) ? pd.GetString() : null;
                var category = item.TryGetProperty("category", out var cat) ? cat.GetString() : null;
                var picture = item.TryGetProperty("picture", out var pic) ? pic.GetString() : null;

                if (!string.IsNullOrWhiteSpace(productCode) && !string.IsNullOrWhiteSpace(description))
                {
                    var imageUrl = !string.IsNullOrWhiteSpace(picture) 
                        ? ToAbsoluteUrl($"/images/products/{picture}") 
                        : null;
                    
                    equipmentItems.Add(new IslaBlocks.EquipmentItem(
                        productCode,
                        description,
                        category,
                        imageUrl
                    ));
                }
            }
        }

        if (equipmentItems.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "No valid products provided" });
        }

        // Build the gallery block
        var galleryBlock = IslaBlocks.BuildEquipmentGalleryBlock(equipmentItems, title, max: 12);

        return JsonSerializer.Serialize(new { 
            ui = new { 
                type = "equipment_gallery",
                galleryHtml = galleryBlock
            },
            message = $"Here are the available options for {title.ToLower()}. Please select one:"
        });
    }

    #region Intelligent Equipment Search Tools

    /// <summary>
    /// Search for equipment with intelligent category mapping and pricing
    /// </summary>
    private async Task<string> HandleSearchEquipmentAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        
        string keyword = "";
        int maxResults = 6;

        if (doc.RootElement.TryGetProperty("keyword", out var kwProp))
            keyword = kwProp.GetString() ?? "";
        if (doc.RootElement.TryGetProperty("max_results", out var mr) && mr.ValueKind == JsonValueKind.Number)
            maxResults = Math.Clamp(mr.GetInt32(), 1, 12);

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return JsonSerializer.Serialize(new { error = "keyword is required" });
        }

        var result = await _equipmentSearch.SearchEquipmentAsync(keyword, maxResults, ct);

        if (!string.IsNullOrEmpty(result.Error))
        {
            return JsonSerializer.Serialize(new { error = result.Error });
        }

        if (result.Items.Count == 0)
        {
            return JsonSerializer.Serialize(new { 
                found = false,
                message = $"No equipment found matching '{keyword}'. Try a different search term.",
                category = result.CategoryName
            });
        }

        // Build product list with pricing info
        var products = result.Items.Select(i => new
        {
            product_code = i.ProductCode,
            description = i.Description,
            category = i.Category,
            day_rate = i.DayRate,
            extra_day_rate = i.ExtraDayRate,
            is_package = i.IsPackage,
            stock_on_hand = i.StockOnHand,
            picture = i.PictureFileName,
            part_of_packages = i.PartOfPackages?.Select(p => new
            {
                package_code = p.PackageCode,
                package_description = p.PackageDescription,
                package_day_rate = p.DayRate
            }).ToList()
        }).ToList();

        // Build UI gallery for visual selection
        var equipmentItems = result.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Description))
            .Select(i => new IslaBlocks.EquipmentItem(
                i.ProductCode,
                $"{i.Description} - ${i.DayRate:F0}/day",
                i.Category,
                !string.IsNullOrWhiteSpace(i.PictureFileName) 
                    ? ToAbsoluteUrl($"/images/products/{i.PictureFileName}") 
                    : null
            ))
            .ToList();

        string? galleryHtml = null;
        if (equipmentItems.Count > 0)
        {
            galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(
                equipmentItems, 
                $"Select a {result.CategoryName}", 
                max: maxResults
            );
        }

        return JsonSerializer.Serialize(new
        {
            found = true,
            category = result.CategoryName,
            count = result.Items.Count,
            products,
            outputToUser = galleryHtml,
            instruction = "Present these options to the user. OUTPUT the 'outputToUser' HTML EXACTLY AS-IS for the visual picker to render. Let the user select their preferred option."
        });
    }

    /// <summary>
    /// Parse user requirements and get equipment recommendations
    /// </summary>
    private async Task<string> HandleGetEquipmentRecommendationsAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string requirementText = "";
        if (doc.RootElement.TryGetProperty("requirements", out var req))
            requirementText = req.GetString() ?? "";

        if (string.IsNullOrWhiteSpace(requirementText))
        {
            return JsonSerializer.Serialize(new { error = "requirements text is required" });
        }

        // Parse user requirements
        var parsedRequirements = _equipmentSearch.ParseUserRequirements(requirementText);

        if (parsedRequirements.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                found = false,
                message = "Could not identify specific equipment requirements. Please specify what equipment you need (e.g., '2 laptops, 1 projector, 2 wireless microphones').",
                parsed_items = Array.Empty<object>()
            });
        }

        // Get recommendations for each category
        var recommendations = await _equipmentSearch.GetRecommendationsAsync(parsedRequirements, ct);

        // Build response with recommendations
        var categoryRecommendations = recommendations.Categories.Select(c => new
        {
            category = c.CategoryName,
            requested_quantity = c.RequestedQuantity,
            original_request = c.OriginalRequest,
            not_found = c.NotFound,
            not_found_message = c.NotFoundMessage,
            top_recommendation = c.TopRecommendation == null ? null : new
            {
                product_code = c.TopRecommendation.ProductCode,
                description = c.TopRecommendation.Description,
                day_rate = c.TopRecommendation.DayRate,
                total_for_qty = c.TopRecommendation.DayRate * c.RequestedQuantity,
                is_package = c.TopRecommendation.IsPackage,
                picture = c.TopRecommendation.PictureFileName
            },
            alternative_options = c.AlternativeOptions.Select(a => new
            {
                product_code = a.ProductCode,
                description = a.Description,
                day_rate = a.DayRate,
                is_package = a.IsPackage
            }).ToList(),
            package_option = c.PackageOption == null ? null : new
            {
                package_code = c.PackageOption.PackageCode,
                package_description = c.PackageOption.PackageDescription,
                day_rate = c.PackageOption.DayRate,
                reason = c.PackageOption.ReasonToRecommend,
                components = c.PackageOption.Components.Select(comp => new
                {
                    product_code = comp.ProductCode,
                    description = comp.Description,
                    quantity = comp.Quantity
                }).ToList()
            }
        }).ToList();

        // Build summary message for the agent
        var summaryParts = new List<string>();
        foreach (var rec in recommendations.Categories.Where(c => !c.NotFound && c.TopRecommendation != null))
        {
            var item = rec.TopRecommendation!;
            summaryParts.Add($"- {rec.RequestedQuantity}x {item.Description} ({item.ProductCode}) at ${item.DayRate:F2}/day = ${item.DayRate * rec.RequestedQuantity:F2}");
            
            if (rec.PackageOption != null)
            {
                summaryParts.Add($"  💡 Package available: {rec.PackageOption.PackageDescription} at ${rec.PackageOption.DayRate:F2}/day includes accessories");
            }
        }

        var responseMessage = summaryParts.Count > 0
            ? $"Based on your requirements, here are my recommendations:\n\n{string.Join("\n", summaryParts)}\n\nEstimated daily total: ${recommendations.EstimatedDayTotal:F2}"
            : "I couldn't find matching equipment for your requirements. Please specify the equipment types you need.";

        return JsonSerializer.Serialize(new
        {
            found = summaryParts.Count > 0,
            parsed_requirements = parsedRequirements.Select(r => new
            {
                original = r.OriginalText,
                normalized = r.NormalizedType,
                quantity = r.Quantity
            }).ToList(),
            recommendations = categoryRecommendations,
            estimated_daily_total = recommendations.EstimatedDayTotal,
            summary_message = responseMessage,
            instruction = "Present these recommendations to the user. Ask them to confirm if they want these items or if they'd like to see alternatives. If a package option is available, mention it as a value-add option."
        });
    }

    /// <summary>
    /// Smart equipment recommendation - automatically selects best equipment based on event context
    /// NO technical questions asked - the AI figures out appropriate specs
    /// </summary>
    private async Task<string> HandleSmartEquipmentRecommendationAsync(string argsJson, string threadId, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        // VALIDATION: Check required contact fields before showing quote summary
        // This prevents the AI from offering to generate a quote before collecting customer info
        var session = _http.HttpContext?.Session;
        var contactName = session?.GetString("Draft:ContactName");
        var contactEmail = session?.GetString("Draft:ContactEmail");
        var contactPhone = session?.GetString("Draft:ContactPhone");
        var organisation = session?.GetString("Draft:Organisation");

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(contactName))
            missingFields.Add("customer name");
        if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
            missingFields.Add("contact email or phone number");
        if (string.IsNullOrWhiteSpace(organisation))
            missingFields.Add("organisation name");

        if (missingFields.Count > 0)
        {
            _logger.LogWarning("Equipment recommendation blocked - missing contact fields: {Fields}", string.Join(", ", missingFields));
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required customer information",
                missingFields = missingFields,
                instruction = $"Do NOT call recommend_equipment_for_event again in this response. Before recommending equipment, you MUST collect: {string.Join(", ", missingFields)}. Politely ask the user for this information (e.g. 'Before I can provide equipment recommendations and a quote, I just need a few quick details about you first.'). Collect the missing information first; then on the next user message you may call the tool."
            });
        }

        // VALIDATION: Schedule must be submitted via time picker before showing quote summary
        var chatService = _http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService;
        var conversationMessages = new List<DisplayMessage>();
        if (chatService != null && !string.IsNullOrEmpty(threadId))
        {
            var (_, messages) = chatService.GetTranscript(threadId);
            conversationMessages = messages.ToList();
            var scheduleTimes = _extraction.ExtractScheduleTimes(messages);
            bool hasSchedule = scheduleTimes.Count > 0 && (scheduleTimes.ContainsKey("show_start_time") || scheduleTimes.ContainsKey("show_end_time"));
            if (!hasSchedule)
            {
                var hasChooseSchedule = messages.Any(m => (m.Parts ?? Enumerable.Empty<string>()).Any(p => p?.StartsWith("Choose schedule:", StringComparison.OrdinalIgnoreCase) == true));
                if (!hasChooseSchedule)
                {
                    // Session fallback: time picker saves to session even when transcript has reformatted message
                    var sessionStart = session?.GetString("Draft:StartTime");
                    var sessionEnd = session?.GetString("Draft:EndTime");
                    var sessionEventDate = session?.GetString("Draft:EventDate");
                    var sessionDateConfirmed = session?.GetString("Draft:DateConfirmed");
                    if (!string.IsNullOrWhiteSpace(sessionStart) && (!string.IsNullOrWhiteSpace(sessionEventDate) || sessionDateConfirmed == "1"))
                        hasSchedule = true;
                    if (!hasSchedule && !string.IsNullOrWhiteSpace(sessionEnd) && (!string.IsNullOrWhiteSpace(sessionEventDate) || sessionDateConfirmed == "1"))
                        hasSchedule = true;
                }
                if (!hasSchedule)
                {
                    _logger.LogWarning("Equipment recommendation blocked - schedule not yet submitted for thread {ThreadId}", threadId);
                    return JsonSerializer.Serialize(new
                    {
                        error = "Cannot show quote summary - event schedule not yet submitted",
                        instruction = "Do NOT call recommend_equipment_for_event again in this response. You MUST collect the event schedule before recommending equipment. Call build_time_picker with the confirmed event date, output the returned content exactly so the time picker appears, and wait for the user to submit their schedule (setup, start, end, pack up times). Collect the schedule first; then on the next user message you may call recommend_equipment_for_event."
                    });
                }
            }
        }

        // ========== CONTEXT VALIDATION - Scan conversation for mentioned equipment ==========
        var conversationContextWarnings = ValidateEquipmentContextFromConversation(argsJson);
        
        // Parse event context
        var setupStyleFromArgs = doc.RootElement.TryGetProperty("setup_style", out var ss) ? ss.GetString() : null;
        var setupStyleFromSession = session?.GetString("Ack:SetupStyle") ?? session?.GetString("Draft:SetupStyle");
        var userStatedStyle = conversationMessages.Count > 0
            ? ExtractSetupStyleFromUserMessages(conversationMessages)
            : null;
        var setupStyle = !string.IsNullOrWhiteSpace(userStatedStyle) ? userStatedStyle : setupStyleFromSession;

        var eventType = doc.RootElement.TryGetProperty("event_type", out var et) ? et.GetString() : null;
        if (string.IsNullOrWhiteSpace(eventType))
            eventType = session?.GetString("Draft:EventType");
        if (string.IsNullOrWhiteSpace(eventType) && chatService != null && !string.IsNullOrWhiteSpace(threadId))
        {
            var (_, transcriptMessages) = chatService.GetTranscript(threadId);
            var (extractedEventType, _) = _extraction.ExtractEventType(transcriptMessages);
            if (!string.IsNullOrWhiteSpace(extractedEventType))
            {
                eventType = extractedEventType;
                if (session != null && string.IsNullOrWhiteSpace(session.GetString("Draft:EventType")))
                    session.SetString("Draft:EventType", extractedEventType);
            }
        }

        var attendeesFromArgs = doc.RootElement.TryGetProperty("expected_attendees", out var ea) && ea.ValueKind == JsonValueKind.Number ? ea.GetInt32() : 0;
        var attendeesFromSessionStr = session?.GetString("Draft:ExpectedAttendees") ?? session?.GetString("Ack:Attendees");
        int attendeesFromSessionParsed = 0;
        if (!string.IsNullOrWhiteSpace(attendeesFromSessionStr))
            int.TryParse(attendeesFromSessionStr, out attendeesFromSessionParsed);

        var userStatedAttendees = conversationMessages.Count > 0
            ? ExtractAttendeesFromUserMessages(conversationMessages)
            : 0;
        // #region agent log
        EmitAgentDebugLog(
            $"thread:{threadId}",
            "H9",
            "AgentToolHandlerService.cs:attendees-sources",
            "Attendee source snapshot before trust resolution",
            new
            {
                attendeesFromArgs,
                attendeesFromSessionStr,
                attendeesFromSessionParsed,
                userStatedAttendees
            });
        // #endregion

        int expectedAttendees;
        var expectedAttendeesSource = "none";
        if (userStatedAttendees > 0)
        {
            expectedAttendees = userStatedAttendees;
            expectedAttendeesSource = "user";
            if (attendeesFromArgs > 0 && attendeesFromArgs != userStatedAttendees)
            {
                _logger.LogWarning("Attendees mismatch for thread {ThreadId}. ToolArgs={ArgsAttendees}, UserStated={UserAttendees}. Using user-stated value.",
                    threadId, attendeesFromArgs, userStatedAttendees);
            }
            if (attendeesFromSessionParsed > 0 && attendeesFromSessionParsed != userStatedAttendees)
            {
                _logger.LogWarning("Attendees mismatch for thread {ThreadId}. Session={SessionAttendees}, UserStated={UserAttendees}. Using user-stated value.",
                    threadId, attendeesFromSessionParsed, userStatedAttendees);
            }
        }
        else if (attendeesFromSessionParsed > 0)
        {
            expectedAttendees = attendeesFromSessionParsed;
            expectedAttendeesSource = "session";
            if (attendeesFromArgs > 0 && attendeesFromArgs != attendeesFromSessionParsed)
            {
                _logger.LogWarning("Attendees mismatch for thread {ThreadId}. ToolArgs={ArgsAttendees}, Session={SessionAttendees}. Using session value.",
                    threadId, attendeesFromArgs, attendeesFromSessionParsed);
            }
        }
        else
        {
            expectedAttendees = 0;
            expectedAttendeesSource = "none";
            if (attendeesFromArgs > 0)
            {
                _logger.LogWarning("Attendees={Attendees} from tool args not backed by user/session for thread {ThreadId} - rejecting value.",
                    attendeesFromArgs, threadId);
            }
        }
        // #region agent log
        EmitAgentDebugLog(
            $"thread:{threadId}",
            "H9",
            "AgentToolHandlerService.cs:attendees-resolution",
            "Expected attendees after trust resolution",
            new
            {
                expectedAttendees,
                expectedAttendeesSource,
                attendeesFromArgs,
                attendeesFromSessionParsed,
                userStatedAttendees
            });
        // #endregion

        if (!string.IsNullOrWhiteSpace(userStatedStyle))
        {
            if (!string.IsNullOrWhiteSpace(setupStyleFromArgs) &&
                !string.Equals(setupStyleFromArgs, userStatedStyle, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Setup style mismatch for thread {ThreadId}. ToolArgs={ArgsSetup}, UserStated={UserSetup}. Using user-stated style.",
                    threadId, setupStyleFromArgs, userStatedStyle);
            }
            setupStyle = userStatedStyle;
        }
        else if (!string.IsNullOrWhiteSpace(setupStyleFromArgs) && string.IsNullOrWhiteSpace(setupStyleFromSession))
        {
            _logger.LogWarning("SetupStyle={Style} from tool args not found in user messages - rejecting fabricated value for thread {ThreadId}",
                setupStyleFromArgs, threadId);
            setupStyle = null;
        }

        var roomNameFromArgs = doc.RootElement.TryGetProperty("room_name", out var rn) ? rn.GetString() : null;
        var roomNameFromSession = session?.GetString("Draft:RoomName");
        var userStatedRoom = conversationMessages.Count > 0
            ? ExtractRoomFromUserMessages(conversationMessages)
            : null;
        var userConversationTextForRoomSignals = string.Join(" ",
            conversationMessages
                .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                .SelectMany(m => m.Parts ?? Enumerable.Empty<string>()));
        var userHasStandaloneThriveToken = Regex.IsMatch(userConversationTextForRoomSignals, @"\bthrive\b", RegexOptions.IgnoreCase);
        if (string.IsNullOrWhiteSpace(userStatedRoom) && userHasStandaloneThriveToken)
        {
            userStatedRoom = "Thrive Boardroom";
            // #region agent log
            EmitAgentDebugLog(
                $"thread:{threadId}",
                "H1",
                "AgentToolHandlerService.cs:room-alias",
                "Mapped standalone Thrive token to canonical room",
                new
                {
                    mappedRoom = userStatedRoom
                });
            // #endregion
        }

        if (!string.IsNullOrWhiteSpace(userStatedRoom) &&
            !string.IsNullOrWhiteSpace(roomNameFromArgs) &&
            !string.Equals(userStatedRoom, roomNameFromArgs, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Room mismatch for thread {ThreadId}. ToolArgs={ArgsRoom}, UserStated={UserRoom}. Using user-stated room.",
                threadId, roomNameFromArgs, userStatedRoom);
        }

        var resolvedRoomName = !string.IsNullOrWhiteSpace(userStatedRoom)
            ? userStatedRoom
            : (!string.IsNullOrWhiteSpace(roomNameFromArgs) ? roomNameFromArgs : roomNameFromSession);

        var eventContext = new EventContext
        {
            EventType = eventType ?? "",
            ExpectedAttendees = expectedAttendees,
            VenueName = doc.RootElement.TryGetProperty("venue_name", out var vn) ? vn.GetString() : null,
            RoomName = resolvedRoomName,
            DurationDays = doc.RootElement.TryGetProperty("duration_days", out var dd) && dd.ValueKind == JsonValueKind.Number ? dd.GetInt32() : 1,
            IsContentHeavy = doc.RootElement.TryGetProperty("is_content_heavy", out var ch) && ch.ValueKind == JsonValueKind.True,
            IsContentLight = doc.RootElement.TryGetProperty("is_content_light", out var cl) && cl.ValueKind == JsonValueKind.True,
            NeedsRecording = doc.RootElement.TryGetProperty("needs_recording", out var nr) && nr.ValueKind == JsonValueKind.True,
            NeedsStreaming = doc.RootElement.TryGetProperty("needs_streaming", out var nst) && nst.ValueKind == JsonValueKind.True,
            NeedsHeavyStreaming = doc.RootElement.TryGetProperty("needs_heavy_streaming", out var nhs) && nhs.ValueKind == JsonValueKind.True,
            NeedsLighting = doc.RootElement.TryGetProperty("needs_lighting", out var nl) && nl.ValueKind == JsonValueKind.True,
            NeedsAdvancedLighting = doc.RootElement.TryGetProperty("needs_advanced_lighting", out var nal) && nal.ValueKind == JsonValueKind.True
        };
        if (string.IsNullOrWhiteSpace(eventContext.VenueName))
            eventContext.VenueName = session?.GetString("Draft:VenueName");
        
        // For Westin Brisbane / Four Points Brisbane, do not trust room names from AI agent args alone.
        // Only accept: (1) user-stated room from conversation, or (2) session-stored room from a prior
        // confirmed user statement (session is written only after a successful user-stated room match).
        var venueNormForRoomTrust = (eventContext.VenueName ?? "").Trim().ToLowerInvariant();
        var isWestinOrFourPointsForRoomTrust = (venueNormForRoomTrust.Contains("westin") && venueNormForRoomTrust.Contains("brisbane")) ||
            (venueNormForRoomTrust.Contains("four points") && venueNormForRoomTrust.Contains("brisbane"));
        if (isWestinOrFourPointsForRoomTrust)
        {
            eventContext.RoomName = !string.IsNullOrWhiteSpace(userStatedRoom)
                ? userStatedRoom
                : roomNameFromSession;  // session value was written from a prior user confirmation
        }

        if (string.IsNullOrWhiteSpace(setupStyle)
            && !string.IsNullOrWhiteSpace(eventContext.RoomName)
            && eventContext.RoomName.Contains("Thrive", StringComparison.OrdinalIgnoreCase))
        {
            setupStyle = "boardroom";
        }

        // #region agent log
        EmitAgentDebugLog(
            $"thread:{threadId}",
            "H1",
            "AgentToolHandlerService.cs:room-trust",
            "Room trust and setup resolution snapshot",
            new
            {
                venue = eventContext.VenueName,
                roomNameFromArgs,
                roomNameFromSession,
                userStatedRoom,
                resolvedRoomName,
                trustedRoomAfterVenueRule = eventContext.RoomName,
                isWestinOrFourPointsForRoomTrust,
                userHasStandaloneThriveToken,
                setupStyleFromArgs,
                setupStyleFromSession,
                userStatedStyle,
                setupStyleAfterThriveFallback = setupStyle
            });
        // #endregion

        // Parse equipment requests
        if (doc.RootElement.TryGetProperty("equipment_requests", out var eqArray) && eqArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in eqArray.EnumerateArray())
            {
                var request = new EquipmentRequest
                {
                    EquipmentType = item.TryGetProperty("equipment_type", out var eqt) ? eqt.GetString() ?? "" : "",
                    Quantity = item.TryGetProperty("quantity", out var qty) && qty.ValueKind == JsonValueKind.Number ? qty.GetInt32() : 1,
                    Preference = item.TryGetProperty("preference", out var pref) ? pref.GetString() : null,
                    MicrophoneType = item.TryGetProperty("microphone_type", out var mt) ? mt.GetString() : null,
                    SpeakerStyle = item.TryGetProperty("speaker_style", out var speakerStyleProp) ? speakerStyleProp.GetString() : null
                };
                
                if (!string.IsNullOrWhiteSpace(request.EquipmentType))
                {
                    eventContext.EquipmentRequests.Add(request);
                }
            }
        }

        eventContext.SpeakerStylePreference = eventContext.EquipmentRequests
            .Where(r => (r.EquipmentType ?? string.Empty).Contains("speaker", StringComparison.OrdinalIgnoreCase) ||
                        (r.EquipmentType ?? string.Empty).Contains("audio", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.SpeakerStyle)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        var userConversationTextForEquipmentSignals = string.Join(" ",
            conversationMessages
                .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                .SelectMany(m => m.Parts ?? Enumerable.Empty<string>()));
        var mentionsZoomOrTeams = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(zoom|teams|video\s+conference|video\s+conferencing|virtual\s+call)\b", RegexOptions.IgnoreCase);
        var userExplicitCamera = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(camera|webcam|ptz)\b", RegexOptions.IgnoreCase);
        var userExplicitMicrophone = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(microphone|mic|lapel|handheld)\b", RegexOptions.IgnoreCase);
        var userExplicitSpeaker = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(speaker|speakers|audio playback|pa)\b", RegexOptions.IgnoreCase);
        var requestedEquipmentTypes = eventContext.EquipmentRequests
            .Select(r => (r.EquipmentType ?? "").Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .OrderBy(t => t)
            .ToList();
        // #region agent log
        EmitAgentDebugLog(
            $"thread:{threadId}",
            "H3",
            "AgentToolHandlerService.cs:equipment-requests",
            "Incoming equipment requests compared to user conversation signals",
            new
            {
                mentionsZoomOrTeams,
                userExplicitCamera,
                userExplicitMicrophone,
                userExplicitSpeaker,
                requestedEquipmentTypes
            });
        // #endregion

        var includesVideoConferenceUnit = requestedEquipmentTypes.Contains("video_conference_unit")
                                          || requestedEquipmentTypes.Contains("video conference unit");
        var hasExplicitVideoConferenceConfirmation = HasExplicitVideoConferenceConfirmation(conversationMessages);
        if (includesVideoConferenceUnit && !hasExplicitVideoConferenceConfirmation)
        {
            _logger.LogWarning("Equipment recommendation blocked - video conferencing package not explicitly confirmed for thread {ThreadId}", threadId);
            // #region agent log
            EmitAgentDebugLog(
                $"thread:{threadId}",
                "H4",
                "AgentToolHandlerService.cs:video-conference-confirmation",
                "Blocked inferred video conference package pending explicit confirmation",
                new
                {
                    mentionsZoomOrTeams,
                    includesVideoConferenceUnit,
                    hasExplicitVideoConferenceConfirmation,
                    requestedEquipmentTypes
                });
            // #endregion
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - video conferencing package not yet confirmed",
                missingFields = new[] { "explicit video conferencing package confirmation" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly one question: 'Would you like me to include a dedicated video conferencing package (camera, microphones, and speaker support) for your Zoom/Teams participants?' Wait for the user's explicit yes/no answer, then call recommend_equipment_for_event again."
            });
        }

        if (eventContext.EquipmentRequests.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                error = "No equipment requests provided",
                message = "Please specify what equipment is needed (e.g., laptops, projectors, screens, microphones)",
                instruction = "Build the full equipment_requests array from the ENTIRE conversation (all AV items discussed: e.g. speakers, projector, screen, clicker, 2x laptop with preference mac). Do NOT call recommend_equipment_for_event again in this response with an empty or partial list. Reply to the user once asking them to confirm what equipment they need, or list what you have noted and ask if anything is missing."
            });
        }

        // Require laptop ownership/preference before summary for presentation laptop workflows.
        var laptopRequests = eventContext.EquipmentRequests
            .Where(r => IsLaptopEquipmentType(r.EquipmentType))
            .ToList();
        var sessionLaptopPreference = NormalizeLaptopPreference(session?.GetString("Draft:LaptopPreference"));
        var sessionOwnershipAnswered = string.Equals(session?.GetString("Draft:LaptopOwnershipAnswered"), "1", StringComparison.Ordinal);
        var sessionNeedsProvidedLaptop = string.Equals(session?.GetString("Draft:NeedsProvidedLaptop"), "1", StringComparison.Ordinal);
        var laptopState = conversationMessages.Count > 0
            ? _extraction.ExtractLaptopAnswerState(conversationMessages)
            : new LaptopAnswerState();
        var effectivePreference = laptopState.PreferenceAnswered && !string.IsNullOrWhiteSpace(laptopState.Preference)
            ? NormalizeLaptopPreference(laptopState.Preference)
            : sessionLaptopPreference;
        var effectiveOwnershipAnswered = laptopState.OwnershipAnswered || sessionOwnershipAnswered;
        var effectiveNeedsProvidedLaptop = laptopState.OwnershipAnswered
            ? laptopState.NeedsProvidedLaptop
            : sessionNeedsProvidedLaptop;

        var hasLaptopDependentAccessories = eventContext.EquipmentRequests.Any(r => IsLaptopDependentAccessoryType(r.EquipmentType));
        var hasLaptopWorkflowSignals = ConversationIndicatesLaptopWorkflow(conversationMessages);
        var ownershipRequired = laptopRequests.Count > 0 || hasLaptopDependentAccessories || hasLaptopWorkflowSignals;
        if (ownershipRequired && !effectiveOwnershipAnswered)
        {
            _logger.LogWarning("Equipment recommendation blocked - laptop ownership missing for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing laptop ownership",
                missingFields = new[] { "laptop ownership (bring own or hire from us)" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly one question: 'Are you bringing your own laptop or do you need one provided by us?' Wait for the user's answer, then call recommend_equipment_for_event again."
            });
        }

        if (laptopRequests.Count > 0)
        {
            var hasMissingLaptopPreference = laptopRequests.Any(r => string.IsNullOrWhiteSpace(r.Preference));
            if (hasMissingLaptopPreference)
            {
                if (!string.IsNullOrWhiteSpace(effectivePreference))
                {
                    foreach (var req in laptopRequests.Where(r => string.IsNullOrWhiteSpace(r.Preference)))
                        req.Preference = effectivePreference;
                    _logger.LogInformation("Hydrated laptop preference from conversation/session state: {Preference}", effectivePreference);
                }
                else if (effectiveNeedsProvidedLaptop)
                {
                    _logger.LogWarning("Equipment recommendation blocked - laptop preference missing for thread {ThreadId}", threadId);
                    return JsonSerializer.Serialize(new
                    {
                        error = "Cannot show quote summary - missing laptop preference",
                        missingFields = new[] { "laptop preference (Windows or Mac)" },
                        instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly one question: 'Would you prefer a Windows or Mac laptop?' Wait for the user's answer, then call recommend_equipment_for_event again with equipment_type='laptop' and preference='windows' or preference='mac'."
                    });
                }
            }
        }

        // Westin Ballroom parent room is ambiguous until user confirms full vs split.
        var isWestinBrisbaneVenue = (eventContext.VenueName ?? "").Trim().Contains("westin", StringComparison.OrdinalIgnoreCase) &&
                                    (eventContext.VenueName ?? "").Trim().Contains("brisbane", StringComparison.OrdinalIgnoreCase);
        var userConfirmedFullWestinBallroom = UserExplicitlyConfirmedFullWestinBallroom(conversationMessages);
        var isFullWestinBallroomSelection =
            IsFullWestinBallroomRoom(eventContext.RoomName) ||
            (IsAmbiguousWestinBallroomParentRoom(eventContext.RoomName) && userConfirmedFullWestinBallroom);

        if (isWestinBrisbaneVenue &&
            IsAmbiguousWestinBallroomParentRoom(eventContext.RoomName) &&
            !userConfirmedFullWestinBallroom)
        {
            _logger.LogWarning("Equipment recommendation blocked - Westin Ballroom split not clarified for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - Westin Ballroom split not specified",
                missingFields = new[] { "Westin Ballroom selection (Full / Ballroom 1 / Ballroom 2)" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly: 'Is that the full Westin Ballroom, Westin Ballroom 1, or Westin Ballroom 2?' Wait for the user to confirm one option, then call recommend_equipment_for_event again."
            });
        }

        // Projector-area capture for Westin Ballroom family.
        var projectionNeeded = RequiresProjectorPlacementArea(eventContext.EquipmentRequests);
        var requestedProjectorCount = GetRequestedProjectorCount(eventContext.EquipmentRequests);
        var isWestinBallroomFamily = IsWestinBallroomFamilyRoom(eventContext.VenueName, eventContext.RoomName);
        // Full Westin Ballroom requires dual projector coverage (minimum two areas).
        // Other rooms follow requested projector quantity (default 1).
        var requiredAreaCount = projectionNeeded
            ? Math.Max(
                isFullWestinBallroomSelection ? 2 : 1,
                requestedProjectorCount > 1 ? Math.Min(requestedProjectorCount, 3) : 1)
            : 0;
        var projectorPromptShownForThread = WasProjectorPromptShownForThread(session, threadId);
        var projectorSelectionCapturedForThread = WasProjectorAreaCapturedForThread(session, threadId);

        List<string> projectorAreas = new();
        bool hasProjectorAreaPromptInConversation = conversationMessages.Any(m =>
            string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) &&
            (m.FullText?.Contains("/images/westin/westin-ballroom/floor-plan.png", StringComparison.OrdinalIgnoreCase) == true ||
             (m.Parts ?? Enumerable.Empty<string>()).Any(p =>
                 p.Contains("/images/westin/westin-ballroom/floor-plan.png", StringComparison.OrdinalIgnoreCase) ||
                 p.Contains("projector placement area", StringComparison.OrdinalIgnoreCase))));
        if (projectionNeeded && isWestinBallroomFamily)
        {
            // CRITICAL: Only trust session for Westin Ballroom projector areas.
            // Do NOT use tool args or extraction - the AI may pass "A" to bypass, or extraction may have false positives.
            // Session is only set when the user explicitly replied after seeing the floor plan (ChatController.TryCaptureProjectorAreaSelections).
            projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorAreas"));
            if (projectorAreas.Count == 0)
                projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorArea"));

            // Fallback: if session was not persisted (edge-case request boundary), recover projector
            // areas from the conversation. Safe because we only inspect the user message that directly
            // followed the floor plan prompt — no risk of false positives from unrelated text.
            if (projectorAreas.Count == 0 && hasProjectorAreaPromptInConversation)
            {
                var conversationAreas = ExtractProjectorAreasFromConversationAfterPrompt(conversationMessages);
                if (conversationAreas.Count > 0)
                {
                    projectorAreas = conversationAreas;
                    if (session != null)
                    {
                        session.SetString("Draft:ProjectorAreas", string.Join(",", projectorAreas));
                        session.SetString("Draft:ProjectorArea", projectorAreas[0]);
                        MarkProjectorAreaCaptured(session, threadId);
                        _logger.LogInformation("Recovered projector areas [{Areas}] from conversation for thread {ThreadId}",
                            string.Join(", ", projectorAreas), threadId);
                    }
                }
            }

            // Guard against stale session carry-over: if this thread has never shown the floor plan prompt,
            // force re-collection from the user instead of silently reusing old projector areas.
            if (!hasProjectorAreaPromptInConversation && !projectorPromptShownForThread && !projectorSelectionCapturedForThread)
                projectorAreas.Clear();
        }
        else if (projectionNeeded)
        {
            projectorAreas = GetNormalizedProjectorAreasFromArgs(doc.RootElement);
            if (projectorAreas.Count == 0)
                projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorAreas"));
            if (projectorAreas.Count == 0)
                projectorAreas = GetNormalizedProjectorAreas(session?.GetString("Draft:ProjectorArea"));
            if (projectorAreas.Count == 0)
                projectorAreas = _extraction.ExtractProjectorAreas(conversationMessages);
        }

        if (session != null && !isWestinBallroomFamily && HasExplicitVenueAndRoomInArgs(doc.RootElement))
        {
            // Only clear projector routing state when the AI explicitly passes BOTH a venue and a room
            // that are not in the Westin Ballroom family. A call with only venue_name (no room_name) is
            // ambiguous — the room may simply have been omitted — so we do not treat it as a context switch.
            ClearProjectorPlacementDraftState(session);
        }
        if (!isWestinBallroomFamily)
        {
            // Projector placement areas are only for Westin Ballroom floor plan routing.
            projectorAreas.Clear();
        }
        if (projectionNeeded && isWestinBallroomFamily)
        {
            var allowedAreas = GetAllowedProjectorAreas(eventContext.RoomName);
            var invalidAreas = projectorAreas.Where(a => !allowedAreas.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
            var validAreas = projectorAreas.Where(a => allowedAreas.Contains(a, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (invalidAreas.Count > 0 || validAreas.Count < requiredAreaCount)
            {
            if (session != null)
                MarkProjectorPromptShown(session, threadId);
            var areaImageUrl = "/images/westin/westin-ballroom/floor-plan.png";
            var allowedText = string.Join(", ", allowedAreas);
            var countText = requiredAreaCount == 1
                ? "please choose the projector placement area"
                : $"please choose {requiredAreaCount} projector placement areas";
            var outputToUser = string.Join("\n", new[]
            {
                $"Great — before I finalise the AV summary, {countText} for the Westin Ballroom layout.",
                "",
                $"For this room, valid areas are: **{allowedText}**.",
                requiredAreaCount == 1
                    ? "Please reply with one area."
                    : $"Please reply with any two areas (e.g. `{allowedAreas[0]} & {allowedAreas[1]}`).",
                "",
                $"![Westin Ballroom projector placement areas]({areaImageUrl})"
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing projector placement area",
                missingFields = new[] { requiredAreaCount == 1 ? "projector placement area" : "projector placement areas" },
                outputToUser,
                instruction = "Do NOT call recommend_equipment_for_event again in this response. OUTPUT the 'outputToUser' value EXACTLY AS-IS so the customer can select projector placement area(s). After the customer replies, call recommend_equipment_for_event again."
            });
        }
            projectorAreas = validAreas.Take(requiredAreaCount).ToList();
        }
        var projectorArea = projectorAreas.FirstOrDefault();
        eventContext.ProjectorAreas = projectorAreas;
        if (session != null && projectorAreas.Count > 0)
        {
            session.SetString("Draft:ProjectorAreas", string.Join(",", projectorAreas));
            session.SetString("Draft:ProjectorArea", projectorAreas[0]);
            MarkProjectorAreaCaptured(session, threadId);
        }
        if (string.IsNullOrWhiteSpace(eventContext.EventType))
        {
            _logger.LogWarning("Equipment recommendation blocked - event type missing for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required event type",
                missingFields = new[] { "event type" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Before recommending equipment, you MUST collect the event type (for example: conference, presentation, wedding, workshop). Ask one clear question to capture the event type first; then on the next user message you may call recommend_equipment_for_event."
            });
        }
        if (eventContext.ExpectedAttendees <= 0)
        {
            _logger.LogWarning("Equipment recommendation blocked - attendees missing for thread {ThreadId}", threadId);
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required attendee count",
                missingFields = new[] { "number of attendees" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Before recommending equipment, you MUST collect the number of attendees. Ask one clear question to capture attendee count first; then on the next user message you may call recommend_equipment_for_event."
            });
        }
        if (string.IsNullOrWhiteSpace(setupStyle))
        {
            _logger.LogWarning("Equipment recommendation blocked - setup style missing for thread {ThreadId}", threadId);
            // #region agent log
            EmitAgentDebugLog(
                $"thread:{threadId}",
                "H2",
                "AgentToolHandlerService.cs:missing-setup-style",
                "Recommendation blocked due to missing setup style",
                new
                {
                    venue = eventContext.VenueName,
                    room = eventContext.RoomName,
                    setupStyleFromArgs,
                    setupStyleFromSession,
                    userStatedStyle
                });
            // #endregion
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing required room setup style",
                missingFields = new[] { "room setup style" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Before showing the quote summary, you MUST collect the room setup style (for example: theatre, boardroom, classroom, banquet, u-shape). Ask one clear question to capture setup style first; then on the next user message you may call recommend_equipment_for_event."
            });
        }

        // Westin Brisbane and Four Points Brisbane require a specific room - do not infer from "board meeting" or "boardroom style"
        var venueNorm = (eventContext.VenueName ?? "").Trim().ToLowerInvariant();
        var isWestinOrFourPoints = (venueNorm.Contains("westin") && venueNorm.Contains("brisbane")) ||
            (venueNorm.Contains("four points") && venueNorm.Contains("brisbane"));
        if (isWestinOrFourPoints && string.IsNullOrWhiteSpace(eventContext.RoomName))
        {
            _logger.LogWarning("Equipment recommendation blocked - room not specified for Westin/Four Points Brisbane for thread {ThreadId}", threadId);
            // #region agent log
            EmitAgentDebugLog(
                $"thread:{threadId}",
                "H1",
                "AgentToolHandlerService.cs:missing-room",
                "Recommendation blocked because trusted room is empty",
                new
                {
                    venue = eventContext.VenueName,
                    roomNameFromArgs,
                    roomNameFromSession,
                    userStatedRoom,
                    resolvedRoomName,
                    trustedRoomAfterVenueRule = eventContext.RoomName
                });
            // #endregion
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - room not specified for this venue",
                missingFields = new[] { "room name" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. The user has not specified which room at this venue. Ask which room they would like (e.g. Westin Ballroom, Westin Ballroom 1, Westin Ballroom 2, Elevate, Elevate 1, Elevate 2, Thrive Boardroom). Do NOT infer a room from 'board meeting' or 'boardroom style' - those describe setup, not the room. Wait for the user to specify the room, then call recommend_equipment_for_event."
            });
        }

        _logger.LogInformation("Smart equipment recommendation for {EventType} with {Attendees} attendees, {Count} equipment types: [{Types}]",
            eventContext.EventType, eventContext.ExpectedAttendees, eventContext.EquipmentRequests.Count,
            string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}")));

        // Capacity check: if Westin + room known, validate attendees vs room capacity for the setup
        string? capacityWarning = null;
        string? capacityOkLine = null;
        if (!string.IsNullOrWhiteSpace(eventContext.VenueName) && !string.IsNullOrWhiteSpace(eventContext.RoomName) &&
            eventContext.VenueName.Trim().Contains("Westin", StringComparison.OrdinalIgnoreCase) && eventContext.VenueName.Trim().Contains("Brisbane", StringComparison.OrdinalIgnoreCase))
        {
            var (warning, okLine) = await TryGetCapacityCheckAsync(
                eventContext.VenueName, eventContext.RoomName, eventContext.ExpectedAttendees,
                setupStyle, ct);
            capacityWarning = warning;
            capacityOkLine = okLine;
        }

        // Get smart recommendations
        var recommendations = await _smartEquipment.GetRecommendationsAsync(eventContext, ct);

        // Technician preference guard:
        // before showing quote summary, confirm which stages the customer wants technician support for.
        var technicianCoverage = ExtractTechnicianCoveragePreference(conversationMessages);

        // If live transcript parsing found nothing, fall back to the value saved from a prior turn.
        if (!technicianCoverage.HasPreference && session != null)
            technicianCoverage = TryLoadTechnicianCoverageFromSession(session) ?? technicianCoverage;

        if (recommendations.LaborItems.Count > 0 && !technicianCoverage.HasPreference)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - missing technician support preference",
                missingFields = new[] { "technician support stages (setup, rehearsal/test & connect, operate, pack down)" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly one question: 'To tailor staffing, which stages would you like technician support for: setup, rehearsal/test & connect, operate during the event, and/or pack down? If you'd like full coverage, just say all stages.' Wait for the user's response, then call recommend_equipment_for_event again."
            });
        }

        if (technicianCoverage.NoTechnicianSupport)
        {
            recommendations.LaborItems.Clear();
        }
        else if (technicianCoverage.HasPreference)
        {
            recommendations.LaborItems = ApplyTechnicianCoveragePreference(recommendations.LaborItems, technicianCoverage);
        }
        if (session != null && technicianCoverage.HasPreference)
        {
            session.SetString("Draft:TechnicianCoverage", JsonSerializer.Serialize(new
            {
                technicianCoverage.NoTechnicianSupport,
                technicianCoverage.Setup,
                technicianCoverage.Rehearsal,
                technicianCoverage.Operate,
                technicianCoverage.Packdown
            }));
        }
        
        // IMPORTANT: If no recommendations returned but equipment was requested, log and handle gracefully
        if (recommendations.Items.Count == 0 && eventContext.EquipmentRequests.Count > 0)
        {
            _logger.LogWarning("No equipment recommendations returned despite {Count} requests: [{Types}]. This may indicate database/pricing issues.",
                eventContext.EquipmentRequests.Count,
                string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}")));
            
            // Return a message asking the AI to proceed with generate_quote, not retry recommend_equipment_for_event
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Equipment search returned no results",
                requested_equipment = eventContext.EquipmentRequests.Select(r => new { r.EquipmentType, r.Quantity }).ToList(),
                message = $"I couldn't find specific products for the requested equipment. Let me prepare a custom quote based on your requirements: {string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}"))}.",
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Tell the user that you will prepare a custom quote for their equipment needs and call generate_quote to create the booking with these requirements noted."
            });
        }

        // Log what we found
        _logger.LogInformation("Smart recommendations returned {Count} items:", recommendations.Items.Count);
        foreach (var item in recommendations.Items)
        {
            _logger.LogInformation("  - {Qty}x {Desc} (Code: {Code}) @ ${Price}/day - {Reason}", 
                item.Quantity, item.Description, item.ProductCode, item.UnitPrice, item.RecommendationReason);
        }

        // Build response with full component breakdown
        var recommendedItems = recommendations.Items.Select(item => new
        {
            product_code = item.ProductCode,
            description = item.Description,
            category = item.Category,
            quantity = item.Quantity,
            unit_price = item.UnitPrice,
            extra_day_rate = item.ExtraDayRate,
            weekly_rate = item.WeeklyRate,
            total_price = item.UnitPrice * item.Quantity,
            picture = item.PictureFileName,
            recommendation_reason = item.RecommendationReason,
            is_package = item.IsPackage,
            components = item.Components.Select(c => new
            {
                product_code = c.ProductCode,
                description = c.Description,
                component_type = c.ComponentType.ToString().ToLower(),
                quantity = c.Quantity,
                is_selectable = c.IsSelectable,
                individual_rate = c.IndividualRate
            }).ToList()
        }).ToList();

        // Build COMPLETE booking summary with event details AND equipment
        // This summary triggers the quote confirmation buttons in the UI
        var summaryLines = new List<string>();
        
        // Retrieve date/time from session for display
        var eventDateStr = session?.GetString("Draft:EventDate");
        var startTimeStr = session?.GetString("Draft:StartTime");
        var endTimeStr = session?.GetString("Draft:EndTime");
        var setupTimeStr = session?.GetString("Draft:SetupTime");
        var rehearsalTimeStr = session?.GetString("Draft:RehearsalTime");
        var packupTimeStr = session?.GetString("Draft:PackupTime");
        var technicianSchedule = new TechnicianScheduleInfo(setupTimeStr, rehearsalTimeStr, startTimeStr, endTimeStr, packupTimeStr);
        
        // Fallback: extract from conversation transcript if not in session
        // Note: chatService is already declared earlier in the method for schedule validation
        if ((string.IsNullOrWhiteSpace(eventDateStr) || string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr)) && !string.IsNullOrEmpty(threadId))
        {
            try
            {
                // Reuse chatService from earlier in the method if available, otherwise get it
                var chatServiceForExtraction = chatService ?? (_http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService);
                if (chatServiceForExtraction != null)
                {
                    var (_, messages) = chatServiceForExtraction.GetTranscript(threadId);
                    
                    // Extract date if not in session
                    if (string.IsNullOrWhiteSpace(eventDateStr))
                    {
                        var (dateDto, _) = _extraction.ExtractEventDate(messages);
                        if (dateDto.HasValue)
                        {
                            eventDateStr = dateDto.Value.ToString("yyyy-MM-dd");
                            _logger.LogInformation("[QUOTE_SUMMARY] Extracted date from conversation: {Date}", eventDateStr);
                        }
                    }
                    
                    // Extract times if not in session
                    if (string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr))
                    {
                        var scheduleTimes = _extraction.ExtractScheduleTimes(messages);
                        
                        if (string.IsNullOrWhiteSpace(startTimeStr) && scheduleTimes.ContainsKey("show_start_time"))
                        {
                            var extractedStart = scheduleTimes["show_start_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedStart.Length == 4 && int.TryParse(extractedStart, out var startMinutes))
                            {
                                var hours = startMinutes / 100;
                                var minutes = startMinutes % 100;
                                startTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[QUOTE_SUMMARY] Extracted start time from conversation: {Time}", startTimeStr);
                            }
                            else
                            {
                                startTimeStr = extractedStart;
                            }
                        }
                        
                        if (string.IsNullOrWhiteSpace(endTimeStr) && scheduleTimes.ContainsKey("show_end_time"))
                        {
                            var extractedEnd = scheduleTimes["show_end_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedEnd.Length == 4 && int.TryParse(extractedEnd, out var endMinutes))
                            {
                                var hours = endMinutes / 100;
                                var minutes = endMinutes % 100;
                                endTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[QUOTE_SUMMARY] Extracted end time from conversation: {Time}", endTimeStr);
                            }
                            else
                            {
                                endTimeStr = extractedEnd;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - use session values only if extraction fails
                _logger.LogWarning(ex, "[QUOTE_SUMMARY] Failed to extract date/time from conversation, using session values only");
            }
        }
        
        // Log if date/time are still missing
        if (string.IsNullOrWhiteSpace(eventDateStr))
        {
            _logger.LogWarning("[QUOTE_SUMMARY] Event date is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(startTimeStr))
        {
            _logger.LogWarning("[QUOTE_SUMMARY] Start time is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(endTimeStr))
        {
            _logger.LogWarning("[QUOTE_SUMMARY] End time is missing from both session and conversation");
        }
        
        // Header - complete quote summary
        summaryLines.Add("## 📋 Quote Summary\n");
        
        // Event Details Section
        summaryLines.Add("### Event Details");
        if (!string.IsNullOrWhiteSpace(eventContext.VenueName))
            summaryLines.Add($"**Venue:** {eventContext.VenueName}");
        if (!string.IsNullOrWhiteSpace(eventContext.RoomName))
            summaryLines.Add($"**Room:** {eventContext.RoomName}");
        if (projectorAreas.Count == 1)
            summaryLines.Add($"**Projector Placement Area:** {projectorAreas[0]}");
        else if (projectorAreas.Count > 1)
            summaryLines.Add($"**Projector Placement Areas:** {string.Join(", ", projectorAreas)}");
        summaryLines.Add($"**Event Type:** {(string.IsNullOrWhiteSpace(eventContext.EventType) ? "TBD" : eventContext.EventType)}");
        summaryLines.Add($"**Attendees:** {(eventContext.ExpectedAttendees <= 0 ? "TBD" : eventContext.ExpectedAttendees.ToString())}");
        
        // Add event date if available
        if (!string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out var eventDate))
        {
            summaryLines.Add($"**Date:** {eventDate:dddd d MMMM yyyy}");
        }
        
        // Add start and end times if available
        if (!string.IsNullOrWhiteSpace(startTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(startTimeStr, out var startTime))
            {
                summaryLines.Add($"**Start Time:** {startTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**Start Time:** {startTimeStr}");
            }
        }
        if (!string.IsNullOrWhiteSpace(endTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(endTimeStr, out var endTime))
            {
                summaryLines.Add($"**End Time:** {endTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**End Time:** {endTimeStr}");
            }
        }
        
        if (!string.IsNullOrEmpty(capacityOkLine) && eventContext.ExpectedAttendees > 0)
            summaryLines.Add(capacityOkLine);
        if (eventContext.DurationDays > 1)
            summaryLines.Add($"**Duration:** {eventContext.DurationDays} days");
        summaryLines.Add("");
        
        // Equipment Section
        summaryLines.Add("### Requirement Summary\n");
        
        // Ensure equipment is displayed - add validation
        if (eventContext.EquipmentRequests.Count == 0)
        {
            summaryLines.Add("*No equipment requirements collected yet. Please confirm required equipment first.*");
            _logger.LogWarning("[QUOTE_SUMMARY] No equipment requests available for summary rendering");
        }
        else
        {
            _logger.LogInformation("[QUOTE_SUMMARY] Displaying {Count} requirement-level equipment lines", eventContext.EquipmentRequests.Count);
            foreach (var request in BuildRequirementSummaryLines(eventContext.EquipmentRequests))
            {
                summaryLines.Add($"- {request}");
            }
            summaryLines.Add("");
        }
        
        // Technician Section
        if (recommendations.LaborItems.Count > 0)
        {
            summaryLines.Add("### Technician Support\n");
            foreach (var labor in recommendations.LaborItems)
            {
                summaryLines.Add($"- **{FormatLaborSummaryLine(labor, technicianSchedule)}**");
            }
            summaryLines.Add("");
        }
        
        // Total - Removed price display as per user request
        summaryLines.Add("");
        
        // This phrase triggers the quote confirmation buttons (Yes/No)
        summaryLines.Add("Would you like me to create the quote now?");

        var summaryMessage = string.Join("\n", summaryLines);
        // Prepend capacity warning if present so user and AI always see it
        var fullOutputToUser = !string.IsNullOrEmpty(capacityWarning)
            ? $"**⚠️ Room capacity:** {capacityWarning}\n\n{summaryMessage}"
            : summaryMessage;
        
        _logger.LogInformation("[QUOTE_SUMMARY] outputToUser length: {Length} chars (no alternatives section)", fullOutputToUser.Length);

        // Build response - includes phrase to trigger quote confirmation buttons
        // Include any context validation warnings
        var contextWarnings = conversationContextWarnings.Count > 0 
            ? $"\n\n**Note:** {string.Join(". ", conversationContextWarnings)}"
            : "";
        
        var hasCapacityWarning = !string.IsNullOrEmpty(capacityWarning);
        var instruction = hasCapacityWarning
            ? "CRITICAL: The room capacity is exceeded. Tell the user the room cannot fit their attendee count, state the room's capacity and suggest larger rooms or reducing attendees. Do NOT create the quote until they adjust. Then OUTPUT the 'outputToUser' EXACTLY AS-IS."
            : (conversationContextWarnings.Count > 0
                ? $"BEFORE outputting the summary, address these potential missing items: {string.Join(", ", conversationContextWarnings)}. Ask the user if they need these items. Then OUTPUT the 'outputToUser' EXACTLY AS-IS."
                : "MANDATORY: OUTPUT the 'outputToUser' EXACTLY AS-IS. This is the quote summary only. Do not add alternative pickers here; show alternatives only when the user explicitly asks (e.g. 'show me other microphones') using show_equipment_alternatives.");

        var response = JsonSerializer.Serialize(new
        {
            success = true,
            event_context = new
            {
                event_type = eventContext.EventType,
                attendees = eventContext.ExpectedAttendees,
                venue = eventContext.VenueName,
                room = eventContext.RoomName,
                projector_area = projectorArea,
                projector_areas = projectorAreas,
                duration_days = eventContext.DurationDays
            },
            recommendations = recommendedItems,
            total_day_rate = recommendations.TotalDayRate,
            alternatives_available = false,
            outputToUser = fullOutputToUser,
            capacity_warning = hasCapacityWarning ? capacityWarning : null,
            context_warnings = conversationContextWarnings.Count > 0 ? conversationContextWarnings : null,
            instruction = instruction
        });
        
        _logger.LogInformation("Full tool response JSON:\n{Response}", response);
        
            // Store equipment in session for later use when creating booking/quote
            try
            {
                // Note: session variable already declared at the start of this method for validation
                if (session != null)
                {
                    // Store as selected_equipment in format matching SelectedEquipmentItem class
                    var selectedEquipment = recommendations.Items.Select(item => new
                    {
                        ProductCode = item.ProductCode,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        IsPackage = item.IsPackage,
                        ParentPackageCode = (string?)null,
                        Comment = item.Comment
                    }).ToList();
                    
                    session.SetString("Draft:SelectedEquipment", JsonSerializer.Serialize(selectedEquipment));
                    
                    // Store as selected_labor in format matching SelectedLaborItem class
                    var selectedLabor = recommendations.LaborItems.Select(item => new SelectedLaborItem
                    {
                        ProductCode = string.IsNullOrWhiteSpace(item.ProductCode) ? "AVTECH" : item.ProductCode,
                        Description = item.Description,
                        Task = item.Task,
                        Quantity = item.Quantity,
                        Hours = item.Hours,
                        Minutes = item.Minutes
                    }).ToList();
                    session.SetString("Draft:SelectedLabor", JsonSerializer.Serialize(selectedLabor));

                    session.SetString("Draft:TotalDayRate", recommendations.TotalDayRate.ToString("F2"));
                    session.SetString("Draft:IsContentHeavy", eventContext.IsContentHeavy ? "1" : "0");
                    session.SetString("Draft:IsContentLight", eventContext.IsContentLight ? "1" : "0");
                    if (!string.IsNullOrWhiteSpace(eventContext.VenueName)) session.SetString("Draft:VenueName", eventContext.VenueName);
                    if (!string.IsNullOrWhiteSpace(eventContext.RoomName)) session.SetString("Draft:RoomName", eventContext.RoomName);
                    if (!string.IsNullOrWhiteSpace(eventContext.VenueName) || !string.IsNullOrWhiteSpace(eventContext.RoomName))
                        _logger.LogInformation("Session venue/room persisted for quote: Venue={Venue}, Room={Room}", eventContext.VenueName ?? "(null)", eventContext.RoomName ?? "(null)");
                    if (!string.IsNullOrWhiteSpace(eventContext.EventType)) session.SetString("Draft:EventType", eventContext.EventType);
                    session.SetString("Draft:ExpectedAttendees", eventContext.ExpectedAttendees.ToString());
                    if (!string.IsNullOrWhiteSpace(setupStyle)) session.SetString("Draft:SetupStyle", setupStyle);
                    if (projectorAreas.Count > 0)
                    {
                        session.SetString("Draft:ProjectorAreas", string.Join(",", projectorAreas));
                        session.SetString("Draft:ProjectorArea", projectorAreas[0]);
                    }
                    session.SetString("Draft:SummaryEquipmentRequests", JsonSerializer.Serialize(eventContext.EquipmentRequests));
                    
                    _logger.LogInformation("Stored {Count} equipment items in session for booking (Venue={Venue}, Room={Room})",
                        recommendations.Items.Count, eventContext.VenueName ?? "(null)", eventContext.RoomName ?? "(null)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store equipment in session");
            }
            
            return response;
        }
        catch (Exception ex)
        {
            var hasEquipmentRequests = !string.IsNullOrWhiteSpace(argsJson) && argsJson.IndexOf("equipment_requests", StringComparison.OrdinalIgnoreCase) >= 0;
            _logger.LogError(ex, "[QUOTE_SUMMARY] Exception in HandleSmartEquipmentRecommendationAsync: {Error}. Args length={Len}, hasEquipmentRequests={HasEq}", ex.Message, argsJson?.Length ?? 0, hasEquipmentRequests);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Failed to generate equipment recommendations",
                message = "I wasn't able to load the equipment summary just now. Could you please try sending your message again?",
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Reply to the user once with this exact message: 'I wasn't able to load the equipment summary just now. Could you please try sending your message again?' Then stop. Do not mention technical issues or apologise further."
            });
        }
    }

    /// <summary>
    /// Apply equipment edits (remove types, add requests) to current Draft:SelectedEquipment and return updated quote summary.
    /// </summary>
    private async Task<string> HandleUpdateEquipmentAsync(string argsJson, CancellationToken ct)
    {
        var session = _http.HttpContext?.Session;
        if (session == null)
            return JsonSerializer.Serialize(new { error = "Session unavailable.", instruction = "Do NOT call update_equipment or recommend_equipment_for_event again in this response. Ask the user to refresh and try again." });

        _logger.LogInformation("[update_equipment] Invoked with args (length={Len})", argsJson?.Length ?? 0);

        var selectedEquipmentJson = session.GetString("Draft:SelectedEquipment");
        if (string.IsNullOrWhiteSpace(selectedEquipmentJson))
        {
            _logger.LogWarning("[update_equipment] No Draft:SelectedEquipment in session");
            return JsonSerializer.Serialize(new
            {
                error = "No quote summary to update.",
                instruction = "Do NOT call update_equipment or recommend_equipment_for_event again in this response. Please show a quote summary first (e.g. by confirming equipment with recommend_equipment_for_event), then I can apply your changes. Ask the user to confirm their equipment needs so we can show a summary, then they can request edits."
            });
        }

        List<SelectedEquipmentItem> currentItems;
        try
        {
            currentItems = JsonSerializer.Deserialize<List<SelectedEquipmentItem>>(selectedEquipmentJson) ?? new List<SelectedEquipmentItem>();
        }
        catch
        {
            currentItems = new List<SelectedEquipmentItem>();
        }

        _logger.LogInformation("[update_equipment] Current session has {Count} items", currentItems.Count);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var root = doc.RootElement;

        // Parse remove_types
        var removeTypes = new List<string>();
        if (root.TryGetProperty("remove_types", out var rt) && rt.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in rt.EnumerateArray())
                if (r.ValueKind == JsonValueKind.String)
                    removeTypes.Add((r.GetString() ?? "").Trim().ToLowerInvariant());
        }
        removeTypes.RemoveAll(string.IsNullOrWhiteSpace);
        if (removeTypes.Count > 0)
            _logger.LogInformation("[update_equipment] remove_types: [{Types}]", string.Join(", ", removeTypes));

        // Apply removals: drop items whose description contains any remove_type
        if (removeTypes.Count > 0)
        {
            currentItems = currentItems.Where(item =>
            {
                var desc = (item.Description ?? "").ToLowerInvariant();
                var match = removeTypes.Any(t => desc.Contains(t));
                if (match) _logger.LogInformation("[update_equipment] Removing item: {Description} (matched remove_types)", item.Description);
                return !match;
            }).ToList();
            _logger.LogInformation("[update_equipment] After removals: {Count} items", currentItems.Count);
        }

        // Event context: args first, then session fallback so add_requests can resolve (e.g. room-specific packages)
        var venueName = root.TryGetProperty("venue_name", out var vnArg) && vnArg.ValueKind == JsonValueKind.String ? vnArg.GetString() : null;
        var roomName = root.TryGetProperty("room_name", out var rnArg) && rnArg.ValueKind == JsonValueKind.String ? rnArg.GetString() : null;
        var eventType = root.TryGetProperty("event_type", out var etArg) && etArg.ValueKind == JsonValueKind.String ? etArg.GetString() : null;
        var expectedAttendees = root.TryGetProperty("expected_attendees", out var eaArg) && eaArg.ValueKind == JsonValueKind.Number ? eaArg.GetInt32() : (int?)null;
        var setupStyle = root.TryGetProperty("setup_style", out var ssArg) && ssArg.ValueKind == JsonValueKind.String ? ssArg.GetString() : null;
        if (string.IsNullOrWhiteSpace(venueName)) venueName = session.GetString("Draft:VenueName");
        if (string.IsNullOrWhiteSpace(roomName)) roomName = session.GetString("Draft:RoomName");
        if (string.IsNullOrWhiteSpace(eventType)) eventType = session.GetString("Draft:EventType");
        if (!expectedAttendees.HasValue && int.TryParse(session.GetString("Draft:ExpectedAttendees"), out var eaSession)) expectedAttendees = eaSession;
        if (!expectedAttendees.HasValue && int.TryParse(session.GetString("Ack:Attendees"), out var ackAttendees)) expectedAttendees = ackAttendees;
        if (string.IsNullOrWhiteSpace(setupStyle)) setupStyle = session.GetString("Draft:SetupStyle");
        if (string.IsNullOrWhiteSpace(setupStyle)) setupStyle = session.GetString("Ack:SetupStyle");
        if (string.IsNullOrWhiteSpace(setupStyle) &&
            !string.IsNullOrWhiteSpace(roomName) &&
            roomName.Contains("Thrive", StringComparison.OrdinalIgnoreCase))
        {
            setupStyle = "boardroom";
        }
        _logger.LogInformation("[update_equipment] Event context: Venue={Venue}, Room={Room}, EventType={EventType}, Attendees={Attendees}",
            venueName ?? "(null)", roomName ?? "(null)", eventType ?? "(null)", expectedAttendees?.ToString() ?? "(null)");
        if (string.IsNullOrWhiteSpace(eventType) || !expectedAttendees.HasValue || expectedAttendees.Value <= 0 || string.IsNullOrWhiteSpace(setupStyle))
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(eventType)) missing.Add("event type");
            if (!expectedAttendees.HasValue || expectedAttendees.Value <= 0) missing.Add("number of attendees");
            if (string.IsNullOrWhiteSpace(setupStyle)) missing.Add("room setup style");
            _logger.LogWarning("[update_equipment] Blocked due to missing fields: {Fields}", string.Join(", ", missing));
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show updated quote summary - missing required event details",
                missingFields = missing,
                instruction = $"Do NOT call update_equipment or recommend_equipment_for_event again in this response. Ask ONE clear follow-up question to collect the next missing item ({string.Join(", ", missing)}), then wait for user reply before updating the summary."
            });
        }

        var addCouldNotFind = new List<string>();

        // Parse add_requests and optional event context; normalize "windows laptop" / "mac laptop" to type + preference
        if (root.TryGetProperty("add_requests", out var addArr) && addArr.ValueKind == JsonValueKind.Array && addArr.GetArrayLength() > 0)
        {
            var eventContext = new EventContext
            {
                EventType = !string.IsNullOrWhiteSpace(eventType) ? eventType : "",
                ExpectedAttendees = expectedAttendees ?? 0,
                VenueName = venueName,
                RoomName = roomName,
                ProjectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorAreas")),
                DurationDays = 1
            };
            if (eventContext.ProjectorAreas.Count == 0)
                eventContext.ProjectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorArea"));
            foreach (var add in addArr.EnumerateArray())
            {
                var eqTypeRaw = add.TryGetProperty("equipment_type", out var eqt) ? eqt.GetString() ?? "" : "";
                var qty = add.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt32() : 1;
                var preferenceFromArg = add.TryGetProperty("preference", out var pref) && pref.ValueKind == JsonValueKind.String ? pref.GetString() : null;
                if (string.IsNullOrWhiteSpace(eqTypeRaw)) continue;

                var eqType = eqTypeRaw.Trim().ToLowerInvariant();
                string? preference = preferenceFromArg;
                if (eqType.Contains("windows") || eqType.Contains("pc"))
                {
                    eqType = "laptop";
                    preference ??= "windows";
                    _logger.LogInformation("[update_equipment] Normalized add to laptop with preference=windows");
                }
                else if (eqType.Contains("mac"))
                {
                    eqType = "laptop";
                    preference ??= "mac";
                    _logger.LogInformation("[update_equipment] Normalized add to laptop with preference=mac");
                }

                eventContext.EquipmentRequests.Add(new EquipmentRequest
                {
                    EquipmentType = eqType,
                    Quantity = qty,
                    Preference = preference
                });
            }

            var recommendations = await _smartEquipment.GetRecommendationsAsync(eventContext, ct);
            if (recommendations.Items.Count == 0)
            {
                var requested = string.Join(", ", eventContext.EquipmentRequests.Select(r => $"{r.Quantity}x {r.EquipmentType}"));
                _logger.LogWarning("[update_equipment] Add returned 0 items for: {Requested}. Keeping list after removals.", requested);
                foreach (var r in eventContext.EquipmentRequests)
                    addCouldNotFind.Add($"{r.Quantity}x {r.EquipmentType}");
            }
            else
            {
                foreach (var item in recommendations.Items)
                {
                    var code = (item.ProductCode ?? "").Trim();
                    var desc = (item.Description ?? "").Trim();
                    var qty = item.Quantity;
                    var existing = currentItems.FirstOrDefault(i =>
                        (!string.IsNullOrEmpty(code) && string.Equals((i.ProductCode ?? "").Trim(), code, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(desc) && string.Equals((i.Description ?? "").Trim(), desc, StringComparison.OrdinalIgnoreCase)));
                    if (existing != null)
                    {
                        existing.Quantity += qty;
                        if (item.IsPackage)
                            existing.IsPackage = true;
                        _logger.LogInformation("[update_equipment] Merged into existing: {Qty} more → {Total}x {Description}", qty, existing.Quantity, existing.Description);
                    }
                    else
                    {
                        currentItems.Add(new SelectedEquipmentItem
                        {
                            ProductCode = code,
                            Description = desc,
                            Quantity = qty,
                            IsPackage = item.IsPackage,
                            ParentPackageCode = null,
                            Comment = item.Comment
                        });
                        _logger.LogInformation("[update_equipment] Added item: {Qty}x {Description}", qty, desc);
                    }
                }
            }
        }

        if (currentItems.Count == 0)
        {
            _logger.LogWarning("[update_equipment] Equipment list would be empty after changes");
            return JsonSerializer.Serialize(new
            {
                error = "Equipment list would be empty after your changes.",
                instruction = "Do NOT call update_equipment or recommend_equipment_for_event again in this response. Do NOT output a quote summary card or an empty equipment list. Tell the user their change would leave the quote empty and suggest keeping or adding at least one item (e.g. microphone, screen, laptop). Then they can request edits again."
            });
        }

        // Look up unit prices for summary
        var productCodes = currentItems.Select(i => i.ProductCode).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        var ratesList = await _db.TblRatetbls.AsNoTracking()
            .Where(r => productCodes.Contains(r.product_code ?? "") && r.TableNo == 0)
            .Select(r => new { r.product_code, r.rate_1st_day })
            .ToListAsync(ct);
        var rates = ratesList
            .GroupBy(x => x.product_code ?? "")
            .ToDictionary(g => g.Key, g => (double)(g.First().rate_1st_day ?? 0));

        // Retrieve date/time from session for display
        var eventDateStr = session.GetString("Draft:EventDate");
        var startTimeStr = session.GetString("Draft:StartTime");
        var endTimeStr = session.GetString("Draft:EndTime");
        var setupTimeStr = session.GetString("Draft:SetupTime");
        var rehearsalTimeStr = session.GetString("Draft:RehearsalTime");
        var packupTimeStr = session.GetString("Draft:PackupTime");
        var technicianSchedule = new TechnicianScheduleInfo(setupTimeStr, rehearsalTimeStr, startTimeStr, endTimeStr, packupTimeStr);
        
        // Fallback: extract from conversation transcript if not in session
        var threadId = session.GetString("AgentThreadId");
        if ((string.IsNullOrWhiteSpace(eventDateStr) || string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr)) && !string.IsNullOrEmpty(threadId))
        {
            try
            {
                var chatService = _http.HttpContext?.RequestServices.GetService(typeof(AzureAgentChatService)) as AzureAgentChatService;
                if (chatService != null)
                {
                    var (_, messages) = chatService.GetTranscript(threadId);
                    
                    // Extract date if not in session
                    if (string.IsNullOrWhiteSpace(eventDateStr))
                    {
                        var (dateDto, _) = _extraction.ExtractEventDate(messages);
                        if (dateDto.HasValue)
                        {
                            eventDateStr = dateDto.Value.ToString("yyyy-MM-dd");
                            _logger.LogInformation("[update_equipment] Extracted date from conversation: {Date}", eventDateStr);
                        }
                    }
                    
                    // Extract times if not in session
                    if (string.IsNullOrWhiteSpace(startTimeStr) || string.IsNullOrWhiteSpace(endTimeStr))
                    {
                        var scheduleTimes = _extraction.ExtractScheduleTimes(messages);
                        
                        if (string.IsNullOrWhiteSpace(startTimeStr) && scheduleTimes.ContainsKey("show_start_time"))
                        {
                            var extractedStart = scheduleTimes["show_start_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedStart.Length == 4 && int.TryParse(extractedStart, out var startMinutes))
                            {
                                var hours = startMinutes / 100;
                                var minutes = startMinutes % 100;
                                startTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[update_equipment] Extracted start time from conversation: {Time}", startTimeStr);
                            }
                            else
                            {
                                startTimeStr = extractedStart;
                            }
                        }
                        
                        if (string.IsNullOrWhiteSpace(endTimeStr) && scheduleTimes.ContainsKey("show_end_time"))
                        {
                            var extractedEnd = scheduleTimes["show_end_time"];
                            // Convert HHmm format to HH:mm for display
                            if (extractedEnd.Length == 4 && int.TryParse(extractedEnd, out var endMinutes))
                            {
                                var hours = endMinutes / 100;
                                var minutes = endMinutes % 100;
                                endTimeStr = $"{hours:D2}:{minutes:D2}";
                                _logger.LogInformation("[update_equipment] Extracted end time from conversation: {Time}", endTimeStr);
                            }
                            else
                            {
                                endTimeStr = extractedEnd;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - use session values only if extraction fails
                _logger.LogWarning(ex, "[update_equipment] Failed to extract date/time from conversation, using session values only");
            }
        }
        
        // Log if date/time are still missing
        if (string.IsNullOrWhiteSpace(eventDateStr))
        {
            _logger.LogWarning("[update_equipment] Event date is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(startTimeStr))
        {
            _logger.LogWarning("[update_equipment] Start time is missing from both session and conversation");
        }
        if (string.IsNullOrWhiteSpace(endTimeStr))
        {
            _logger.LogWarning("[update_equipment] End time is missing from both session and conversation");
        }
        
        double totalDayRate = 0;
        var summaryRequests = GetSummaryRequestsFromSession(session, currentItems);
        ApplySummaryRequestRemovals(summaryRequests, removeTypes);
        AppendSummaryRequests(summaryRequests, root);
        var projectionNeededForSummary = RequiresProjectorPlacementArea(summaryRequests);
        var projectorAreas = new List<string>();
        if (projectionNeededForSummary)
        {
            projectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorAreas"));
            if (projectorAreas.Count == 0)
                projectorAreas = GetNormalizedProjectorAreas(session.GetString("Draft:ProjectorArea"));
        }
        var summaryLines = new List<string>();
        summaryLines.Add("## 📋 Quote Summary\n");
        summaryLines.Add("### Event Details");
        if (!string.IsNullOrWhiteSpace(venueName)) summaryLines.Add($"**Venue:** {venueName}");
        if (!string.IsNullOrWhiteSpace(roomName)) summaryLines.Add($"**Room:** {roomName}");
        if (projectorAreas.Count == 1) summaryLines.Add($"**Projector Placement Area:** {projectorAreas[0]}");
        else if (projectorAreas.Count > 1) summaryLines.Add($"**Projector Placement Areas:** {string.Join(", ", projectorAreas)}");
        summaryLines.Add($"**Event Type:** {eventType}");
        summaryLines.Add($"**Attendees:** {expectedAttendees}");
        
        // Add event date if available
        if (!string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out var eventDate))
        {
            summaryLines.Add($"**Date:** {eventDate:dddd d MMMM yyyy}");
        }
        
        // Add start and end times if available
        if (!string.IsNullOrWhiteSpace(startTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(startTimeStr, out var startTime))
            {
                summaryLines.Add($"**Start Time:** {startTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**Start Time:** {startTimeStr}");
            }
        }
        if (!string.IsNullOrWhiteSpace(endTimeStr))
        {
            // TimeSpan format uses lowercase hh/mm (HH is for DateTime only)
            if (TimeSpan.TryParse(endTimeStr, out var endTime))
            {
                summaryLines.Add($"**End Time:** {endTime:hh\\:mm}");
            }
            else
            {
                summaryLines.Add($"**End Time:** {endTimeStr}");
            }
        }
        
        summaryLines.Add("");
        summaryLines.Add("### Requirement Summary\n");
        
        // Ensure equipment is displayed - add validation
        if (currentItems.Count == 0)
        {
            summaryLines.Add("*No equipment items in the quote. Please add equipment to continue.*");
            _logger.LogWarning("[update_equipment] Equipment list is empty after updates");
        }
        else
        {
            _logger.LogInformation("[update_equipment] Displaying {Count} requirement-level equipment lines", summaryRequests.Count);
            foreach (var line in BuildRequirementSummaryLines(summaryRequests))
                summaryLines.Add($"- {line}");
            summaryLines.Add("");

            _logger.LogInformation("[update_equipment] Calculating total day rate from {Count} selected items", currentItems.Count);
            foreach (var item in currentItems)
            {
                _logger.LogInformation("[update_equipment] Processing item: {Qty}x {Desc} (Code: {Code})", item.Quantity, item.Description, item.ProductCode);

                var rate = rates.GetValueOrDefault(item.ProductCode ?? "", 0);
                totalDayRate += rate * item.Quantity;
            }
            if (addCouldNotFind.Count > 0)
            {
                summaryLines.Add($"*Note: Could not find {string.Join(", ", addCouldNotFind)} to add; please ask for alternatives if needed.*\n");
                _logger.LogWarning("[update_equipment] Could not find {Count} requested items: {Items}", addCouldNotFind.Count, string.Join(", ", addCouldNotFind));
            }
        }

        // Recalculate technician support from updated equipment so Technician Support section is not dropped
        List<RecommendedLaborItem> laborItems = new List<RecommendedLaborItem>();
        try
        {
            var equipmentForLabor = currentItems.Select(i => new EquipmentItemForLabor
            {
                ProductCode = i.ProductCode ?? "",
                Description = i.Description ?? "",
                Quantity = i.Quantity
            }).ToList();
            var updateEventContext = new EventContext
            {
                EventType = !string.IsNullOrWhiteSpace(eventType) ? eventType : "",
                ExpectedAttendees = expectedAttendees ?? 0,
                VenueName = venueName,
                RoomName = roomName,
                DurationDays = 1,
                IsContentHeavy = string.Equals(session.GetString("Draft:IsContentHeavy"), "1", StringComparison.OrdinalIgnoreCase),
                IsContentLight = string.Equals(session.GetString("Draft:IsContentLight"), "1", StringComparison.OrdinalIgnoreCase)
            };
            var recommended = await _smartEquipment.RecommendLaborForEquipmentAsync(equipmentForLabor, updateEventContext, ct);
            laborItems = recommended?.ToList() ?? new List<RecommendedLaborItem>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[update_equipment] Labor recalculation failed; falling back to existing Draft:SelectedLabor if any");
            var existingLaborJson = session.GetString("Draft:SelectedLabor");
            if (!string.IsNullOrWhiteSpace(existingLaborJson))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<List<SelectedLaborItem>>(existingLaborJson);
                    if (existing != null && existing.Count > 0)
                    {
                        laborItems = existing.Select(l => new RecommendedLaborItem
                        {
                            ProductCode = string.IsNullOrWhiteSpace(l.ProductCode) ? "AVTECH" : l.ProductCode,
                            Description = l.Description ?? "",
                            Task = l.Task ?? "",
                            Quantity = l.Quantity,
                            Hours = l.Hours,
                            Minutes = l.Minutes,
                            RecommendationReason = "From previous recommendation"
                        }).ToList();
                    }
                }
                catch { /* ignore */ }
            }
        }

        if (laborItems.Count > 0)
        {
            var storedCoverage = session.GetString("Draft:TechnicianCoverage");
            if (!string.IsNullOrWhiteSpace(storedCoverage))
            {
                try
                {
                    using var coverageDoc = JsonDocument.Parse(storedCoverage);
                    var coverageRoot = coverageDoc.RootElement;
                    var hasCoverage = coverageRoot.ValueKind == JsonValueKind.Object;
                    if (hasCoverage)
                    {
                        var noTechnician = coverageRoot.TryGetProperty("NoTechnicianSupport", out var noTechProp) && noTechProp.ValueKind == JsonValueKind.True;
                        var stored = new TechnicianCoveragePreference(
                            hasCoverage,
                            noTechnician,
                            coverageRoot.TryGetProperty("Setup", out var setupProp) && setupProp.ValueKind == JsonValueKind.True,
                            coverageRoot.TryGetProperty("Rehearsal", out var rehearsalProp) && rehearsalProp.ValueKind == JsonValueKind.True,
                            coverageRoot.TryGetProperty("Operate", out var operateProp) && operateProp.ValueKind == JsonValueKind.True,
                            coverageRoot.TryGetProperty("Packdown", out var packdownProp) && packdownProp.ValueKind == JsonValueKind.True
                        );

                        laborItems = stored.NoTechnicianSupport
                            ? new List<RecommendedLaborItem>()
                            : ApplyTechnicianCoveragePreference(laborItems, stored);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[update_equipment] Failed to parse Draft:TechnicianCoverage");
                }
            }
        }

        if (laborItems.Count > 0)
        {
            summaryLines.Add("### Technician Support\n");
            foreach (var labor in laborItems)
            {
                summaryLines.Add($"- **{FormatLaborSummaryLine(labor, technicianSchedule)}**");
            }
            summaryLines.Add("");
        }
        
        // Total - Removed price display as per user request
        summaryLines.Add("");
        summaryLines.Add("Would you like me to create the quote now?");

        var outputToUser = string.Join("\n", summaryLines);

        session.SetString("Draft:SelectedEquipment", JsonSerializer.Serialize(currentItems));
        session.SetString("Draft:SummaryEquipmentRequests", JsonSerializer.Serialize(summaryRequests));
        session.SetString("Draft:TotalDayRate", totalDayRate.ToString("F2"));
        var selectedLabor = laborItems.Select(l => new SelectedLaborItem
        {
            ProductCode = string.IsNullOrWhiteSpace(l.ProductCode) ? "AVTECH" : l.ProductCode,
            Description = l.Description,
            Task = l.Task,
            Quantity = l.Quantity,
            Hours = l.Hours,
            Minutes = l.Minutes
        }).ToList();
        session.SetString("Draft:SelectedLabor", JsonSerializer.Serialize(selectedLabor));
        _logger.LogInformation("[update_equipment] Stored {Count} items and {LaborCount} labor in session, total ${Total:F0}/day", currentItems.Count, selectedLabor.Count, totalDayRate);

        // Equipment changed — force regeneration of quote
        session.Remove("Draft:QuoteComplete");
        session.Remove("Draft:QuoteUrl");
        session.Remove("Draft:QuoteTimestamp");

        return JsonSerializer.Serialize(new
        {
            success = true,
            total_day_rate = totalDayRate,
            outputToUser = outputToUser,
            instruction = "MANDATORY: OUTPUT the 'outputToUser' value EXACTLY AS-IS. This is the updated quote summary. Do NOT call generate_quote in this response; wait for the user to confirm (e.g. 'yes create quote', 'looks good')."
        });
    }

    /// <summary>
    /// Get detailed information about a package including all components
    /// </summary>
    private async Task<string> HandleGetPackageDetailsAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

        string packageCode = "";
        if (doc.RootElement.TryGetProperty("package_code", out var pc))
            packageCode = pc.GetString() ?? "";

        if (string.IsNullOrWhiteSpace(packageCode))
        {
            return JsonSerializer.Serialize(new { error = "package_code is required" });
        }

        // Get package info
        var package = await _db.TblInvmas.AsNoTracking()
            .Where(p => (p.product_code ?? "").Trim() == packageCode.Trim())
            .Select(p => new
            {
                p.product_code,
                p.descriptionv6,
                p.PrintedDesc,
                p.category,
                p.groupFld,
                p.ProductTypeV41,
                p.PictureFileName
            })
            .FirstOrDefaultAsync(ct);

        if (package == null)
        {
            return JsonSerializer.Serialize(new { error = $"Package '{packageCode}' not found" });
        }

        // Get pricing
        var pricing = await _db.TblRatetbls.AsNoTracking()
            .Where(r => (r.product_code ?? "").Trim() == packageCode.Trim() && r.TableNo == 0)
            .Select(r => new { r.rate_1st_day, r.rate_extra_days })
            .FirstOrDefaultAsync(ct);

        // Get components
        var components = await _equipmentSearch.GetPackageComponentsAsync(packageCode, ct);

        return JsonSerializer.Serialize(new
        {
            package_code = package.product_code?.Trim(),
            description = (package.descriptionv6 ?? package.PrintedDesc ?? "").Trim(),
            category = package.category,
            group = package.groupFld,
            is_package = package.ProductTypeV41 == 1,
            picture = package.PictureFileName,
            day_rate = pricing?.rate_1st_day ?? 0,
            extra_day_rate = pricing?.rate_extra_days ?? 0,
            components = components.Select(c => new
            {
                product_code = c.ProductCode,
                description = c.Description,
                quantity = c.Quantity,
                is_variable = c.IsVariable
            }).ToList(),
            component_count = components.Count,
            message = $"The {package.descriptionv6?.Trim()} package includes {components.Count} items"
        });
    }

    #endregion

    private string? GetBaseUrl()
    {
        var request = _http.HttpContext?.Request;
        if (request == null) return null;
        return $"{request.Scheme}://{request.Host}";
    }

    private string ToAbsoluteUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return "";
        
        var request = _http.HttpContext?.Request;
        if (request == null) return relativePath;

        var scheme = request.Scheme;
        var host = request.Host.ToUriComponent();
        return $"{scheme}://{host}{relativePath}";
    }

    /// <summary>
    /// Validates that equipment mentioned in conversation is included in the request.
    /// Returns a list of warnings for potentially missing equipment.
    /// </summary>
    private List<string> ValidateEquipmentContextFromConversation(string argsJson)
    {
        var warnings = new List<string>();
        
        try
        {
            // Get conversation from session (if available)
            var session = _http.HttpContext?.Session;
            var conversationText = session?.GetString("Draft:ConversationSummary") ?? "";
            
            // If no summary, try to get it from other session fields that might contain requirements
            if (string.IsNullOrWhiteSpace(conversationText))
            {
                var eventNotes = session?.GetString("Draft:EventNotes") ?? "";
                var avRequirements = session?.GetString("Draft:AVRequirements") ?? "";
                conversationText = $"{eventNotes} {avRequirements}";
            }
            
            // Parse the equipment requests from the args
            var requestedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(argsJson))
            {
                using var doc = JsonDocument.Parse(argsJson);
                if (doc.RootElement.TryGetProperty("equipment_requests", out var eqArray) && eqArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in eqArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("equipment_type", out var eqt))
                        {
                            var type = eqt.GetString()?.ToLowerInvariant() ?? "";
                            requestedTypes.Add(type);
                            // Also add related types
                            if (type.Contains("projector") || type.Contains("screen"))
                            {
                                requestedTypes.Add("projector");
                                requestedTypes.Add("screen");
                            }
                        }
                    }
                }
            }
            
            // Define keyword to equipment mappings
            var keywordMappings = new Dictionary<string[], string[]>(new ArrayComparer())
            {
                { new[] { "teams", "zoom", "video call", "video conference", "remote", "hybrid", "webcam" }, new[] { "camera", "microphone", "speaker", "display" } },
                { new[] { "presentation", "slides", "powerpoint", "present" }, new[] { "projector", "screen" } },
                { new[] { "video with sound", "play video", "audio playback", "music", "sound system" }, new[] { "speaker" } },
                { new[] { "speech", "presenter", "speaker at event", "speaking" }, new[] { "microphone" } },
                { new[] { "record", "recording", "film", "capture video" }, new[] { "camera" } }
            };
            
            // Check conversation for keywords
            var convLower = conversationText.ToLowerInvariant();
            foreach (var mapping in keywordMappings)
            {
                var keywords = mapping.Key;
                var requiredEquipment = mapping.Value;
                
                // Check if any keyword is mentioned
                var keywordFound = keywords.FirstOrDefault(k => convLower.Contains(k));
                if (keywordFound != null)
                {
                    // Check if all required equipment is in the request
                    foreach (var equipment in requiredEquipment)
                    {
                        if (!requestedTypes.Any(rt => rt.Contains(equipment) || equipment.Contains(rt)))
                        {
                            warnings.Add($"User mentioned '{keywordFound}' but '{equipment}' is not in the equipment request. Please confirm if this is needed.");
                        }
                    }
                }
            }
            
            if (warnings.Count > 0)
            {
                _logger.LogWarning("Context validation found potential missing equipment: {Warnings}", string.Join("; ", warnings));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating equipment context from conversation");
        }
        
        return warnings;
    }
    
    /// <summary>
    /// Custom comparer for string arrays in dictionary
    /// </summary>
    private class ArrayComparer : IEqualityComparer<string[]>
    {
        public bool Equals(string[]? x, string[]? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.SequenceEqual(y);
        }
        
        public int GetHashCode(string[] obj)
        {
            return obj.Aggregate(0, (a, b) => HashCode.Combine(a, b?.GetHashCode() ?? 0));
        }
    }
    
    internal static int ExtractAttendeesFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages?.ToList() ?? new List<DisplayMessage>();
        var userMessages = ordered
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (userMessages.Count == 0) return 0;

        var latestAttendees = 0;
        var userParts = userMessages.SelectMany(m => m.Parts ?? Enumerable.Empty<string>());
        foreach (var part in userParts)
        {
            if (string.IsNullOrWhiteSpace(part) || IsScheduleOrTimeSelectionMessage(part)) continue;

            var directMatch = Regex.Match(part, @"\b(\d{1,4})\s*(?:people|attendees|pax|participants|guests)\b", RegexOptions.IgnoreCase);
            if (directMatch.Success && int.TryParse(directMatch.Groups[1].Value, out var direct) && direct > 0)
            {
                latestAttendees = direct;
                continue;
            }

            var expectingMatch = Regex.Match(part, @"\b(?:expecting|about|around|approximately|roughly)\s+(\d{1,4})(?:\s*(?:people|attendees|pax|participants|guests))?\b", RegexOptions.IgnoreCase);
            if (expectingMatch.Success && int.TryParse(expectingMatch.Groups[1].Value, out var expecting) && expecting > 0)
            {
                latestAttendees = expecting;
                continue;
            }

            var updatedCountMatch = Regex.Match(part, @"\b(?:change|update|set|make)\s+(?:the\s+)?(?:attendee(?:s)?|count)\s+(?:to|as)\s+(\d{1,4})\b", RegexOptions.IgnoreCase);
            if (updatedCountMatch.Success && int.TryParse(updatedCountMatch.Groups[1].Value, out var changed) && changed > 0)
            {
                latestAttendees = changed;
            }
        }

        if (latestAttendees > 0)
        {
            return latestAttendees;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            var msg = ordered[i];
            if (!msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var assistantText = string.Join(" ", msg.Parts ?? Enumerable.Empty<string>());
            if (!Regex.IsMatch(assistantText, @"\b(?:attendees|how many|number of (?:people|guests|attendees|participants))\b", RegexOptions.IgnoreCase))
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null) continue;

            var nextText = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsScheduleOrTimeSelectionMessage(nextText))
                continue;

            if (TryParseAttendeeLikeReply(nextText, out var contextual))
            {
                latestAttendees = contextual;
                break;
            }
        }

        return latestAttendees;
    }

    internal static string? ExtractSetupStyleFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages?.ToList() ?? new List<DisplayMessage>();
        var userMessages = ordered
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (userMessages.Count == 0) return null;

        var explicitPattern = @"\b(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\s+(?:setup|style|layout)\b";
        var reversePattern = @"\b(?:setup|style|layout)\s+(?:is|will\s+be|should\s+be)?\s*(?:a\s+)?(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\b";
        string? latestStyle = null;

        foreach (var message in userMessages)
        {
            var part = GetMessageText(message);
            if (string.IsNullOrWhiteSpace(part))
                continue;

            var explicitMatch = Regex.Match(part, explicitPattern, RegexOptions.IgnoreCase);
            if (explicitMatch.Success)
            {
                latestStyle = NormalizeSetupStyleToken(explicitMatch.Groups[1].Value);
                continue;
            }

            var reverseMatch = Regex.Match(part, reversePattern, RegexOptions.IgnoreCase);
            if (reverseMatch.Success)
            {
                latestStyle = NormalizeSetupStyleToken(reverseMatch.Groups[1].Value);
                continue;
            }

            var standaloneMatch = Regex.Match(
                part,
                @"(?:^|\b)(?:yes|yeah|yep|correct|please|go with|let'?s go with|can we go in|i want|we want)?\s*(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)\b",
                RegexOptions.IgnoreCase);
            if (standaloneMatch.Success)
                latestStyle = NormalizeSetupStyleToken(standaloneMatch.Groups[1].Value);
        }

        // Fallback: short direct reply after assistant asks for setup style
        for (int i = 0; i < ordered.Count; i++)
        {
            var msg = ordered[i];
            if (!msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var assistantText = string.Join(" ", msg.Parts ?? Enumerable.Empty<string>());
            if (!Regex.IsMatch(assistantText, @"\b(setup|style|layout|boardroom style|theatre style|banquet style|classroom style)\b", RegexOptions.IgnoreCase))
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null) continue;

            var nextText = GetMessageText(nextUser).Trim();
            var tokenMatch = Regex.Match(
                nextText,
                @"^(?:yes|yeah|yep|correct|please|go with|let'?s go with|can we go in|i want|we want)?\s*(theatre|theater|boardroom|classroom|schoolroom|banquet|u-?shape|u\s+shape|cocktail|reception|cabaret|dinner)(?:\s+(?:setup|style|layout))?$",
                RegexOptions.IgnoreCase);
            if (tokenMatch.Success)
                latestStyle = NormalizeSetupStyleToken(tokenMatch.Groups[1].Value);
        }

        return latestStyle;
    }

    internal static string? ExtractRoomFromUserMessages(IEnumerable<DisplayMessage> messages)
    {
        var userMessages = (messages ?? Enumerable.Empty<DisplayMessage>())
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        string? latestRoom = null;
        foreach (var message in userMessages)
        {
            var part = GetMessageText(message);
            if (string.IsNullOrWhiteSpace(part)) continue;
            var text = part.ToLowerInvariant();

            if (text.Contains("westin ballroom 1") || Regex.IsMatch(text, @"\bballroom\s*1\b")) latestRoom = "Westin Ballroom 1";
            else if (text.Contains("westin ballroom 2") || Regex.IsMatch(text, @"\bballroom\s*2\b")) latestRoom = "Westin Ballroom 2";
            else if (text.Contains("full westin ballroom") || text.Contains("westin ballroom full") || text.Contains("full ballroom")) latestRoom = "Westin Ballroom";
            else if (text.Contains("westin ballroom")) latestRoom = "Westin Ballroom";
            else if (text.Contains("thrive boardroom") || text.Contains("thrive room")) latestRoom = "Thrive Boardroom";
            else if (Regex.IsMatch(text, @"\bthrive\b")) latestRoom = "Thrive Boardroom";
            else if (Regex.IsMatch(text, @"\belevate\b")) latestRoom = "Elevate";
            // Keep quoting focus on Thrive / Elevate / Westin Ballroom variants.
            // Do not auto-resolve non-primary rooms for quote flow.
            else if (text.Contains("meeting room") && text.Contains("four points")) latestRoom = "Meeting Room";
        }

        return latestRoom;
    }

    private static string GetMessageText(DisplayMessage message)
    {
        var partsText = string.Join(" ", (message.Parts ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p))).Trim();
        if (!string.IsNullOrWhiteSpace(partsText))
            return partsText;

        return (message.FullText ?? string.Empty).Trim();
    }

    private static List<WestinRoom> FilterQuotableWestinRooms(IEnumerable<WestinRoom> rooms)
        => (rooms ?? Enumerable.Empty<WestinRoom>())
            .Where(r => IsQuotableWestinRoomName(r.Name))
            .ToList();

    private static bool IsQuotableWestinRoomName(string? roomName)
    {
        var normalized = (roomName ?? "").Trim().ToLowerInvariant();
        return normalized == "westin ballroom"
            || normalized == "westin ballroom 1"
            || normalized == "westin ballroom 2"
            || normalized == "elevate"
            || normalized == "thrive boardroom";
    }

    private static bool IsScheduleOrTimeSelectionMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.Trim().ToLowerInvariant();

        if (lower.StartsWith("choose schedule:", StringComparison.OrdinalIgnoreCase)
            || lower.StartsWith("choose time:", StringComparison.OrdinalIgnoreCase)
            || lower.StartsWith("i've selected this schedule:", StringComparison.OrdinalIgnoreCase)
            || lower.StartsWith("schedule selected:", StringComparison.OrdinalIgnoreCase))
            return true;

        // Only treat as a schedule message when stage names appear alongside time tokens —
        // a plain "setup, rehearsal and pack up" reply is a valid technician-stage answer.
        return lower.Contains("setup") && lower.Contains("rehearsal") && lower.Contains("pack up")
               && Regex.IsMatch(lower, @"\d{1,2}:\d{2}");
    }

    private static bool TryParseAttendeeLikeReply(string text, out int attendees)
    {
        attendees = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        var replyMatch = Regex.Match(
            normalized,
            @"^(?:about|around|approximately|roughly)?\s*(\d{1,4})\s*(?:people|attendees|pax|participants|guests)?\.?$",
            RegexOptions.IgnoreCase);
        return replyMatch.Success &&
               int.TryParse(replyMatch.Groups[1].Value, out attendees) &&
               attendees > 0;
    }

    internal static bool HasExplicitVideoConferenceConfirmation(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0) return false;

        var userMessages = ordered
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var userMsg in userMessages)
        {
            var text = string.Join(" ", userMsg.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (Regex.IsMatch(text,
                @"\b(camera|webcam|ptz|microphone|mic|speaker|speakers|video\s+conference\s+unit|conference\s+camera|zoom|teams|video\s+call|video\s+conference|hybrid|remote attendees)\b",
                RegexOptions.IgnoreCase))
                return true;
        }

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            if (!string.Equals(current.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", current.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksVideoConferenceQuestion =
                Regex.IsMatch(assistantText, @"\b(video\s+conference|video\s+call|teams|zoom|remote attendees|hybrid)\b", RegexOptions.IgnoreCase) &&
                assistantText.Contains("?", StringComparison.Ordinal);
            if (!asksVideoConferenceQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsLikelyAffirmativeReply(reply))
                return true;
        }

        return false;
    }

    internal static bool IsLikelyAffirmativeReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
        if (Regex.IsMatch(normalized,
            @"\b(yes|yep|yeah|sure|please do|go ahead|sounds good|that works|correct|affirmative|definitely|absolutely|of course|for sure|please|will need|that would be great|that('s| is) (good|great|fine|perfect))\b"))
            return true;
        return normalized is "ok" or "okay";
    }

    internal sealed record TechnicianCoveragePreference(
        bool HasPreference,
        bool NoTechnicianSupport,
        bool Setup,
        bool Rehearsal,
        bool Operate,
        bool Packdown);

    /// <summary>
    /// Tries to deserialize a previously stored technician coverage preference from the session.
    /// Returns null if nothing is stored or the value cannot be parsed.
    /// </summary>
    internal static TechnicianCoveragePreference? TryLoadTechnicianCoverageFromSession(ISession session)
    {
        var stored = session.GetString("Draft:TechnicianCoverage");
        if (string.IsNullOrWhiteSpace(stored)) return null;

        try
        {
            using var doc = JsonDocument.Parse(stored);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var noTech = root.TryGetProperty("NoTechnicianSupport", out var noTechProp) && noTechProp.ValueKind == JsonValueKind.True;
            return new TechnicianCoveragePreference(
                HasPreference: true,
                NoTechnicianSupport: noTech,
                Setup:     root.TryGetProperty("Setup",     out var sp) && sp.ValueKind == JsonValueKind.True,
                Rehearsal: root.TryGetProperty("Rehearsal", out var rp) && rp.ValueKind == JsonValueKind.True,
                Operate:   root.TryGetProperty("Operate",   out var op) && op.ValueKind == JsonValueKind.True,
                Packdown:  root.TryGetProperty("Packdown",  out var pp) && pp.ValueKind == JsonValueKind.True
            );
        }
        catch
        {
            return null;
        }
    }

    internal static TechnicianCoveragePreference ExtractTechnicianCoveragePreference(IEnumerable<DisplayMessage> messages)
    {
        var ordered = (messages ?? Enumerable.Empty<DisplayMessage>()).ToList();
        if (ordered.Count == 0)
            return new TechnicianCoveragePreference(false, false, false, false, false, false);

        bool setup = false;
        bool rehearsal = false;
        bool operate = false;
        bool packdown = false;
        bool hasPreference = false;

        var userMessages = ordered
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var userMessage in userMessages)
        {
            var text = string.Join(" ", userMessage.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(text) || IsScheduleOrTimeSelectionMessage(text))
                continue;

            var normalized = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ");
            var mentionsTechnician = Regex.IsMatch(normalized, @"\b(technician|technicians|tech|crew|operator|operate|operation|staffing)\b");

            if (Regex.IsMatch(normalized, @"\b(no tech|no technician|no technicians|without technician|without tech|self[-\s]?operated|self[-\s]?service)\b"))
            {
                return new TechnicianCoveragePreference(true, true, false, false, false, false);
            }

            // Accept "all stages" / "full coverage" without requiring a technician keyword — the user
            // is responding to the stage-selection question and this phrasing is unambiguous enough.
            var allStages = Regex.IsMatch(normalized,
                @"\b(all (stages|day|day long|day-long|of them)|full coverage|full support|everything|whole duration|whole event|whole time|entire duration|entire event|full duration|for the duration|the whole thing)\b");

            if (allStages)
            {
                return new TechnicianCoveragePreference(true, false, true, true, true, true);
            }

            var foundSetup = Regex.IsMatch(normalized, @"\b(setup|set up|bump in)\b");
            var foundRehearsal = Regex.IsMatch(normalized, @"\b(rehearsal|test\s*&\s*connect|test and connect|soundcheck)\b");
            var foundOperate = Regex.IsMatch(normalized, @"\b(operate|operation|operator|during the event|during event|live support|show support|run the event)\b");
            var foundPackdown = Regex.IsMatch(normalized, @"\b(pack\s*down|packdown|pack\s*up|packup|bump out)\b");

            if (mentionsTechnician || foundSetup || foundRehearsal || foundOperate || foundPackdown)
            {
                setup |= foundSetup;
                rehearsal |= foundRehearsal;
                operate |= foundOperate;
                packdown |= foundPackdown;
                hasPreference = setup || rehearsal || operate || packdown;
            }
        }

        if (hasPreference)
            return new TechnicianCoveragePreference(true, false, setup, rehearsal, operate, packdown);

        // Contextual fallback 1: when assistant asks stage question and user gives short affirmative,
        // interpret as full-coverage preference.
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var assistant = ordered[i];
            if (!string.Equals(assistant.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", assistant.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksTechnicianStages =
                Regex.IsMatch(assistantText, @"\btechnician\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bsetup\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\brehearsal\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bpack\s*down|packdown|pack\s*up|packup\b", RegexOptions.IgnoreCase);

            if (!asksTechnicianStages)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (IsLikelyAffirmativeReply(reply))
                return new TechnicianCoveragePreference(true, false, true, true, true, true);

            if (Regex.IsMatch(reply, @"\b(no|not needed|none)\b", RegexOptions.IgnoreCase))
                return new TechnicianCoveragePreference(true, true, false, false, false, false);
        }

        // Contextual fallback 2: when assistant asks "entire event or only setup/rehearsal",
        // treat a short affirmative as full coverage and a short negative as no technician.
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var assistant = ordered[i];
            if (!string.Equals(assistant.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            var assistantText = string.Join(" ", assistant.Parts ?? Enumerable.Empty<string>());
            if (string.IsNullOrWhiteSpace(assistantText))
                continue;

            var asksBinaryCoverageQuestion =
                Regex.IsMatch(assistantText, @"\btechnical operator\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\b(entire event|whole duration|whole event)\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\bsetup\b", RegexOptions.IgnoreCase) &&
                Regex.IsMatch(assistantText, @"\brehearsal\b", RegexOptions.IgnoreCase);

            if (!asksBinaryCoverageQuestion)
                continue;

            var nextUser = ordered.Skip(i + 1).FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null)
                continue;

            var reply = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>()).Trim();
            if (string.IsNullOrWhiteSpace(reply))
                continue;

            if (IsLikelyAffirmativeReply(reply))
                return new TechnicianCoveragePreference(true, false, true, true, true, true);

            if (Regex.IsMatch(reply, @"\b(no|not needed|none)\b", RegexOptions.IgnoreCase))
                return new TechnicianCoveragePreference(true, true, false, false, false, false);

            var normalizedReply = Regex.Replace(reply.ToLowerInvariant(), @"\s+", " ");
            if (Regex.IsMatch(normalizedReply,
                @"\b(entire event|whole event|all day|full event|whole duration|whole time|full duration|entire duration|for the duration)\b"))
                return new TechnicianCoveragePreference(true, false, true, true, true, true);

            if (Regex.IsMatch(normalizedReply, @"\b(setup|set up|bump in)\b")
                || Regex.IsMatch(normalizedReply, @"\brehearsal\b"))
            {
                var includeSetup = Regex.IsMatch(normalizedReply, @"\b(setup|set up|bump in)\b");
                var includeRehearsal = Regex.IsMatch(normalizedReply, @"\brehearsal\b");
                return new TechnicianCoveragePreference(true, false, includeSetup, includeRehearsal, false, false);
            }
        }

        return new TechnicianCoveragePreference(false, false, false, false, false, false);
    }

    private static bool ShouldIncludeLaborTaskForCoverage(string? task, TechnicianCoveragePreference coverage)
    {
        if (coverage.NoTechnicianSupport)
            return false;
        if (!coverage.HasPreference)
            return true;

        var taskNorm = (task ?? "").Trim().ToLowerInvariant();
        if (taskNorm.Contains("setup"))
            return coverage.Setup;
        if (taskNorm.Contains("rehearsal") || taskNorm.Contains("test"))
            return coverage.Rehearsal;
        if (taskNorm.Contains("operate") || taskNorm.Contains("operator") || taskNorm.Contains("support"))
            return coverage.Operate;
        if (taskNorm.Contains("pack"))
            return coverage.Packdown;

        return coverage.Operate;
    }

    private static List<RecommendedLaborItem> ApplyTechnicianCoveragePreference(
        IEnumerable<RecommendedLaborItem> laborItems,
        TechnicianCoveragePreference coverage)
    {
        var source = (laborItems ?? Enumerable.Empty<RecommendedLaborItem>()).ToList();
        if (source.Count == 0)
            return source;
        if (coverage.NoTechnicianSupport)
            return new List<RecommendedLaborItem>();
        if (!coverage.HasPreference)
            return source;

        var filtered = source
            .Where(l => ShouldIncludeLaborTaskForCoverage(l.Task, coverage))
            .ToList();

        var operateTemplate = source.FirstOrDefault(l => IsOperateLaborTask(l.Task));
        if (operateTemplate != null)
        {
            if (coverage.Setup && !filtered.Any(l => IsSetupLaborTask(l.Task)))
            {
                filtered.Add(CloneLaborForStage(
                    operateTemplate,
                    task: "Setup",
                    hours: 1,
                    minutes: 0,
                    reasonSuffix: "Added setup coverage because the customer requested technician support for this stage."));
            }

            if (coverage.Rehearsal && !filtered.Any(l => IsRehearsalLaborTask(l.Task)))
            {
                filtered.Add(CloneLaborForStage(
                    operateTemplate,
                    task: "Rehearsal",
                    hours: 0,
                    minutes: 30,
                    reasonSuffix: "Added rehearsal coverage because the customer requested technician support for this stage."));
            }

            if (coverage.Packdown && !filtered.Any(l => IsPackdownLaborTask(l.Task)))
            {
                filtered.Add(CloneLaborForStage(
                    operateTemplate,
                    task: "Packdown",
                    hours: 1,
                    minutes: 0,
                    reasonSuffix: "Added pack down coverage because the customer requested technician support for this stage."));
            }
        }

        return filtered
            .OrderBy(GetLaborTaskSortOrder)
            .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RecommendedLaborItem CloneLaborForStage(
        RecommendedLaborItem template,
        string task,
        double hours,
        int minutes,
        string reasonSuffix)
    {
        var baseReason = string.IsNullOrWhiteSpace(template.RecommendationReason)
            ? "Derived from the technician coverage request."
            : template.RecommendationReason.Trim();

        return new RecommendedLaborItem
        {
            ProductCode = string.IsNullOrWhiteSpace(template.ProductCode) ? "AVTECH" : template.ProductCode,
            Description = template.Description ?? "",
            Task = task,
            Quantity = Math.Max(1, template.Quantity),
            Hours = hours,
            Minutes = minutes,
            RecommendationReason = $"{baseReason} {reasonSuffix}".Trim()
        };
    }

    private static bool IsSetupLaborTask(string? task)
        => (task ?? "").Contains("setup", StringComparison.OrdinalIgnoreCase);

    private static bool IsRehearsalLaborTask(string? task)
    {
        var normalized = task ?? "";
        return normalized.Contains("rehearsal", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOperateLaborTask(string? task)
    {
        var normalized = task ?? "";
        return normalized.Contains("operate", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("operator", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("support", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackdownLaborTask(string? task)
        => (task ?? "").Contains("pack", StringComparison.OrdinalIgnoreCase);

    private static int GetLaborTaskSortOrder(RecommendedLaborItem labor)
    {
        if (IsSetupLaborTask(labor.Task))
            return 0;
        if (IsRehearsalLaborTask(labor.Task))
            return 1;
        if (IsOperateLaborTask(labor.Task))
            return 2;
        if (IsPackdownLaborTask(labor.Task))
            return 3;
        return 4;
    }

    private static string NormalizeSetupStyleToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        if (normalized == "theater") return "theatre";
        if (normalized == "schoolroom") return "classroom";
        if (normalized == "u shape") return "u-shape";
        return normalized;
    }

    #region Alternatives Gallery Builders
    
    /// <summary>
    /// Builds visual gallery pickers for equipment alternatives in each category.
    /// Shows alternatives the user can select if they want to change the recommendation.
    /// </summary>
    private async Task<Dictionary<string, string>> BuildAlternativesGalleriesAsync(
        IEnumerable<RecommendedEquipmentItem> recommendations,
        CancellationToken ct)
    {
        var galleries = new Dictionary<string, string>();
        var processedSearchKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var item in recommendations)
        {
            try
            {
                // Get category key (may be empty)
                var categoryKey = (item.Category ?? "").Trim().ToUpperInvariant();
                
                // Try to map category to search keyword
                var searchKeyword = !string.IsNullOrWhiteSpace(categoryKey) 
                    ? MapCategoryToSearchKeyword(categoryKey) 
                    : null;
                
                // FALLBACK: If category mapping fails, try to infer from description
                if (string.IsNullOrWhiteSpace(searchKeyword) && !string.IsNullOrWhiteSpace(item.Description))
                {
                    searchKeyword = InferSearchKeywordFromDescription(item.Description);
                    _logger.LogInformation("[GALLERY] Category '{Category}' not mapped, inferred '{SearchKeyword}' from description: {Description}", 
                        categoryKey, searchKeyword ?? "(none)", item.Description);
                }
                
                if (string.IsNullOrWhiteSpace(searchKeyword))
                {
                    _logger.LogDebug("[GALLERY] Skipping item - no search keyword for category '{Category}', description: {Description}", 
                        categoryKey, item.Description);
                    continue;
                }
                
                // Skip if we already built a gallery for this search keyword
                if (processedSearchKeywords.Contains(searchKeyword))
                    continue;
                processedSearchKeywords.Add(searchKeyword);
                
                _logger.LogInformation("[GALLERY] Searching for alternatives: category='{Category}', keyword='{SearchKeyword}'", 
                    categoryKey, searchKeyword);
                
                // Search for alternatives in this category
                var searchResult = await _equipmentSearch.SearchEquipmentAsync(searchKeyword, 6, ct);
                
                _logger.LogInformation("[GALLERY] Search returned {Count} items for keyword '{SearchKeyword}'", 
                    searchResult.Items.Count, searchKeyword);
                
                if (searchResult.Items.Count <= 1)
                    continue; // No alternatives to show
                
                // Build equipment items for the gallery (description only, no price)
                var equipmentItems = searchResult.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Description))
                    .Select(i => new IslaBlocks.EquipmentItem(
                        i.ProductCode,
                        i.Description,
                        i.Category,
                        !string.IsNullOrWhiteSpace(i.PictureFileName)
                            ? ToAbsoluteUrl($"/images/products/{i.PictureFileName}")
                            : null
                    ))
                    .Take(6)
                    .ToList();
                
                if (equipmentItems.Count > 0)
                {
                    // Use the search keyword as gallery key to avoid duplicates
                    var galleryKey = !string.IsNullOrWhiteSpace(categoryKey) ? categoryKey : searchKeyword.ToUpperInvariant();
                    var title = GetAlternativesTitle(galleryKey);
                    var galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(equipmentItems, title, max: 6);
                    galleries[galleryKey] = galleryHtml;
                    
                    _logger.LogInformation("[GALLERY] Built alternatives gallery for '{GalleryKey}': {Count} items", galleryKey, equipmentItems.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GALLERY] Failed to build alternatives gallery for item: {Description}", item.Description);
            }
        }
        
        _logger.LogInformation("[GALLERY] Total galleries built: {Count}", galleries.Count);
        return galleries;
    }
    
    /// <summary>
    /// Infers a search keyword from the equipment description when category mapping fails.
    /// </summary>
    private static string? InferSearchKeywordFromDescription(string description)
    {
        var desc = description.ToLowerInvariant();
        
        // Laptops
        if (desc.Contains("laptop") || desc.Contains("notebook") || desc.Contains("dell") || desc.Contains("hp ") || desc.Contains("lenovo"))
            return "laptop";
        
        // MacBooks
        if (desc.Contains("macbook") || desc.Contains("mac book") || desc.Contains("apple mac"))
            return "mac laptop";
        
        // Projectors
        if (desc.Contains("projector") || desc.Contains("epson") || desc.Contains("panasonic") || desc.Contains("barco"))
            return "projector";
        
        // Screens
        if (desc.Contains("screen") || desc.Contains("fastfold") || desc.Contains("stumpfl") || desc.Contains("tripod screen"))
            return "screen";
        
        // Microphones
        if (desc.Contains("microphone") || desc.Contains("mic ") || desc.Contains(" mic") || desc.Contains("wireless mic") || desc.Contains("shure") || desc.Contains("sennheiser"))
            return "microphone";
        
        // Speakers
        if (desc.Contains("speaker") || desc.Contains("pa system") || desc.Contains("jbl") || desc.Contains("qsc") || desc.Contains("bose"))
            return "speaker";
        
        // Cameras
        if (desc.Contains("camera") || desc.Contains("webcam") || desc.Contains("ptz"))
            return "camera";
        
        // Displays/Monitors
        if (desc.Contains("display") || desc.Contains("monitor") || desc.Contains("tv") || desc.Contains("led screen") || desc.Contains("lcd"))
            return "display";
        
        // Lecterns
        if (desc.Contains("lectern") || desc.Contains("podium"))
            return "lectern";
        
        // Clickers / presentation remotes
        if (desc.Contains("clicker") || desc.Contains("presentation remote") || (desc.Contains("wireless") && desc.Contains("presenter") && !desc.Contains("mic")))
            return "clicker";
        
        return null;
    }
    
    /// <summary>
    /// Maps database category codes to user-friendly search keywords.
    /// Includes main categories, subcategories, and common variations.
    /// </summary>
    private static string? MapCategoryToSearchKeyword(string category)
    {
        return category.ToUpperInvariant().Trim() switch
        {
            // Projectors
            "PROJECTR" => "projector",
            "PROJECTOR" => "projector",
            "EPSON" => "projector",
            "PANASONIC" => "projector",
            "BARCO" => "projector",
            "NEC" => "projector",
            
            // Screens
            "SCREEN" => "screen",
            "SCREENS" => "screen",
            "FASTFOLD" => "screen",
            "STUMPFL" => "screen",
            
            // Microphones
            "W/MIC" => "wireless microphone",
            "MIC" => "microphone",
            "MICROPHONE" => "microphone",
            "SHURE" => "microphone",
            "SENNHEISER" => "microphone",
            "AUDIO" => "microphone",
            
            // Speakers
            "SPEAKER" => "speaker",
            "SPEAKERS" => "speaker",
            "PWRD SPKR" => "speaker",
            "PA" => "speaker",
            "JBL" => "speaker",
            "QSC" => "speaker",
            "BOSE" => "speaker",
            
            // Cameras
            "CAMERA" => "camera",
            "CAMERAS" => "camera",
            "VIDEO" => "camera",
            "WEBCAM" => "camera",
            "PTZ" => "camera",
            
            // Laptops - including package subcategories
            "LAPTOP" => "laptop",
            "LAPTOPS" => "laptop",
            "LAPPACK" => "laptop",
            "PC" => "laptop",
            "DELL" => "laptop",
            "HP" => "laptop",
            "LENOVO" => "laptop",
            
            // MacBooks - including package subcategories
            "MACBOOK" => "mac laptop",
            "MAC" => "mac laptop",
            "MBPPACK" => "mac laptop",
            "APPLE" => "mac laptop",
            
            // Lecterns
            "LECTERN" => "lectern",
            "PODIUM" => "lectern",
            
            // Clickers / presentation remotes
            "CLICKER" => "clicker",
            "WIRPRES" => "clicker",
            "PRESENTER" => "clicker",
            
            // Displays/Monitors
            "DISPLAY" => "display",
            "DISPLAYS" => "display",
            "MONITOR" => "monitor",
            "MONITORS" => "monitor",
            "TV" => "display",
            "LED" => "display",
            "LCD" => "display",
            
            // Lighting
            "LIGHTING" => "lighting",
            "LIGHT" => "lighting",
            "LED LIGHT" => "lighting",
            
            // Staging
            "STAGING" => "staging",
            "STAGE" => "staging",
            "RISER" => "staging",
            
            _ => null
        };
    }
    
    /// <summary>
    /// Gets a user-friendly title for the alternatives gallery.
    /// Matches the expanded category mappings.
    /// </summary>
    private static string GetAlternativesTitle(string category)
    {
        return category.ToUpperInvariant().Trim() switch
        {
            // Projectors
            "PROJECTR" or "PROJECTOR" or "EPSON" or "PANASONIC" or "BARCO" or "NEC" 
                => "Alternative Projectors",
            
            // Screens
            "SCREEN" or "SCREENS" or "FASTFOLD" or "STUMPFL" 
                => "Alternative Screens",
            
            // Microphones
            "W/MIC" or "MIC" or "MICROPHONE" or "SHURE" or "SENNHEISER" or "AUDIO" 
                => "Alternative Microphones",
            
            // Speakers
            "SPEAKER" or "SPEAKERS" or "PWRD SPKR" or "PA" or "JBL" or "QSC" or "BOSE" 
                => "Alternative Speakers",
            
            // Cameras
            "CAMERA" or "CAMERAS" or "VIDEO" or "WEBCAM" or "PTZ" 
                => "Alternative Cameras",
            
            // Laptops
            "LAPTOP" or "LAPTOPS" or "LAPPACK" or "PC" or "DELL" or "HP" or "LENOVO" 
                => "Alternative Laptops",
            
            // MacBooks
            "MACBOOK" or "MAC" or "MBPPACK" or "APPLE" 
                => "Alternative MacBooks",
            
            // Lecterns
            "LECTERN" or "PODIUM" 
                => "Alternative Lecterns",
            
            // Clickers / presentation remotes
            "CLICKER" or "WIRPRES" or "PRESENTER" 
                => "Alternative Presentation Remotes",
            
            // Displays/Monitors
            "DISPLAY" or "DISPLAYS" or "MONITOR" or "MONITORS" or "TV" or "LED" or "LCD" 
                => "Alternative Displays",
            
            // Lighting
            "LIGHTING" or "LIGHT" or "LED LIGHT" 
                => "Alternative Lighting",
            
            // Staging
            "STAGING" or "STAGE" or "RISER" 
                => "Alternative Staging",
            
            _ => "Alternatives"
        };
    }
    
    /// <summary>
    /// Handles showing equipment alternatives for a specific category.
    /// Called when user asks for different options.
    /// </summary>
    public async Task<string> HandleShowEquipmentAlternativesAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        
        string equipmentType = "";
        string? excludeCode = null;
        int maxResults = 8;
        
        if (doc.RootElement.TryGetProperty("equipment_type", out var etProp))
            equipmentType = etProp.GetString() ?? "";
        if (doc.RootElement.TryGetProperty("exclude_product_code", out var excProp))
            excludeCode = excProp.GetString();
        if (doc.RootElement.TryGetProperty("max_results", out var mrProp) && mrProp.ValueKind == JsonValueKind.Number)
            maxResults = Math.Clamp(mrProp.GetInt32(), 1, 12);
        
        if (string.IsNullOrWhiteSpace(equipmentType))
        {
            return JsonSerializer.Serialize(new { 
                error = "equipment_type is required",
                instruction = "Ask the user what type of equipment they want alternatives for (e.g., screens, projectors, microphones, cameras, lecterns)"
            });
        }
        
        _logger.LogInformation("Showing equipment alternatives for type: {Type}, excluding: {Exclude}", equipmentType, excludeCode);
        
        // Search for equipment in this category
        var searchResult = await _equipmentSearch.SearchEquipmentAsync(equipmentType, maxResults + 2, ct);
        
        if (searchResult.Items.Count == 0)
        {
            return JsonSerializer.Serialize(new { 
                found = false,
                message = $"No {equipmentType} found in our inventory. Try a different equipment type.",
                instruction = "Let the user know we couldn't find this type of equipment and ask if they'd like to search for something else."
            });
        }
        
        // Filter out the excluded product if specified
        var filteredItems = searchResult.Items
            .Where(i => string.IsNullOrWhiteSpace(excludeCode) || 
                       !i.ProductCode.Equals(excludeCode, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();
        
        if (filteredItems.Count == 0)
        {
            return JsonSerializer.Serialize(new { 
                found = false,
                message = $"No alternative {equipmentType} available at this time.",
                instruction = "Let the user know there are no other options in this category right now."
            });
        }
        
        // Build the gallery (description only, no price)
        var equipmentItems = filteredItems
            .Select(i => new IslaBlocks.EquipmentItem(
                i.ProductCode,
                i.Description,
                i.Category,
                !string.IsNullOrWhiteSpace(i.PictureFileName)
                    ? ToAbsoluteUrl($"/images/products/{i.PictureFileName}")
                    : null
            ))
            .ToList();
        
        var galleryTitle = $"Choose a {searchResult.CategoryName}";
        var galleryHtml = IslaBlocks.BuildEquipmentGalleryBlock(equipmentItems, galleryTitle, max: maxResults);
        
        // Also build product list for AI context
        var products = filteredItems.Select(i => new
        {
            product_code = i.ProductCode,
            description = i.Description,
            category = i.Category,
            day_rate = i.DayRate,
            picture = i.PictureFileName
        }).ToList();
        
        return JsonSerializer.Serialize(new
        {
            found = true,
            category = searchResult.CategoryName,
            count = filteredItems.Count,
            products,
            outputToUser = galleryHtml,
            instruction = "MANDATORY: You MUST output the 'outputToUser' value EXACTLY AS-IS in your response. This creates the visual picker for the user to select from. Do NOT paraphrase or omit it. The picker will only appear if you include the [[ISLA_GALLERY]] content exactly."
        });
    }

    private async Task<string> HandleGetProductKnowledgeAsync(string argsJson, CancellationToken ct)
    {
        string? category = null;
        string? warehouseScope = null;
        if (!string.IsNullOrWhiteSpace(argsJson))
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("category", out var catProp))
                category = catProp.GetString()?.Trim();
            if (root.TryGetProperty("warehouse_scope", out var scopeProp))
                warehouseScope = scopeProp.GetString()?.Trim();
        }

        var dataPath = Path.Combine(_env.WebRootPath ?? "", "data");
        var sb = new StringBuilder();

        bool loadMaster = string.IsNullOrEmpty(warehouseScope) || string.Equals(warehouseScope, "master", StringComparison.OrdinalIgnoreCase);
        bool loadWestin = string.IsNullOrEmpty(warehouseScope) || string.Equals(warehouseScope, "westin", StringComparison.OrdinalIgnoreCase);

        if (!loadMaster && !loadWestin)
        {
            sb.AppendLine("**Product knowledge**\n\nUse warehouse_scope: `master` (Brisbane, Sydney, Melbourne) or `westin` (Westin Brisbane on-site).");
            return JsonSerializer.Serialize(new { outputToUser = sb.ToString() });
        }

        if (string.IsNullOrEmpty(category) && loadMaster && loadWestin)
        {
            sb.AppendLine("**MicroHire product knowledge**\n");
            sb.AppendLine("Products are recorded in RentalPoint. Inventory is held at:");
            sb.AppendLine("- **Master warehouses:** Brisbane (Main), Sydney (WH2), Melbourne (WH3)");
            sb.AppendLine("- **On-site:** Westin Brisbane (dedicated AV warehouse inside The Westin Brisbane)\n");
            sb.AppendLine("**Categories:** Audio Equipment, Visual Equipment, Lighting Equipment, Staging & Structures, Event Technology Solutions, Computers & Playback Systems, Cables/Power/Rigging Essentials, Special Effects & Theming.");
            sb.AppendLine("\nCall `get_product_knowledge` with `category` (e.g. \"Audio\", \"Lighting\") and/or `warehouse_scope` (\"master\" or \"westin\") for full details.");
            return JsonSerializer.Serialize(new { outputToUser = sb.ToString() });
        }

        if (loadMaster)
        {
            var masterPath = Path.Combine(dataPath, "product-knowledge-master.json");
            if (File.Exists(masterPath))
            {
                var masterJson = await File.ReadAllTextAsync(masterPath, ct);
                sb.AppendLine(FormatProductKnowledgeJson(masterJson, category, "Master (Brisbane, Sydney, Melbourne)"));
            }
        }

        if (loadWestin)
        {
            var westinPath = Path.Combine(dataPath, "product-knowledge-westin.json");
            if (File.Exists(westinPath))
            {
                var westinJson = await File.ReadAllTextAsync(westinPath, ct);
                if (sb.Length > 0) sb.AppendLine("\n---\n");
                sb.AppendLine(FormatProductKnowledgeJson(westinJson, category, "Westin Brisbane on-site"));
            }
        }

        var output = sb.ToString();
        if (string.IsNullOrWhiteSpace(output))
            output = "No product knowledge found for the requested category or warehouse scope.";

        return JsonSerializer.Serialize(new { outputToUser = output });
    }

    private async Task<string> HandleGetWestinVenueGuideAsync(CancellationToken ct)
    {
        var dataPath = Path.Combine(_env.WebRootPath ?? "", "data");
        var guidePath = Path.Combine(dataPath, "westin-venue-guide.json");
        if (!File.Exists(guidePath))
        {
            var msg = "Westin Brisbane venue guide is not available.";
            return JsonSerializer.Serialize(new { outputToUser = msg });
        }

        var json = await File.ReadAllTextAsync(guidePath, ct);
        var sb = new StringBuilder();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("description", out var descProp))
        {
            sb.AppendLine(descProp.GetString() ?? "");
            sb.AppendLine();
        }

        if (root.TryGetProperty("venues", out var venues) && venues.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("## Venues at The Westin Brisbane");
            sb.AppendLine();
            foreach (var v in venues.EnumerateArray())
            {
                var name = v.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!IsQuotableWestinRoomName(name))
                    continue;
                sb.AppendLine($"### {name}");
                sb.AppendLine();

                if (v.TryGetProperty("configuration", out var config) && config.ValueKind != JsonValueKind.Null)
                {
                    var configStr = config.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(configStr))
                        sb.AppendLine("**Configuration:** " + configStr + "  ");
                }
                if (v.TryGetProperty("sizeSqm", out var sqm) && sqm.ValueKind != JsonValueKind.Null)
                {
                    int? sqmVal = null;
                    if (sqm.ValueKind == JsonValueKind.Number && sqm.TryGetInt32(out var sV)) sqmVal = sV;
                    else if (sqm.ValueKind == JsonValueKind.String && int.TryParse(sqm.GetString(), out var sV2)) sqmVal = sV2;

                    if (sqmVal.HasValue)
                    {
                        var sqftLine = "";
                        if (v.TryGetProperty("sizeSqft", out var sqft) && sqft.ValueKind != JsonValueKind.Null)
                        {
                            int? sqftVal = null;
                            if (sqft.ValueKind == JsonValueKind.Number && sqft.TryGetInt32(out var sfV)) sqftVal = sfV;
                            else if (sqft.ValueKind == JsonValueKind.String && int.TryParse(sqft.GetString(), out var sfV2)) sqftVal = sfV2;
                            if (sqftVal.HasValue) sqftLine = $" ({sqftVal} sq.ft)";
                        }
                        sb.AppendLine($"**Area/Size:** {sqmVal} sqm{sqftLine}  ");
                    }
                }
                if (v.TryGetProperty("capacities", out var cap) && cap.ValueKind == JsonValueKind.Object)
                {
                    var capParts = new List<string>();
                    foreach (var p in cap.EnumerateObject())
                    {
                        string? val = p.Value.ValueKind == JsonValueKind.String
                            ? p.Value.GetString()
                            : (p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetRawText() : p.Value.GetString() ?? p.Value.GetRawText());
                        if (!string.IsNullOrEmpty(val) && val != "null")
                            capParts.Add($"{p.Name}: {val}");
                    }
                    if (capParts.Count > 0)
                        sb.AppendLine("**Capacities:** " + string.Join("; ", capParts) + "  ");
                }
                if (v.TryGetProperty("builtInAV", out var builtIn) && builtIn.ValueKind != JsonValueKind.Null)
                {
                    var s = builtIn.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                        sb.AppendLine("**Built-in AV:** " + s + "  ");
                }
                if (v.TryGetProperty("microhireAddOns", out var addOns) && addOns.ValueKind != JsonValueKind.Null)
                {
                    var s = addOns.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                        sb.AppendLine("**MicroHire add-ons:** " + s + "  ");
                }
                if (v.TryGetProperty("bestFor", out var bestFor) && bestFor.ValueKind != JsonValueKind.Null)
                {
                    var s = bestFor.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s))
                        sb.AppendLine("**Best for:** " + s + "  ");
                }
                sb.AppendLine();
            }
        }

        if (root.TryGetProperty("avInfrastructureAcrossAllSpaces", out var avInfra))
        {
            var s = avInfra.GetString()?.Trim();
            if (!string.IsNullOrEmpty(s))
            {
                sb.AppendLine("## AV Infrastructure Across All Westin Spaces");
                sb.AppendLine();
                sb.AppendLine(s);
                sb.AppendLine();
            }
        }

        if (root.TryGetProperty("roomSetupTypes", out var setupTypes) && setupTypes.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("## Room Setup Types");
            sb.AppendLine();
            foreach (var st in setupTypes.EnumerateArray())
            {
                var stName = st.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "";
                var stDesc = st.TryGetProperty("description", out var sd) ? sd.GetString() ?? "" : "";
                sb.AppendLine($"- **{stName}:** {stDesc}");
            }
        }

        var output = sb.ToString();
        if (string.IsNullOrWhiteSpace(output))
            output = "No venue guide data found.";

        return JsonSerializer.Serialize(new { outputToUser = output });
    }

    /// <summary>
    /// For Westin Brisbane: resolve room, use explicit setup_style, and check expected_attendees vs capacity.
    /// Returns (capacityWarning, capacityOkLine). If over capacity, warning includes suggested larger rooms.
    /// </summary>
    private async Task<(string? capacityWarning, string? capacityOkLine)> TryGetCapacityCheckAsync(
        string venueName, string roomName, int expectedAttendees, string? setupStyle, CancellationToken ct)
    {
        if (expectedAttendees <= 0 || string.IsNullOrWhiteSpace(setupStyle)) return (null, null);
        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var roomNorm = roomName.Trim().ToLowerInvariant();
        var roomNormSlug = roomNorm.Replace(" ", "-");

        // Robust room resolution
        var room = rooms.FirstOrDefault(r => r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Equals(roomName, StringComparison.OrdinalIgnoreCase))
                ?? rooms.FirstOrDefault(r => r.Slug.Equals(roomNormSlug, StringComparison.OrdinalIgnoreCase))
                ?? (roomNorm.Contains("thrive") ? rooms.FirstOrDefault(r => r.Slug == "thrive-boardroom") : null)
                ?? (roomNorm == "elevate 1" ? rooms.FirstOrDefault(r => r.Slug == "elevate-1") : null)
                ?? (roomNorm == "elevate 2" ? rooms.FirstOrDefault(r => r.Slug == "elevate-2") : null)
                ?? (roomNorm == "elevate" ? rooms.FirstOrDefault(r => r.Slug == "elevate") : null)
                ?? (roomNorm == "ballroom 1" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-1") : null)
                ?? (roomNorm == "ballroom 2" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-2") : null)
                ?? (roomNorm == "ballroom" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom") : null)
                ?? rooms.FirstOrDefault(r => r.Name.Contains(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Contains(roomNormSlug, StringComparison.OrdinalIgnoreCase));

        if (room is null) return (null, null);

        // Use explicit setup_style only. Do not infer from event type.
        var style = (setupStyle ?? "").Trim().ToLowerInvariant();
        string layoutType = style switch
        {
            "theatre" or "theater" => "Theatre",
            "banquet" => "Banquet",
            "classroom" or "schoolroom" => "Classroom",
            "boardroom" or "conference" => "Boardroom",
            "ushape" or "u-shape" or "u shape" => "U-Shape",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(layoutType))
            return (null, null);

        var layout = room.Layouts.FirstOrDefault(l => l.Type.Equals(layoutType, StringComparison.OrdinalIgnoreCase));
        var capacity = layout?.Capacity ?? 0;
        if (capacity <= 0) return (null, null);

        if (expectedAttendees > capacity)
        {
            // Suggest larger rooms that have this layout and fit the attendees
            var larger = rooms
                .Select(r2 =>
                {
                    var l2 = r2.Layouts.FirstOrDefault(l => l.Type.Equals(layoutType, StringComparison.OrdinalIgnoreCase));
                    return (room: r2, cap: l2?.Capacity ?? 0);
                })
                .Where(x => x.cap >= expectedAttendees)
                .OrderBy(x => x.cap)
                .Take(3)
                .Select(x => $"{x.room.Name} ({x.cap} {layoutType.ToLowerInvariant()})")
                .ToList();
            var suggest = larger.Count > 0 ? " Consider " + string.Join(", or ", larger) + "." : "";
            return ($"{room.Name} {layoutType} capacity is {capacity}; your event has {expectedAttendees} attendees.{suggest}", null);
        }

        return (null, $"**Room capacity ({layoutType}):** {capacity} — your event fits.");
    }

    private async Task<string> HandleGetRoomCapacityAsync(string argsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var roomName = doc.RootElement.TryGetProperty("room_name", out var rn) ? rn.GetString()?.Trim() : null;
        var setupType = doc.RootElement.TryGetProperty("setup_type", out var st) ? st.GetString()?.Trim() : null;

        if (string.IsNullOrWhiteSpace(roomName))
        {
            return JsonSerializer.Serialize(new { outputToUser = "Please specify a room name (e.g. Elevate, Thrive Boardroom, Westin Ballroom)." });
        }

        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var roomNorm = roomName.Trim().ToLowerInvariant();
        var roomNormSlug = roomNorm.Replace(" ", "-");

        // Robust room resolution
        var room = rooms.FirstOrDefault(r => r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Equals(roomName, StringComparison.OrdinalIgnoreCase))
                ?? rooms.FirstOrDefault(r => r.Slug.Equals(roomNormSlug, StringComparison.OrdinalIgnoreCase))
                ?? (roomNorm.Contains("thrive") ? rooms.FirstOrDefault(r => r.Slug == "thrive-boardroom") : null)
                ?? (roomNorm == "elevate 1" ? rooms.FirstOrDefault(r => r.Slug == "elevate-1") : null)
                ?? (roomNorm == "elevate 2" ? rooms.FirstOrDefault(r => r.Slug == "elevate-2") : null)
                ?? (roomNorm == "elevate" ? rooms.FirstOrDefault(r => r.Slug == "elevate") : null)
                ?? (roomNorm == "ballroom 1" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-1") : null)
                ?? (roomNorm == "ballroom 2" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom-2") : null)
                ?? (roomNorm == "ballroom" ? rooms.FirstOrDefault(r => r.Slug == "westin-ballroom") : null)
                ?? rooms.FirstOrDefault(r => r.Name.Contains(roomName, StringComparison.OrdinalIgnoreCase) || r.Slug.Contains(roomNormSlug, StringComparison.OrdinalIgnoreCase));

        if (room is null)
        {
            return JsonSerializer.Serialize(new { outputToUser = $"Room '{roomName}' was not found at The Westin Brisbane. Use list_westin_rooms to see available rooms." });
        }

        var capacities = room.Layouts.Where(l => l.Capacity > 0).ToList();
        if (capacities.Count == 0)
        {
            return JsonSerializer.Serialize(new { outputToUser = $"{room.Name}: capacity by setup is not specified in the catalog. Contact the venue for details." });
        }

        // Map optional setup_type (tool param) to layout type (catalog uses Theatre, Boardroom, U-Shape, etc.)
        string? layoutType = null;
        if (!string.IsNullOrWhiteSpace(setupType))
        {
            var setupNorm = setupType.Trim().ToLowerInvariant();
            layoutType = setupNorm switch
            {
                "theatre" or "theater" => "Theatre",
                "banquet" => "Banquet",
                "classroom" or "schoolroom" => "Classroom",
                "boardroom" or "conference" => "Boardroom",
                "ushape" or "u-shape" or "u shape" => "U-Shape",
                "reception" or "cocktail" => "Reception", // catalog may not have Reception; we use Cocktail where applicable
                _ => capacities.FirstOrDefault(l => l.Type.Equals(setupType, StringComparison.OrdinalIgnoreCase))?.Type
            };
            if (string.IsNullOrEmpty(layoutType) && capacities.All(c => !c.Type.Equals(setupType, StringComparison.OrdinalIgnoreCase)))
                layoutType = setupNorm switch
                {
                    "reception" => capacities.FirstOrDefault(c => c.Type.Equals("Reception", StringComparison.OrdinalIgnoreCase))?.Type ?? "Reception",
                    "cocktail" => "Banquet", // fallback for cocktail
                    _ => null
                };
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(layoutType))
        {
            var layout = room.Layouts.FirstOrDefault(l => l.Type.Equals(layoutType, StringComparison.OrdinalIgnoreCase));
            if (layout != null && layout.Capacity > 0)
            {
                sb.AppendLine($"{room.Name} — **{layout.Type}** capacity: **{layout.Capacity}**.");
                return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
            }
            sb.AppendLine($"{room.Name}: no capacity listed for '{setupType}'. Available setups: " +
                string.Join(", ", capacities.Select(c => $"{c.Type} ({c.Capacity})")) + ".");
            return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
        }

        sb.AppendLine($"**{room.Name}** — capacities by setup:");
        foreach (var c in capacities.OrderByDescending(c => c.Capacity))
            sb.AppendLine($"- **{c.Type}:** {c.Capacity}");
        return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
    }

    private async Task<string> HandleGetCapacityTableAsync(CancellationToken ct)
    {
        var rooms = await _roomCatalog.GetRoomsAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("## Westin Brisbane - Room Capacities (Theatre Style)");
        sb.AppendLine();
        sb.AppendLine("| Room Name | Theatre Capacity | Reception | Area (sqm) |");
        sb.AppendLine("| :--- | :---: | :---: | :---: |");

        var sortedRooms = rooms
            .Select(r => new
            {
                r.Name,
                Theatre = r.Layouts.FirstOrDefault(l => l.Type == "Theatre")?.Capacity ?? 0,
                Reception = r.Layouts.FirstOrDefault(l => l.Type == "Reception")?.Capacity ?? 0,
                Area = 0 // Will fetch from guide if needed, but for sorting we use Theatre
            })
            .OrderByDescending(x => x.Theatre)
            .ToList();

        // Get area from guide for completeness
        var dataPath = Path.Combine(_env.WebRootPath ?? "", "data");
        var guidePath = Path.Combine(dataPath, "westin-venue-guide.json");
        var areas = new Dictionary<string, int>();
        if (File.Exists(guidePath))
        {
            var guideJson = await File.ReadAllTextAsync(guidePath, ct);
            using var doc = JsonDocument.Parse(guideJson);
            foreach (var v in doc.RootElement.GetProperty("venues").EnumerateArray())
            {
                var name = v.GetProperty("name").GetString() ?? "";
                if (v.TryGetProperty("sizeSqm", out var sqm) && sqm.ValueKind == JsonValueKind.Number)
                    areas[name] = sqm.GetInt32();
            }
        }

        foreach (var r in sortedRooms)
        {
            var area = areas.TryGetValue(r.Name, out var a) ? a.ToString() : "-";
            var theatre = r.Theatre > 0 ? r.Theatre.ToString() : "-";
            var reception = r.Reception > 0 ? r.Reception.ToString() : "-";
            sb.AppendLine($"| {r.Name} | {theatre} | {reception} | {area} |");
        }

        return JsonSerializer.Serialize(new { outputToUser = sb.ToString().Trim() });
    }

    private static string FormatProductKnowledgeJson(string json, string? categoryFilter, string scopeLabel)
    {
        var sb = new StringBuilder();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("description", out var descProp))
        {
            sb.AppendLine($"**{scopeLabel}**");
            sb.AppendLine(descProp.GetString() ?? "");
            sb.AppendLine();
        }

        if (root.TryGetProperty("seasonalAvailability", out var seasonal) && seasonal.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("**Seasonal availability**");
            foreach (var prop in seasonal.EnumerateObject())
                sb.AppendLine($"- {prop.Name}: {prop.Value.GetString() ?? ""}");
            sb.AppendLine();
        }

        if (!root.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
            return sb.ToString();

        foreach (var cat in categories.EnumerateArray())
        {
            var id = cat.TryGetProperty("id", out var idP) ? idP.GetString()?.Trim() : null;
            var name = cat.TryGetProperty("name", out var nameP) ? nameP.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(name)) continue;

            if (!string.IsNullOrEmpty(categoryFilter))
            {
                var filter = categoryFilter.Trim();
                var match = string.Equals(id, filter, StringComparison.OrdinalIgnoreCase) ||
                            (name != null && name.Contains(filter, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;
            }

            sb.AppendLine($"## {name}");
            if (cat.TryGetProperty("description", out var d)) sb.AppendLine(d.GetString() ?? "").AppendLine();

            if (cat.TryGetProperty("equipmentTypes", out var et) && et.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Equipment types**");
                foreach (var item in et.EnumerateArray())
                    sb.Append("- ").AppendLine(item.GetString() ?? "");
                sb.AppendLine();
            }

            if (cat.TryGetProperty("eventRecommendations", out var er) && er.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Event recommendations**");
                foreach (var rec in er.EnumerateArray())
                {
                    var ev = rec.TryGetProperty("eventType", out var ep) ? ep.GetString() : null;
                    var recText = rec.TryGetProperty("recommendation", out var rp) ? rp.GetString() : null;
                    sb.AppendLine($"- **{ev}**: {recText}");
                }
                sb.AppendLine();
            }

            if (cat.TryGetProperty("integrationAndScalability", out var ias))
            { sb.AppendLine("**Integration & scalability**"); sb.AppendLine(ias.GetString() ?? ""); sb.AppendLine(); }
            if (cat.TryGetProperty("operationalNotes", out var on))
            { sb.AppendLine("**Operational notes**"); sb.AppendLine(on.GetString() ?? ""); sb.AppendLine(); }
            var hasOperationMode = cat.TryGetProperty("operationMode", out var operationModeProp);
            var hasOperationGuidance = cat.TryGetProperty("operationGuidance", out var operationGuidanceProp);
            if (hasOperationMode || hasOperationGuidance)
            {
                sb.AppendLine("**Operator requirement**");
                if (hasOperationMode)
                {
                    var mode = operationModeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(mode))
                        sb.AppendLine("- Mode: " + FormatOperationModeLabel(mode));
                }

                if (hasOperationGuidance)
                {
                    var guidance = operationGuidanceProp.GetString();
                    if (!string.IsNullOrWhiteSpace(guidance))
                        sb.AppendLine("- Guidance: " + guidance);
                }

                sb.AppendLine();
            }
            if (cat.TryGetProperty("externalSupport", out var es))
            { sb.AppendLine("**External support**"); sb.AppendLine(es.GetString() ?? ""); sb.AppendLine(); }

            if (cat.TryGetProperty("availabilitySeasons", out var av) && av.ValueKind == JsonValueKind.Object)
            {
                sb.AppendLine("**Availability & seasons**");
                foreach (var prop in av.EnumerateObject())
                    sb.AppendLine($"- {prop.Name}: {prop.Value.GetString() ?? ""}");
                sb.AppendLine();
            }

            if (cat.TryGetProperty("warehouseStock", out var ws) && ws.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Warehouse stock**");
                foreach (var wh in ws.EnumerateArray())
                {
                    var whName = wh.TryGetProperty("warehouseName", out var wn) ? wn.GetString() : null;
                    sb.AppendLine($"- **{whName}**");
                    if (wh.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                        foreach (var it in items.EnumerateArray())
                        {
                            var iname = it.TryGetProperty("name", out var n) ? n.GetString() : null;
                            var qty = it.TryGetProperty("quantity", out var q) ? q.GetInt32() : (int?)null;
                            sb.AppendLine($"  - {iname}" + (qty.HasValue ? $": {qty}" : ""));
                        }
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FormatOperationModeLabel(string mode)
    {
        return mode.Trim().ToLowerInvariant() switch
        {
            "self_operated" => "Self-operated",
            "operator_recommended" => "Operator recommended",
            "operator_required" => "Operator required",
            _ => mode
        };
    }

    private readonly record struct TechnicianScheduleInfo(
        string? Setup,
        string? Rehearsal,
        string? Start,
        string? End,
        string? Packup);

    private static string FormatLaborSummaryLine(RecommendedLaborItem labor, TechnicianScheduleInfo schedule)
    {
        var details = new List<string>();
        var taskLabel = NormalizeLaborTaskLabel(labor.Task);
        if (!string.IsNullOrWhiteSpace(taskLabel))
            details.Add(taskLabel);

        var duration = FormatLaborDuration(labor.Hours, labor.Minutes);
        if (string.IsNullOrWhiteSpace(duration) && taskLabel == "Operate")
            duration = ComputeOperateDurationFromSchedule(schedule);
        if (!string.IsNullOrWhiteSpace(duration))
            details.Add(duration);

        if (details.Count == 0)
            return $"{labor.Quantity}x {labor.Description}";

        return $"{labor.Quantity}x {labor.Description} ({string.Join(" | ", details)})";
    }

    private static string ComputeOperateDurationFromSchedule(TechnicianScheduleInfo schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.Start) || string.IsNullOrWhiteSpace(schedule.End))
            return "";
        if (!TimeSpan.TryParse(schedule.Start, out var start) || !TimeSpan.TryParse(schedule.End, out var end))
            return "";
        var duration = end - start;
        if (duration <= TimeSpan.Zero)
            return "";
        var totalMinutes = (int)duration.TotalMinutes;
        return FormatLaborDuration(totalMinutes / 60.0, totalMinutes % 60);
    }

    private static string FormatLaborDuration(double hours, int minutes)
    {
        var totalMinutes = minutes;
        if (hours > 0)
            totalMinutes += (int)Math.Round(hours * 60);

        if (totalMinutes <= 0)
            return "";

        var wholeHours = totalMinutes / 60;
        var remainingMinutes = totalMinutes % 60;

        if (wholeHours > 0 && remainingMinutes > 0)
            return $"{wholeHours}h {remainingMinutes}m";
        if (wholeHours > 0)
            return $"{wholeHours}h";

        return $"{remainingMinutes}m";
    }

    private static string NormalizeLaborTaskLabel(string? task)
    {
        var normalized = (task ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return "";

        if (normalized.Contains("test", StringComparison.OrdinalIgnoreCase))
            return "Rehearsal / Test & Connect";
        if (normalized.Contains("pack", StringComparison.OrdinalIgnoreCase))
            return "Pack down";
        if (normalized.Contains("setup", StringComparison.OrdinalIgnoreCase))
            return "Setup";
        if (normalized.Contains("operate", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("operator", StringComparison.OrdinalIgnoreCase))
            return "Operate";
        if (normalized.Contains("rehearsal", StringComparison.OrdinalIgnoreCase))
            return "Rehearsal";

        return normalized;
    }

    private static string BuildLaborTimeWindow(string taskLabel, TechnicianScheduleInfo schedule)
    {
        return taskLabel switch
        {
            "Setup" => FormatSingleTime(schedule.Setup, "by "),
            "Rehearsal" => FormatSingleTime(schedule.Rehearsal, "at "),
            "Rehearsal / Test & Connect" => !string.IsNullOrWhiteSpace(schedule.Rehearsal)
                ? FormatSingleTime(schedule.Rehearsal, "around ")
                : FormatSingleTime(schedule.Start, "before "),
            "Operate" => FormatRange(schedule.Start, schedule.End),
            "Pack down" => FormatSingleTime(schedule.Packup, "from "),
            _ => ""
        };
    }

    private static string FormatSingleTime(string? time, string prefix)
    {
        var formatted = FormatChatTime(time);
        return string.IsNullOrWhiteSpace(formatted) ? "" : $"{prefix}{formatted}";
    }

    private static string FormatRange(string? start, string? end)
    {
        var startFormatted = FormatChatTime(start);
        var endFormatted = FormatChatTime(end);
        if (string.IsNullOrWhiteSpace(startFormatted) || string.IsNullOrWhiteSpace(endFormatted))
            return "";
        return $"{startFormatted} to {endFormatted}";
    }

    private static string FormatChatTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        if (!TimeSpan.TryParse(value, out var ts))
            return value.Trim();

        var dt = DateTime.Today.Add(ts);
        return dt.ToString("h:mm tt");
    }

    private static List<string> BuildRequirementSummaryLines(IEnumerable<EquipmentRequest> requests)
    {
        var normalized = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.EquipmentType))
            .Select(r => new
            {
                EquipmentType = NormalizeEquipmentTypeLabel(r.EquipmentType),
                Quantity = Math.Max(1, r.Quantity)
            })
            .GroupBy(r => r.EquipmentType, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                EquipmentType = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .OrderBy(x => x.EquipmentType, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Quantity} {PluralizeEquipmentLabel(x.EquipmentType, x.Quantity)}")
            .ToList();

        return normalized;
    }

    private static string NormalizeEquipmentTypeLabel(string? rawType)
    {
        var value = (rawType ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            return "equipment item";

        return value switch
        {
            "mic" or "microphones" => "microphone",
            "speakers" => "speaker",
            "screens" => "screen",
            "projectors" => "projector",
            "laptops" => "laptop",
            "clickers" => "clicker",
            _ => value
        };
    }

    private static bool IsLaptopEquipmentType(string? equipmentType)
    {
        var value = (equipmentType ?? "").Trim().ToLowerInvariant();
        return value == "laptop"
            || value == "laptops"
            || value.Contains("laptop", StringComparison.OrdinalIgnoreCase)
            || value.Contains("macbook", StringComparison.OrdinalIgnoreCase)
            || value.Contains("notebook", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLaptopDependentAccessoryType(string? equipmentType)
    {
        var value = (equipmentType ?? "").Trim().ToLowerInvariant();
        return value.Contains("hdmi", StringComparison.OrdinalIgnoreCase)
            || value.Contains("adaptor", StringComparison.OrdinalIgnoreCase)
            || value.Contains("adapter", StringComparison.OrdinalIgnoreCase)
            || value.Contains("clicker", StringComparison.OrdinalIgnoreCase)
            || value.Contains("presentation remote", StringComparison.OrdinalIgnoreCase)
            || value.Contains("switcher", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ConversationIndicatesLaptopWorkflow(IEnumerable<DisplayMessage> messages)
    {
        foreach (var message in messages)
        {
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = string.IsNullOrWhiteSpace(message.FullText)
                ? string.Join(" ", message.Parts ?? Enumerable.Empty<string>())
                : message.FullText;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (Regex.IsMatch(text, @"\b(laptop|laptops|macbook|notebook|slides?|slide deck|presentation)\b", RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private static string PluralizeEquipmentLabel(string label, int quantity)
    {
        if (quantity == 1) return label;
        return label switch
        {
            "microphone" => "microphones",
            "speaker" => "speakers",
            "screen" => "screens",
            "projector" => "projectors",
            "laptop" => "laptops",
            "clicker" => "clickers",
            _ when label.EndsWith("s", StringComparison.OrdinalIgnoreCase) => label,
            _ => $"{label}s"
        };
    }

    private static List<EquipmentRequest> GetSummaryRequestsFromSession(ISession session, List<SelectedEquipmentItem> currentItems)
    {
        var json = session.GetString("Draft:SummaryEquipmentRequests");
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<EquipmentRequest>>(json);
                if (parsed is { Count: > 0 })
                    return parsed;
            }
            catch
            {
                // Fall through to inferred requests.
            }
        }

        return currentItems
            .Where(i => !string.IsNullOrWhiteSpace(i.Description))
            .GroupBy(i => InferEquipmentTypeFromDescription(i.Description), StringComparer.OrdinalIgnoreCase)
            .Select(g => new EquipmentRequest
            {
                EquipmentType = g.Key,
                Quantity = Math.Max(1, g.Sum(x => x.Quantity))
            })
            .ToList();
    }

    private static string InferEquipmentTypeFromDescription(string? description)
    {
        var text = (description ?? "").ToLowerInvariant();
        if (text.Contains("mic")) return "microphone";
        if (text.Contains("screen")) return "screen";
        if (text.Contains("projector")) return "projector";
        if (text.Contains("speaker")) return "speaker";
        if (text.Contains("laptop") || text.Contains("macbook") || text.Contains("notebook")) return "laptop";
        if (text.Contains("clicker") || text.Contains("presenter")) return "clicker";
        if (text.Contains("camera")) return "camera";
        if (text.Contains("flipchart")) return "flipchart";
        if (text.Contains("lectern") || text.Contains("podium")) return "lectern";
        if (text.Contains("mixer")) return "mixer";
        if (text.Contains("foldback") || text.Contains("confidence monitor")) return "foldback_monitor";
        if (text.Contains("switcher") || text.Contains("v1hd")) return "switcher";
        if (text.Contains("usbc") || text.Contains("hdmi adaptor")) return "hdmi_adaptor";
        if (text.Contains("sdi") && text.Contains("extension")) return "laptop_at_stage";
        return "equipment item";
    }

    private static void ApplySummaryRequestRemovals(List<EquipmentRequest> summaryRequests, List<string> removeTypes)
    {
        if (removeTypes.Count == 0) return;
        summaryRequests.RemoveAll(r =>
        {
            var type = (r.EquipmentType ?? "").Trim().ToLowerInvariant();
            return removeTypes.Any(remove => type.Contains(remove, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static void AppendSummaryRequests(List<EquipmentRequest> summaryRequests, JsonElement root)
    {
        if (!root.TryGetProperty("add_requests", out var addArr) || addArr.ValueKind != JsonValueKind.Array || addArr.GetArrayLength() == 0)
            return;

        foreach (var add in addArr.EnumerateArray())
        {
            var rawType = add.TryGetProperty("equipment_type", out var eqt) ? eqt.GetString() : null;
            if (string.IsNullOrWhiteSpace(rawType))
                continue;
            var quantity = add.TryGetProperty("quantity", out var qty) && qty.ValueKind == JsonValueKind.Number ? qty.GetInt32() : 1;
            var equipmentType = NormalizeEquipmentTypeLabel(rawType);
            var existing = summaryRequests.FirstOrDefault(x => string.Equals(x.EquipmentType, equipmentType, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                summaryRequests.Add(new EquipmentRequest
                {
                    EquipmentType = equipmentType,
                    Quantity = Math.Max(1, quantity)
                });
            }
            else
            {
                existing.Quantity += Math.Max(1, quantity);
            }
        }
    }

    private static void EmitAgentDebugLog(
        string runId,
        string hypothesisId,
        string location,
        string message,
        object data)
    {
        try
        {
            const string debugPath = "/Users/nitwit-watson/INTENT/repos/MicrohireAgentChat/.cursor/debug-7953da.log";
            var debugDir = System.IO.Path.GetDirectoryName(debugPath);
            if (!string.IsNullOrWhiteSpace(debugDir))
            {
                System.IO.Directory.CreateDirectory(debugDir);
            }

            var payload = new
            {
                sessionId = "7953da",
                runId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            var line = JsonSerializer.Serialize(payload);
            System.IO.File.AppendAllText(debugPath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Debug instrumentation must never break runtime flow.
            Console.Error.WriteLine($"[DEBUG7953] EmitAgentDebugLog write failed: {ex.Message}");
        }
    }

    #endregion
}

// Supporting types for check_date_availability
public class CheckDateArgs
{
    public string? date { get; set; }
    public string? endDate { get; set; }
    public int? venueId { get; set; }
    public string? room { get; set; }
    public string? startTime { get; set; }
    public string? endTime { get; set; }
}

