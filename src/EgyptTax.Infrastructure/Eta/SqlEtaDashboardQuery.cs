using Dapper;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Eta;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// FR-035 / SC-012 — Dapper-backed implementation of the dashboard
/// query. Uses raw SQL (rather than EF's LINQ provider) because the
/// query plan is the load-bearing piece of the SC-012 perf bar:
/// "ETA dashboard correctly identifies &lt; 24 h-deadline invoices in
/// &lt; 2 s on a 50 k-doc database". The SQL deliberately filters on
/// a leading equality (status &lt;&gt; Submitted, expressed as IN
/// because Pending and Failed are the only non-terminal values) plus
/// a range on submission_window_expires_at_utc, so the
/// <c>ix_eta_submissions_dashboard</c> covering index handles the
/// entire query without a heap lookup.
/// </summary>
public sealed class SqlEtaDashboardQuery(AppDbContext db) : IEtaDashboardQuery
{
    // T-SQL flavour: schema-qualified [brackets], N'literal' for
    // nvarchar. Targets the ix_eta_submissions_dashboard covering
    // index for the SC-012 < 2 s perf bar at 50 k rows.
    private const string SqlSqlServer =
        @"
SELECT
    [sales_invoice_id]                  AS SalesInvoiceId,
    [id]                                AS EtaSubmissionId,
    [status]                            AS StatusRaw,
    [submission_window_expires_at_utc]  AS SubmissionWindowExpiresAtUtc,
    [attempt_count]                     AS AttemptCount,
    [error_code]                        AS LastErrorCode
FROM [eta].[eta_submissions]
WHERE [status] IN (N'Pending', N'Failed')
  AND [submission_window_expires_at_utc] >= @nowUtc
  AND [submission_window_expires_at_utc] <  @cutoffUtc
ORDER BY [submission_window_expires_at_utc] ASC;";

    // SQLite flavour: no schema (EF Core SQLite drops the schema
    // qualifier when mapping ToTable(..., schema: ""eta"")); no
    // [brackets] or N'' literal prefix. Same index name, same
    // semantics — SQLite picks the index on its own based on
    // selectivity, no covering index hint needed.
    private const string SqlSqlite =
        @"
SELECT
    sales_invoice_id                    AS SalesInvoiceId,
    id                                  AS EtaSubmissionId,
    status                              AS StatusRaw,
    submission_window_expires_at_utc    AS SubmissionWindowExpiresAtUtc,
    attempt_count                       AS AttemptCount,
    error_code                          AS LastErrorCode
FROM eta_submissions
WHERE status IN ('Pending', 'Failed')
  AND submission_window_expires_at_utc >= @nowUtc
  AND submission_window_expires_at_utc <  @cutoffUtc
ORDER BY submission_window_expires_at_utc ASC;";

    private readonly AppDbContext _db = db;

    private string ResolveSql() =>
        _db.Database.IsSqlite() ? SqlSqlite : SqlSqlServer;

    public async Task<IReadOnlyList<EtaDashboardRow>> GetUpcomingDeadlinesAsync(
        TimeSpan within,
        DateTime nowUtc,
        CancellationToken cancellationToken = default
    )
    {
        if (within <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(within),
                "Lookahead window must be strictly positive."
            );
        }

        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var rows = await connection.QueryAsync<DapperRow>(
            new CommandDefinition(
                ResolveSql(),
                parameters: new { nowUtc, cutoffUtc = nowUtc.Add(within) },
                cancellationToken: cancellationToken
            )
        );

        return rows.Select(r => new EtaDashboardRow(
                SalesInvoiceId: r.SalesInvoiceId,
                EtaSubmissionId: r.EtaSubmissionId,
                Status: Enum.Parse<EtaSubmissionStatus>(r.StatusRaw),
                SubmissionWindowExpiresAtUtc: r.SubmissionWindowExpiresAtUtc,
                AttemptCount: r.AttemptCount,
                LastErrorCode: r.LastErrorCode
            ))
            .ToList()
            .AsReadOnly();
    }

    private sealed class DapperRow
    {
        public Guid SalesInvoiceId { get; set; }
        public Guid EtaSubmissionId { get; set; }
        public string StatusRaw { get; set; } = default!;
        public DateTime SubmissionWindowExpiresAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public string? LastErrorCode { get; set; }
    }
}
