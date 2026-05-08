using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.UnitTests.Domain.Documents;

/// <summary>
/// C6 / FR-051 / FR-053 / Phase 9 — supplier payment voucher
/// aggregate invariants. Pure-unit tests (no DB) — they exercise
/// the in-memory state-machine + allocation rules so a regression
/// at the aggregate boundary is caught by the fastest test layer.
/// </summary>
public class SupplierPaymentVoucherTests
{
    [Fact]
    public void CreateDraft_StartsInDraft_WithGrossEqualToNet_AndZeroWht()
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            supplierId: Guid.NewGuid(),
            paymentDate: new DateOnly(2026, 5, 9),
            paymentMethod: PaymentMethod.BankTransfer,
            paymentReference: "TR-001",
            grossPaymentAmount: MoneyEgp.From(10_000m)
        );

        v.State.Should().Be(DocumentState.Draft);
        v.GrossPaymentAmount.Amount.Should().Be(10_000m);
        v.WhtPayableAmount.Amount.Should()
            .Be(
                0m,
                because: "Phase 9 default — WHT is zero until US7's WhtComputeService applies a split"
            );
        v.NetCashPaid.Amount.Should().Be(10_000m, because: "with no WHT, cash leg equals gross");
        v.GeneratedWhtCertificateId.Should().BeNull();
    }

    [Fact]
    public void AddAllocation_RespectsVoucherCap_PerFr053()
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            Guid.NewGuid(),
            new DateOnly(2026, 5, 9),
            PaymentMethod.Cash,
            "REF",
            MoneyEgp.From(1_000m)
        );

        v.AddAllocation(Guid.NewGuid(), MoneyEgp.From(700m));

        // Adding another 400 would push allocated total to 1,100 —
        // 100 over the gross. FR-053 voucher cap MUST refuse.
        var act = () => v.AddAllocation(Guid.NewGuid(), MoneyEgp.From(400m));
        act.Should()
            .Throw<InvalidOperationException>()
            .Where(ex =>
                ex.Message.Contains("FR-053", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("voucher cap", StringComparison.OrdinalIgnoreCase)
            );

        v.Allocations.Should()
            .HaveCount(1, because: "the rejection MUST happen before the second allocation lands");
    }

    [Fact]
    public void MarkPosted_RequiresAtLeastOneAllocation()
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            Guid.NewGuid(),
            new DateOnly(2026, 5, 9),
            PaymentMethod.Cash,
            "REF",
            MoneyEgp.From(500m)
        );

        var act = () =>
            v.MarkPosted(
                "PAY-001",
                Guid.NewGuid(),
                new DateTime(2026, 5, 9, 10, 0, 0, DateTimeKind.Utc)
            );

        act.Should()
            .Throw<InvalidOperationException>()
            .Where(ex =>
                ex.Message.Contains("at least one allocation", StringComparison.OrdinalIgnoreCase)
            );
    }

    [Fact]
    public void MarkPosted_TransitionsToPosted_OnSuccess()
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            Guid.NewGuid(),
            new DateOnly(2026, 5, 9),
            PaymentMethod.Cash,
            "REF",
            MoneyEgp.From(500m)
        );
        v.AddAllocation(Guid.NewGuid(), MoneyEgp.From(500m));

        v.MarkPosted(
            "PAY-001",
            Guid.NewGuid(),
            new DateTime(2026, 5, 9, 10, 0, 0, DateTimeKind.Utc)
        );

        v.State.Should().Be(DocumentState.Posted);
        v.DocumentNumber.Should().Be("PAY-001");
    }

    [Fact]
    public void AddAllocation_RefusedAfterPost()
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            Guid.NewGuid(),
            new DateOnly(2026, 5, 9),
            PaymentMethod.Cash,
            "REF",
            MoneyEgp.From(500m)
        );
        v.AddAllocation(Guid.NewGuid(), MoneyEgp.From(500m));
        v.MarkPosted(
            "PAY-001",
            Guid.NewGuid(),
            new DateTime(2026, 5, 9, 10, 0, 0, DateTimeKind.Utc)
        );

        var act = () => v.AddAllocation(Guid.NewGuid(), MoneyEgp.From(50m));
        act.Should()
            .Throw<InvalidOperationException>(
                because: "Posted vouchers are immutable — corrections route through reversal"
            );
    }

    [Fact]
    public void ApplyWhtSplit_ReducesNetCashPaid_AndStampsCertificateId()
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            Guid.NewGuid(),
            new DateOnly(2026, 5, 9),
            PaymentMethod.BankTransfer,
            "REF",
            MoneyEgp.From(10_000m)
        );

        var certId = Guid.NewGuid();
        v.ApplyWhtSplit(MoneyEgp.From(500m), certId);

        v.WhtPayableAmount.Amount.Should().Be(500m);
        v.NetCashPaid.Amount.Should().Be(9_500m, because: "10k gross − 500 WHT = 9,500 cash leg");
        v.GeneratedWhtCertificateId.Should().Be(certId);
    }

    [Fact]
    public void ApplyWhtSplit_RefusesWhtExceedingGross()
    {
        var v = SupplierPaymentVoucher.CreateDraft(
            Guid.NewGuid(),
            new DateOnly(2026, 5, 9),
            PaymentMethod.BankTransfer,
            "REF",
            MoneyEgp.From(1_000m)
        );

        var act = () => v.ApplyWhtSplit(MoneyEgp.From(2_000m), Guid.NewGuid());
        act.Should()
            .Throw<ArgumentOutOfRangeException>(
                because: "WHT cannot exceed the gross — would produce negative cash leg"
            );
    }
}
