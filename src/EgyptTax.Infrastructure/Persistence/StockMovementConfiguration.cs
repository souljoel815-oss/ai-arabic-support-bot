using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movements", schema: "master");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(m => m.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(m => m.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();
        b.Property(m => m.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("decimal(19,3)")
            .IsRequired();
        b.Property(m => m.QuantityOnHandAfter)
            .HasColumnName("quantity_on_hand_after")
            .HasColumnType("decimal(19,3)")
            .IsRequired();
        b.Property(m => m.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        b.Property(m => m.SourceDocumentId).HasColumnName("source_document_id");
        b.Property(m => m.Note).HasColumnName("note").HasMaxLength(500);
        b.Property(m => m.CreatedByUserId).HasColumnName("created_by_user_id");

        b.HasOne<Item>()
            .WithMany()
            .HasForeignKey(m => m.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(m => new { m.ItemId, m.OccurredAtUtc })
            .HasDatabaseName("ix_stock_movements_item_occurred");
    }
}
