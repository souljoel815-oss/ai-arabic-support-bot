namespace EgyptTax.Domain.Audit;

/// <summary>
/// Implementation of the verifier algorithm in
/// <c>contracts/audit-chain-verifier.md</c>. Walks an ordered enumerable of
/// <see cref="AuditLogEntry"/> in ascending index, recomputes each entry's
/// hash from the canonicalized payload + prev_hash, and reports any
/// mismatches. When an <see cref="AuditCheckpoint"/> is provided, also
/// detects tail truncation (chain head index &lt; checkpoint last_index)
/// and checkpoint mismatch (head hash differs from checkpoint hash).
/// </summary>
public static class AuditChainVerifier
{
    public static AuditChainReport Verify(
        IReadOnlyList<AuditLogEntry> entries,
        AuditCheckpoint? checkpoint = null
    )
    {
        ArgumentNullException.ThrowIfNull(entries);

        var findings = new List<AuditChainFinding>();
        long expectedIndex = 1;
        var expectedPrevHash = AuditChainHasher.GenesisHash;

        foreach (var entry in entries)
        {
            if (entry.Index != expectedIndex)
            {
                findings.Add(
                    new AuditChainFinding(
                        Kind: AuditChainFindingKind.MissingIndex,
                        AtIndex: expectedIndex,
                        Notes: $"Expected index {expectedIndex} but observed {entry.Index}."
                    )
                );
            }

            if (!entry.PrevHash.AsSpan().SequenceEqual(expectedPrevHash))
            {
                findings.Add(
                    new AuditChainFinding(
                        Kind: AuditChainFindingKind.PrevHashMismatch,
                        AtIndex: entry.Index
                    )
                );
            }

            byte[]? recomputed = null;
            try
            {
                recomputed = AuditChainHasher.ComputeHash(entry.PayloadJson, entry.PrevHash);
            }
            catch (Exception ex)
            {
                // BUG-008 (May 2026 testing report) — a malformed payload
                // (empty / non-JSON / truncated) used to bubble a
                // System.Text.Json exception out of the verifier and
                // crash the walk. Surface it as a finding so the
                // auditor sees WHICH entry is bad, and keep walking.
                findings.Add(
                    new AuditChainFinding(
                        Kind: AuditChainFindingKind.ThisHashMismatch,
                        AtIndex: entry.Index,
                        Notes: $"Could not canonicalize payload: {ex.Message}"
                    )
                );
            }
            if (recomputed is not null && !entry.ThisHash.AsSpan().SequenceEqual(recomputed))
            {
                findings.Add(
                    new AuditChainFinding(
                        Kind: AuditChainFindingKind.ThisHashMismatch,
                        AtIndex: entry.Index
                    )
                );
            }

            expectedIndex = entry.Index + 1;
            expectedPrevHash = entry.ThisHash;
        }

        if (checkpoint is not null)
        {
            VerifyCheckpoint(entries, checkpoint, findings);
        }

        return new AuditChainReport(IsValid: findings.Count == 0, Findings: findings);
    }

    private static void VerifyCheckpoint(
        IReadOnlyList<AuditLogEntry> entries,
        AuditCheckpoint checkpoint,
        List<AuditChainFinding> findings
    )
    {
        var maxIndex = entries.Count > 0 ? entries[^1].Index : 0;

        if (maxIndex < checkpoint.LastIndex)
        {
            findings.Add(
                new AuditChainFinding(
                    Kind: AuditChainFindingKind.TailTruncation,
                    AtIndex: checkpoint.LastIndex,
                    Notes: $"Checkpoint last_index={checkpoint.LastIndex} but chain head is {maxIndex}."
                )
            );
            return;
        }

        AuditLogEntry? entryAtCheckpoint = null;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Index == checkpoint.LastIndex)
            {
                entryAtCheckpoint = entries[i];
                break;
            }
        }

        if (entryAtCheckpoint is null)
        {
            findings.Add(
                new AuditChainFinding(
                    Kind: AuditChainFindingKind.CheckpointMismatch,
                    AtIndex: checkpoint.LastIndex,
                    Notes: $"No entry found at checkpoint index {checkpoint.LastIndex}."
                )
            );
            return;
        }

        if (!entryAtCheckpoint.ThisHash.AsSpan().SequenceEqual(checkpoint.LastHash))
        {
            findings.Add(
                new AuditChainFinding(
                    Kind: AuditChainFindingKind.CheckpointMismatch,
                    AtIndex: checkpoint.LastIndex,
                    Notes: "Chain entry hash at checkpoint index does not match the checkpoint hash."
                )
            );
        }
    }
}

public sealed record AuditChainReport(bool IsValid, IReadOnlyList<AuditChainFinding> Findings);

public sealed record AuditChainFinding(
    AuditChainFindingKind Kind,
    long AtIndex,
    string? Expected = null,
    string? Actual = null,
    string? Notes = null
);

public enum AuditChainFindingKind
{
    MissingIndex,
    PrevHashMismatch,
    ThisHashMismatch,
    TailTruncation,
    CheckpointMismatch,
}
