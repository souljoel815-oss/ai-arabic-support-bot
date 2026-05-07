using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("companies", schema: "master");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        b.ComplexProperty(c => c.LegalName, n =>
        {
            n.Property(x => x.Arabic).HasColumnName("legal_name_ar").HasMaxLength(200).IsRequired();
            n.Property(x => x.English).HasColumnName("legal_name_en").HasMaxLength(200).IsRequired();
        });

        b.Property(c => c.TaxRegistrationNumber).HasColumnName("tax_registration_number").HasMaxLength(9).IsUnicode(false).IsRequired();
        b.Property(c => c.CommercialRegistrationNumber).HasColumnName("commercial_registration_number").HasMaxLength(32).IsUnicode(false).IsRequired();

        b.ComplexProperty(c => c.Address, addr =>
        {
            addr.Property(x => x.DisplayArabic).HasColumnName("address_ar").HasMaxLength(500).IsRequired();
            addr.Property(x => x.DisplayEnglish).HasColumnName("address_en").HasMaxLength(500).IsRequired();
            addr.Property(x => x.Country).HasColumnName("address_country").HasMaxLength(2).IsUnicode(false).IsRequired();
            addr.Property(x => x.Governorate).HasColumnName("address_governorate").HasMaxLength(100).IsRequired();
            addr.Property(x => x.RegionCity).HasColumnName("address_region_city").HasMaxLength(100).IsRequired();
            addr.Property(x => x.Street).HasColumnName("address_street").HasMaxLength(200).IsRequired();
            addr.Property(x => x.BuildingNumber).HasColumnName("address_building_number").HasMaxLength(32).IsUnicode(false).IsRequired();
            addr.Property(x => x.PostalCode).HasColumnName("address_postal_code").HasMaxLength(16).IsUnicode(false);
        });

        b.Property(c => c.LogoPath).HasColumnName("logo_path").HasMaxLength(512);
        b.Property(c => c.FiscalYearStartMonth).HasColumnName("fiscal_year_start_month").IsRequired();
        b.Property(c => c.DefaultCurrency).HasColumnName("default_currency").HasMaxLength(3).IsUnicode(false).IsRequired();
        b.Property(c => c.DefaultLanguage).HasColumnName("default_language").HasConversion<string>().HasMaxLength(2).IsRequired();
        b.Property(c => c.TaxpayerActivityCode).HasColumnName("taxpayer_activity_code").HasMaxLength(16).IsUnicode(false).IsRequired();
    }
}
