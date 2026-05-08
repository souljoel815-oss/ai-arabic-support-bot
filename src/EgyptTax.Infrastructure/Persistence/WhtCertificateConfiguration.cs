using EgyptTax.Domain.Tax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class WhtCertificateConfiguration : IEntityTypeConfiguration<WhtCertificate>
{
    public void Configure(EntityTypeBuilder<WhtCertificate> b)
    {
        b.ToTable("wht_certificates", schema: "tax");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(c => c.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        b.Property(c => c.Date).HasColumnName("date").HasColumnType("date").IsRequired();
        b.Property(c => c.CounterpartyId).HasColumnName("counterparty_id").IsRequired();
        b.Property(c => c.SourceVoucherId).HasColumnName("source_voucher_id").IsRequired();
        b.Property(c => c.SourceInvoiceId).HasColumnName("source_invoice_id").IsRequired();
        b.Property(c => c.WhtCategoryId).HasColumnName("wht_category_id").IsRequired();
        b.Property(c => c.RateAppliedPercent)
            .HasColumnName("rate_applied_percent")
            .HasColumnType("decimal(5,2)")
            .IsRequired();
        b.Property(c => c.CertificateNumber)
            .HasColumnName("certificate_number")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        b.Property(c => c.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(c => c.IncludedInForm41FilingId).HasColumnName("included_in_form41_filing_id");

        b.ComplexProperty(
            c => c.AmountWithheld,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("amount_withheld")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        // Outbound certificate numbers MUST be unique (we generate
        // them); inbound certificate numbers come from the customer
        // and may collide across different customers, so the
        // uniqueness constraint is filtered by direction.
        b.HasIndex(c => c.CertificateNumber)
            .IsUnique()
            .HasFilter("[direction] = N'OutboundToSupplier'")
            .HasDatabaseName("ux_wht_certificates_outbound_certificate_number");

        b.HasIndex(c => c.SourceVoucherId).HasDatabaseName("ix_wht_certificates_source_voucher_id");
        b.HasIndex(c => c.SourceInvoiceId).HasDatabaseName("ix_wht_certificates_source_invoice_id");
        b.HasIndex(c => c.CounterpartyId).HasDatabaseName("ix_wht_certificates_counterparty_id");
        b.HasIndex(c => c.Date).HasDatabaseName("ix_wht_certificates_date");

        // Form 41 generator filters on this column to skip
        // already-included certs; filtered index keeps the index
        // tiny (most rows have it null until a filing covers them).
        b.HasIndex(c => c.IncludedInForm41FilingId)
            .HasFilter("[included_in_form41_filing_id] IS NOT NULL")
            .HasDatabaseName("ix_wht_certificates_included_in_form41_filing_id");
    }
}
