using System.Security.Cryptography;
using EgyptTax.Domain.Signatures;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Signatures;

/// <summary>
/// v3 §11 #8 — issue + verify signature-request tokens. 256-bit
/// random base64url-encoded; default expiry 30 days. Mirrors the
/// L5 customer-portal token shape but for one-time signing not
/// recurring access.
/// </summary>
public sealed class SignatureService
{
    private const int DefaultExpiryDays = 30;
    private const int TokenEntropyBytes = 32;

    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public SignatureService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> RequestAsync(
        SignatureDocumentType documentType,
        Guid documentId,
        Guid? createdByUserId,
        CancellationToken ct = default)
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenEntropyBytes);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').Replace("=", "", StringComparison.Ordinal);
        var now = _clock.UtcNow;

        _db.Add(new SignatureRequest(
            documentType: documentType,
            documentId: documentId,
            token: token,
            createdAtUtc: now,
            expiresAtUtc: now.AddDays(DefaultExpiryDays),
            createdByUserId: createdByUserId));
        await _db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<SignatureRequest?> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var sr = await _db.Set<SignatureRequest>()
            .FirstOrDefaultAsync(s => s.Token == token, ct);
        if (sr is null) return null;
        return sr.IsValid(_clock.UtcNow) ? sr : null;
    }

    public async Task<SignatureRequest?> SignAsync(
        string token, string signerName, string? signerIp, CancellationToken ct = default)
    {
        var sr = await _db.Set<SignatureRequest>()
            .FirstOrDefaultAsync(s => s.Token == token, ct);
        if (sr is null) return null;
        sr.RecordSignature(signerName, signerIp, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return sr;
    }
}
