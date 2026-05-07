using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Compliance.TinRevalidation;
using EgyptTax.Domain.Audit;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// R-13 / Differentiator 1 — periodic supplier-TIN revalidation. The
/// Hangfire cron fires on the configured schedule (default daily);
/// each tick:
///
///  * Loads the set of suppliers whose TIN should be re-checked from
///    <see cref="ISupplierTinSource"/>. The US1-time stub returns an
///    empty list (no Supplier aggregate yet); when US2 lands the
///    EF-backed implementation iterates the supplier master rows
///    whose <c>last_revalidated_at</c> is older than the cron
///    interval.
///  * For each supplier, calls <see cref="ISupplierTinRevalidator"/>
///    (MVP stub: <c>AlwaysValidTinRevalidator</c>). The Near-term
///    real implementation hits the published ETA TIN registry.
///  * Emits one <c>supplier.tin_revalidation_run</c> audit summary
///    per tick (counts checked / valid / invalid). Per-supplier
///    invalidations also emit a <c>supplier.tin_invalidated</c>
///    finding so the cockpit can flag the row.
///
/// Per-tick work is batched + per-row failures are caught so a
/// single registry hiccup doesn't abort the run. The audit summary
/// lands even when the source returns empty — operators reading the
/// chain need to know the cron actually fired.
/// </summary>
public sealed class SupplierTinRevalidationJob
{
    private readonly ISupplierTinSource _source;
    private readonly ISupplierTinRevalidator _revalidator;
    private readonly IAuditLogStore _auditLog;
    private readonly IClock _clock;

    public SupplierTinRevalidationJob(
        ISupplierTinSource source,
        ISupplierTinRevalidator revalidator,
        IAuditLogStore auditLog,
        IClock clock)
    {
        _source = source;
        _revalidator = revalidator;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<SupplierTinRevalidationJobResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await _source.GetSuppliersToRevalidateAsync(cancellationToken);

        var checkedCount = 0;
        var validCount = 0;
        var invalidCount = 0;

        foreach (var row in suppliers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkedCount++;

            TinRevalidationResult result;
            try
            {
                result = await _revalidator.RevalidateAsync(row.Tin, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Single-row failure: audit + continue. The summary
                // event below shows checked > (valid + invalid) so
                // operators can spot when the registry is flaky.
                await _auditLog.AppendAsync(new AuditLogPayload(
                    Kind: "supplier.tin_revalidation_failed",
                    ActorUserId: null, ActorFirmName: null, CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"supplier_id":"{{row.SupplierId:D}}","tin":"{{row.Tin}}","error":"{{Escape(ex.GetType().FullName ?? "Exception")}}","message":"{{Escape(ex.Message)}}"}"""),
                    cancellationToken);
                continue;
            }

            if (result.IsValid)
            {
                validCount++;
                continue;
            }

            invalidCount++;
            await _auditLog.AppendAsync(new AuditLogPayload(
                Kind: "supplier.tin_invalidated",
                ActorUserId: null, ActorFirmName: null, CompanyId: Guid.Empty,
                PayloadJson: $$"""{"supplier_id":"{{row.SupplierId:D}}","tin":"{{row.Tin}}","display_name":"{{Escape(row.DisplayName)}}","registry":"{{Escape(result.RegistryName ?? "")}}","reason":"{{Escape(result.Reason ?? "")}}"}"""),
                cancellationToken);
        }

        await _auditLog.AppendAsync(new AuditLogPayload(
            Kind: "supplier.tin_revalidation_run",
            ActorUserId: null, ActorFirmName: null, CompanyId: Guid.Empty,
            PayloadJson: $$"""{"checked":{{checkedCount.ToString(CultureInfo.InvariantCulture)}},"valid":{{validCount.ToString(CultureInfo.InvariantCulture)}},"invalid":{{invalidCount.ToString(CultureInfo.InvariantCulture)}},"ran_at_utc":"{{_clock.UtcNow.ToString("o", CultureInfo.InvariantCulture)}}"}"""),
            cancellationToken);

        return new SupplierTinRevalidationJobResult(
            CheckedCount: checkedCount,
            ValidCount: validCount,
            InvalidCount: invalidCount);
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
}

public sealed record SupplierTinRevalidationJobResult(
    int CheckedCount,
    int ValidCount,
    int InvalidCount);
