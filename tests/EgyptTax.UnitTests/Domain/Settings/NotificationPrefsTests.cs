using EgyptTax.Domain.Settings;

namespace EgyptTax.UnitTests.Domain.Settings;

/// <summary>
/// Gux.13 — covers <see cref="NotificationPrefs"/> entity invariants.
/// Day-array setters validate range + sort descending so reading
/// order is natural ("7, 3, 1 days").
/// </summary>
public class NotificationPrefsTests
{
    private static readonly int[] DaysShuffled = new[] { 1, 7, 3 };
    private static readonly int[] DaysWithDupes = new[] { 7, 3, 7, 1, 3 };
    private static readonly int[] DaysExpiry30And1 = new[] { 30, 1 };
    private static readonly int[] DaysCertShuffled = new[] { 7, 30, 14 };

    [Fact]
    public void CreateDefault_AllNotificationsOn_EmailOff()
    {
        var p = NotificationPrefs.CreateDefault();

        p.TaxDeadlineEnabled.Should().BeTrue();
        p.TaxDeadlineDaysBefore.Should().Equal(7, 3, 1);

        p.LicenseExpiryEnabled.Should().BeTrue();
        p.LicenseExpiryDaysBefore.Should().Equal(30, 7, 1);

        p.EtaFailureEnabled.Should().BeTrue();
        p.PendingApprovalsEnabled.Should().BeTrue();
        p.BackupReminderEnabled.Should().BeTrue();
        p.BackupReminderDays.Should().Be(7);

        p.EtaCertExpiryEnabled.Should().BeTrue();
        p.EtaCertExpiryDaysBefore.Should().Equal(30, 7);

        // Email defaults OFF — requires SMTP configured first.
        p.EmailNotificationsEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetTaxDeadline_SortsDescending()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetTaxDeadline(true, DaysShuffled);
        p.TaxDeadlineDaysBefore.Should().Equal(7, 3, 1);
    }

    [Fact]
    public void SetTaxDeadline_DedupesDuplicates()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetTaxDeadline(true, DaysWithDupes);
        p.TaxDeadlineDaysBefore.Should().Equal(7, 3, 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetTaxDeadline_RejectsOutOfRangeDay(int bad)
    {
        var p = NotificationPrefs.CreateDefault();
        var days = new[] { 7, bad }; // inline literal would trip CA1861; hoist to local
        var act = () => p.SetTaxDeadline(true, days);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetTaxDeadline_RejectsEmptyArray()
    {
        var p = NotificationPrefs.CreateDefault();
        var act = () => p.SetTaxDeadline(true, Array.Empty<int>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetLicenseExpiry_TogglesIndependentlyOfDays()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetLicenseExpiry(false, DaysExpiry30And1);
        p.LicenseExpiryEnabled.Should().BeFalse();
        p.LicenseExpiryDaysBefore.Should().Equal(30, 1);
    }

    [Fact]
    public void SetEtaFailure_PureToggle()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetEtaFailure(false);
        p.EtaFailureEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetPendingApprovals_PureToggle()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetPendingApprovals(false);
        p.PendingApprovalsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetBackupReminder_RejectsOutOfRangeDays(int bad)
    {
        var p = NotificationPrefs.CreateDefault();
        var act = () => p.SetBackupReminder(true, bad);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetBackupReminder_AcceptsValidDays()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetBackupReminder(true, 14);
        p.BackupReminderDays.Should().Be(14);
    }

    [Fact]
    public void SetEtaCertExpiry_SortsAndValidatesLikeOtherDayArrays()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetEtaCertExpiry(true, DaysCertShuffled);
        p.EtaCertExpiryDaysBefore.Should().Equal(30, 14, 7);
    }

    [Fact]
    public void SetEmailNotifications_PureToggle()
    {
        var p = NotificationPrefs.CreateDefault();
        p.SetEmailNotifications(true);
        p.EmailNotificationsEnabled.Should().BeTrue();
    }
}
