using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Quotations;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Quotations;

/// <summary>
/// L1.5 (v3 roadmap) — application orchestration for the quotation
/// aggregate. The Razor pages call into here for the operations that
/// touch more than one row (Send → allocate per-year sequence;
/// Convert → create a SalesInvoice from the lines + record the link).
/// Pure CRUD on a single Quotation row stays inline in the page
/// using AppDbContext directly, same as the rest of the app.
/// </summary>
public sealed class QuotationService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public QuotationService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Draft → Sent. Allocates the per-year sequence number by
    /// counting existing Sent/Accepted/Rejected/Expired quotations
    /// in the same calendar year + 1. Race-conditions can mint
    /// duplicate numbers under concurrent sends; acceptable for a
    /// non-fiscal document and rare in a single-tenant install.
    /// </summary>
    public async Task SendAsync(Guid quotationId, CancellationToken ct = default)
    {
        var quotation = await _db.Set<Quotation>()
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quotationId, ct)
            ?? throw new InvalidOperationException($"Quotation {quotationId} not found.");

        var year = quotation.DocumentDate.Year;
        // Count quotations from this year that already received a
        // number (i.e. anything past Draft). Materialise the count
        // server-side; SQLite handles int aggregation fine.
        var existingThisYear = await _db.Set<Quotation>()
            .CountAsync(q => q.QuotationNumber != null
                && q.DocumentDate.Year == year, ct);

        quotation.MarkSent(existingThisYear + 1, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Accepted → SalesInvoice (Draft). Creates the invoice via the
    /// regular SalesInvoice.CreateDraft factory + AddLine for each
    /// quotation line. Operator reviews + posts the invoice
    /// normally — this method does NOT post. Returns the new
    /// SalesInvoice id so the caller can navigate.
    /// </summary>
    public async Task<Guid> ConvertToInvoiceAsync(
        Guid quotationId,
        Guid? createdByUserId,
        CancellationToken ct = default)
    {
        var quotation = await _db.Set<Quotation>()
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quotationId, ct)
            ?? throw new InvalidOperationException($"Quotation {quotationId} not found.");

        if (quotation.State != QuotationState.Accepted)
            throw new InvalidOperationException(
                $"Quotation {quotationId} is in state {quotation.State}; only Accepted quotes convert.");
        if (quotation.ConvertedToInvoiceId is not null)
            throw new InvalidOperationException(
                $"Quotation {quotationId} was already converted to invoice {quotation.ConvertedToInvoiceId}.");

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var invoice = SalesInvoice.CreateDraft(
            customerId: quotation.CustomerId,
            customerTaxProfileSnapshot: quotation.CustomerTaxProfileSnapshot,
            documentDate: today,
            createdByUserId: createdByUserId);

        foreach (var line in quotation.Lines)
        {
            invoice.AddLine(
                itemId: line.ItemId,
                quantity: line.Quantity,
                unitPrice: line.UnitPrice,
                vatCategoryId: line.VatCategoryId,
                vatRatePercent: line.VatRatePercent);
        }

        _db.Add(invoice);
        quotation.RecordConvertedToInvoice(invoice.Id, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return invoice.Id;
    }

    /// <summary>
    /// Daily-firing expiry sweep — invoked by the Hangfire job.
    /// Marks all Sent quotations whose ValidUntilDate has passed
    /// as Expired. Idempotent: subsequent calls find nothing.
    /// </summary>
    public async Task<int> SweepExpiredAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var stale = await _db.Set<Quotation>()
            .Where(q => q.State == QuotationState.Sent && q.ValidUntilDate < today)
            .ToListAsync(ct);

        if (stale.Count == 0) return 0;

        var now = _clock.UtcNow;
        foreach (var q in stale)
        {
            q.MarkExpired(now);
        }
        await _db.SaveChangesAsync(ct);
        return stale.Count;
    }
}
