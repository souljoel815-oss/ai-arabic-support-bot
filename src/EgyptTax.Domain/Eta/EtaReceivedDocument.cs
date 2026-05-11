namespace EgyptTax.Domain.Eta;

/// <summary>
/// P1.5 — one document the operator has RECEIVED from a supplier
/// via the ETA platform (counterpart to <see cref="EtaSubmission"/>
/// which tracks documents we SENT). Populated by the Hangfire
/// inbox-pull job from the regulator's "Get Received Documents"
/// endpoint; the operator works the resulting queue from the
/// ETA Inbox page (review → import as Purchase Invoice draft).
///
/// Stored as a thin "letter from the regulator" envelope: we
/// capture supplier identity + document totals + the regulator's
/// long UUID for traceability, but DO NOT bake the lines or the
/// full payload into our schema. The operator triggers the
/// per-document Purchase-Invoice creation explicitly so FR-016
/// (attachments + categorisation review) is satisfied.
/// </summary>
public sealed class EtaReceivedDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The regulator-side long UUID — natural unique key for dedupe.</summary>
    public string RegulatorLongUuid { get; init; } = default!;

    public string SupplierTin { get; init; } = default!;
    public string SupplierLegalName { get; init; } = default!;
    public string DocumentNumber { get; init; } = default!;
    public DateOnly DocumentDate { get; init; }

    public decimal NetBeforeVatEgp { get; init; }
    public decimal VatTotalEgp { get; init; }
    public decimal GrandTotalEgp { get; init; }

    public EtaReceivedDocumentStatus Status { get; private set; }
        = EtaReceivedDocumentStatus.NeedsReview;

    /// <summary>When the inbox job first pulled this document.</summary>
    public DateTime FirstSeenAtUtc { get; init; }

    /// <summary>
    /// When an operator imported this received document into the
    /// books as a Purchase Invoice draft (or marked it ignored).
    /// </summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Set when Status transitions to Imported — points at the new PurchaseInvoice.</summary>
    public Guid? ImportedAsPurchaseInvoiceId { get; private set; }

    private EtaReceivedDocument() { }

    public EtaReceivedDocument(
        string regulatorLongUuid,
        string supplierTin,
        string supplierLegalName,
        string documentNumber,
        DateOnly documentDate,
        decimal netBeforeVatEgp,
        decimal vatTotalEgp,
        decimal grandTotalEgp,
        DateTime firstSeenAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regulatorLongUuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierTin);
        ArgumentException.ThrowIfNullOrWhiteSpace(supplierLegalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        RegulatorLongUuid = regulatorLongUuid;
        SupplierTin = supplierTin;
        SupplierLegalName = supplierLegalName;
        DocumentNumber = documentNumber;
        DocumentDate = documentDate;
        NetBeforeVatEgp = netBeforeVatEgp;
        VatTotalEgp = vatTotalEgp;
        GrandTotalEgp = grandTotalEgp;
        FirstSeenAtUtc = firstSeenAtUtc;
    }

    public void MarkImported(Guid purchaseInvoiceId, DateTime nowUtc)
    {
        if (purchaseInvoiceId == Guid.Empty)
        {
            throw new ArgumentException("Must point at a real Purchase Invoice.", nameof(purchaseInvoiceId));
        }
        if (Status != EtaReceivedDocumentStatus.NeedsReview)
        {
            throw new InvalidOperationException(
                $"EtaReceivedDocument {Id} is in status {Status}; only NeedsReview rows can be imported.");
        }
        Status = EtaReceivedDocumentStatus.Imported;
        ImportedAsPurchaseInvoiceId = purchaseInvoiceId;
        ResolvedAtUtc = nowUtc;
    }

    public void MarkIgnored(DateTime nowUtc)
    {
        if (Status != EtaReceivedDocumentStatus.NeedsReview)
        {
            throw new InvalidOperationException(
                $"EtaReceivedDocument {Id} is in status {Status}; only NeedsReview rows can be ignored.");
        }
        Status = EtaReceivedDocumentStatus.Ignored;
        ResolvedAtUtc = nowUtc;
    }
}

public enum EtaReceivedDocumentStatus
{
    /// <summary>Default — operator hasn't decided yet.</summary>
    NeedsReview,
    /// <summary>Imported as a Purchase Invoice draft.</summary>
    Imported,
    /// <summary>Operator dismissed the document (duplicate, unrelated, etc.).</summary>
    Ignored,
}
