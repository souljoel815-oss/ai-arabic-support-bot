using System.Globalization;
using EgyptTax.Application.Audit;
using EgyptTax.Application.Inventory;
using EgyptTax.Application.Numbering;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Inventory;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Inventory;

/// <summary>
/// v5 F.5 — landed-cost post handler. Allocates a
/// <c>LC-{year}-{n}</c> document number, transitions the aggregate
/// from Draft to Validated, and emits the balanced JE inside the
/// same <c>SaveChangesAsync</c>:
///
///   DR Inventory (1300)         per-allocation amounts (one DR per allocation)
///   CR Clearing account (op-supplied)  per-cost-line amounts (one CR per cost line)
///
/// The two sides MUST balance because <see cref="LandedCost.Validate"/>
/// already asserted sum(allocations) == sum(cost-lines).
/// </summary>
public sealed class ValidateLandedCostHandler
{
    private readonly AppDbContext _db;
    private readonly IDocumentNumberAllocator _allocator;
    private readonly IClock _clock;
    private readonly IAuditLogStore _auditLog;

    public ValidateLandedCostHandler(
        AppDbContext db,
        IDocumentNumberAllocator allocator,
        IClock clock,
        IAuditLogStore auditLog)
    {
        _db = db;
        _allocator = allocator;
        _clock = clock;
        _auditLog = auditLog;
    }

    public async Task<LandedCost> HandleAsync(
        ValidateLandedCostCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var lc = await _db.Set<LandedCost>()
            .Include(x => x.Lines)
            .Include(x => x.Allocations)
            .FirstOrDefaultAsync(x => x.Id == command.LandedCostId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"LandedCost {command.LandedCostId} not found.");

        var fiscalYear = lc.DocumentDate.Year;
        var documentNumber = await _allocator.AllocateAsync(
            DocumentType.LandedCost, fiscalYear, cancellationToken);
        var nowUtc = _clock.UtcNow;

        lc.Validate(documentNumber, command.ValidatedByUserId, nowUtc);

        // Emit the JE: one DR per allocation, one CR per cost line.
        var jeLines = new List<(string AccountCode, MoneyEgp Debit, MoneyEgp Credit, string Description)>(
            lc.Allocations.Count + lc.Lines.Count);
        foreach (var alloc in lc.Allocations)
        {
            if (alloc.AllocatedAmount.Amount <= 0m) continue;
            jeLines.Add((
                ChartOfAccountCodes.Inventory,
                alloc.AllocatedAmount,
                MoneyEgp.Zero,
                $"Landed cost {documentNumber} — allocation to PI line {alloc.PurchaseInvoiceLineId:N}"));
        }
        foreach (var costLine in lc.Lines)
        {
            jeLines.Add((
                costLine.ClearingAccountCode,
                MoneyEgp.Zero,
                costLine.Amount,
                $"Landed cost {documentNumber} — clearing {costLine.ClearingAccountCode}"
                    + (costLine.Description is null ? "" : $" ({costLine.Description})")));
        }

        var entry = JournalEntry.Create(
            sourceDocumentId: lc.Id,
            sourceDocumentNumber: documentNumber,
            sourceDocumentType: DocumentType.LandedCost,
            postedAtUtc: nowUtc,
            lines: jeLines);
        _db.Add(entry);

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.AppendAsync(
            new AuditLogPayload(
                Kind: "landed_cost.validated",
                ActorUserId: command.ValidatedByUserId,
                ActorFirmName: null,
                CompanyId: Guid.Empty,
                PayloadJson: BuildPayloadJson(lc)),
            cancellationToken);

        return lc;
    }

    private static string BuildPayloadJson(LandedCost lc)
    {
        var inv = CultureInfo.InvariantCulture;
        return $$"""{"landed_cost_id":"{{lc.Id:D}}","document_number":"{{lc.DocumentNumber}}","document_date":"{{lc.DocumentDate:yyyy-MM-dd}}","split_method":"{{lc.SplitMethod}}","cost_total_egp":{{lc.TotalCost().Amount.ToString("F2", inv)}},"allocation_count":{{lc.Allocations.Count}},"line_count":{{lc.Lines.Count}}}""";
    }
}
