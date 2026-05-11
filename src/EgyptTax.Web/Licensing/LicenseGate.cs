using System.Runtime.Versioning;

namespace EgyptTax.Web.Licensing;

/// <summary>
/// Boot-time entry point invoked from Program.cs BEFORE Kestrel
/// starts. Resolves the current license state and writes it into
/// the <see cref="LicenseStatus"/> singleton; downstream middleware
/// + scattered checks read from there.
///
/// Never throws — all failure paths land in the singleton with a
/// reason so the banner middleware can render a useful message.
/// </summary>
public static class LicenseGate
{
    public const string DefaultStateDirRelativeName = "license";

    /// <summary>
    /// Run the gate. <paramref name="stateDirectory"/> defaults to
    /// %PROGRAMDATA%/DaftarX/license on Windows; the portable EXE
    /// passes its own working directory.
    /// </summary>
    public static void Run(string? stateDirectory = null)
    {
        // Defensive — never let a licensing failure (WMI down, DPAPI
        // refused, registry locked, malformed token, etc.) crash the
        // service. Worst case lands in Tampered state which the
        // middleware renders as the activation banner. Service stays
        // up; operator sees an actionable HTTP page instead of
        // ERR_CONNECTION_REFUSED.
        try
        {
            RunInner(stateDirectory);
        }
        catch (Exception ex)
        {
            try
            {
                Console.Error.WriteLine($"[LicenseGate] Unhandled: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                var dir = stateDirectory ?? DefaultStateDirectory();
                Directory.CreateDirectory(dir);
                File.WriteAllText(
                    Path.Combine(dir, "license-gate-crash.log"),
                    $"{DateTime.UtcNow:o}\n{ex}\n");
            }
            catch { /* best-effort logging */ }
            LicenseStatus.RecordFailure(
                LicenseFailureReason.EnvelopeMalformed,
                hwid: "(license gate threw — see C:\\ProgramData\\DaftarX\\license\\license-gate-crash.log)");
        }
    }

    private static void RunInner(string? stateDirectory)
    {
        var dir = stateDirectory ?? DefaultStateDirectory();
        Directory.CreateDirectory(dir);

        if (!OperatingSystem.IsWindows())
        {
            // Linux container demo — licensing is Windows-only by
            // design. Mark as Active so the rest of the stack runs.
            LicenseStatus.RecordValid(
                new LicensePayload(
                    Version: 1,
                    Hwid: "linux-dev",
                    Customer: "Linux dev container",
                    Edition: "Dev",
                    IssuedAtUtc: DateTime.UtcNow,
                    ExpiresAtUtc: DateTime.UtcNow.AddYears(10),
                    SalesPhone: "+20 100 000 0000",
                    SalesEmail: "sales@daftarx.local"),
                hwid: "linux-dev");
            return;
        }

        RunWindows(dir);
    }

    [SupportedOSPlatform("windows")]
    private static void RunWindows(string dir)
    {
        var hwid = HardwareId.Get();
        var flow = new ActivationFlow(dir);

        // Path 1 — already activated, just reconstruct + verify.
        var existing = flow.ReadActivated();
        if (existing.IsValid)
        {
            LicenseStatus.RecordValid(existing.Payload!, hwid);
            return;
        }

        // Path 2 — first run with a vendor-supplied token sitting in
        // the state dir. Activate + persist + verify.
        if (flow.TryActivate())
        {
            return; // ActivationFlow.TryActivate already updated LicenseStatus.
        }

        // Path 3 — not activated, no token. Refuse-to-start state.
        // The middleware will show the HWID + sales contact banner.
        if (existing.FailureReason == LicenseFailureReason.None
            || existing.FailureReason == LicenseFailureReason.EnvelopeMissingOrEmpty)
        {
            LicenseStatus.RecordFailure(LicenseFailureReason.EnvelopeMissingOrEmpty, hwid);
        }
        else
        {
            LicenseStatus.RecordFailure(existing.FailureReason, hwid, existing.Payload);
        }
    }

    public static string DefaultStateDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(pd, "DaftarX", "license");
        }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".daftarx", "license");
    }
}
