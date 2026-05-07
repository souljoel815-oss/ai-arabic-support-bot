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

        b.Property(c => c.Code).HasColumnName("code").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.HasIndex(c => c.Code).IsUnique().HasDatabaseName("ux_customers_code");

        b.ComplexProperty(c => c.Name, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
            n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        });

        b.ComplexProperty(c => c.Address, a =>
        {
            a.Property(x => x.Arabic).HasColumnName("address_ar").HasMaxLength(500).IsRequired();
            a.Property(x => x.English).HasColumnName("address_en").HasMaxLength(500).IsRequired();
        });

        b.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(32).IsUnicode(false);
        b.Property(c => c.Email).HasColumnName("email").HasMaxLength(254).IsUnicode(false);
        b.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();

        b.ComplexProperty(c => c.TaxProfile, t =>
        {
            t.Property(x => x.ProfileType).HasColumnName("tax_profile_type")
                .HasConversion<string>().HasMaxLength(32).IsRequired();
            t.Property(x => x.TinValue).HasColumnName("tax_profile_tin")
                .HasMaxLength(9).IsUnicode(false);
            t.Property(x => x.VatExemption).HasColumnName("tax_profile_vat_exemption").IsRequired();
            t.Property(x => x.DefaultSalesVatCategoryId).HasColumnName("tax_profile_default_sales_vat_category_id");
        });
    }
}
