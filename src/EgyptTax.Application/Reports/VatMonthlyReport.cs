using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Reports;

/// <summary>
/// FR-021 — monthly VAT report. For a given (year, month) period:
///  * <see cref="OutputVat"/> = sum of VAT charged on POSTED sales
///    invoices + credit notes whose document_date falls inside the
///    period. Credit notes contribute negatively (their VAT total is
///    negative by construction — the negate-line-quantities pattern
///    from FR-013), so net VAT charged is OutputVat as-is.
///  * <see cref="InputVatRecoverable"/> = sum of VAT on POSTED
///    purchase-invoice LINES whose:
///      - parent date_received falls inside the period,
///      - line.deductible_flag = true,
///      - parent supplier-tax-profile snapshot ProfileType =
///        RegisteredTaxpayer (FR-020 / INV-011 — only registered
///        suppliers' input VAT is recoverable).
///  * <see cref="NetPayable"/> = OutputVat - InputVatRecoverable.
///    Positive = the firm owes ETA. Negative = credit balance carried
///    forward.
///
/// Rows surface each contributing document so the operator can drill
/// into a specific invoice from the report (FR-025).
/// </summary>
public sealed record VatMonthlyReport(
    int Year,
    int Month,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    MoneyEgp OutputVat,
    MoneyEgp InputVatRecoverable,
    MoneyEgp NetPayable,
    IReadOnlyList<VatMonthlyReportRow> Rows
);

/// <summary>
/// One contributing document in <see cref="VatMonthlyReport.Rows"/>.
/// Sorted by date ascending then by document number — the operator
/// reads top-to-bottom in the order documents were issued / received.
/// </summary>
public sealed record VatMonthlyReportRow(
    Guid DocumentId,
    DocumentType DocumentType,
    string? DocumentNumber,
    DateOnly DocumentDate,
    string CounterpartyName,
    MoneyEgp NetAmount,
    MoneyEgp VatAmount,
    bool ContributesToOutput
);
