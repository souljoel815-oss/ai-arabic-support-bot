using EgyptTax.Web.Licensing;

namespace EgyptTax.UnitTests.Web.Licensing;

/// <summary>
/// Gux.13 — covers the static <see cref="EditionGate"/> and its
/// helpers <see cref="Feature.DefaultsFor"/> +
/// <see cref="Feature.MinimumEditionFor"/>.
///
/// Tests run inside a Collection because <see cref="LicenseStatus"/>
/// is a process-wide singleton — parallel mutations from other
/// tests would corrupt assertions here.
/// </summary>
[Collection(nameof(EditionGateTests))]
public class EditionGateTests
{
    private static readonly DateTime FarFuture = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static LicensePayload PayloadFor(LicenseEdition edition,
        int? maxUsers = null,
        int? maxCompanies = null,
        string[]? features = null) =>
        new(
            Version: 2,
            Hwid: "TEST-HWID",
            Customer: "Test",
            Edition: edition.ToWireString(),
            IssuedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: FarFuture,
            SalesPhone: "+20",
            SalesEmail: "x@x",
            MaxUsers: maxUsers,
            MaxCompanies: maxCompanies,
            Features: features);

    [Fact]
    public void Allows_ReturnsFalse_WhenUnlicensed()
    {
        // Default state is NotChecked → IsLicensed = false → no
        // features available.
        // Note: TrialEvaluationTests + this fixture share state, so
        // we explicitly install a fresh "unlicensed" baseline.
        LicenseStatus.RecordFailure(LicenseFailureReason.EnvelopeMissingOrEmpty, "TEST-HWID");

        EditionGate.Allows(Feature.BankImport).Should().BeFalse();
        EditionGate.Allows(Feature.FirmPortal).Should().BeFalse();
    }

    [Fact]
    public void Solo_HasOnlyCoreFeatures_NoBankImport_NoMultiUser()
    {
        LicenseStatus.RecordValid(PayloadFor(LicenseEdition.Solo), "TEST-HWID");

        EditionGate.CurrentEdition().Should().Be(LicenseEdition.Solo);
        EditionGate.Allows(Feature.BankImport).Should().BeFalse();
        EditionGate.Allows(Feature.MultiUser).Should().BeFalse();
        EditionGate.Allows(Feature.FirmPortal).Should().BeFalse();
        EditionGate.MaxUsers().Should().Be(1);
        EditionGate.MaxCompanies().Should().Be(1);
    }

    [Fact]
    public void Smb_UnlocksOperations_StillNoEnterpriseFeatures()
    {
        LicenseStatus.RecordValid(PayloadFor(LicenseEdition.Smb), "TEST-HWID");

        EditionGate.Allows(Feature.BankImport).Should().BeTrue();
        EditionGate.Allows(Feature.BulkInvoice).Should().BeTrue();
        EditionGate.Allows(Feature.MultiUser).Should().BeTrue();
        EditionGate.Allows(Feature.ReceiptOcr).Should().BeTrue();

        EditionGate.Allows(Feature.IncomeTaxReturn).Should().BeFalse();
        EditionGate.Allows(Feature.MultiCompany).Should().BeFalse();
        EditionGate.Allows(Feature.FirmPortal).Should().BeFalse();
        EditionGate.MaxUsers().Should().Be(3);
    }

    [Fact]
    public void Enterprise_UnlocksIncomeTaxAndMultiCompany_NotFirmPortal()
    {
        LicenseStatus.RecordValid(PayloadFor(LicenseEdition.Enterprise), "TEST-HWID");

        EditionGate.Allows(Feature.IncomeTaxReturn).Should().BeTrue();
        EditionGate.Allows(Feature.MultiCompany).Should().BeTrue();
        EditionGate.Allows(Feature.ComplianceHealth).Should().BeTrue();
        EditionGate.Allows(Feature.CloudBackup).Should().BeTrue();

        EditionGate.Allows(Feature.FirmPortal).Should().BeFalse();
        EditionGate.Allows(Feature.CommissionLedger).Should().BeFalse();
        EditionGate.MaxCompanies().Should().Be(3);
    }

    [Fact]
    public void Firm_UnlocksEverything_IncludingFirmPortal()
    {
        LicenseStatus.RecordValid(PayloadFor(LicenseEdition.Firm), "TEST-HWID");

        EditionGate.Allows(Feature.FirmPortal).Should().BeTrue();
        EditionGate.Allows(Feature.CommissionLedger).Should().BeTrue();
        EditionGate.Allows(Feature.CompanySwitcher).Should().BeTrue();
        EditionGate.Allows(Feature.IncomeTaxReturn).Should().BeTrue();
        EditionGate.Allows(Feature.BankImport).Should().BeTrue();
        EditionGate.MaxCompanies().Should().Be(int.MaxValue);
        EditionGate.MaxUsers().Should().Be(int.MaxValue);
    }

