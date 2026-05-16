namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v5 F.6 — multi-dimensional cost-center distribution. The legacy
/// single-tag <c>CostCenterId</c> on Expense / SalesInvoiceLine /
/// PurchaseInvoiceLine handles the 1-tag case. When a multi-branch
/// business needs "this expense is 30% Cairo / 70% Alexandria",
/// the operator opens the split-allocation editor on the source
/// line and writes N allocation rows that MUST sum to 10000 basis
/// points (= 100%).
///
/// Reports prefer allocations when present (any rows for the
/// source line), falling back to the legacy single tag for back-
/// compat with already-posted documents that were never split.
///
/// Percentage stored as basis points (1bp = 0.01%) so 100% = 10000;
/// integer arithmetic avoids decimal-rounding drift on the sum.
/// </summary>
public sealed class CostCenterAllocation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public CostCenterAllocationSourceType SourceType { get; init; }
    public Guid SourceLineId { get; init; }
    public Guid CostCenterId { get; init; }
    public int PercentBp { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    private CostCenterAllocation() { }

    public CostCenterAllocation(
        CostCenterAllocationSourceType sourceType,
        Guid sourceLineId,
        Guid costCenterId,
        int percentBp)
    {
        if (sourceLineId == Guid.Empty)
        {
            throw new ArgumentException("SourceLineId is required.", nameof(sourceLineId));
        }
        if (costCenterId == Guid.Empty)
        {
            throw new ArgumentException("CostCenterId is required.", nameof(costCenterId));
        }
        if (percentBp <= 0 || percentBp > 10000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentBp),
                $"PercentBp must be in (0, 10000]. Got {percentBp}.");
        }
        SourceType = sourceType;
        SourceLineId = sourceLineId;
        CostCenterId = costCenterId;
        PercentBp = percentBp;
    }
}

public enum CostCenterAllocationSourceType
{
    Expense,
    SalesInvoiceLine,
    PurchaseInvoiceLine,
}
