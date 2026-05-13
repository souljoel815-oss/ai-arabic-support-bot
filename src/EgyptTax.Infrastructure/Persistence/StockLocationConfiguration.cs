using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> b)
    {
        b.ToTable("stock_locations", schema: "master_data");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(32)
            .IsUnicode(false).IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_stock_locations_code");

        b.ComplexProperty(x => x.Name, n =>
        {
            n.Property(p => p.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            n.Property(p => p.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        });

        b.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
    }
}

internal sealed class ItemStockByLocationConfiguration : IEntityTypeConfiguration<ItemStockByLocation>
{
    public void Configure(EntityTypeBuilder<ItemStockByLocation> b)
    {
        b.ToTable("item_stock_by_location", schema: "master_data");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        b.Property(x => x.Quantity).HasColumnName("quantity")
            .HasColumnType("decimal(19,4)").IsRequired();

        b.HasIndex(x => new { x.ItemId, x.LocationId })
            .IsUnique()
            .HasDatabaseName("ux_item_stock_by_location_item_location");
    }
}
