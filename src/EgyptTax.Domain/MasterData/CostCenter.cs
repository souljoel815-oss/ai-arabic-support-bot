using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// v3 §11 #3 (cost centers) — analytical-accounting tag attached
/// to expense documents (and later: sales/purchase invoices) so
/// project-costed businesses can see P&L per project / department
/// / branch.
///
/// v1 ships flat (no parent/hierarchy). Construction firms don't
/// usually need nested cost centers; "Project A / Project B / ..."
/// works for the first wave of customers. Hierarchy lands when a
/// real customer asks.
///
/// Code is the operator-readable identifier (e.g. "PRJ-001",
/// "DEPT-FIN", "BR-CAIRO"). Unique per install.
/// </summary>
public sealed class CostCenter
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; private set; }
    public CostCenterStatus Status { get; private set; } = CostCenterStatus.Active;

    private CostCenter() { }

    public CostCenter(string code, ArabicEnglishText name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code.Trim();
        Name = name;
    }

    public void Rename(ArabicEnglishText name) => Name = name;
    public void Deactivate() => Status = CostCenterStatus.Inactive;
    public void Reactivate() => Status = CostCenterStatus.Active;
}

public enum CostCenterStatus
{
    Active,
    Inactive,
}
