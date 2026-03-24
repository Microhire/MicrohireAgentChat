using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services;

public sealed partial class AgentToolHandlerService
{
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
        if (text.Contains("laptop at stage") || text.Contains("laptop on stage") || text.Contains("sdicross")
            || (text.Contains("sdi") && (text.Contains("cross") || text.Contains("extension"))))
            return "laptop_at_stage";
        if (text.Contains("laptop") || text.Contains("macbook") || text.Contains("notebook")) return "laptop";
        if (text.Contains("clicker") || text.Contains("presenter")) return "clicker";
        if (text.Contains("camera")) return "camera";
        if (text.Contains("flipchart")) return "flipchart";
        if (text.Contains("lectern") || text.Contains("podium")) return "lectern";
        if (text.Contains("mixer")) return "mixer";
        if (text.Contains("foldback") || text.Contains("confidence monitor")) return "foldback_monitor";
        if (text.Contains("switcher") || text.Contains("v1hd")) return "switcher";
        if (text.Contains("usbc") || text.Contains("hdmi adaptor")) return "hdmi_adaptor";
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
}
