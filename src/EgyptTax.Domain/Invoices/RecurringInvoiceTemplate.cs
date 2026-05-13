using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Invoices;

/// <summary>
/// L3 (v3 roadmap) — recurring invoice template. Subscription /
/// service businesses (gyms, ISPs, monthly retainer accountants,
/// maintenance contracts) generate the same invoice every period.
/// Without this they copy-paste through QuickBooks every month —
/// lost vertical. With it: define once, the daily Hangfire job
/// generates Draft invoices on the next-run date.
///
/// Always generates as Draft — operator reviews before posting.
/// No prorated billing, no mid-cycle changes, no dunning. Those
/// belong in v4 if customers actually ask.
/// </summary>
public sealed class RecurringInvoiceTemplate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public string Name { get; private set; } = "";
    public RecurringInterval Interval { get; private set; }
    public DateOnly NextRunDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Note { get; private set; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateOnly? LastGeneratedDate { get; private set; }
    public int InvoicesGeneratedCount { get; private set; }

    private readonly List<RecurringInvoiceTemplateLine> _lines = new();
    public IReadOnlyCollection<RecurringInvoiceTemplateLine> Lines => _lines;

    private RecurringInvoiceTemplate() { }

    public RecurringInvoiceTemplate(
        Guid customerId,
        string name,
        RecurringInterval interval,
        DateOnly nextRunDate,
        DateOnly? endDate = null,
        string? note = null,
        Guid? createdByUserId = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (endDate is { } e && e < nextRunDate)
            throw new ArgumentException("EndDate cannot be before NextRunDate.", nameof(endDate));
        CustomerId = customerId;
        Name = name.Trim();
        Interval = interval;
        NextRunDate = nextRunDate;
        EndDate = endDate;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CreatedByUserId = createdByUserId;
    }

    public void AddLine(Guid itemId, decimal quantity, MoneyEgp unitPrice, Guid vatCategoryId, decimal vatRatePercent)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("ItemId required.", nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        _lines.Add(new RecurringInvoiceTemplateLine(itemId, quantity, unitPrice, vatCategoryId, vatRatePercent));
    }

    public void RemoveLine(Guid lineId) => _lines.RemoveAll(l => l.Id == lineId);

    public void Pause() => IsActive = false;
    public void Resume() => IsActive = true;

    /// <summary>
    /// Called by the Hangfire job after it successfully generates an
    /// invoice from this template. Advances NextRunDate by one
    /// interval; bumps LastGeneratedDate + the counter.
    /// </summary>
    public void RecordGeneration(DateOnly generationDate)
    {
        LastGeneratedDate = generationDate;
        InvoicesGeneratedCount++;
        NextRunDate = Interval switch
        {
            RecurringInterval.Weekly    => NextRunDate.AddDays(7),
            RecurringInterval.Monthly   => NextRunDate.AddMonths(1),
            RecurringInterval.Quarterly => NextRunDate.AddMonths(3),
            RecurringInterval.Yearly    => NextRunDate.AddYears(1),
            _                            => NextRunDate.AddMonths(1),
        };
        // Auto-pause once we pass the optional end date.
        if (EndDate is { } e && NextRunDate > e)
        {
            IsActive = false;
        }
    }

    public void UpdateNote(string? note) => Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}

public sealed class RecurringInvoiceTemplateLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RecurringInvoiceTemplateId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public MoneyEgp UnitPrice { get; init; }
    public Guid VatCategoryId { get; init; }
    public decimal VatRatePercent { get; init; }

    private RecurringInvoiceTemplateLine() { UnitPrice = MoneyEgp.Zero; }

    public RecurringInvoiceTemplateLine(Guid itemId, decimal quantity, MoneyEgp unitPrice, Guid vatCategoryId, decimal vatRatePercent)
    {
        ItemId = itemId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatCategoryId = vatCategoryId;
        VatRatePercent = vatRatePercent;
    }
}

public enum RecurringInterval
{
    Weekly,
    Monthly,
    Quarterly,
    Yearly,
}
