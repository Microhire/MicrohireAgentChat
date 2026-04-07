using MicrohireAgentChat.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
    private static bool IsThriveBoardroomRoom(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        return room.Contains("thrive");
    }

    private static bool IsProjectionEquipmentType(string? equipmentType)
    {
        var type = (equipmentType ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type)) return false;
        return type.Contains("projector")
            || type.Contains("screen")
            || type.Contains("display")
            || type.Contains("vision")
            || type is "av" or "base av" or "base_av";
    }

    private static bool IsSpeakerOrMicrophoneEquipmentType(string? equipmentType)
    {
        var type = (equipmentType ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type)) return false;
        return type.Contains("speaker")
            || type.Contains("audio")
            || type.Contains("microphone")
            || type.Contains("mic");
    }

    private static bool IsDisallowedThriveAccessoryType(string? equipmentType)
    {
        var type = (equipmentType ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type)) return false;
        return type.Contains("lectern")
            || type.Contains("foldback")
            || type.Contains("switcher");
    }

    private static void EnsureEquipmentRequest(List<EquipmentRequest> requests, string equipmentType, int quantity = 1)
    {
        if (requests.Any(r => string.Equals((r.EquipmentType ?? "").Trim(), equipmentType, StringComparison.OrdinalIgnoreCase)))
            return;

        requests.Add(new EquipmentRequest
        {
            EquipmentType = equipmentType,
            Quantity = Math.Max(1, quantity)
        });
    }

    private static bool ConversationIndicatesProjectionNeed(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Regex.IsMatch(text, @"\b(slides?|powerpoint|ppt|projector|screen|display|presentation)\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(text, @"\b(video|videos)\b", RegexOptions.IgnoreCase);
    }

    private static bool RequiresProjectorPlacementArea(IEnumerable<EquipmentRequest> requests)
    {
        foreach (var request in requests)
        {
            var type = (request.EquipmentType ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(type)) continue;
            if (type.Contains("projector") || type.Contains("screen") || type.Contains("display"))
                return true;
        }
        return false;
    }

    private static int GetRequestedProjectorCount(IEnumerable<EquipmentRequest> requests)
    {
        var qty = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.EquipmentType))
            .Where(r => (r.EquipmentType ?? "").Contains("projector", StringComparison.OrdinalIgnoreCase))
            .Sum(r => Math.Max(1, r.Quantity));
        return qty <= 0 ? 1 : qty;
    }

    private static bool IsWestinBallroomFamilyRoom(string? venueName, string? roomName)
    {
        var venue = (venueName ?? "").Trim().ToLowerInvariant();
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        if (!(venue.Contains("westin") && venue.Contains("brisbane"))) return false;
        if (string.IsNullOrWhiteSpace(room)) return false;
        return room == "westin ballroom"
            || room == "westin ballroom 1"
            || room == "westin ballroom 2"
            || room == "full westin ballroom"
            || room == "westin ballroom full"
            || room == "ballroom"
            || room == "full ballroom"
            || room == "ballroom 1"
            || room == "ballroom 2";
    }

    private static bool IsAmbiguousWestinBallroomParentRoom(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        return room == "westin ballroom" || room == "ballroom";
    }

    private static bool IsFullWestinBallroomRoom(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        return room == "full westin ballroom"
            || room == "westin ballroom full"
            || room == "full ballroom";
    }

    private static bool HasExplicitVenueOrRoomInArgs(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        return (root.TryGetProperty("venue_name", out var venueProp)
                && venueProp.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(venueProp.GetString()))
            || (root.TryGetProperty("room_name", out var roomProp)
                && roomProp.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(roomProp.GetString()));
    }

    /// <summary>
    /// Returns true only when the tool args carry BOTH a non-empty venue_name AND a non-empty
    /// room_name. A call with only venue_name (and no room_name) is considered ambiguous - the
    /// AI may simply have omitted the room - so it must NOT be treated as an explicit context switch
    /// away from the Westin Ballroom flow.
    /// </summary>
    private static bool HasExplicitVenueAndRoomInArgs(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        var hasVenue = root.TryGetProperty("venue_name", out var venueProp)
            && venueProp.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(venueProp.GetString());
        var hasRoom = root.TryGetProperty("room_name", out var roomProp)
            && roomProp.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(roomProp.GetString());
        return hasVenue && hasRoom;
    }

    /// <summary>
    /// Extracts projector placement areas from the user message that immediately followed the
    /// Westin Ballroom floor plan prompt in the conversation transcript. Used as a session-miss
    /// fallback so that a valid user reply (e.g. "A and D") is never silently discarded due to
    /// session persistence edge-cases between HTTP requests.
    /// </summary>
    private static List<string> ExtractProjectorAreasFromConversationAfterPrompt(IEnumerable<DisplayMessage> messages)
    {
        var ordered = messages.ToList();
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            var msg = ordered[i];
            if (!string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)) continue;

            var text = msg.FullText ?? string.Join(" ", msg.Parts ?? Enumerable.Empty<string>());
            var isFloorPlanPrompt =
                text.Contains("/images/westin/westin-ballroom/floor-plan.png", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("projector placement area", StringComparison.OrdinalIgnoreCase);

            if (!isFloorPlanPrompt) continue;

            // Find the first user message that follows this floor plan prompt.
            var nextUser = ordered
                .Skip(i + 1)
                .FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (nextUser == null) continue;

            var userText = string.Join(" ", nextUser.Parts ?? Enumerable.Empty<string>());
            var areas = GetNormalizedProjectorAreas(userText);
            if (areas.Count > 0) return areas;
        }
        return new List<string>();
    }

    private static bool WasProjectorPromptShownForThread(ISession? session, string threadId) =>
        session != null
        && string.Equals(session.GetString("Draft:ProjectorPromptShown"), "1", StringComparison.Ordinal)
        && string.Equals(session.GetString("Draft:ProjectorPromptThreadId"), threadId, StringComparison.Ordinal);

    private static bool WasProjectorAreaCapturedForThread(ISession? session, string threadId) =>
        session != null
        && string.Equals(session.GetString("Draft:ProjectorAreaCaptured"), "1", StringComparison.Ordinal)
        && string.Equals(session.GetString("Draft:ProjectorAreaThreadId"), threadId, StringComparison.Ordinal);

    private static void MarkProjectorPromptShown(ISession session, string threadId)
    {
        session.SetString("Draft:ProjectorPromptShown", "1");
        session.SetString("Draft:ProjectorPromptThreadId", threadId);
    }

    private static void MarkProjectorAreaCaptured(ISession session, string threadId)
    {
        MarkProjectorPromptShown(session, threadId);
        session.SetString("Draft:ProjectorAreaCaptured", "1");
        session.SetString("Draft:ProjectorAreaThreadId", threadId);
    }

    private static void ClearProjectorPlacementDraftState(ISession session)
    {
        session.Remove("Draft:ProjectorArea");
        session.Remove("Draft:ProjectorAreas");
        session.Remove("Draft:ProjectorPromptShown");
        session.Remove("Draft:ProjectorPromptThreadId");
        session.Remove("Draft:ProjectorAreaCaptured");
        session.Remove("Draft:ProjectorAreaThreadId");
    }

    private static string? NormalizeLaptopPreference(string? preference)
    {
        var normalized = (preference ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "mac" or "macbook" or "apple" => "mac",
            "windows" or "pc" => "windows",
            _ => string.IsNullOrWhiteSpace(normalized) ? null : normalized
        };
    }

    private static bool UserExplicitlyConfirmedFullWestinBallroom(Microsoft.AspNetCore.Http.ISession? session, IEnumerable<DisplayMessage> messages)
    {
        if (session != null && string.Equals(session.GetString("Draft:VenueConfirmSubmitted"), "1", StringComparison.OrdinalIgnoreCase))
        {
            var draftRoom = session.GetString("Draft:RoomName");
            if (!string.IsNullOrWhiteSpace(draftRoom) &&
                (draftRoom.Contains("Westin Ballroom", StringComparison.OrdinalIgnoreCase) ||
                 draftRoom.Contains("full ballroom", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        foreach (var message in messages)
        {
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var part in message.Parts ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                var text = part.Trim().ToLowerInvariant();
                if (text.Contains("full westin ballroom", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("westin ballroom full", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("full ballroom", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var fullText = (message.FullText ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(fullText) &&
                (fullText.Contains("full westin ballroom", StringComparison.OrdinalIgnoreCase) ||
                 fullText.Contains("westin ballroom full", StringComparison.OrdinalIgnoreCase) ||
                 fullText.Contains("full ballroom", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetAllowedProjectorAreas(string? roomName)
    {
        var room = (roomName ?? "").Trim().ToLowerInvariant();
        if (room.Contains("ballroom 1", StringComparison.Ordinal) || room.Contains("ballroom-1", StringComparison.Ordinal))
            return new List<string> { "E", "D", "C" };
        if (room.Contains("ballroom 2", StringComparison.Ordinal) || room.Contains("ballroom-2", StringComparison.Ordinal))
            return new List<string> { "A", "F", "B" };
        return new List<string> { "A", "B", "C", "D", "E", "F" };
    }

    private static List<string> GetNormalizedProjectorAreasFromArgs(JsonElement root)
    {
        if (root.TryGetProperty("projector_areas", out var arr))
        {
            if (arr.ValueKind == JsonValueKind.Array)
            {
                var merged = string.Join(",", arr.EnumerateArray().Select(x => x.GetString() ?? ""));
                return GetNormalizedProjectorAreas(merged);
            }
            if (arr.ValueKind == JsonValueKind.String)
                return GetNormalizedProjectorAreas(arr.GetString());
        }

        if (root.TryGetProperty("projector_area", out var single) && single.ValueKind == JsonValueKind.String)
            return GetNormalizedProjectorAreas(single.GetString());

        return new List<string>();
    }

    private static List<string> GetNormalizedProjectorAreas(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        var text = raw.Trim().ToUpperInvariant().Replace(" ", "");
        if (text.Length == 1 && text[0] is >= 'A' and <= 'F')
            return new List<string> { text };

        var plusMatch = Regex.Match(text, @"^([A-F])\+([A-F])$");
        if (plusMatch.Success)
        {
            foreach (var g in new[] { plusMatch.Groups[1].Value, plusMatch.Groups[2].Value })
            {
                if (!result.Contains(g, StringComparer.OrdinalIgnoreCase))
                    result.Add(g);
            }

            return result;
        }

        foreach (var segment in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var seg = segment.Trim().ToUpperInvariant().Replace(" ", "");
            if (seg.Length == 1 && seg[0] is >= 'A' and <= 'F' && !result.Contains(seg, StringComparer.OrdinalIgnoreCase))
                result.Add(seg);
        }

        if (result.Count > 0)
            return result;

        foreach (Match m in Regex.Matches(text, @"\b(?:PROJECTOR\s+AREA|AREA)?\s*[:\-]?\s*([A-F])\b", RegexOptions.IgnoreCase))
        {
            var area = m.Groups[1].Value.ToUpperInvariant();
            if (!result.Contains(area, StringComparer.OrdinalIgnoreCase))
                result.Add(area);
        }
        return result;
    }
}
