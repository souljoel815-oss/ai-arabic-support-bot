using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Inventory;

/// <summary>
/// v5 F.5 — a single cost component that lands on top of imported
/// goods (e.g. shipping = $5000 on clearing account 5300, customs =
/// $3000 on clearing account 5310). The clearing account is operator-
/// supplied per-line so the JE credit lands wherever the operator
/// pre-paid the cost from. At validation the JE emits one CR per
/// cost line and one DR per allocation.
/// </summary>
public sealed class LandedCostLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LandedCostId { get; init; }
    public string ClearingAccountCode { get; init; } = "";
    public MoneyEgp Amount { get; init; } = MoneyEgp.Zero;
    public string? Description { get; init; }

    private LandedCostLine() { }

    public LandedCostLine(Guid landedCostId, string clearingAccountCode, MoneyEgp amount, string? description)
    {
        LandedCostId = landedCostId;
        ClearingAccountCode = clearingAccountCode;
        Amount = amount;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
