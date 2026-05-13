using System.Security.Cryptography;
using EgyptTax.Domain.Customers;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Customers;

/// <summary>
/// L5 (v3 roadmap) — issues + verifies customer-portal magic-link
/// tokens. Tokens are 32 random bytes (256 bits) encoded base64url
/// → ~43 chars. Default expiry: 30 days.
/// </summary>
public sealed class CustomerPortalService
{
    private const int DefaultExpiryDays = 30;
    private const int TokenEntropyBytes = 32;

    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public CustomerPortalService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Mints a fresh portal-access row for the customer + returns
    /// the new token. Does NOT revoke previous tokens — the
    /// operator can have multiple active links if they want to
    /// give access to (say) the owner + the accountant separately.
    /// </summary>
    public async Task<string> GenerateLinkAsync(
        Guid customerId,
        Guid? createdByUserId,
        CancellationToken ct = default)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId required.", nameof(customerId));

        var bytes = RandomNumberGenerator.GetBytes(TokenEntropyBytes);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').Replace("=", "", StringComparison.Ordinal);

        var now = _clock.UtcNow;
        var access = new CustomerPortalAccess(
            customerId: customerId,
            token: token,
            createdAtUtc: now,
            expiresAtUtc: now.AddDays(DefaultExpiryDays),
            createdByUserId: createdByUserId);
        _db.Add(access);
        await _db.SaveChangesAsync(ct);
        return token;
    }

    /// <summary>
    /// Look up a token. Returns null when not found / revoked /
    /// expired. On success records the view timestamp on the
    /// access row (best-effort — failures are swallowed so a
    /// view-tracking write doesn't fail the customer's load).
    /// </summary>
    public async Task<CustomerPortalAccess?> ValidateAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var access = await _db.Set<CustomerPortalAccess>()
            .FirstOrDefaultAsync(a => a.Token == token, ct);
        if (access is null || !access.IsValid(_clock.UtcNow)) return null;

        try
        {
            access.RecordView(_clock.UtcNow);
            await _db.SaveChangesAsync(ct);
        }
        catch { /* best-effort, swallow */ }

        return access;
    }

    public async Task RevokeAllForCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var rows = await _db.Set<CustomerPortalAccess>()
            .Where(a => a.CustomerId == customerId && !a.Revoked)
            .ToListAsync(ct);
        foreach (var r in rows) r.Revoke();
        await _db.SaveChangesAsync(ct);
    }
}
