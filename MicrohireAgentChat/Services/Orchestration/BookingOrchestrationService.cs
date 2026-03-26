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
    private readonly ContactResolutionService _contactResolution;
    private readonly BookingPersistenceService _bookingService;
    private readonly ItemPersistenceService _itemService;
    private readonly CrewPersistenceService _crewService;
    private readonly ILogger<BookingOrchestrationService> _logger;

    public BookingOrchestrationService(
        BookingDbContext db,
        ConversationExtractionService extractor,
        ContactResolutionService contactResolution,
        BookingPersistenceService bookingService,
        ItemPersistenceService itemService,
        CrewPersistenceService crewService,
        ILogger<BookingOrchestrationService> logger)
    {
        _db = db;
        _extractor = extractor;
        _contactResolution = contactResolution;
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

            // 2, 3, 4. Resolve Contact and Organization with Jenny's "no-update" rule
            var (contactId, orgId, customerCode) = await ResolveContactAndOrganizationAsync(
                contactInfo.Name,
                contactInfo.Email,
                contactInfo.PhoneE164,
                contactInfo.Position,
                orgName,
                orgAddress,
                ct);

            result.ContactId = contactId;
            result.OrganizationId = orgId;
            result.CustomerCode = customerCode;

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

            var (contactId, orgId, _) = await ResolveContactAndOrganizationAsync(
                contactInfo.Name,
                contactInfo.Email,
                contactInfo.PhoneE164,
                contactInfo.Position,
                orgName,
                orgAddress,
                ct);

            await transaction.CommitAsync(ct);
            return (contactId, orgId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save contact and organization");
            return (null, null);
        }
    }

    /// <summary>
    /// Shared logic to resolve contact and organization following Jenny's "no-update" rule.
    /// Reuse existing contact only if already linked to the organization. Otherwise, create new.
    /// Reuse existing organization without updating fields.
    /// </summary>
    private async Task<(decimal? contactId, decimal? orgId, string? customerCode)> ResolveContactAndOrganizationAsync(
        string? contactName,
        string? contactEmail,
        string? contactPhone,
        string? contactPosition,
        string? orgName,
        string? orgAddress,
        CancellationToken ct)
    {
        var res = await _contactResolution.ResolveAsync(
            contactName,
            contactEmail,
            contactPhone,
            contactPosition,
            orgName,
            orgAddress,
            ct,
            leadAuthoritative: false);

        return (res.contactId, res.orgId, res.customerCode);
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

