using MicrohireAgentChat.Data;
using MicrohireAgentChat.Models;
using Microsoft.EntityFrameworkCore;

namespace MicrohireAgentChat.Services.Persistence;

/// <summary>
/// Implements Jenny's "no-update" rule for contact and organization resolution.
/// Rule: Do not change existing contact/company details.
/// Reuse contact ONLY if it exists and is already linked to the organization.
/// Otherwise, create a new contact record.
/// </summary>
public sealed class ContactResolutionService
{
    private readonly ContactPersistenceService _contactService;
    private readonly OrganizationPersistenceService _orgService;
    private readonly ILogger<ContactResolutionService> _logger;

    public ContactResolutionService(
        ContactPersistenceService contactService,
        OrganizationPersistenceService orgService,
        ILogger<ContactResolutionService> logger)
    {
        _contactService = contactService;
        _orgService = orgService;
        _logger = logger;
    }

    /// <summary>
    /// Resolves contact and organization following the "no-update" policy.
    /// </summary>
    public async Task<(decimal? contactId, decimal? orgId, string? customerCode, string contactAction, string orgAction)> ResolveAsync(
        string? contactName,
        string? contactEmail,
        string? contactPhone,
        string? contactPosition,
        string? orgName,
        string? orgAddress,
        CancellationToken ct,
        bool leadAuthoritative = false)
    {
        decimal? resolvedContactId = null;
        decimal? resolvedOrgId = null;
        string? resolvedCustomerCode = null;
        string orgAction = "skipped";
        string contactAction = "skipped";

        // 1. Resolve Organization (No-update rule)
        if (!string.IsNullOrWhiteSpace(orgName))
        {
            var orgRes = await _orgService.UpsertOrganisationAsync(
                orgName.Trim(),
                orgAddress?.Trim(),
                contactId: null,
                ct,
                leadAuthoritative: leadAuthoritative,
                noUpdateExisting: true);

            resolvedOrgId = orgRes.Id;
            orgAction = orgRes.Action;
            if (resolvedOrgId.HasValue)
            {
                resolvedCustomerCode = await _orgService.GetCustomerCodeByIdAsync(resolvedOrgId.Value, ct);
            }
        }

        // 2. Resolve Contact
        var hasEmail = !string.IsNullOrWhiteSpace(contactEmail);
        var hasPhone = !string.IsNullOrWhiteSpace(contactPhone);
        
        if (!string.IsNullOrWhiteSpace(contactName) && (hasEmail || hasPhone))
        {
            // Find existing
            var existingContact = await _contactService.FindExistingContactAsync(
                contactEmail?.Trim(),
                contactPhone?.Trim(),
                contactName.Trim(),
                allowPhoneLookup: true,
                ct);

            if (existingContact != null && resolvedOrgId.HasValue)
            {
                var isLinked = await _orgService.IsContactLinkedToOrganisationAsync(resolvedOrgId.Value, existingContact.Id, ct);
                if (isLinked)
                {
                    // Reuse existing contact - NO UPDATES per Jenny
                    resolvedContactId = existingContact.Id;
                    contactAction = "reused";
                    _logger.LogInformation("Reusing existing linked contact: ID={ContactId}", resolvedContactId);
                }
            }

            if (!resolvedContactId.HasValue)
            {
                // Create brand new contact per Jenny
                var contactRes = await _contactService.CreateNewContactAsync(
                    contactName.Trim(),
                    contactEmail?.Trim(),
                    contactPhone?.Trim(),
                    contactPosition?.Trim(),
                    ct);
                resolvedContactId = contactRes.Id;
                contactAction = "created";
                _logger.LogInformation("Created new contact per no-update rule: ID={ContactId}", resolvedContactId);
            }
        }

        // 3. Ensure Link
        if (resolvedContactId.HasValue && !string.IsNullOrWhiteSpace(resolvedCustomerCode))
        {
            await _orgService.LinkContactToOrganisationAsync(resolvedCustomerCode!, resolvedContactId.Value, ct);
        }

        return (resolvedContactId, resolvedOrgId, resolvedCustomerCode, contactAction, orgAction);
    }
}
