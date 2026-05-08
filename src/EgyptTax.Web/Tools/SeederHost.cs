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
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
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
