using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v5 E.1 — money a customer has paid up-front before any final
/// sales invoice is issued. Egyptian construction / custom-mfg /
/// professional-services SMBs collect deposits ("دفعة مقدمة") all
/// the time; today the operator either books the deposit as a
/// regular receipt (which lands as a stray credit on the customer
/// ledger) or — worse — fakes an invoice and posts early
/// (premature revenue recognition; IFRS / EAS violation).
///
/// Flow:
///   1. Operator records a CustomerAdvance against a posted
///      quotation (or any customer, no quotation required).
///   2. JE on post: DR Cash 1100 / CR Customer Advances 2310 —
///      cash hit + a liability for the held amount.
///   3. Later, when the final sales invoice posts, the operator
///      applies the advance against it: AppliedToInvoiceId is set
///      and an offset JE runs DR Customer Advances 2310 / CR AR
///      1200 — clearing the held liability against the new
///      receivable.
///   4. The CustomerStatement surfaces unapplied advances as a
///      credit row so the customer ledger reflects what's
///      actually owed.
///
/// MVP scope: one advance → one final invoice (no partial
/// draw-downs across multiple invoices). Multi-currency advances
/// also out of scope until per-invoice currency override ships.
/// </summary>
public sealed class CustomerAdvance
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public Guid CashAccountId { get; init; }
    public MoneyEgp Amount { get; init; } = MoneyEgp.Zero;
    public DateOnly ReceivedDate { get; init; }
    public Guid? QuotationId { get; private set; }
    public string? Notes { get; private set; }
    public CustomerAdvanceStatus Status { get; private set; } = CustomerAdvanceStatus.Held;
    public Guid? AppliedToInvoiceId { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }

    private CustomerAdvance() { }

    public CustomerAdvance(
        Guid customerId,
        Guid cashAccountId,
        MoneyEgp amount,
        DateOnly receivedDate,
        Guid? quotationId,
        string? notes,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }
        if (cashAccountId == Guid.Empty)
        {
            throw new ArgumentException("CashAccountId is required.", nameof(cashAccountId));
        }
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount),
                "Advance amount must be positive — zero / negative makes no sense.");
        }
        CustomerId = customerId;
        CashAccountId = cashAccountId;
        Amount = amount;
        ReceivedDate = receivedDate;
        QuotationId = quotationId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    /// <summary>
    /// Apply this held advance against the given sales invoice. The
    /// caller is responsible for posting the offset JE
    /// (DR 2310 / CR 1200) — this method only records the
    /// state change so the customer-statement query can omit the
    /// advance from the open-credit list.
    /// </summary>
    public void ApplyTo(Guid invoiceId, DateTime nowUtc)
    {
        if (Status != CustomerAdvanceStatus.Held)
        {
            throw new InvalidOperationException(
                $"Cannot apply advance {Id}: status is {Status}, not Held.");
        }
        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));
        }
        AppliedToInvoiceId = invoiceId;
        AppliedAtUtc = nowUtc;
        Status = CustomerAdvanceStatus.Applied;
    }

    public void Refund(DateTime nowUtc)
    {
        if (Status != CustomerAdvanceStatus.Held)
        {
            throw new InvalidOperationException(
                $"Cannot refund advance {Id}: status is {Status}, not Held.");
        }
        AppliedAtUtc = nowUtc;
        Status = CustomerAdvanceStatus.Refunded;
    }
}

public enum CustomerAdvanceStatus
{
    /// <summary>Money received, sitting in 2310 Customer Advances.</summary>
    Held,
    /// <summary>Applied against a final sales invoice — AR cleared by that amount.</summary>
    Applied,
    /// <summary>Returned to the customer (engagement cancelled). Reverses the original receipt.</summary>
    Refunded,
}
