using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Purchases;

/// <summary>
/// v5 E.3 — request to draft a credit note correcting a Posted
/// PurchaseInvoice. Mirrors the sales-side IssueCreditNoteCommand.
/// The handler creates a new PurchaseInvoice (as a credit note via
/// the factory), copies the supplier + tax-profile snapshot from
/// the original, and attaches the supplied lines with negative
/// quantities. Partial credit is allowed: caller picks per-line
/// quantities. The created draft is not posted here — operator
/// reviews + posts through the regular flow.
/// </summary>
public sealed record IssuePurchaseCreditNoteCommand(
    Guid OriginalPurchaseInvoiceId,
    IReadOnlyCollection<IssuePurchaseCreditNoteLine> Lines,
    string Reason,
    string SupplierCreditNoteNumber,
    DateOnly DateReceived
);

public sealed record IssuePurchaseCreditNoteLine(
    Guid? ItemId,
    Guid? ExpenseCategoryId,
    decimal Quantity,
    MoneyEgp UnitPrice,
    Guid VatCategoryId,
    decimal VatRatePercent,
    bool DeductibleFlag,
    Guid? CostCenterId
);
