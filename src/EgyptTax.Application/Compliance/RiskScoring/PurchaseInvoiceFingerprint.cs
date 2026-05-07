using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Compliance.RiskScoring;

/// <summary>
/// T142 — minimal projection of an existing PurchaseInvoice for the
/// dedup rule's comparison. The caller (a Razor page or a query
/// helper) loads the candidate set via
/// <see cref="IPurchaseInvoiceFingerprintQuery"/> before scoring;
/// passing them inside the context keeps the rule free of any infra
/// dependency, which makes its unit tests trivial.
/// </summary>
public sealed record PurchaseInvoiceFingerprint(
    Guid Id,
    string? DocumentNumber,
    string SupplierInvoiceNumber,
    DateOnly DateReceived,
    MoneyEgp GrandTotal);
