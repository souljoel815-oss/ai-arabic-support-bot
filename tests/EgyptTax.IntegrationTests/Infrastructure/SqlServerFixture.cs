using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace EgyptTax.IntegrationTests.Infrastructure;

/// <summary>
/// Shared Testcontainers-backed SQL Server fixture per research.md R-02.
/// One container per xUnit collection; each test class call to
/// <see cref="CreateContextAsync"/> opens a fresh empty
/// <c>EgyptTax_Test_{guid}</c> database against the running container so
/// tests are isolated even though they share the engine.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Create an <see cref="AppDbContext"/> backed by a fresh database with
    /// EF migrations applied. The DB is named uniquely per call so tests
    /// don't see each other's data.
    /// </summary>
    public async Task<AppDbContext> CreateContextAsync()
    {
        // GUID format "N" yields 32 hex chars — alphanumeric only, safe to
        // interpolate into a CREATE DATABASE identifier (which can't be
        // parameterized in T-SQL anyway).
        var dbName = $"EgyptTax_Test_{Guid.NewGuid():N}";
        var baseConn = _container.GetConnectionString();
        var perTestConn = baseConn.Replace(
            "Database=master",
            $"Database={dbName}",
            StringComparison.OrdinalIgnoreCase
        );

        // Bootstrap the database against master.
        var bootstrap = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(baseConn).Options;
        await using (var bootstrapCtx = new AppDbContext(bootstrap))
        {
#pragma warning disable EF1002 // dbName is a server-generated GUID, not user input; CREATE DATABASE doesn't accept parameters anyway.
            await bootstrapCtx.Database.ExecuteSqlRawAsync(
                $"IF DB_ID('{dbName}') IS NULL CREATE DATABASE [{dbName}];"
            );
#pragma warning restore EF1002
        }

        // Open a per-test context against the new database; apply migrations.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(perTestConn).Options;
        var ctx = new AppDbContext(options);
        await ctx.Database.MigrateAsync();
        return ctx;
    }
}

/// <summary>
/// xUnit collection definition that pins the SQL Server container to a
/// single instance shared across every test class in the collection.
/// </summary>
#pragma warning disable CA1711 // "Collection" suffix is the xUnit-documented naming convention.
[CollectionDefinition(SqlServerCollection.Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
#pragma warning restore CA1711
