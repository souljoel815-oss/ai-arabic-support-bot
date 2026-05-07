using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Reports;

/// <summary>
/// FR-023 — taxable income report. For a given period:
///  * <see cref="Revenue"/> = sum of posted SalesInvoice +
///    CreditNote NetBeforeVat (CN contributes negatively).
///  * <see cref="DeductibleExpenses"/> = sum of:
///      - PurchaseInvoiceLine.LineSubtotal where line.deductible_flag,
///      - Expense.Amount where deductible_flag,
///    both restricted to posted documents inside the window.
///  * <see cref="NonDeductibleAdjustments"/> = sum of the same with
///    deductible_flag = false. These appear in the management P&amp;L
///    but are "added back" when computing taxable income — they do
///    NOT reduce taxable profit per US2 acceptance scenario 2.
///  * <see cref="ManagementProfitLoss"/> = Revenue - (Deductible +
///    Non-deductible). What an internal P&amp;L would show.
///  * <see cref="TaxableIncome"/> = Revenue - Deductible only. What
///    you actually pay tax on. The arithmetic identity
///    <c>TaxableIncome == ManagementProfitLoss + NonDeductibleAdjustments</c>
///    is the "add-back".
/// </summary>
public sealed record TaxableIncomeReport(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    MoneyEgp Revenue,
    MoneyEgp DeductibleExpenses,
    MoneyEgp NonDeductibleAdjustments,
    MoneyEgp ManagementProfitLoss,
    MoneyEgp TaxableIncome,
    IReadOnlyList<TaxableIncomeRow> Rows);

/// <summary>
/// One contributing line. <see cref="Bucket"/> is the user-facing
/// label (Revenue / Deductible expense / Non-deductible adjustment)
/// so the page can group by it without re-deriving from the
/// document type.
/// </summary>
public sealed record TaxableIncomeRow(
    string Bucket,
    Guid DocumentId,
    DocumentType DocumentType,
    string? DocumentNumber,
    DateOnly DocumentDate,
    string Description,
    MoneyEgp Amount);
