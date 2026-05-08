using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.Web.Tools;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Audit;

/// <summary>
/// T163 — exercises the `verify-audit` CLI verb against real
/// Testcontainers SQL by calling the static
/// <see cref="VerifyAudit.RunAsync"/> directly with captured
/// TextWriters. The host wrapper (<see cref="VerifyAuditHost"/>)
/// is a thin connection-string + DbContext factory that we don't
/// need to spin up here — the test is about the verb's logic +
/// exit codes, which the static method owns.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class VerifyAuditCliTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task FreshChain_ExitCode0_HumanOutput_ReportsValid()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedEntriesAsync(db, count: 3);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await VerifyAudit.RunAsync(
            args: BasicArgs,
            db: db,
            checkpointStore: new SqlSchemaCheckpointStore(db),
            stdout: stdout,
            stderr: stderr
        );

        exit.Should()
            .Be(
                0,
                because: "a clean chain MUST return exit code 0 — operator runbooks rely on this for scripted verification"
            );
        stdout.ToString().Should().Contain("CHAIN VALID");
    }

    [Fact]
    public async Task TamperedChain_ExitCode1_HumanOutput_ListsFinding()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedEntriesAsync(db, count: 3);

        // Tamper a payload via raw SQL (the EF entity's PayloadJson is
        // init-only, and the append-only trigger only fires on
        // app-name connections).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [audit].[audit_log] SET payload_json = {"{\"tampered\":true}"} WHERE [index] = 1"
        );

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await VerifyAudit.RunAsync(
            args: BasicArgs,
            db: db,
            checkpointStore: new SqlSchemaCheckpointStore(db),
            stdout: stdout,
            stderr: stderr
        );

        exit.Should()
            .Be(
                1,
                because: "any chain finding MUST surface as exit code 1 so a CI / nightly verification job fails loudly"
            );
        stdout.ToString().Should().Contain("CHAIN INTEGRITY FAILED");
        stdout.ToString().Should().Contain("ThisHashMismatch");
    }

    [Fact]
    public async Task JsonFlag_EmitsParseable_Json_Report()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedEntriesAsync(db, count: 2);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await VerifyAudit.RunAsync(
            args: JsonArgs,
            db: db,
            checkpointStore: new SqlSchemaCheckpointStore(db),
            stdout: stdout,
            stderr: stderr
        );

        exit.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("\"isValid\":true");
        output.Should().Contain("\"entriesScanned\":2");
        output.Should().Contain("\"findings\":[]");
    }

    [Fact]
    public async Task MaxFlag_Caps_EntriesScanned()
    {
        await using var db = await _fixture.CreateContextAsync();
        await SeedEntriesAsync(db, count: 5);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await VerifyAudit.RunAsync(
            args: JsonMaxArgs,
            db: db,
            checkpointStore: new SqlSchemaCheckpointStore(db),
            stdout: stdout,
            stderr: stderr
        );

        exit.Should().Be(0);
        stdout
            .ToString()
            .Should()
            .Contain(
                "\"entriesScanned\":3",
                because: "the --max flag MUST cap the verifier scope so operators can sample-verify large chains without paying for the full walk"
            );
        stdout.ToString().Should().Contain("\"truncated\":true");
    }

    [Fact]
    public void IsVerifyAuditInvocation_Recognises_TheVerb()
    {
        VerifyAudit.IsVerifyAuditInvocation(BasicArgs).Should().BeTrue();
        VerifyAudit
            .IsVerifyAuditInvocation(UpperCaseArgs)
            .Should()
            .BeTrue(because: "case-insensitive match is friendlier on Windows shells");
        VerifyAudit.IsVerifyAuditInvocation(SeedArgs).Should().BeFalse();
        VerifyAudit.IsVerifyAuditInvocation(Array.Empty<string>()).Should().BeFalse();
    }

    private static readonly string[] BasicArgs = { "verify-audit" };
    private static readonly string[] JsonArgs = { "verify-audit", "--json" };
    private static readonly string[] JsonMaxArgs = { "verify-audit", "--json", "--max", "3" };
    private static readonly string[] UpperCaseArgs = { "VERIFY-AUDIT" };
    private static readonly string[] SeedArgs = { "seed" };

    private static async Task SeedEntriesAsync(AppDbContext db, int count)
    {
        var store = new SqlAuditLogStore(db);
        for (var i = 1; i <= count; i++)
        {
            await store.AppendAsync(
                new AuditLogPayload(
                    Kind: $"test.event_{i}",
                    ActorUserId: Guid.NewGuid(),
                    ActorFirmName: null,
                    CompanyId: Guid.NewGuid(),
                    PayloadJson: $$"""{"i":{{i}}}"""
                )
            );
        }
    }
}
