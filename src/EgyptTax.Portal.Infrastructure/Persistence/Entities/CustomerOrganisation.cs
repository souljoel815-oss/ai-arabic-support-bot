namespace EgyptTax.Portal.Infrastructure.Persistence.Entities;

/// <summary>
/// T022 per data-model.md §1. Top-level account that owns subscriptions,
/// licences, invoices, tickets, and team-member memberships. Created on
/// signup; one per real business customer. Soft-deleted on user request
/// (FR-024); the nightly purge job hard-deletes 30 days later but retains
/// audit-log entries indefinitely.
/// </summary>
public sealed class CustomerOrganisation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string LegalNameAr { get; set; } = string.Empty;

    public string? LegalNameEn { get; set; }

    public string? TaxRegistrationNumber { get; set; }

    public string BillingEmail { get; set; } = string.Empty;

    public string? BillingPhone { get; set; }

    public string? BillingAddressJson { get; set; }

    public string CountryCode { get; set; } = "EG";

    /// <summary>FR-011 org-policy MFA enforcement per T155.</summary>
    public bool RequiresMfaForOwners { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? SoftDeletedAtUtc { get; set; }
}
