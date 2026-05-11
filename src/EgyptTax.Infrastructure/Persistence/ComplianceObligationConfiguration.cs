using EgyptTax.Domain.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ComplianceObligationConfiguration : IEntityTypeConfiguration<ComplianceObligation>
{
    public void Configure(EntityTypeBuilder<ComplianceObligation> b)
    {
        b.ToTable("compliance_obligations", schema: "workflow");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(o => o.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.Property(o => o.PeriodYear).HasColumnName("period_year").IsRequired();
        b.Property(o => o.PeriodOrdinal).HasColumnName("period_ordinal").IsRequired();
        b.Property(o => o.PeriodStart).HasColumnName("period_start").HasColumnType("date").IsRequired();
        b.Property(o => o.PeriodEnd).HasColumnName("period_end").HasColumnType("date").IsRequired();
        b.Property(o => o.DueDate).HasColumnName("due_date").HasColumnType("date").IsRequired();
        b.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        b.Property(o => o.FiledAtUtc).HasColumnName("filed_at_utc").HasColumnType("datetime2(3)");
        b.Property(o => o.FilingReference).HasColumnName("filing_reference").HasMaxLength(128);
        b.Property(o => o.ProofAttachmentId).HasColumnName("proof_attachment_id");

        // Idempotency on the natural key — generator never produces a
        // duplicate per (Kind, PeriodYear, PeriodOrdinal).
        b.HasIndex(o => new { o.Kind, o.PeriodYear, o.PeriodOrdinal })
            .IsUnique()
            .HasDatabaseName("ux_compliance_obligations_natural_key");

        // Dashboard ordering: by due date ascending.
        b.HasIndex(o => new { o.Status, o.DueDate })
            .HasDatabaseName("ix_compliance_obligations_status_due");
    }
}
