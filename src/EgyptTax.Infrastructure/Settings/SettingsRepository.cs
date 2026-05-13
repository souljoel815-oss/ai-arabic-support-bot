using EgyptTax.Domain.Settings;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Settings;

/// <summary>
/// Gux.13 — lazy-init accessor for the four single-row settings
/// entities. The first call after upgrade creates a default row
/// transparently; subsequent calls return the existing row. The
/// admin-panel tabs and the Hangfire jobs that consume these
/// settings just call <see cref="GetInvoiceSettingsAsync"/> etc.
/// without worrying about whether the row exists yet.
///
/// Each entity is a singleton (one row per install). No company
/// scoping needed at this layer; Firm-edition multi-company
/// installs get their own admin panel per company via the
/// company-switcher path.
/// </summary>
public sealed class SettingsRepository
{
    private readonly AppDbContext _db;

    public SettingsRepository(AppDbContext db) => _db = db;

    public async Task<InvoiceSettings> GetInvoiceSettingsAsync(CancellationToken ct = default)
    {
        var existing = await _db.Set<InvoiceSettings>().FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        var fresh = InvoiceSettings.CreateDefault();
        _db.Add(fresh);
        await _db.SaveChangesAsync(ct);
        return fresh;
    }

    public async Task<SmtpSettings> GetSmtpSettingsAsync(CancellationToken ct = default)
    {
        var existing = await _db.Set<SmtpSettings>().FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        var fresh = SmtpSettings.CreateDefault();
        _db.Add(fresh);
        await _db.SaveChangesAsync(ct);
        return fresh;
    }

    public async Task<BackupConfig> GetBackupConfigAsync(CancellationToken ct = default)
    {
        var existing = await _db.Set<BackupConfig>().FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        var fresh = BackupConfig.CreateDefault();
        _db.Add(fresh);
        await _db.SaveChangesAsync(ct);
        return fresh;
    }

    public async Task<NotificationPrefs> GetNotificationPrefsAsync(CancellationToken ct = default)
    {
        var existing = await _db.Set<NotificationPrefs>().FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        var fresh = NotificationPrefs.CreateDefault();
        _db.Add(fresh);
        await _db.SaveChangesAsync(ct);
        return fresh;
    }

    public async Task<SalesRepSettings> GetSalesRepSettingsAsync(CancellationToken ct = default)
    {
        var existing = await _db.Set<SalesRepSettings>().FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        var fresh = SalesRepSettings.CreateDefault();
        _db.Add(fresh);
        await _db.SaveChangesAsync(ct);
        return fresh;
    }
}
