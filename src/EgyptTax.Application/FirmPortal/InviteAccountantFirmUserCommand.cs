using EgyptTax.SharedKernel;

namespace EgyptTax.Application.FirmPortal;

/// <summary>
/// US8 / FR-049 — invite an external accountant to the installation.
/// Creates a User row (with a temporary password requiring change on
/// first login) plus the AccountantFirmUser profile row tying that
/// user to a named firm. Acceptance handshake (the invitee logging
/// in + flipping the row's accepted_at_utc) is a separate command.
/// </summary>
public sealed record InviteAccountantFirmUserCommand(
    string InviteeEmail,
    ArabicEnglishText InviteeDisplayName,
    string FirmName,
    string FirmExternalIdentifier,
    string TemporaryPasswordHash,
    Guid InvitedByUserId,
    Language PreferredLanguage = Language.Ar
);

/// <summary>US8 / FR-049 — invitee accepts. Idempotent.</summary>
public sealed record AcceptInvitationCommand(Guid UserId);

/// <summary>
/// US8 / FR-049 / scenario 4 — Administrator revokes the firm user's
/// scoped access. Effective immediately; previous audit entries +
/// posted documents tagged with this firm name remain readable
/// (FR-049 — append-only history).
/// </summary>
public sealed record RevokeFirmUserCommand(Guid UserId, Guid RevokedByUserId);
