using EgyptTax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgyptTax.ContractTests.Api;

/// <summary>
/// T075 — the OpenAPI document served by the running application MUST
/// match <c>contracts/api/openapi.yaml</c> byte-for-byte (modulo line
/// endings) so consumers fetching the spec from a deployed instance
/// see exactly the contract committed to the repo. The contract file
/// is the source of truth — Swashbuckle-style generated docs would
/// drift from it.
/// </summary>
public class OpenApiAlignmentTests : IClassFixture<EgyptTaxContractTestFactory>
{
    private readonly EgyptTaxContractTestFactory _factory;

    public OpenApiAlignmentTests(EgyptTaxContractTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ServedOpenApiYaml_MatchesContractFileByteForByte()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi.yaml", UriKind.Relative));
        response
            .IsSuccessStatusCode.Should()
            .BeTrue(because: $"GET /openapi.yaml MUST succeed; got {(int)response.StatusCode}");

        response
            .Content.Headers.ContentType?.MediaType.Should()
            .Be("application/yaml", because: "the served OpenAPI MUST advertise application/yaml");

        var served = NormalizeLineEndings(await response.Content.ReadAsStringAsync());

        var contractPath = Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "api",
            "openapi.yaml"
        );
        File.Exists(contractPath)
            .Should()
            .BeTrue(
                because: $"the contract file MUST be copied alongside the test binary; expected at {contractPath}"
            );
        var canonical = NormalizeLineEndings(await File.ReadAllTextAsync(contractPath));

        served
            .Should()
            .Be(
                canonical,
                because: "served OpenAPI MUST be the canonical contract — no drift permitted"
            );
    }

    [Fact]
    public async Task ServedOpenApiYaml_DeclaresHealthLiveAndReadyEndpoints()
    {
        using var client = _factory.CreateClient();
        var served = await client.GetStringAsync(new Uri("/openapi.yaml", UriKind.Relative));

        served
            .Should()
            .Contain(
                "/health/live",
                because: "T073 health endpoints MUST be declared in the served contract"
            );
        served.Should().Contain("/health/ready");
    }

    private static string NormalizeLineEndings(string s) =>
        s.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
}

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> for ContractTests.
/// Swaps the production SQL Server <c>AppDbContext</c> for an in-memory
/// EF provider so the host boots without a real database.
/// </summary>
public sealed class EgyptTaxContractTestFactory : WebApplicationFactory<Program>
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
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("ContractTests"));
        });
    }
}
