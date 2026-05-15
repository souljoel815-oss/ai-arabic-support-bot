using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Accounting;

/// <summary>
/// v5 E.10 — reusable journal-entry template for the month-end
/// accruals an accountant types every period (rent, utilities,
/// salaries, depreciation pre-Hangfire). Operator builds the line
/// set once, then clicks "Generate from template" each period to
/// post a fresh JournalVoucher with current-period date stamped
/// onto the same lines.
///
/// Lines on the template are COPIED into the resulting JV (no FK
/// back), so later edits to the template don't retro-mutate JVs
/// already posted.
///
/// Auto-reverse flag is captured at the template level — a future
/// Hangfire job can pick up templates with <c>AutoReverse = true</c>
/// and schedule the reversal JV for the first day of the next
/// period. v1 does NOT auto-fire generation: every JV in the system
/// requires interactive confirmation per FR-027.
/// </summary>
public sealed class JournalTemplate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public JournalTemplateSchedule Schedule { get; private set; } = JournalTemplateSchedule.Monthly;
    public bool AutoReverse { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }

    private readonly List<JournalTemplateLine> _lines = new();
    public IReadOnlyCollection<JournalTemplateLine> Lines => _lines;

    private JournalTemplate() { }

    public JournalTemplate(
        string name,
        string? description,
        JournalTemplateSchedule schedule,
        bool autoReverse,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Schedule = schedule;
        AutoReverse = autoReverse;
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public void Update(string name, string? description, JournalTemplateSchedule schedule, bool autoReverse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Schedule = schedule;
        AutoReverse = autoReverse;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    public JournalTemplateLine AddLine(
        string accountCode,
        MoneyEgp debit,
        MoneyEgp credit,
        string description)
    {
        var line = new JournalTemplateLine(Id, accountCode, debit, credit, description);
        _lines.Add(line);
        return line;
    }

    public void RemoveAllLines() => _lines.Clear();

    /// <summary>
    /// Validate the template invariants used by both editor save
    /// and the generator. ≥ 2 lines + balanced + every line is a
    /// debit XOR credit. Throws if violated.
    /// </summary>
    public void EnsureValid()
    {
        if (_lines.Count < 2)
        {
            throw new InvalidOperationException(
                $"Journal template '{Name}' must have at least 2 lines.");
        }
        var debit = _lines.Sum(l => l.Debit.Amount);
        var credit = _lines.Sum(l => l.Credit.Amount);
        if (debit != credit)
        {
            throw new InvalidOperationException(
                $"Journal template '{Name}' is unbalanced: debit {debit:N2} ≠ credit {credit:N2}.");
        }
    }
}

public enum JournalTemplateSchedule
{
    /// <summary>Operator generates once a month (most common — rent, utilities).</summary>
    Monthly,
    /// <summary>Operator generates once a quarter.</summary>
    Quarterly,
    /// <summary>No fixed cadence — fire whenever (one-off recurring use).</summary>
    Adhoc,
}

public sealed class JournalTemplateLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JournalTemplateId { get; init; }
    public string AccountCode { get; private set; } = "";
    public MoneyEgp Debit { get; private set; } = MoneyEgp.Zero;
    public MoneyEgp Credit { get; private set; } = MoneyEgp.Zero;
    public string Description { get; private set; } = "";

    private JournalTemplateLine() { }

    public JournalTemplateLine(
        Guid templateId,
        string accountCode,
        MoneyEgp debit,
        MoneyEgp credit,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountCode);
        if (debit.Amount < 0m || credit.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(debit),
                "Debit and credit must be non-negative.");
        }
        if (debit.Amount > 0m && credit.Amount > 0m)
        {
            throw new ArgumentException(
                $"Line on template {templateId} debits AND credits {accountCode} — pick one side.");
        }
        if (debit.Amount == 0m && credit.Amount == 0m)
        {
            throw new ArgumentException(
                $"Line on template {templateId} has both sides zero.");
        }
        JournalTemplateId = templateId;
        AccountCode = accountCode.Trim();
        Debit = debit;
        Credit = credit;
        Description = description ?? "";
    }
}
