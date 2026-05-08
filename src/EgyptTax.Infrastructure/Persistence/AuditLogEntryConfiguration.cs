using EgyptTax.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("audit_log", schema: "audit");

        b.HasKey(e => e.Index);
        b.Property(e => e.Index).ValueGeneratedNever().HasColumnName("index");

        b.Property(e => e.TsUtc).HasColumnName("ts_utc").HasColumnType("datetime2(3)").IsRequired();

        b.Property(e => e.ActorUserId).HasColumnName("actor_user_id");

        b.Property(e => e.ActorFirmName).HasColumnName("actor_firm_name").HasMaxLength(200);

        b.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();

        b.Property(e => e.Kind)
            .HasColumnName("kind")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();

        b.Property(e => e.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        b.Property(e => e.PrevHash)
            .HasColumnName("prev_hash")
            .HasColumnType("binary(32)")
            .IsRequired();

        b.Property(e => e.ThisHash)
            .HasColumnName("this_hash")
            .HasColumnType("binary(32)")
            .IsRequired();

        b.HasIndex(e => e.TsUtc).HasDatabaseName("ix_audit_log_ts_utc");
        b.HasIndex(e => new { e.CompanyId, e.TsUtc }).HasDatabaseName("ix_audit_log_company_ts");
    }
}
