using System.Globalization;
using MicrohireAgentChat.Config;
using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MicrohireAgentChat.Services;

public interface ILeadSubmissionFollowUp
{
    /// <summary>Upsert contact + organisation in BookingsDb (RetailOps), then create a light pencil booking. Call from HTTP request when response flags are needed.</summary>
    Task<LeadBookingsSyncResult> SyncLeadToBookingsDbAsync(CreateLeadRequest request, CancellationToken ct);

    /// <summary>Send lead notification email only (run in background scope).</summary>
    Task SendLeadNotificationAsync(int westinLeadId, string chatLink, CancellationToken ct);
}

public sealed class LeadSubmissionFollowUpService : ILeadSubmissionFollowUp
{
    private readonly AppDbContext _db;
    private readonly BookingDbContext _bookingDb;
    private readonly ContactResolutionService _resolution;
    private readonly BookingPersistenceService _bookingService;
    private readonly OrganizationPersistenceService _orgService;
    private readonly ILeadEmailService _emailService;
    private readonly RentalPointDefaultsOptions _rpDefaults;
    private readonly ILogger<LeadSubmissionFollowUpService> _logger;

    public LeadSubmissionFollowUpService(
        AppDbContext db,
        BookingDbContext bookingDb,
        ContactResolutionService resolution,
        BookingPersistenceService bookingService,
        OrganizationPersistenceService orgService,
        ILeadEmailService emailService,
        IOptions<RentalPointDefaultsOptions> rpDefaults,
        ILogger<LeadSubmissionFollowUpService> logger)
    {
        _db = db;
        _bookingDb = bookingDb;
        _resolution = resolution;
        _bookingService = bookingService;
        _orgService = orgService;
        _emailService = emailService;
        _rpDefaults = rpDefaults.Value;
        _logger = logger;
    }

    public async Task<LeadBookingsSyncResult> SyncLeadToBookingsDbAsync(CreateLeadRequest request, CancellationToken ct)
    {
        string contactAction = "skipped";
        string orgAction = "skipped";
        decimal? contactId = null;
        decimal? orgId = null;
        string? customerCode = null;

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

            contactId = res.contactId;
            orgId = res.orgId;
            customerCode = res.customerCode;
            contactAction = res.contactAction;
            orgAction = res.orgAction;

            if (!contactId.HasValue || contactId.Value <= 0)
            {
                _logger.LogWarning("BookingsDb lead sync: contact resolution failed for {Email}", request.Email);
                return new LeadBookingsSyncResult { ContactAction = "error", OrgAction = orgAction };
            }

            // If an existing org was explicitly selected via autocomplete, prefer it
            if (request.ExistingOrgId.HasValue && request.ExistingOrgId.Value > 0)
            {
                var explicitCode = await _orgService.GetCustomerCodeByIdAsync(request.ExistingOrgId.Value, ct);
                if (!string.IsNullOrWhiteSpace(explicitCode))
                {
                    orgId = request.ExistingOrgId.Value;
                    customerCode = explicitCode;
                    if (orgAction == "created")
                        orgAction = "reused"; // Autocomplete-selected org overrides newly created one
                }
            }

            _logger.LogInformation(
                "BookingsDb lead sync: contact {ContactAction}, org {OrgAction} for {Email}",
                contactAction, orgAction, request.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BookingsDb lead sync failed for email {Email}", request.Email);
            return new LeadBookingsSyncResult { ContactAction = "error", OrgAction = "error" };
        }

        // Create light pencil booking (separate try/catch so org+contact sync result is preserved)
        string? bookingNo = null;
        string bookingAction = "skipped";
        try
        {
            if (orgId.HasValue && !string.IsNullOrWhiteSpace(customerCode))
            {
                bookingNo = await CreateLeadBookingAsync(request, contactId, orgId.Value, customerCode!, ct);
                bookingAction = string.IsNullOrEmpty(bookingNo) ? "error" : "created";
            }
            else
            {
                _logger.LogWarning("Skipping booking creation: orgId={OrgId}, customerCode={CustCode}", orgId, customerCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Booking creation failed for lead {Email}, org/contact sync was successful", request.Email);
            bookingAction = "error";
        }

        return new LeadBookingsSyncResult
        {
            ContactAction = contactAction,
            OrgAction = orgAction,
            BookingNo = bookingNo,
            BookingAction = bookingAction,
        };
    }

    private async Task<string?> CreateLeadBookingAsync(
        CreateLeadRequest request,
        decimal? contactId,
        decimal orgId,
        string customerCode,
        CancellationToken ct)
    {
        var bookingNo = await _bookingService.GenerateNextBookingNoAsync(customerCode, ct);

        if (!DateTime.TryParseExact(request.EventStartDate?.Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
        {
            _logger.LogError("Invalid EventStartDate '{Date}' for booking creation", request.EventStartDate);
            return null;
        }

        if (!DateTime.TryParseExact(request.EventEndDate?.Trim(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            endDate = startDate;
        }

        var venueId = await _bookingService.ResolveVenueIdAsync(request.Venue, ct) ?? 20;

        var contactName = $"{request.FirstName?.Trim()} {request.LastName?.Trim()}".Trim();
        int.TryParse(request.Attendees?.Trim(), out var attendees);

        var booking = new TblBooking
        {
            booking_no = bookingNo,
            order_no = bookingNo,
            EntryDate = DateTime.Now,
            order_date = DateTime.Now,
            booking_type_v32 = 2,       // Quote
            BookingProgressStatus = 1,  // Light pencil
            From_locn = 20,
            Trans_to_locn = 20,
            return_to_locn = 20,
            CustCode = Trunc(customerCode, 30),
            CustID = orgId,
            ContactID = contactId,
            contact_nameV6 = Trunc(contactName, 35),
            OrganizationV6 = Trunc(request.Organisation?.Trim(), 50),
            Salesperson = Trunc(_rpDefaults.Salesperson, 30),
            VenueID = venueId,
            VenueRoom = Trunc(request.Room?.Trim(), 35),
            expAttendees = attendees > 0 ? attendees : (int?)null,
            dDate = startDate,
            rDate = endDate,
            SDate = startDate,
            ShowSDate = startDate,
            ShowEdate = endDate,
            SetDate = startDate,
            RehDate = startDate,
            invoiced = "N",
        };

        _bookingDb.TblBookings.Add(booking);
        await _bookingDb.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Lead booking {BookingNo} created for customer {CustCode} (OrgId={OrgId}, ContactId={ContactId})",
            bookingNo, customerCode, orgId, contactId);

        return bookingNo;
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

    private static string? Trunc(string? s, int len)
        => string.IsNullOrWhiteSpace(s) ? s : (s!.Length <= len ? s : s[..len]);
}
