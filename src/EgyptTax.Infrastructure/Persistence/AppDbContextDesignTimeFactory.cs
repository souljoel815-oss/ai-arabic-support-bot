using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EgyptTax.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> CLI tools to instantiate <see cref="AppDbContext"/>
/// at design time. Reads the connection string from environment variable
/// <c>EGYPTTAX_EF_CONNECTION</c> when set; otherwise falls back to a local
/// SQL Express instance reachable via Shared Memory (<c>lpc:</c> protocol)
/// per the dev-environment convention. The connection is only used by
/// <c>dotnet ef migrations</c> tooling — at runtime the application wires
/// its own DbContextOptions.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnection =
        @"Server=lpc:.\SQLEXPRESS02;Database=EgyptTax_Tooling;Trusted_Connection=Yes;TrustServerCertificate=Yes;";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("EGYPTTAX_EF_CONNECTION")
            ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
