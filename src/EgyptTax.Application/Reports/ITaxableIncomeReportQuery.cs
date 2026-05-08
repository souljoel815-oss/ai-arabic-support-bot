namespace EgyptTax.Application.Reports;

/// <summary>
/// FR-023 — port for the taxable income report. Period boundaries
/// are caller-supplied so the report can serve a fiscal year, a
/// calendar year, a quarter, or any custom window.
/// </summary>
public interface ITaxableIncomeReportQuery
{
    Task<TaxableIncomeReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default
    );
}
