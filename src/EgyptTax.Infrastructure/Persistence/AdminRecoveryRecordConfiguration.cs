using EgyptTax.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class AdminRecoveryRecordConfiguration
    : IEntityTypeConfiguration<AdminRecoveryRecord>
{
    public void Configure(EntityTypeBuilder<AdminRecoveryRecord> b)
    {
        b.ToTable("admin_recovery_log", schema: "identity");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(r => r.TargetUserId).HasColumnName("target_user_id").IsRequired();
        b.Property(r => r.TargetEmail)
            .HasColumnName("target_email")
            .HasMaxLength(254)
            .IsUnicode(false)
            .IsRequired();
        b.Property(r => r.RecoveredAtUtc)
            .HasColumnName("recovered_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(r => r.MachineName).HasColumnName("machine_name").HasMaxLength(256).IsRequired();
        b.Property(r => r.OperatorIdentity).HasColumnName("operator_identity").HasMaxLength(256);
        b.Property(r => r.AuditEmittedAtUtc)
            .HasColumnName("audit_emitted_at_utc")
            .HasColumnType("datetime2(3)");

        b.HasIndex(r => r.AuditEmittedAtUtc)
            .HasDatabaseName("ix_admin_recovery_log_audit_pending")
            .HasFilter("[audit_emitted_at_utc] IS NULL");
    }
}
