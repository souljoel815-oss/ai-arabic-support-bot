using EgyptTax.Domain.Eta;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class EtaSubmissionConfiguration : IEntityTypeConfiguration<EtaSubmission>
{
    public void Configure(EntityTypeBuilder<EtaSubmission> b)
    {
        b.ToTable("eta_submissions", schema: "eta");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.SalesInvoiceId).HasColumnName("sales_invoice_id").IsRequired();
        // 1:1 with SalesInvoice (per data-model C1) so the FK is also a
        // unique key. We do not declare a SQL FK here to keep the
        // T083 perf-test seed simple (the seeder bulk-inserts both
        // tables; a FK would force an ordering constraint we don't
        // need at the storage layer because the unique index on
        // sales_invoice_id is sufficient to enforce 1:1).
        b.HasIndex(s => s.SalesInvoiceId)
            .IsUnique()
            .HasDatabaseName("ux_eta_submissions_sales_invoice_id");

        b.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(s => s.SubmissionUuid)
            .HasColumnName("submission_uuid")
            .HasMaxLength(64)
            .IsUnicode(false);
        b.Property(s => s.LastAttemptAtUtc)
            .HasColumnName("last_attempt_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(s => s.AttemptCount).HasColumnName("attempt_count").IsRequired();
        b.Property(s => s.ErrorCode).HasColumnName("error_code").HasMaxLength(64).IsUnicode(false);
        b.Property(s => s.ErrorMessage).HasColumnName("error_message").HasMaxLength(1024);

        // P1.3 — regulator-side acknowledgement / rejection (populated
        // by the status-polling job, distinct from transport-level
        // submission state above).
        b.Property(s => s.RegulatorLongUuid)
            .HasColumnName("regulator_long_uuid")
            .HasMaxLength(64)
            .IsUnicode(false);
        b.Property(s => s.RegulatorAcknowledgedAtUtc)
            .HasColumnName("regulator_acknowledged_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(s => s.RegulatorRejectedAtUtc)
            .HasColumnName("regulator_rejected_at_utc")
            .HasColumnType("datetime2(3)");

        b.Property(s => s.SubmissionWindowExpiresAtUtc)
            .HasColumnName("submission_window_expires_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        b.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        // SC-012 covering index — the dashboard's "upcoming deadlines"
        // query filters on (status, submission_window_expires_at_utc).
        // Putting status first lets the index seek to the non-Submitted
        // partition, then a range scan on the deadline column. Including
        // attempt_count + sales_invoice_id makes this a covering index
        // for the dashboard projection so the query never hits the heap.
        b.HasIndex(s => new { s.Status, s.SubmissionWindowExpiresAtUtc })
            .HasDatabaseName("ix_eta_submissions_dashboard")
            .IncludeProperties(s => new
            {
                s.SalesInvoiceId,
                s.AttemptCount,
                s.ErrorCode,
            });
    }
}
