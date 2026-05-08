using EgyptTax.Application.FirmPortal;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.FirmPortal;

/// <summary>
/// US8 / FR-049 / T225 — EF-backed firm-context resolver. Indexes
/// the lookup by <c>user_id</c> (PK on <c>AccountantFirmUser</c>
/// — single seek). Returns null for in-house users (no row) AND
/// for revoked firm users (the firm name is still on the row but
/// we don't retroactively re-tag NEW audits with a firm the user
/// no longer represents — historical entries keep their original
/// tag because the audit log is append-only).
/// </summary>
public sealed class SqlFirmContextResolver(AppDbContext db) : IFirmContextResolver
{
    private readonly AppDbContext _db = db;

    public async Task<string?> ResolveFirmNameAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        if (userId == Guid.Empty)
        {
            return null;
        }
        var row = await _db.Set<AccountantFirmUser>()
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.RevokedAtUtc == null)
            .Select(a => new { a.FirmName })
            .FirstOrDefaultAsync(cancellationToken);
        return row?.FirmName;
    }
}
