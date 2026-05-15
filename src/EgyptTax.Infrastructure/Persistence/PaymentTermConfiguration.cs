using EgyptTax.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PaymentTermConfiguration : IEntityTypeConfiguration<PaymentTerm>
{
    public void Configure(EntityTypeBuilder<PaymentTerm> b)
    {
        b.ToTable("payment_terms", schema: "master_data");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        b.ComplexProperty(
            t => t.Name,
            n =>
            {
                n.Property(x => x.Arabic).HasColumnName("name_ar").HasMaxLength(120).IsRequired();
                n.Property(x => x.English).HasColumnName("name_en").HasMaxLength(120).IsRequired();
            });

        b.Property(t => t.NetDays).HasColumnName("net_days").IsRequired();
        b.Property(t => t.DiscountPercent).HasColumnName("discount_percent")
            .HasColumnType("decimal(5,2)").IsRequired();
        b.Property(t => t.DiscountWindowDays).HasColumnName("discount_window_days").IsRequired();
        b.Property(t => t.IsDefault).HasColumnName("is_default").IsRequired();
        b.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();

        b.HasIndex(t => t.IsActive).HasDatabaseName("ix_payment_terms_is_active");
        b.HasIndex(t => t.IsDefault).HasDatabaseName("ix_payment_terms_is_default");
    }
}
