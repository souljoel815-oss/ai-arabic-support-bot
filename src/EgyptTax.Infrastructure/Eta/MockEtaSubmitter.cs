using System.Security.Cryptography;
using EgyptTax.Application.Eta;
using EgyptTax.Domain.Eta;
using EgyptTax.SharedKernel;

namespace EgyptTax.Infrastructure.Eta;

/// <summary>
/// FR-035 — in-process mock of the ETA submission endpoint. Returns
/// a synthetic UUID + Submitted on success; on simulated failure
/// returns a non-zero error code + Failed so the
/// <see cref="EtaSubmission"/> row picks up the retry-eligible state
/// the dashboard surfaces. The failure rate is configurable so
/// dev / smoke / load-test environments can dial in coverage of both
/// branches without depending on an external mock.
/// </summary>
public sealed class MockEtaSubmitter : IEtaSubmitter
{
    /// <summary>
    /// Default failure rate: 0% — production-faithful with the real
    /// ETA's success-path behaviour and keeps the dev experience
    /// predictable. Operators or tests crank this up to exercise the
    /// Failed → retry path.
    /// </summary>
    public const double DefaultFailureRate = 0.0;

    private readonly double _failureRate;

    public MockEtaSubmitter(double failureRate = DefaultFailureRate)
    {
        if (failureRate is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureRate),
                "Failure rate must be in [0, 1]."
            );
        }
        _failureRate = failureRate;
    }

    public Task<EtaSubmissionAttemptResult> SubmitAsync(
        Guid salesInvoiceId,
        string eInvoiceJson,
        CancellationToken cancellationToken = default
    )
    {
        // Scattered honeypot — refuse to push to ETA from an
        // unlicensed install even if the boot-time gate was patched.
        LicenseSentry.EnsureLicensed("MockEtaSubmitter.SubmitAsync");
        ArgumentException.ThrowIfNullOrWhiteSpace(eInvoiceJson);

        // RandomNumberGenerator gives a per-call independent draw —
        // unlike System.Random which would be seedable + reproducible
        // across threads. The mock is meant to be non-deterministic in
        // dev so failures surface organically; tests that need a
        // specific outcome inject a deterministic IEtaSubmitter stub
        // instead of constructing this type with a fixed rate.
        var coinFlip = RandomNumberGenerator.GetInt32(0, 10_000) / 10_000d;
        if (coinFlip < _failureRate)
        {
            return Task.FromResult(
                new EtaSubmissionAttemptResult(
                    OutcomeStatus: EtaSubmissionStatus.Failed,
                    SubmissionUuid: null,
                    ErrorCode: "ETA_MOCK_500",
                    ErrorMessage: "Mock ETA returned a simulated 5xx — eligible for retry within the submission window."
                )
            );
        }

        return Task.FromResult(
            new EtaSubmissionAttemptResult(
                OutcomeStatus: EtaSubmissionStatus.Submitted,
                SubmissionUuid: $"mock-{Guid.NewGuid():N}",
                ErrorCode: null,
                ErrorMessage: null
            )
        );
    }
}
