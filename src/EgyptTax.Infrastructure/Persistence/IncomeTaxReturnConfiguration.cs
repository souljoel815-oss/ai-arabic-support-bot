using EgyptTax.Domain.Tax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class IncomeTaxReturnConfiguration : IEntityTypeConfiguration<IncomeTaxReturn>
{
    public void Configure(EntityTypeBuilder<IncomeTaxReturn> b)
    {
        b.ToTable("income_tax_returns", schema: "tax");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(r => r.Regime)
            .HasColumnName("regime")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        b.Property(r => r.FiscalYear).HasColumnName("fiscal_year").IsRequired();
        b.Property(r => r.PeriodStart).HasColumnName("period_start").HasColumnType("date").IsRequired();
        b.Property(r => r.PeriodEnd).HasColumnName("period_end").HasColumnType("date").IsRequired();

        b.ComplexProperty(r => r.Revenue, p =>
            p.Property(x => x.Amount).HasColumnName("revenue").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.DeductibleExpenses, p =>
            p.Property(x => x.Amount).HasColumnName("deductible_expenses").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.NonDeductibleAdjustments, p =>
            p.Property(x => x.Amount).HasColumnName("non_deductible_adjustments").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.ManagementProfitLoss, p =>
            p.Property(x => x.Amount).HasColumnName("management_pl").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.TaxableIncome, p =>
            p.Property(x => x.Amount).HasColumnName("taxable_income").HasColumnType("decimal(19,2)").IsRequired());
        b.ComplexProperty(r => r.TaxDue, p =>
            p.Property(x => x.Amount).HasColumnName("tax_due").HasColumnType("decimal(19,2)").IsRequired());

        b.Property(r => r.EffectiveRatePercent)
            .HasColumnName("effective_rate_percent")
            .HasColumnType("decimal(5,2)")
            .IsRequired();

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

        // Drives the list page sort (most-recent fiscal year first)
        // and the per-(regime,year) lookup at generation time.
        b.HasIndex(r => new { r.Regime, r.FiscalYear })
            .HasDatabaseName("ix_income_tax_returns_period");
    }
}
