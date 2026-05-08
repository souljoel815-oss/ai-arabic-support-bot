using System.Globalization;
using EgyptTax.Application.Accounting;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Expenses;
using EgyptTax.Application.Numbering;
using EgyptTax.Application.Periods;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Expenses;

/// <summary>
/// US2 / FR-016 / FR-026 — expense posting handler. Mirrors
/// PostPurchaseInvoiceHandler: allocates an `EXP-{year}-{n}` series
/// number, transitions via <see cref="Expense.MarkPosted"/>,
/// emits FR-028 audit. FR-016 enforced HERE: a deductible expense
/// MUST have at least one Attachment row.
/// </summary>
public sealed class PostExpenseHandler
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberAllocator _allocator;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;
    private readonly IExpenseJournalEmitter? _journalEmitter;
    private readonly ITaxPeriodLockGuard? _periodLockGuard;

    public PostExpenseHandler(
        AppDbContext db,
        IDocumentNumberAllocator allocator,
        IClock clock,
        IAuditLogStore auditLog,
        IExpenseJournalEmitter? journalEmitter = null,
        ITaxPeriodLockGuard? periodLockGuard = null
    )
    {
        _db = db;
        _allocator = allocator;
        _clock = clock;
        _auditLog = auditLog;
        _journalEmitter = journalEmitter;
        _periodLockGuard = periodLockGuard;
    }

    public async Task<Expense> HandleAsync(
        PostExpenseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var expense =
            await _db.Set<Expense>()
                .FirstOrDefaultAsync(e => e.Id == command.ExpenseId, cancellationToken)
            ?? throw new InvalidOperationException($"Expense {command.ExpenseId} not found.");

        // FR-037 — reject backdated posts into a Locked VAT period.
        if (_periodLockGuard is not null)
        {
            var lockCheck = await _periodLockGuard.CheckVatMonthAsync(
                expense.DocumentDate,
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
                        PayloadJson: $$"""{"expense_id":"{{expense.Id:D}}","document_type":"Expense","document_date":"{{expense.DocumentDate:yyyy-MM-dd}}","period_year":{{lockCheck.Year}},"period_month":{{lockCheck.MonthOrQuarter}}}"""
                    ),
                    cancellationToken
                );
                throw new InvalidOperationException(
                    $"Cannot post expense {expense.Id}: document date {expense.DocumentDate:yyyy-MM-dd} falls inside Locked VAT period {lockCheck.Year}-{lockCheck.MonthOrQuarter:D2} (FR-037). An Administrator must reopen the period before backdated posts are allowed."
                );
            }
        }

        // FR-016 — deductible expenses require at least one attachment.
        if (expense.DeductibleFlag)
        {
            var attachmentCount = await _db.Set<Attachment>()
                .CountAsync(
                    a => a.DocumentId == expense.Id && a.DocumentType == DocumentType.Expense,
                    cancellationToken
                );
            if (attachmentCount == 0)
            {
                throw new InvalidOperationException(
                    $"Cannot post expense {expense.Id}: marked deductible but no attachment is on file. Per FR-016, deductible expenses require supporting documents."
                );
            }
        }

        var approvalSetting = await _db.Set<DocumentTypeApprovalSetting>()
            .FirstOrDefaultAsync(s => s.DocumentType == DocumentType.Expense, cancellationToken);
        var approvalRequired = approvalSetting?.ApprovalRequired ?? true;

        // T159 / FR-026 — see the matching guard in
        // PostSalesInvoiceHandler. Refuse Draft → Posted when this
        // type requires approval, BEFORE the allocator runs.
        if (approvalRequired && expense.State == DocumentState.Draft)
        {
            throw new InvalidOperationException(
                $"Cannot post Expense {expense.Id}: this document type requires approval (FR-026). "
                    + "Submit the document for approval, then have an Approver approve it before posting."
            );
        }

        var fiscalYear = expense.DocumentDate.Year;
        var documentNumber = await _allocator.AllocateAsync(
            DocumentType.Expense,
            fiscalYear,
            cancellationToken
        );

        var postingMode = approvalRequired
            ? DocumentPostingMode.ApprovedThenPosted
            : DocumentPostingMode.UnapprovedDirect;

        var nowUtc = _clock.UtcNow;
        expense.MarkPosted(
            documentNumber: documentNumber,
            postedByUserId: command.PostedByUserId,
            postedAtUtc: nowUtc,
            postingMode: postingMode,
            approvalEnabled: approvalRequired
        );

        // US4 / FR-014 — emit the 2-line balanced expense journal
        // (DR Expense / CR AP) IN THE SAME SaveChangesAsync. Optional
        // for legacy callers, like the sales + purchase emitters.
        if (_journalEmitter is not null)
        {
            await _journalEmitter.EmitForExpenseAsync(expense, nowUtc, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "expense.posted",
                ActorUserId: command.PostedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(expense, postingMode)
            ),
            cancellationToken
        );

        return expense;
    }

    private static string BuildPayloadJson(Expense expense, DocumentPostingMode postingMode) =>
        $$"""{"expense_id":"{{expense.Id:D}}","category_id":"{{expense.CategoryId:D}}","document_number":"{{expense.DocumentNumber}}","document_date":"{{expense.DocumentDate:yyyy-MM-dd}}","posted_at_utc":"{{expense.PostedAtUtc?.ToString("o", CultureInfo.InvariantCulture)}}","posting_mode":"{{postingMode}}","amount_egp":{{expense.Amount.Amount.ToString("F2", CultureInfo.InvariantCulture)}},"deductible":{{(expense.DeductibleFlag ? "true" : "false")}}}""";
}
