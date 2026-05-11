using EgyptTax.Application.Compliance.CertificateMonitor;

namespace EgyptTax.Infrastructure.Compliance.CertificateMonitor;

/// <summary>
/// Non-Windows fallback for <see cref="ICertificateMonitorQuery"/>.
/// Returns an empty inventory so the Certificates page renders cleanly
/// (with the "no certificates installed" empty state) on Linux/macOS
/// containers used for development and CI. Production runs Windows-only,
/// where the real X509Store-backed implementation is wired in.
/// </summary>
public sealed class NullCertificateMonitorQuery : ICertificateMonitorQuery
{
    public IReadOnlyList<CertificateInfo> List(DateTime nowUtc) => Array.Empty<CertificateInfo>();
}