    [Fact]
    public void Trial_GetsAllEnterpriseFeatures_NotFirmPortal()
    {
        LicenseStatus.RecordTrial("TEST-HWID", FarFuture);

        EditionGate.CurrentEdition().Should().Be(LicenseEdition.Trial);
        EditionGate.Allows(Feature.BankImport).Should().BeTrue();
        EditionGate.Allows(Feature.IncomeTaxReturn).Should().BeTrue();
        EditionGate.Allows(Feature.ArabicAiAssistant).Should().BeTrue();

        // Firm Portal is excluded from trial — prospects experience
        // the Enterprise tier, not the firm-reseller tier.
        EditionGate.Allows(Feature.FirmPortal).Should().BeFalse();
        EditionGate.Allows(Feature.CommissionLedger).Should().BeFalse();
    }

    [Fact]
    public void Require_ThrowsWithMinimumEdition_WhenFeatureGated()
    {
        LicenseStatus.RecordValid(PayloadFor(LicenseEdition.Solo), "TEST-HWID");

        var act = () => EditionGate.Require(Feature.BankImport);
        var ex = act.Should().Throw<LicenseRestrictionException>().Which;
        ex.FeatureKey.Should().Be(Feature.BankImport);
        ex.CurrentEdition.Should().Be(LicenseEdition.Solo);
        ex.MinimumEdition.Should().Be(LicenseEdition.Smb);
        ex.ArabicMessage.Should().Contain("أعمال صغيرة");
    }

    [Fact]
    public void Require_DoesNotThrow_WhenFeatureAllowed()
    {
        LicenseStatus.RecordValid(PayloadFor(LicenseEdition.Smb), "TEST-HWID");
        var act = () => EditionGate.Require(Feature.BankImport);
        act.Should().NotThrow();
    }

    [Fact]
    public void ExplicitFeaturesArrayInPayload_OverridesEditionDefaults()
    {
        // A custom-license scenario: SMB edition but with one
        // additional feature unlocked (e.g., promotional bundle).
        LicenseStatus.RecordValid(PayloadFor(
            LicenseEdition.Smb,
            features: new[] { Feature.BankImport, Feature.IncomeTaxReturn }),
            "TEST-HWID");

        EditionGate.Allows(Feature.BankImport).Should().BeTrue();
        EditionGate.Allows(Feature.IncomeTaxReturn).Should().BeTrue(); // normally Enterprise+
        EditionGate.Allows(Feature.BulkInvoice).Should().BeFalse(); // not in the explicit list
    }

    [Fact]
    public void ExplicitMaxUsersInPayload_OverridesEditionDefault()
    {
        LicenseStatus.RecordValid(PayloadFor(
            LicenseEdition.Smb,
            maxUsers: 10),
            "TEST-HWID");

        EditionGate.MaxUsers().Should().Be(10); // not the SMB default of 3
    }

    [Theory]
    [InlineData("Solo",       LicenseEdition.Solo)]
    [InlineData("solo",       LicenseEdition.Solo)]
    [InlineData("SMB",        LicenseEdition.Smb)]
    [InlineData("Enterprise", LicenseEdition.Enterprise)]
    [InlineData("FIRM",       LicenseEdition.Firm)]
    [InlineData("Standard",   LicenseEdition.Solo)]   // pre-Gux.13 fallback
    [InlineData("Pro",        LicenseEdition.Solo)]   // pre-Gux.13 fallback
    [InlineData("",           LicenseEdition.Solo)]
    [InlineData("nonsense",   LicenseEdition.Solo)]
    public void ParseOrSolo_HandlesAllKnownAndUnknownInputs(string input, LicenseEdition expected)
    {
        LicenseEditionExtensions.ParseOrSolo(input).Should().Be(expected);
    }

    [Fact]
    public void MinimumEditionFor_FirmFeatures_ReturnsFirm()
    {
        Feature.MinimumEditionFor(Feature.FirmPortal).Should().Be(LicenseEdition.Firm);
        Feature.MinimumEditionFor(Feature.CommissionLedger).Should().Be(LicenseEdition.Firm);
    }

    [Fact]
    public void MinimumEditionFor_EnterpriseFeatures_ReturnsEnterprise()
    {
        Feature.MinimumEditionFor(Feature.IncomeTaxReturn).Should().Be(LicenseEdition.Enterprise);
        Feature.MinimumEditionFor(Feature.MultiCompany).Should().Be(LicenseEdition.Enterprise);
        Feature.MinimumEditionFor(Feature.ArabicAiAssistant).Should().Be(LicenseEdition.Enterprise);
    }

    [Fact]
    public void MinimumEditionFor_SmbFeatures_ReturnsSmb()
    {
        Feature.MinimumEditionFor(Feature.BankImport).Should().Be(LicenseEdition.Smb);
        Feature.MinimumEditionFor(Feature.MultiUser).Should().Be(LicenseEdition.Smb);
        Feature.MinimumEditionFor(Feature.ReceiptOcr).Should().Be(LicenseEdition.Smb);
    }
}
