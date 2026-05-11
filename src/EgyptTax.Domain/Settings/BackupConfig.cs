namespace EgyptTax.Domain.Settings;

/// <summary>
/// Gux.13 Tab 8 — backup scheduling + retention. Single-row config.
/// AutoBackup defaults to OFF — operators opt in once they trust
/// their data is worth backing up. Manual "Backup Now" works
/// regardless of this flag.
///
/// The actual backup mechanism is provider-aware (SQL Server vs
/// SQLite) — see the Tab 8 spec. This entity just holds the
/// scheduling preferences.
/// </summary>
public sealed class BackupConfig
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public bool AutoBackupEnabled { get; private set; }
    public BackupFrequency Frequency { get; private set; } = BackupFrequency.Daily;
    public string SavePath { get; private set; } = ResolveDefaultSavePath();
    public int RetentionCount { get; private set; } = 30;
    public DateTime? LastBackupAtUtc { get; private set; }
    public long? LastBackupSizeBytes { get; private set; }
    public string? LastBackupPath { get; private set; }

    private BackupConfig() { }

    public static BackupConfig CreateDefault() => new();

    public void UpdateAutoBackup(bool enabled) => AutoBackupEnabled = enabled;

    public void UpdateFrequency(BackupFrequency frequency) => Frequency = frequency;

    public void UpdateSavePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SavePath = path.Trim();
    }

    public void UpdateRetentionCount(int count)
    {
        if (count is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(count),
                "Retention must be 1-365 backups.");
        RetentionCount = count;
    }

    /// <summary>Called by the backup engine after a successful run
    /// to update the "last backup" telemetry shown in the UI.</summary>
    public void RecordBackupCompleted(DateTime atUtc, long sizeBytes, string path)
    {
        LastBackupAtUtc = atUtc;
        LastBackupSizeBytes = sizeBytes;
        LastBackupPath = path;
    }

    private static string ResolveDefaultSavePath()
    {
        // %USERPROFILE%\DaftarX\Backups\ — matches what the spec
        // shows in the placeholder. The folder is created on first
        // backup if missing.
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return System.IO.Path.Combine(profile, "DaftarX", "Backups");
    }
}

public enum BackupFrequency
{
    Daily = 0,
    Weekly = 1,
    /// <summary>Triggered when the operator locks a tax period
    /// (FR-037). Aligns the backup with the natural compliance
    /// rhythm.</summary>
    OnClosing = 2,
}
