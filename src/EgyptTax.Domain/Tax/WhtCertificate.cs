using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Tax;

/// <summary>
/// G1 / FR-045 / US7 — withholding-tax certificate (شهادة خصم).
/// Two flavours:
///   * <see cref="WhtCertificateDirection.OutboundToSupplier"/> —
///     auto-generated when the company withholds tax from a
///     supplier-services payment. The bilingual PDF (T207) is what
///     the supplier files for their own credit.
///   * <see cref="WhtCertificateDirection.InboundFromCustomer"/> —
///     recorded when a customer withholds tax from a payment to
///     the company; the customer-issued certificate id is
///     captured so the company can substantiate the WHT-receivable
///     asset.
///
/// Rate + amount are FROZEN to the certificate row at issue time
/// — even if the underlying <see cref="EgyptTax.Domain.MasterData.WhtCategory"/>
/// is later superseded, the certificate keeps its original numbers
/// (an Egyptian regulator never accepts retroactive rate changes).
/// </summary>
public sealed class WhtCertificate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public WhtCertificateDirection Direction { get; init; }
    public DateOnly Date { get; init; }
    public Guid CounterpartyId { get; init; }
    public Guid SourceVoucherId { get; init; }
    public Guid SourceInvoiceId { get; init; }
    public Guid WhtCategoryId { get; init; }
    public decimal RateAppliedPercent { get; init; }
    public MoneyEgp AmountWithheld { get; init; }
    public string CertificateNumber { get; init; } = "";
    public DateTime IssuedAtUtc { get; init; }

    private WhtCertificate() { }

    public WhtCertificate(
        WhtCertificateDirection direction,
        DateOnly date,
        Guid counterpartyId,
        Guid sourceVoucherId,
        Guid sourceInvoiceId,
        Guid whtCategoryId,
        decimal rateAppliedPercent,
        MoneyEgp amountWithheld,
        string certificateNumber,
        DateTime issuedAtUtc)
    {
        if (counterpartyId == Guid.Empty)
        {
            throw new ArgumentException("CounterpartyId is required.", nameof(counterpartyId));
        }
        if (sourceVoucherId == Guid.Empty)
        {
            throw new ArgumentException("SourceVoucherId is required.", nameof(sourceVoucherId));
        }
        if (sourceInvoiceId == Guid.Empty)
        {
            throw new ArgumentException("SourceInvoiceId is required.", nameof(sourceInvoiceId));
        }
        if (whtCategoryId == Guid.Empty)
        {
            throw new ArgumentException("WhtCategoryId is required.", nameof(whtCategoryId));
        }
        if (rateAppliedPercent < 0m || rateAppliedPercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(rateAppliedPercent),
                "Rate must be in the range [0, 100] percent.");
        }
        if (amountWithheld.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amountWithheld),
                "Withheld amount cannot be negative.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateNumber);

        Direction = direction;
        Date = date;
        CounterpartyId = counterpartyId;
        SourceVoucherId = sourceVoucherId;
        SourceInvoiceId = sourceInvoiceId;
        WhtCategoryId = whtCategoryId;
        RateAppliedPercent = rateAppliedPercent;
        AmountWithheld = amountWithheld;
        CertificateNumber = certificateNumber;
        IssuedAtUtc = issuedAtUtc;
    }
}

/// <summary>FR-045 — direction of a WHT certificate. Outbound:
/// company withholds from supplier (the certificate is OUR output,
/// the supplier files it). Inbound: customer withholds from us
/// (the certificate is the CUSTOMER's output, we record it as
/// substantiation for the WHT-receivable asset).</summary>
public enum WhtCertificateDirection
{
    OutboundToSupplier,
    InboundFromCustomer,
}
