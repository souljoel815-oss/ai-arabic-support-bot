using EgyptTax.Domain.Numbering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class DocumentNumberAllocatorConfiguration : IEntityTypeConfiguration<DocumentNumberAllocator>
{
    public void Configure(EntityTypeBuilder<DocumentNumberAllocator> b)
    {
        b.ToTable("document_number_allocator", schema: "numbering");

        b.HasKey(a => new { a.SeriesId, a.FiscalYear });

        b.Property(a => a.SeriesId).HasColumnName("series_id");
        b.Property(a => a.FiscalYear).HasColumnName("fiscal_year");
        b.Property(a => a.NextNumber).HasColumnName("next_number");

        b.HasOne<DocumentSeries>()
            .WithMany()
            .HasForeignKey(a => a.SeriesId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
