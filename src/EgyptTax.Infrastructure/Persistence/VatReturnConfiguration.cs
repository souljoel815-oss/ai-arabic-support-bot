using EgyptTax.Domain.Tax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class VatReturnConfiguration : IEntityTypeConfiguration<VatReturn>
{
    public void Configure(EntityTypeBuilder<VatReturn> b)
    {
        b.ToTable("vat_returns", schema: "tax");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(r => r.PeriodKind)
            .HasColumnName("period_kind")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        b.Property(r => r.PeriodYear).HasColumnName("period_year").IsRequired();
        b.Property(r => r.PeriodOrdinal).HasColumnName("period_ordinal").IsRequired();
        b.Property(r => r.PeriodStart).HasColumnName("period_start").HasColumnType("date").IsRequired();
        b.Property(r => r.PeriodEnd).HasColumnName("period_end").HasColumnType("date").IsRequired();

        b.ComplexProperty(r => r.OutputVat, p =>
            p.Property(x => x.Amount).HasColumnName("output_vat").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.InputVatRecoverable, p =>
            p.Property(x => x.Amount).HasColumnName("input_vat_recoverable").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.InputVatNonRecoverable, p =>
            p.Property(x => x.Amount).HasColumnName("input_vat_non_recoverable").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.NetPayable, p =>
            p.Property(x => x.Amount).HasColumnName("net_payable").HasColumnType("decimal(19,2)").IsRequired());

        b.Property(r => r.ContributingDocumentCount).HasColumnName("contributing_document_count").IsRequired();

        b.Property(r => r.GeneratedAtUtc).HasColumnName("generated_at_utc").HasColumnType("datetime2(3)").IsRequired();
        b.Property(r => r.GeneratedByUserId).HasColumnName("generated_by_user_id").IsRequired();

        b.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        b.Property(r => r.RegulatorSubmissionReference).HasColumnName("submission_reference").HasMaxLength(100);
        b.Property(r => r.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("datetime2(3)");
        b.Property(r => r.SubmittedByUserId).HasColumnName("submitted_by_user_id");

        b.Property(r => r.RegulatorAcknowledgementReference).HasColumnName("ack_reference").HasMaxLength(100);
        b.Property(r => r.AcknowledgedAtUtc).HasColumnName("ack_at_utc").HasColumnType("datetime2(3)");
        b.Property(r => r.AcknowledgedByUserId).HasColumnName("ack_by_user_id");

        b.Property(r => r.Note).HasColumnName("note").HasMaxLength(2000);

        // Drives the list page sort (most-recent period first) and
        // the per-period uniqueness check at generation time.
        b.HasIndex(r => new { r.PeriodKind, r.PeriodYear, r.PeriodOrdinal })
            .HasDatabaseName("ix_vat_returns_period");
    }
}
