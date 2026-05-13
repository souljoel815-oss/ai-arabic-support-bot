using EgyptTax.Domain.Audit;

namespace EgyptTax.UnitTests.Audit;

public class AuditChainVerifierTests
{
    [Fact]
    public void MalformedPayloadJson_BecomesFinding_DoesNotThrow()
    {
        // BUG-008 (May 2026 testing report) — a single bad row used to
        // bubble a System.Text.Json exception out of the verifier and
        // fail the whole walk + crash the /api/v1/audit/verify endpoint
        // (which then surfaced in the UI as the misleading client-side
        // parse error "The input does not contain any JSON tokens").
        // The verifier must instead treat the bad row as a finding and
        // keep walking.
        var prevHash = AuditChainHasher.GenesisHash;
        var entry = new AuditLogEntry(
            index: 1,
            tsUtc: DateTime.UtcNow,
            actorUserId: null,
            actorFirmName: null,
            companyId: Guid.Empty,
            kind: "Test.Bad",
            payloadJson: "",
            prevHash: prevHash,
            thisHash: new byte[32]);

        var report = AuditChainVerifier.Verify(new[] { entry });

        report.IsValid.Should().BeFalse();
        report.Findings.Should().ContainSingle()
            .Which.Kind.Should().Be(AuditChainFindingKind.ThisHashMismatch);
        report.Findings[0].Notes.Should().Contain("Could not canonicalize payload");
    }

    [Fact]
    public void GoodFollowedByBad_Reports_BadAndContinues()
    {
        // Confirm walk continues past a malformed row — the second
        // entry's prev-hash check still runs.
        var goodPayload = """{"x":1}""";
        var goodHash = AuditChainHasher.ComputeHash(goodPayload, AuditChainHasher.GenesisHash);
        var goodEntry = new AuditLogEntry(
            index: 1,
            tsUtc: DateTime.UtcNow,
            actorUserId: null,
            actorFirmName: null,
            companyId: Guid.Empty,
            kind: "Test.Good",
            payloadJson: goodPayload,
            prevHash: AuditChainHasher.GenesisHash,
            thisHash: goodHash);

        var badEntry = new AuditLogEntry(
            index: 2,
            tsUtc: DateTime.UtcNow,
            actorUserId: null,
            actorFirmName: null,
            companyId: Guid.Empty,
            kind: "Test.Bad",
            payloadJson: "   ",
            prevHash: goodHash,
            thisHash: new byte[32]);

        var report = AuditChainVerifier.Verify(new[] { goodEntry, badEntry });

        report.IsValid.Should().BeFalse();
        report.Findings.Should().ContainSingle(f =>
            f.AtIndex == 2 && f.Kind == AuditChainFindingKind.ThisHashMismatch);
    }
}
