using System.Data.Common;

namespace EgyptTax.Web.Tools;

/// <summary>
/// Defaults for the single-file portable mode. When no connection
/// string is configured we point at a SQLite file beside the EXE so
/// the customer's first double-click "just works" with zero setup.
/// </summary>
public static class PortableDefaults
{
    /// <summary>
    /// Default location for the portable SQLite database file.
    /// Beside the EXE on Windows so the install folder owns its data
    /// (and a quick copy = a backup); under ~/.daftarx on other
    /// platforms.
    /// </summary>
    public static string DefaultSqliteConnection()
    {
        // Beside the EXE on Windows so the install folder owns its
        // data. For single-file apps AppContext.BaseDirectory points
        // at the extraction temp dir (rotates per build), so we use
        // Environment.ProcessPath which is the real EXE location.
        // Fallback to AppContext.BaseDirectory for the
        // dotnet-run / non-single-file dev case.
        string dir;
        if (OperatingSystem.IsWindows())
        {
            var exePath = Environment.ProcessPath;
            dir = !string.IsNullOrEmpty(exePath)
                ? Path.GetDirectoryName(exePath)!
                : AppContext.BaseDirectory;
        }
        else
        {
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".daftarx");
        }
        var path = Path.Combine(dir, "daftarx.db");
        return $"Data Source={path}";
    }

    /// <summary>
    /// SQLite errors out cryptically (SQLITE_CANTOPEN) if the directory
    /// the DB file lives in doesn't exist yet. Create it eagerly here
    /// so the first connection attempt succeeds on a clean machine.
    /// </summary>
    public static void EnsureSqliteDirectory(string? connectionString, DatabaseProvider provider)
    {
        if (provider != DatabaseProvider.Sqlite || string.IsNullOrWhiteSpace(connectionString))
            return;
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (!builder.TryGetValue("Data Source", out var raw)) return;
            var path = raw?.ToString();
            if (string.IsNullOrWhiteSpace(path)) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch
        {
            // Best-effort — if we can't create the directory the DB
            // open will fail with a more useful error downstream.
        }
    }
}
