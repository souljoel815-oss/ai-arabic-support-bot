using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Quotations;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// L1.5 follow-on (v3 roadmap) — daily sweep that flips Sent
/// quotations whose ValidUntilDate has passed into Expired state.
/// Without this, quotations sit in "Sent" forever after their
/// validity window — operator + customer both see misleading
/// status. Idempotent: re-running finds nothing on the second
/// pass. Logs the count for visibility.
///
/// Wraps <see cref="QuotationService.SweepExpiredAsync"/> with its
/// own DbContext scope (the service itself is request-scoped, so
/// Hangfire needs a fresh AppDbContext per fire).
/// </summary>
public sealed class QuotationExpirySweepJob
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IClock _clock;
    private readonly ILogger<QuotationExpirySweepJob> _log;

    public QuotationExpirySweepJob(
        IDbContextFactory<AppDbContext> dbFactory,
        IClock clock,
        ILogger<QuotationExpirySweepJob> log)
    {
        _dbFactory = dbFactory;
        _clock = clock;
        _log = log;
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var service = new QuotationService(db, _clock);
        var expired = await service.SweepExpiredAsync(ct);
        if (expired > 0)
        {
            _log.LogInformation("Marked {Count} quotation(s) as Expired.", expired);
        }
        else
        {
            _log.LogDebug("No quotations to expire.");
        }
    }
}
