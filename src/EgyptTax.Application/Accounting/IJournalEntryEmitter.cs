using EgyptTax.Domain.Invoices;

namespace EgyptTax.Application.Accounting;

/// <summary>
/// T095 — port for the post-time journal emitter. The implementation
/// builds a balanced double-entry journal from the just-posted
/// <see cref="SalesInvoice"/> (or credit note) and adds it to the
/// current EF change tracker; the caller (the post handler) is
/// responsible for calling <c>SaveChangesAsync</c> so the journal
/// commits in the same transaction as the post itself.
/// </summary>
public interface IJournalEntryEmitter
{
    /// <summary>
    /// Build + attach a balanced journal entry for the supplied
    /// posted invoice. Throws <see cref="InvalidOperationException"/>
    /// if the invoice is not Posted (defence-in-depth — the post
    /// handler only calls this after MarkPosted succeeds).
    /// </summary>
    Task EmitForSalesInvoiceAsync(SalesInvoice invoice, DateTime postedAtUtc, CancellationToken cancellationToken = default);
}
