using System.Runtime.CompilerServices;
using EgyptTax.SharedKernel;
using EgyptTax.Web.Licensing;

namespace EgyptTax.E2ETests;

/// <summary>
/// Flips <see cref="LicenseSentry.IsLicensedProvider"/> + records a
/// fake-valid <see cref="LicenseStatus"/> + bypasses the real
/// <see cref="LicenseGate"/> at assembly load. Mirrors the contract
/// test assembly's initializer — see
/// <c>tests/EgyptTax.ContractTests/LicenseSentryTestInitializer.cs</c>
/// for rationale.
/// </summary>
internal static class LicenseSentryTestInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        Environment.SetEnvironmentVariable("EGYPTTAX_SKIP_LICENSE_GATE", "1");

        LicenseSentry.IsLicensedProvider = static () => true;
        LicenseStatus.RecordValid(
            new LicensePayload(
                Version: 1,
                Hwid: "test-hwid",
                Customer: "E2E Test Suite",
                Edition: "Test",
                IssuedAtUtc: DateTime.UtcNow,
                ExpiresAtUtc: DateTime.UtcNow.AddYears(10),
                SalesPhone: "+20 100 000 0000",
                SalesEmail: "sales@daftarx.local"),
            hwid: "test-hwid");
    }
}
