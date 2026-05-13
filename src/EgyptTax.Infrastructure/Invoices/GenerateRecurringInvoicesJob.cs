using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Invoices;

/// <summary>
/// L3 (v3 roadmap) — daily Hangfire job. Finds active recurring
/// invoice templates whose NextRunDate is today or earlier and
/// generates a Draft SalesInvoice from each one. Always Draft —
/// the operator posts manually after review.
///
/// Idempotent inside a single calendar day: a template that's
/// already had its LastGeneratedDate set to today is skipped, so
/// running the job twice on the same day doesn't double-generate.
/// </summary>
public sealed class GenerateRecurringInvoicesJob
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public GenerateRecurringInvoicesJob(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var due = await _db.Set<RecurringInvoiceTemplate>()
            .Include(t => t.Lines)
            .Where(t => t.IsActive
                && t.NextRunDate <= today
                && (t.LastGeneratedDate == null || t.LastGeneratedDate < today))
            .ToListAsync(cancellationToken);

        var generated = 0;
        foreach (var t in due)
        {
            // Snapshot the customer's tax profile at generation time
            // — matches the SalesInvoice.CreateDraft contract.
            var customer = await _db.Set<Customer>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == t.CustomerId, cancellationToken);
            if (customer is null)
            {
                // Customer deleted; pause the template and move on.
                t.Pause();
                continue;
            }
            var draft = SalesInvoice.CreateDraft(
                customerId: t.CustomerId,
                customerTaxProfileSnapshot: customer.TaxProfile,
                documentDate: today,
                createdByUserId: t.CreatedByUserId);
            foreach (var l in t.Lines)
            {
                draft.AddLine(l.ItemId, l.Quantity, l.UnitPrice, l.VatCategoryId, l.VatRatePercent);
            }
            _db.Add(draft);
            t.RecordGeneration(today);
            generated++;
        }
        if (generated > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        return generated;
    }
}
