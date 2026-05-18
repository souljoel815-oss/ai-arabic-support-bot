namespace EgyptTax.Portal.Application.Organisations;

/// <summary>
/// T029. Per-request organisation scope. Populated by
/// <c>OrganisationScopeMiddleware</c> (Infrastructure) when an
/// authenticated request resolves to a valid OrganisationMembership;
/// every Application handler that queries org-scoped data MUST consult
/// this rather than trust a URL/query parameter (FR-021 — prevents
/// URL-tampering escape from org A to org B).
/// </summary>
public interface IOrganisationContext
{
    /// <summary>True once <see cref="OrganisationId"/> has been set by middleware.</summary>
    bool IsScoped { get; }

    /// <summary>The active organisation for this request. Throws if not scoped.</summary>
    Guid OrganisationId { get; }

    /// <summary>The signed-in user's role in <see cref="OrganisationId"/>.</summary>
    string Role { get; }

    /// <summary>The TeamMember id (PortalUser.Id) of the signed-in user.</summary>
    Guid TeamMemberId { get; }

    /// <summary>Snapshot of the user's display name — used in audit-log writes.</summary>
    string DisplayName { get; }

    /// <summary>Set by middleware once per request after the membership query resolves.</summary>
    void SetScope(Guid organisationId, Guid teamMemberId, string role, string displayName);
}
