using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using MicrohireAgentChat.Services.Extraction;
using MicrohireAgentChat.Services.Persistence;

namespace MicrohireAgentChat.Services.Orchestration;

/// <summary>
/// Orchestrates the booking creation process by coordinating extraction and persistence services
/// </summary>
public sealed class BookingOrchestrationService
{
    private readonly BookingDbContext _db;
    private readonly ConversationExtractionService _extractor;
    private readonly ContactPersistenceService _contactService;
    private readonly OrganizationPersistenceService _orgService;
    private readonly BookingPersistenceService _bookingService;
    private readonly ItemPersistenceService _itemService;
    private readonly CrewPersistenceService _crewService;
    private readonly ILogger<BookingOrchestrationService> _logger;

    public BookingOrchestrationService(
        BookingDbContext db,
        ConversationExtractionService extractor,
        ContactPersistenceService contactService,
        OrganizationPersistenceService orgService,
        BookingPersistenceService bookingService,
        ItemPersistenceService itemService,
        CrewPersistenceService crewService,
        ILogger<BookingOrchestrationService> logger)
    {
        _db = db;
        _extractor = extractor;
        _contactService = contactService;
        _orgService = orgService;
        _bookingService = bookingService;
        _itemService = itemService;
        _crewService = crewService;
        _logger = logger;
    }

