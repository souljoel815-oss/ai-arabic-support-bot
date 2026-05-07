using EgyptTax.Domain.Numbering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class DocumentSeriesConfiguration : IEntityTypeConfiguration<DocumentSeries>
{
    public void Configure(EntityTypeBuilder<DocumentSeries> b)
    {
        b.ToTable("document_series", schema: "numbering");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.Code).HasColumnName("code").HasMaxLength(16).IsUnicode(false).IsRequired();
        b.HasIndex(s => s.Code).IsUnique().HasDatabaseName("ux_document_series_code");

        b.Property(s => s.DocumentType)
            .HasColumnName("document_type")
            .HasConversion<string>()
            .HasMaxLength(32);
        b.HasIndex(s => s.DocumentType).IsUnique().HasDatabaseName("ux_document_series_doc_type");

        // ArabicEnglishText is a value-object struct → ComplexProperty
        // (per the existing T024 / T044 pattern). Seeding the bilingual
        // Name happens in the migration via raw SQL because EF Core's
        // HasData doesn't seed complex properties cleanly.
        b.ComplexProperty(s => s.Name, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(100).IsRequired();
            n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(100).IsRequired();
        });
    }
}
