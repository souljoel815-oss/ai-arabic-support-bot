using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Audit;

/// <summary>
/// FR-028 happy-path: append a few audit entries, run the verifier, observe
/// "valid" with no findings. Validates the SHA-256 chain produces a clean
/// state when no tampering has occurred and that the indices are
/// monotonically increasing per the spec.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AuditChainHappyPathTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Append_Then_Verify_CleanLog_ReturnsValid()
    {
        // Arrange — fresh database for this test class.
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);

        // Act — append three audit entries.
        await store.AppendAsync(new AuditLogPayload(
            Kind: "DocumentPosted",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"document":"INV-2026-000001","total":1140}"""));

        await store.AppendAsync(new AuditLogPayload(
            Kind: "FieldChanged",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: "Test Firm LLP",
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"field":"customer_name","before":"A","after":"B"}"""));

        await store.AppendAsync(new AuditLogPayload(
            Kind: "LoginSucceeded",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"ip":"127.0.0.1"}"""));

        // Assert — verifier reports clean.
        var entries = await db.Set<AuditLogEntry>()
            .OrderBy(e => e.Index)
            .ToListAsync();
        entries.Should().HaveCount(3);
        entries[0].Index.Should().Be(1);
        entries[1].Index.Should().Be(2);
        entries[2].Index.Should().Be(3);

        var report = AuditChainVerifier.Verify(entries);
        report.IsValid.Should().BeTrue();
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Append_AssignsMonotonicallyIncreasingIndex()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);

        for (var i = 0; i < 5; i++)
        {
            await store.AppendAsync(new AuditLogPayload(
                Kind: "TestEntry",
                ActorUserId: Guid.NewGuid(),
                ActorFirmName: null,
                CompanyId: Guid.NewGuid(),
                PayloadJson: $$"""{"i":{{i}}}"""));
        }

        var indices = await db.Set<AuditLogEntry>()
            .OrderBy(e => e.Index)
            .Select(e => e.Index)
            .ToListAsync();

        indices.Should().Equal(new long[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task Append_ChainsHashesAcrossEntries()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);

        await store.AppendAsync(new AuditLogPayload(
            Kind: "First",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"x":1}"""));

        await store.AppendAsync(new AuditLogPayload(
            Kind: "Second",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: """{"x":2}"""));

        var entries = await db.Set<AuditLogEntry>()
            .OrderBy(e => e.Index)
            .ToListAsync();

        // The first entry's PrevHash is all zeros (genesis).
        entries[0].PrevHash.Should().BeEquivalentTo(new byte[32]);

        // The second entry's PrevHash equals the first entry's ThisHash.
        entries[1].PrevHash.Should().BeEquivalentTo(entries[0].ThisHash);
    }
}
