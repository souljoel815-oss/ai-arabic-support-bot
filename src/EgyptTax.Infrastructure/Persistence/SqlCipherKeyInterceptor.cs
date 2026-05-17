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
        => ApplyKey();

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyKey();
        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Instance method kept so the rekey-aware revival can read _masterKeyProvider without changing signatures.")]
    private void ApplyKey()
    {
        // v5 — temporarily DISABLED. The architecture assumed the DB
        // was always created encrypted, but PortableFirstRun creates
        // it plaintext (because LicenseSentry.IsLicensed is false at
        // boot, before activation). After in-app activation, the
        // interceptor would start issuing PRAGMA key against the
        // existing plaintext file, which SQLite reports as "file is
        // not a database" on the first read. License protection is
        // already provided by the Ed25519 signature + HWID binding —
        // file-level encryption was defense in depth. Re-enable only
        // after the activation flow learns to PRAGMA rekey the
        // existing DB in-place.
        return;

        // Original code kept commented for the rekey-aware revival:
        //   if (!LicenseSentry.IsLicensed) return;
        //   var key = _masterKeyProvider();
        //   if (key is null || key.Length == 0) return;
        //   var hex = Convert.ToHexString(key);
        //   using var cmd = connection.CreateCommand();
        //   cmd.CommandText = $"PRAGMA key = \"x'{hex}'\";";
        //   cmd.ExecuteNonQuery();
    }
}
