using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class WhtCategoryConfiguration : IEntityTypeConfiguration<WhtCategory>
{
    public void Configure(EntityTypeBuilder<WhtCategory> b)
    {
        b.ToTable("wht_categories", schema: "tax");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(c => c.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.Property(c => c.RatePercent)
            .HasColumnName("rate_percent")
            .HasColumnType("decimal(5,2)")
            .IsRequired();
        b.Property(c => c.EffectiveFromDate)
            .HasColumnName("effective_from_date")
            .HasColumnType("date")
            .IsRequired();
        b.Property(c => c.EffectiveToDate).HasColumnName("effective_to_date").HasColumnType("date");
        b.Property(c => c.ApplicableTo)
            .HasColumnName("applicable_to")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        b.ComplexProperty(
            c => c.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(100).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(100).IsRequired();
            }
        );

        // Lookup by code + payment date is the dominant query
        // (R-17 selects the row in force on the payment date).
        // Code isn't unique because the operator supersedes a row
        // by inserting a new one with a later effective_from.
        b.HasIndex(c => c.Code).HasDatabaseName("ix_wht_categories_code");
        b.HasIndex(c => new { c.Code, c.EffectiveFromDate })
            .HasDatabaseName("ix_wht_categories_code_effective_from");
    }
}
