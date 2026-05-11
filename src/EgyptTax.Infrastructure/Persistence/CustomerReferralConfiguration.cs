using EgyptTax.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class CustomerReferralConfiguration : IEntityTypeConfiguration<CustomerReferral>
{
    public void Configure(EntityTypeBuilder<CustomerReferral> b)
    {
        b.ToTable("customer_referrals", schema: "referrals");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(r => r.ReferredContactName).HasColumnName("referred_contact_name").HasMaxLength(200).IsRequired();
        b.Property(r => r.ReferredContactDetail).HasColumnName("referred_contact_detail").HasMaxLength(200);
        b.Property(r => r.InvitedAtUtc).HasColumnName("invited_at_utc").HasColumnType("datetime2(3)").IsRequired();

        b.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsUnicode(false)
            .IsRequired();
        b.Property(r => r.InstalledAtUtc).HasColumnName("installed_at_utc").HasColumnType("datetime2(3)");
        b.Property(r => r.PurchasedAtUtc).HasColumnName("purchased_at_utc").HasColumnType("datetime2(3)");
        b.Property(r => r.ReferredCustomerHwid).HasColumnName("referred_customer_hwid").HasMaxLength(32).IsUnicode(false);
        b.Property(r => r.RewardDays).HasColumnName("reward_days").IsRequired();
        b.Property(r => r.Note).HasColumnName("note").HasMaxLength(2000);

        // Drives the page's "by-status" filter (Invited/Installed/Purchased).
        b.HasIndex(r => r.Status)
            .HasDatabaseName("ix_customer_referrals_status");
    }
}
