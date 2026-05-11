using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Tax;

/// <summary>
/// G3.3 — one quarterly or monthly VAT-return record. The vendor's
/// existing <c>VatMonthlyReport</c> computes the numbers; this
/// entity captures the snapshot at generation time so future
/// adjustments don't retroactively change a submitted return.
///
/// Lifecycle:
///   Generated → Submitted → Acknowledged
///
/// Generated = preview/draft. Numbers are frozen at this point.
/// Operator reviews + clicks "Generate".
/// Submitted = operator has fed the return into the regulator's
/// portal (manual for v1 since ETA hasn't published a bulk-submit
/// API yet). Marks the row + records the operator's confirmation.
/// Acknowledged = regulator returned an acknowledgement reference
/// (manual recording today; webhook-driven once the API ships).
///
/// Gate: the source <c>TaxPeriod</c> MUST be Locked before a
/// VatReturn can be generated — same invariant as Form 41. Forces
/// the operator through the Closing-Cockpit gate first so drafts /
/// missing attachments / failed ETA don't leak into the return.
///
/// One row per (period_year, period_month) for the VatMonthly regime;
/// per (period_year, period_quarter) for the Law-6 quarterly regime.
/// Re-generation produces a NEW row (the old one stays for audit);
/// the dashboard shows the latest by ReturnPeriodEnd.
/// </summary>
public sealed class VatReturn
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>VatMonthly / VatQuarterly — drives the period
    /// arithmetic.</summary>
    public VatReturnPeriodKind PeriodKind { get; init; }
    public int PeriodYear { get; init; }
    /// <summary>1-12 for monthly; 1-4 for quarterly.</summary>
    public int PeriodOrdinal { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }

    /// <summary>Frozen snapshot from VatMonthlyReport at generation
    /// time — output VAT, input VAT recoverable, net payable. The
    /// regulator submission carries exactly these numbers; later
    /// adjustments to the underlying invoices do NOT alter this
    /// row (a corrected return becomes a NEW VatReturn).</summary>
    public MoneyEgp OutputVat { get; init; } = MoneyEgp.Zero;
    public MoneyEgp InputVatRecoverable { get; init; } = MoneyEgp.Zero;
    public MoneyEgp InputVatNonRecoverable { get; init; } = MoneyEgp.Zero;
    public MoneyEgp NetPayable { get; init; } = MoneyEgp.Zero;

    /// <summary>Count of contributing documents (sales + purchase)
    /// at generation time — surfaced on the list page so the
    /// operator sees "May 2026 — 47 docs, 12,300 EGP" without
    /// drilling into the snapshot.</summary>
    public int ContributingDocumentCount { get; init; }

    public DateTime GeneratedAtUtc { get; init; }
    public Guid GeneratedByUserId { get; init; }

    public VatReturnStatus Status { get; private set; } = VatReturnStatus.Generated;

    /// <summary>Operator-supplied reference from the regulator
    /// portal — what they typed in after submitting through the
    /// official website. Free-form because ETA hasn't published a
    /// canonical format yet.</summary>
    public string? RegulatorSubmissionReference { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }

    public string? RegulatorAcknowledgementReference { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }

    public string? Note { get; private set; }

    private VatReturn() { }

    public VatReturn(
        VatReturnPeriodKind periodKind,
        int periodYear,
        int periodOrdinal,
        DateOnly periodStart,
        DateOnly periodEnd,
        MoneyEgp outputVat,
        MoneyEgp inputVatRecoverable,
        MoneyEgp inputVatNonRecoverable,
        MoneyEgp netPayable,
        int contributingDocumentCount,
        DateTime generatedAtUtc,
        Guid generatedByUserId)
    {
        if (periodYear is < 1900 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(periodYear));
        switch (periodKind)
        {
            case VatReturnPeriodKind.VatMonthly when periodOrdinal is < 1 or > 12:
                throw new ArgumentOutOfRangeException(nameof(periodOrdinal), "Monthly VAT ordinal must be 1-12.");
            case VatReturnPeriodKind.VatQuarterly when periodOrdinal is < 1 or > 4:
                throw new ArgumentOutOfRangeException(nameof(periodOrdinal), "Quarterly VAT ordinal must be 1-4.");
        }
        if (periodStart > periodEnd)
            throw new ArgumentException("PeriodStart can't be after PeriodEnd.", nameof(periodStart));
        if (generatedByUserId == Guid.Empty)
            throw new ArgumentException("GeneratedByUserId is required.", nameof(generatedByUserId));

        PeriodKind = periodKind;
        PeriodYear = periodYear;
        PeriodOrdinal = periodOrdinal;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        OutputVat = outputVat;
        InputVatRecoverable = inputVatRecoverable;
        InputVatNonRecoverable = inputVatNonRecoverable;
        NetPayable = netPayable;
        ContributingDocumentCount = contributingDocumentCount;
        GeneratedAtUtc = generatedAtUtc;
        GeneratedByUserId = generatedByUserId;
    }

    /// <summary>Operator submitted the return via the regulator's
    /// portal and pasted back the reference number. Idempotent —
    /// re-marking submitted with the same reference is a no-op;
    /// changing the reference is refused (use a follow-up return
    /// instead).</summary>
    public void MarkSubmitted(string regulatorReference, Guid submittedByUserId, DateTime submittedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regulatorReference);
        if (submittedByUserId == Guid.Empty)
            throw new ArgumentException("SubmittedByUserId required.", nameof(submittedByUserId));

        if (Status == VatReturnStatus.Acknowledged)
            throw new InvalidOperationException($"VatReturn {Id} is already acknowledged.");
        if (Status == VatReturnStatus.Submitted)
        {
            // Idempotent ONLY for same reference; refuse a different
            // reference — accidental double-submit on different
            // references is almost always an operator error.
            if (!string.Equals(RegulatorSubmissionReference, regulatorReference.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"VatReturn {Id} already submitted with reference '{RegulatorSubmissionReference}'. " +
                    "Generate a new return to record a different submission.");
            }
            return;
        }

        RegulatorSubmissionReference = regulatorReference.Trim();
        SubmittedAtUtc = submittedAtUtc;
        SubmittedByUserId = submittedByUserId;
        Status = VatReturnStatus.Submitted;
    }

    public void MarkAcknowledged(string acknowledgementReference, Guid byUserId, DateTime acknowledgedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acknowledgementReference);
        if (Status == VatReturnStatus.Generated)
            throw new InvalidOperationException($"VatReturn {Id} hasn't been submitted yet.");
        if (Status == VatReturnStatus.Acknowledged) return; // idempotent

        RegulatorAcknowledgementReference = acknowledgementReference.Trim();
        AcknowledgedByUserId = byUserId;
        AcknowledgedAtUtc = acknowledgedAtUtc;
        Status = VatReturnStatus.Acknowledged;
    }

    public void UpdateNote(string? note)
    {
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

public enum VatReturnPeriodKind
{
    /// <summary>Standard regime — monthly VAT return.</summary>
    VatMonthly,
    /// <summary>Law 6/2025 simplified regime — quarterly VAT return.</summary>
    VatQuarterly,
}

public enum VatReturnStatus
{
    /// <summary>Numbers frozen but not yet submitted to the
    /// regulator. Operator can review + regenerate freely.</summary>
    Generated,
    /// <summary>Operator submitted the return through the regulator
    /// portal and pasted back the reference.</summary>
    Submitted,
    /// <summary>Regulator acknowledged the submission.</summary>
    Acknowledged,
}
