using System.Data;
using System.Diagnostics;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Audit;

/// <summary>
/// SC-010 perf gate: chain-verification of a 1,000,000-entry audit log MUST
/// complete in under 30 seconds. The per-row <see cref="EgyptTax.Infrastructure.Audit.SqlAuditLogStore"/>
/// path would take many minutes to seed 1M rows (each call opens a
/// transaction + locks the tail), so this test computes the entire chain
/// in-memory and bulk-inserts via <see cref="SqlBulkCopy"/>. The verifier
/// itself is the unit under test, not the seeder.
///
/// This is a SLOW test (~2-5 minutes including the seed) — categorised as
/// such so CI / local runs can filter when needed.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Slow")]
public class AuditChainPerformanceTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    private const int RowCount = 1_000_000;

    [Fact]
    public async Task Verify_OneMillionEntries_CompletesInUnder30Seconds()
    {
        // Arrange — seed 1M chained entries via bulk insert.
        await using var db = await _fixture.CreateContextAsync();
        var connectionString = db.Database.GetConnectionString()!;

        var seedStopwatch = Stopwatch.StartNew();
        await BulkSeedChainedEntriesAsync(connectionString, RowCount);
        seedStopwatch.Stop();

        // Sanity-load the rows. AsNoTracking + an explicit projection-free
        // read is the cheapest way to materialise the entire log.
        var loadStopwatch = Stopwatch.StartNew();
        var entries = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .ToListAsync();
        loadStopwatch.Stop();

        entries.Should().HaveCount(RowCount);

        // Act — measure ONLY the verifier; SC-010 is about the verifier
        // throughput, not seed/load.
        var verifyStopwatch = Stopwatch.StartNew();
        var report = AuditChainVerifier.Verify(entries);
        verifyStopwatch.Stop();

        // Assert.
        report.IsValid.Should().BeTrue($"clean chain of {RowCount} entries should verify clean");
        report.Findings.Should().BeEmpty();
        verifyStopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(30),
            $"SC-010 requires < 30 s for 1,000,000 entries; verify took {verifyStopwatch.Elapsed.TotalSeconds:F1} s "
            + $"(seed {seedStopwatch.Elapsed.TotalSeconds:F1} s, load {loadStopwatch.Elapsed.TotalSeconds:F1} s)");
    }

    /// <summary>
    /// Compute hashes in a tight in-memory loop (chain dependency makes this
    /// inherently sequential) and stream rows into <c>audit.audit_log</c>
    /// via SqlBulkCopy in batches. Bypasses <c>SqlAuditLogStore.AppendAsync</c>
    /// because the per-row transaction overhead would dominate seeding time
    /// to the point of making this test impractical.
    /// </summary>
    private static async Task BulkSeedChainedEntriesAsync(string connectionString, int rowCount)
    {
        var table = BuildEmptyAuditLogTable();
        var prevHash = AuditChainHasher.GenesisHash;
        var ts = DateTime.UtcNow;
        var companyId = Guid.NewGuid();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = "[audit].[audit_log]",
            BatchSize = 50_000,
            BulkCopyTimeout = 120,
        };
        MapBulkColumns(bulk);

        const int BatchSize = 50_000;
        for (var i = 1; i <= rowCount; i++)
        {
            var payload = $$"""{"i":{{i}}}""";
            var thisHash = AuditChainHasher.ComputeHash(payload, prevHash);

            var row = table.NewRow();
            row["index"] = (long)i;
            row["ts_utc"] = ts;
            row["actor_user_id"] = DBNull.Value;
            row["actor_firm_name"] = DBNull.Value;
            row["company_id"] = companyId;
            row["kind"] = "PerfSeed";
            row["payload_json"] = payload;
            row["prev_hash"] = prevHash;
            row["this_hash"] = thisHash;
            table.Rows.Add(row);

            prevHash = thisHash;

            if (table.Rows.Count >= BatchSize)
            {
                await bulk.WriteToServerAsync(table);
                table.Clear();
            }
        }

        if (table.Rows.Count > 0)
        {
            await bulk.WriteToServerAsync(table);
        }
    }

    private static DataTable BuildEmptyAuditLogTable()
    {
        var t = new DataTable();
        t.Columns.Add("index", typeof(long));
        t.Columns.Add("ts_utc", typeof(DateTime));
        t.Columns.Add("actor_user_id", typeof(Guid));
        t.Columns.Add("actor_firm_name", typeof(string));
        t.Columns.Add("company_id", typeof(Guid));
        t.Columns.Add("kind", typeof(string));
        t.Columns.Add("payload_json", typeof(string));
        t.Columns.Add("prev_hash", typeof(byte[]));
        t.Columns.Add("this_hash", typeof(byte[]));

        // Allow nulls on actor columns (they're nullable in the schema).
        t.Columns["actor_user_id"]!.AllowDBNull = true;
        t.Columns["actor_firm_name"]!.AllowDBNull = true;
        return t;
    }

    private static void MapBulkColumns(SqlBulkCopy bulk)
    {
        bulk.ColumnMappings.Add("index", "index");
        bulk.ColumnMappings.Add("ts_utc", "ts_utc");
        bulk.ColumnMappings.Add("actor_user_id", "actor_user_id");
        bulk.ColumnMappings.Add("actor_firm_name", "actor_firm_name");
        bulk.ColumnMappings.Add("company_id", "company_id");
        bulk.ColumnMappings.Add("kind", "kind");
        bulk.ColumnMappings.Add("payload_json", "payload_json");
        bulk.ColumnMappings.Add("prev_hash", "prev_hash");
        bulk.ColumnMappings.Add("this_hash", "this_hash");
    }
}
