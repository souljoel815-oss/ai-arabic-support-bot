using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Audit;

/// <summary>
/// FR-028 / SC-010 — synthetic tamper-case suite. The verifier is required
/// to detect every category of tampering listed in
/// <c>contracts/audit-chain-verifier.md §"Required tests"</c>. Each test
/// appends a known-good chain, captures a checkpoint, then mutates the
/// chain or checkpoint to simulate one tamper category, and asserts the
/// verifier returns the expected finding kind at the expected index.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class AuditChainTamperTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    private static AuditLogPayload Payload(int i) =>
        new(
            Kind: "Synthetic",
            ActorUserId: Guid.NewGuid(),
            ActorFirmName: null,
            CompanyId: Guid.NewGuid(),
            PayloadJson: $$"""{"i":{{i}}}"""
        );

    private static async Task<List<AuditLogEntry>> SeedAsync(
        SqlAuditLogStore store,
        AppDbContext db,
        int count = 5
    )
    {
        for (var i = 1; i <= count; i++)
        {
            await store.AppendAsync(Payload(i));
        }
        return await db.Set<AuditLogEntry>().AsNoTracking().OrderBy(e => e.Index).ToListAsync();
    }

    private static AuditCheckpoint CheckpointFor(IReadOnlyList<AuditLogEntry> entries) =>
        new(entries[^1].Index, entries[^1].ThisHash, entries[^1].TsUtc);

    [Fact]
    public async Task Case1_CleanLog_ReturnsValid()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db);
        var checkpoint = CheckpointFor(entries);

        var report = AuditChainVerifier.Verify(entries, checkpoint);

        report.IsValid.Should().BeTrue();
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Case2_InsertAtMid_ReportsPrevHashMismatch()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db);
        var checkpoint = CheckpointFor(entries);

        // Insert a forged row in between index 2 and 3 (chain breaks at original index 3).
        // Braces in JSON payload doubled to {{ / }} so ExecuteSqlRawAsync's
        // composite-format-string parser doesn't read them as parameter
        // placeholders.
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [audit].[audit_log] ([index], [ts_utc], [actor_user_id], [actor_firm_name], [company_id], [kind], [payload_json], [prev_hash], [this_hash])
            VALUES (99, SYSUTCDATETIME(), NEWID(), NULL, NEWID(), 'Forged', '{{"x":1}}', 0x0000000000000000000000000000000000000000000000000000000000000000, 0x0000000000000000000000000000000000000000000000000000000000000000)
            """
        );

        var tampered = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .ToListAsync();

        var report = AuditChainVerifier.Verify(tampered, checkpoint);

        report.IsValid.Should().BeFalse();
        report.Findings.Should().Contain(f => f.Kind == AuditChainFindingKind.PrevHashMismatch);
    }

    [Fact]
    public async Task Case3_EditAtMid_ReportsThisHashMismatch()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db);
        var checkpoint = CheckpointFor(entries);

        // Mutate the payload of entry 3 without recomputing the hash.
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE [audit].[audit_log] SET [payload_json] = '{{"i":3,"tampered":true}}' WHERE [index] = 3
            """
        );

        var tampered = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .ToListAsync();

        var report = AuditChainVerifier.Verify(tampered, checkpoint);

        report.IsValid.Should().BeFalse();
        report
            .Findings.Should()
            .Contain(f => f.Kind == AuditChainFindingKind.ThisHashMismatch && f.AtIndex == 3);
    }

    [Fact]
    public async Task Case4_DeleteAtMid_ReportsMissingIndex()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db);
        var checkpoint = CheckpointFor(entries);

        await db.Database.ExecuteSqlRawAsync("DELETE FROM [audit].[audit_log] WHERE [index] = 3");

        var tampered = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .ToListAsync();

        var report = AuditChainVerifier.Verify(tampered, checkpoint);

        report.IsValid.Should().BeFalse();
        // The verifier reports MissingIndex at the EXPECTED (missing) index,
        // not the next observed index — so deleting row 3 surfaces AtIndex=3.
        report
            .Findings.Should()
            .Contain(f => f.Kind == AuditChainFindingKind.MissingIndex && f.AtIndex == 3);
    }

    [Fact]
    public async Task Case5_ReorderTwoEntries_ReportsPrevHashMismatch()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db);
        var checkpoint = CheckpointFor(entries);

        // Swap rows 2 and 3 by flipping their indices via a temp value.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [audit].[audit_log] SET [index] = -2 WHERE [index] = 2"
        );
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [audit].[audit_log] SET [index] = 2 WHERE [index] = 3"
        );
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [audit].[audit_log] SET [index] = 3 WHERE [index] = -2"
        );

        var tampered = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .ToListAsync();

        var report = AuditChainVerifier.Verify(tampered, checkpoint);

        report.IsValid.Should().BeFalse();
        report.Findings.Should().Contain(f => f.Kind == AuditChainFindingKind.PrevHashMismatch);
    }

    [Fact]
    public async Task Case6_TailTruncation_DetectedViaCheckpoint()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db, count: 7);
        var checkpoint = CheckpointFor(entries);

        // Truncate the last 3 entries (indices 5, 6, 7) — the chain head 1..4
        // remains internally consistent, so without the checkpoint the
        // verifier would (wrongly) report valid.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [audit].[audit_log] WHERE [index] >= 5");

        var tampered = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .ToListAsync();
        tampered.Should().HaveCount(4);

        var report = AuditChainVerifier.Verify(tampered, checkpoint);

        report.IsValid.Should().BeFalse();
        report
            .Findings.Should()
            .Contain(f =>
                f.Kind == AuditChainFindingKind.TailTruncation && f.AtIndex == checkpoint.LastIndex
            );
    }

    [Fact]
    public async Task Case7_CheckpointStaleButTailIntact_ReportsCheckpointMismatch()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db);
        var checkpoint = CheckpointFor(entries);

        // Edit the LAST entry's payload but leave the chain intact:
        // recompute the hash so it links forward correctly. Result is a
        // chain that internally re-verifies clean, but its head hash no
        // longer matches the captured checkpoint hash.
        var newPayload = "{\"i\":5,\"oh\":\"no\"}";
        var newHash = AuditChainHasher.ComputeHash(newPayload, entries[^1].PrevHash);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE [audit].[audit_log] SET [payload_json] = @p0, [this_hash] = @p1 WHERE [index] = 5",
            newPayload,
            newHash
        );

        var tampered = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .ToListAsync();

        var report = AuditChainVerifier.Verify(tampered, checkpoint);

        report.IsValid.Should().BeFalse();
        report
            .Findings.Should()
            .Contain(f => f.Kind == AuditChainFindingKind.CheckpointMismatch && f.AtIndex == 5);
    }

    [Fact]
    public async Task Case8_CheckpointTamperedOnly_ReportsCheckpointMismatch()
    {
        await using var db = await _fixture.CreateContextAsync();
        var store = new SqlAuditLogStore(db);
        var entries = await SeedAsync(store, db);

        // Forge a checkpoint whose hash doesn't match the actual head — this
        // simulates someone tampering with the checkpoint store contents
        // (file or table) while leaving the chain itself intact.
        var forgedHash = new byte[32];
        forgedHash[0] = 0xDE;
        forgedHash[1] = 0xAD;
        var forgedCheckpoint = new AuditCheckpoint(
            LastIndex: 5,
            LastHash: forgedHash,
            TsUtc: DateTime.UtcNow
        );

        var report = AuditChainVerifier.Verify(entries, forgedCheckpoint);

        report.IsValid.Should().BeFalse();
        report
            .Findings.Should()
            .Contain(f => f.Kind == AuditChainFindingKind.CheckpointMismatch && f.AtIndex == 5);
    }
}
