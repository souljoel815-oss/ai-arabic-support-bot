namespace EgyptTax.Application.Compliance.PenaltyShield;

/// <summary>
/// Query that the Penalty Shield UI calls to render the dashboard.
/// Implementation lives in Infrastructure (joins EtaSubmission with
/// SalesInvoice + computes counts/projections per <see cref="PenaltyRegime"/>).
/// </summary>
public interface IPenaltyExposureQuery
{
    Task<PenaltyExposureReport> GetAsync(DateTime nowUtc, CancellationToken ct = default);
}
