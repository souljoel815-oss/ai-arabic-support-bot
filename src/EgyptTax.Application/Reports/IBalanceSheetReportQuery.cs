namespace EgyptTax.Application.Reports;

/// <summary>Balance Sheet / الميزانية — point-in-time snapshot.</summary>
public interface IBalanceSheetReportQuery
{
    Task<BalanceSheetReport> RunAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default
    );
}
