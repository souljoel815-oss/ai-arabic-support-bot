using System.Globalization;
using EgyptTax.Application.Numbering;
using EgyptTax.Domain.Numbering;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Numbering;

/// <summary>
/// FR-011 / R-06 — atomic upsert + counter-bump for the per-(series,
/// fiscal_year) allocator row. The allocator runs inside whatever EF
/// Core transaction is open on its <see cref="AppDbContext"/>; per
/// FR-011, callers MUST wrap the allocation + the post in a single
/// transaction so a rolled-back post releases the consumed number.
///
/// Provider-aware: SQL Server uses the <c>MERGE WITH (HOLDLOCK)</c>
/// idiom (HOLDLOCK + range lock prevents phantom inserts on the first
/// call for a new fiscal year). SQLite uses
/// <c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c>, which is
/// atomic at the statement level and serialises naturally under
/// SQLite's single-writer model — sufficient for the single-process
/// portable mode this code path runs in.
/// </summary>
public sealed class SqlSequentialNumberAllocator(AppDbContext db) : IDocumentNumberAllocator
{
    private readonly AppDbContext _db = db;

    public async Task<string> AllocateAsync(
        DocumentType type,
        int fiscalYear,
        CancellationToken cancellationToken = default
    )
    {
        var series =
            await _db.Set<DocumentSeries>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.DocumentType == type, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No DocumentSeries seeded for document type {type}."
            );

        var assigned = await UpsertAndBumpAsync(series.Id, fiscalYear, cancellationToken);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{series.Code}-{fiscalYear:D4}-{assigned:D6}"
        );
    }

    private async Task<int> UpsertAndBumpAsync(Guid seriesId, int fiscalYear, CancellationToken ct)
    {
        // Positional {0}/{1} placeholders so EF Core's provider
        // generates the correct DbParameter type (SqlParameter for
        // SQL Server, SqliteParameter for SQLite). Hard-coding
        // SqlParameter here breaks portable-mode posts with
        // "Unable to cast object of type 'SqlParameter' to type
        // 'SqliteParameter'" — see the May 2026 testing report.
        if (_db.Database.IsSqlite())
        {
            // RETURNING gives us the *new* (post-bump) value: on the
            // INSERT path it returns 2 (the seeded NextNumber), on the
            // UPDATE path it returns previous+1. Subtract 1 to get
            // back to the assigned number (1 / previous).
            const string SqliteUpsertSql =
                @"
INSERT INTO document_number_allocator (series_id, fiscal_year, next_number)
VALUES ({0}, {1}, 2)
ON CONFLICT(series_id, fiscal_year)
DO UPDATE SET next_number = next_number + 1
RETURNING next_number;";

            var newNext = await _db
                .Database.SqlQueryRaw<int>(SqliteUpsertSql, seriesId, fiscalYear)
                .ToListAsync(ct);
            return newNext.Single() - 1;
        }

        // MERGE WITH (HOLDLOCK) is the SQL-Server-recommended
        // atomic-upsert pattern. The OUTPUT clause returns the
        // assigned (= pre-update or bootstrapped 1) value back to
        // the caller in a single round-trip.
        const string MergeSql =
            @"
MERGE INTO [numbering].[document_number_allocator] WITH (HOLDLOCK) AS target
USING (SELECT {0} AS series_id, {1} AS fiscal_year) AS src
ON target.series_id = src.series_id AND target.fiscal_year = src.fiscal_year
WHEN MATCHED THEN
    UPDATE SET next_number = next_number + 1
WHEN NOT MATCHED THEN
    INSERT (series_id, fiscal_year, next_number) VALUES (src.series_id, src.fiscal_year, 2)
OUTPUT
    CASE WHEN $action = N'UPDATE' THEN deleted.next_number ELSE 1 END AS [Value];";

        var assigned = await _db
            .Database.SqlQueryRaw<int>(MergeSql, seriesId, fiscalYear)
            .ToListAsync(ct);

        return assigned.Single();
    }
}
