using EgyptTax.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class FixedAssetConfiguration : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> b)
    {
        b.ToTable("fixed_assets", schema: "documents");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(a => a.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.Property(a => a.AssetCategory)
            .HasColumnName("asset_category")
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.InServiceDate)
            .HasColumnName("in_service_date")
            .HasColumnType("date")
            .IsRequired();
        b.Property(a => a.UsefulLifeMonths).HasColumnName("useful_life_months").IsRequired();
        b.Property(a => a.DepreciationMethod)
            .HasColumnName("depreciation_method")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        b.Property(a => a.Convention)
            .HasColumnName("convention")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(a => a.DisposedOn).HasColumnName("disposed_on").HasColumnType("date");

        b.ComplexProperty(
            a => a.Description,
            n =>
            {
                n.Property(x => x.Arabic)
                    .HasColumnName("description_ar")
                    .HasMaxLength(500)
                    .IsRequired();
                n.Property(x => x.English)
                    .HasColumnName("description_en")
                    .HasMaxLength(500)
                    .IsRequired();
            }
        );

        b.ComplexProperty(
            a => a.Cost,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("cost")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );
        b.ComplexProperty(
            a => a.SalvageValue,
            p =>
                p.Property(x => x.Amount)
                    .HasColumnName("salvage_value")
                    .HasColumnType("decimal(19,2)")
                    .IsRequired()
        );

        b.HasIndex(a => a.Code).IsUnique().HasDatabaseName("ux_fixed_assets_code");
        b.HasIndex(a => a.Status).HasDatabaseName("ix_fixed_assets_status");
        b.HasIndex(a => a.InServiceDate).HasDatabaseName("ix_fixed_assets_in_service_date");
    }
}
