using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers", schema: "master");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(c => c.Code)
            .HasColumnName("code")
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();
        b.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_customers_code");

        b.ComplexProperty(
            c => c.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
            }
        );

        b.ComplexProperty(
            c => c.Address,
            addr =>
            {
                addr.Property(x => x.DisplayArabic)
                    .HasColumnName("address_ar")
                    .HasMaxLength(500)
                    .IsRequired();
                addr.Property(x => x.DisplayEnglish)
                    .HasColumnName("address_en")
                    .HasMaxLength(500)
                    .IsRequired();
                addr.Property(x => x.Country)
                    .HasColumnName("address_country")
                    .HasMaxLength(2)
                    .IsUnicode(false)
                    .IsRequired();
                addr.Property(x => x.Governorate)
                    .HasColumnName("address_governorate")
                    .HasMaxLength(100)
                    .IsRequired();
                addr.Property(x => x.RegionCity)
                    .HasColumnName("address_region_city")
                    .HasMaxLength(100)
                    .IsRequired();
                addr.Property(x => x.Street)
                    .HasColumnName("address_street")
                    .HasMaxLength(200)
                    .IsRequired();
                addr.Property(x => x.BuildingNumber)
                    .HasColumnName("address_building_number")
                    .HasMaxLength(32)
                    .IsUnicode(false)
                    .IsRequired();
                addr.Property(x => x.PostalCode)
                    .HasColumnName("address_postal_code")
                    .HasMaxLength(16)
                    .IsUnicode(false);
            }
        );

        b.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(32).IsUnicode(false);
        b.Property(c => c.Email).HasColumnName("email").HasMaxLength(254).IsUnicode(false);
        b.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(c => c.CreditLimitEgp)
            .HasColumnName("credit_limit_egp")
            .HasColumnType("decimal(19,2)");

        // v5 B.2 — default pricelist FK (nullable). No CASCADE; we
        // soft-deactivate pricelists via PricelistStatus.Inactive
        // and keep historical assignments intact.
        b.Property(c => c.DefaultPricelistId).HasColumnName("default_pricelist_id");
        b.HasIndex(c => c.DefaultPricelistId).HasDatabaseName("ix_customers_default_pricelist_id");

        b.ComplexProperty(
            c => c.TaxProfile,
            t =>
            {
                t.Property(x => x.ProfileType)
                    .HasColumnName("tax_profile_type")
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .IsRequired();
                t.Property(x => x.TinValue)
                    .HasColumnName("tax_profile_tin")
                    .HasMaxLength(9)
                    .IsUnicode(false);
                t.Property(x => x.VatExemption)
                    .HasColumnName("tax_profile_vat_exemption")
                    .IsRequired();
                t.Property(x => x.DefaultSalesVatCategoryId)
                    .HasColumnName("tax_profile_default_sales_vat_category_id");
            }
        );
    }
}
