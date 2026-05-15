using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Accounting;

/// <summary>
/// v5 E.11 — money the company has paid up-front for an expense
/// that economically belongs to multiple future months (annual
/// insurance premium, advance rent, prepaid SaaS subscription,
/// training retainer). Recognition is straight-line over the
/// configured number of periods; one operator click per period
/// posts the unwind JE.
///
/// Flow:
///   1. Operator records the PrepaidExpense:
///        DR 1400 Prepaid Expense Asset / CR 1100 Cash (or 2100 AP)
///   2. Each period the operator clicks "Recognize next period":
///        DR &lt;ExpenseAccountCode&gt; / CR 1400 Prepaid Expense Asset
///      for an amount = total / period_count, until period_count
///      recognitions have run and the prepaid asset balance is zero.
///
/// MVP scope (per the v5 roadmap):
///   * Straight-line only (no custom amortization curves)
///   * Operator manually clicks each period — no Hangfire auto-fire
///     (parity with E.10 — every JV in this system is interactive
///     per FR-027)
///   * Mid-life adjustments / write-downs need a manual JV; this
///     aggregate doesn't expose them
/// </summary>
public sealed class PrepaidExpense
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public MoneyEgp TotalAmount { get; init; } = MoneyEgp.Zero;
    public string ExpenseAccountCode { get; init; } = "";
    public DateOnly StartMonth { get; init; }
    public int PeriodCount { get; init; }
    public int PeriodsRecognized { get; private set; }
    public DateTime? LastRecognizedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }

    private PrepaidExpense() { }

    public PrepaidExpense(
        string name,
        string? description,
        MoneyEgp totalAmount,
        string expenseAccountCode,
        DateOnly startMonth,
        int periodCount,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(expenseAccountCode);
        if (totalAmount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount),
                "Prepaid amount must be positive.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(periodCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(periodCount, 60);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        TotalAmount = totalAmount;
        ExpenseAccountCode = expenseAccountCode.Trim();
        StartMonth = new DateOnly(startMonth.Year, startMonth.Month, 1);
        PeriodCount = periodCount;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    /// <summary>Amount recognized per period (straight-line).</summary>
    public MoneyEgp MonthlyAmount => MoneyEgp.From(
        decimal.Round(TotalAmount.Amount / PeriodCount, 2, MidpointRounding.ToEven));

    /// <summary>Amount recognized to date.</summary>
    public MoneyEgp RecognizedToDate => MoneyEgp.From(MonthlyAmount.Amount * PeriodsRecognized);

    /// <summary>Amount still sitting in the prepaid-asset account.</summary>
    public MoneyEgp RemainingBalance =>
        MoneyEgp.From(TotalAmount.Amount - RecognizedToDate.Amount);

    /// <summary>True once <see cref="PeriodsRecognized"/> hits <see cref="PeriodCount"/>.</summary>
    public bool IsFullyRecognized => PeriodsRecognized >= PeriodCount;

    /// <summary>Month that the NEXT recognition click should record
    /// (used to stamp the JE narration). Returns <see cref="StartMonth"/>
    /// shifted by <see cref="PeriodsRecognized"/> months.</summary>
    public DateOnly NextRecognitionMonth => StartMonth.AddMonths(PeriodsRecognized);

    /// <summary>
    /// Mark one more period recognized. Caller is responsible for
    /// posting the actual JV (DR expense / CR 1400) — this method
    /// only advances the period counter so the page can't be
    /// double-clicked into recognizing 13 months for a 12-period
    /// prepayment.
    /// </summary>
    public void RecognizeNextPeriod(DateTime nowUtc)
    {
        if (IsFullyRecognized)
        {
            throw new InvalidOperationException(
                $"Prepaid '{Name}' is fully recognized ({PeriodsRecognized}/{PeriodCount}).");
        }
        PeriodsRecognized++;
        LastRecognizedAtUtc = nowUtc;
    }
}
