using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class PaymentReminderDispatchConfiguration
    : IEntityTypeConfiguration<PaymentReminderDispatch>
{
    public void Configure(EntityTypeBuilder<PaymentReminderDispatch> b)
    {
        b.ToTable("payment_reminder_dispatches", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.SentAtUtc).HasColumnName("sent_at_utc")
            .HasColumnType("datetime2(3)").IsRequired();
        b.Property(x => x.OutstandingAtSendEgp).HasColumnName("outstanding_at_send_egp")
            .HasColumnType("decimal(19,2)").IsRequired();
        b.Property(x => x.OldestUnpaidInvoiceDate).HasColumnName("oldest_unpaid_invoice_date")
            .HasColumnType("date").IsRequired();
        b.Property(x => x.SentToEmail).HasColumnName("sent_to_email")
            .HasMaxLength(254).IsUnicode(false).IsRequired();

        // v4 B.2 — escalation tier. Stored as a string for
        // human-readable diagnostics (same convention as other enums
        // in the project).
        b.Property(x => x.Tier).HasColumnName("tier")
            .HasConversion<string>().HasMaxLength(16).IsRequired()
            .HasDefaultValue(ReminderTier.Gentle);

        b.HasIndex(x => new { x.CustomerId, x.SentAtUtc })
            .HasDatabaseName("ix_payment_reminder_dispatches_customer_sent");
        // Per-tier cooldown lookup driver — the v4 job queries
        // "latest dispatch of THIS tier for THIS customer".
        b.HasIndex(x => new { x.CustomerId, x.Tier, x.SentAtUtc })
            .HasDatabaseName("ix_payment_reminder_dispatches_customer_tier_sent");
    }
}
