using EgyptTax.Domain.Purchases;

namespace EgyptTax.Application.Accounting;

/// <summary>
/// US4 / FR-016 / FR-020 — port for the post-time journal emitter
/// on the purchase side. Mirrors <see cref="IJournalEntryEmitter"/>:
/// the implementation builds a balanced double-entry journal from
/// the just-posted purchase invoice and adds it to the current EF
/// change tracker; the caller (the post handler) is responsible for
/// the SaveChangesAsync that commits the journal in the same
/// transaction as the post itself.
///
/// Per-line semantics:
///   * Deductible line  → DR Expense (subtotal) + DR InputVAT (vat).
///   * Non-deductible line → DR Expense (subtotal + vat) — the
///     non-recoverable VAT becomes part of the expense.
///   * One CR AP for the invoice grand total closes the entry.
/// </summary>
public interface IPurchaseInvoiceJournalEmitter
{
    Task EmitForPurchaseInvoiceAsync(
        PurchaseInvoice invoice,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default);
}
