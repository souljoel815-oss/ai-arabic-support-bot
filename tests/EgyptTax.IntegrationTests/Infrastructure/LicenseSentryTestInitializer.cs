using System.Runtime.CompilerServices;
using EgyptTax.SharedKernel;

namespace EgyptTax.IntegrationTests.Infrastructure;

/// <summary>
/// Flips <see cref="LicenseSentry.IsLicensedProvider"/> to
/// always-true at assembly load. In production this is wired in
/// <c>Program.cs</c> from the boot-time <c>LicenseGate.Run()</c>
/// result; integration tests bypass <c>Program.cs</c> and construct
/// handlers directly, so without this every post handler / PDF
/// renderer / ETA submitter would throw <see cref="LicenseGateException"/>
/// — the same crash production sees when the customer's
/// license.token is missing.
///
/// Runs via the C# 9 <c>[ModuleInitializer]</c> attribute — exactly
/// once when the test assembly loads, before any xUnit collection
/// fixture or test method.
/// </summary>
internal static class LicenseSentryTestInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        LicenseSentry.IsLicensedProvider = static () => true;
    }
}
