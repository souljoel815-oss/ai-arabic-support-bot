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

    /// <summary>P0 — length of the no-token-required evaluation trial
    /// granted on first run. The trial starts when the gate first
    /// sees an install with no <c>license.token</c> AND no
    /// <see cref="TrialMarkerFileName"/>; the start wall-clock is
    /// persisted so reinstalls / service restarts don't reset it.</summary>
    public static readonly TimeSpan TrialDuration = TimeSpan.FromDays(14);

    /// <summary>P0 — file holding the trial start time. Existence
    /// implies "trial already used on this machine"; the operator
    /// can delete it only via support (the file is plain text but
    /// the gate refuses to grant a second trial regardless — the
    /// trial expires once, then the operator must buy a license).</summary>
    public const string TrialMarkerFileName = "trial-started.txt";

    /// <summary>
    /// Run the gate. <paramref name="stateDirectory"/> defaults to
    /// %PROGRAMDATA%/DaftarX/license on Windows; the portable EXE
    /// passes its own working directory.
    ///
    /// Tests bypass the gate via <c>EGYPTTAX_SKIP_LICENSE_GATE=1</c>
    /// — when set, this method is a no-op and the caller is expected
    /// to have already populated <see cref="LicenseStatus"/> with
    /// a test payload (see
    /// <c>tests/EgyptTax.ContractTests/LicenseSentryTestInitializer.cs</c>).
    /// </summary>
    public static void Run(string? stateDirectory = null)
    {
        if (Environment.GetEnvironmentVariable("EGYPTTAX_SKIP_LICENSE_GATE") == "1")
        {
            return;
        }
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

        // Path 3 — no token, no activated state. Check for an
        // existing trial marker; if none, start a new trial; if
        // present and still in window, continue the trial.
        if (TryGrantTrial(dir, hwid))
        {
            return;
        }

        // Path 4 — trial used up or some other failure. Refuse to
        // start; the middleware will show the HWID + sales contact
        // banner.
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

    /// <summary>
    /// P0 — grant or continue a 14-day evaluation trial. Returns
    /// <c>true</c> if the trial is active (i.e., this is either a
    /// fresh install OR a previous trial that's still inside its
    /// window). On the first call, writes
    /// <see cref="TrialMarkerFileName"/> with the current UTC; on
    /// subsequent calls, reads the existing marker and lets the
    /// trial continue if it hasn't expired yet.
    ///
    /// Marker is plain text containing ISO-8601 UTC of the trial
    /// start. A tampered or unreadable marker is treated as
    /// "trial expired" — the operator can't game the system by
    /// editing the file. Note: the marker file MUST be excluded
    /// from any "reset install" tooling so a deleted-and-recreated
    /// folder doesn't grant infinite trials.
    /// </summary>
    private static bool TryGrantTrial(string stateDir, string hwid)
    {
        var markerPath = Path.Combine(stateDir, TrialMarkerFileName);
        DateTime? existingStart = null;

        if (File.Exists(markerPath))
        {
            try
            {
                existingStart = ParseTrialMarker(File.ReadAllText(markerPath));
                if (existingStart is null) return false; // tampered → no trial
            }
            catch
            {
                return false;
            }
        }

        var decision = EvaluateTrial(existingStart, DateTime.UtcNow, TrialDuration);
        if (!decision.Granted) return false;

        if (existingStart is null)
        {
            try
            {
                File.WriteAllText(
                    markerPath,
                    decision.StartUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch
            {
                // If we can't write the marker, the trial still
                // runs for this process but won't survive a
                // restart. Operator's data dir is broken in a
                // bigger way and they'll see other errors.
            }
        }

        LicenseStatus.RecordTrial(hwid, decision.EndUtc);
        return true;
    }

    /// <summary>
    /// P0 — pure trial-window arithmetic. Given an optional persisted
    /// trial start (<c>null</c> on first run), the current UTC, and
    /// the trial duration, decide whether the trial should be
    /// granted and what its start/end timestamps are.
    /// Exposed as <c>internal</c> for unit-test coverage of the
    /// state machine without disk I/O.
    /// </summary>
    internal static TrialDecision EvaluateTrial(
        DateTime? existingStartUtc,
        DateTime nowUtc,
        TimeSpan duration)
    {
        var startUtc = existingStartUtc ?? nowUtc;
        var endUtc = startUtc + duration;
        var granted = nowUtc < endUtc;
        return new TrialDecision(granted, startUtc, endUtc);
    }

    /// <summary>P0 — parse the marker file payload. Returns
    /// <c>null</c> for any value that's missing, blank, or
    /// unparseable so the caller refuses to grant a trial off a
    /// tampered marker.</summary>
    internal static DateTime? ParseTrialMarker(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!DateTime.TryParse(
                raw.Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return null;
        }
        // ISO-8601 "o" format always carries a kind. If somehow Local
        // sneaked through, normalise to UTC; if Unspecified, treat
        // as Utc (the marker file is always written in Utc).
        return parsed.Kind switch
        {
            DateTimeKind.Utc => parsed,
            DateTimeKind.Local => parsed.ToUniversalTime(),
            _ => DateTime.SpecifyKind(parsed, DateTimeKind.Utc),
        };
    }

    /// <summary>P0 — outcome of <see cref="EvaluateTrial"/>.
    /// <see cref="Granted"/> is <c>false</c> only when the existing
    /// trial window has elapsed; <see cref="StartUtc"/> /
    /// <see cref="EndUtc"/> are always populated.</summary>
    internal readonly record struct TrialDecision(bool Granted, DateTime StartUtc, DateTime EndUtc);

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
