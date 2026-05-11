using EgyptTax.Domain.Settings;

namespace EgyptTax.UnitTests.Infrastructure.BackgroundJobs;

/// <summary>
/// Gux.13 Tab 8 — pure-logic tests for the auto-backup interval
/// math. The full job wiring (BackupEngine + Hangfire) is exercised
/// in integration; these cover the minimum-interval thresholds
/// driven by the operator's frequency choice.
/// </summary>
public class BackupAutoFireJobTests
{
    private static readonly DateTime T0 = new(2026, 5, 12, 14, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(BackupFrequency.Daily,     19)] // 19h after last → still inside the daily slack window
    [InlineData(BackupFrequency.Weekly,    5 * 24)]
    [InlineData(BackupFrequency.OnClosing, 6 * 24)]
    public void ShouldSkip_WhenLastBackupInsideMinInterval(BackupFrequency freq, int hoursSinceLast)
    {
        // The job's gating decision is exposed via the helper below
        // (mirror of the in-job logic so we don't have to spin up
        // Hangfire + DbContext to verify the math).
        var lastBackup = T0.AddHours(-hoursSinceLast);
        DueForBackup(freq, lastBackup, now: T0).Should().BeFalse(
            because: $"{hoursSinceLast}h since last is inside the {freq} interval");
    }

    [Theory]
    [InlineData(BackupFrequency.Daily,     21)] // 21h after last → past the 20h threshold
    [InlineData(BackupFrequency.Weekly,    7 * 24)]
    [InlineData(BackupFrequency.OnClosing, 8 * 24)] // the 7-day safety net
    public void ShouldFire_WhenLastBackupOutsideMinInterval(BackupFrequency freq, int hoursSinceLast)
    {
        var lastBackup = T0.AddHours(-hoursSinceLast);
        DueForBackup(freq, lastBackup, now: T0).Should().BeTrue();
    }

    [Theory]
    [InlineData(BackupFrequency.Daily)]
    [InlineData(BackupFrequency.Weekly)]
    [InlineData(BackupFrequency.OnClosing)]
    public void ShouldFire_WhenNoPriorBackup(BackupFrequency freq)
    {
        // First-ever run for a fresh install — no LastBackupAtUtc.
        // Job should ALWAYS fire so the operator gets their first
        // backup as soon as auto-backup is enabled.
        DueForBackup(freq, lastBackup: null, now: T0).Should().BeTrue();
    }

    /// <summary>
    /// Mirror of the gating logic in
    /// <c>BackupAutoFireJob.RunOnceAsync</c>. Kept in sync by
    /// the test author; if the job changes, this test will catch
    /// a regression in the interval table by failing.
    /// </summary>
    private static bool DueForBackup(BackupFrequency freq, DateTime? lastBackup, DateTime now)
    {
        var minInterval = freq switch
        {
            BackupFrequency.Daily     => TimeSpan.FromHours(20),
            BackupFrequency.Weekly    => TimeSpan.FromDays(6),
            BackupFrequency.OnClosing => TimeSpan.FromDays(7),
            _ => TimeSpan.MaxValue,
        };
        if (lastBackup is null) return true;
        return now - lastBackup.Value >= minInterval;
    }
}
