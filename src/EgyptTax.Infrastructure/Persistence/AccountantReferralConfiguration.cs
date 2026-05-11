using EgyptTax.Domain.FirmPortal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class AccountantReferralConfiguration : IEntityTypeConfiguration<AccountantReferral>
{
    public void Configure(EntityTypeBuilder<AccountantReferral> b)
    {
        b.ToTable("accountant_referrals", schema: "firm_portal");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(r => r.AccountantFirmUserId).HasColumnName("accountant_firm_user_id").IsRequired();
        b.Property(r => r.ReferredCustomerName).HasColumnName("referred_customer_name").HasMaxLength(200).IsRequired();
        b.Property(r => r.ReferredCustomerHwid).HasColumnName("referred_customer_hwid").HasMaxLength(32).IsUnicode(false).IsRequired();
        b.Property(r => r.LicenseEdition).HasColumnName("license_edition").HasMaxLength(40).IsRequired();
        b.Property(r => r.LicenseAnnualPriceEgp).HasColumnName("license_annual_price_egp").HasColumnType("decimal(19,2)").IsRequired();
        b.Property(r => r.CommissionRatePercent).HasColumnName("commission_rate_percent").HasColumnType("decimal(5,2)").IsRequired();
        b.Property(r => r.CommissionEgp).HasColumnName("commission_egp").HasColumnType("decimal(19,2)").IsRequired();
        b.Property(r => r.ReferredAtUtc).HasColumnName("referred_at_utc").HasColumnType("datetime2(3)").IsRequired();

        b.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        b.Property(r => r.EarnedAtUtc).HasColumnName("earned_at_utc").HasColumnType("datetime2(3)");
        b.Property(r => r.PaidAtUtc).HasColumnName("paid_at_utc").HasColumnType("datetime2(3)");
        b.Property(r => r.PaidReference).HasColumnName("paid_reference").HasMaxLength(100);
        b.Property(r => r.Note).HasColumnName("note").HasMaxLength(2000);

        // Dedupe: one HWID can only be referred once. Vendor sees a
        // duplicate-key error if they try to record a referral twice
        // for the same install.
        b.HasIndex(r => r.ReferredCustomerHwid)
            .IsUnique()
            .HasDatabaseName("ix_accountant_referrals_hwid_unique");

        // Drives the per-firm commissions list view.
        b.HasIndex(r => new { r.AccountantFirmUserId, r.Status })
            .HasDatabaseName("ix_accountant_referrals_firm_status");
    }
}
