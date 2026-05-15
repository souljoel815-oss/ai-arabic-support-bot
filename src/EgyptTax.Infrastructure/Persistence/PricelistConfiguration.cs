using EgyptTax.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PricelistConfiguration : IEntityTypeConfiguration<Pricelist>
{
    public void Configure(EntityTypeBuilder<Pricelist> b)
    {
        b.ToTable("pricelists", schema: "pricing");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        b.ComplexProperty(p => p.Name, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(120).IsRequired();
            n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(120).IsRequired();
        });

        b.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(p => p.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        b.HasMany(p => p.Rules)
            .WithOne()
            .HasForeignKey(r => r.PricelistId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Navigation(p => p.Rules).Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PricelistRuleConfiguration : IEntityTypeConfiguration<PricelistRule>
{
    public void Configure(EntityTypeBuilder<PricelistRule> b)
    {
        b.ToTable("pricelist_rules", schema: "pricing");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(r => r.PricelistId).HasColumnName("pricelist_id").IsRequired();
        b.Property(r => r.ItemId).HasColumnName("item_id");
        b.Property(r => r.CustomerId).HasColumnName("customer_id");
        b.Property(r => r.DiscountPercent)
            .HasColumnName("discount_percent")
            .HasColumnType("decimal(5,2)");
        b.Property(r => r.FixedPriceEgp)
            .HasColumnName("fixed_price_egp")
            .HasColumnType("decimal(19,2)");
        b.Property(r => r.Sequence).HasColumnName("sequence").IsRequired();

        b.HasIndex(r => r.PricelistId).HasDatabaseName("ix_pricelist_rules_pricelist_id");
        b.HasIndex(r => r.ItemId).HasDatabaseName("ix_pricelist_rules_item_id");
    }
}
