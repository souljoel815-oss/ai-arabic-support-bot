using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ItemLotConfiguration : IEntityTypeConfiguration<ItemLot>
{
    public void Configure(EntityTypeBuilder<ItemLot> b)
    {
        b.ToTable("item_lots", schema: "master_data");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(l => l.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(l => l.LotCode).HasColumnName("lot_code")
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        b.Property(l => l.ExpiryDate).HasColumnName("expiry_date").HasColumnType("date");
        b.Property(l => l.QuantityOnHand).HasColumnName("quantity_on_hand")
            .HasColumnType("decimal(19,4)").IsRequired();
        b.Property(l => l.ReceivedAtUtc).HasColumnName("received_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(l => l.SupplierReference).HasColumnName("supplier_reference")
            .HasMaxLength(200);

        // (item × lot_code) is unique — receiving more of the
        // same lot increments the existing row instead of inserting
        // a duplicate (caller is responsible for the upsert).
        b.HasIndex(l => new { l.ItemId, l.LotCode })
            .IsUnique()
            .HasDatabaseName("ux_item_lots_item_lot_code");

        // Index on expiry to keep the dashboard "expiring soon"
        // query cheap on installs with thousands of lots.
        b.HasIndex(l => l.ExpiryDate).HasDatabaseName("ix_item_lots_expiry_date");
    }
}
