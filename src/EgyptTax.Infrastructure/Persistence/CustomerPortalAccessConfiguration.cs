using EgyptTax.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CustomerPortalAccessConfiguration
    : IEntityTypeConfiguration<CustomerPortalAccess>
{
    public void Configure(EntityTypeBuilder<CustomerPortalAccess> b)
    {
        b.ToTable("customer_portal_access", schema: "master_data");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.Token).HasColumnName("token")
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        b.HasIndex(x => x.Token).IsUnique().HasDatabaseName("ux_customer_portal_access_token");

        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.LastViewedAtUtc).HasColumnName("last_viewed_at_utc")
            .HasColumnType("datetime2(3)");
        b.Property(x => x.Revoked).HasColumnName("revoked").IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");

        b.HasIndex(x => x.CustomerId).HasDatabaseName("ix_customer_portal_access_customer_id");
    }
}
