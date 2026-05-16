using System.Globalization;
using EgyptTax.Application.Accounting;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Application.Numbering;
using EgyptTax.Application.Periods;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
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
    private readonly ITaxPeriodLockGuard? _periodLockGuard;
    private readonly EgyptTax.Infrastructure.Api.WebhookDispatcher? _webhooks;

    public PostSalesInvoiceHandler(
        AppDbContext db,
        IDocumentNumberAllocator allocator,
        IClock clock,
        IAuditLogStore auditLog,
        IJournalEntryEmitter? journalEmitter = null,
        ITaxPeriodLockGuard? periodLockGuard = null,
        EgyptTax.Infrastructure.Api.WebhookDispatcher? webhooks = null
    )
    {
        _db = db;
        _allocator = allocator;
        _clock = clock;
        _auditLog = auditLog;
        _journalEmitter = journalEmitter;
        _periodLockGuard = periodLockGuard;
        _webhooks = webhooks;
    }

    public async Task<SalesInvoice> HandleAsync(
        PostSalesInvoiceCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var invoice =
            await _db.Set<SalesInvoice>()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == command.SalesInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Sales invoice {command.SalesInvoiceId} not found."
            );

        // FR-037 — reject backdated posts into a Locked tax period
        // BEFORE the document number is allocated. The audit chain
        // captures the rejection via the post handler's caller (the
        // Razor page surfaces the InvalidOperationException + writes
        // a `tax_period.post_rejected` audit event).
        if (_periodLockGuard is not null)
        {
            var lockCheck = await _periodLockGuard.CheckVatMonthAsync(
                invoice.DocumentDate,
                cancellationToken
            );
            if (lockCheck.IsLocked)
            {
                await _auditLog.AppendAsync(
                    new AuditLogPayload(
                        Kind: "tax_period.post_rejected",
                        ActorUserId: command.PostedByUserId,
                        ActorFirmName: null,
                        CompanyId: Guid.Empty,
                        PayloadJson: $$"""{"invoice_id":"{{invoice.Id:D}}","document_type":"SalesInvoice","document_date":"{{invoice.DocumentDate:yyyy-MM-dd}}","period_year":{{lockCheck.Year}},"period_month":{{lockCheck.MonthOrQuarter}}}"""
                    ),
                    cancellationToken
                );
                throw new InvalidOperationException(
                    $"Cannot post sales invoice {invoice.Id}: document date {invoice.DocumentDate:yyyy-MM-dd} falls inside Locked VAT period {lockCheck.Year}-{lockCheck.MonthOrQuarter:D2} (FR-037). An Administrator must reopen the period before backdated posts are allowed."
                );
            }
        }

        // Phase C.2 — credit-limit guard. Skipped for credit notes
        // (which REDUCE the receivable). For regular sales invoices,
        // load the customer's CreditLimit; if set, sum the customer's
        // existing posted receivable + this invoice's grand total and
        // refuse the post when it would push the customer over.
        // Note: SQLite's EF translator can't Sum() decimals server-
        // side ("cannot apply aggregate operator 'Sum' on decimal"),
        // so we project the amounts to a list and sum in memory. The
        // row count per customer stays small (a single rep's book of
        // business), so this is operationally fine.
        if (!invoice.IsCreditNote)
        {
            var customer = await _db.Set<Customer>().AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == invoice.CustomerId, cancellationToken);
            if (customer?.CreditLimitEgp is { } limit)
            {
                var postedSalesAmounts = await _db.Set<SalesInvoice>().AsNoTracking()
                    .Where(i => i.CustomerId == invoice.CustomerId
                        && i.State == DocumentState.Posted)
                    .Select(i => i.GrandTotal.Amount)
                    .ToListAsync(cancellationToken);
                // Credit-note grand totals are negative; receipts go on
                // the credit side. The list above includes credit notes
                // by construction (negative amounts subtract via Sum).
                // Receipts we deduct explicitly.
                var receiptAmounts = await _db.Set<CustomerReceiptVoucher>().AsNoTracking()
                    .Where(r => r.CustomerId == invoice.CustomerId
                        && r.State == DocumentState.Posted)
                    .Select(r => r.GrossReceiptAmount.Amount)
                    .ToListAsync(cancellationToken);
                var existingReceivable = postedSalesAmounts.Sum() - receiptAmounts.Sum();
                var projected = existingReceivable + invoice.GrandTotal.Amount;
                if (projected > limit)
                {
                    throw new InvalidOperationException(
                        $"Cannot post sales invoice {invoice.Id}: this would push the customer's "
                            + $"outstanding balance to {projected:F2} EGP, above their credit limit of "
                            + $"{limit:F2} EGP. Collect a receipt or raise the limit before posting."
                    );
                }
            }
        }

        // FR-013 — credit notes allocate from the CN series + use the
        // CreditNote approval setting; regular invoices use SalesInvoice.
        // Derived from the entity rather than the command so the
        // call-site is the same regardless of document type.
        var documentType = invoice.IsCreditNote
            ? DocumentType.CreditNote
            : DocumentType.SalesInvoice;
        var approvalSetting = await _db.Set<DocumentTypeApprovalSetting>()
            .FirstOrDefaultAsync(s => s.DocumentType == documentType, cancellationToken);
        var approvalRequired = approvalSetting?.ApprovalRequired ?? true;

        // T159 / FR-026 — when this document type requires approval,
        // refuse to direct-post a Draft. The Approver MUST move the
        // document Draft → Submitted → Approved first; only then can
        // it land on Posted. The state machine would also reject this
        // transition, but failing here surfaces a clearer message AND
        // skips the allocator below — no wasted document number on a
        // gated post.
        if (approvalRequired && invoice.State == DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot post {documentType} {invoice.Id}: this document type requires approval (FR-026). "
                    + "Submit the document for approval, then have an Approver approve it before posting."
            );
        }

        var fiscalYear = invoice.DocumentDate.Year;
        var nowUtc = _clock.UtcNow;

        // Phase D — stock check + decrement runs BEFORE MarkPosted so
        // an insufficient-stock throw leaves the invoice's in-memory
        // State as Draft (BUG-D-001 fix). Otherwise the page's tracked
        // entity shows Posted on reload even though the rollback kept
        // the DB row at Draft, which is misleading.
        //
        // Sales invoices decrement; credit notes (negated quantities
        // → negative line.Quantity) effectively increment because we
        // apply the line's signed quantity directly. Items are loaded
        // with tracking so the QuantityOnHand mutation is included in
        // the pending SaveChanges below.
        var lineItemIds = invoice.Lines.Select(l => l.ItemId).Distinct().ToArray();
        var trackedItems = await _db.Set<EgyptTax.Domain.MasterData.Item>()
            .Where(i => lineItemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var pendingMovements = new List<EgyptTax.Domain.MasterData.StockMovement>();

        // L4 phase 2 — load the default-location row alongside so we
        // can mirror per-location stock changes. Safe-clamp design:
        // never throw on per-location decrement (Item.QuantityOnHand
        // stays authoritative). Installs that haven't seeded location
        // rows for these items see no behaviour change.
        var defaultLocationId = await _db.Set<EgyptTax.Domain.MasterData.StockLocation>()
            .Where(l => l.IsDefault && l.IsActive)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var locationStockRows = defaultLocationId is { } locId
            ? await _db.Set<EgyptTax.Domain.MasterData.ItemStockByLocation>()
                .Where(s => lineItemIds.Contains(s.ItemId) && s.LocationId == locId)
                .ToDictionaryAsync(s => s.ItemId, cancellationToken)
            : new Dictionary<Guid, EgyptTax.Domain.MasterData.ItemStockByLocation>();

        foreach (var line in invoice.Lines)
        {
            if (!trackedItems.TryGetValue(line.ItemId, out var item)) continue;
            var qty = line.Quantity; // negative on credit notes
            if (qty > 0)
            {
                try { item.DecreaseStock(qty); }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        $"Cannot post invoice {invoice.Id}: {ex.Message} "
                            + "Receive more stock or reduce the line quantity before posting.",
                        ex);
                }

                // Mirror to per-location row — clamp to 0 to stay
                // additive. If the row was never seeded for this
                // item, skip entirely (operator hasn't started using
                // locations for this SKU yet).
                if (locationStockRows.TryGetValue(line.ItemId, out var locRow))
                {
                    var deduction = Math.Min(qty, locRow.Quantity);
                    if (deduction > 0) locRow.DecreaseStock(deduction);
                }

                pendingMovements.Add(new EgyptTax.Domain.MasterData.StockMovement(
                    itemId: item.Id,
                    occurredAtUtc: nowUtc,
                    quantity: -qty,
                    quantityOnHandAfter: item.QuantityOnHand,
                    kind: EgyptTax.Domain.MasterData.StockMovementKind.Sale,
                    sourceDocumentId: invoice.Id,
                    createdByUserId: command.PostedByUserId));
            }
            else if (qty < 0)
            {
                // Credit note line — quantity is negative, so the
                // absolute value is what we put back into stock.
                var put = -qty;
                item.IncreaseStock(put);

                // Mirror to per-location row. Create the row if it
                // doesn't exist yet (the return seeds the default
                // location with the returned units — sensible default).
                if (defaultLocationId is { } defLoc)
                {
                    if (locationStockRows.TryGetValue(line.ItemId, out var locRow))
                    {
                        locRow.IncreaseStock(put);
                    }
                    else
                    {
                        var fresh = new EgyptTax.Domain.MasterData.ItemStockByLocation(
                            itemId: line.ItemId, locationId: defLoc, initialQuantity: put);
                        _db.Add(fresh);
                        locationStockRows[line.ItemId] = fresh;
                    }
                }

                pendingMovements.Add(new EgyptTax.Domain.MasterData.StockMovement(
                    itemId: item.Id,
                    occurredAtUtc: nowUtc,
                    quantity: put,
                    quantityOnHandAfter: item.QuantityOnHand,
                    kind: EgyptTax.Domain.MasterData.StockMovementKind.Return,
                    sourceDocumentId: invoice.Id,
                    createdByUserId: command.PostedByUserId));
            }
        }

        var documentNumber = await _allocator.AllocateAsync(
            documentType,
            fiscalYear,
            cancellationToken
        );

        // Stamp the now-known document number on the pending stock
        // movement notes so the audit trail links back to the doc.
        foreach (var m in pendingMovements)
        {
            _db.Add(new EgyptTax.Domain.MasterData.StockMovement(
                itemId: m.ItemId,
                occurredAtUtc: m.OccurredAtUtc,
                quantity: m.Quantity,
                quantityOnHandAfter: m.QuantityOnHandAfter,
                kind: m.Kind,
                sourceDocumentId: m.SourceDocumentId,
                note: documentNumber,
                createdByUserId: m.CreatedByUserId));
        }

        var postingMode = approvalRequired
            ? DocumentPostingMode.ApprovedThenPosted
            : DocumentPostingMode.UnapprovedDirect;

        invoice.MarkPosted(
            documentNumber: documentNumber,
            postedByUserId: command.PostedByUserId,
            postedAtUtc: nowUtc,
            postingMode: postingMode,
            approvalEnabled: approvalRequired
        );

        // FR-035 — open the ETA submission row in Pending state with
        // the regulator-imposed 7-day window so the dashboard (T083
        // query) immediately surfaces it. Auto-submission to the mock
        // ETA endpoint is owned by a follow-up batch; the row exists
        // from post-time so the UI can show "Pending" before any
        // submission attempt fires.
        var etaSubmission = new EtaSubmission(
            salesInvoiceId: invoice.Id,
            postedAtUtc: nowUtc,
            nowUtc: nowUtc
        );
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

        // v5 D.1 v2 — for every line whose item has TracksSerials and
        // the operator pre-selected serial ids on the line form,
        // transition each ItemSerial row to Sold and back-point the
        // customer + invoice. Loaded eagerly so we don't issue a query
        // per serial; per-row MarkSold is a domain transition that
        // throws if the serial isn't InStock/Reserved (operator gets a
        // useful error rather than a silent stuck-state).
        var allSerialIds = invoice.Lines
            .SelectMany(l => l.GetSoldSerialIds())
            .Distinct()
            .ToList();
        if (allSerialIds.Count > 0)
        {
            var serials = await _db.Set<EgyptTax.Domain.MasterData.ItemSerial>()
                .Where(s => allSerialIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, cancellationToken);
            foreach (var line in invoice.Lines)
            {
                foreach (var serialId in line.GetSoldSerialIds())
                {
                    if (!serials.TryGetValue(serialId, out var serial)) continue;
                    serial.MarkSold(invoice.CustomerId, invoice.Id, nowUtc);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "sales_invoice.posted",
                ActorUserId: command.PostedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(invoice, postingMode)
            ),
            cancellationToken
        );

        // v4 B.3 — fan out the invoice.posted webhook event. Fire-
        // and-forget; the dispatcher swallows + logs failures so a
        // misconfigured receiver never blocks the post path.
        _webhooks?.Enqueue("invoice.posted", new
        {
            invoice_id = invoice.Id,
            customer_id = invoice.CustomerId,
            document_number = invoice.DocumentNumber,
            document_date = invoice.DocumentDate.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture),
            posted_at_utc = invoice.PostedAtUtc?.ToString(
                "o", CultureInfo.InvariantCulture),
            grand_total_egp = invoice.GrandTotal.Amount,
            is_credit_note = invoice.IsCreditNote,
        });

        return invoice;
    }

    private static string BuildPayloadJson(SalesInvoice invoice, DocumentPostingMode postingMode) =>
        $$"""{"invoice_id":"{{invoice.Id:D}}","customer_id":"{{invoice.CustomerId:D}}","document_number":"{{invoice.DocumentNumber}}","document_date":"{{invoice.DocumentDate:yyyy-MM-dd}}","posted_at_utc":"{{invoice.PostedAtUtc?.ToString("o", CultureInfo.InvariantCulture)}}","posting_mode":"{{postingMode}}","subtotal_egp":{{invoice.Subtotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"vat_total_egp":{{invoice.VatTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"grand_total_egp":{{invoice.GrandTotal.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"line_count":{{invoice.Lines.Count}}}""";
}
