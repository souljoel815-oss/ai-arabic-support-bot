namespace EgyptTax.Domain.Audit;

/// <summary>
/// Snapshot of the audit-log chain head, written periodically (every
/// 1,000 entries OR every 15 minutes per FR-028) to a store outside the
/// audit table. The verifier compares the checkpoint to the actual chain
/// head to detect tail truncation, which a chain-only walk cannot detect.
/// Per research.md R-05, the store is one of: a separate-schema table
/// (<see cref="EgyptTax.Application.Audit.IAuditCheckpointStore"/> backed
/// by <c>audit_meta.checkpoint</c>) OR a file under a write-restricted OS
/// directory.
/// </summary>
public sealed record AuditCheckpoint(long LastIndex, byte[] LastHash, DateTime TsUtc);
