using EgyptTax.Application.Identity;
using EgyptTax.Infrastructure.Identity;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EgyptTax.Web.Tools;

/// <summary>
/// T072 — minimal host for the <c>seed</c> CLI verb. Boots a stripped-
/// down configuration + a single <see cref="AppDbContext"/> + the
/// production password hasher, then dispatches to <see cref="Seeder"/>.
/// Mirrors the <c>recover-admin</c> host pattern so the two CLI surfaces
/// share an idiom: short-circuit before the web host builds, run, exit.
/// </summary>
internal static class SeederHost
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        // Read config from the directory the .exe lives in (NOT the
        // current working directory). Required for the MSI install
        // path: msiexec launches the deferred CA exe from msiexec's
        // own cwd (typically C:\Windows\System32), but the
        // appsettings*.json files ship + are written next to
        // EgyptTax.Web.exe at C:\Program Files\EgyptTax\.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
                optional: true
            )
            .AddEnvironmentVariables()
            .AddUserSecrets(typeof(SeederHost).Assembly, optional: true)
            .Build();

        var connectionString =
            configuration.GetConnectionString("EgyptTax")
            ?? configuration["ConnectionStrings:EgyptTax"]
            ?? Environment.GetEnvironmentVariable("EGYPTTAX_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console
                .Error.WriteLineAsync(
                    "[seed] No connection string found. Set ConnectionStrings:EgyptTax in appsettings or EGYPTTAX_CONNECTION env var."
                )
                .WaitAsync(cancellationToken);
            return 4;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var db = new AppDbContext(options);

        // Apply migrations BEFORE seeding so a fresh install (no
        // database, or no schema) bootstraps cleanly. MigrateAsync
        // is idempotent — no-ops when the schema is up to date —
        // so it's safe to always run regardless of the
        // --apply-migrations flag (which is preserved as an arg
        // for documentation purposes + future fine-grained control).
        // This is the path the MSI's ApplyMigrationsAndSeed custom
        // action takes; without it, MSI install on a machine with
        // SQL Server reachable but no EgyptTax database fails with
        // SQL error 4060 "Cannot open database" before seeding can
        // even start.
        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await Console
                .Error.WriteLineAsync(
                    $"[seed] Failed to apply EF migrations: {ex.Message}"
                )
                .WaitAsync(cancellationToken);
            return 5;
        }

        return await Seeder.RunAsync(
            args,
            db,
            new Argon2idPasswordHasher(),
            Console.Out,
            Console.Error,
            cancellationToken
        );
    }
}
