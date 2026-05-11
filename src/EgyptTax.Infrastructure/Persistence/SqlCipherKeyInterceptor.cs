using System.Data.Common;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EgyptTax.Infrastructure.Persistence;

/// <summary>
/// EF Core connection interceptor that issues <c>PRAGMA key</c> on
/// every freshly-opened SQLite connection, using the per-install
/// master key reconstructed from Shamir's shares (see
/// <c>EgyptTax.Web.Licensing.IsLicenseValid.MasterKey</c>).
///
/// Effect: the SQLite database file (<c>daftarx.db</c>) is encrypted
/// with the customer's per-install key. Copying the file to another
/// machine, or to a different user account on the same machine,
/// produces a database that won't open without the matching key —
/// and the key only reconstructs successfully on the original
/// install.
///
/// Wiring: <c>opt.UseSqlite(...)</c> followed by
/// <c>opt.AddInterceptors(new SqlCipherKeyInterceptor(provider))</c>.
/// </summary>
public sealed class SqlCipherKeyInterceptor : DbConnectionInterceptor
{
    private readonly Func<byte[]?> _masterKeyProvider;

    public SqlCipherKeyInterceptor(Func<byte[]?> masterKeyProvider)
    {
        _masterKeyProvider = masterKeyProvider;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => ApplyKey(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyKey(connection);
        return Task.CompletedTask;
    }

    private void ApplyKey(DbConnection connection)
    {
        // Linux dev container path — sentry pre-set to allow; key
        // bytes optional. SQLCipher gracefully accepts no PRAGMA key
        // (treats the DB as unencrypted) so dev mode keeps working.
        if (!LicenseSentry.IsLicensed) return;
        var key = _masterKeyProvider();
        if (key is null || key.Length == 0) return;

        // SQLCipher accepts hex-encoded keys via PRAGMA key = "x'...'";
        // safer than passing raw bytes through a string parameter.
        var hex = Convert.ToHexString(key);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA key = \"x'{hex}'\";";
        cmd.ExecuteNonQuery();
    }
}
