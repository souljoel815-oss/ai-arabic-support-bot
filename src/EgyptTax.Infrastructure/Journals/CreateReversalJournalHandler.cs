using EgyptTax.Application.Audit;
using EgyptTax.Application.Journals;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Journals;

/// <summary>
/// FR-012 / FR-031 — reversal-voucher handler. The aggregate-level
/// flip-and-balance happens inside
/// <see cref="JournalVoucher.CreateReversal"/>; this handler owns
/// the DB-side checks the aggregate cannot perform alone:
/// (1) original exists; (2) original has not already been reversed.
/// Reuses the same <see cref="CreateManualAdjustingJournalHandler.AllowedRoleCodes"/>
/// FR-031 role set (Administrator + Accountant) — a Bookkeeper
/// cannot author either flavour of GL adjustment.
/// </summary>
public sealed class CreateReversalJournalHandler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public CreateReversalJournalHandler(
        AppDbContext db, IClock clock, IAuditLogStore auditLog)
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<JournalVoucher> HandleAsync(
        CreateReversalJournalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // FR-031 role gate (mirrors CreateManualAdjustingJournalHandler).
        var user = await _db.Set<User>()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == command.CreatedByUserId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"User {command.CreatedByUserId} not found; cannot create a reversal journal voucher.");

        var heldCodes = user.Roles.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!heldCodes.Overlaps(CreateManualAdjustingJournalHandler.AllowedRoleCodes))
        {
            await _auditLog.AppendAsync(new AuditLogPayload(
                Kind: "journal_voucher.reversal.permission_denied",
                ActorUserId: command.CreatedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: $$"""{"original_voucher_id":"{{command.OriginalJournalVoucherId:D}}","user_id":"{{command.CreatedByUserId:D}}","held_roles":[{{string.Join(",", heldCodes.Select(c => "\"" + c + "\""))}}],"required_any_of":[{{string.Join(",", CreateManualAdjustingJournalHandler.AllowedRoleCodes.Select(c => "\"" + c + "\""))}}]}"""),
                cancellationToken);

            throw new UnauthorizedAccessException(
                $"User {command.CreatedByUserId} cannot create a reversal journal voucher: " +
                $"FR-031 requires one of [{string.Join(", ", CreateManualAdjustingJournalHandler.AllowedRoleCodes)}]; user holds [{string.Join(", ", heldCodes)}].");
        }

        // Load original (with lines — the reversal copies them).
        var original = await _db.Set<JournalVoucher>()
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.Id == command.OriginalJournalVoucherId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Original JournalVoucher {command.OriginalJournalVoucherId} not found; cannot reverse a non-existent voucher.");

        // FR-012 single-reversal invariant: an original may be reversed
        // exactly once. Two reversals would produce two compensating
        // entries against the same source — accounting chaos. Checked
        // here because the aggregate factory has no DB visibility.
        var alreadyReversed = await _db.Set<JournalVoucher>().AsNoTracking()
            .AnyAsync(v => v.ReversesJournalVoucherId == original.Id, cancellationToken);
        if (alreadyReversed)
        {
            throw new InvalidOperationException(
                $"Cannot reverse JournalVoucher {original.Id}: a reversal has already been posted against it. " +
                "If a further correction is needed, post a new manual adjusting voucher with the desired effect.");
        }

        var nowUtc = _clock.UtcNow;
        var reversal = JournalVoucher.CreateReversal(
            original: original,
            reversalDate: command.ReversalDate,
            reversalNarration: command.ReversalNarration,
            createdByUserId: command.CreatedByUserId,
            createdAtUtc: nowUtc);

        _db.Add(reversal);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(new AuditLogPayload(
            Kind: "journal_voucher.reversal.created",
            ActorUserId: command.CreatedByUserId,
            ActorFirmName: null,
            CompanyId: Guid.Empty,
            PayloadJson: $$"""{"reversal_voucher_id":"{{reversal.Id:D}}","reverses_journal_voucher_id":"{{original.Id:D}}","date":"{{reversal.Date:yyyy-MM-dd}}","line_count":{{reversal.Lines.Count}},"created_by_user_id":"{{reversal.CreatedByUserId:D}}"}"""),
            cancellationToken);

        return reversal;
    }
}
