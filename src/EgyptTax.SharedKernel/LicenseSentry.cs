namespace EgyptTax.SharedKernel;

/// <summary>
/// Tiny dependency-free honeypot consumed by sensitive operations
/// across every layer (Domain, Infrastructure, Application). Web
/// layer sets <see cref="IsLicensedProvider"/> at startup; before
/// it's set the sentry pessimistically returns false.
///
/// Scattered call sites — PDF renderer, ETA submitter, invoice
/// post handler, inspection-bundle generator — invoke
/// <see cref="EnsureLicensed"/> at the top of any operation that
/// produces customer-visible state. A cracker who patches the
/// boot-time gate to "always succeed" still has to find and patch
/// every honeypot site OR risk the install throwing on the next
/// invoice/PDF/ETA call.
/// </summary>
public static class LicenseSentry
{
    /// <summary>
    /// Set once at application startup by the Web layer. Defaults
    /// to false-pessimistic before set so any pre-Program-Main code
    /// path is denied.
    /// </summary>
    public static Func<bool> IsLicensedProvider { get; set; } = static () => false;

    public static bool IsLicensed => IsLicensedProvider();

    /// <summary>
    /// Throws when the install isn't licensed. Use at the top of
    /// any operation that writes customer-visible state.
    /// </summary>
    public static void EnsureLicensed(string callSite)
    {
        if (IsLicensed) return;
        throw new LicenseGateException(
            $"License gate not active. Call site: {callSite}. " +
            "Reactivate the install (see /api/v1/license/status).");
    }
}

public sealed class LicenseGateException : Exception
{
    public LicenseGateException(string message) : base(message) { }
}
