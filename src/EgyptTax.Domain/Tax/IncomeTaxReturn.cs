using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Tax;

/// <summary>
/// G3.4 — annual income-tax return snapshot. Same shape as
/// <see cref="VatReturn"/> but spans a fiscal year rather than a
/// month/quarter. Uses the existing <c>TaxableIncomeReport</c> for
/// the accounting numbers and applies the bracket calculator on
/// generation; the result is frozen on this row so later edits to
/// the underlying invoices don't retroactively change a submitted
/// return.
///
/// Lifecycle: Generated → Submitted → Acknowledged (mirrors
/// VatReturn so the operator workflow is identical).
///
/// Gate: every VAT month inside the fiscal year MUST be Locked
/// before the return can be generated. Forces the operator to walk
/// the closing cockpit twelve times — drafts, missing attachments,
/// failed ETA submissions are flushed before anything reaches the
/// regulator. Re-generation produces a NEW row.
///
/// Two regimes:
///   * Standard — bracket-based corporate income tax via
///     <c>CorporateIncomeTaxBracketCalculator</c>.
///   * Law6Simplified — flat-rate turnover tax via
///     <c>TurnoverTaxBracketCalculator</c> (revenue-based, no
///     deductions); the snapshot still records taxable income and
///     deductions for transparency but the tax-due figure comes
///     from the turnover route.
/// </summary>
public sealed class IncomeTaxReturn
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public IncomeTaxRegime Regime { get; init; }

    /// <summary>The fiscal year identifier — matches the year in
    /// which the fiscal year ENDS, per Egyptian convention. So a
    /// company with a calendar fiscal year reports FY 2026 for
    /// 2026-01-01 through 2026-12-31; a company starting July 1
    /// reports FY 2026 for 2025-07-01 through 2026-06-30.</summary>
    public int FiscalYear { get; init; }

    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }

    /// <summary>Frozen TaxableIncomeReport snapshot.</summary>
    public MoneyEgp Revenue { get; init; } = MoneyEgp.Zero;
    public MoneyEgp DeductibleExpenses { get; init; } = MoneyEgp.Zero;
    public MoneyEgp NonDeductibleAdjustments { get; init; } = MoneyEgp.Zero;
    public MoneyEgp ManagementProfitLoss { get; init; } = MoneyEgp.Zero;
    public MoneyEgp TaxableIncome { get; init; } = MoneyEgp.Zero;

    /// <summary>The bottom-line number the operator pays. For the
    /// Standard regime this is the bracket-summed corporate income
    /// tax on TaxableIncome; for the Law-6 regime it's the flat
    /// turnover-tax rate on Revenue.</summary>
    public MoneyEgp TaxDue { get; init; } = MoneyEgp.Zero;

    /// <summary>Blended effective rate at generation time —
    /// surfaced on the list page so the operator sees "FY2026 —
    /// 850k income, 18.4% effective" without re-running the
    /// computation.</summary>
    public decimal EffectiveRatePercent { get; init; }

    public int ContributingDocumentCount { get; init; }

    public DateTime GeneratedAtUtc { get; init; }
    public Guid GeneratedByUserId { get; init; }

    public IncomeTaxReturnStatus Status { get; private set; } = IncomeTaxReturnStatus.Generated;

    public string? RegulatorSubmissionReference { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }

    public string? RegulatorAcknowledgementReference { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }

    public string? Note { get; private set; }

    private IncomeTaxReturn() { }

    public IncomeTaxReturn(
        IncomeTaxRegime regime,
        int fiscalYear,
        DateOnly periodStart,
        DateOnly periodEnd,
        MoneyEgp revenue,
        MoneyEgp deductibleExpenses,
        MoneyEgp nonDeductibleAdjustments,
        MoneyEgp managementProfitLoss,
        MoneyEgp taxableIncome,
        MoneyEgp taxDue,
        decimal effectiveRatePercent,
        int contributingDocumentCount,
        DateTime generatedAtUtc,
        Guid generatedByUserId)
    {
        if (fiscalYear is < 1900 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(fiscalYear));
        if (periodStart > periodEnd)
            throw new ArgumentException("PeriodStart can't be after PeriodEnd.", nameof(periodStart));
        if (generatedByUserId == Guid.Empty)
            throw new ArgumentException("GeneratedByUserId is required.", nameof(generatedByUserId));
        if (effectiveRatePercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(effectiveRatePercent));

        Regime = regime;
        FiscalYear = fiscalYear;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Revenue = revenue;
        DeductibleExpenses = deductibleExpenses;
        NonDeductibleAdjustments = nonDeductibleAdjustments;
        ManagementProfitLoss = managementProfitLoss;
        TaxableIncome = taxableIncome;
        TaxDue = taxDue;
        EffectiveRatePercent = effectiveRatePercent;
        ContributingDocumentCount = contributingDocumentCount;
        GeneratedAtUtc = generatedAtUtc;
        GeneratedByUserId = generatedByUserId;
    }

    public void MarkSubmitted(string regulatorReference, Guid submittedByUserId, DateTime submittedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regulatorReference);
        if (submittedByUserId == Guid.Empty)
            throw new ArgumentException("SubmittedByUserId required.", nameof(submittedByUserId));

        if (Status == IncomeTaxReturnStatus.Acknowledged)
            throw new InvalidOperationException($"IncomeTaxReturn {Id} is already acknowledged.");
        if (Status == IncomeTaxReturnStatus.Submitted)
        {
            // Idempotent ONLY for same reference; refuse a different
            // reference — accidental double-submit on different
            // references is almost always operator error (re-generate
            // a new return for a corrected submission).
            if (!string.Equals(RegulatorSubmissionReference, regulatorReference.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"IncomeTaxReturn {Id} already submitted with reference '{RegulatorSubmissionReference}'. " +
                    "Generate a new return to record a different submission.");
            }
            return;
        }

        RegulatorSubmissionReference = regulatorReference.Trim();
        SubmittedAtUtc = submittedAtUtc;
        SubmittedByUserId = submittedByUserId;
        Status = IncomeTaxReturnStatus.Submitted;
    }

    public void MarkAcknowledged(string acknowledgementReference, Guid byUserId, DateTime acknowledgedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acknowledgementReference);
        if (Status == IncomeTaxReturnStatus.Generated)
            throw new InvalidOperationException($"IncomeTaxReturn {Id} hasn't been submitted yet.");
        if (Status == IncomeTaxReturnStatus.Acknowledged) return;

        RegulatorAcknowledgementReference = acknowledgementReference.Trim();
        AcknowledgedByUserId = byUserId;
        AcknowledgedAtUtc = acknowledgedAtUtc;
        Status = IncomeTaxReturnStatus.Acknowledged;
    }

    public void UpdateNote(string? note)
    {
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

public enum IncomeTaxRegime
{
    /// <summary>Standard corporate income tax — progressive brackets per Law 91/2005.</summary>
    Standard,
    /// <summary>Law 6/2025 simplified turnover tax — flat rate on revenue, no deductions.</summary>
    Law6Simplified,
}

public enum IncomeTaxReturnStatus
{
    Generated,
    Submitted,
    Acknowledged,
}
