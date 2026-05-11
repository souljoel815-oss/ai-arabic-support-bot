using EgyptTax.Application.Updates;
using EgyptTax.SharedKernel.Time;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// G4.1 — daily Hangfire job that calls the
/// <see cref="IUpdateChannel"/> and writes the result to
/// <see cref="UpdateStatus"/>. Failures are swallowed (logged at
/// Info level + recorded on UpdateStatus.LastCheckError); the
/// banner shows whatever the last successful check found, so a
/// transient outage doesn't hide an update we already know about.
/// </summary>
public sealed class UpdateCheckJob
{
    private readonly IUpdateChannel _channel;
    private readonly IClock _clock;
    private readonly ILogger<UpdateCheckJob> _logger;

    public UpdateCheckJob(IUpdateChannel channel, IClock clock, ILogger<UpdateCheckJob> logger)
    {
        _channel = channel;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        try
        {
            var manifest = await _channel.CheckLatestAsync(cancellationToken);
            if (manifest is null)
            {
                UpdateStatus.RecordFailure("Empty manifest", nowUtc);
                return;
            }
            UpdateStatus.RecordSuccess(manifest, nowUtc);
            _logger.LogInformation(
                "Update check: current={Current}, latest={Latest}, update-available={Available}",
                UpdateStatus.CurrentVersion, manifest.LatestVersion, UpdateStatus.UpdateAvailable);
        }
        catch (Exception ex)
        {
            UpdateStatus.RecordFailure(ex.Message, nowUtc);
        }
    }
}
