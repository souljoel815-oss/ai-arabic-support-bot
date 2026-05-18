using EgyptTax.Portal.Infrastructure.Identity;
using EgyptTax.Portal.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Portal.Infrastructure.Persistence;

/// <summary>
/// T017. The portal's DbContext. Composes AspNetCore.Identity's schema
/// (users / roles / claims / tokens) with the portal's commercial-relationship
/// entities (CustomerOrganisation, OrganisationMembership, AuditLogEntry —
/// and in later phases Subscription, Licence, Invoice, SalesLead,
/// SupportTicket+Reply+Attachment, Invitation, DownloadArtifactVersion).
///
/// Auto-discovers every <see cref="IEntityTypeConfiguration{T}"/> in this
/// assembly so the Configurations/ folder is the single source of truth for
/// table names, indexes, and column types — no per-entity OnModelCreating
/// noise here.
///
/// Snake-case column naming convention applied at the end of OnModelCreating
/// matches the existing on-prem product's PostgreSQL schema conventions and
/// the data-model.md table-name examples (<c>customer_organisations</c>,
/// <c>audit_log_entries</c>, etc.). SQL Server accepts these table names
/// without quoting per default collation.
/// </summary>
public sealed class PortalDbContext : IdentityDbContext<PortalUser, PortalRole, Guid>
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options)
        : base(options)
    {
    }

    public DbSet<CustomerOrganisation> CustomerOrganisations => Set<CustomerOrganisation>();

    public DbSet<OrganisationMembership> OrganisationMemberships => Set<OrganisationMembership>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(PortalDbContext).Assembly);

        // Identity table renames — AspNetCore.Identity defaults to
        // AspNetUsers/AspNetRoles/etc., which read awkwardly alongside
        // our snake_case domain tables. Rename them to match the
        // portal's commercial schema look-and-feel.
        builder.Entity<PortalUser>().ToTable("team_members");
        builder.Entity<PortalRole>().ToTable("portal_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("team_member_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("team_member_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("team_member_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("team_member_tokens");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("portal_role_claims");

        // Map every PascalCase property to snake_case column.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
            foreach (var key in entityType.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }
            foreach (var index in entityType.GetIndexes())
            {
                if (index.GetDatabaseName() is { Length: > 0 } existing
                    && !existing.StartsWith("UX_", StringComparison.Ordinal)
                    && !existing.StartsWith("IX_", StringComparison.Ordinal))
                {
                    index.SetDatabaseName(ToSnakeCase(existing));
                }
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new System.Text.StringBuilder(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(input[i - 1]) || (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
