using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;
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
        var laborSummary = GetFact(facts, "labor_summary");
        if (string.IsNullOrWhiteSpace(laborSummary)) return;

        // Parse labor summary
        var crew = ParseLaborSummary(laborSummary);
        if (!crew.Any()) return;

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

        // Insert new crew rows
        // PER GUIDE: Crew seq_no starts at 65530 and increments
        decimal seqNo = 65530;
        
        foreach (var item in crew)
        {
            // PER GUIDE: Get rate from tblInvmas_Labour_Rates with tblinvmasID=2939, Locn=20, IsDefault=1
            var laborRate = await ResolveLaborRateAsync(item.Task, ct);
            var rateId = laborRate?.ID;
            var unitRate = laborRate?.Rate ?? item.UnitRate ?? 110m; // default $110/hr from guide

            // Calculate price
            var totalHours = item.Hours + (item.Minutes / 60m);
            var price = unitRate * totalHours * item.Quantity;
            
            // PER GUIDE: StraightTime = hours + mins worked
            var straightTime = (double)(item.Hours + (item.Minutes / 60m));

            var row = new TblCrew
            {
                BookingNoV32 = bookingNo,
                HeadingNo = 0, // PER GUIDE: Usually 0 for crew
                SeqNo = seqNo, // PER GUIDE: Starts at 65530
                SubSeqNo = 0, // PER GUIDE: Usually 0 for crew
                // PER GUIDE: Use "AVTECH" product code (ID of AVTECH is 2939)
                ProductCodeV42 = "AVTECH",
                // PER GUIDE: TransQty = number of people
                TransQty = (int)item.Quantity,
                Price = (double)price,
                UnitRate = (double)unitRate,
                Hours = (byte)item.Hours,
                Minutes = (byte)item.Minutes,
                // PER GUIDE: Person field should be EMPTY
                Person = null,
                Task = (byte?)TryParseTask(item.Task),
                // PER GUIDE: techrateIsHourorDay = "H" not "D"
                TechrateIsHourOrDay = "H",
                HourlyRateID = 0M, // Default value
                UnpaidHours = 0,
                UnpaidMins = 0,
                TechIsConfirmed = false,
                MeetTechOnSite = false,
                // PER GUIDE: S.T. values = hours + mins work
                StraightTime = straightTime
            };

            _db.TblCrews.Add(row);
            seqNo++; // PER GUIDE: Increment (65530, 65531, 65532, ...)
        }

        await _db.SaveChangesAsync(ct);
    }

    // ==================== PRIVATE HELPERS ====================

    private record ParsedCrew(
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

            var hours = (int)Math.Floor(hoursDecimal);
            var minutes = (int)((hoursDecimal - hours) * 60);

            crew.Add(new ParsedCrew(
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

    private static byte? TryParseTask(string? task)
    {
        if (string.IsNullOrWhiteSpace(task)) return null;
        return task.ToLowerInvariant() switch
        {
            "setup" => (byte)2,
            "rehearsal" => (byte)7,
            "technical support" => (byte)3,
            "packdown" => (byte)4,
            _ => (byte)1
        };
    }

    /// <summary>
    /// Resolve labor rate from tblInvmas_Labour_Rates
    /// PER GUIDE: Get rate using these parameters:
    /// - tblinvmasID = 2939 (This is the ID of AVTECH)
    /// - Locn = 20 (This is Microhire westin code)
    /// - IsDefault = 1
    /// </summary>
    private async Task<(string ID, decimal Rate)?> ResolveLaborRateAsync(string task, CancellationToken ct)
    {
        // PER GUIDE: Get rate from tblInvmas_Labour_Rates with specific parameters
        // tblinvmasID = 2939 (AVTECH), Locn = 20 (Westin Brisbane), IsDefault = 1
        try
        {
            // Query tblInvmas_Labour_Rates for the default AVTECH rate at Westin location
            var labourRate = await _db.Database
                .SqlQuery<decimal?>($"SELECT Labour_rate FROM tblInvmas_Labour_Rates WHERE tblinvmasID = 2939 AND Locn = 20 AND IsDefault = 1")
                .FirstOrDefaultAsync(ct);

            if (labourRate.HasValue && labourRate.Value > 0)
            {
                return ("AVTECH", labourRate.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch labour rate from tblInvmas_Labour_Rates, using default");
        }

        // Fallback to default rate of $110/hr as seen in the database
        return ("AVTECH", 110m);
    }
}

