using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> b)
    {
        b.ToTable("currencies", schema: "settings");
        b.HasKey(c => c.Code);
        b.Property(c => c.Code).HasColumnName("code").HasMaxLength(3).IsUnicode(false);
        b.Property(c => c.Symbol).HasColumnName("symbol").HasMaxLength(8);
        b.Property(c => c.NameEn).HasColumnName("name_en").HasMaxLength(100).IsRequired();
        b.Property(c => c.NameAr).HasColumnName("name_ar").HasMaxLength(100).IsRequired();
        b.Property(c => c.IsBase).HasColumnName("is_base").IsRequired();
        b.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
    }
}

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("exchange_rates", schema: "settings");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(r => r.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(3).IsUnicode(false).IsRequired();
        b.Property(r => r.EffectiveDate).HasColumnName("effective_date")
            .HasColumnType("date").IsRequired();
        b.Property(r => r.RateToBase).HasColumnName("rate_to_base")
            .HasColumnType("decimal(19,6)").IsRequired();
        b.Property(r => r.Source).HasColumnName("source").HasMaxLength(100);

        // One rate per (currency, day). Re-saving the same day
        // updates via the entity's UpdateRate method.
        b.HasIndex(r => new { r.CurrencyCode, r.EffectiveDate })
            .IsUnique()
            .HasDatabaseName("ux_exchange_rates_currency_date");
    }
}
