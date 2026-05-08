using System.Globalization;
using EgyptTax.Application.Numbering;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Numbering;

/// <summary>
/// SC-006: zero gaps and zero duplicates in document numbering within
/// any (series, fiscal year) window, validated against a stress run of
/// 500 concurrent posting attempts spanning a fiscal-year boundary;
/// numbering correctly resets to 1 on the first posting of the new
/// fiscal year.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Slow")]
public class ConcurrentPostingStressTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task FiveHundred_ConcurrentAllocations_SingleYear_ProduceGapFreeUniqueNumbers()
    {
        await using var db = await _fixture.CreateContextAsync();
        var connectionString = db.Database.GetConnectionString()!;

        const int Count = 500;
        const int FiscalYear = 2026;

        var allocateTasks = Enumerable
            .Range(0, Count)
            .Select(_ =>
                Task.Run(async () =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlServer(connectionString)
                        .Options;
                    await using var localDb = new AppDbContext(options);
                    await using var tx = await localDb.Database.BeginTransactionAsync();
                    var allocator = new SqlSequentialNumberAllocator(localDb);
                    var number = await allocator.AllocateAsync(
                        DocumentType.SalesInvoice,
                        FiscalYear
                    );
                    await tx.CommitAsync();
                    return number;
                })
            )
            .ToArray();

        var assigned = await Task.WhenAll(allocateTasks);

        assigned.Should().HaveCount(Count);
        assigned.Should().OnlyHaveUniqueItems();

        // Parse the trailing 6-digit suffix from each formatted number; expect 1..Count.
        var numericSuffixes = assigned
            .Select(s => int.Parse(s.Split('-').Last(), CultureInfo.InvariantCulture))
            .OrderBy(n => n)
            .ToArray();
        numericSuffixes.Should().Equal(Enumerable.Range(1, Count));

        // All should share the canonical INV-2026-NNNNNN shape.
        assigned.Should().AllSatisfy(s => s.Should().MatchRegex(@"^INV-2026-\d{6}$"));
    }

    [Fact]
    public async Task FiveHundred_ConcurrentAllocations_AcrossFiscalYearBoundary_EachYearGapFreeIndependent()
    {
        await using var db = await _fixture.CreateContextAsync();
        var connectionString = db.Database.GetConnectionString()!;

        const int CountPerYear = 250;

        async Task<string[]> AllocateAsync(int year, int count)
        {
            var tasks = Enumerable
                .Range(0, count)
                .Select(_ =>
                    Task.Run(async () =>
                    {
                        var options = new DbContextOptionsBuilder<AppDbContext>()
                            .UseSqlServer(connectionString)
                            .Options;
                        await using var localDb = new AppDbContext(options);
                        await using var tx = await localDb.Database.BeginTransactionAsync();
                        var allocator = new SqlSequentialNumberAllocator(localDb);
                        var number = await allocator.AllocateAsync(DocumentType.SalesInvoice, year);
                        await tx.CommitAsync();
                        return number;
                    })
                )
                .ToArray();
            return await Task.WhenAll(tasks);
        }

        // Run 2025 + 2026 concurrently — both fiscal-year buckets must remain gap-free.
        var year2025Task = AllocateAsync(2025, CountPerYear);
        var year2026Task = AllocateAsync(2026, CountPerYear);
        await Task.WhenAll(year2025Task, year2026Task);

        var year2025 = year2025Task.Result;
        var year2026 = year2026Task.Result;

        year2025.Should().AllSatisfy(s => s.Should().StartWith("INV-2025-"));
        year2026.Should().AllSatisfy(s => s.Should().StartWith("INV-2026-"));

        var year2025Suffixes = year2025
            .Select(s => int.Parse(s.Split('-').Last(), CultureInfo.InvariantCulture))
            .OrderBy(n => n)
            .ToArray();
        var year2026Suffixes = year2026
            .Select(s => int.Parse(s.Split('-').Last(), CultureInfo.InvariantCulture))
            .OrderBy(n => n)
            .ToArray();

        year2025Suffixes
            .Should()
            .Equal(
                Enumerable.Range(1, CountPerYear),
                because: "2025 fiscal-year numbering must be 1..N gap-free"
            );
        year2026Suffixes
            .Should()
            .Equal(
                Enumerable.Range(1, CountPerYear),
                because: "2026 fiscal-year numbering must be 1..N gap-free, independent of 2025"
            );
    }
}
