using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Inventory;

/// <summary>
/// v5 F.5 — landed-cost aggregate. An importer brings in a
/// container; the supplier bill (PurchaseInvoice) carries the FOB
/// goods cost. On top of that the importer pays shipping, customs,
/// insurance, clearance fees — each of which MUST roll into the
/// inventory cost basis or margins look fake on subsequent sales.
///
/// A LandedCost groups N <c>LandedCostLine</c> rows (per cost
/// type, with its own clearing account on the credit side) and
/// allocates the total across N selected purchase-invoice lines
/// via an operator-chosen split method (<see cref="LandedCostSplitMethod"/>).
///
/// Lifecycle: Draft → Validated. Validation:
///   1. Asserts allocations sum equals cost-line total.
///   2. Marks the aggregate Validated (terminal — no edits after).
///   3. The post handler then emits a JE:
///      DR Inventory (per-allocation amounts)
///      CR each clearing account (per-cost-line amounts)
///   4. WeightedAvgCostQuery folds the allocations into the
///      per-item average so subsequent sales price against the
///      true landed cost.
///
/// v1 scope: <see cref="LandedCostSplitMethod.Equal"/>,
/// <see cref="LandedCostSplitMethod.ByQty"/>, and
/// <see cref="LandedCostSplitMethod.ByCost"/>. ByWeight / ByVolume
/// require physical attributes on Item we don't carry yet — defer
/// to v2 once a customer asks.
/// </summary>
public sealed class LandedCost
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateOnly DocumentDate { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? Note { get; private set; }
    public LandedCostState State { get; private set; } = LandedCostState.Draft;
    public LandedCostSplitMethod SplitMethod { get; private set; } = LandedCostSplitMethod.ByCost;
    public DateTime? ValidatedAtUtc { get; private set; }
    public Guid? ValidatedByUserId { get; private set; }

    private readonly List<LandedCostLine> _lines = new();
    public IReadOnlyCollection<LandedCostLine> Lines => _lines;

    private readonly List<LandedCostAllocation> _allocations = new();
    public IReadOnlyCollection<LandedCostAllocation> Allocations => _allocations;

    private LandedCost() { }

    public static LandedCost CreateDraft(
        DateOnly documentDate,
        LandedCostSplitMethod splitMethod,
        string? note = null)
    {
        return new LandedCost
        {
            DocumentDate = documentDate,
            SplitMethod = splitMethod,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
    }

    public LandedCostLine AddCostLine(string clearingAccountCode, MoneyEgp amount, string? description = null)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(clearingAccountCode);
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Cost-line amount must be positive.");
        }
        var line = new LandedCostLine(Id, clearingAccountCode.Trim(), amount, description);
        _lines.Add(line);
        return line;
    }

    public void RemoveCostLine(Guid lineId)
    {
        EnsureDraft();
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException($"Cost line {lineId} not on landed-cost {Id}.");
        _lines.Remove(line);
    }

    public void ChangeSplitMethod(LandedCostSplitMethod method)
    {
        EnsureDraft();
        SplitMethod = method;
    }

    public void UpdateNote(string? note)
    {
        EnsureDraft();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Replace all allocations with a freshly computed set.
    /// Caller computes the split (needs DB access for qty / cost lookups);
    /// the aggregate just verifies the sum and stores the rows.</summary>
    public void SetAllocations(IEnumerable<(Guid PurchaseInvoiceLineId, MoneyEgp Amount)> allocations)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(allocations);
        _allocations.Clear();
        foreach (var (lineId, amount) in allocations)
        {
            if (lineId == Guid.Empty)
            {
                throw new ArgumentException("Allocation target purchase-invoice line id is required.");
            }
            if (amount.Amount < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(allocations),
                    "Allocation amount cannot be negative.");
            }
            _allocations.Add(new LandedCostAllocation(Id, lineId, amount));
        }
    }

    public MoneyEgp TotalCost() =>
        MoneyEgp.From(_lines.Sum(l => l.Amount.Amount));

    public MoneyEgp TotalAllocated() =>
        MoneyEgp.From(_allocations.Sum(a => a.AllocatedAmount.Amount));

    public void Validate(string documentNumber, Guid validatedByUserId, DateTime validatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        EnsureDraft();
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot validate landed-cost {Id}: at least one cost line is required.");
        }
        if (_allocations.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot validate landed-cost {Id}: no allocations computed. Pick target purchase lines and Compute.");
        }
        var totalCost = TotalCost().Amount;
        var totalAllocated = TotalAllocated().Amount;
        // Allow 1 piaster tolerance per allocation row to absorb rounding.
        var tolerance = 0.01m * Math.Max(_allocations.Count, 1);
        if (Math.Abs(totalAllocated - totalCost) > tolerance)
        {
            throw new InvalidOperationException(
                $"Cannot validate landed-cost {Id}: allocations total {totalAllocated:F2} does not match cost-line total {totalCost:F2} (tolerance {tolerance:F2}).");
        }
        DocumentNumber = documentNumber;
        ValidatedAtUtc = validatedAtUtc;
        ValidatedByUserId = validatedByUserId;
        State = LandedCostState.Validated;
    }

    private void EnsureDraft()
    {
        if (State != LandedCostState.Draft)
        {
            throw new InvalidOperationException(
                $"Landed-cost {Id} is in state {State}; only Draft is editable.");
        }
    }
}

public enum LandedCostState
{
    Draft,
    Validated,
}

/// <summary>v5 F.5 / F.5 v2 — split methods for distributing
/// landed-cost total across selected purchase-invoice lines.
/// Physical-attribute methods (ByWeight, ByVolume) skip lines whose
/// item has the relevant attribute null.</summary>
public enum LandedCostSplitMethod
{
    /// <summary>Split the cost equally across each selected line.</summary>
    Equal,
    /// <summary>Split proportional to each line's Quantity.</summary>
    ByQty,
    /// <summary>Split proportional to each line's (Quantity × UnitPrice) — the
    /// "by-cost" allocation that customs accountants prefer because it
    /// keeps the relative cost percentages stable.</summary>
    ByCost,
    /// <summary>v5 F.5 v2 — split proportional to (Quantity × Item.WeightKg).
    /// Lines whose item has WeightKg = null get zero weight (skipped from
    /// allocation). Right call when the bill of lading priced per-kg.</summary>
    ByWeight,
    /// <summary>v5 F.5 v2 — split proportional to (Quantity × Item.VolumeM3).
    /// Lines whose item has VolumeM3 = null get zero weight. Right call
    /// when the freight contract priced per-cubic-meter.</summary>
    ByVolume,
}
