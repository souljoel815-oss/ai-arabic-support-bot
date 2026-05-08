using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B8 — deductible expense category per FR-015. Drives the default
/// deductible flag on Expense documents; the operator can override
/// per-line, but the override is recorded in the audit log so an
/// inspector can trace any "marked deductible against type-default"
/// decisions later (FR-015 + FR-027).
///
/// The <see cref="DefaultAccountId"/> FK to ChartOfAccount is
/// declared here as a Guid value-type but the FK constraint itself
/// is deferred until US4's GL work seeds the chart of accounts —
/// for now expense categories carry the Guid forward and the
/// referential integrity is documented but not DB-enforced.
/// </summary>
public sealed class DeductibleExpenseCategory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = default!;
    public ArabicEnglishText Name { get; init; }
    public bool DefaultDeductible { get; private set; }
    public Guid DefaultAccountId { get; private set; }
    public DeductibleExpenseCategoryStatus Status { get; private set; } =
        DeductibleExpenseCategoryStatus.Active;

    private DeductibleExpenseCategory() { }

    public DeductibleExpenseCategory(
        string code,
        ArabicEnglishText name,
        bool defaultDeductible,
        Guid defaultAccountId
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Name = name;
        DefaultDeductible = defaultDeductible;
        DefaultAccountId = defaultAccountId;
    }

    public void UpdateDefaults(bool deductible, Guid accountId)
    {
        DefaultDeductible = deductible;
        DefaultAccountId = accountId;
    }

    public void Deactivate() => Status = DeductibleExpenseCategoryStatus.Inactive;

    public void Reactivate() => Status = DeductibleExpenseCategoryStatus.Active;
}

public enum DeductibleExpenseCategoryStatus
{
    Active,
    Inactive,
}
