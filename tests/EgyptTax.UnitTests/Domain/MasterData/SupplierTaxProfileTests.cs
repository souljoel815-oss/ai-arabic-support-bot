using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Domain.MasterData;

/// <summary>
/// B3a / FR-041 / INV-011 — locks in the data-model invariants for
/// SupplierTaxProfile before PurchaseInvoice (US2) starts depending
/// on them. The TaxRiskScore rule `NonRecoverableInputVatRule`
/// (T143, deferred to the PurchaseInvoice batch) reads
/// <see cref="SupplierTaxProfile.InputVatRecoverable"/>; nailing the
/// predicate now keeps that rule's logic to a one-liner.
/// </summary>
public class SupplierTaxProfileTests
{
    private static readonly Guid VatId = Guid.NewGuid();

    [Fact]
    public void RegisteredTaxpayer_HasTin_AndIsRecoverable()
    {
        var profile = SupplierTaxProfile.RegisteredTaxpayer(
            EgyptianTin.Parse("123456789"), VatId);

        profile.ProfileType.Should().Be(SupplierTaxProfileType.RegisteredTaxpayer);
        profile.TinValue.Should().Be("123456789");
        profile.Tin.Should().NotBeNull();
        profile.ReverseChargeFlag.Should().BeFalse(
            because: "reverse charge applies to ForeignSupplier only");
        profile.InputVatRecoverable.Should().BeTrue(
            because: "FR-020 / FR-041 — input VAT is recoverable only for registered taxpayers, and this is one");
    }

    [Fact]
    public void Unregistered_HasNoTin_AndIsNotRecoverable()
    {
        var profile = SupplierTaxProfile.Unregistered(VatId);

        profile.ProfileType.Should().Be(SupplierTaxProfileType.Unregistered);
        profile.TinValue.Should().BeNull();
        profile.Tin.Should().BeNull();
        profile.ReverseChargeFlag.Should().BeFalse();
        profile.InputVatRecoverable.Should().BeFalse(
            because: "FR-020 — unregistered suppliers cannot pass through recoverable VAT to the buyer");
    }

    [Fact]
    public void ForeignSupplier_TriggersReverseCharge_AndIsNotRecoverable()
    {
        var profile = SupplierTaxProfile.ForeignSupplier(VatId);

        profile.ProfileType.Should().Be(SupplierTaxProfileType.ForeignSupplier);
        profile.TinValue.Should().BeNull();
        profile.ReverseChargeFlag.Should().BeTrue(
            because: "imported services / goods from a foreign supplier are subject to reverse-charge VAT");
        profile.InputVatRecoverable.Should().BeFalse(
            because: "the foreign supplier itself doesn't issue Egyptian-recoverable input VAT — reverse-charge is the buyer's own self-invoiced output VAT");
    }

    [Fact]
    public void Tin_RoundTrips_Through_EgyptianTin()
    {
        var profile = SupplierTaxProfile.RegisteredTaxpayer(
            EgyptianTin.Parse("987654321"), VatId);

        profile.Tin!.Value.Value.Should().Be("987654321");
    }

    [Fact]
    public void DefaultPurchaseVatCategory_IsOptional()
    {
        SupplierTaxProfile.RegisteredTaxpayer(EgyptianTin.Parse("111111111"), defaultPurchaseVatCategoryId: null)
            .DefaultPurchaseVatCategoryId.Should().BeNull();
        SupplierTaxProfile.Unregistered(defaultPurchaseVatCategoryId: null)
            .DefaultPurchaseVatCategoryId.Should().BeNull();
        SupplierTaxProfile.ForeignSupplier(defaultPurchaseVatCategoryId: null)
            .DefaultPurchaseVatCategoryId.Should().BeNull();
    }
}
