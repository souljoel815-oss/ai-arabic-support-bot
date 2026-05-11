using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Documents;

/// <summary>
/// C7 / FR-052 / Phase 9 — customer receipt voucher. Mirror of
/// <see cref="SupplierPaymentVoucher"/> on the receipts side:
/// records the cash leg of money received from a customer + the
/// optional WHT receivable when the customer withholds tax (US7).
/// Allocations distribute the gross receipt across one or more
/// outstanding SalesInvoices (or CreditNotes that net against
/// receivables).
///
/// Lifecycle: Draft → Posted (terminal). Posting emits the balanced
/// JE: DR Cash (net) + DR WHT Receivable (wht) + CR AR (gross).
/// At Phase 9 cut WHT amounts default to zero and the JE collapses
/// to DR Cash / CR AR.
/// </summary>
public sealed class CustomerReceiptVoucher
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public DateOnly ReceiptDate { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string PaymentReference { get; private set; } = "";
    public string? Note { get; private set; }
    public DocumentState State { get; private set; } = DocumentState.Draft;
    public string? DocumentNumber { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public Guid? PostedByUserId { get; private set; }

    /// <summary>
    /// P3.1 — which specific cashbox / bank account received the
    /// payment. Nullable for backward-compat; the JE emitter falls
    /// back to the legacy hard-coded <c>1100 Cash</c> account when null.
    /// </summary>
    public Guid? CashAccountId { get; private set; }

    public MoneyEgp GrossReceiptAmount { get; private set; } = MoneyEgp.Zero;

    /// <summary>FR-052 / US7 — withholding tax the customer withheld
    /// from this receipt. Zero at Phase 9 cut.</summary>
    public MoneyEgp WhtReceivableAmount { get; private set; } = MoneyEgp.Zero;

    /// <summary>Cash actually received = Gross − WhtReceivable.</summary>
    public MoneyEgp NetCashReceived { get; private set; } = MoneyEgp.Zero;

    /// <summary>FR-052 / US7 — back-pointer to the customer-issued
    /// WHT certificate the operator records on this receipt.</summary>
    public Guid? CustomerWhtCertificateId { get; private set; }

    private readonly List<PaymentAllocation> _allocations = new();
    public IReadOnlyCollection<PaymentAllocation> Allocations => _allocations;

    private CustomerReceiptVoucher() { }

    private CustomerReceiptVoucher(
        Guid customerId,
        DateOnly receiptDate,
        PaymentMethod paymentMethod,
        string paymentReference,
        string? note,
        MoneyEgp grossReceiptAmount
    )
    {
        CustomerId = customerId;
        ReceiptDate = receiptDate;
        PaymentMethod = paymentMethod;
        PaymentReference = paymentReference;
        Note = note;
        GrossReceiptAmount = grossReceiptAmount;
        NetCashReceived = grossReceiptAmount;
    }

    public static CustomerReceiptVoucher CreateDraft(
        Guid customerId,
        DateOnly receiptDate,
        PaymentMethod paymentMethod,
        string paymentReference,
        MoneyEgp grossReceiptAmount,
        string? note = null
    )
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        if (grossReceiptAmount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grossReceiptAmount),
                "Gross receipt amount must be positive."
            );
        }
        return new CustomerReceiptVoucher(
            customerId,
            receiptDate,
            paymentMethod,
            paymentReference,
            note,
            grossReceiptAmount
        );
    }

    public PaymentAllocation AddAllocation(
        Guid salesInvoiceId,
        MoneyEgp amount,
        DocumentType targetType = DocumentType.SalesInvoice
    )
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot add allocation to customer receipt voucher {Id}: state {State} is not Draft."
            );
        }
        var allocatedSoFar = _allocations.Sum(a => a.AllocatedAmount.Amount);
        if (allocatedSoFar + amount.Amount > GrossReceiptAmount.Amount)
        {
            throw new InvalidOperationException(
                $"Cannot allocate {amount.Amount:F2} to invoice {salesInvoiceId}: "
                    + $"would push voucher's allocated total {allocatedSoFar + amount.Amount:F2} past the gross receipt {GrossReceiptAmount.Amount:F2} (FR-053 voucher cap)."
            );
        }
        var allocation = new PaymentAllocation(
            supplierPaymentVoucherId: null,
            customerReceiptVoucherId: Id,
            targetDocumentId: salesInvoiceId,
            targetDocumentType: targetType,
            allocatedAmount: amount
        );
        _allocations.Add(allocation);
        return allocation;
    }

    /// <summary>
    /// P3.1 — pin the voucher to a specific cashbox / bank account.
    /// Allowed only while still in Draft.
    /// </summary>
    public void SetCashAccount(Guid cashAccountId)
    {
        if (cashAccountId == Guid.Empty)
        {
            throw new ArgumentException("CashAccountId is required.", nameof(cashAccountId));
        }
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot change the cash account on customer receipt voucher {Id}: state {State} is not Draft.");
        }
        CashAccountId = cashAccountId;
    }

    public void RemoveAllocation(Guid allocationId)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot remove allocation from customer receipt voucher {Id}: state {State} is not Draft."
            );
        }
        var found =
            _allocations.FirstOrDefault(a => a.Id == allocationId)
            ?? throw new InvalidOperationException(
                $"Allocation {allocationId} is not on voucher {Id}."
            );
        _allocations.Remove(found);
    }

    /// <summary>FR-052 / US7 — record the customer-issued WHT
    /// certificate on this receipt. Splits the gross into cash +
    /// WHT receivable. Phase 9 callers don't invoke this.</summary>
    public void ApplyCustomerWhtCertificate(
        MoneyEgp whtReceivableAmount,
        Guid customerWhtCertificateId
    )
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot apply customer WHT certificate to receipt voucher {Id}: state {State} is not Draft."
            );
        }
        if (whtReceivableAmount.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(whtReceivableAmount),
                "WHT receivable amount cannot be negative."
            );
        }
        if (whtReceivableAmount.Amount > GrossReceiptAmount.Amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(whtReceivableAmount),
                $"WHT receivable {whtReceivableAmount.Amount:F2} cannot exceed gross receipt {GrossReceiptAmount.Amount:F2}."
            );
        }
        WhtReceivableAmount = whtReceivableAmount;
        NetCashReceived = MoneyEgp.From(GrossReceiptAmount.Amount - whtReceivableAmount.Amount);
        CustomerWhtCertificateId = customerWhtCertificateId;
    }

    public void MarkPosted(string documentNumber, Guid postedByUserId, DateTime postedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot post customer receipt voucher {Id}: state {State} is not Draft."
            );
        }
        if (_allocations.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot post customer receipt voucher {Id}: at least one allocation is required."
            );
        }
        var allocatedTotal = _allocations.Sum(a => a.AllocatedAmount.Amount);
        if (allocatedTotal > GrossReceiptAmount.Amount)
        {
            throw new InvalidOperationException(
                $"Cannot post customer receipt voucher {Id}: allocations total {allocatedTotal:F2} exceeds gross receipt {GrossReceiptAmount.Amount:F2} (FR-053)."
            );
        }
        DocumentNumber = documentNumber;
        PostedByUserId = postedByUserId;
        PostedAtUtc = postedAtUtc;
        State = DocumentState.Posted;
    }
}
