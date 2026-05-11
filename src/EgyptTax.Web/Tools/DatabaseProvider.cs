namespace EgyptTax.Web.Tools;

/// <summary>
/// Single-file/portable mode picks the SQLite provider; the on-prem
/// Windows Service install picks SQL Server. The choice is driven by
/// the connection string shape rather than a separate env var so both
/// flavours can coexist in the same binary.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    Sqlite,
}

public static class DatabaseProviderDetector
{
    /// <summary>
    /// Returns Sqlite when the connection string starts with "Data Source="
    /// and contains either ".db" or ".sqlite" (so a stray
    /// "Server=...; Data Source=..." SQL Server string still picks
    /// SqlServer). Defaults to SqlServer for backwards-compatibility.
    /// </summary>
    public static DatabaseProvider Detect(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return DatabaseProvider.SqlServer;
        var trimmed = connectionString.TrimStart();
        if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProvider.Sqlite;
        }
        return DatabaseProvider.SqlServer;
    }
}
