namespace EgyptTax.Domain.Identity;

/// <summary>
/// FR-038 — out-of-band administrator recovery marker. Written by the
/// CLI tool when an operator with database access bypasses the normal
/// password-reset flow (e.g. when no administrator with current
/// credentials remains). The audit chain enforces app-account-only
/// writes via the <c>trg_audit_log_append_only</c> trigger so the CLI
/// cannot itself emit a <c>FR-028</c> chain entry; instead, it stages
/// this row and the next application start drains it through
/// <c>SqlAuditLogStore</c>, which produces a chain-aligned event.
/// </summary>
public sealed class AdminRecoveryRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TargetUserId { get; init; }
    public string TargetEmail { get; init; } = default!;
    public DateTime RecoveredAtUtc { get; init; }
    public string MachineName { get; init; } = default!;
    public string? OperatorIdentity { get; init; }
    public DateTime? AuditEmittedAtUtc { get; private set; }

    private AdminRecoveryRecord() { }

    public AdminRecoveryRecord(
        Guid targetUserId,
        string targetEmail,
        DateTime recoveredAtUtc,
        string machineName,
        string? operatorIdentity
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineName);

        TargetUserId = targetUserId;
        TargetEmail = targetEmail;
        RecoveredAtUtc = recoveredAtUtc;
        MachineName = machineName;
        OperatorIdentity = operatorIdentity;
    }

    public void MarkAuditEmitted(DateTime nowUtc) => AuditEmittedAtUtc = nowUtc;
}
