using EgyptTax.Web.Licensing;

namespace EgyptTax.UnitTests.Web.Licensing;

/// <summary>
/// P0 trial-mode unit tests. Covers the pure window arithmetic
/// (<see cref="LicenseGate.EvaluateTrial"/>) and the marker parser
/// (<see cref="LicenseGate.ParseTrialMarker"/>) without touching disk
/// or WMI. The integration of these helpers into <c>RunWindows</c>
/// is best exercised manually on a Windows box.
/// </summary>
public class TrialEvaluationTests
{
    private static readonly TimeSpan FourteenDays = TimeSpan.FromDays(14);
    private static readonly DateTime T0 = new(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstRun_NoExistingMarker_GrantsTrialStartingNow()
    {
        var decision = LicenseGate.EvaluateTrial(
            existingStartUtc: null,
            nowUtc: T0,
            duration: FourteenDays);

        decision.Granted.Should().BeTrue();
        decision.StartUtc.Should().Be(T0);
        decision.EndUtc.Should().Be(T0.AddDays(14));
    }

    [Fact]
    public void MidTrial_ExistingMarker_PreservesOriginalStart()
    {
        var trialStart = T0;
        var now = T0.AddDays(7); // 7 days into the trial

        var decision = LicenseGate.EvaluateTrial(
            existingStartUtc: trialStart,
            nowUtc: now,
            duration: FourteenDays);

        decision.Granted.Should().BeTrue();
        decision.StartUtc.Should().Be(trialStart);
        decision.EndUtc.Should().Be(trialStart.AddDays(14));
    }

    [Fact]
    public void LastDay_OneSecondBeforeExpiry_StillGrants()
    {
        var trialStart = T0;
        var now = trialStart.AddDays(14).AddSeconds(-1);

        var decision = LicenseGate.EvaluateTrial(
            existingStartUtc: trialStart,
            nowUtc: now,
            duration: FourteenDays);

        decision.Granted.Should().BeTrue();
    }

    [Fact]
    public void ExactExpiry_AtTheBoundary_Refuses()
    {
        var trialStart = T0;
        var now = trialStart.AddDays(14); // exact boundary

        var decision = LicenseGate.EvaluateTrial(
            existingStartUtc: trialStart,
            nowUtc: now,
            duration: FourteenDays);

        decision.Granted.Should().BeFalse();
    }

    [Fact]
    public void Expired_OneSecondAfter_Refuses()
    {
        var trialStart = T0;
        var now = trialStart.AddDays(14).AddSeconds(1);

        var decision = LicenseGate.EvaluateTrial(
            existingStartUtc: trialStart,
            nowUtc: now,
            duration: FourteenDays);

        decision.Granted.Should().BeFalse();
    }

    [Fact]
    public void Expired_TenDaysAfter_Refuses()
    {
        var trialStart = T0;
        var now = trialStart.AddDays(24);

        var decision = LicenseGate.EvaluateTrial(
            existingStartUtc: trialStart,
            nowUtc: now,
            duration: FourteenDays);

        decision.Granted.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    [InlineData("2026")]
    [InlineData("garbage")]
    public void ParseTrialMarker_RejectsTamperedValues(string? raw)
    {
        var parsed = LicenseGate.ParseTrialMarker(raw);
        parsed.Should().BeNull();
    }

    [Fact]
    public void ParseTrialMarker_AcceptsIsoUtc()
    {
        var raw = T0.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        var parsed = LicenseGate.ParseTrialMarker(raw);

        parsed.Should().NotBeNull();
        parsed!.Value.Kind.Should().Be(DateTimeKind.Utc);
        parsed.Value.Should().Be(T0);
    }

    [Fact]
    public void ParseTrialMarker_AcceptsValueWithSurroundingWhitespace()
    {
        var raw = "\n  " + T0.ToString("o", System.Globalization.CultureInfo.InvariantCulture) + "  \n";

        var parsed = LicenseGate.ParseTrialMarker(raw);

        parsed.Should().Be(T0);
    }
}

/// <summary>
/// P0 — <see cref="LicenseStatus"/> state-machine tests. These touch
/// process-wide static state, so they reset to a known state per test
/// (each call to RecordValid / RecordTrial / RecordFailure is total
/// — the methods don't expose a clear-to-default operation, so we
/// chain transitions that match real lifecycle).
/// </summary>
[Collection(nameof(LicenseStatusTests))]
public class LicenseStatusTests
{
    [Fact]
    public void RecordTrial_FlipsStateToTrial_AndIsLicensedReturnsTrue()
    {
        LicenseStatus.RecordTrial("TEST-HWID", DateTime.UtcNow.AddDays(14));

        LicenseStatus.State.Should().Be(LicenseState.Trial);
        LicenseStatus.IsLicensed.Should().BeTrue();
        LicenseStatus.Hwid.Should().Be("TEST-HWID");
        LicenseStatus.TrialExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void IsLicensed_TrueForActiveAndTrial_FalseOtherwise()
    {
        // Active
        LicenseStatus.RecordValid(
            new LicensePayload(1, "h", "c", "Std",
                DateTime.UtcNow, DateTime.UtcNow.AddYears(1),
                "+20 100 000 0000", "sales@daftarx.local"),
            "h");
        LicenseStatus.IsLicensed.Should().BeTrue();

        // Trial
        LicenseStatus.RecordTrial("h", DateTime.UtcNow.AddDays(14));
        LicenseStatus.IsLicensed.Should().BeTrue();

        // NotActivated
        LicenseStatus.RecordFailure(LicenseFailureReason.EnvelopeMissingOrEmpty, "h");
        LicenseStatus.IsLicensed.Should().BeFalse();

        // Expired
        LicenseStatus.RecordFailure(LicenseFailureReason.Expired, "h");
        LicenseStatus.IsLicensed.Should().BeFalse();
    }
}
