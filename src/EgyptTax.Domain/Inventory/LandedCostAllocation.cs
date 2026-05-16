using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Inventory;

/// <summary>
/// v5 F.5 — one row of computed allocation: a chunk of the
/// landed-cost total assigned to a specific posted-purchase
/// invoice line. After validation, <see cref="WeightedAvgCostQuery"/>
/// folds these into the per-item weighted average so subsequent
/// sales price against the true landed cost.
/// </summary>
public sealed class LandedCostAllocation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LandedCostId { get; init; }
    public Guid PurchaseInvoiceLineId { get; init; }
    public MoneyEgp AllocatedAmount { get; init; } = MoneyEgp.Zero;

    private LandedCostAllocation() { }

    public LandedCostAllocation(Guid landedCostId, Guid purchaseInvoiceLineId, MoneyEgp allocatedAmount)
    {
        LandedCostId = landedCostId;
        PurchaseInvoiceLineId = purchaseInvoiceLineId;
        AllocatedAmount = allocatedAmount;
    }
}
