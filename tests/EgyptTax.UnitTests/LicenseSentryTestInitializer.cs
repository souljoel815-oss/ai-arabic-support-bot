using System.Runtime.CompilerServices;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests;

/// <summary>
/// Flips <see cref="LicenseSentry.IsLicensedProvider"/> to
/// always-true at assembly load. See
/// <c>tests/EgyptTax.IntegrationTests/Infrastructure/LicenseSentryTestInitializer.cs</c>
/// for the rationale.
/// </summary>
internal static class LicenseSentryTestInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        LicenseSentry.IsLicensedProvider = static () => true;
    }
}
