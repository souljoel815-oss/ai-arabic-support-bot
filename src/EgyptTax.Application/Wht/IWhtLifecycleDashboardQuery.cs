using EgyptTax.Domain.Tax;

namespace EgyptTax.Application.Wht;

/// <summary>
/// FR-047 / T211 / US7 — read surface behind the WHT lifecycle
/// dashboard. Three views collapse onto one page so the operator
/// sees the full WHT cash-flow picture at a glance:
///
///   * Owed — outbound WHT certificates issued (we withheld from
///     suppliers) but NOT yet stamped into a Filed Form 41. Bare
///     liability the company will remit when the next quarter's
///     filing closes.
///   * Expected — inbound WHT certificates the company collected
///     from customers; the WHT-receivable asset we'll claim back
///     from ETA.
///   * Filings — list of every Form 41 row with its derived status
///     (Filed / Unfiled / Overdue) + estimated penalty for overdue
///     ones. Drives the FR-047 red-indicator UI surface that T201
///     pins.
/// </summary>
public interface IWhtLifecycleDashboardQuery
{
    Task<WhtLifecycleDashboard> GetAsync(
        DateOnly asOf, CancellationToken cancellationToken = default);
}

public sealed record WhtLifecycleDashboard(
    WhtOwedView Owed,
    WhtExpectedView Expected,
    IReadOnlyList<Form41FilingRow> Filings);

public sealed record WhtOwedView(
    decimal TotalAccruedNotYetFiled,
    int CertCount,
    DateOnly? OldestUnfiledCertDate);

public sealed record WhtExpectedView(
    decimal TotalReceivableFromCustomerWht,
    int InboundCertCount);

public sealed record Form41FilingRow(
    Guid Id,
    int FiscalYear,
    int Quarter,
    Form41Status DerivedStatus,
    DateOnly DueDate,
    int DaysOverdue,
    decimal TotalWhtPayable,
    int LineCount,
    decimal? EstimatedPenalty);
