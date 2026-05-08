namespace EgyptTax.Domain.Identity;

/// <summary>
/// A4 / FR-049 / US8 — accountant-firm-user profile. Composition over
/// <see cref="User"/> rather than inheritance: the row's <see cref="UserId"/>
/// is both PK and FK→User; the row's existence is the
/// "this user is a firm user" discriminator. This keeps the User
/// surface unchanged for normal company employees and lets US8's
/// audit-tagging key off "is there a firm-user row for the actor?".
///
/// Data model:
/// <list type="bullet">
///   <item>firm_name — captured at invitation; immutable except via
///   Administrator action which is itself audit-logged (INV-015).</item>
///   <item>firm_external_identifier — the contact email or IdP subject
///   used to invite the user. Surfaced in audit entries so an
///   inspector can correlate "which firm rep posted this voucher".</item>
///   <item>invitation lifecycle timestamps — invited / accepted /
///   revoked, plus the inviter / revoker user-ids for traceability.</item>
/// </list>
///
/// Acceptance flips <see cref="AcceptedAtUtc"/> from null to the
/// accept-time UTC; revocation flips <see cref="RevokedAtUtc"/>.
/// A revoked firm-user can NOT be re-activated by editing this row —
/// the operator re-invites (a fresh row, fresh accept handshake).
/// Previous records remain readable in perpetuity (FR-049 / US8 scenario 4).
/// </summary>
public sealed class AccountantFirmUser
{
    public Guid UserId { get; init; }
    public string FirmName { get; private set; } = default!;
    public string FirmExternalIdentifier { get; init; } = default!;

    public DateTime InvitedAtUtc { get; init; }
    public Guid InvitedByUserId { get; init; }

    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    private AccountantFirmUser() { }

    public AccountantFirmUser(
        Guid userId,
        string firmName,
        string firmExternalIdentifier,
        DateTime invitedAtUtc,
        Guid invitedByUserId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must be a non-empty Guid.", nameof(userId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(firmName);
        ArgumentException.ThrowIfNullOrWhiteSpace(firmExternalIdentifier);
        if (invitedByUserId == Guid.Empty)
        {
            throw new ArgumentException("InvitedByUserId must be a non-empty Guid.", nameof(invitedByUserId));
        }

        UserId = userId;
        FirmName = firmName;
        FirmExternalIdentifier = firmExternalIdentifier;
        InvitedAtUtc = invitedAtUtc;
        InvitedByUserId = invitedByUserId;
    }

    public bool IsActive => AcceptedAtUtc is not null && RevokedAtUtc is null;

    /// <summary>
    /// FR-049 / US8 scenario 1 — invitee accepts. Idempotent: a
    /// second accept on an already-accepted row is a no-op (the
    /// original timestamp wins, so the audit trail stays
    /// authoritative). Refuses if the row was already revoked
    /// before acceptance — operator must re-invite a fresh row.
    /// </summary>
    public void Accept(DateTime acceptedAtUtc)
    {
        if (RevokedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "Cannot accept an invitation that was already revoked. Re-invite to issue a fresh handshake.");
        }
        AcceptedAtUtc ??= acceptedAtUtc;
    }

    /// <summary>
    /// FR-049 / US8 scenario 4 — Administrator revokes the firm
    /// user's scoped access. Effective immediately; previous audit
    /// entries + posted documents tagged with this firm name remain
    /// readable. A revoked row stays revoked — the entity is append-
    /// only past this point.
    /// </summary>
    public void Revoke(DateTime revokedAtUtc, Guid revokedByUserId)
    {
        if (revokedByUserId == Guid.Empty)
        {
            throw new ArgumentException("RevokedByUserId must be a non-empty Guid.", nameof(revokedByUserId));
        }
        RevokedAtUtc ??= revokedAtUtc;
        RevokedByUserId ??= revokedByUserId;
    }

    /// <summary>
    /// INV-015 — firm-name correction by Administrator (e.g., the
    /// firm rebrands). Audit-logged at the call site so the change
    /// is traceable. The new name is the firm-name attached to all
    /// audit entries written from this row's user from now on;
    /// historical entries keep the prior firm-name they were tagged
    /// with at write time (audit log is append-only).
    /// </summary>
    public void RenameFirm(string newFirmName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newFirmName);
        FirmName = newFirmName;
    }
}
