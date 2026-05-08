using System.Globalization;
using EgyptTax.Application.Accounting;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Numbering;
using EgyptTax.Application.Payments;
using EgyptTax.Application.Wht;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Tax;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Payments;

/// <summary>
/// Phase 9 / FR-052 — customer receipt voucher post handler.
/// Mirror of <see cref="PostSupplierPaymentVoucherHandler"/> on
/// the receipts side. Allocates a `CRV-{year}-{n}` document
/// number, transitions the aggregate, emits the balanced JE
/// (DR Cash / CR AR; or 3-line with DR WhtReceivable when US7's
/// customer-WHT certificate has been recorded).
/// </summary>
public sealed class PostCustomerReceiptVoucherHandler
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberAllocator _allocator;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;
    private readonly ICustomerReceiptVoucherJournalEmitter? _journalEmitter;
    private readonly IWhtComputeService? _whtCompute;

    public PostCustomerReceiptVoucherHandler(
        AppDbContext db,
        IDocumentNumberAllocator allocator,
        IClock clock,
        IAuditLogStore auditLog,
        ICustomerReceiptVoucherJournalEmitter? journalEmitter = null,
        IWhtComputeService? whtCompute = null)
    {
        _db = db;
        _allocator = allocator;
        _clock = clock;
        _auditLog = auditLog;
        _journalEmitter = journalEmitter;
        _whtCompute = whtCompute;
    }

    public async Task<CustomerReceiptVoucher> HandleAsync(
        PostCustomerReceiptVoucherCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var voucher = await _db.Set<CustomerReceiptVoucher>()
            .Include(v => v.Allocations)
            .FirstOrDefaultAsync(v => v.Id == command.CustomerReceiptVoucherId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"CustomerReceiptVoucher {command.CustomerReceiptVoucherId} not found.");

        var fiscalYear = voucher.ReceiptDate.Year;
        var documentNumber = await _allocator.AllocateAsync(
            DocumentType.CustomerReceiptVoucher, fiscalYear, cancellationToken);

        var nowUtc = _clock.UtcNow;

        // FR-052 / US7 — when the customer issued a WHT certificate
        // (the customer withheld tax from their payment), record the
        // inbound certificate row and apply the split on the voucher
        // BEFORE MarkPosted. The customer-supplied amount is the
        // authoritative value; we look up the category id for audit
        // (the WhtComputeService just resolves the row, the amount
        // it computes is informational here — the customer's number
        // is what hits the books).
        WhtCertificate? cert = null;
        if (command.CustomerWhtCertificateNumber is not null
            && command.CustomerWhtAmount is { } amount
            && command.WhtCategoryCode is not null
            && command.WhtSourceInvoiceId is { } invoiceId
            && _whtCompute is not null)
        {
            // Resolve the category for audit (we record its id on the
            // certificate row); the amount on the cert is the
            // customer-supplied number.
            var compute = await _whtCompute.ComputeAsync(
                command.WhtCategoryCode, voucher.ReceiptDate,
                voucher.GrossReceiptAmount, WhtApplicableTo.CustomersServices,
                cancellationToken);
            if (compute is null)
            {
                throw new InvalidOperationException(
                    $"WHT category '{command.WhtCategoryCode}' is not effective on receipt date {voucher.ReceiptDate:yyyy-MM-dd} for CustomersServices direction.");
            }
            cert = new WhtCertificate(
                direction: WhtCertificateDirection.InboundFromCustomer,
                date: voucher.ReceiptDate,
                counterpartyId: voucher.CustomerId,
                sourceVoucherId: voucher.Id,
                sourceInvoiceId: invoiceId,
                whtCategoryId: compute.WhtCategoryId,
                rateAppliedPercent: compute.RateAppliedPercent,
                amountWithheld: MoneyEgp.From(amount),
                certificateNumber: command.CustomerWhtCertificateNumber,
                issuedAtUtc: nowUtc);
            _db.Add(cert);
            voucher.ApplyCustomerWhtCertificate(MoneyEgp.From(amount), cert.Id);
        }

        voucher.MarkPosted(documentNumber, command.PostedByUserId, nowUtc);

        if (_journalEmitter is not null)
        {
            await _journalEmitter.EmitForCustomerReceiptAsync(voucher, nowUtc, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "customer_receipt_voucher.posted",
                ActorUserId: command.PostedByUserId,
                ActorFirmName: null, CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(voucher)),
            cancellationToken);

        return voucher;
    }

    private static string BuildPayloadJson(CustomerReceiptVoucher v)
    {
        var inv = CultureInfo.InvariantCulture;
        return $$"""{"voucher_id":"{{v.Id:D}}","customer_id":"{{v.CustomerId:D}}","document_number":"{{v.DocumentNumber}}","receipt_date":"{{v.ReceiptDate:yyyy-MM-dd}}","payment_method":"{{v.PaymentMethod}}","gross_egp":{{v.GrossReceiptAmount.Amount.ToString("F2", inv)}},"wht_receivable_egp":{{v.WhtReceivableAmount.Amount.ToString("F2", inv)}},"net_cash_received_egp":{{v.NetCashReceived.Amount.ToString("F2", inv)}},"allocation_count":{{v.Allocations.Count}}}""";
    }
}
