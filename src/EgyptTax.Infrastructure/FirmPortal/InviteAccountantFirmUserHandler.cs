using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.FirmPortal;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.FirmPortal;

/// <summary>
/// US8 / FR-049 — full invitation lifecycle: invite, accept, revoke.
/// Each transition appends an FR-028 audit row tagged with the
/// firm name so the audit chain shows "firm X's user did Y" even
/// if X is later revoked or renamed (audit log is append-only;
/// historical entries keep the firm-name they were written with).
/// </summary>
public sealed class InviteAccountantFirmUserHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public InviteAccountantFirmUserHandler(AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<AccountantFirmUser> InviteAsync(
        InviteAccountantFirmUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = new User(
            email: command.InviteeEmail,
            displayName: command.InviteeDisplayName,
            passwordHash: command.TemporaryPasswordHash,
            preferredLanguage: command.PreferredLanguage,
            passwordMustChange: true);
        _db.Add(user);

        var nowUtc = _clock.UtcNow;
        var firmUser = new AccountantFirmUser(
            userId: user.Id,
            firmName: command.FirmName,
            firmExternalIdentifier: command.FirmExternalIdentifier,
            invitedAtUtc: nowUtc,
            invitedByUserId: command.InvitedByUserId);
        _db.Add(firmUser);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(new AuditLogPayload(
            Kind: "firm_user.invited",
            ActorUserId: command.InvitedByUserId,
            ActorFirmName: command.FirmName,
            CompanyId: Guid.Empty,
            PayloadJson: $$"""{"user_id":"{{user.Id:D}}","firm_name":"{{Escape(command.FirmName)}}","firm_external_identifier":"{{Escape(command.FirmExternalIdentifier)}}","invited_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}"}"""),
            cancellationToken);

        return firmUser;
    }

    public async Task<AccountantFirmUser> AcceptAsync(
        AcceptInvitationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var firmUser = await _db.Set<AccountantFirmUser>()
            .FirstOrDefaultAsync(a => a.UserId == command.UserId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No firm-user invitation found for user {command.UserId:D}.");

        var nowUtc = _clock.UtcNow;
        var alreadyAccepted = firmUser.AcceptedAtUtc is not null;
        firmUser.Accept(nowUtc);
        await _db.SaveChangesAsync(cancellationToken);

        if (!alreadyAccepted)
        {
            await _auditLog.AppendAsync(new AuditLogPayload(
                Kind: "firm_user.accepted",
                ActorUserId: command.UserId,
                ActorFirmName: firmUser.FirmName,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"user_id":"{{firmUser.UserId:D}}","accepted_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}"}"""),
                cancellationToken);
        }

        return firmUser;
    }

    public async Task<AccountantFirmUser> RevokeAsync(
        RevokeFirmUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var firmUser = await _db.Set<AccountantFirmUser>()
            .FirstOrDefaultAsync(a => a.UserId == command.UserId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No firm-user record found for user {command.UserId:D}.");

        var nowUtc = _clock.UtcNow;
        var alreadyRevoked = firmUser.RevokedAtUtc is not null;
        firmUser.Revoke(nowUtc, command.RevokedByUserId);
        await _db.SaveChangesAsync(cancellationToken);

        if (!alreadyRevoked)
        {
            await _auditLog.AppendAsync(new AuditLogPayload(
                Kind: "firm_user.revoked",
                ActorUserId: command.RevokedByUserId,
                ActorFirmName: firmUser.FirmName,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"user_id":"{{firmUser.UserId:D}}","revoked_at_utc":"{{nowUtc.ToString("o", CultureInfo.InvariantCulture)}}","revoked_by_user_id":"{{command.RevokedByUserId:D}}"}"""),
                cancellationToken);
        }

        return firmUser;
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);
}
