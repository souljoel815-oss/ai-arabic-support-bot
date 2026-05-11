using System.IO.Compression;
using System.Text.Json;
using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Settings;

/// <summary>
/// Gux.13 Tab 8 — provider-aware backup engine.
///
/// SQL Server Express path: runs <c>BACKUP DATABASE ... TO DISK</c>
/// to produce a .bak file, then zips it with the attachments folder
/// + a manifest.json.
///
/// SQLite (SQLCipher) path: uses the SQLite Online Backup API
/// (Microsoft.Data.Sqlite Backup() method) to copy the encrypted
/// .db file safely while the engine is in use, then zips it with
/// the attachments folder + manifest.
///
/// The .dxbak file is a renamed .zip containing:
///   manifest.json   - provider, app version, schema version, timestamp
///   db.bak | db.db  - the database backup (extension depends on provider)
///   attachments/    - mirrored attachment files (path-preserved)
///
/// Restore reads the manifest first; if the provider doesn't match
/// the current install, it refuses (cross-provider restore is
/// intentionally unsupported per the spec).
/// </summary>
public sealed class BackupEngine
{
    private const string ManifestFileName = "manifest.json";
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly SettingsRepository _settings;
    private readonly IClock _clock;
    private readonly string _attachmentsRoot;

    public BackupEngine(
        IDbContextFactory<AppDbContext> dbFactory,
        SettingsRepository settings,
        IClock clock,
        string attachmentsRoot)
    {
        _dbFactory = dbFactory;
        _settings = settings;
        _clock = clock;
        _attachmentsRoot = attachmentsRoot;
    }

