namespace EgyptTax.Domain.Audit;

/// <summary>
/// Append-only, SHA-256-chained audit-log row per FR-028 + research.md R-05.
/// The combination of (PrevHash, ThisHash) forms a Merkle-style chain such
/// that any subsequent insert/edit/delete/reorder produces a hash mismatch
/// detectable by <see cref="AuditChainVerifier"/>. Constructor is private —
/// instances are created only via the SqlAuditLogStore append path so Index
/// allocation and hash-chain wiring stay coherent.
/// </summary>
public sealed class AuditLogEntry
{
    /// <summary>
    /// Monotonically increasing entry index (1-based). Allocated inside the
    /// same transaction as the INSERT so the chain remains gap-free.
    /// </summary>
    public long Index { get; init; }

    public DateTime TsUtc { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorFirmName { get; init; }
    public Guid CompanyId { get; init; }
    public string Kind { get; init; } = default!;

    /// <summary>
    /// Canonicalized JSON payload (post-RFC 8785 JCS) of the event-specific
    /// data. The verifier hashes <c>canonicalize(payload) || prev_hash</c>
    /// to recompute <see cref="ThisHash"/>.
    /// </summary>
    public string PayloadJson { get; init; } = default!;

    public byte[] PrevHash { get; init; } = default!;
    public byte[] ThisHash { get; init; } = default!;

    // EF Core requires a parameterless constructor.
    private AuditLogEntry() { }

    public AuditLogEntry(
        long index,
        DateTime tsUtc,
        Guid? actorUserId,
        string? actorFirmName,
        Guid companyId,
        string kind,
        string payloadJson,
        byte[] prevHash,
        byte[] thisHash
    )
    {
        Index = index;
        TsUtc = tsUtc;
        ActorUserId = actorUserId;
        ActorFirmName = actorFirmName;
        CompanyId = companyId;
        Kind = kind;
        PayloadJson = payloadJson;
        PrevHash = prevHash;
        ThisHash = thisHash;
    }
}

/// <summary>Input DTO for appending a new entry; the store fills in Index, TsUtc, hashes.</summary>
public sealed record AuditLogPayload(
    string Kind,
    Guid? ActorUserId,
    string? ActorFirmName,
    Guid CompanyId,
    string PayloadJson
);
