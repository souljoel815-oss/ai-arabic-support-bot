using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Audit;

/// <summary>
/// T162 — exercises the same composition path the
/// `POST /api/v1/audit/verify` endpoint uses (load entries
/// ordered by index → call `AuditChainVerifier.Verify` with
/// the latest checkpoint), against a real Testcontainers SQL.
/// Skips the HTTP layer because that path is identical for any
/// minimal-API endpoint and the test harness already proves it
/// in HealthSmokeTests; what we want here is regression coverage
/// for the verifier composition the endpoint embeds.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AuditVerifyEndpointPathTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task EndpointPath_Returns_IsValid_True_For_FreshChain()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);

        // Append 3 entries via the production store so the chain
        // produces real hashes the verifier can re-compute.
        for (var i = 1; i <= 3; i++)
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

        // Same composition the endpoint runs.
        var entries = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .Take(50_000)
            .ToListAsync();
        var report = AuditChainVerifier.Verify(entries, checkpoint: null);

        report
            .IsValid.Should()
            .BeTrue(
                because: "the chain produced by SqlAuditLogStore MUST verify clean — that's the FR-028 invariant"
            );
        report.Findings.Should().BeEmpty();
        entries.Count.Should().Be(3);
    }

    [Fact]
    public async Task EndpointPath_Surfaces_Findings_When_Entry_Is_Tampered()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);

        await store.AppendAsync(
            new AuditLogPayload(
                Kind: "test.original",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: """{"original":true}"""
            )
        );
        await store.AppendAsync(
            new AuditLogPayload(
                Kind: "test.next",
                ActorUserId: null,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: """{"next":true}"""
            )
        );

        // Tamper the first entry's payload via raw SQL. Use
        // ExecuteSqlInterpolatedAsync so the JSON braces don't get
        // mistaken for string.Format placeholders, and pass the new
        // payload as a parameter (also avoids any quoting drama).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [audit].[audit_log] SET payload_json = {"{\"tampered\":true}"} WHERE [index] = 1"
        );

        var entries = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .Take(50_000)
            .ToListAsync();
        var report = AuditChainVerifier.Verify(entries, checkpoint: null);

        report
            .IsValid.Should()
            .BeFalse(
                because: "tampered payload no longer hashes to the stored ThisHash — verifier MUST surface the discrepancy"
            );
        report
            .Findings.Should()
            .Contain(f => f.Kind == AuditChainFindingKind.ThisHashMismatch && f.AtIndex == 1);
        // The tamper at index 1 also breaks index 2's prev-hash linkage
        // because the verifier walks expected-prev-hash forward from
        // genesis using ENTRY's PrevHash; index 2's PrevHash is still
        // the original index-1 ThisHash so it stays consistent with
        // the recorded chain — which is exactly why ThisHashMismatch
        // at the tampered row is the precise signal.
    }
}
