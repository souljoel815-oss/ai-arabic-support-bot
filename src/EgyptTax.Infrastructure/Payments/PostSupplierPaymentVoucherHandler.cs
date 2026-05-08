using System.Globalization;
using EgyptTax.Application.Accounting;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Numbering;
using EgyptTax.Application.Payments;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Payments;

/// <summary>
/// Phase 9 / FR-051 — supplier payment voucher post handler.
/// Allocates a `SPV-{year}-{n}` document number, transitions the
/// aggregate via <see cref="SupplierPaymentVoucher.MarkPosted"/>,
/// emits the balanced JE inside the same SaveChangesAsync.
/// US7-ready: when the voucher carries a WHT split (set via
/// <c>ApplyWhtSplit</c> earlier in the draft lifecycle), the
/// emitter automatically produces the 3-line form (DR AP / CR Cash /
/// CR WhtPayable).
/// </summary>
public sealed class PostSupplierPaymentVoucherHandler
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberAllocator _allocator;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;
    private readonly ISupplierPaymentVoucherJournalEmitter? _journalEmitter;

    public PostSupplierPaymentVoucherHandler(
        AppDbContext db,
        IDocumentNumberAllocator allocator,
        IClock clock,
        IAuditLogStore auditLog,
        ISupplierPaymentVoucherJournalEmitter? journalEmitter = null)
    {
        _db = db;
        _allocator = allocator;
        _clock = clock;
        _auditLog = auditLog;
        _journalEmitter = journalEmitter;
    }

    public async Task<SupplierPaymentVoucher> HandleAsync(
        PostSupplierPaymentVoucherCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var voucher = await _db.Set<SupplierPaymentVoucher>()
            .Include(v => v.Allocations)
            .FirstOrDefaultAsync(v => v.Id == command.SupplierPaymentVoucherId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"SupplierPaymentVoucher {command.SupplierPaymentVoucherId} not found.");

        var fiscalYear = voucher.PaymentDate.Year;
        var documentNumber = await _allocator.AllocateAsync(
            DocumentType.SupplierPaymentVoucher, fiscalYear, cancellationToken);

        var nowUtc = _clock.UtcNow;
        voucher.MarkPosted(documentNumber, command.PostedByUserId, nowUtc);

        if (_journalEmitter is not null)
        {
            await _journalEmitter.EmitForSupplierPaymentAsync(voucher, nowUtc, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "supplier_payment_voucher.posted",
                ActorUserId: command.PostedByUserId,
                ActorFirmName: null, CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(voucher)),
            cancellationToken);

        return voucher;
    }

    private static string BuildPayloadJson(SupplierPaymentVoucher v)
    {
        var inv = CultureInfo.InvariantCulture;
        return $$"""{"voucher_id":"{{v.Id:D}}","supplier_id":"{{v.SupplierId:D}}","document_number":"{{v.DocumentNumber}}","payment_date":"{{v.PaymentDate:yyyy-MM-dd}}","payment_method":"{{v.PaymentMethod}}","gross_egp":{{v.GrossPaymentAmount.Amount.ToString("F2", inv)}},"wht_payable_egp":{{v.WhtPayableAmount.Amount.ToString("F2", inv)}},"net_cash_paid_egp":{{v.NetCashPaid.Amount.ToString("F2", inv)}},"allocation_count":{{v.Allocations.Count}}}""";
    }
}
