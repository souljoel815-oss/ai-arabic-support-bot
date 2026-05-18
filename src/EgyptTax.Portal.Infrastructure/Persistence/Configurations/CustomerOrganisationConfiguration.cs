using EgyptTax.Portal.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Portal.Infrastructure.Persistence.Configurations;

/// <summary>T025 per data-model.md §1.</summary>
internal sealed class CustomerOrganisationConfiguration : IEntityTypeConfiguration<CustomerOrganisation>
{
    public void Configure(EntityTypeBuilder<CustomerOrganisation> b)
    {
        b.ToTable("customer_organisations");

        b.HasKey(x => x.Id);

        b.Property(x => x.LegalNameAr).IsRequired().HasMaxLength(256);
        b.Property(x => x.LegalNameEn).HasMaxLength(256);
        b.Property(x => x.TaxRegistrationNumber).HasMaxLength(32);
        b.Property(x => x.BillingEmail).IsRequired().HasMaxLength(256);
        b.Property(x => x.BillingPhone).HasMaxLength(32);
        b.Property(x => x.BillingAddressJson);
        b.Property(x => x.CountryCode).IsRequired().HasMaxLength(2).IsFixedLength();

        b.HasIndex(x => x.BillingEmail);
        b.HasIndex(x => x.SoftDeletedAtUtc).HasFilter("[soft_deleted_at_utc] IS NOT NULL");
    }
}
