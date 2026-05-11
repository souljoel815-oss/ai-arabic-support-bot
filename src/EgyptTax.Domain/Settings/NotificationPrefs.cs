namespace EgyptTax.Domain.Settings;

/// <summary>
/// Gux.13 Tab 9 — which kinds of in-app + email notifications fire,
/// and how many days ahead of a deadline. Single-row config.
///
/// Day-arrays are stored as comma-separated strings on disk
/// (SQLite-friendly) and exposed as int arrays via the property
/// converters in the entity configuration. Defaults are populated
/// for new installs by <see cref="CreateDefault"/>.
/// </summary>
public sealed class NotificationPrefs
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public bool TaxDeadlineEnabled { get; private set; } = true;
    public int[] TaxDeadlineDaysBefore { get; private set; } = new[] { 7, 3, 1 };

    public bool LicenseExpiryEnabled { get; private set; } = true;
    public int[] LicenseExpiryDaysBefore { get; private set; } = new[] { 30, 7, 1 };

    public bool EtaFailureEnabled { get; private set; } = true;
    public bool PendingApprovalsEnabled { get; private set; } = true;
    public bool BackupReminderEnabled { get; private set; } = true;
    public int BackupReminderDays { get; private set; } = 7;

    /// <summary>Gux.13 v2 — alerts when the USB-token ETA
    /// certificate's NotAfter date approaches. Silent expiry breaks
    /// invoicing, so this is on by default.</summary>
    public bool EtaCertExpiryEnabled { get; private set; } = true;
    public int[] EtaCertExpiryDaysBefore { get; private set; } = new[] { 30, 7 };

    public bool EmailNotificationsEnabled { get; private set; }

    private NotificationPrefs() { }

    public static NotificationPrefs CreateDefault() => new();

    public void SetTaxDeadline(bool enabled, int[] daysBefore)
    {
        TaxDeadlineEnabled = enabled;
        TaxDeadlineDaysBefore = ValidateDayArray(daysBefore);
    }

    public void SetLicenseExpiry(bool enabled, int[] daysBefore)
    {
        LicenseExpiryEnabled = enabled;
        LicenseExpiryDaysBefore = ValidateDayArray(daysBefore);
    }

    public void SetEtaFailure(bool enabled) => EtaFailureEnabled = enabled;
    public void SetPendingApprovals(bool enabled) => PendingApprovalsEnabled = enabled;

    public void SetBackupReminder(bool enabled, int days)
    {
        if (days is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(days));
        BackupReminderEnabled = enabled;
        BackupReminderDays = days;
    }

    public void SetEtaCertExpiry(bool enabled, int[] daysBefore)
    {
        EtaCertExpiryEnabled = enabled;
        EtaCertExpiryDaysBefore = ValidateDayArray(daysBefore);
    }

    public void SetEmailNotifications(bool enabled) => EmailNotificationsEnabled = enabled;

    private static int[] ValidateDayArray(int[] days)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (days.Length == 0)
            throw new ArgumentException("Must specify at least one day-before threshold.", nameof(days));
        if (days.Any(d => d is < 1 or > 365))
            throw new ArgumentOutOfRangeException(nameof(days), "Day thresholds must be 1-365.");
        // Sort descending so the soonest threshold is last — matches
        // the natural reading order ("7 days, 3 days, 1 day").
        return days.OrderByDescending(d => d).Distinct().ToArray();
    }
}
