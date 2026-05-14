using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Quotations;

/// <summary>
/// v5 A.4 — reusable quotation template. The agency that quotes
/// "logo design + 3 revisions + brand guide" 50 times a year saves
/// the line set once + applies it with one dropdown pick on
/// <c>/quotations/new</c>. The applied lines are COPIED into the
/// new quotation (no FK back to the template), so later edits to
/// the template don't retro-mutate quotations already created.
///
/// Lines on the template carry default quantities + unit prices,
/// but operators can edit per-line after applying — the template is
/// a starting point, not a contract.
/// </summary>
public sealed class QuotationTemplate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }

    private readonly List<QuotationTemplateLine> _lines = new();
    public IReadOnlyCollection<QuotationTemplateLine> Lines => _lines;

    private QuotationTemplate() { }

    public QuotationTemplate(
        string name,
        string? description,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void UpdateDescription(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    public QuotationTemplateLine AddLine(
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent)
    {
        var line = new QuotationTemplateLine(Id, itemId, quantity, unitPrice, vatCategoryId, vatRatePercent);
        _lines.Add(line);
        return line;
    }

    public void RemoveAllLines() => _lines.Clear();
}

public sealed class QuotationTemplateLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid QuotationTemplateId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public MoneyEgp UnitPrice { get; init; } = MoneyEgp.Zero;
    public Guid VatCategoryId { get; init; }
    public decimal VatRatePercent { get; init; }

    private QuotationTemplateLine() { }

    public QuotationTemplateLine(
        Guid quotationTemplateId,
        Guid itemId,
        decimal quantity,
        MoneyEgp unitPrice,
        Guid vatCategoryId,
        decimal vatRatePercent)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("ItemId required.", nameof(itemId));
        }
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }
        if (unitPrice.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }
        if (vatRatePercent < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRatePercent), "VAT rate cannot be negative.");
        }
        QuotationTemplateId = quotationTemplateId;
        ItemId = itemId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatCategoryId = vatCategoryId;
        VatRatePercent = vatRatePercent;
    }
}
