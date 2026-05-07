namespace EgyptTax.Domain.Audit;

/// <summary>
/// Implementation of the verifier algorithm in
/// <c>contracts/audit-chain-verifier.md</c>. Walks an ordered enumerable of
/// <see cref="AuditLogEntry"/> in ascending index, recomputes each entry's
/// hash from the canonicalized payload + prev_hash, and reports any
/// mismatches. Pure domain — depends only on <see cref="AuditChainHasher"/>
/// and Application-layer port interfaces (TBD; this initial cut works on a
/// pre-loaded list which is all the happy-path test needs).
/// </summary>
public static class AuditChainVerifier
{
    public static AuditChainReport Verify(IReadOnlyList<AuditLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var findings = new List<AuditChainFinding>();
        long expectedIndex = 1;
        byte[] expectedPrevHash = AuditChainHasher.GenesisHash;

        foreach (var entry in entries)
        {
            if (entry.Index != expectedIndex)
            {
                findings.Add(new AuditChainFinding(
                    Kind: AuditChainFindingKind.MissingIndex,
                    AtIndex: entry.Index,
                    Notes: $"Expected index {expectedIndex} but observed {entry.Index}."));
            }

            if (!entry.PrevHash.AsSpan().SequenceEqual(expectedPrevHash))
            {
                findings.Add(new AuditChainFinding(
                    Kind: AuditChainFindingKind.PrevHashMismatch,
                    AtIndex: entry.Index));
            }

            var recomputed = AuditChainHasher.ComputeHash(entry.PayloadJson, entry.PrevHash);
            if (!entry.ThisHash.AsSpan().SequenceEqual(recomputed))
            {
                findings.Add(new AuditChainFinding(
                    Kind: AuditChainFindingKind.ThisHashMismatch,
                    AtIndex: entry.Index));
            }

            expectedIndex = entry.Index + 1;
            expectedPrevHash = entry.ThisHash;
        }

        return new AuditChainReport(IsValid: findings.Count == 0, Findings: findings);
    }
}

public sealed record AuditChainReport(bool IsValid, IReadOnlyList<AuditChainFinding> Findings);

public sealed record AuditChainFinding(
    AuditChainFindingKind Kind,
    long AtIndex,
    string? Expected = null,
    string? Actual = null,
    string? Notes = null);

public enum AuditChainFindingKind
{
    MissingIndex,
    PrevHashMismatch,
    ThisHashMismatch,
    TailTruncation,
    CheckpointMismatch,
}
