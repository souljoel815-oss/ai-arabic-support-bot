using EgyptTax.Application.Workflow;
using EgyptTax.Domain.Workflow;

namespace EgyptTax.UnitTests.Application.Workflow;

public class PostedDocumentImmutabilityGuardTests
{
    [Fact]
    public void EnsureNotPosted_OnPostedDocument_Throws()
    {
        Action act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                DocumentState.Posted,
                DocumentType.SalesInvoice,
                Guid.NewGuid(),
                operation: "Edit"
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*Posted*credit note*");
    }

    [Theory]
    [InlineData(DocumentState.Draft)]
    [InlineData(DocumentState.Submitted)]
    [InlineData(DocumentState.Approved)]
    [InlineData(DocumentState.Voided)]
    public void EnsureNotPosted_OnNonPostedState_DoesNotThrow(DocumentState state)
    {
        Action act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                state,
                DocumentType.SalesInvoice,
                Guid.NewGuid(),
                operation: "Edit"
            );

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(DocumentType.SalesInvoice, "credit note")]
    [InlineData(DocumentType.CreditNote, "credit note")]
    [InlineData(DocumentType.PurchaseInvoice, "credit note")]
    [InlineData(DocumentType.JournalVoucher, "reversal voucher")]
    [InlineData(DocumentType.SupplierPaymentVoucher, "reversal voucher")]
    public void EnsureNotPosted_HintsCorrectCorrectionPath_ByDocumentType(
        DocumentType type,
        string expectedHint
    )
    {
        Action act = () =>
            PostedDocumentImmutabilityGuard.EnsureNotPosted(
                DocumentState.Posted,
                type,
                Guid.NewGuid(),
                operation: "Edit"
            );

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{expectedHint}*");
    }
}
