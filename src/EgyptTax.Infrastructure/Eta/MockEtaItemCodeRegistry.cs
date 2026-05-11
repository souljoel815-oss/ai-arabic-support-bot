using System.Security.Cryptography;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.MasterData;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// P1.6 — in-process mock of the GS1 Egypt + EGS registries.
/// Resolves a request once it's older than the registry's typical
/// SLA window:
///
///   * GS1: 60 seconds (real registry: 24-48 hours). Compressed
///     for the demo so the operator sees the transition during a
///     single sitting.
///   * EGS: 90 seconds (real registry: ~15 days). Same compression
///     reasoning.
///
/// Per-item outcome is deterministic (hash of the item id) so the
/// same item resolves the same way across pollings — a real
/// registry never flips a previously-issued code from Active to
/// Failed.
///
/// Outcome distribution: 90% Active, 10% Failed (with a rotation
/// across the four most common rejection reasons we see in real
/// Egyptian e-invoice rejections).
/// </summary>
public sealed class MockEtaItemCodeRegistry : IEtaItemCodeRegistry
{
    private static readonly TimeSpan Gs1SlaWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EgsSlaWindow = TimeSpan.FromSeconds(90);
    private const double FailureRate = 0.10;

    private static readonly string[] FailureCatalog =
    {
        "GTIN_NOT_FOUND",
        "GTIN_OWNER_MISMATCH",
        "DESCRIPTION_TOO_GENERIC",
        "CATEGORY_MAPPING_AMBIGUOUS",
    };

    public Task<EtaItemCodeLookupResult> LookupAsync(
        Guid itemId,
        EtaItemCodeKind kind,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var elapsed = DateTime.UtcNow - requestedAtUtc;
        var sla = kind == EtaItemCodeKind.Gs1 ? Gs1SlaWindow : EgsSlaWindow;
        if (elapsed < sla)
        {
            return Task.FromResult(new EtaItemCodeLookupResult(
                EtaItemCodeLookupStatus.StillPending, null, null));
        }

        // Deterministic outcome per item — once decided, stays decided.
        var hashByte = SHA256.HashData(itemId.ToByteArray())[0];
        var coinFlip = hashByte / 256d;
        if (coinFlip < FailureRate)
        {
            var reason = FailureCatalog[hashByte % FailureCatalog.Length];
            return Task.FromResult(new EtaItemCodeLookupResult(
                EtaItemCodeLookupStatus.Failed, null, reason));
        }

        var prefix = kind == EtaItemCodeKind.Gs1 ? "GS1-EG" : "EGS";
        var code = $"{prefix}-{Convert.ToHexString(itemId.ToByteArray()).AsSpan(0, 12)}";
        return Task.FromResult(new EtaItemCodeLookupResult(
            EtaItemCodeLookupStatus.Active, code, null));
    }
}
