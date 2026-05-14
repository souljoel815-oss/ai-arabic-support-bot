using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> b)
    {
        b.ToTable("stock_adjustments", schema: "master_data");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
        b.Property(x => x.CountedAtUtc)
            .HasColumnName("counted_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(x => x.CountedByUserId).HasColumnName("counted_by_user_id").IsRequired();
        b.Property(x => x.SystemCount).HasColumnName("system_count")
            .HasColumnType("decimal(19,4)").IsRequired();
        b.Property(x => x.ActualCount).HasColumnName("actual_count")
            .HasColumnType("decimal(19,4)").IsRequired();
        b.Property(x => x.Delta).HasColumnName("delta")
            .HasColumnType("decimal(19,4)").IsRequired();
        b.Property(x => x.Reason).HasColumnName("reason")
            .HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);

        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StockLocation>().WithMany().HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.LocationId, x.CountedAtUtc })
            .HasDatabaseName("ix_stock_adjustments_location_counted");
        b.HasIndex(x => new { x.ItemId, x.CountedAtUtc })
            .HasDatabaseName("ix_stock_adjustments_item_counted");
    }
}
