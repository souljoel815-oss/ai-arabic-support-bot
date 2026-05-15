using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class FiscalPositionConfiguration : IEntityTypeConfiguration<FiscalPosition>
{
    public void Configure(EntityTypeBuilder<FiscalPosition> b)
    {
        b.ToTable("fiscal_positions", schema: "master_data");
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        b.ComplexProperty(f => f.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(120).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(120).IsRequired();
            });
        b.Property(f => f.Description).HasColumnName("description").HasMaxLength(2000);
        b.Property(f => f.CountryAutoApply).HasColumnName("country_auto_apply")
            .HasMaxLength(64);
        b.Property(f => f.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(f => f.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();

        b.HasMany(f => f.Mappings)
            .WithOne()
            .HasForeignKey(m => m.FiscalPositionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(FiscalPosition.Mappings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(f => f.IsActive).HasDatabaseName("ix_fiscal_positions_is_active");
    }
}

internal sealed class FiscalPositionMappingConfiguration : IEntityTypeConfiguration<FiscalPositionMapping>
{
    public void Configure(EntityTypeBuilder<FiscalPositionMapping> b)
    {
        b.ToTable("fiscal_position_mappings", schema: "master_data");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(m => m.FiscalPositionId).HasColumnName("fiscal_position_id").IsRequired();
        b.Property(m => m.SourceVatCategoryId).HasColumnName("source_vat_category_id").IsRequired();
        b.Property(m => m.DestinationVatCategoryId).HasColumnName("destination_vat_category_id");
    }
}
