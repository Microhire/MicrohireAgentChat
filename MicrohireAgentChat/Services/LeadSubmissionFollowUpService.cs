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
    private readonly ContactPersistenceService _contacts;
    private readonly OrganizationPersistenceService _orgs;
    private readonly ILeadEmailService _emailService;
    private readonly ILogger<LeadSubmissionFollowUpService> _logger;

    public LeadSubmissionFollowUpService(
        AppDbContext db,
        ContactPersistenceService contacts,
        OrganizationPersistenceService orgs,
        ILeadEmailService emailService,
        ILogger<LeadSubmissionFollowUpService> logger)
    {
        _db = db;
        _contacts = contacts;
        _orgs = orgs;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<LeadBookingsSyncResult> SyncLeadToBookingsDbAsync(CreateLeadRequest request, CancellationToken ct)
    {
        try
        {
            var fullName = $"{request.FirstName!.Trim()} {request.LastName!.Trim()}".Trim();
            var contactRes = await _contacts.UpsertContactAsync(
                fullName,
                request.Email!.Trim(),
                request.PhoneNumber!.Trim(),
                position: null,
                ct);

            if (!contactRes.Id.HasValue || contactRes.Id.Value <= 0)
            {
                _logger.LogWarning(
                    "BookingsDb sync skipped: contact action {Action} for email {Email}",
                    contactRes.Action,
                    request.Email);
                return new LeadBookingsSyncResult
                {
                    ContactAction = contactRes.Action,
                    OrgAction = "skipped",
                };
            }

            var orgRes = await _orgs.UpsertOrganisationAsync(
                request.Organisation!.Trim(),
                request.OrganisationAddress!.Trim(),
                contactRes.Id,
                ct,
                leadAuthoritative: true);

            if (!orgRes.Id.HasValue)
            {
                return new LeadBookingsSyncResult
                {
                    ContactAction = contactRes.Action,
                    OrgAction = orgRes.Action,
                };
            }

            var code = await _orgs.GetCustomerCodeByIdAsync(orgRes.Id.Value, ct);
            if (!string.IsNullOrWhiteSpace(code))
                await _orgs.LinkContactToOrganisationAsync(code, contactRes.Id.Value, ct);

            _logger.LogInformation(
                "BookingsDb lead sync: contact {ContactAction}, org {OrgAction} for {Email}",
                contactRes.Action,
                orgRes.Action,
                request.Email);

            return new LeadBookingsSyncResult
            {
                ContactAction = contactRes.Action,
                OrgAction = orgRes.Action,
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
