using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("suppliers", schema: "master");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(s => s.Code).HasColumnName("code").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.HasIndex(s => s.Code).IsUnique().HasDatabaseName("ux_suppliers_code");

        b.ComplexProperty(s => s.Name, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        });

        b.ComplexProperty(s => s.Address, a =>
        {
            a.Property(x => x.Arabic).HasColumnName("address_ar").HasMaxLength(500).IsRequired();
            a.Property(x => x.English).HasColumnName("address_en").HasMaxLength(500).IsRequired();
        });

        b.Property(s => s.Phone).HasColumnName("phone").HasMaxLength(32).IsUnicode(false);
        b.Property(s => s.Email).HasColumnName("email").HasMaxLength(254).IsUnicode(false);
        b.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();

        b.ComplexProperty(s => s.TaxProfile, t =>
        {
            t.Property(x => x.ProfileType).HasColumnName("tax_profile_type")
                .HasConversion<string>().HasMaxLength(32).IsRequired();
            t.Property(x => x.TinValue).HasColumnName("tax_profile_tin")
                .HasMaxLength(9).IsUnicode(false);
            t.Property(x => x.ReverseChargeFlag).HasColumnName("tax_profile_reverse_charge").IsRequired();
            t.Property(x => x.DefaultPurchaseVatCategoryId).HasColumnName("tax_profile_default_purchase_vat_category_id");
        });

        // R-13 — covering field for the SupplierTinRevalidationJob's
        // "stale or never checked" filter. Indexed so the cron's row
        // selection stays seek-friendly as the supplier base grows.
        b.Property(s => s.LastTinRevalidatedAtUtc)
            .HasColumnName("last_tin_revalidated_at_utc")
            .HasColumnType("datetime2(3)");
        b.HasIndex(s => s.LastTinRevalidatedAtUtc)
            .HasDatabaseName("ix_suppliers_last_tin_revalidated_at_utc");
    }
}
