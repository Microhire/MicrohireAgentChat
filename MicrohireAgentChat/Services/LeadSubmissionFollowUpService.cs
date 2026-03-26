using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Services;

public interface ILeadSubmissionFollowUp
{
    /// <summary>Upsert contact + organisation in BookingsDb (RetailOps). Call from HTTP request when response flags are needed.</summary>
    Task<LeadBookingsSyncResult> SyncLeadToBookingsDbAsync(CreateLeadRequest request, CancellationToken ct);

    /// <summary>Send lead notification email only (run in background scope).</summary>
    Task SendLeadNotificationAsync(int westinLeadId, string chatLink, CancellationToken ct);
}

public sealed class LeadSubmissionFollowUpService : ILeadSubmissionFollowUp
{
    private readonly AppDbContext _db;
    private readonly ContactResolutionService _resolution;
    private readonly ILeadEmailService _emailService;
    private readonly ILogger<LeadSubmissionFollowUpService> _logger;

    public LeadSubmissionFollowUpService(
        AppDbContext db,
        ContactResolutionService resolution,
        ILeadEmailService emailService,
        ILogger<LeadSubmissionFollowUpService> logger)
    {
        _db = db;
        _resolution = resolution;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<LeadBookingsSyncResult> SyncLeadToBookingsDbAsync(CreateLeadRequest request, CancellationToken ct)
    {
        try
        {
            var fullName = $"{request.FirstName!.Trim()} {request.LastName!.Trim()}".Trim();
            
            var res = await _resolution.ResolveAsync(
                fullName,
                request.Email,
                request.PhoneNumber,
                contactPosition: null,
                request.Organisation,
                request.OrganisationAddress,
                ct,
                leadAuthoritative: true);

            if (!res.contactId.HasValue || res.contactId.Value <= 0)
            {
                _logger.LogWarning("BookingsDb lead sync: contact resolution failed for {Email}", request.Email);
                return new LeadBookingsSyncResult { ContactAction = "error", OrgAction = res.orgAction };
            }

            _logger.LogInformation(
                "BookingsDb lead sync: contact {ContactAction}, org {OrgAction} for {Email}",
                res.contactAction,
                res.orgAction,
                request.Email);

            return new LeadBookingsSyncResult
            {
                ContactAction = res.contactAction,
                OrgAction = res.orgAction,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BookingsDb lead sync failed for email {Email}", request.Email);
            return new LeadBookingsSyncResult { ContactAction = "error", OrgAction = "error" };
        }
    }

    public async Task SendLeadNotificationAsync(int westinLeadId, string chatLink, CancellationToken ct)
    {
        var lead = await _db.WestinLeads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == westinLeadId, ct);
        if (lead == null)
        {
            _logger.LogWarning("WestinLead {LeadId} not found for notification email.", westinLeadId);
            return;
        }

        try
        {
            await _emailService.SendLeadNotificationAsync(lead, chatLink, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email for lead {LeadId}. Lead was saved.", westinLeadId);
        }
    }
}
