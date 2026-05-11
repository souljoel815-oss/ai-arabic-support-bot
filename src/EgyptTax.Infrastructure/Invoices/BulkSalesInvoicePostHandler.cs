using EgyptTax.Application.Invoices;
using EgyptTax.Application.Invoices.Bulk;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Invoices;

/// <summary>
/// G1.2 — processes a batch of parsed <see cref="BulkSalesInvoiceRow"/>
/// from the Excel upload. For each row:
///   1. Resolve customer / item / VAT category by code (pre-loaded
///      once for the batch — O(1) lookups per row).
///   2. Build a single-line <see cref="SalesInvoice"/> draft.
///   3. Save the draft + delegate to the existing
///      <see cref="PostSalesInvoiceWithEtaSubmissionHandler"/> for
///      numbering / JE / ETA submission.
///   4. Yield the outcome immediately so the UI can stream
///      progress to the operator (good UX at 50-500 rows).
///
/// Each row is its own SaveChanges transaction — one bad row
/// doesn't roll back the whole batch. The operator can re-upload
/// just the failed rows after fixing the source.
/// </summary>
public sealed class BulkSalesInvoicePostHandler
{
    private readonly AppDbContext _db;
    private readonly PostSalesInvoiceWithEtaSubmissionHandler _postHandler;

    public BulkSalesInvoicePostHandler(
        AppDbContext db,
        PostSalesInvoiceWithEtaSubmissionHandler postHandler)
    {
        _db = db;
        _postHandler = postHandler;
    }

    public async IAsyncEnumerable<BulkPostOutcome> PostAllAsync(
        IReadOnlyList<BulkSalesInvoiceRow> rows,
        Guid postedByUserId,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) yield break;

        var customerCodes = rows.Select(r => r.CustomerCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var itemCodes = rows.Select(r => r.ItemCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var vatCodes = rows.Select(r => r.VatCategoryCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var customers = await _db.Set<Customer>()
            .Where(c => customerCodes.Contains(c.Code))
            .ToDictionaryAsync(c => c.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var items = await _db.Set<Item>()
            .Where(i => itemCodes.Contains(i.Code))
            .ToDictionaryAsync(i => i.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var vatCategories = await _db.Set<VatCategory>()
            .Where(v => vatCodes.Contains(v.Code))
            .ToDictionaryAsync(v => v.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in rows)
        {
            BulkPostOutcome outcome;
            try
            {
                outcome = await PostOneAsync(row, customers, items, vatCategories, postedByUserId, cancellationToken);
            }
            catch (Exception ex)
            {
                outcome = new BulkPostOutcome(
                    LineNumber: row.LineNumber,
                    Success: false,
                    DocumentNumber: null,
                    SalesInvoiceId: null,
                    ErrorMessage: ex.Message);
            }
            yield return outcome;
        }
    }

    private async Task<BulkPostOutcome> PostOneAsync(
        BulkSalesInvoiceRow row,
        Dictionary<string, Customer> customers,
        Dictionary<string, Item> items,
        Dictionary<string, VatCategory> vatCategories,
        Guid postedByUserId,
        System.Threading.CancellationToken cancellationToken)
    {
        if (!customers.TryGetValue(row.CustomerCode, out var customer))
        {
            return Fail(row, $"Customer code '{row.CustomerCode}' not found in master data.");
        }
        if (!items.TryGetValue(row.ItemCode, out var item))
        {
            return Fail(row, $"Item code '{row.ItemCode}' not found in master data.");
        }
        if (!vatCategories.TryGetValue(row.VatCategoryCode, out var vat))
        {
            return Fail(row, $"VAT category '{row.VatCategoryCode}' not found.");
        }

        var draft = SalesInvoice.CreateDraft(
            customerId: customer.Id,
            customerTaxProfileSnapshot: customer.TaxProfile,
            documentDate: row.DocumentDate);

        draft.AddLine(
            itemId: item.Id,
            quantity: row.Quantity,
            unitPrice: MoneyEgp.From(row.UnitPrice),
            vatCategoryId: vat.Id,
            vatRatePercent: vat.RatePercent);

        _db.Add(draft);
        await _db.SaveChangesAsync(cancellationToken);

        var posted = await _postHandler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, postedByUserId),
            cancellationToken);

        return new BulkPostOutcome(
            LineNumber: row.LineNumber,
            Success: true,
            DocumentNumber: posted.DocumentNumber,
            SalesInvoiceId: posted.Id,
            ErrorMessage: null);
    }

    private static BulkPostOutcome Fail(BulkSalesInvoiceRow row, string message) =>
        new(LineNumber: row.LineNumber,
            Success: false,
            DocumentNumber: null,
            SalesInvoiceId: null,
            ErrorMessage: message);
}

public sealed record BulkPostOutcome(
    int LineNumber,
    bool Success,
    string? DocumentNumber,
    Guid? SalesInvoiceId,
    string? ErrorMessage);
