namespace EgyptTax.Infrastructure.Audit;

/// <summary>
/// Single-row table mapped to <c>audit_meta.checkpoint</c>. The table is
/// constrained to a single row by a CHECK on <c>id = 1</c> in the migration.
/// The Infrastructure-internal entity exists separately from the Domain
/// record <c>AuditCheckpoint</c> so the EF model and the domain abstraction
/// can evolve independently.
/// </summary>
internal sealed class AuditCheckpointRow
{
    public int Id { get; set; }
    public long LastIndex { get; set; }
    public byte[] LastHash { get; set; } = default!;
    public DateTime TsUtc { get; set; }
}
