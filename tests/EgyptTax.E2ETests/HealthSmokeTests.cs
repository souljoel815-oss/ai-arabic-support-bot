using System.Net;
using System.Net.Http.Json;
using EgyptTax.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgyptTax.E2ETests;

/// <summary>
/// T076 — application boots end-to-end and serves the FR-028 / R-23
/// readiness probe with a 200 status when the database is reachable
/// (here, the EF in-memory provider). Asserts the JSON shape declared
/// in <c>contracts/api/openapi.yaml#HealthReady</c>: status, db
/// (reachable + latencyMs), ntpSkewSeconds, auditCheckpoint
/// (mode + writable + lastIndex + lastWrittenAt). This is the
/// minimum smoke proving Stage 2 is wired end-to-end.
/// </summary>
public class HealthSmokeTests : IClassFixture<EgyptTaxE2EFactory>
{
    private readonly EgyptTaxE2EFactory _factory;

    public HealthSmokeTests(EgyptTaxE2EFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Live_Returns200_WithStatusUp()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/health/live", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LiveResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("up");
        body.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ready_Returns200_WhenDbReachable_AndAuditCheckpointWritable()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative));
        response
            .StatusCode.Should()
            .Be(
                HttpStatusCode.OK,
                because: "the InMemory EF provider is reachable and the checkpoint store is writable"
            );

        var body = await response.Content.ReadFromJsonAsync<ReadyResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("ready");
        body.Db.Reachable.Should().BeTrue();
        body.AuditCheckpoint.Writable.Should().BeTrue();
        body.AuditCheckpoint.Mode.Should().Be("table");
    }

    private sealed record LiveResponse(string Status, string Version);

    private sealed record ReadyResponse(
        string Status,
        DbBlock Db,
        int NtpSkewSeconds,
        AuditCheckpointBlock AuditCheckpoint
    );

    private sealed record DbBlock(bool Reachable, int LatencyMs);

    private sealed record AuditCheckpointBlock(
        string Mode,
        bool Writable,
        long LastIndex,
        DateTime LastWrittenAt
    );
}

/// <summary>
/// Shared E2E factory — swaps the SQL Server-bound <c>AppDbContext</c>
/// for an in-memory provider so the host boots without a real database.
/// Each test class gets a fresh logical database so cross-class state
/// leakage cannot mask a regression.
/// </summary>
public sealed class EgyptTaxE2EFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var existing = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                .ToList();
            foreach (var d in existing)
            {
                services.Remove(d);
            }
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase($"E2E_{Guid.NewGuid():N}")
            );
        });
    }
}