    public async Task<BackupResult> BackupNowAsync(CancellationToken ct = default)
    {
        var config = await _settings.GetBackupConfigAsync(ct);
        Directory.CreateDirectory(config.SavePath);

        var stamp = _clock.UtcNow.ToString("yyyyMMdd-HHmmss",
            System.Globalization.CultureInfo.InvariantCulture);
        var bakFileName = $"daftarx-backup-{stamp}.dxbak";
        var bakPath = Path.Combine(config.SavePath, bakFileName);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var provider = db.Database.ProviderName ?? "";

        // Stage everything into a temp folder so the final .dxbak is
        // atomic — partial files don't pollute the backup directory.
        var stagingDir = Path.Combine(Path.GetTempPath(),
            $"daftarx-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        try
        {
            string dbFileName;
            if (provider == SqlServerProvider)
            {
                dbFileName = "db.bak";
                await BackupSqlServerAsync(db, Path.Combine(stagingDir, dbFileName), ct);
            }
            else if (provider == SqliteProvider)
            {
                dbFileName = "db.db";
                BackupSqlite(db, Path.Combine(stagingDir, dbFileName));
            }
            else
            {
                return BackupResult.Failure($"Unsupported DB provider: {provider}");
            }

            // Manifest tells Restore which path to take.
            var manifest = new BackupManifest(
                Provider: provider,
                ProviderFriendly: provider == SqlServerProvider ? "SQL Server Express" : "SQLite (SQLCipher)",
                CreatedAtUtc: _clock.UtcNow,
                AppVersion: typeof(BackupEngine).Assembly.GetName().Version?.ToString() ?? "?",
                DbFileName: dbFileName,
                HasAttachments: Directory.Exists(_attachmentsRoot));
            await File.WriteAllTextAsync(
                Path.Combine(stagingDir, ManifestFileName),
                JsonSerializer.Serialize(manifest, IndentedJson),
                ct);

            // Copy attachments (best-effort — missing folder = empty backup).
            var attachmentsStaging = Path.Combine(stagingDir, "attachments");
            if (Directory.Exists(_attachmentsRoot))
            {
                CopyDirectoryRecursive(_attachmentsRoot, attachmentsStaging);
            }

            // Zip + write to final path.
            ZipFile.CreateFromDirectory(stagingDir, bakPath, CompressionLevel.Fastest, includeBaseDirectory: false);

            // Update telemetry on the BackupConfig entity.
            var size = new FileInfo(bakPath).Length;
            config.RecordBackupCompleted(_clock.UtcNow, size, bakPath);
            await db.SaveChangesAsync(ct);

            // Apply retention — delete older .dxbak files beyond the limit.
            ApplyRetention(config.SavePath, config.RetentionCount);

            return BackupResult.Success(bakPath, size);
        }
        catch (Exception ex)
        {
            return BackupResult.Failure(ex.Message);
        }
        finally
        {
            try { Directory.Delete(stagingDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    public async Task<RestoreResult> RestoreAsync(string dxbakPath, CancellationToken ct = default)
    {
        if (!File.Exists(dxbakPath))
            return RestoreResult.Failure($"Backup file not found: {dxbakPath}");

        var stagingDir = Path.Combine(Path.GetTempPath(),
            $"daftarx-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        try
        {
            ZipFile.ExtractToDirectory(dxbakPath, stagingDir);

            var manifestPath = Path.Combine(stagingDir, ManifestFileName);
            if (!File.Exists(manifestPath))
                return RestoreResult.Failure("Backup is missing manifest.json — file may be corrupted or not a DaftarX backup.");

            var manifestJson = await File.ReadAllTextAsync(manifestPath, ct);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson)
                ?? throw new InvalidOperationException("manifest.json is malformed.");

            // Cross-provider restore guard (v3 spec). The .dxbak format
            // packs a provider-specific binary; restoring a SQL Server
            // .bak onto a SQLite install would brick the install.
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var currentProvider = db.Database.ProviderName ?? "";
            if (!string.Equals(manifest.Provider, currentProvider, StringComparison.Ordinal))
            {
                return RestoreResult.Failure(
                    $"This backup is from {manifest.ProviderFriendly}. " +
                    $"Current install uses {currentProvider}. Cross-provider restore is not supported.");
            }

            // From here, the actual restore is provider-specific and
            // requires the app to be quiesced (no other connections).
            // For v1 we surface the manifest + ask the operator to
            // restart the service to complete the restore — the
            // service-shutdown + database-replace dance is a separate
            // PowerShell step. The real restore engine is a follow-on.
            return RestoreResult.PartialSuccess(
                $"Backup validated ({manifest.ProviderFriendly}, " +
                $"created {manifest.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC, " +
                $"app v{manifest.AppVersion}). " +
                "Manual restore: stop the EgyptTax service, replace the DB file, restart.");
        }
        catch (Exception ex)
        {
            return RestoreResult.Failure(ex.Message);
        }
        finally
        {
            try { Directory.Delete(stagingDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static async Task BackupSqlServerAsync(AppDbContext db, string destPath, CancellationToken ct)
    {
        // Run T-SQL BACKUP DATABASE. The destPath must be writable
        // by the SQL Server service account — staging in %TEMP%
        // requires the service to have read-permission on %TEMP%
        // which it does by default for the LocalSystem-equivalent
        // SQL Express service.
        var sql = $"BACKUP DATABASE [{db.Database.GetDbConnection().Database}] " +
                  $"TO DISK = N'{destPath.Replace("'", "''", StringComparison.Ordinal)}' " +
                  "WITH FORMAT, INIT, COMPRESSION";
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private static void BackupSqlite(AppDbContext db, string destPath)
    {
        // SQLite Online Backup API — copies a consistent snapshot
        // even while the source is being written to.
        var sourceConnection = db.Database.GetDbConnection();
        if (sourceConnection.State != System.Data.ConnectionState.Open)
        {
            sourceConnection.Open();
        }
        // Cast to Microsoft.Data.Sqlite.SqliteConnection for the
        // BackupDatabase method. EF Core 8 gives us a SqliteConnection
        // when the provider is Sqlite.
        var sqliteConn = (Microsoft.Data.Sqlite.SqliteConnection)sourceConnection;
        using var destConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={destPath}");
        destConn.Open();
        sqliteConn.BackupDatabase(destConn);
    }

    private static void CopyDirectoryRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var subdir in Directory.GetDirectories(source))
        {
            CopyDirectoryRecursive(subdir, Path.Combine(dest, Path.GetFileName(subdir)));
        }
    }

    private static void ApplyRetention(string folder, int retentionCount)
    {
        if (retentionCount < 1) return;
        try
        {
            var files = Directory.GetFiles(folder, "daftarx-backup-*.dxbak")
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();
            foreach (var old in files.Skip(retentionCount))
            {
                try { old.Delete(); } catch { /* best-effort */ }
            }
        }
        catch { /* best-effort retention; failure shouldn't poison the backup */ }
    }
}

public sealed record BackupManifest(
    string Provider,
    string ProviderFriendly,
    DateTime CreatedAtUtc,
    string AppVersion,
    string DbFileName,
    bool HasAttachments);

public sealed record BackupResult(bool Ok, string? Path, long? SizeBytes, string? Error)
{
    public static BackupResult Success(string path, long size) => new(true, path, size, null);
    public static BackupResult Failure(string err) => new(false, null, null, err);
}

public sealed record RestoreResult(bool Ok, string Message)
{
    public static RestoreResult Success(string msg) => new(true, msg);
    public static RestoreResult PartialSuccess(string msg) => new(true, msg);
    public static RestoreResult Failure(string msg) => new(false, msg);
}
