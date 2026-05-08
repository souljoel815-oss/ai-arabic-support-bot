namespace EgyptTax.Application.Reports;

/// <summary>
/// FR-021 — port for the monthly VAT report. Implementations select
/// the contributing document set + compute totals; the page calls
/// once per (year, month) refresh.
/// </summary>
public interface IVatMonthlyReportQuery
{
    Task<VatMonthlyReport> RunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default
    );
}
