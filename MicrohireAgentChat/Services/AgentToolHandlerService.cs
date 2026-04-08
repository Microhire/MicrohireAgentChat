using Azure.AI.Agents.Persistent;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

/// <summary>Result of <see cref="AgentToolHandlerService.RecommendEquipmentFromWizardSessionAsync"/> (server-driven follow-up AV path).</summary>
public sealed record WizardEquipmentRecommendResult(bool Success, string? ErrorMessage);

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
    /// Runs the same recommendation pipeline as <c>recommend_equipment_for_event</c> with an empty
    /// <c>equipment_requests</c> array so <see cref="MergeWizardSessionIntoEquipmentRequests"/> supplies
    /// wizard fields from session (<c>Draft:FollowUpAvSubmitted</c>). Persists
    /// <c>Draft:SelectedEquipment</c>, labor, rates, etc., when successful.
    /// </summary>
    public async Task<WizardEquipmentRecommendResult> RecommendEquipmentFromWizardSessionAsync(string threadId, CancellationToken ct)
    {
        const string argsJson = "{\"equipment_requests\":[]}";
        var json = await HandleSmartEquipmentRecommendationAsync(argsJson, threadId, ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True)
                return new WizardEquipmentRecommendResult(true, null);
            if (root.TryGetProperty("success", out s) && s.ValueKind == JsonValueKind.False)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : "Equipment recommendation failed";
                return new WizardEquipmentRecommendResult(false, err);
            }
            if (root.TryGetProperty("error", out var errEl))
                return new WizardEquipmentRecommendResult(false, errEl.GetString());
            return new WizardEquipmentRecommendResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FollowUpAv] Failed to parse recommend_equipment JSON (len={Len})", json?.Length ?? 0);
            return new WizardEquipmentRecommendResult(false, "Could not parse equipment recommendation response");
        }
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
                "build_contact_form" => HandleBuildContactForm(argsJson),
                "build_event_form" => HandleBuildEventForm(argsJson),
                "build_av_extras_form" => HandleBuildAvExtrasFormDeprecated(argsJson),
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
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var stringValue = prop.Value.GetString() ?? "";
                if (prop.Name.Equals("venueId", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(stringValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var venueId))
                        map["venueId"] = venueId;
                }
                else
                {
                    map[prop.Name] = stringValue;
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                if (prop.Name.Equals("venueId", StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.TryGetInt32(out var venueId))
                        map["venueId"] = venueId;
                }
                else if (prop.Value.TryGetDouble(out var d))
                    map[prop.Name] = d;
            }
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
        CheckDateArgs args;
        try
        {
            args = JsonSerializer.Deserialize<CheckDateArgs>(JsonSerializer.Serialize(map), opts)
                   ?? throw new InvalidOperationException("check_date_availability: missing/invalid args");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "check_date_availability: failed to parse args from model");
            return JsonSerializer.Serialize(new
            {
                error = "Invalid check_date_availability arguments",
                detail = ex.Message,
                received = map
            });
        }

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

    private string HandleBuildContactForm(string argsJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var title = doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : "Before we begin, please share your details";
        var submitLabel = doc.RootElement.TryGetProperty("submitLabel", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : "Send details";

        var payload = new
        {
            ui = new
            {
                type = "contactForm",
                title,
                submitLabel
            }
        };

        var jsonToEmbed = JsonSerializer.Serialize(payload);
        return JsonSerializer.Serialize(new
        {
            success = true,
            outputToUser = $"Please complete this quick contact form:\n\n{jsonToEmbed}",
            instruction = "OUTPUT THE 'outputToUser' VALUE EXACTLY AS-IS in your response so the form renders."
        });
    }

    private string HandleBuildEventForm(string argsJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var session = _http.HttpContext?.Session;

        var title = doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()
            : "Great, now let's capture your event details";
        var submitLabel = doc.RootElement.TryGetProperty("submitLabel", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : "Send event details";

        var todayIso = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var eventDate = session?.GetString("Draft:EventDate") ?? todayIso;
        var eventType = session?.GetString("Draft:EventType") ?? "";
        var attendees = session?.GetString("Draft:ExpectedAttendees") ?? "";
        var setupStyle = session?.GetString("Draft:SetupStyle") ?? "";

        var rooms = _roomCatalog.GetVenueConfirmRoomOptions();
        var draftRoom = session?.GetString("Draft:RoomName")?.Trim() ?? "";
        var selectedSlug = WestinRoomCatalog.MatchDraftRoomNameToSlug(draftRoom, rooms);
        var roomOptions = rooms.Select(r => new { id = r.Slug, label = r.Name }).ToArray();

        var setupOptions = new[]
        {
            "Theatre", "Classroom", "Banquet", "Cocktail", "U-Shape", "Boardroom"
        };

        var payload = new
        {
            ui = new
            {
                type = "eventForm",
                title,
                submitLabel,
                eventType,
                attendees,
                eventDate,
                minDate = todayIso,
                setupStyle,
                venueLabel = WestinRoomCatalog.VenueName,
                roomOptions,
                selectedRoomSlug = selectedSlug,
                setupOptions,
                schedule = new
                {
                    setup = session?.GetString("Draft:SetupTime") ?? "07:00",
                    rehearsal = session?.GetString("Draft:RehearsalTime") ?? "09:30",
                    start = session?.GetString("Draft:StartTime") ?? "10:00",
                    end = session?.GetString("Draft:EndTime") ?? "16:00",
                    packup = session?.GetString("Draft:PackupTime") ?? "18:00",
                    stepMinutes = 30
                }
            }
        };

        var jsonToEmbed = JsonSerializer.Serialize(payload);
        return JsonSerializer.Serialize(new
        {
            success = true,
            outputToUser = $"Please complete this event form:\n\n{jsonToEmbed}",
            instruction = "OUTPUT THE 'outputToUser' VALUE EXACTLY AS-IS in your response so the form renders."
        });
    }

    /// <summary>
    /// Legacy tool removed from agent definitions — chat wizard injects followUpAvForm after Base AV.
    /// Kept so stale Azure agent configs that still expose the tool do not render a duplicate form.
    /// </summary>
    private static string HandleBuildAvExtrasFormDeprecated(string argsJson)
    {
        _ = argsJson;
        return JsonSerializer.Serialize(new
        {
            success = true,
            deprecated = true,
            outputToUser =
                "Do not show an AV extras form. The chat already includes the structured follow-up card (microphones, lectern, wireless presenter, laptop switcher, stage laptop, video conference). Ask the customer in one short sentence to complete that red button card if it is visible, or acknowledge if they already submitted it.",
            instruction =
                "Reply in plain text only. Do not embed JSON or {\"ui\":...} blocks. Do not call this tool again."
        });
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
        var isLeadEntry = string.Equals(session?.GetString("Draft:EntrySource"), "lead", StringComparison.OrdinalIgnoreCase);

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(contactName))
            missingFields.Add("customer name");
        if (string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contactPhone))
            missingFields.Add("contact email or phone number");
        if (string.IsNullOrWhiteSpace(organisation) && !isLeadEntry)
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
            int.TryParse(attendeesFromSessionStr, NumberStyles.None, CultureInfo.InvariantCulture, out attendeesFromSessionParsed);

        var leadSeededAttendeesStr = session?.GetString("Draft:LeadSeededExpectedAttendees");
        var leadSeededParsed = 0;
        if (!string.IsNullOrWhiteSpace(leadSeededAttendeesStr))
            int.TryParse(leadSeededAttendeesStr.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out leadSeededParsed);

        var explicitAttendeesFromTranscript = conversationMessages.Count > 0
            ? ExtractExplicitAttendeesFromUserMessages(conversationMessages)
            : 0;
        var userStatedAttendeesFull = conversationMessages.Count > 0
            ? ExtractAttendeesFromUserMessages(conversationMessages)
            : 0;

        var userAttendeesFromTranscript = ResolveUserAttendeesFromTranscriptForEquipmentRecommendation(
            explicitAttendeesFromTranscript,
            userStatedAttendeesFull,
            attendeesFromSessionParsed,
            leadSeededParsed);
        if (leadSeededParsed > 0
            && explicitAttendeesFromTranscript == 0
            && userStatedAttendeesFull > 0
            && attendeesFromSessionParsed > 0
            && userStatedAttendeesFull != attendeesFromSessionParsed
            && userAttendeesFromTranscript == 0)
        {
            _logger.LogWarning(
                "Attendees contextual inference ({Inferred}) disagrees with session ({Session}) while CRM prefill seed is {Seed} for thread {ThreadId} — using session, not contextual inference.",
                userStatedAttendeesFull, attendeesFromSessionParsed, leadSeededParsed, threadId);
        }

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
                leadSeededParsed,
                explicitAttendeesFromTranscript,
                userStatedAttendeesFull,
                userAttendeesFromTranscript
            });
        // #endregion

        int expectedAttendees;
        var expectedAttendeesSource = "none";
        if (userAttendeesFromTranscript > 0)
        {
            expectedAttendees = userAttendeesFromTranscript;
            expectedAttendeesSource = "user";
            if (attendeesFromArgs > 0 && attendeesFromArgs != userAttendeesFromTranscript)
            {
                _logger.LogWarning("Attendees mismatch for thread {ThreadId}. ToolArgs={ArgsAttendees}, UserStated={UserAttendees}. Using user-stated value.",
                    threadId, attendeesFromArgs, userAttendeesFromTranscript);
            }
            if (attendeesFromSessionParsed > 0 && attendeesFromSessionParsed != userAttendeesFromTranscript)
            {
                _logger.LogWarning("Attendees mismatch for thread {ThreadId}. Session={SessionAttendees}, UserStated={UserAttendees}. Using user-stated value.",
                    threadId, attendeesFromSessionParsed, userAttendeesFromTranscript);
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
                userAttendeesFromTranscript,
                leadSeededParsed
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

        var roomNameFromArgs = NormalizeRoomNameAlias(doc.RootElement.TryGetProperty("room_name", out var rn) ? rn.GetString() : null);
        var roomNameFromSession = NormalizeRoomNameAlias(session?.GetString("Draft:RoomName"));
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

        userStatedRoom = NormalizeRoomNameAlias(userStatedRoom);

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
            // Lead + venue-confirm session room (e.g. Elevate from sales form) must not be overridden
            // by fuzzy transcript extraction that echoes assistant "Westin Ballroom" wording.
            var entryLead = string.Equals(session?.GetString("Draft:EntrySource"), "lead", StringComparison.OrdinalIgnoreCase);
            var venueConfirmed = string.Equals(session?.GetString("Draft:VenueConfirmSubmitted"), "1", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(roomNameFromSession) && (entryLead || venueConfirmed))
                eventContext.RoomName = roomNameFromSession;
            else if (!string.IsNullOrWhiteSpace(userStatedRoom))
                eventContext.RoomName = userStatedRoom;
            else
                eventContext.RoomName = roomNameFromSession;
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

        MergeWizardSessionIntoEquipmentRequests(session, eventContext);

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
        var mentionsProjectionNeed = ConversationIndicatesProjectionNeed(userConversationTextForEquipmentSignals);
        var userExplicitCamera = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(camera|webcam|ptz)\b", RegexOptions.IgnoreCase);
        var userExplicitMicrophone = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(microphone|mic|lapel|handheld)\b", RegexOptions.IgnoreCase);
        var userExplicitSpeaker = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(speaker|speakers|audio playback|pa)\b", RegexOptions.IgnoreCase);

        // Thrive-specific guardrails: do not allow speaker/microphone/lectern/foldback/switcher requests.
        if (IsThriveBoardroomRoom(eventContext.RoomName))
        {
            eventContext.EquipmentRequests = eventContext.EquipmentRequests
                .Where(r => !IsSpeakerOrMicrophoneEquipmentType(r.EquipmentType))
                .Where(r => !IsDisallowedThriveAccessoryType(r.EquipmentType))
                .ToList();
        }

        // If projection intent is present in conversation but AI omitted projector/screen request,
        // add a projection request so projector-placement validation is enforced.
        var hasProjectionRequest = eventContext.EquipmentRequests.Any(r => IsProjectionEquipmentType(r.EquipmentType));
        if (!hasProjectionRequest && mentionsProjectionNeed)
        {
            EnsureEquipmentRequest(eventContext.EquipmentRequests, "projector", 1);
            hasProjectionRequest = true;
        }

        // If user confirmed clicker in conversation/session but request was omitted, add it.
        var clickerFromSession = string.Equals(session?.GetString("Draft:NeedsClicker"), "yes", StringComparison.OrdinalIgnoreCase);
        var clickerFromConversation = Regex.IsMatch(userConversationTextForEquipmentSignals, @"\b(clicker|wireless presenter|presentation remote)\b", RegexOptions.IgnoreCase);
        if (!eventContext.EquipmentRequests.Any(r => (r.EquipmentType ?? "").Contains("clicker", StringComparison.OrdinalIgnoreCase))
            && (clickerFromSession || clickerFromConversation))
        {
            EnsureEquipmentRequest(eventContext.EquipmentRequests, "clicker", 1);
        }
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

        // Rehearsal operator guard: ask whether the customer wants an operator for their rehearsal.
        var sessionRehearsalOp = (session?.GetString("Draft:RehearsalOperator") ?? "").Trim().ToLowerInvariant();
        var rehearsalOperatorConfirmed = sessionRehearsalOp == "yes"
            || HasExplicitRehearsalOperatorConfirmation(conversationMessages);
        var rehearsalOperatorDeclined = sessionRehearsalOp == "no"
            || HasExplicitRehearsalOperatorDeclined(conversationMessages);
        if (!rehearsalOperatorConfirmed && !rehearsalOperatorDeclined)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Cannot show quote summary - rehearsal operator preference not yet confirmed",
                missingFields = new[] { "rehearsal operator preference" },
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Ask exactly one question: 'Would you like an operator for your rehearsal?' Wait for the user's explicit yes/no answer, then call recommend_equipment_for_event again."
            });
        }
        if (rehearsalOperatorConfirmed)
        {
            session?.SetString("Draft:RehearsalOperator", "yes");
        }
        else if (rehearsalOperatorDeclined)
        {
            session?.SetString("Draft:RehearsalOperator", "no");
        }

        var includesVideoConferenceUnit = requestedEquipmentTypes.Contains("video_conference_unit")
                                          || requestedEquipmentTypes.Contains("video conference unit");
        var sessionVc = (session?.GetString("Draft:VideoConference") ?? "").Trim().ToLowerInvariant();
        var wizardConfirmedVideoConference = sessionVc is "yes" or "teams";
        var hasExplicitVideoConferenceConfirmation = HasExplicitVideoConferenceConfirmation(conversationMessages)
            || wizardConfirmedVideoConference;
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

        // HDMI adaptor must only be considered when the customer is bringing their own laptop.
        if (effectiveOwnershipAnswered && effectiveNeedsProvidedLaptop)
        {
            eventContext.EquipmentRequests = eventContext.EquipmentRequests
                .Where(r => !(r.EquipmentType ?? "").Contains("hdmi_adaptor", StringComparison.OrdinalIgnoreCase))
                .Where(r => !(r.EquipmentType ?? "").Contains("hdmi adaptor", StringComparison.OrdinalIgnoreCase))
                .Where(r => !(r.EquipmentType ?? "").Contains("usbc adaptor", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

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
        var userConfirmedFullWestinBallroom = UserExplicitlyConfirmedFullWestinBallroom(session, conversationMessages);
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
        var isWestinBallroomFamily = IsWestinBallroomFamilyRoom(eventContext.VenueName, eventContext.RoomName);
        var projectionNeeded = RequiresProjectorPlacementArea(eventContext.EquipmentRequests)
            || (isWestinBallroomFamily && mentionsProjectionNeed);
        var requestedProjectorCount = GetRequestedProjectorCount(eventContext.EquipmentRequests);
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
            // Exception: Base AV wizard captured placement into session — treat as authoritative.
            if (!hasProjectorAreaPromptInConversation && !projectorPromptShownForThread && !projectorSelectionCapturedForThread)
            {
                var baseAvPlacement = string.Equals(session?.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(session?.GetString("Draft:ProjectorArea"));
                if (!baseAvPlacement)
                    projectorAreas.Clear();
            }
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

            var allowSingleBaseAvFullBallroom = isFullWestinBallroomSelection
                && requiredAreaCount > 1
                && validAreas.Count == 1
                && string.Equals(session?.GetString("Draft:BaseAvSubmitted"), "1", StringComparison.Ordinal);

            if (invalidAreas.Count > 0 || (validAreas.Count < requiredAreaCount && !allowSingleBaseAvFullBallroom))
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
        if (!string.IsNullOrWhiteSpace(eventContext.VenueName) && !string.IsNullOrWhiteSpace(eventContext.RoomName) &&
            eventContext.VenueName.Trim().Contains("Westin", StringComparison.OrdinalIgnoreCase) && eventContext.VenueName.Trim().Contains("Brisbane", StringComparison.OrdinalIgnoreCase))
        {
            var (warning, _) = await TryGetCapacityCheckAsync(
                eventContext.VenueName, eventContext.RoomName, eventContext.ExpectedAttendees,
                setupStyle, ct);
            capacityWarning = warning;
        }

        // Get smart recommendations
        var recommendations = await _smartEquipment.GetRecommendationsAsync(eventContext, ct);

        // Technician preference guard:
        // before showing quote summary, confirm which stages the customer wants technician support for.
        var technicianCoverage = ExtractTechnicianCoveragePreference(conversationMessages);

        // If live transcript parsing found nothing, fall back to the value saved from a prior turn.
        if (!technicianCoverage.HasPreference && session != null)
            technicianCoverage = TryLoadTechnicianCoverageFromSession(session) ?? technicianCoverage;

        // Wizard session (TechWholeEvent / tech window) may hold technician intent before the synthetic
        // FollowUpAv line is appended to the Azure thread; transcript extraction would miss it.
        if (!technicianCoverage.HasPreference && session != null)
        {
            var inferred = TryInferTechnicianCoverageFromDraftSession(session);
            if (inferred != null)
                technicianCoverage = inferred;
        }

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
            recommendations.LaborItems = ApplyTechnicianCoveragePreference(recommendations.LaborItems, technicianCoverage, eventContext.RoomName);
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

        // Add rehearsal labour (task code 7) when the customer confirmed they want an operator for rehearsal.
        if (rehearsalOperatorConfirmed && !recommendations.LaborItems.Any(l => IsRehearsalLaborTask(l.Task)))
        {
            var operateTemplate = recommendations.LaborItems.FirstOrDefault(l => IsOperateLaborTask(l.Task));
            var productCode = operateTemplate?.ProductCode ?? "AVTECH";
            var description = operateTemplate?.Description ?? "AV Technician";
            recommendations.LaborItems.Add(new RecommendedLaborItem
            {
                ProductCode = productCode,
                Description = description,
                Task = "Rehearsal",
                Quantity = 1,
                Hours = 0,
                Minutes = 30,
                RecommendationReason = "Customer confirmed they would like an operator for their rehearsal."
            });
            recommendations.LaborItems = recommendations.LaborItems
                .OrderBy(GetLaborTaskSortOrder)
                .ThenBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        // Remove any rehearsal labour items when the customer explicitly declined a rehearsal operator.
        else if (rehearsalOperatorDeclined)
        {
            recommendations.LaborItems = recommendations.LaborItems
                .Where(l => !IsRehearsalLaborTask(l.Task))
                .ToList();
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

        // No long quote-summary UI: equipment is stored in session below. Brief line for the model only.
        string fullOutputToUser;
        var hasCapacityWarning = !string.IsNullOrEmpty(capacityWarning);
        if (hasCapacityWarning)
        {
            fullOutputToUser =
                $"**⚠️ Room capacity:** {capacityWarning}\n\n" +
                "This room cannot fit the attendee count for the chosen setup. Please choose a larger room or reduce attendees before we create a quote.";
        }
        else if (conversationContextWarnings.Count > 0)
        {
            fullOutputToUser = "**Note:** " + string.Join(". ", conversationContextWarnings) + ".";
        }
        else
        {
            fullOutputToUser = "Equipment selection is saved. Next: call **generate_quote** (do not list equipment or ask to confirm a quote summary).";
        }

        _logger.LogInformation("[EQUIP_RECOMMEND] Minimal outputToUser length: {Length} chars", fullOutputToUser.Length);

        var instruction = hasCapacityWarning
            ? "CRITICAL: The room capacity is exceeded. OUTPUT the 'outputToUser' value EXACTLY AS-IS. Do NOT call generate_quote until the user adjusts room or attendees."
            : (conversationContextWarnings.Count > 0
                ? $"Address these items briefly: {string.Join(", ", conversationContextWarnings)}. OUTPUT 'outputToUser' EXACTLY AS-IS, then call generate_quote if requirements are met."
                : "Do NOT output an equipment quote summary or ask 'Would you like me to create the quote now?'. OUTPUT 'outputToUser' EXACTLY AS-IS (one short line). Then call **generate_quote** in this turn when contact and schedule requirements are satisfied; if something is still missing, ask only for that.");

        if (session != null
            && leadSeededParsed > 0
            && explicitAttendeesFromTranscript == 0
            && int.TryParse(session.GetString("Draft:ExpectedAttendees"), NumberStyles.None, CultureInfo.InvariantCulture, out var sessAlign)
            && sessAlign == leadSeededParsed
            && eventContext.ExpectedAttendees != sessAlign)
        {
            _logger.LogWarning(
                "Aligning event context attendees from {Resolved} to session/CRM seed {Session} for thread {ThreadId} before tool response.",
                eventContext.ExpectedAttendees, sessAlign, threadId);
            eventContext.ExpectedAttendees = sessAlign;
        }

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
                    session.SetString("Draft:ExpectedAttendees", eventContext.ExpectedAttendees.ToString(CultureInfo.InvariantCulture));
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
                message = "I wasn't able to finalize equipment just now. Could you please try sending your message again?",
                instruction = "Do NOT call recommend_equipment_for_event again in this response. Reply once briefly and ask the user to try again. Then stop."
            });
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
            DevelopmentDebugLog.TryAppendLine("debug-7953da.log", line);
        }
        catch
        {
            /* never throw from debug instrumentation */
        }
    }

    /// <summary>
    /// When the chat wizard has submitted follow-up AV, merge session-captured fields into
    /// <see cref="EventContext.EquipmentRequests"/> so recommend_equipment_for_event succeeds even if the model
    /// sends a sparse <c>equipment_requests</c> array.
    /// </summary>
    private static void MergeWizardSessionIntoEquipmentRequests(ISession? session, EventContext eventContext)
    {
        if (session == null) return;
        if (!string.Equals(session.GetString("Draft:FollowUpAvSubmitted"), "1", StringComparison.Ordinal))
            return;

        var requests = eventContext.EquipmentRequests;
        if (requests is null) return;

        static bool HasAnyEquipmentType(List<EquipmentRequest> list, Func<string?, bool> predicate)
        {
            foreach (var r in list)
            {
                var t = (r.EquipmentType ?? "").Trim();
                if (string.IsNullOrEmpty(t)) continue;
                if (predicate(t)) return true;
            }
            return false;
        }

        static string NormalizeWizardMic(string? raw)
        {
            var t = (raw ?? "").Trim().ToLowerInvariant();
            return t == "lapel" ? "lapel" : "handheld";
        }

        if (int.TryParse(session.GetString("Draft:PresenterCount") ?? "", out var presCount) && presCount > 0)
            eventContext.NumberOfPresentations = Math.Max(eventContext.NumberOfPresentations, presCount);

        var inbuiltProj = string.Equals(session.GetString("Draft:BuiltInProjector"), "yes", StringComparison.OrdinalIgnoreCase);
        var inbuiltScreen = string.Equals(session.GetString("Draft:BuiltInScreen"), "yes", StringComparison.OrdinalIgnoreCase);
        var inbuiltAudio = string.Equals(session.GetString("Draft:BuiltInSpeakers"), "yes", StringComparison.OrdinalIgnoreCase);

        // Map "Inbuilt" combinations to specific types to help recommendation logic prioritize combined packages
        if (inbuiltProj && inbuiltScreen && inbuiltAudio)
        {
            if (!HasAnyEquipmentType(requests, t => t is "av" or "base av" or "base_av"))
                EnsureEquipmentRequest(requests, "av", 1);
        }
        else
        {
            if ((inbuiltProj || inbuiltScreen) && !HasAnyEquipmentType(requests, t => t is "vision" or "projector" or "screen" or "display" or "av"))
                EnsureEquipmentRequest(requests, "vision", 1);

            if (inbuiltAudio && !HasAnyEquipmentType(requests, t => t is "audio" or "speaker" or "av"))
                EnsureEquipmentRequest(requests, "audio", 1);
        }

        // For rooms with a combined "Inbuilt projector and screen" checkbox (e.g. Elevate),
        // unchecking means "no projection needed at all" — do NOT add external projector/screen.
        var combinedPS = string.Equals(session.GetString("Draft:CombinedProjectorScreen"), "1", StringComparison.Ordinal);

        if (!combinedPS)
        {
            // Standard checks for external items (when inbuilt is NOT selected but requested/needed)
            if (!inbuiltProj && !HasAnyEquipmentType(requests, t => t.Contains("projector", StringComparison.OrdinalIgnoreCase) || t == "av" || t == "vision"))
                EnsureEquipmentRequest(requests, "projector", 1);

            if (!inbuiltScreen && !HasAnyEquipmentType(requests, t => t.Contains("screen", StringComparison.OrdinalIgnoreCase) || t.Contains("display", StringComparison.OrdinalIgnoreCase) || t == "av" || t == "vision"))
                EnsureEquipmentRequest(requests, "screen", 1);
        }

        if (combinedPS && !inbuiltProj)
            eventContext.UserDeclinedProjection = true;

        // Do NOT auto-add speakers when inbuilt audio was not checked — the user made a deliberate
        // choice. The audio pairing logic respects UserDeclinedAudio to avoid overriding this.
        if (!inbuiltAudio && !HasAnyEquipmentType(requests, t => t.Contains("speaker", StringComparison.OrdinalIgnoreCase) || t is "pa" or "audio" or "av"))
            eventContext.UserDeclinedAudio = true;

        if (string.Equals(session.GetString("Draft:Flipchart") ?? "", "yes", StringComparison.OrdinalIgnoreCase))
            EnsureEquipmentRequest(requests, "flipchart", 1);

        var laptopMode = (session.GetString("Draft:LaptopMode") ?? "").Trim().ToLowerInvariant();
        _ = int.TryParse(session.GetString("Draft:LaptopQty") ?? "", out var laptopQty);
        if (laptopQty > 0 && !HasAnyEquipmentType(requests, IsLaptopEquipmentType))
        {
            if (laptopMode is "windows" or "mac")
            {
                requests.Add(new EquipmentRequest
                {
                    EquipmentType = "laptop",
                    Quantity = laptopQty,
                    Preference = laptopMode
                });
            }
        }

        if (string.Equals(session.GetString("Draft:AdapterOwnLaptops") ?? "", "yes", StringComparison.OrdinalIgnoreCase))
            EnsureEquipmentRequest(requests, "hdmi_adaptor", Math.Max(1, laptopQty));

        if (int.TryParse(session.GetString("Draft:MicQty") ?? "", out var micQty) && micQty > 0
            && !HasAnyEquipmentType(requests, t => (t ?? "").Contains("mic", StringComparison.OrdinalIgnoreCase)))
        {
            requests.Add(new EquipmentRequest
            {
                EquipmentType = "microphone",
                Quantity = micQty,
                MicrophoneType = NormalizeWizardMic(session.GetString("Draft:MicType"))
            });
        }

        var lectern = (session.GetString("Draft:Lectern") ?? "").Trim().ToLowerInvariant();
        if (lectern is "lectern-only" or "lectern-mic")
            EnsureEquipmentRequest(requests, "lectern", 1);

        if (string.Equals(session.GetString("Draft:FoldbackMonitor") ?? "", "yes", StringComparison.OrdinalIgnoreCase))
            EnsureEquipmentRequest(requests, "foldback_monitor", 1);

        if (string.Equals(session.GetString("Draft:WirelessPresenter") ?? "", "yes", StringComparison.OrdinalIgnoreCase))
            EnsureEquipmentRequest(requests, "wireless presenter", 1);

        if (string.Equals(session.GetString("Draft:LaptopSwitcher") ?? "", "yes", StringComparison.OrdinalIgnoreCase))
            EnsureEquipmentRequest(requests, "switcher", 1);

        if (string.Equals(session.GetString("Draft:StageLaptop") ?? "", "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(session.GetString("Draft:NeedsSdiCross") ?? "", "2", StringComparison.Ordinal))
            EnsureEquipmentRequest(requests, "laptop_at_stage", 1);

        var vc = (session.GetString("Draft:VideoConference") ?? "").Trim().ToLowerInvariant();
        if (vc is "yes" or "teams")
            EnsureEquipmentRequest(requests, "video_conference_unit", 1);
    }

    private static string? NormalizeRoomNameAlias(string? roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return roomName;

        var value = roomName.Trim();
        var lower = value.ToLowerInvariant();

        if (lower.Contains("westin ballroom 1", StringComparison.Ordinal) || Regex.IsMatch(lower, @"\bballroom\s*1\b"))
            return "Westin Ballroom 1";
        if (lower.Contains("westin ballroom 2", StringComparison.Ordinal) || Regex.IsMatch(lower, @"\bballroom\s*2\b"))
            return "Westin Ballroom 2";
        if (lower.Contains("westin ballroom full", StringComparison.Ordinal) || lower.Contains("full westin ballroom", StringComparison.Ordinal) || lower.Equals("westin ballroom", StringComparison.Ordinal))
            return "Westin Ballroom";
        if (lower.Contains("elevate 1", StringComparison.Ordinal))
            return "Elevate 1";
        if (lower.Contains("elevate 2", StringComparison.Ordinal))
            return "Elevate 2";
        if (lower.Contains("elevate full", StringComparison.Ordinal) || lower.Equals("elevate", StringComparison.Ordinal))
            return "Elevate";
        if (lower.Contains("thrive", StringComparison.Ordinal))
            return "Thrive Boardroom";

        return value;
    }

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

