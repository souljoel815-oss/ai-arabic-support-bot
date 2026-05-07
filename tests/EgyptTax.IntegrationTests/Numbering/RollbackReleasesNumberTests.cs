using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Numbering;

/// <summary>
/// FR-011 invariant: a number is consumed only when posting succeeds
/// end-to-end. A failed or rolled-back posting attempt MUST NOT
/// consume a number, and a subsequent successful post MUST receive
/// the same number that the failed attempt would have received.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class RollbackReleasesNumberTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Rollback_ThenAllocate_AssignsSameNumber()
    {
        await using var db = await _fixture.CreateContextAsync();
        var connectionString = db.Database.GetConnectionString()!;

        // Attempt 1 — allocate, then ROLLBACK.
        string firstAssigned;
        await using (var bagDb = NewContext(connectionString))
        await using (var tx = await bagDb.Database.BeginTransactionAsync())
        {
            var allocator = new SqlSequentialNumberAllocator(bagDb);
            firstAssigned = await allocator.AllocateAsync(DocumentType.SalesInvoice, 2026);
            await tx.RollbackAsync();
        }

        // Attempt 2 — fresh transaction, allocate, COMMIT. Should reuse the
        // number released by the rollback.
        string secondAssigned;
        await using (var bagDb = NewContext(connectionString))
        await using (var tx = await bagDb.Database.BeginTransactionAsync())
        {
            var allocator = new SqlSequentialNumberAllocator(bagDb);
            secondAssigned = await allocator.AllocateAsync(DocumentType.SalesInvoice, 2026);
            await tx.CommitAsync();
        }

        firstAssigned.Should().Be(secondAssigned, because:
            "FR-011 mandates a number is consumed only on successful end-to-end post; the rollback releases it");
        secondAssigned.Should().Be("INV-2026-000001");
    }

    [Fact]
    public async Task Rollback_DoesNotPersistAllocatorRow_FreshDatabase()
    {
        await using var db = await _fixture.CreateContextAsync();
        var connectionString = db.Database.GetConnectionString()!;

        // Allocate then rollback — the allocator row insertion should also
        // roll back, leaving the table empty for that (series, year).
        await using (var bagDb = NewContext(connectionString))
        await using (var tx = await bagDb.Database.BeginTransactionAsync())
        {
            var allocator = new SqlSequentialNumberAllocator(bagDb);
            await allocator.AllocateAsync(DocumentType.SalesInvoice, 2099);
            await tx.RollbackAsync();
        }

        // Direct query: no allocator row for fiscal_year=2099 should exist.
        var rowCount = await db.Database
            .SqlQuery<int>($@"SELECT COUNT(*) AS [Value] FROM [numbering].[document_number_allocator] WHERE [fiscal_year] = {2099}")
            .FirstAsync();
        rowCount.Should().Be(0);
    }

    private static AppDbContext NewContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
