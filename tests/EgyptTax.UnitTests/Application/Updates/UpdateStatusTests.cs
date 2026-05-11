using EgyptTax.Application.Updates;

namespace EgyptTax.UnitTests.Application.Updates;

/// <summary>
/// G4.1 — covers <see cref="UpdateStatus"/> version-comparison
/// logic + record-on-success / record-on-failure semantics.
///
/// Tests run inside a Collection so the static state mutations
/// don't race other tests in parallel.
/// </summary>
[Collection(nameof(UpdateStatusTests))]
public class UpdateStatusTests
{
    private static readonly DateTime T0 = new(2026, 5, 11, 4, 15, 0, DateTimeKind.Utc);

    [Fact]
    public void RecordSuccess_StoresManifest_AndComputesUpdateAvailable()
    {
        var manifest = NewManifest(version: "999.0.0");
        UpdateStatus.RecordSuccess(manifest, T0);

        UpdateStatus.Latest.Should().Be(manifest);
        UpdateStatus.LastCheckedUtc.Should().Be(T0);
        UpdateStatus.LastCheckError.Should().BeNull();
        UpdateStatus.UpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public void UpdateAvailable_FalseWhenLatestIsCurrent()
    {
        var current = UpdateStatus.CurrentVersion;
        var manifest = NewManifest(version: current.ToString());
        UpdateStatus.RecordSuccess(manifest, T0);

        UpdateStatus.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public void UpdateAvailable_FalseWhenLatestIsOlder()
    {
        var manifest = NewManifest(version: "0.0.1");
        UpdateStatus.RecordSuccess(manifest, T0);

        UpdateStatus.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public void UpdateAvailable_FalseWhenManifestVersionUnparseable()
    {
        var manifest = NewManifest(version: "not-a-version");
        UpdateStatus.RecordSuccess(manifest, T0);

        UpdateStatus.UpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public void RecordFailure_KeepsPreviousLatest()
    {
        var good = NewManifest(version: "999.0.0");
        UpdateStatus.RecordSuccess(good, T0);

        UpdateStatus.RecordFailure("CDN blip", T0.AddDays(1));

        UpdateStatus.Latest.Should().Be(good); // preserved
        UpdateStatus.LastCheckError.Should().Be("CDN blip");
        UpdateStatus.LastCheckedUtc.Should().Be(T0.AddDays(1));
        // A previously-known update is still surfaced — transient
        // failure doesn't hide it.
        UpdateStatus.UpdateAvailable.Should().BeTrue();
    }

    private static UpdateManifest NewManifest(string version) =>
        new(
            LatestVersion: version,
            MinSupportedVersion: "1.0.0",
            DownloadUrl: "https://daftarx.com/downloads/Setup.exe",
            ReleasedAt: T0,
            ChangelogAr: "تحسينات",
            ChangelogEn: "Improvements.");
}
