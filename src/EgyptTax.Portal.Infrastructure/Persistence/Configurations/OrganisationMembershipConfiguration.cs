using EgyptTax.Portal.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Portal.Infrastructure.Persistence.Configurations;

/// <summary>
/// T025 per data-model.md §2. The filtered unique index lets the same
/// (org, member) pair re-exist after a previous revocation (e.g. an
/// accountant rehired after leaving).
/// </summary>
internal sealed class OrganisationMembershipConfiguration : IEntityTypeConfiguration<OrganisationMembership>
{
    public void Configure(EntityTypeBuilder<OrganisationMembership> b)
    {
        b.ToTable("organisation_memberships");

        b.HasKey(x => x.Id);

        b.Property(x => x.Role).IsRequired().HasMaxLength(16);

        b.HasIndex(x => x.OrganisationId);
        b.HasIndex(x => x.TeamMemberId);

        // Filtered unique: at most one active row per (org, member).
        // Past revoked rows are retained for audit purposes.
        b.HasIndex(x => new { x.OrganisationId, x.TeamMemberId })
            .IsUnique()
            .HasFilter("[revoked_at_utc] IS NULL")
            .HasDatabaseName("UX_organisation_memberships_active_org_member");
    }
}
