using EgyptTax.Domain.Workflow;

namespace EgyptTax.UnitTests.Domain.Workflow;

public class DocumentStateMachineTests
{
    [Theory]
    [InlineData(DocumentState.Draft, DocumentState.Submitted, true)]
    [InlineData(DocumentState.Submitted, DocumentState.Approved, true)]
    [InlineData(DocumentState.Submitted, DocumentState.Draft, true)]    // reject returns to Draft
    [InlineData(DocumentState.Approved, DocumentState.Posted, true)]
    [InlineData(DocumentState.Draft, DocumentState.Voided, true)]
    [InlineData(DocumentState.Submitted, DocumentState.Voided, true)]
    [InlineData(DocumentState.Approved, DocumentState.Voided, true)]
    public void ApprovalEnabled_TransitionsAllowed(DocumentState from, DocumentState to, bool expected)
    {
        DocumentStateMachine
            .CanTransition(from, to, approvalEnabled: true)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(DocumentState.Draft, DocumentState.Posted)]   // direct post requires approval-disabled
    [InlineData(DocumentState.Draft, DocumentState.Approved)] // skip submitted
    [InlineData(DocumentState.Posted, DocumentState.Voided)]  // posted is immutable
    [InlineData(DocumentState.Posted, DocumentState.Draft)]
    [InlineData(DocumentState.Voided, DocumentState.Draft)]   // voided is terminal
    [InlineData(DocumentState.Voided, DocumentState.Posted)]
    public void ApprovalEnabled_TransitionsRejected(DocumentState from, DocumentState to)
    {
        DocumentStateMachine
            .CanTransition(from, to, approvalEnabled: true)
            .Should().BeFalse();
    }

    [Fact]
    public void ApprovalDisabled_AllowsDirectPost_FromDraft()
    {
        DocumentStateMachine
            .CanTransition(DocumentState.Draft, DocumentState.Posted, approvalEnabled: false)
            .Should().BeTrue();
    }

    [Fact]
    public void ApprovalDisabled_StillRejectsPostFromVoided()
    {
        DocumentStateMachine
            .CanTransition(DocumentState.Voided, DocumentState.Posted, approvalEnabled: false)
            .Should().BeFalse();
    }

    [Fact]
    public void Posted_IsTerminal_RegardlessOfApprovalConfig()
    {
        var allStates = Enum.GetValues<DocumentState>();
        foreach (var target in allStates)
        {
            DocumentStateMachine.CanTransition(DocumentState.Posted, target, approvalEnabled: true).Should().BeFalse();
            DocumentStateMachine.CanTransition(DocumentState.Posted, target, approvalEnabled: false).Should().BeFalse();
        }
    }

    [Fact]
    public void Voided_IsTerminal_RegardlessOfApprovalConfig()
    {
        var allStates = Enum.GetValues<DocumentState>();
        foreach (var target in allStates)
        {
            DocumentStateMachine.CanTransition(DocumentState.Voided, target, approvalEnabled: true).Should().BeFalse();
            DocumentStateMachine.CanTransition(DocumentState.Voided, target, approvalEnabled: false).Should().BeFalse();
        }
    }

    [Fact]
    public void Transition_OnRejection_ThrowsInvalidOperationException()
    {
        Action act = () => DocumentStateMachine.Transition(
            DocumentState.Posted,
            DocumentState.Voided,
            approvalEnabled: true);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Posted*Voided*");
    }

    [Fact]
    public void Transition_OnAccepted_ReturnsTarget()
    {
        DocumentStateMachine
            .Transition(DocumentState.Draft, DocumentState.Submitted, approvalEnabled: true)
            .Should().Be(DocumentState.Submitted);
    }
}
