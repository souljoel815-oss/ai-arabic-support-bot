using System.Globalization;
using EgyptTax.Application.Numbering;
using EgyptTax.Domain.Numbering;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Numbering;

/// <summary>
/// FR-011 / R-06 — atomic upsert + counter-bump using SQL Server's
/// <c>MERGE WITH (HOLDLOCK)</c> idiom. Concurrent allocators serialize
/// on the (series, fiscal_year) row via the HOLDLOCK range lock
/// (which prevents phantom inserts on the first call for a new
/// fiscal year). The allocator runs inside whatever EF Core
/// transaction is open on its <see cref="AppDbContext"/>; per
/// FR-011, callers MUST wrap the allocation + the post in a single
/// transaction so a rolled-back post releases the consumed number.
/// </summary>
public sealed class SqlSequentialNumberAllocator(AppDbContext db) : IDocumentNumberAllocator
{
    private readonly AppDbContext _db = db;

    public async Task<string> AllocateAsync(
        DocumentType type,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        var series = await _db.Set<DocumentSeries>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DocumentType == type, cancellationToken)
            ?? throw new InvalidOperationException($"No DocumentSeries seeded for document type {type}.");

        var assigned = await UpsertAndBumpAsync(series.Id, fiscalYear, cancellationToken);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{series.Code}-{fiscalYear:D4}-{assigned:D6}");
    }

    private async Task<int> UpsertAndBumpAsync(Guid seriesId, int fiscalYear, CancellationToken ct)
    {
        // MERGE WITH (HOLDLOCK) is the SQL-Server-recommended atomic-upsert
        // pattern. The OUTPUT clause returns the assigned (= pre-update or
        // bootstrapped 1) value back to the caller in a single round-trip.
        const string MergeSql = @"
MERGE INTO [numbering].[document_number_allocator] WITH (HOLDLOCK) AS target
USING (SELECT @SeriesId AS series_id, @FiscalYear AS fiscal_year) AS src
ON target.series_id = src.series_id AND target.fiscal_year = src.fiscal_year
WHEN MATCHED THEN
    UPDATE SET next_number = next_number + 1
WHEN NOT MATCHED THEN
    INSERT (series_id, fiscal_year, next_number) VALUES (src.series_id, src.fiscal_year, 2)
OUTPUT
    CASE WHEN $action = N'UPDATE' THEN deleted.next_number ELSE 1 END AS [Value];";

        var assigned = await _db.Database
            .SqlQueryRaw<int>(
                MergeSql,
                new SqlParameter("@SeriesId", seriesId),
                new SqlParameter("@FiscalYear", fiscalYear))
            .ToListAsync(ct);

        return assigned.Single();
    }
}
