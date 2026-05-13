using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.ToTable("items", schema: "master");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(i => i.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.HasIndex(i => i.Code).IsUnique().HasDatabaseName("ux_items_code");

        b.ComplexProperty(
            i => i.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
            }
        );

        b.Property(i => i.DefaultVatCategoryId)
            .HasColumnName("default_vat_category_id")
            .IsRequired();
        b.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(i => i.QuantityOnHand)
            .HasColumnName("quantity_on_hand")
            .HasColumnType("decimal(19,3)")
            .IsRequired();
        b.Property(i => i.LowStockThreshold)
            .HasColumnName("low_stock_threshold")
            .HasColumnType("decimal(19,3)");
        b.Property(i => i.EtaItemCode)
            .HasColumnName("eta_item_code")
            .HasMaxLength(32)
            .IsUnicode(false);

        // P1.6 — code-registration lifecycle (None → PendingX → Active/Failed).
        b.Property(i => i.EtaCodeKind)
            .HasColumnName("eta_code_kind")
            .HasConversion<string?>()
            .HasMaxLength(8)
            .IsUnicode(false);
        b.Property(i => i.EtaCodeStatus)
            .HasColumnName("eta_code_status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        b.Property(i => i.EtaCodeRequestedAtUtc)
            .HasColumnName("eta_code_requested_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(i => i.EtaCodeActivatedAtUtc)
            .HasColumnName("eta_code_activated_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(i => i.EtaCodeFailureReason)
            .HasColumnName("eta_code_failure_reason")
            .HasMaxLength(256);

        // The check job filters by EtaCodeStatus + EtaCodeRequestedAtUtc;
        // index keeps the periodic scan cheap.
        b.HasIndex(i => new { i.EtaCodeStatus, i.EtaCodeRequestedAtUtc })
            .HasDatabaseName("ix_items_eta_code_pending");
    }
}
