using System.Globalization;
using EgyptTax.Application.Accounting;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Numbering;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Invoices;

/// <summary>
/// US1 / FR-026 — sales invoice posting handler. Looks up the
/// per-document-type approval setting (Phase-1 default for SalesInvoice
/// is <c>ApprovalRequired = false</c> so the direct-post path is
/// allowed), allocates the next document number from the FR-011
/// sequential allocator, transitions the aggregate to Posted via the
/// state machine, and emits the FR-026 audit event with
/// <c>posting_mode</c> captured. Designed to run **inside** the
/// MediatR <c>TransactionBehavior</c> envelope so the document-number
/// row, the state transition, and the audit-chain entry commit
/// atomically — a partial post that wastes a number is the failure
/// mode the test in <c>RollbackReleasesNumberTests</c> already proved
/// the allocator handles.
/// </summary>
public sealed class PostSalesInvoiceHandler
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberAllocator _allocator;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;
    private readonly IJournalEntryEmitter? _journalEmitter;

    public PostSalesInvoiceHandler(
        AppDbContext db,
        IDocumentNumberAllocator allocator,
        IClock clock,
        IAuditLogStore auditLog,
        IJournalEntryEmitter? journalEmitter = null)
    {
        _db = db;
        _allocator = allocator;
        _clock = clock;
        _auditLog = auditLog;
        _journalEmitter = journalEmitter;
    }

    public async Task<SalesInvoice> HandleAsync(
        PostSalesInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoice = await _db.Set<SalesInvoice>()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == command.SalesInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Sales invoice {command.SalesInvoiceId} not found.");

        // FR-013 — credit notes allocate from the CN series + use the
        // CreditNote approval setting; regular invoices use SalesInvoice.
        // Derived from the entity rather than the command so the
        // call-site is the same regardless of document type.
        var documentType = invoice.IsCreditNote ? DocumentType.CreditNote : DocumentType.SalesInvoice;
        var approvalSetting = await _db.Set<DocumentTypeApprovalSetting>()
            .FirstOrDefaultAsync(s => s.DocumentType == documentType, cancellationToken);
        var approvalRequired = approvalSetting?.ApprovalRequired ?? true;

        var fiscalYear = invoice.DocumentDate.Year;
        var documentNumber = await _allocator.AllocateAsync(
            documentType, fiscalYear, cancellationToken);

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

        // FR-035 — open the ETA submission row in Pending state with
        // the regulator-imposed 7-day window so the dashboard (T083
        // query) immediately surfaces it. Auto-submission to the mock
        // ETA endpoint is owned by a follow-up batch; the row exists
        // from post-time so the UI can show "Pending" before any
        // submission attempt fires.
        var etaSubmission = new EtaSubmission(
            salesInvoiceId: invoice.Id,
            postedAtUtc: nowUtc,
            nowUtc: nowUtc);
        _db.Add(etaSubmission);

        // T095 — emit the balanced journal entry IN THE SAME
        // SaveChangesAsync as the post + ETA-submission row so all
        // three commit atomically. If the emitter isn't wired (e.g.
        // legacy callers in tests) we skip silently; production DI
        // always supplies it.
        if (_journalEmitter is not null)
        {
            await _journalEmitter.EmitForSalesInvoiceAsync(invoice, nowUtc, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "sales_invoice.posted",
                ActorUserId: command.PostedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(invoice, postingMode)),
            cancellationToken);

        return invoice;
    }

    private static string BuildPayloadJson(SalesInvoice invoice, DocumentPostingMode postingMode) =>
        $$"""{"invoice_id":"{{invoice.Id:D}}","customer_id":"{{invoice.CustomerId:D}}","document_number":"{{invoice.DocumentNumber}}","document_date":"{{invoice.DocumentDate:yyyy-MM-dd}}","posted_at_utc":"{{invoice.PostedAtUtc?.ToString("o", CultureInfo.InvariantCulture)}}","posting_mode":"{{postingMode}}","subtotal_egp":{{invoice.Subtotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"vat_total_egp":{{invoice.VatTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"grand_total_egp":{{invoice.GrandTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"line_count":{{invoice.Lines.Count}}}""";
}
