using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Periods;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Settings;

/// <summary>
/// Gux.13 Tab 4 — applies the next-invoice-number override after
/// running the three guards from §4 of the spec:
///
/// 1. <see cref="InvoiceNumberValidationException"/> if the new
///    number is ≤ the highest existing invoice number (posted or
///    draft).
/// 2. <see cref="InvoiceNumberPeriodLockedException"/> (FR-037) if
///    the tax period containing the most recent invoice is Locked.
///    Operator must Reopen the period first.
/// 3. Audit-log entry recording the old → new number on every
///    successful change.
///
/// The "create a gap" warning is UI-side (a confirmation dialog
/// before the form posts); this handler treats gap-creation as
/// allowed once the operator has confirmed.
/// </summary>
public sealed class UpdateInvoiceNumberHandler
{
    private readonly AppDbContext _db;
    private readonly SettingsRepository _settings;
    private readonly IAuditLogStore _audit;

    public UpdateInvoiceNumberHandler(
        AppDbContext db,
        SettingsRepository settings,
        IAuditLogStore audit)
    {
        _db = db;
        _settings = settings;
        _audit = audit;
    }

    public async Task<UpdateInvoiceNumberResult> HandleAsync(
        UpdateInvoiceNumberCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.NewNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(command),
                "Next invoice number must be ≥ 1.");
        if (command.OperatorUserId == Guid.Empty)
            throw new ArgumentException("OperatorUserId required.", nameof(command));

        // Guard 1: must exceed the highest existing invoice number.
        // Pull all numeric suffixes — the prefix is part of the
        // template, the numeric tail is what we compare.
        var settings = await _settings.GetInvoiceSettingsAsync(ct);
        var highestUsed = await ResolveHighestUsedNumberAsync(settings.Prefix, ct);
        if (command.NewNumber <= highestUsed)
        {
            throw new InvoiceNumberValidationException(
                requested: command.NewNumber,
                highestUsed: highestUsed);
        }

        // Guard 2: period-lock check. Find the tax period containing
        // the most recent invoice's DocumentDate. If it's Locked, refuse.
        var mostRecentDate = await _db.Set<SalesInvoice>().AsNoTracking()
            .OrderByDescending(i => i.DocumentDate)
            .Select(i => (DateOnly?)i.DocumentDate)
            .FirstOrDefaultAsync(ct);

        if (mostRecentDate is { } d)
        {
            var period = await _db.Set<TaxPeriod>().AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.PeriodKind == TaxPeriodKind.VatMonth
                    && p.Year == d.Year
                    && p.MonthOrQuarter == d.Month, ct);
            if (period is { Status: TaxPeriodStatus.Locked })
            {
                throw new InvoiceNumberPeriodLockedException(d.Year, d.Month);
            }
        }

        // Apply the change.
        var oldNumber = settings.NextNumber;
        settings.UpdateNextNumber(command.NewNumber);

        // Audit entry. Uses the existing audit kind set; the payload
        // captures both numbers + the operator's reason if supplied.
        // CompanyId is left empty — invoice numbering is install-wide
        // in v1 (multi-company comes with Firm edition + Phase 2j).
        await _audit.AppendAsync(new AuditLogPayload(
            Kind: "InvoiceNumberOverridden",
            ActorUserId: command.OperatorUserId,
            ActorFirmName: null,
            CompanyId: Guid.Empty,
            PayloadJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                oldNumber,
                newNumber = command.NewNumber,
                command.Reason,
                gap = command.NewNumber - highestUsed - 1,
            })), ct);

        await _db.SaveChangesAsync(ct);

        return new UpdateInvoiceNumberResult(
            OldNumber: oldNumber,
            NewNumber: command.NewNumber,
            HighestUsed: highestUsed,
            GapCreated: command.NewNumber - highestUsed - 1);
    }

    /// <summary>Find the highest numeric suffix among existing
    /// invoice document numbers that share the configured prefix.
    /// Returns 0 when no invoices have been issued yet.</summary>
    private async Task<int> ResolveHighestUsedNumberAsync(string prefix, CancellationToken ct)
    {
        var numbers = await _db.Set<SalesInvoice>().AsNoTracking()
            .Where(i => i.DocumentNumber != null)
            .Select(i => i.DocumentNumber!)
            .ToListAsync(ct);

        var highest = 0;
        foreach (var doc in numbers)
        {
            var tail = doc.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? doc[prefix.Length..]
                : doc;
            if (int.TryParse(tail.Trim(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var n) && n > highest)
            {
                highest = n;
            }
        }
        return highest;
    }
}

public sealed record UpdateInvoiceNumberCommand(
    int NewNumber,
    Guid OperatorUserId,
    string? Reason = null);

public sealed record UpdateInvoiceNumberResult(
    int OldNumber,
    int NewNumber,
    int HighestUsed,
    int GapCreated);

public sealed class InvoiceNumberValidationException : InvalidOperationException
{
    public int Requested { get; }
    public int HighestUsed { get; }

    public InvoiceNumberValidationException(int requested, int highestUsed)
        : base($"Next invoice number {requested} must exceed the highest used number {highestUsed}.")
    {
        Requested = requested;
        HighestUsed = highestUsed;
    }

    public string ArabicMessage =>
        $"لا يمكن استخدام رقم أقل من آخر فاتورة (رقم {HighestUsed}). اختر رقماً أكبر.";
}

public sealed class InvoiceNumberPeriodLockedException : InvalidOperationException
{
    public int Year { get; }
    public int Month { get; }

    public InvoiceNumberPeriodLockedException(int year, int month)
        : base($"Cannot change invoice number — period {year}-{month:D2} is Locked (FR-037).")
    {
        Year = year;
        Month = month;
    }

    public string ArabicMessage =>
        $"لا يمكن تغيير الترقيم — الفترة الضريبية {Year}-{Month:D2} مقفولة. افتح الفترة أولاً من قمرة الإقفال.";
}
