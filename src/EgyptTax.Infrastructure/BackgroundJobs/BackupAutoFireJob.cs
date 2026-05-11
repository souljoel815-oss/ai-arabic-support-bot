using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Settings;
using EgyptTax.SharedKernel.Time;
using Microsoft.Extensions.Logging;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// Gux.13 Tab 8 — Hangfire job that fires <see cref="BackupEngine"/>
/// per the operator's configured schedule. Runs hourly; when it
/// fires it checks <see cref="BackupConfig"/> for whether
/// auto-backup is enabled and whether enough time has passed since
/// the last backup to satisfy the configured frequency.
///
/// Daily / Weekly are time-based — the job compares
/// <see cref="BackupConfig.LastBackupAtUtc"/> against the
/// frequency interval. OnClosing is event-driven (fires when a
/// tax period gets locked) and is wired into
/// <c>LockTaxPeriodHandler</c> separately, not via this job.
///
/// Idempotent: re-running within the same window after a successful
/// backup is a no-op (the LastBackupAtUtc check skips it). Failures
/// are logged but don't stop the next attempt — a transient disk
/// issue at 02:00 doesn't poison tomorrow's run.
/// </summary>
public sealed class BackupAutoFireJob
{
    private readonly SettingsRepository _settings;
    private readonly BackupEngine _engine;
    private readonly IClock _clock;
    private readonly ILogger<BackupAutoFireJob> _log;

    public BackupAutoFireJob(
        SettingsRepository settings,
        BackupEngine engine,
        IClock clock,
        ILogger<BackupAutoFireJob> log)
    {
        _settings = settings;
        _engine = engine;
        _clock = clock;
        _log = log;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var config = await _settings.GetBackupConfigAsync(cancellationToken);
        if (!config.AutoBackupEnabled)
        {
            _log.LogDebug("Auto-backup disabled; skipping.");
            return;
        }

        // Minimum-interval-since-last-backup based on frequency.
        // OnClosing is meant to be event-driven (the period-lock
        // handler fires it), but this cron also acts as a safety
        // net — if no period gets locked for 7+ days, take a backup
        // anyway so the operator isn't silently uncovered.
        var minInterval = config.Frequency switch
        {
            BackupFrequency.Daily     => TimeSpan.FromHours(20),  // <24h with slack so a 2:00am run isn't skipped
            BackupFrequency.Weekly    => TimeSpan.FromDays(6),    // <7d with slack
            BackupFrequency.OnClosing => TimeSpan.FromDays(7),    // safety net
            _ => TimeSpan.MaxValue,
        };

        var now = _clock.UtcNow;
        if (config.LastBackupAtUtc is { } last && now - last < minInterval)
        {
            _log.LogDebug(
                "Last backup was {Elapsed:c} ago; minimum interval for {Frequency} is {Min:c}; skipping.",
                now - last, config.Frequency, minInterval);
            return;
        }

        await FireBackupAsync(config.Frequency, cancellationToken);
    }

    /// <summary>
    /// Event-driven path called by <c>LockTaxPeriodHandler</c> via
    /// Hangfire fire-and-forget when a tax period locks. Fires
    /// unconditionally when the operator's frequency is OnClosing
    /// (no interval gate — the closing IS the trigger). Other
    /// frequencies short-circuit so a stray enqueue on a Daily-
    /// configured operator doesn't double-fire alongside the cron.
    /// </summary>
    public async Task RunOnClosingEventAsync(CancellationToken cancellationToken)
    {
        var config = await _settings.GetBackupConfigAsync(cancellationToken);
        if (!config.AutoBackupEnabled)
        {
            _log.LogDebug("OnClosing event ignored: auto-backup disabled.");
            return;
        }
        if (config.Frequency != BackupFrequency.OnClosing)
        {
            _log.LogDebug(
                "OnClosing event ignored: frequency is {Frequency}, not OnClosing.",
                config.Frequency);
            return;
        }

        await FireBackupAsync(config.Frequency, cancellationToken);
    }

    private async Task FireBackupAsync(BackupFrequency frequency, CancellationToken cancellationToken)
    {
        _log.LogInformation("Auto-backup firing ({Frequency}). Running BackupEngine.", frequency);

        var result = await _engine.BackupNowAsync(cancellationToken);
        if (result.Ok)
        {
            _log.LogInformation(
                "Auto-backup succeeded: {Path} ({Size} bytes).",
                result.Path, result.SizeBytes);
        }
        else
        {
            // Log + return — don't throw; Hangfire would retry the
            // whole job and we'd cascade. Operator sees the failure
            // via the Tab 8 telemetry (LastBackupAtUtc didn't update).
            _log.LogError("Auto-backup failed: {Error}", result.Error);
        }
    }
}
