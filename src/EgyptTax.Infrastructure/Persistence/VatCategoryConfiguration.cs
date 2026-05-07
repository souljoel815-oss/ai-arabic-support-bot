using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class VatCategoryConfiguration : IEntityTypeConfiguration<VatCategory>
{
    public void Configure(EntityTypeBuilder<VatCategory> b)
    {
        b.ToTable("vat_categories", schema: "tax");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(v => v.Code).HasColumnName("code").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.HasIndex(v => new { v.Code, v.EffectiveFromDate })
            .IsUnique()
            .HasDatabaseName("ux_vat_categories_code_effective_from");

        b.ComplexProperty(v => v.Name, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(100).IsRequired();
            n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(100).IsRequired();
        });

        b.Property(v => v.RatePercent).HasColumnName("rate_percent").HasColumnType("decimal(5,2)").IsRequired();
        b.Property(v => v.EffectiveFromDate).HasColumnName("effective_from_date").HasColumnType("date").IsRequired();
        b.Property(v => v.EffectiveToDate).HasColumnName("effective_to_date").HasColumnType("date");
        b.Property(v => v.RecoverableInputVat).HasColumnName("recoverable_input_vat").IsRequired();
    }
}