    /// <summary>
    /// Process conversation and create/update booking with related entities
    /// </summary>
    public async Task<BookingResult> ProcessConversationAsync(
        IEnumerable<DisplayMessage> messages,
        string? existingBookingNo,
        CancellationToken ct,
        Dictionary<string, string>? additionalFacts = null)
    {
        var result = new BookingResult();

        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            // 1. Extract all structured data from conversation
            var contactInfo = _extractor.ExtractContactInfo(messages);
            var (orgName, orgAddress) = _extractor.ExtractOrganisationFromTranscript(messages);
            var facts = _extractor.ExtractExpectedFields(messages);

            // Check for multi-day events
            var multiDayDetails = _extractor.ExtractMultiDayEventDetails(messages);
            
            // Merge any additional facts (e.g., equipment from session)
            if (additionalFacts != null)
            {
                foreach (var kvp in additionalFacts)
                {
                    facts[kvp.Key] = kvp.Value;
                }
            }

            _logger.LogInformation("Extracted contact: {Name}, org: {Org}", contactInfo.Name, orgName);

            // 2. Upsert Contact
            // PER GUIDE: Only save contact when we have email OR phone - NOT just name alone (prevents duplicates)
            decimal? contactId = null;
            var hasEmail = !string.IsNullOrWhiteSpace(contactInfo.Email);
            var hasPhone = !string.IsNullOrWhiteSpace(contactInfo.PhoneE164);
            if (!string.IsNullOrWhiteSpace(contactInfo.Name) && (hasEmail || hasPhone))
            {
                try
                {
                    var contactUpsert = await _contactService.UpsertContactAsync(
                        contactInfo.Name,
                        contactInfo.Email,
                        contactInfo.PhoneE164,
                        contactInfo.Position,
                        ct);
                    contactId = contactUpsert.Id;

                    result.ContactId = contactId;
                    _logger.LogInformation("Contact upserted: ID={ContactId} ({Action})", contactId, contactUpsert.Action);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upsert contact: {Name}", contactInfo.Name);
                    result.Errors.Add($"Contact creation failed: {ex.Message}");
                }
            }

            // 3. Upsert Organization
            decimal? orgId = null;
            string? customerCode = null;

            if (!string.IsNullOrWhiteSpace(orgName))
            {
                try
                {
                    // Check if exists first
                    var existing = await _orgService.FindOrganisationAsync(orgName!, ct);
                    if (existing.HasValue)
                    {
                        orgId = existing.Value.Id;
                        customerCode = existing.Value.Code;
                        _logger.LogInformation("Found existing org: {Org} (ID={OrgId})", orgName, orgId);
                    }
                    else
                    {
                        // Create new - IMPORTANT: pass contactId to link contact
                        var orgUpsert = await _orgService.UpsertOrganisationAsync(orgName!, orgAddress, contactId, ct);
                        orgId = orgUpsert.Id;
                        if (orgId.HasValue)
                        {
                            customerCode = await _orgService.GetCustomerCodeByIdAsync(orgId.Value, ct);
                            _logger.LogInformation("Created new org: {Org} (ID={OrgId}) ({Action})", orgName, orgId, orgUpsert.Action);
                        }
                    }

                    result.OrganizationId = orgId;
                    result.CustomerCode = customerCode;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upsert organization: {Org}", orgName);
                    result.Errors.Add($"Organization creation failed: {ex.Message}");
                }
            }

            // 4. Link Contact to Organization
            if (contactId.HasValue && !string.IsNullOrWhiteSpace(customerCode))
            {
                try
                {
                    await _orgService.LinkContactToOrganisationAsync(customerCode!, contactId.Value, ct);
                    _logger.LogInformation("Linked contact {ContactId} to org {CustomerCode}", contactId, customerCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to link contact to org");
                    result.Errors.Add($"Contact-Organization link failed: {ex.Message}");
                }
            }

            // 5. Create/Update Booking
            try
            {
                var bookingNo = await _bookingService.SaveBookingAsync(
                    existingBookingNo,
                    facts,
                    contactId,
                    orgId,
                    customerCode,
                    contactInfo.Name,
                    multiDayDetails,
                    ct);

                result.BookingNo = bookingNo;
                _logger.LogInformation("Booking saved: {BookingNo}", bookingNo);

                // 6. Save Items (equipment) - check for selected_equipment OR equipment_summary
                if (!string.IsNullOrWhiteSpace(bookingNo) && 
                    (facts.ContainsKey("selected_equipment") || facts.ContainsKey("equipment_summary")))
                {
                    try
                    {
                        await _itemService.UpsertItemsFromSummaryAsync(bookingNo!, facts, ct);
                        _logger.LogInformation("Items saved for booking {BookingNo}", bookingNo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save items for booking {BookingNo}", bookingNo);
                        result.Errors.Add($"Item persistence failed: {ex.Message}");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(bookingNo))
                {
                    _logger.LogInformation("No equipment data found for booking {BookingNo}", bookingNo);
                }

                // 7. Save Crew (labor)
                if (!string.IsNullOrWhiteSpace(bookingNo) &&
                    (facts.ContainsKey("selected_labor") || facts.ContainsKey("labor_summary")))
                {
                    try
                    {
                        await _crewService.InsertCrewRowsAsync(bookingNo!, facts, ct);
                        _logger.LogInformation("Crew saved for booking {BookingNo}", bookingNo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save crew for booking {BookingNo}", bookingNo);
                        result.Errors.Add($"Crew persistence failed: {ex.Message}");
                    }
                }

                // 8. Save Transcript
                if (!string.IsNullOrWhiteSpace(bookingNo))
                {
                    try
                    {
                        await _bookingService.SaveFullTranscriptToBooknoteAsync(bookingNo!, messages, ct);
                        _logger.LogInformation("Transcript saved for booking {BookingNo}", bookingNo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save transcript");
                        result.Errors.Add($"Transcript save failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save booking");
                result.Errors.Add($"Booking creation failed: {ex.Message}");
            }

            await transaction.CommitAsync(ct);
            result.Success = result.Errors.Count == 0;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Booking orchestration failed");
            result.Success = false;
            result.Errors.Add($"Orchestration failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Extract and save only contact information (lighter operation)
    /// PER GUIDE: Do NOT save contact until we have email or phone (not just name)
    /// </summary>
    public async Task<(decimal? ContactId, decimal? OrgId)> SaveContactAndOrganizationAsync(
        IEnumerable<DisplayMessage> messages,
        CancellationToken ct)
    {
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var contactInfo = _extractor.ExtractContactInfo(messages);
            var (orgName, orgAddress) = _extractor.ExtractOrganisationFromTranscript(messages);

            decimal? contactId = null;
            decimal? orgId = null;

            // PER GUIDE: Only save contact when we have email OR phone - NOT just name alone
            // This prevents duplicate records when user provides info in stages
            var hasEmail = !string.IsNullOrWhiteSpace(contactInfo.Email);
            var hasPhone = !string.IsNullOrWhiteSpace(contactInfo.PhoneE164);
            
            if (!string.IsNullOrWhiteSpace(contactInfo.Name) && (hasEmail || hasPhone))
            {
                var contactUpsert = await _contactService.UpsertContactAsync(
                    contactInfo.Name,
                    contactInfo.Email,
                    contactInfo.PhoneE164,
                    contactInfo.Position,
                    ct);
                contactId = contactUpsert.Id;
            }

            // Save organization
            if (!string.IsNullOrWhiteSpace(orgName))
            {
                var existing = await _orgService.FindOrganisationAsync(orgName!, ct);
                if (existing.HasValue)
                {
                    orgId = existing.Value.Id;
                }
                else
                {
                    var orgUpsert = await _orgService.UpsertOrganisationAsync(orgName!, orgAddress, contactId, ct);
                    orgId = orgUpsert.Id;
                }
            }

            // Link them
            if (contactId.HasValue && orgId.HasValue)
            {
                var customerCode = await _orgService.GetCustomerCodeByIdAsync(orgId.Value, ct);
                if (!string.IsNullOrWhiteSpace(customerCode))
                {
                    await _orgService.LinkContactToOrganisationAsync(customerCode!, contactId.Value, ct);
                }
            }

            await transaction.CommitAsync(ct);
            return (contactId, orgId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save contact and organization");
            return (null, null);
        }
    }
}

/// <summary>
/// Result of booking orchestration process
/// </summary>
public class BookingResult
{
    public bool Success { get; set; }
    public string? BookingNo { get; set; }
    public decimal? ContactId { get; set; }
    public decimal? OrganizationId { get; set; }
    public string? CustomerCode { get; set; }
    public List<string> Errors { get; set; } = new();
}

