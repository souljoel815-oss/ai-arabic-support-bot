namespace EgyptTax.Portal.Infrastructure.Persistence.Entities;

/// <summary>
/// T023 per data-model.md §2 join table. Many-to-many between TeamMember
/// (the AspNetCore.Identity user) and CustomerOrganisation. Cross-org
/// membership is allowed for accounting-firm staff who service multiple
/// client orgs. Filtered unique index on
/// <c>(OrganisationId, TeamMemberId) WHERE RevokedAtUtc IS NULL</c> per
/// T025 — same email can rejoin the same org after being revoked.
/// </summary>
public sealed class OrganisationMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganisationId { get; set; }

    /// <summary>FK to AspNetCore.Identity user (PortalUser.Id).</summary>
    public Guid TeamMemberId { get; set; }

    /// <summary>One of <see cref="OrganisationRole"/>.</summary>
    public string Role { get; set; } = OrganisationRole.Owner;

    public DateTime InvitedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? AcceptedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>FR-022 — bumped on revoke to invalidate Identity security stamps within 5 min.</summary>
    public int SecurityStampVersion { get; set; }
}

/// <summary>
/// Role enum for OrganisationMembership.Role. Stored as a string so EF Core
/// migrations can add new roles without an enum-renumber churn.
/// </summary>
public static class OrganisationRole
{
    public const string Owner = nameof(Owner);
    public const string BillingAdmin = nameof(BillingAdmin);
    public const string SupportAdmin = nameof(SupportAdmin);
    public const string ReadOnly = nameof(ReadOnly);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Owner, BillingAdmin, SupportAdmin, ReadOnly,
    };

    public static bool IsValid(string role) => All.Contains(role);
}
