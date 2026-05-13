using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Sales;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Sales;

/// <summary>
/// Phase F — convert a Confirmed <see cref="SalesOrder"/> into a
/// Draft <see cref="SalesInvoice"/>. Copies the customer, the lines,
/// and stamps the invoice's CreatedByUserId with the converter (so
/// the office user — not the rep — owns the resulting invoice for
/// per-rep visibility scoping). Marks the order Converted with a
/// back-pointer so it can't be re-converted.
///
/// The converted invoice is created in Draft state — the office still
/// needs to Post it (which runs credit-limit + stock + ETA path).
/// That's intentional: the order is the rep's intent, posting is the
/// office's commitment.
/// </summary>
public sealed class ConvertSalesOrderToInvoiceHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public ConvertSalesOrderToInvoiceHandler(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SalesInvoice> HandleAsync(
        Guid salesOrderId,
        Guid convertedByUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.Set<SalesOrder>()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Sales order {salesOrderId} not found.");

        if (order.State != SalesOrderState.Confirmed)
            throw new InvalidOperationException(
                $"Only Confirmed orders can be converted. {order.OrderNumber} is {order.State}.");

        var customer = await _db.Set<Customer>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == order.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Customer {order.CustomerId} no longer exists; cannot convert order {order.OrderNumber}.");

        var nowUtc = _clock.UtcNow;
        var creator = convertedByUserId == Guid.Empty ? (Guid?)null : convertedByUserId;
        var draft = SalesInvoice.CreateDraft(
            customerId: order.CustomerId,
            customerTaxProfileSnapshot: customer.TaxProfile,
            documentDate: DateOnly.FromDateTime(nowUtc),
            createdByUserId: creator);
        foreach (var line in order.Lines)
        {
            draft.AddLine(
                line.ItemId,
                line.Quantity,
                line.UnitPrice,
                line.VatCategoryId,
                line.VatRatePercent);
        }
        _db.Add(draft);

        order.MarkConverted(draft.Id, nowUtc);
        await _db.SaveChangesAsync(cancellationToken);
        return draft;
    }
}
