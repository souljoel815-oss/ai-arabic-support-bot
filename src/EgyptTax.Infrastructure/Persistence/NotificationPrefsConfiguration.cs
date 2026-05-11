using System.Globalization;
using EgyptTax.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EgyptTax.Infrastructure.Persistence;

internal sealed class NotificationPrefsConfiguration : IEntityTypeConfiguration<NotificationPrefs>
{
    public void Configure(EntityTypeBuilder<NotificationPrefs> b)
    {
        b.ToTable("notification_prefs", schema: "settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        b.Property(x => x.TaxDeadlineEnabled).HasColumnName("tax_deadline_enabled").IsRequired();
        b.Property(x => x.TaxDeadlineDaysBefore)
            .HasColumnName("tax_deadline_days_before")
            .HasMaxLength(100)
            .HasConversion(IntArrayConverter)
            .Metadata.SetValueComparer(IntArrayComparer);

        b.Property(x => x.LicenseExpiryEnabled).HasColumnName("license_expiry_enabled").IsRequired();
        b.Property(x => x.LicenseExpiryDaysBefore)
            .HasColumnName("license_expiry_days_before")
            .HasMaxLength(100)
            .HasConversion(IntArrayConverter)
            .Metadata.SetValueComparer(IntArrayComparer);

        b.Property(x => x.EtaFailureEnabled).HasColumnName("eta_failure_enabled").IsRequired();
        b.Property(x => x.PendingApprovalsEnabled).HasColumnName("pending_approvals_enabled").IsRequired();
        b.Property(x => x.BackupReminderEnabled).HasColumnName("backup_reminder_enabled").IsRequired();
        b.Property(x => x.BackupReminderDays).HasColumnName("backup_reminder_days").IsRequired();

        b.Property(x => x.EtaCertExpiryEnabled).HasColumnName("eta_cert_expiry_enabled").IsRequired();
        b.Property(x => x.EtaCertExpiryDaysBefore)
            .HasColumnName("eta_cert_expiry_days_before")
            .HasMaxLength(100)
            .HasConversion(IntArrayConverter)
            .Metadata.SetValueComparer(IntArrayComparer);

        b.Property(x => x.EmailNotificationsEnabled).HasColumnName("email_notifications_enabled").IsRequired();
    }

    /// <summary>Stores int[] as comma-separated string. Avoids JSON
    /// (overkill for ≤10 small ints) and works on both SQLite and
    /// SQL Server without provider-specific column types.</summary>
    private static readonly ValueConverter<int[], string> IntArrayConverter = new(
        v => string.Join(",", v.Select(i => i.ToString(CultureInfo.InvariantCulture))),
        v => string.IsNullOrWhiteSpace(v)
            ? Array.Empty<int>()
            : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture))
                .ToArray());

    /// <summary>EF needs an explicit comparer for mutable reference
    /// types so change-tracking knows when an array was modified
    /// rather than just reassigned.</summary>
    private static readonly ValueComparer<int[]> IntArrayComparer = new(
        (a, b) => (a ?? Array.Empty<int>()).SequenceEqual(b ?? Array.Empty<int>()),
        v => v == null ? 0 : v.Aggregate(0, (acc, i) => HashCode.Combine(acc, i)),
        v => v == null ? Array.Empty<int>() : v.ToArray());
}
