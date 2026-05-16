using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ItemSerialConfiguration : IEntityTypeConfiguration<ItemSerial>
{
    public void Configure(EntityTypeBuilder<ItemSerial> b)
    {
        b.ToTable("item_serials", schema: "master");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(s => s.SerialNumber)
            .HasColumnName("serial_number")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        b.Property(s => s.LotId).HasColumnName("lot_id");
        b.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(s => s.CurrentLocationId).HasColumnName("current_location_id");
        b.Property(s => s.CurrentCustomerId).HasColumnName("current_customer_id");
        b.Property(s => s.SoldOnSalesInvoiceId).HasColumnName("sold_on_sales_invoice_id");
        b.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(s => s.LastStatusChangeAtUtc)
            .HasColumnName("last_status_change_at_utc")
            .HasColumnType("datetime2(3)");

        b.HasIndex(s => new { s.ItemId, s.SerialNumber })
            .IsUnique()
            .HasDatabaseName("ux_item_serials_item_serial");
        b.HasIndex(s => s.Status).HasDatabaseName("ix_item_serials_status");
    }
}
