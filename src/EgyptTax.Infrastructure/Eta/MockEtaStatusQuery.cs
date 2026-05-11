using System.Collections.Concurrent;
using System.Security.Cryptography;
using EgyptTax.Application.Eta;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// P1.3 — in-process mock of the ETA "Get Document" status endpoint.
/// Simulates the regulator's validation pipeline: each submission
/// goes through a brief PendingAcknowledgement window (default 30s
/// after first lookup), then resolves to Acknowledged with a long
/// UUID (~85% of the time) or Rejected (~15% — distributed across
/// the four most common rejection reasons we see in real Egyptian
/// e-invoice data).
///
/// The first-seen-at timestamp per submission UUID is held in-memory
/// because the production poller talks to a real HTTP API where the
/// regulator owns the clock; in the mock we approximate by recording
/// when WE first observed the submission. Lost on restart, which is
/// fine — the polling job will simply observe again on next tick.
/// </summary>
public sealed class MockEtaStatusQuery : IEtaStatusQuery
{
    /// <summary>
    /// How long an unseen submission stays in PendingAcknowledgement
    /// before resolving. Tuned for demo (you see the transition
    /// happen within a couple of poll ticks); production replaces
    /// this whole class with a real HTTP client.
    /// </summary>
    private static readonly TimeSpan AcknowledgementLatency = TimeSpan.FromSeconds(30);

    private const double RejectionRate = 0.15;

    private static readonly (string code, string message)[] RejectionCatalog =
    {
        ("EGS_CODE_NOT_ACTIVE", "EGS code on line 1 is not yet active in the regulator's catalog."),
        ("INVALID_TAXPAYER_TIN", "Customer TIN is not registered with the tax authority."),
        ("MATH_VALIDATION_FAILED", "Computed totals don't match submitted line totals."),
        ("DUPLICATE_DOCUMENT_NUMBER", "A document with this number was already accepted in the period."),
    };

    private readonly ConcurrentDictionary<string, DateTime> _firstSeenUtc = new(StringComparer.Ordinal);

    public Task<EtaDocumentStatusResult> GetStatusAsync(
        string submissionUuid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionUuid);

        var nowUtc = DateTime.UtcNow;
        var firstSeen = _firstSeenUtc.GetOrAdd(submissionUuid, nowUtc);
        if (nowUtc - firstSeen < AcknowledgementLatency)
        {
            return Task.FromResult(new EtaDocumentStatusResult(
                EtaDocumentStatus.PendingAcknowledgement, null, null, null));
        }

        // Stable per-submission outcome — once a uuid resolves we want
        // re-polls to return the same answer (a real regulator wouldn't
        // flip a previously-acknowledged doc to rejected).
        var hashByte = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(submissionUuid))[0];
        var coinFlip = hashByte / 256d;
        if (coinFlip < RejectionRate)
        {
            var pick = RejectionCatalog[hashByte % RejectionCatalog.Length];
            return Task.FromResult(new EtaDocumentStatusResult(
                EtaDocumentStatus.Rejected, null, pick.code, pick.message));
        }

        var longUuid = $"ETA-{Guid.NewGuid():N}";
        return Task.FromResult(new EtaDocumentStatusResult(
            EtaDocumentStatus.Acknowledged, longUuid, null, null));
    }
}
