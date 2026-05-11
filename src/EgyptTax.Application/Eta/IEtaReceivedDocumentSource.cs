namespace EgyptTax.Application.Eta;

/// <summary>
/// P1.5 — port over the regulator's "Get Received Documents"
/// endpoint. Used by the Hangfire inbox-pull job to discover
/// documents your suppliers have submitted to ETA against your
/// TIN. Each result lands as an <c>EtaReceivedDocument</c> row
/// (idempotent on <see cref="EtaReceivedDocumentEnvelope.RegulatorLongUuid"/>)
/// for the operator to review + import as a Purchase Invoice
/// draft.
///
/// Production wires a real-ETA HTTP client paginating through
/// the receiver feed; the MVP wires <c>MockEtaReceivedDocumentSource</c>
/// returning a small fixture set so the inbox pipeline demos
/// end-to-end.
/// </summary>
public interface IEtaReceivedDocumentSource
{
    /// <summary>
    /// Pull every document received by this taxpayer since the
    /// last successful poll. The job uses <paramref name="sinceUtc"/>
    /// as a server-side filter when supported; the dedupe in the
    /// job itself (against <c>regulator_long_uuid</c>) is the
    /// safety net.
    /// </summary>
    Task<IReadOnlyList<EtaReceivedDocumentEnvelope>> FetchSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}

public sealed record EtaReceivedDocumentEnvelope(
    string RegulatorLongUuid,
    string SupplierTin,
    string SupplierLegalName,
    string DocumentNumber,
    DateOnly DocumentDate,
    decimal NetBeforeVatEgp,
    decimal VatTotalEgp,
    decimal GrandTotalEgp);
