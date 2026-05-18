using EgyptTax.Portal.Application.Organisations;

namespace EgyptTax.Portal.Infrastructure.Middleware;

/// <summary>
/// T029. Scoped DI registration of <see cref="IOrganisationContext"/>.
/// One instance per HTTP request.
/// </summary>
internal sealed class OrganisationContext : IOrganisationContext
{
    public bool IsScoped { get; private set; }

    public Guid OrganisationId => IsScoped
        ? _organisationId
        : throw new InvalidOperationException("OrganisationContext is not yet scoped for this request. Did OrganisationScopeMiddleware run?");

    public string Role => IsScoped
        ? _role
        : throw new InvalidOperationException("OrganisationContext is not yet scoped for this request.");

    public Guid TeamMemberId => IsScoped
        ? _teamMemberId
        : throw new InvalidOperationException("OrganisationContext is not yet scoped for this request.");

    public string DisplayName => IsScoped
        ? _displayName
        : throw new InvalidOperationException("OrganisationContext is not yet scoped for this request.");

    private Guid _organisationId;
    private Guid _teamMemberId;
    private string _role = string.Empty;
    private string _displayName = string.Empty;

    public void SetScope(Guid organisationId, Guid teamMemberId, string role, string displayName)
    {
        if (organisationId == Guid.Empty) throw new ArgumentException("OrganisationId must be non-empty.", nameof(organisationId));
        if (teamMemberId == Guid.Empty) throw new ArgumentException("TeamMemberId must be non-empty.", nameof(teamMemberId));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role must be non-empty.", nameof(role));

        _organisationId = organisationId;
        _teamMemberId = teamMemberId;
        _role = role;
        _displayName = displayName ?? string.Empty;
        IsScoped = true;
    }
}
