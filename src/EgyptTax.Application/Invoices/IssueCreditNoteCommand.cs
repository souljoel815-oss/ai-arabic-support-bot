using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Invoices;

/// <summary>
/// FR-013 — request to draft a credit note correcting a Posted
/// SalesInvoice. The handler creates a new SalesInvoice (as a
/// credit note via the factory), copies the customer + tax-profile
/// snapshot from the original, and attaches the supplied lines —
/// each with a negative quantity per FR-013. Partial credit is
/// allowed: caller picks per-line quantities (subset of the
/// original's quantities or smaller). The created draft is then
/// posted via the regular <see cref="PostSalesInvoiceCommand"/>
/// flow which allocates a number from the CN series.
/// </summary>
public sealed record IssueCreditNoteCommand(
    Guid OriginalSalesInvoiceId,
    IReadOnlyCollection<IssueCreditNoteLine> Lines,
    string Reason,
    DateOnly DocumentDate);

public sealed record IssueCreditNoteLine(
    Guid ItemId,
    decimal Quantity,
    MoneyEgp UnitPrice,
    Guid VatCategoryId,
    decimal VatRatePercent);
