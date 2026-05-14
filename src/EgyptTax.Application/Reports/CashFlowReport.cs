using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Reports;

/// <summary>
/// v4 B.4 — Cash Flow statement, direct method. Sums every
/// journal-entry line that hit a cash account in the period (the
/// "1100" base code + every <c>CashAccount.AccountCode</c> the
/// operator has registered) and buckets by source-document type:
///
///   * Customer Receipts (CRVs)
///   * Supplier Payments (SPVs — outflow, surfaced negative)
///   * Other Cash Movements (sales/credit-note/expense/asset
///     entries that touched cash directly + manual JEs)
///
/// v1 lumps everything into the Operating section per the v4
/// scope; the Investing / Financing split lands when a customer
/// asks for it. Opening + closing cash balances come from
/// summing all cash-account activity strictly before /
/// up-to-and-including the period.
/// </summary>
public sealed record CashFlowReport(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    MoneyEgp OpeningCash,
    IReadOnlyList<CashFlowLine> CustomerReceipts,
    IReadOnlyList<CashFlowLine> SupplierPayments,
    IReadOnlyList<CashFlowLine> OtherMovements,
    MoneyEgp NetCashChange,
    MoneyEgp ClosingCash
);

/// <summary>One row per source document that touched a cash
/// account. Amount is signed (positive = inflow / debit on cash;
/// negative = outflow / credit on cash) so the page can render
/// it directly without re-computing the sign.</summary>
public sealed record CashFlowLine(
    DateTime PostedAtUtc,
    Guid SourceDocumentId,
    string SourceDocumentNumber,
    DocumentType SourceDocumentType,
    string Description,
    MoneyEgp Amount
);
