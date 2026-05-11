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
            ?? Environment.GetEnvironmentVariable("EGYPTTAX_CONNECTION")
            ?? PortableDefaults.DefaultSqliteConnection();
        var provider = DatabaseProviderDetector.Detect(connectionString);
        PortableDefaults.EnsureSqliteDirectory(connectionString, provider);

        var optsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        if (provider == DatabaseProvider.Sqlite)
        {
            optsBuilder.UseSqlite(connectionString);
        }
        else
        {
            optsBuilder.UseSqlServer(connectionString);
        }
        await using var db = new AppDbContext(optsBuilder.Options);

        // Bootstrap schema. SQL Server install runs the migration
        // pipeline (MigrateAsync — idempotent). SQLite single-file
        // mode skips migrations (provider-specific migrations don't
        // exist for SQLite) and uses EnsureCreatedAsync — sufficient
        // for a clean install where the file doesn't yet exist.
        try
        {
            if (provider == DatabaseProvider.Sqlite)
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }
            else
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await Console
                .Error.WriteLineAsync(
                    $"[seed] Failed to bootstrap schema: {ex.Message}"
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
