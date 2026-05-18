using Microsoft.AspNetCore.Identity;

namespace EgyptTax.Portal.Infrastructure.Identity;

/// <summary>
/// T019 per data-model.md §2. Placeholder — actual role logic is
/// organisation-scoped via
/// <c>OrganisationMembership.Role</c> (Owner / BillingAdmin / SupportAdmin /
/// ReadOnly). We carry an IdentityRole-derived class only so AspNetCore.Identity's
/// EF Core schema composes correctly; the Identity-level role table stays
/// empty in v1 — every permission decision flows through
/// <c>PortalAuthorizationPolicies</c> reading the active org membership.
/// </summary>
public class PortalRole : IdentityRole<Guid>
{
    public PortalRole() { }

    public PortalRole(string roleName) : base(roleName) { }
}
