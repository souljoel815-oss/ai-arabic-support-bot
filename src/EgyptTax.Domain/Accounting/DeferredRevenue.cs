using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Accounting;

/// <summary>
/// v5 F.1 — mirror of <see cref="PrepaidExpense"/> on the income
/// side. Revenue collected up-front (annual maintenance contracts,
/// prepaid training, subscription invoices, prepaid hosting) that
/// can't be recognised in month 1 without violating accrual
/// accounting / EAS revenue-recognition rules.
///
/// Flow:
///   1. Operator records the DeferredRevenue:
///        DR 1200 AR (or 1100 Cash) / CR 2320 Unearned Revenue
///   2. Each period the operator clicks "Recognize next period":
///        DR 2320 Unearned Revenue / CR &lt;RevenueAccountCode&gt;
///      for an amount = total / period_count, until period_count
///      recognitions have run and the 2320 balance for this row
///      is zero.
///
/// MVP scope mirrors E.11: straight-line only, operator-triggered
/// recognition (no Hangfire), no mid-life write-down (issue a sales
/// credit note against the source invoice instead).
/// </summary>
public sealed class DeferredRevenue
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public MoneyEgp TotalAmount { get; init; } = MoneyEgp.Zero;
    public string RevenueAccountCode { get; init; } = "";
    public DateOnly StartMonth { get; init; }
    public int PeriodCount { get; init; }
    public int PeriodsRecognized { get; private set; }
    public DateTime? LastRecognizedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }

    private DeferredRevenue() { }

    public DeferredRevenue(
        string name,
        string? description,
        MoneyEgp totalAmount,
        string revenueAccountCode,
        DateOnly startMonth,
        int periodCount,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(revenueAccountCode);
        if (totalAmount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount),
                "Deferred revenue amount must be positive.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(periodCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(periodCount, 60);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        TotalAmount = totalAmount;
        RevenueAccountCode = revenueAccountCode.Trim();
        StartMonth = new DateOnly(startMonth.Year, startMonth.Month, 1);
        PeriodCount = periodCount;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public MoneyEgp MonthlyAmount => MoneyEgp.From(
        decimal.Round(TotalAmount.Amount / PeriodCount, 2, MidpointRounding.ToEven));

    public MoneyEgp RecognizedToDate => MoneyEgp.From(MonthlyAmount.Amount * PeriodsRecognized);

    public MoneyEgp RemainingBalance =>
        MoneyEgp.From(TotalAmount.Amount - RecognizedToDate.Amount);

    public bool IsFullyRecognized => PeriodsRecognized >= PeriodCount;

    public DateOnly NextRecognitionMonth => StartMonth.AddMonths(PeriodsRecognized);

    public void RecognizeNextPeriod(DateTime nowUtc)
    {
        if (IsFullyRecognized)
        {
            throw new InvalidOperationException(
                $"Deferred revenue '{Name}' is fully recognized ({PeriodsRecognized}/{PeriodCount}).");
        }
        PeriodsRecognized++;
        LastRecognizedAtUtc = nowUtc;
    }
}
