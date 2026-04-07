using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Handles crew/labor persistence to tblcrew table
/// </summary>
public sealed class CrewPersistenceService
{
    private readonly BookingDbContext _db;
    private readonly ILogger<CrewPersistenceService> _logger;

    public CrewPersistenceService(BookingDbContext db, ILogger<CrewPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Insert crew rows from labor summary.
    /// Schema: tblcrew with columns (per guide):
    /// - ID (decimal 18,0) PK identity
    /// - booking_no_v32 (varchar 20) FK
    /// - heading_no (tinyint) - PER GUIDE: usually 0 for crew
    /// - seq_no (decimal 19,0) - PER GUIDE: starts at 65530 and increments
    /// - sub_seq_no (int) - PER GUIDE: usually 0 for crew
    /// - product_code_v42 (varchar 50) - labor code
    /// - trans_qty (decimal 10,0) - number of people
    /// - price (decimal 19,4) - total price
    /// - hours (int) - hours worked
    /// - Minutes (int) - minutes worked
    /// - person (varchar 50) - person name
    /// - task (byte) - task type code
    /// - techrateIsHourorDay (varchar 2) - "H" or "D"
    /// - HourlyRateID (decimal) - FK to rate table
    /// - UnpaidHours (int) - unpaid hours
    /// - UnpaidMins (int) - unpaid minutes
    /// - TechIsConfirmed (bit) - confirmed flag
    /// - MeetTechOnSite (bit) - meet on site flag
    /// </summary>
    public async Task InsertCrewRowsAsync(
        string bookingNo,
        Dictionary<string, string> facts,
        CancellationToken ct)
    {
        var crew = ParseSelectedLabor(GetFact(facts, "selected_labor"));
        if (!crew.Any())
        {
            var laborSummary = GetFact(facts, "labor_summary");
            if (string.IsNullOrWhiteSpace(laborSummary))
            {
                _logger.LogInformation("Skipping crew persistence for booking {BookingNo}: no selected_labor or labor_summary facts.", bookingNo);
                return;
            }
            // Backward-compatible fallback for legacy summary payloads.
            crew = ParseLaborSummary(laborSummary);
        }

        if (!crew.Any())
        {
            _logger.LogInformation("Skipping crew persistence for booking {BookingNo}: parsed crew list is empty.", bookingNo);
            return;
        }

        // Get existing crew for this booking
        var existing = await _db.TblCrews
            .Where(c => c.BookingNoV32 == bookingNo)
            .ToListAsync(ct);

        // Delete existing (we'll recreate)
        if (existing.Any())
        {
            _db.TblCrews.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);
        }

        // PER GUIDE: Start/end times from facts (HHmm e.g. "0930"); used for del_time_* and return_time_*
        var showStart = ParseHHmm(GetFact(facts, "show_start_time"));
        var showEnd = ParseHHmm(GetFact(facts, "show_end_time"));
        var setupTime = ParseHHmm(GetFact(facts, "setup_time")) ?? showStart;
        var rehearsalTime = ParseHHmm(GetFact(facts, "rehearsal_time")) ?? setupTime ?? showStart;
        var packupTime = ParseHHmm(GetFact(facts, "packup_time")) ?? showEnd ?? rehearsalTime ?? setupTime ?? showStart;

        DateTime? eventDate = null;
        var eventDateStr = GetFact(facts, "event_date");
        if (!string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out var parsedEventDate))
            eventDate = parsedEventDate.Date;

        // Insert new crew rows
        // PER GUIDE: Crew seq_no starts at 65530 and increments
        decimal seqNo = 65530;
        
