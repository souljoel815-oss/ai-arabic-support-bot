using EgyptTax.Domain.Tax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class Form41FilingConfiguration : IEntityTypeConfiguration<Form41Filing>
{
    public void Configure(EntityTypeBuilder<Form41Filing> b)
    {
        b.ToTable("form41_filings", schema: "tax");
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(f => f.FiscalYear).HasColumnName("fiscal_year").IsRequired();
        b.Property(f => f.Quarter).HasColumnName("quarter").IsRequired();
        b.Property(f => f.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(f => f.GeneratedAtUtc)
            .HasColumnName("generated_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(f => f.FiledAtUtc).HasColumnName("filed_at_utc").HasColumnType("datetime2(3)");
        b.Property(f => f.FiledByUserId).HasColumnName("filed_by_user_id");
        b.Property(f => f.PdfPath).HasColumnName("pdf_path").HasMaxLength(500).IsRequired();
        b.Property(f => f.StructuredJsonPath)
            .HasColumnName("structured_json_path")
            .HasMaxLength(500)
            .IsRequired();
        b.Property(f => f.LineCount).HasColumnName("line_count").IsRequired();

        b.ComplexProperty(
            f => f.TotalWhtPayable,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("total_wht_payable")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        // (FiscalYear, Quarter) MUST be unique (one canonical filing
        // per quarter per FR-046). The lifecycle-dashboard query
        // hits this index hard.
        b.HasIndex(f => new { f.FiscalYear, f.Quarter })
            .IsUnique()
            .HasDatabaseName("ux_form41_filings_fiscal_year_quarter");
        b.HasIndex(f => f.Status).HasDatabaseName("ix_form41_filings_status");
    }
}
