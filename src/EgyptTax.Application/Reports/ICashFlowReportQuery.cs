namespace EgyptTax.Application.Reports;

/// <summary>v4 B.4 — Cash Flow statement query, period-scoped.</summary>
public interface ICashFlowReportQuery
{
    Task<CashFlowReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default
    );
}
