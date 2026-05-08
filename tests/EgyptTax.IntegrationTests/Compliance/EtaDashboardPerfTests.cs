using System.Data;
using System.Diagnostics;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Eta;
using EgyptTax.Infrastructure.Eta;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Compliance;

/// <summary>
/// T083 — SC-012 perf bar: the ETA Compliance Dashboard MUST identify
/// invoices whose submission window expires within 24 h on a 50,000-
/// document database in &lt; 2 s. Seeds 50,000 EtaSubmission rows
/// (with parent SalesInvoice headers — minimum-viable column set so
/// the seed satisfies NOT-NULL constraints) via SqlBulkCopy, then
/// runs the dashboard query and asserts both correctness (right
/// rows returned) and the perf bar.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Slow")]
public class EtaDashboardPerfTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task SC012_50kDocs_LessThan2Seconds_FindsCorrectUpcomingDeadlines()
    {
        await using var db = await _fixture.CreateContextAsync();

        var connectionString = db.Database.GetConnectionString()!;
        var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

        // Seed: 50,000 EtaSubmission rows with a randomized but
        // controlled distribution. The expected count for the
        // 24h-window dashboard query is computed deterministically so
        // the assertion is exact — not a sampled estimate.
        var expectedHits = await SeedFiftyThousandAsync(connectionString, nowUtc);

        var queryUnderTest = new SqlEtaDashboardQuery(db);

        // Warm up the connection pool / plan cache so the timed run
        // measures the steady-state query, not the first-hit overhead.
        _ = await queryUnderTest.GetUpcomingDeadlinesAsync(
            TimeSpan.FromHours(24),
            nowUtc,
            CancellationToken.None
        );

        var sw = Stopwatch.StartNew();
        var rows = await queryUnderTest.GetUpcomingDeadlinesAsync(
            TimeSpan.FromHours(24),
            nowUtc,
            CancellationToken.None
        );
        sw.Stop();

        // Correctness: only Pending + Failed rows whose deadline is in
        // [nowUtc, nowUtc + 24h). Submitted rows (terminal) are
        // excluded; rows with deadlines beyond the window are excluded.
        rows.Count.Should()
            .Be(
                expectedHits,
                because: "the dashboard MUST return exactly the rows whose status is non-terminal and whose deadline is within the 24h lookahead"
            );

        rows.Should()
            .OnlyContain(r =>
                r.Status == EtaSubmissionStatus.Pending || r.Status == EtaSubmissionStatus.Failed
            );
        rows.Should()
            .OnlyContain(r =>
                r.SubmissionWindowExpiresAtUtc >= nowUtc
                && r.SubmissionWindowExpiresAtUtc < nowUtc.AddHours(24)
            );

        // Ordering: dashboard surfaces "most-urgent first" — ascending deadline.
        rows.Should().BeInAscendingOrder(r => r.SubmissionWindowExpiresAtUtc);

        // SC-012 perf bar.
        sw.Elapsed.Should()
            .BeLessThan(
                TimeSpan.FromSeconds(2),
                because: $"SC-012 — dashboard MUST complete in < 2 s on 50k docs; observed {sw.ElapsedMilliseconds} ms"
            );
    }

    [Fact]
    public async Task EmptyDatabase_DashboardReturnsEmpty_Quickly()
    {
        await using var db = await _fixture.CreateContextAsync();
        var query = new SqlEtaDashboardQuery(db);

        var sw = Stopwatch.StartNew();
        var rows = await query.GetUpcomingDeadlinesAsync(
            TimeSpan.FromHours(24),
            DateTime.UtcNow,
            CancellationToken.None
        );
        sw.Stop();

        rows.Should().BeEmpty();
        sw.Elapsed.Should()
            .BeLessThan(
                TimeSpan.FromMilliseconds(500),
                because: "an empty table query MUST return promptly via the index seek"
            );
    }

    [Fact]
    public async Task NegativeOrZeroLookahead_Throws()
    {
        await using var db = await _fixture.CreateContextAsync();
        var query = new SqlEtaDashboardQuery(db);

        var act = () =>
            query.GetUpcomingDeadlinesAsync(TimeSpan.Zero, DateTime.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Seeds 50,000 minimum-viable SalesInvoice + EtaSubmission rows
    /// via two SqlBulkCopy passes. Returns the count of rows that
    /// SHOULD be matched by the 24h-window dashboard query —
    /// computed deterministically by the seeder so the test assertion
    /// is exact rather than a sampling estimate.
    /// </summary>
    private static async Task<int> SeedFiftyThousandAsync(string connectionString, DateTime nowUtc)
    {
        const int totalRows = 50_000;
        var random = new Random(42);

        var invoiceTable = NewSalesInvoicesTable();
        var submissionTable = NewEtaSubmissionsTable();

        var expectedHits = 0;

        for (var i = 0; i < totalRows; i++)
        {
            var invoiceId = Guid.NewGuid();
            var submissionId = Guid.NewGuid();

            // Distribute statuses + deadlines so the dashboard query
            // has to filter:
            //   * 60% Pending — half within 24h, half outside
            //   * 20% Failed — half within 24h, half outside
            //   * 20% Submitted — split evenly across deadline buckets
            //     (these MUST be excluded regardless of deadline)
            var bucket = random.Next(100);
            EtaSubmissionStatus status;
            DateTime deadline;
            bool inWindow;

            if (bucket < 60)
            {
                status = EtaSubmissionStatus.Pending;
                inWindow = (i & 1) == 0;
            }
            else if (bucket < 80)
            {
                status = EtaSubmissionStatus.Failed;
                inWindow = (i & 1) == 0;
            }
            else
            {
                status = EtaSubmissionStatus.Submitted;
                inWindow = (i & 1) == 0;
            }

            if (inWindow)
            {
                // Random offset in [0, 24h) — strictly inside the window
                // boundaries so the half-open [now, now+24h) check is
                // unambiguous.
                deadline = nowUtc.AddSeconds(random.Next(60, 23 * 60 * 60));
            }
            else
            {
                // Either past or beyond the window. Past is also excluded
                // because the dashboard's lower bound is nowUtc.
                deadline =
                    (i & 2) == 0
                        ? nowUtc.AddDays(-1).AddSeconds(random.Next(60, 60 * 60))
                        : nowUtc.AddDays(2).AddSeconds(random.Next(60, 23 * 60 * 60));
            }

            if (inWindow && status != EtaSubmissionStatus.Submitted)
            {
                expectedHits++;
            }

            AppendSalesInvoiceRow(invoiceTable, invoiceId, nowUtc);
            AppendEtaSubmissionRow(
                submissionTable,
                submissionId,
                invoiceId,
                status,
                deadline,
                nowUtc
            );
        }

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Bulk insert SalesInvoice headers first so the eta_submissions
        // 1:1 unique index has matching parent ids; FK is not declared
        // at SQL level so insert order is purely logical, but keeping it
        // ordered makes the seed easier to reason about.
        using (
            var bulk = new SqlBulkCopy(conn)
            {
                DestinationTableName = "documents.sales_invoices",
                BulkCopyTimeout = 120,
            }
        )
        {
            MapColumns(bulk, invoiceTable);
            await bulk.WriteToServerAsync(invoiceTable);
        }

        using (
            var bulk = new SqlBulkCopy(conn)
            {
                DestinationTableName = "eta.eta_submissions",
                BulkCopyTimeout = 120,
            }
        )
        {
            MapColumns(bulk, submissionTable);
            await bulk.WriteToServerAsync(submissionTable);
        }

        return expectedHits;
    }

    private static void MapColumns(SqlBulkCopy bulk, DataTable table)
    {
        foreach (DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }
    }

    private static DataTable NewSalesInvoicesTable()
    {
        var t = new DataTable();
        t.Columns.Add("id", typeof(Guid));
        t.Columns.Add("customer_id", typeof(Guid));
        t.Columns.Add("document_date", typeof(DateTime));
        t.Columns.Add("state", typeof(string));
        t.Columns.Add("document_number", typeof(string));
        t.Columns.Add("posted_at_utc", typeof(DateTime));
        t.Columns.Add("posted_by_user_id", typeof(Guid));
        t.Columns.Add("posting_mode", typeof(string));
        t.Columns.Add("customer_tax_profile_snapshot_type", typeof(string));
        t.Columns.Add("customer_tax_profile_snapshot_tin", typeof(string));
        t.Columns.Add("customer_tax_profile_snapshot_vat_exemption", typeof(bool));
        t.Columns.Add("customer_tax_profile_snapshot_default_sales_vat_category_id", typeof(Guid));
        t.Columns.Add("subtotal", typeof(decimal));
        t.Columns.Add("invoice_level_discount_amount", typeof(decimal));
        t.Columns.Add("invoice_level_discount_percent", typeof(decimal));
        t.Columns.Add("net_before_vat", typeof(decimal));
        t.Columns.Add("vat_total", typeof(decimal));
        t.Columns.Add("grand_total", typeof(decimal));
        return t;
    }

    private static void AppendSalesInvoiceRow(DataTable table, Guid invoiceId, DateTime nowUtc)
    {
        var row = table.NewRow();
        row["id"] = invoiceId;
        row["customer_id"] = Guid.Empty;
        row["document_date"] = nowUtc.Date;
        row["state"] = "Posted";
        row["document_number"] = $"INV-2026-{invoiceId.GetHashCode():X8}";
        row["posted_at_utc"] = nowUtc;
        row["posted_by_user_id"] = Guid.Empty;
        row["posting_mode"] = "UnapprovedDirect";
        row["customer_tax_profile_snapshot_type"] = "B2BRegistered";
        row["customer_tax_profile_snapshot_tin"] = "987654321";
        row["customer_tax_profile_snapshot_vat_exemption"] = false;
        row["customer_tax_profile_snapshot_default_sales_vat_category_id"] = Guid.Empty;
        row["subtotal"] = 1000m;
        row["invoice_level_discount_amount"] = 0m;
        row["invoice_level_discount_percent"] = 0m;
        row["net_before_vat"] = 1000m;
        row["vat_total"] = 140m;
        row["grand_total"] = 1140m;
        table.Rows.Add(row);
    }

    private static DataTable NewEtaSubmissionsTable()
    {
        var t = new DataTable();
        t.Columns.Add("id", typeof(Guid));
        t.Columns.Add("sales_invoice_id", typeof(Guid));
        t.Columns.Add("status", typeof(string));
        t.Columns.Add("submission_uuid", typeof(string));
        t.Columns["submission_uuid"]!.AllowDBNull = true;
        t.Columns.Add("last_attempt_at_utc", typeof(DateTime));
        t.Columns["last_attempt_at_utc"]!.AllowDBNull = true;
        t.Columns.Add("attempt_count", typeof(int));
        t.Columns.Add("error_code", typeof(string));
        t.Columns["error_code"]!.AllowDBNull = true;
        t.Columns.Add("error_message", typeof(string));
        t.Columns["error_message"]!.AllowDBNull = true;
        t.Columns.Add("submission_window_expires_at_utc", typeof(DateTime));
        t.Columns.Add("created_at_utc", typeof(DateTime));
        return t;
    }

    private static void AppendEtaSubmissionRow(
        DataTable table,
        Guid id,
        Guid salesInvoiceId,
        EtaSubmissionStatus status,
        DateTime deadlineUtc,
        DateTime nowUtc
    )
    {
        var row = table.NewRow();
        row["id"] = id;
        row["sales_invoice_id"] = salesInvoiceId;
        row["status"] = status.ToString();
        row["submission_uuid"] =
            status == EtaSubmissionStatus.Submitted
                ? (object)Guid.NewGuid().ToString("D")
                : DBNull.Value;
        row["last_attempt_at_utc"] =
            status == EtaSubmissionStatus.Pending ? DBNull.Value : (object)nowUtc.AddMinutes(-5);
        row["attempt_count"] = status == EtaSubmissionStatus.Pending ? 0 : 1;
        row["error_code"] = status == EtaSubmissionStatus.Failed ? (object)"SIM-001" : DBNull.Value;
        row["error_message"] =
            status == EtaSubmissionStatus.Failed
                ? (object)"Simulated transient failure"
                : DBNull.Value;
        row["submission_window_expires_at_utc"] = deadlineUtc;
        row["created_at_utc"] = nowUtc.AddDays(-2);
        table.Rows.Add(row);
    }
}
