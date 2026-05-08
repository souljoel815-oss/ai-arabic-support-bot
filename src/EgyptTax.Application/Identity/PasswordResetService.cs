using System.Security.Cryptography;
using EgyptTax.Domain.Identity;

namespace EgyptTax.Application.Identity;

/// <summary>
/// FR-038 — administrator-issued password-reset flow. Two operations
/// live here: <see cref="IssueAsync"/> (called by an admin) returns the
/// plaintext token for one-time delivery to the user; <see cref="RedeemAsync"/>
/// (called by the user) verifies the token, sets the user's new
/// password, and stamps the token as redeemed. Tokens are stored as
/// SHA-256 hashes so a DB compromise does not expose live tokens.
/// </summary>
public interface IPasswordResetService
{
    Task<PasswordResetIssued> IssueAsync(
        Guid targetUserId,
        Guid issuingAdminUserId,
        CancellationToken cancellationToken = default
    );

    Task<PasswordResetRedeemResult> RedeemAsync(
        string plaintextToken,
        string newPassword,
        CancellationToken cancellationToken = default
    );
}

public sealed record PasswordResetIssued(string PlaintextToken, DateTime ExpiresAtUtc);

public sealed record PasswordResetRedeemResult(bool Succeeded, string? FailureReason);

/// <summary>
/// Helper for building the SHA-256 token hash. Lives at the Application
/// boundary so the same canonicalisation is shared between issuer and
/// redeemer paths.
/// </summary>
public static class PasswordResetTokenHasher
{
    public static byte[] HashPlaintext(string plaintextToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintextToken);
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintextToken));
    }

    public static string GeneratePlaintext()
    {
        // 256-bit URL-safe token. Length 43 base64url chars (no padding).
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
