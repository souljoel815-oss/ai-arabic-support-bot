namespace EgyptTax.Application.Reports;

/// <summary>Income Statement / قائمة الدخل — period-scoped.</summary>
public interface IProfitLossReportQuery
{
    Task<ProfitLossReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default
    );
}
