using EgyptTax.Domain.Eta;

namespace EgyptTax.Application.Eta;

/// <summary>
/// FR-035 / SC-012 — port over the ETA Compliance Dashboard's
/// "upcoming deadline" query. Returns the rows the dashboard surfaces
/// to the operator: documents whose submission window expires within
/// the supplied lookahead AND that have NOT yet been submitted. The
/// query is required to complete in &lt; 2 s on a 50,000-document
/// installation per SC-012; the implementation is responsible for
/// hitting an indexed scan.
/// </summary>
public interface IEtaDashboardQuery
{
    Task<IReadOnlyList<EtaDashboardRow>> GetUpcomingDeadlinesAsync(
        TimeSpan within,
        DateTime nowUtc,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// One row of the dashboard's upcoming-deadline list. The set of
/// columns is the minimum the UI needs — denormalized from
/// SalesInvoice + EtaSubmission so the dashboard query reads from a
/// covering index without joining wide tables.
/// </summary>
public sealed record EtaDashboardRow(
    Guid SalesInvoiceId,
    Guid EtaSubmissionId,
    EtaSubmissionStatus Status,
    DateTime SubmissionWindowExpiresAtUtc,
    int AttemptCount,
    string? LastErrorCode
);
