using EgyptTax.Portal.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Portal.Infrastructure.Persistence.Configurations;

/// <summary>T025 per data-model.md §8.</summary>
internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("audit_log_entries");

        b.HasKey(x => x.Id);

        b.Property(x => x.ActorDisplayNameSnapshot).IsRequired().HasMaxLength(128);
        b.Property(x => x.Verb).IsRequired().HasMaxLength(64);
        b.Property(x => x.SubjectKind).IsRequired().HasMaxLength(32);
        b.Property(x => x.SubjectId).IsRequired().HasMaxLength(64);
        b.Property(x => x.PayloadJson);
        b.Property(x => x.OriginatingIp).IsRequired().HasMaxLength(45); // IPv6 max

        b.HasIndex(x => new { x.OrganisationId, x.OccurredAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_audit_log_entries_org_occurred_desc");

        b.HasIndex(x => x.Verb);
    }
}
