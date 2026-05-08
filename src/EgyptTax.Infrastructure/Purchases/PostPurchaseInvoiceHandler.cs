using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Numbering;
using EgyptTax.Application.Periods;
using EgyptTax.Application.Purchases;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Purchases;

/// <summary>
/// US2 / FR-016 / FR-026 — purchase invoice posting handler. Mirrors
/// <see cref="EgyptTax.Infrastructure.Invoices.PostSalesInvoiceHandler"/>
/// but on the buy side. Allocates a `PI-{year}-{n}` series number
/// from the FR-011 sequential allocator (DocumentType.PurchaseInvoice),
/// transitions the aggregate via <see cref="PurchaseInvoice.MarkPosted"/>,
/// and emits an FR-028 audit event.
///
/// FR-016 enforcement: any line marked deductible MUST have at least
/// one Attachment row pointing at this document. The check happens
/// here at post-time — the operator can attach in any order while the
/// document is in Draft. The post fails if the invariant is violated.
/// </summary>
public sealed class PostPurchaseInvoiceHandler
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberAllocator _allocator;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;
    private readonly ITaxPeriodLockGuard? _periodLockGuard;

    public PostPurchaseInvoiceHandler(
        AppDbContext db,
        IDocumentNumberAllocator allocator,
        IClock clock,
        IAuditLogStore auditLog,
        ITaxPeriodLockGuard? periodLockGuard = null)
    {
        _db = db;
        _allocator = allocator;
        _clock = clock;
        _auditLog = auditLog;
        _periodLockGuard = periodLockGuard;
    }

    public async Task<PurchaseInvoice> HandleAsync(
        PostPurchaseInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoice = await _db.Set<PurchaseInvoice>()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.PurchaseInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Purchase invoice {command.PurchaseInvoiceId} not found.");

        // FR-037 — reject backdated posts into a Locked VAT period
        // BEFORE numbering / FR-016 attachment check.
        if (_periodLockGuard is not null)
        {
            var lockCheck = await _periodLockGuard.CheckVatMonthAsync(
                invoice.DateReceived, cancellationToken);
            if (lockCheck.IsLocked)
            {
                await _auditLog.AppendAsync(new AuditLogPayload(
                    Kind: "tax_period.post_rejected",
                    ActorUserId: command.PostedByUserId, ActorFirmName: null, CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"invoice_id":"{{invoice.Id:D}}","document_type":"PurchaseInvoice","document_date":"{{invoice.DateReceived:yyyy-MM-dd}}","period_year":{{lockCheck.Year}},"period_month":{{lockCheck.MonthOrQuarter}}}"""),
                    cancellationToken);
                throw new InvalidOperationException(
                    $"Cannot post purchase invoice {invoice.Id}: date received {invoice.DateReceived:yyyy-MM-dd} falls inside Locked VAT period {lockCheck.Year}-{lockCheck.MonthOrQuarter:D2} (FR-037). An Administrator must reopen the period before backdated posts are allowed.");
            }
        }

        // FR-016 — deductible lines require at least one attachment.
        // Checked at post-time (NOT add-line-time) so the operator can
        // attach in any order during draft editing.
        if (invoice.Lines.Any(l => l.DeductibleFlag))
        {
            var attachmentCount = await _db.Set<Attachment>()
                .CountAsync(a => a.DocumentId == invoice.Id
                    && a.DocumentType == DocumentType.PurchaseInvoice, cancellationToken);
            if (attachmentCount == 0)
            {
                throw new InvalidOperationException(
                    $"Cannot post purchase invoice {invoice.Id}: at least one line is marked deductible but no attachment is on file. Per FR-016, deductible expenses require supporting documents.");
            }
        }

        var approvalSetting = await _db.Set<DocumentTypeApprovalSetting>()
            .FirstOrDefaultAsync(s => s.DocumentType == DocumentType.PurchaseInvoice, cancellationToken);
        var approvalRequired = approvalSetting?.ApprovalRequired ?? true;

        // T159 / FR-026 — see the matching guard in
        // PostSalesInvoiceHandler. Refuse Draft → Posted when this
        // type requires approval, BEFORE the allocator runs.
        if (approvalRequired && invoice.State == DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot post PurchaseInvoice {invoice.Id}: this document type requires approval (FR-026). " +
                "Submit the document for approval, then have an Approver approve it before posting.");
        }

        var fiscalYear = invoice.DateReceived.Year;
        var documentNumber = await _allocator.AllocateAsync(
            DocumentType.PurchaseInvoice, fiscalYear, cancellationToken);

        var postingMode = approvalRequired
            ? DocumentPostingMode.ApprovedThenPosted
            : DocumentPostingMode.UnapprovedDirect;

        var nowUtc = _clock.UtcNow;
        invoice.MarkPosted(
            documentNumber: documentNumber,
            postedByUserId: command.PostedByUserId,
            postedAtUtc: nowUtc,
            postingMode: postingMode,
            approvalEnabled: approvalRequired);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "purchase_invoice.posted",
                ActorUserId: command.PostedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(invoice, postingMode)),
            cancellationToken);

        return invoice;
    }

    private static string BuildPayloadJson(PurchaseInvoice invoice, DocumentPostingMode postingMode) =>
        $$"""{"invoice_id":"{{invoice.Id:D}}","supplier_id":"{{invoice.SupplierId:D}}","supplier_invoice_number":"{{invoice.SupplierInvoiceNumber}}","document_number":"{{invoice.DocumentNumber}}","date_received":"{{invoice.DateReceived:yyyy-MM-dd}}","posted_at_utc":"{{invoice.PostedAtUtc?.ToString("o", CultureInfo.InvariantCulture)}}","posting_mode":"{{postingMode}}","subtotal_egp":{{invoice.Subtotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"vat_total_egp":{{invoice.VatTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"grand_total_egp":{{invoice.GrandTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"line_count":{{invoice.Lines.Count}},"deductible_line_count":{{invoice.Lines.Count(l => l.DeductibleFlag)}}}""";
}
