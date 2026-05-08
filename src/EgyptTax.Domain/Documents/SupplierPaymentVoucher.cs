using EgyptTax.Domain.Workflow;
using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Documents;

/// <summary>
/// C6 / FR-051 / Phase 9 — supplier payment voucher. Records the
/// cash leg of paying a supplier + the optional WHT split that
/// US7 lights up. Allocations (1:N → <see cref="PaymentAllocation"/>)
/// distribute the gross payment across one or more outstanding
/// PurchaseInvoices.
///
/// Lifecycle: Draft → Posted (terminal). Allocations may be added
/// or removed only while Draft. Posting calls into the post handler
/// which (1) verifies SUM(allocations) ≤ gross AND each allocation
/// ≤ target invoice's open balance (FR-053), (2) emits the balanced
/// JE: DR AP (gross) + CR Cash (net) + CR WHT Payable (wht). At
/// Phase 9 cut WHT amounts default to zero and the JE collapses to
/// DR AP / CR Cash; US7 batches add the WHT compute.
/// </summary>
public sealed class SupplierPaymentVoucher
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SupplierId { get; init; }
    public DateOnly PaymentDate { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string PaymentReference { get; private set; } = "";
    public string? Note { get; private set; }
    public DocumentState State { get; private set; } = DocumentState.Draft;
    public string? DocumentNumber { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public Guid? PostedByUserId { get; private set; }

    public MoneyEgp GrossPaymentAmount { get; private set; } = MoneyEgp.Zero;

    /// <summary>FR-051 / US7 — withholding-tax payable on this payment.
    /// Zero at Phase 9 cut; US7 compute fills it on Post via
    /// <see cref="ApplyWhtSplit"/>.</summary>
    public MoneyEgp WhtPayableAmount { get; private set; } = MoneyEgp.Zero;

    /// <summary>Cash actually paid to the supplier = Gross − WhtPayable.</summary>
    public MoneyEgp NetCashPaid { get; private set; } = MoneyEgp.Zero;

    /// <summary>FR-051 / US7 — back-pointer to the auto-generated
    /// outbound WHT certificate. Filled by US7 when the WHT split is
    /// applied + the certificate is created.</summary>
    public Guid? GeneratedWhtCertificateId { get; private set; }

    private readonly List<PaymentAllocation> _allocations = new();
    public IReadOnlyCollection<PaymentAllocation> Allocations => _allocations;

    private SupplierPaymentVoucher() { }

    private SupplierPaymentVoucher(
        Guid supplierId, DateOnly paymentDate, PaymentMethod paymentMethod,
        string paymentReference, string? note, MoneyEgp grossPaymentAmount)
    {
        SupplierId = supplierId;
        PaymentDate = paymentDate;
        PaymentMethod = paymentMethod;
        PaymentReference = paymentReference;
        Note = note;
        GrossPaymentAmount = grossPaymentAmount;
        NetCashPaid = grossPaymentAmount; // initial = gross; WHT subtraction happens at Post
    }

    public static SupplierPaymentVoucher CreateDraft(
        Guid supplierId,
        DateOnly paymentDate,
        PaymentMethod paymentMethod,
        string paymentReference,
        MoneyEgp grossPaymentAmount,
        string? note = null)
    {
        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("SupplierId is required.", nameof(supplierId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        if (grossPaymentAmount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(grossPaymentAmount),
                "Gross payment amount must be positive.");
        }
        return new SupplierPaymentVoucher(supplierId, paymentDate, paymentMethod,
            paymentReference, note, grossPaymentAmount);
    }

    /// <summary>
    /// Allocate part of the gross payment to a specific posted
    /// purchase invoice. Refuses if the cumulative allocated total
    /// would exceed gross (FR-053 voucher cap). The per-invoice cap
    /// (allocation ≤ invoice open balance) is checked in the
    /// application handler because it requires a DB lookup.
    /// </summary>
    public PaymentAllocation AddAllocation(Guid purchaseInvoiceId, MoneyEgp amount)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot add allocation to supplier payment voucher {Id}: state {State} is not Draft.");
        }
        var allocatedSoFar = _allocations.Sum(a => a.AllocatedAmount.Amount);
        if (allocatedSoFar + amount.Amount > GrossPaymentAmount.Amount)
        {
            throw new InvalidOperationException(
                $"Cannot allocate {amount.Amount:F2} to invoice {purchaseInvoiceId}: " +
                $"would push voucher's allocated total {allocatedSoFar + amount.Amount:F2} past the gross payment {GrossPaymentAmount.Amount:F2} (FR-053 voucher cap).");
        }
        var allocation = new PaymentAllocation(
            supplierPaymentVoucherId: Id,
            customerReceiptVoucherId: null,
            targetDocumentId: purchaseInvoiceId,
            targetDocumentType: DocumentType.PurchaseInvoice,
            allocatedAmount: amount);
        _allocations.Add(allocation);
        return allocation;
    }

    public void RemoveAllocation(Guid allocationId)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot remove allocation from supplier payment voucher {Id}: state {State} is not Draft.");
        }
        var found = _allocations.FirstOrDefault(a => a.Id == allocationId)
            ?? throw new InvalidOperationException($"Allocation {allocationId} is not on voucher {Id}.");
        _allocations.Remove(found);
    }

    /// <summary>FR-051 / US7 — apply the WHT split computed by US7's
    /// WhtComputeService. Fills WhtPayableAmount + NetCashPaid +
    /// GeneratedWhtCertificateId. Phase 9 callers leave WHT at zero
    /// (no certificate produced).</summary>
    public void ApplyWhtSplit(MoneyEgp whtPayableAmount, Guid generatedWhtCertificateId)
    {
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot apply WHT split to supplier payment voucher {Id}: state {State} is not Draft.");
        }
        if (whtPayableAmount.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(whtPayableAmount),
                "WHT payable amount cannot be negative.");
        }
        if (whtPayableAmount.Amount > GrossPaymentAmount.Amount)
        {
            throw new ArgumentOutOfRangeException(nameof(whtPayableAmount),
                $"WHT payable {whtPayableAmount.Amount:F2} cannot exceed gross payment {GrossPaymentAmount.Amount:F2}.");
        }
        WhtPayableAmount = whtPayableAmount;
        NetCashPaid = MoneyEgp.From(GrossPaymentAmount.Amount - whtPayableAmount.Amount);
        GeneratedWhtCertificateId = generatedWhtCertificateId;
    }

    public void MarkPosted(string documentNumber, Guid postedByUserId, DateTime postedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        if (State != DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot post supplier payment voucher {Id}: state {State} is not Draft.");
        }
        if (_allocations.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot post supplier payment voucher {Id}: at least one allocation is required.");
        }
        var allocatedTotal = _allocations.Sum(a => a.AllocatedAmount.Amount);
        if (allocatedTotal > GrossPaymentAmount.Amount)
        {
            throw new InvalidOperationException(
                $"Cannot post supplier payment voucher {Id}: allocations total {allocatedTotal:F2} exceeds gross payment {GrossPaymentAmount.Amount:F2} (FR-053).");
        }
        DocumentNumber = documentNumber;
        PostedByUserId = postedByUserId;
        PostedAtUtc = postedAtUtc;
        State = DocumentState.Posted;
    }
}
