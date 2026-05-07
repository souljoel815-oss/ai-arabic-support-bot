using EgyptTax.Application.Audit;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Identity;

/// <summary>
/// FR-038 — Infrastructure-layer implementation of the password-reset
/// flow. Issues a 1-hour single-use token whose plaintext is shown to
/// the issuing admin exactly once; redemption verifies the token's
/// hash, applies the new password (with <c>PasswordMustChange=false</c>
/// because the user themselves chose it), and marks the token consumed.
/// Both operations write FR-028 audit events.
/// </summary>
public sealed class PasswordResetService(
    AppDbContext db,
    IPasswordHasher hasher,
    IClock clock,
    IAuditLogStore auditLog) : IPasswordResetService
{
    private readonly AppDbContext _db = db;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly IClock _clock = clock;
    private readonly IAuditLogStore _auditLog = auditLog;

    public async Task<PasswordResetIssued> IssueAsync(
        Guid targetUserId,
        Guid issuingAdminUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken)
            ?? throw new InvalidOperationException($"Target user {targetUserId} not found.");

        var plaintext = PasswordResetTokenHasher.GeneratePlaintext();
        var hash = PasswordResetTokenHasher.HashPlaintext(plaintext);
        var token = new PasswordResetToken(user.Id, hash, issuingAdminUserId, _clock.UtcNow);
        _db.Add(token);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "password.reset_token_issued",
                ActorUserId: issuingAdminUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"target_user_id":"{{user.Id:D}}","token_id":"{{token.Id:D}}","expires_at_utc":"{{token.ExpiresAtUtc:o}}"}"""),
            cancellationToken);

        return new PasswordResetIssued(plaintext, token.ExpiresAtUtc);
    }

    public async Task<PasswordResetRedeemResult> RedeemAsync(
        string plaintextToken,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        var hash = PasswordResetTokenHasher.HashPlaintext(plaintextToken);
        var token = await _db.Set<PasswordResetToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return new PasswordResetRedeemResult(false, "Token not found.");
        }

        var nowUtc = _clock.UtcNow;
        if (!token.IsActive(nowUtc))
        {
            return new PasswordResetRedeemResult(false,
                token.RedeemedAtUtc is not null
                    ? "Token already redeemed."
                    : "Token expired.");
        }

        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"User {token.UserId} for redeemed token not found.");

        user.SetPassword(_hasher.Hash(newPassword), mustChange: false);
        token.Redeem(nowUtc);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "password.reset_token_redeemed",
                ActorUserId: user.Id,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"target_user_id":"{{user.Id:D}}","token_id":"{{token.Id:D}}","redeemed_at_utc":"{{nowUtc:o}}"}"""),
            cancellationToken);

        return new PasswordResetRedeemResult(true, null);
    }
}
