namespace EgyptTax.Application.Wht;

/// <summary>
/// FR-045 / US7 — hydrate a <see cref="WhtCertificatePayload"/>
/// from a persisted <c>WhtCertificate</c> row. Centralised so the
/// PDF renderer (T207), the contract-validating JSON serializer
/// (T198), and the future Form 41 generator (T209) all see the
/// same payload shape from the same source — no per-consumer
/// drift.
/// </summary>
public interface IWhtCertificatePayloadBuilder
{
    Task<WhtCertificatePayload?> BuildAsync(
        Guid certificateId, CancellationToken cancellationToken = default);
}
