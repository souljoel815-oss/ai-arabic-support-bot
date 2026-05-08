using System.Globalization;
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
/// FR-031 — manual adjusting journal voucher handler.
///
/// Two invariants gate every successful create:
///   * Authorisation: the caller MUST hold a role whose code is in
///     <see cref="AllowedRoleCodes"/> (Administrator + Accountant —
///     Bookkeeper is the canonical rejected role per Round-6 F9).
///     Looking up by role code rather than a separate Permission row
///     is the MVP pragma; if the role-permission scaffolding ever
///     gets fleshed out (FR-003), this swap becomes a one-line
///     change.
///   * Balance: SUM(debits) == SUM(credits). Enforced inside
///     <see cref="JournalVoucher.CreateManual"/> so an unbalanced
///     caller never produces a voucher; the EF Save is unreachable
///     on that path.
/// </summary>
public sealed class CreateManualAdjustingJournalHandler
{
    /// <summary>
    /// FR-031 role codes permitted to create manual adjusting
    /// journals. Bookkeeper is intentionally excluded; that's the
    /// regression guarded by T167a.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedRoleCodes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Administrator",
        "Accountant",
    };

    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public CreateManualAdjustingJournalHandler(
        AppDbContext db,
        IClock clock,
        IAuditLogStore auditLog
    )
    {
        _db = db;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<JournalVoucher> HandleAsync(
        CreateManualAdjustingJournalCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines is null || command.Lines.Count == 0)
        {
            throw new InvalidOperationException(
                "CreateManualAdjustingJournalCommand requires at least one line; the JournalVoucher invariant requires at least two."
            );
        }

        // FR-031 role check. Loaded with .Include(u.Roles) so we can
        // inspect role codes WITHOUT trusting any caller-supplied
        // role list (the command only carries a user id; the truth
        // lives in the user_roles table).
        var user =
            await _db.Set<User>()
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == command.CreatedByUserId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"User {command.CreatedByUserId} not found; cannot create a manual adjusting journal."
            );

        var heldCodes = user.Roles.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!heldCodes.Overlaps(AllowedRoleCodes))
        {
            // The audit chain captures the rejection so an inspector
            // can see the attempt + the held roles (FR-031 violation
            // attempts are themselves auditable).
            await _auditLog.AppendAsync(
                new AuditLogPayload(
                    Kind: "journal_voucher.manual.permission_denied",
                    ActorUserId: command.CreatedByUserId,
                    ActorFirmName: null,
                    CompanyId: Guid.Empty,
                    PayloadJson: $$"""{"user_id":"{{command.CreatedByUserId:D}}","held_roles":[{{string.Join(",", heldCodes.Select(c => "\"" + c + "\""))}}],"required_any_of":[{{string.Join(",", AllowedRoleCodes.Select(c => "\"" + c + "\""))}}]}"""
                ),
                cancellationToken
            );

            throw new UnauthorizedAccessException(
                $"User {command.CreatedByUserId} cannot create a manual adjusting journal voucher: "
                    + $"FR-031 requires one of [{string.Join(", ", AllowedRoleCodes)}]; user holds [{string.Join(", ", heldCodes)}]."
            );
        }

        var nowUtc = _clock.UtcNow;
        var voucher = JournalVoucher.CreateManual(
            date: command.Date,
            narration: command.Narration,
            createdByUserId: command.CreatedByUserId,
            createdAtUtc: nowUtc,
            lines: command.Lines.Select(l => (l.AccountCode, l.Debit, l.Credit, l.Description))
        );

        _db.Add(voucher);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "journal_voucher.manual.created",
                ActorUserId: command.CreatedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(voucher)
            ),
            cancellationToken
        );

        return voucher;
    }

    private static string BuildPayloadJson(JournalVoucher v)
    {
        var inv = CultureInfo.InvariantCulture;
        var totalDebits = v.Lines.Sum(l => l.Debit.Amount);
        return $$"""{"voucher_id":"{{v.Id:D}}","date":"{{v.Date:yyyy-MM-dd}}","line_count":{{v.Lines.Count}},"total_debits_egp":{{totalDebits.ToString("F2", inv)}},"created_by_user_id":"{{v.CreatedByUserId:D}}"}""";
    }
}
