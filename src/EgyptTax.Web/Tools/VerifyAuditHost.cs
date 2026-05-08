using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EgyptTax.Web.Tools;

/// <summary>
/// T163 — minimal host for the <c>verify-audit</c> CLI verb.
/// Mirrors the <see cref="SeederHost"/> + <see cref="AdminRecoveryHost"/>
/// pattern: build a stripped-down configuration, open one
/// <see cref="AppDbContext"/> + the SQL-schema-mode checkpoint
/// store, dispatch to <see cref="VerifyAudit"/>, exit.
///
/// We default to the SQL-schema-mode checkpoint store because it
/// shares the same connection string as the audit chain itself
/// (the file-mode store needs an extra `--checkpoint-file` flag
/// to find the right path; that variant lands when an operator
/// explicitly asks for it).
/// </summary>
internal static class VerifyAuditHost
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets(typeof(VerifyAuditHost).Assembly, optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("EgyptTax")
            ?? configuration["ConnectionStrings:EgyptTax"]
            ?? Environment.GetEnvironmentVariable("EGYPTTAX_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync(
                "[verify-audit] No connection string found. Set ConnectionStrings:EgyptTax in appsettings or EGYPTTAX_CONNECTION env var.")
                .WaitAsync(cancellationToken);
            return 3;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var db = new AppDbContext(options);
        var checkpointStore = new SqlSchemaCheckpointStore(db);

        return await VerifyAudit.RunAsync(
            args, db, checkpointStore, Console.Out, Console.Error, cancellationToken);
    }
}
