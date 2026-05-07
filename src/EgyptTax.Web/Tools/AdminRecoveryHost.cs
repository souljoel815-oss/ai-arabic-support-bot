using EgyptTax.Application.Identity;
using EgyptTax.Infrastructure.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EgyptTax.Web.Tools;

/// <summary>
/// FR-038 — minimal host for the <c>recover-admin</c> CLI. Boots a
/// stripped-down configuration + DI graph (just the EF context, the
/// password hasher, and the system clock) so the recovery operation
/// does not depend on the full Web pipeline. The audit chain emission
/// happens later via <see cref="AdminRecoveryDrainer"/> on next
/// regular application start.
/// </summary>
internal static class AdminRecoveryHost
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets(typeof(AdminRecoveryHost).Assembly, optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("EgyptTax")
            ?? configuration["ConnectionStrings:EgyptTax"]
            ?? Environment.GetEnvironmentVariable("EGYPTTAX_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync(
                "recover-admin: no connection string found. Set ConnectionStrings:EgyptTax in appsettings or EGYPTTAX_CONNECTION env var.")
                .WaitAsync(cancellationToken);
            return 4;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var db = new AppDbContext(options);

        return await AdminRecover.RunAsync(
            args,
            db,
            new Argon2idPasswordHasher(),
            new SystemClock(),
            Console.Out,
            Console.Error,
            cancellationToken);
    }
}
