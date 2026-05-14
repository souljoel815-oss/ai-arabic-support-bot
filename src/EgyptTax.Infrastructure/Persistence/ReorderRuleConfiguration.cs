using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class ReorderRuleConfiguration : IEntityTypeConfiguration<ReorderRule>
{
    public void Configure(EntityTypeBuilder<ReorderRule> b)
    {
        b.ToTable("reorder_rules", schema: "master_data");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(r => r.ItemId).HasColumnName("item_id").IsRequired();
        b.Property(r => r.MinQuantity).HasColumnName("min_quantity")
            .HasColumnType("decimal(19,4)").IsRequired();
        b.Property(r => r.TargetQuantity).HasColumnName("target_quantity")
            .HasColumnType("decimal(19,4)").IsRequired();
        b.Property(r => r.PreferredSupplierId).HasColumnName("preferred_supplier_id");
        b.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();

        // One rule per item — enforced via unique index.
        b.HasIndex(r => r.ItemId).IsUnique().HasDatabaseName("ux_reorder_rules_item_id");
    }
}