        foreach (var item in crew)
        {
            var taskCode = TryParseTask(item.Task);
            // PER GUIDE: Operate/technician on site (task 3) hours = event duration; other stages use the requested or default stage duration.
            int hours = item.Hours;
            int minutes = item.Minutes;
            if (taskCode == 3 && showStart.HasValue && showEnd.HasValue)
            {
                var duration = MinutesBetween(showStart.Value, showEnd.Value);
                if (duration.HasValue && duration.Value > 0)
                {
                    hours = duration.Value / 60;
                    minutes = duration.Value % 60;
                }
            }
            else if (hours == 0 && minutes == 0)
            {
                // Keep a practical minimum duration for non-operate crew lines.
                hours = 1;
            }

            if (taskCode == 3 && hours == 0 && minutes == 0)
            {
                // If show times are missing, keep a conservative default.
                hours = 1;
            }

            // PER GUIDE: Start time comes from the stage-specific schedule slot.
            var startTime = taskCode switch
            {
                3 => showStart ?? rehearsalTime ?? setupTime,
                // 7 => rehearsalTime ?? setupTime ?? showStart,
                4 => packupTime ?? showEnd ?? rehearsalTime ?? setupTime ?? showStart,
                _ => setupTime ?? showStart
            };
            byte delH = 0, delM = 0, retH = 0, retM = 0;
            if (startTime.HasValue)
            {
                delH = startTime.Value.hour;
                delM = startTime.Value.min;
                if (taskCode == 3 && showEnd.HasValue)
                {
                    retH = showEnd.Value.hour;
                    retM = showEnd.Value.min;
                }
                else
                {
                    var durationMinutes = (hours * 60) + minutes;
                    if (durationMinutes <= 0)
                        durationMinutes = 60;
                    var stageEnd = AddMinutesToTime(startTime.Value, durationMinutes);
                    retH = stageEnd.hour;
                    retM = stageEnd.min;
                }
            }

            var laborCode = string.IsNullOrWhiteSpace(item.ProductCode) ? "AVTECH" : item.ProductCode.Trim().ToUpperInvariant();
            var laborRate = await ResolveLaborRateAsync(laborCode, ct);
            var unitRate = laborRate?.Rate ?? item.UnitRate ?? DefaultRateForCode(laborCode);

            var totalHours = hours + (minutes / 60m);
            var price = unitRate * totalHours * item.Quantity;
            var straightTime = (double)(hours + (minutes / 60m));

            DateTime? firstDate = null;
            DateTime? retnDate = null;
            if (eventDate.HasValue)
            {
                firstDate = eventDate.Value.Date.AddHours(delH).AddMinutes(delM);
                retnDate = eventDate.Value.Date.AddHours(retH).AddMinutes(retM);
                if (retnDate < firstDate)
                    retnDate = retnDate.Value.AddDays(1);
            }

            var row = new TblCrew
            {
                BookingNoV32 = bookingNo,
                HeadingNo = 0,
                SeqNo = seqNo,
                SubSeqNo = 0,
                ProductCodeV42 = laborRate?.Code ?? laborCode,
                TransQty = (int)item.Quantity,
                Price = (double)price,
                UnitRate = (double)unitRate,
                Hours = (byte)hours,
                Minutes = (byte)minutes,
                Person = null,
                Task = (byte?)taskCode,
                TechrateIsHourOrDay = "H",
                HourlyRateID = 0M,
                UnpaidHours = 0,
                UnpaidMins = 0,
                TechIsConfirmed = false,
                MeetTechOnSite = false,
                StraightTime = straightTime,
                DelTimeHour = delH,
                DelTimeMin = delM,
                ReturnTimeHour = retH,
                ReturnTimeMin = retM,
                FirstDate = firstDate,
                RetnDate = retnDate
            };

            // InMemory provider does not auto-generate decimal identity keys.
            if ((_db.Database.ProviderName ?? "").Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                row.ID = seqNo;
            }

            _db.TblCrews.Add(row);
            seqNo++;
        }

