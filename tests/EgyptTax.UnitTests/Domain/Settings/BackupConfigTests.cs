using EgyptTax.Domain.Settings;

namespace EgyptTax.UnitTests.Domain.Settings;

/// <summary>
/// Gux.13 — covers <see cref="BackupConfig"/> entity invariants.
/// The actual backup execution lives in <c>BackupEngine</c>; these
/// tests cover the config entity that holds scheduling preferences.
/// </summary>
public class BackupConfigTests
{
    [Fact]
    public void CreateDefault_AutoBackupOff_DailyFrequency_30Retention()
    {
        var c = BackupConfig.CreateDefault();

        c.AutoBackupEnabled.Should().BeFalse();
        c.Frequency.Should().Be(BackupFrequency.Daily);
        c.RetentionCount.Should().Be(30);
        c.SavePath.Should().Contain("DaftarX");
        c.LastBackupAtUtc.Should().BeNull();
    }

    [Fact]
    public void UpdateAutoBackup_Toggles()
    {
        var c = BackupConfig.CreateDefault();
        c.UpdateAutoBackup(true);
        c.AutoBackupEnabled.Should().BeTrue();
        c.UpdateAutoBackup(false);
        c.AutoBackupEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateFrequency_AcceptsAllThreeValues()
    {
        var c = BackupConfig.CreateDefault();
        c.UpdateFrequency(BackupFrequency.Weekly);
        c.Frequency.Should().Be(BackupFrequency.Weekly);
        c.UpdateFrequency(BackupFrequency.OnClosing);
        c.Frequency.Should().Be(BackupFrequency.OnClosing);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateSavePath_RejectsBlank(string? bad)
    {
        var c = BackupConfig.CreateDefault();
        var act = () => c.UpdateSavePath(bad!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateSavePath_TrimsWhitespace()
    {
        var c = BackupConfig.CreateDefault();
        c.UpdateSavePath("  C:\\Backups\\  ");
        c.SavePath.Should().Be("C:\\Backups\\");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void UpdateRetentionCount_RejectsOutOfRange(int bad)
    {
        var c = BackupConfig.CreateDefault();
        var act = () => c.UpdateRetentionCount(bad);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    public void UpdateRetentionCount_AcceptsInRange(int good)
    {
        var c = BackupConfig.CreateDefault();
        c.UpdateRetentionCount(good);
        c.RetentionCount.Should().Be(good);
    }

    [Fact]
    public void RecordBackupCompleted_PopulatesTelemetry()
    {
        var c = BackupConfig.CreateDefault();
        var when = new DateTime(2026, 5, 12, 14, 0, 0, DateTimeKind.Utc);

        c.RecordBackupCompleted(when, sizeBytes: 1_234_567, path: @"C:\Backups\test.dxbak");

        c.LastBackupAtUtc.Should().Be(when);
        c.LastBackupSizeBytes.Should().Be(1_234_567);
        c.LastBackupPath.Should().Be(@"C:\Backups\test.dxbak");
    }

    [Fact]
    public void RecordBackupCompleted_OverwritesPreviousTelemetry()
    {
        var c = BackupConfig.CreateDefault();
        c.RecordBackupCompleted(new DateTime(2026, 5, 1, 14, 0, 0, DateTimeKind.Utc), 100, "old.dxbak");
        c.RecordBackupCompleted(new DateTime(2026, 5, 12, 14, 0, 0, DateTimeKind.Utc), 200, "new.dxbak");

        c.LastBackupAtUtc!.Value.Day.Should().Be(12);
        c.LastBackupSizeBytes.Should().Be(200);
        c.LastBackupPath.Should().Be("new.dxbak");
    }
}
