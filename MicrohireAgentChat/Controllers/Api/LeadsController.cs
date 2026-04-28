using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services;
using MicrohireAgentChat.Services.Persistence;

namespace MicrohireAgentChat.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class LeadsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILeadSubmissionFollowUp _leadFollowUp;
    private readonly OrganizationPersistenceService _orgService;
    private readonly IOptions<LeadEmailOptions> _leadEmailOptions;
    private readonly ILogger<LeadsController> _logger;

    public LeadsController(
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
        ILeadSubmissionFollowUp leadFollowUp,
        OrganizationPersistenceService orgService,
        IOptions<MicrohireAgentChat.Config.LeadEmailOptions> leadEmailOptions,
        ILogger<LeadsController> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _leadFollowUp = leadFollowUp;
        _orgService = orgService;
        _leadEmailOptions = leadEmailOptions;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest(new { success = false, error = "Request body is required." });

        var errors = ValidateRequest(request);
        if (errors.Count > 0)
            return BadRequest(new { success = false, errors });

        var token = Guid.NewGuid();
        var lead = new WestinLead
        {
            Token = token,
            Organisation = request.Organisation!.Trim(),
            OrganisationAddress = request.OrganisationAddress!.Trim(),
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            Email = request.Email!.Trim().ToLowerInvariant(),
            PhoneNumber = request.PhoneNumber!.Trim(),
            EventStartDate = request.EventStartDate!,
            EventEndDate = request.EventEndDate!,
            EventScheduleJson = SerializeSchedule(request.EventDays),
            Venue = request.Venue!.Trim(),
            Room = request.Room!.Trim(),
            Attendees = request.Attendees!.Trim(),
            CreatedUtc = DateTime.UtcNow
        };

        _db.WestinLeads.Add(lead);
        await _db.SaveChangesAsync(ct);

        var baseUrl = ResolveLeadChatBaseUrl();
        var chatLink = $"{baseUrl}/Chat?leadId={token}";

        var leadId = lead.Id;
        var syncResult = await _leadFollowUp.SyncLeadToBookingsDbAsync(request, ct);

        // Persist booking number back to the WestinLead record
        if (!string.IsNullOrEmpty(syncResult.BookingNo))
        {
            lead.BookingNo = syncResult.BookingNo;
            await _db.SaveChangesAsync(ct);
        }

        _ = SendLeadNotificationInBackgroundAsync(leadId, chatLink);

        return Ok(new
        {
            success = true,
            chatLink,
            emailQueued = true,
            contactSync = syncResult.ContactAction,
            orgSync = syncResult.OrgAction,
            bookingNo = syncResult.BookingNo,
            bookingSync = syncResult.BookingAction,
        });

        async Task SendLeadNotificationInBackgroundAsync(int id, string link)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var followUp = scope.ServiceProvider.GetRequiredService<ILeadSubmissionFollowUp>();
                await followUp.SendLeadNotificationAsync(id, link, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background lead notification failed for WestinLead {LeadId}", id);
            }
        }
    }

    [HttpGet("organizations/search")]
    public async Task<IActionResult> SearchOrganizations([FromQuery] string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
            return BadRequest(new { error = "Query must be at least 3 characters." });

        var results = await _orgService.SearchOrganisationsAsync(q.Trim(), 15, ct);
        return Ok(results);
    }

    private string ResolveLeadChatBaseUrl()
    {
        var opts = _leadEmailOptions.Value;
        // Public links in customer emails: prefer explicit ChatBaseUrl whenever set (even when API runs locally).
        var configured = opts.ChatBaseUrl?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured.TrimEnd('/');

        if (opts.LocalDevelopment)
        {
            var local = opts.LocalChatBaseUrl?.Trim();
            return string.IsNullOrEmpty(local) ? "http://localhost:5216" : local.TrimEnd('/');
        }

        return $"{Request.Scheme}://{Request.Host}";
    }

    private static List<string> ValidateRequest(CreateLeadRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Organisation)) errors.Add("Organisation is required.");
        if (string.IsNullOrWhiteSpace(r.OrganisationAddress)) errors.Add("Organisation address is required.");
        if (string.IsNullOrWhiteSpace(r.FirstName)) errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(r.LastName)) errors.Add("Last name is required.");
        if (string.IsNullOrWhiteSpace(r.Email)) errors.Add("Email is required.");
        else if (!IsValidEmail(r.Email)) errors.Add("Invalid email format.");
        if (string.IsNullOrWhiteSpace(r.PhoneNumber)) errors.Add("Phone number is required.");

        DateOnly? startDateParsed = null;
        DateOnly? endDateParsed = null;
        if (string.IsNullOrWhiteSpace(r.EventStartDate)) errors.Add("Event start date is required.");
        else if (!DateOnly.TryParseExact(r.EventStartDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
            errors.Add("Event start date must be YYYY-MM-DD.");
        else startDateParsed = sd;

        if (string.IsNullOrWhiteSpace(r.EventEndDate)) errors.Add("Event end date is required.");
        else if (!DateOnly.TryParseExact(r.EventEndDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed))
            errors.Add("Event end date must be YYYY-MM-DD.");
        else endDateParsed = ed;

        if (startDateParsed.HasValue && endDateParsed.HasValue)
            ValidateEventDays(r.EventDays, startDateParsed.Value, endDateParsed.Value, errors);

        if (string.IsNullOrWhiteSpace(r.Venue)) errors.Add("Venue is required.");
        if (string.IsNullOrWhiteSpace(r.Room)) errors.Add("Room is required.");
        if (string.IsNullOrWhiteSpace(r.Attendees)) errors.Add("Attendees is required.");
        else if (!int.TryParse(r.Attendees, out var a) || a < 1) errors.Add("Attendees must be a positive number.");
        return errors;
    }

    private static void ValidateEventDays(
        List<EventDayInput>? days,
        DateOnly expectedStart,
        DateOnly expectedEnd,
        List<string> errors)
    {
        if (days == null || days.Count == 0)
        {
            errors.Add("Event schedule is required.");
            return;
        }

        var expectedDates = new List<DateOnly>();
        for (var d = expectedStart; d <= expectedEnd; d = d.AddDays(1))
            expectedDates.Add(d);

        if (days.Count != expectedDates.Count)
        {
            errors.Add($"Event schedule must include {expectedDates.Count} day(s) between start and end date.");
            return;
        }

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            if (!DateOnly.TryParseExact(day.Date?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                errors.Add($"Event day #{i + 1} has an invalid date.");
                continue;
            }
            if (parsedDate != expectedDates[i])
            {
                errors.Add($"Event day #{i + 1} must be {expectedDates[i]:yyyy-MM-dd}.");
                continue;
            }
            if (!IsValidHhMm(day.StartTime))
            {
                errors.Add($"Event day #{i + 1} start time must be HH:mm.");
                continue;
            }
            if (!IsValidHhMm(day.EndTime))
            {
                errors.Add($"Event day #{i + 1} end time must be HH:mm.");
                continue;
            }
            if (string.CompareOrdinal(day.EndTime!.Trim(), day.StartTime!.Trim()) <= 0)
            {
                errors.Add($"Event day #{i + 1} end time must be after start time.");
            }
        }
    }

    private static bool IsValidHhMm(string? s)
        => !string.IsNullOrWhiteSpace(s)
           && System.Text.RegularExpressions.Regex.IsMatch(s.Trim(), @"^([01]\d|2[0-3]):[0-5]\d$");

    private static string? SerializeSchedule(List<EventDayInput>? days)
    {
        if (days == null || days.Count == 0) return null;
        var normalized = days
            .Where(d => !string.IsNullOrWhiteSpace(d.Date))
            .Select(d => new
            {
                date = d.Date!.Trim(),
                startTime = d.StartTime?.Trim() ?? "",
                endTime = d.EndTime?.Trim() ?? "",
            })
            .ToList();
        return JsonSerializer.Serialize(normalized);
    }

    private static bool IsValidEmail(string email) =>
        email.Length <= 200 && System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$");
}