        await _db.SaveChangesAsync(ct);
    }

    // ==================== PRIVATE HELPERS ====================

    private record ParsedCrew(
        string ProductCode,
        string Task,
        decimal Quantity,
        int Hours,
        int Minutes,
        bool IsHourly,
        decimal? UnitRate,
        string? Person);

    private static string? GetFact(Dictionary<string, string> facts, string key)
    {
        if (facts.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val.Trim();
        return null;
    }

    /// <summary>
    /// Parse structured selected labor payload into crew items.
    /// </summary>
    private List<ParsedCrew> ParseSelectedLabor(string? selectedLaborJson)
    {
        if (string.IsNullOrWhiteSpace(selectedLaborJson))
        {
            _logger.LogDebug("ParseSelectedLabor called with empty payload.");
            return new List<ParsedCrew>();
        }

        try
        {
            var laborItems = JsonSerializer.Deserialize<List<SelectedLaborItem>>(
                selectedLaborJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (laborItems == null || laborItems.Count == 0)
            {
                _logger.LogInformation("ParseSelectedLabor found no labor items after deserialization.");
                return new List<ParsedCrew>();
            }

            var crew = new List<ParsedCrew>();
            foreach (var item in laborItems)
            {
                var quantity = item.Quantity <= 0 ? 1 : item.Quantity;
                var task = string.IsNullOrWhiteSpace(item.Task) ? "Operate" : item.Task.Trim();
                var code = string.IsNullOrWhiteSpace(item.ProductCode)
                    ? InferLaborCode(task, item.Description)
                    : item.ProductCode.Trim().ToUpperInvariant();

                var hoursWhole = (int)Math.Floor(item.Hours);
                var hoursRemainderMins = (int)Math.Round((item.Hours - hoursWhole) * 60);
                var minutes = item.Minutes + Math.Max(0, hoursRemainderMins);
                var normalizedHours = hoursWhole + (minutes / 60);
                var normalizedMinutes = minutes % 60;

                crew.Add(new ParsedCrew(
                    ProductCode: code,
                    Task: task,
                    Quantity: quantity,
                    Hours: normalizedHours,
                    Minutes: normalizedMinutes,
                    IsHourly: true,
                    UnitRate: null,
                    Person: null));
            }

            return crew;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ParseSelectedLabor failed to deserialize selected_labor payload.");
            return new List<ParsedCrew>();
        }
    }

    /// <summary>
    /// Parse labor summary text into structured crew items.
    /// Expected formats:
    /// - "2x Technician @ 8 hours = $800"
    /// - "1x Audio Engineer 4hrs @ $75/hr"
    /// - "Setup Crew (3 people) 2 hours"
    /// </summary>
    private static List<ParsedCrew> ParseLaborSummary(string summary)
    {
        var crew = new List<ParsedCrew>();

        // Pattern: "2x Task @ 8 hours" or "2x Task 8hrs" etc.
        var pattern = @"(\d+)\s*x?\s+([^@\n]+?)(?:\s+@\s+)?(\d+(?:\.\d+)?)\s*(hours?|hrs?|h)\s*(?:@\s*\$?([\d,\.]+))?";
        var matches = Regex.Matches(summary, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match m in matches)
        {
            var qty = decimal.Parse(m.Groups[1].Value);
            var task = m.Groups[2].Value.Trim();
            var hoursDecimal = decimal.Parse(m.Groups[3].Value);
            var unitRate = ParseDecimal(m.Groups[5].Value);
            var productCode = InferLaborCode(task, task);

            var hours = (int)Math.Floor(hoursDecimal);
            var minutes = (int)((hoursDecimal - hours) * 60);

            crew.Add(new ParsedCrew(
                ProductCode: productCode,
                Task: task,
                Quantity: qty,
                Hours: hours,
                Minutes: minutes,
                IsHourly: true,
                UnitRate: unitRate,
                Person: null
            ));
        }

        return crew;
    }

    private static decimal? ParseDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        val = Regex.Replace(val, @"[^\d\.]", "");
        if (decimal.TryParse(val, out var d)) return d;
        return null;
    }

    private static string? Trunc(string? s, int len)
        => string.IsNullOrWhiteSpace(s) ? s : (s!.Length <= len ? s : s[..len]);

    private static string InferLaborCode(string? task, string? description)
    {
        var text = $"{task} {description}".ToLowerInvariant();
        if (text.Contains("axtech") || text.Contains("audio technician") || text.Contains("senior audio"))
            return "AXTECH";
        if (text.Contains("vxtech") || text.Contains("vision technician") || text.Contains("video technician") || text.Contains("streaming technician"))
            return "VXTECH";
        if (text.Contains("lxtech") || text.Contains("lighting technician"))
            return "LXTECH";
        if (text.Contains("savtech") || text.Contains("senior av technician"))
            return "SAVTECH";
        return "AVTECH";
    }

    private static decimal DefaultRateForCode(string? productCode)
    {
        return (productCode ?? "").Trim().ToUpperInvariant() switch
        {
            "SAVTECH" => 125m,
            _ => 110m
        };
    }

    private static byte? TryParseTask(string? task)
    {
        if (string.IsNullOrWhiteSpace(task)) return null;
        var normalized = Regex.Replace(task.ToLowerInvariant(), @"[^a-z]", "");
        return normalized switch
        {
            "setup" or "su" => (byte)2,
            "testconnect" or "testandconnect" or "tc" => (byte)15,
            "rehearsal" or "reh" => (byte)7,
            "technicalsupport" or "operate" or "operator" or "technicianonsite" or "op" => (byte)3,
            "packdown" or "packup" or "pd" => (byte)4,
            _ => (byte)1
        };
    }

    /// <summary>Parse HHmm string (e.g. "0930" or "9:30") to (hour, min).</summary>
    private static (byte hour, byte min)? ParseHHmm(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        var digits = val.Replace(":", "").Trim().PadLeft(4, '0');
        if (digits.Length < 4 || !int.TryParse(digits.AsSpan(0, 2), out var h) || !int.TryParse(digits.AsSpan(2, 2), out var m))
            return null;
        if (h < 0 || h > 23 || m < 0 || m > 59) return null;
        return ((byte)h, (byte)m);
    }

    /// <summary>Add minutes to a time (hour, min). Wraps at 24h.</summary>
    private static (byte hour, byte min) AddMinutesToTime((byte hour, byte min) t, int addMinutes)
    {
        var totalMins = t.hour * 60 + t.min + addMinutes;
        totalMins = ((totalMins % 1440) + 1440) % 1440;
        return ((byte)(totalMins / 60), (byte)(totalMins % 60));
    }

    /// <summary>Minutes from start to end (same day).</summary>
    private static int? MinutesBetween((byte hour, byte min) start, (byte hour, byte min) end)
    {
        var startMins = start.hour * 60 + start.min;
        var endMins = end.hour * 60 + end.min;
        if (endMins < startMins) endMins += 1440; // next day
        return endMins - startMins;
    }

    /// <summary>
    /// Resolve labor rate from tblInvmas_Labour_Rates by labor product code.
    /// PER GUIDE: Get default rate where:
    /// - Locn = 20 (This is Microhire westin code)
    /// - IsDefault = 1
    /// </summary>
    private async Task<(string Code, decimal Rate)?> ResolveLaborRateAsync(string productCode, CancellationToken ct)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(productCode)
            ? "AVTECH"
            : productCode.Trim().ToUpperInvariant();

        try
        {
            // Resolve by product code via tblInvmas.ID -> tblInvmas_Labour_Rates.tblinvmasID
            // Labour_rate is float/real in SQL Server; cast so SqlClient materializes as decimal for EF.
            var labourRate = await _db.Database
                .SqlQuery<decimal?>($@"
                    SELECT TOP 1 CAST(r.Labour_rate AS decimal(18, 4)) AS [Value]
                    FROM tblInvmas_Labour_Rates r
                    INNER JOIN tblInvmas i ON r.tblinvmasID = i.ID
                    WHERE LTRIM(RTRIM(i.product_code)) = {normalizedCode}
                      AND r.Locn = 20
                      AND r.IsDefault = 1
                    ORDER BY r.rate_no")
                .FirstOrDefaultAsync(ct);

            if (labourRate.HasValue && labourRate.Value > 0)
            {
                return (normalizedCode, labourRate.Value);
            }

            // Fallback to AVTECH default for unknown/unsupported labor codes.
            if (!normalizedCode.Equals("AVTECH", StringComparison.OrdinalIgnoreCase))
            {
                var avtechRate = await _db.Database
                    .SqlQuery<decimal?>($@"
                        SELECT TOP 1 CAST(Labour_rate AS decimal(18, 4)) AS [Value]
                        FROM tblInvmas_Labour_Rates
                        WHERE tblinvmasID = 2939
                          AND Locn = 20
                          AND IsDefault = 1
                        ORDER BY rate_no")
                    .FirstOrDefaultAsync(ct);

                if (avtechRate.HasValue && avtechRate.Value > 0)
                {
                    return ("AVTECH", avtechRate.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch labour rate from tblInvmas_Labour_Rates, using default");
        }

        return (normalizedCode, DefaultRateForCode(normalizedCode));
    }
}

