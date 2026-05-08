using EgyptTax.Application.Wht;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Tax;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Wht;

/// <summary>
/// FR-045 / US7 / T207 — EF-backed payload builder. Branches on
/// certificate direction to load the right voucher + invoice +
/// counterparty rows. Returns null when the certificate id doesn't
/// resolve. The Company row supplies the issuer fields.
/// </summary>
public sealed class SqlWhtCertificatePayloadBuilder : IWhtCertificatePayloadBuilder
{
    private readonly AppDbContext _db;

    public SqlWhtCertificatePayloadBuilder(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WhtCertificatePayload?> BuildAsync(
        Guid certificateId, CancellationToken cancellationToken = default)
    {
        var cert = await _db.Set<WhtCertificate>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == certificateId, cancellationToken);
        if (cert is null) return null;

        var company = await _db.Set<Company>().AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No Company row exists. Seed the company profile before issuing WHT certificates.");

        var category = await _db.Set<WhtCategory>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cert.WhtCategoryId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"WhtCategory {cert.WhtCategoryId} referenced by certificate {cert.Id} not found.");

        // Direction picks which side of the system the voucher +
        // counterparty live on.
        string voucherNumber;
        DateOnly voucherDate;
        string counterpartyTin;
        BilingualText counterpartyName;
        string invoiceNumber;
        DateOnly invoiceDate;
        decimal? gross;
        decimal? net;

        if (cert.Direction == WhtCertificateDirection.OutboundToSupplier)
        {
            var voucher = await _db.Set<SupplierPaymentVoucher>().AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == cert.SourceVoucherId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"SupplierPaymentVoucher {cert.SourceVoucherId} not found.");
            var supplier = await _db.Set<Supplier>().AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == cert.CounterpartyId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Supplier {cert.CounterpartyId} not found.");
            var invoice = await _db.Set<PurchaseInvoice>().AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == cert.SourceInvoiceId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"PurchaseInvoice {cert.SourceInvoiceId} not found.");

            voucherNumber = voucher.DocumentNumber ?? "(unknown)";
            voucherDate = voucher.PaymentDate;
            // Suppliers MUST have a TIN to receive a WHT certificate
            // (Egyptian tax authority requires it on the cert). The
            // TIN lives on the SupplierTaxProfile snapshot per FR-041.
            counterpartyTin = supplier.TaxProfile.TinValue
                ?? throw new InvalidOperationException(
                    $"Supplier {supplier.Id} has no TIN; cannot issue a WHT certificate without one (FR-045 + FR-041).");
            counterpartyName = new BilingualText(supplier.Name.Arabic, supplier.Name.English);
            invoiceNumber = invoice.DocumentNumber ?? "(unknown)";
            invoiceDate = invoice.DateReceived;
            gross = voucher.GrossPaymentAmount.Amount;
            net = voucher.NetCashPaid.Amount;
        }
        else
        {
            var voucher = await _db.Set<CustomerReceiptVoucher>().AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == cert.SourceVoucherId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"CustomerReceiptVoucher {cert.SourceVoucherId} not found.");
            var customer = await _db.Set<Customer>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cert.CounterpartyId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Customer {cert.CounterpartyId} not found.");
            var invoice = await _db.Set<SalesInvoice>().AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == cert.SourceInvoiceId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"SalesInvoice {cert.SourceInvoiceId} not found.");

            voucherNumber = voucher.DocumentNumber ?? "(unknown)";
            voucherDate = voucher.ReceiptDate;
            counterpartyTin = customer.TaxProfile.TinValue
                ?? throw new InvalidOperationException(
                    $"Customer {customer.Id} has no TIN; cannot record an inbound WHT certificate without one (FR-045 + FR-040).");
            counterpartyName = new BilingualText(customer.Name.Arabic, customer.Name.English);
            invoiceNumber = invoice.DocumentNumber ?? "(unknown)";
            invoiceDate = invoice.DocumentDate;
            gross = voucher.GrossReceiptAmount.Amount;
            net = voucher.NetCashReceived.Amount;
        }

        return new WhtCertificatePayload(
            CertificateNumber: cert.CertificateNumber,
            Direction: cert.Direction.ToString(),
            IssuedAt: cert.IssuedAtUtc,
            IssuerCompanyTin: company.TaxRegistrationNumber,
            IssuerCompanyName: new BilingualText(company.LegalName.Arabic, company.LegalName.English),
            CounterpartyTin: counterpartyTin,
            CounterpartyName: counterpartyName,
            SourceInvoiceNumber: invoiceNumber,
            SourceInvoiceDate: invoiceDate,
            SourceVoucherNumber: voucherNumber,
            SourceVoucherDate: voucherDate,
            WhtCategoryCode: category.Code,
            WhtCategoryName: new BilingualText(category.Name.Arabic, category.Name.English),
            RateAppliedPercent: cert.RateAppliedPercent,
            AmountWithheld: cert.AmountWithheld.Amount,
            GrossPayment: gross,
            NetPayment: net,
            Currency: "EGP",
            Language: "ar+en",
            AuditChainHash: null); // wired when audit-chain back-pointer lands (out of scope this batch)
    }
}
