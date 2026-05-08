namespace EgyptTax.Application.FirmPortal;

/// <summary>
/// US8 / FR-049 / T225 — resolves "is this user a firm user, and
/// if so what's their firm name" for audit-tagging purposes.
///
/// Used by <c>AuditEmitBehavior</c> to OVERLAY the firm name onto
/// any audit payload whose <c>ActorFirmName</c> wasn't already
/// populated by the calling command (defense in depth — if a
/// future handler forgets to fill it, the behavior catches it
/// from the actor's user-id alone). The lookup is on the hot path
/// so the implementation indexes by user_id.
///
/// Returns null when the user is NOT a firm user (the common case
/// for in-house bookkeepers / accountants / approvers etc.) —
/// audit rows for those actors stay un-tagged with a firm name,
/// which is correct.
/// </summary>
public interface IFirmContextResolver
{
    Task<string?> ResolveFirmNameAsync(Guid userId, CancellationToken cancellationToken = default);
}
