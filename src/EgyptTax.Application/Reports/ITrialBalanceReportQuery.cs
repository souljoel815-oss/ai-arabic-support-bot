namespace EgyptTax.Application.Reports;

/// <summary>FR-024 — trial balance, period-scoped.</summary>
public interface ITrialBalanceReportQuery
{
    Task<TrialBalanceReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default);
}
