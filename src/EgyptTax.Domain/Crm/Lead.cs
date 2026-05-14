using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Crm;

/// <summary>
/// N.2 (v3 §11 roadmap) — sales-pipeline lead. The top-of-funnel
/// entity that lives BEFORE a Customer record exists. Captures
/// "I met someone at a conference / got a referral / found a tip
/// on Facebook — need to follow up" and tracks them through 5
/// fixed stages until they either convert (becomes a Customer
/// + Quotation) or die in Lost.
///
/// State machine:
///   New → Qualified → ProposalSent → Negotiation → Won
///                                                → Lost (from any stage)
///
/// On Won: operator clicks "Convert" which creates a Customer
/// record from the lead's contact info + opens a new Quotation
/// pre-filled with the customer. The lead's
/// <see cref="ConvertedToCustomerId"/> + <see cref="ConvertedAtUtc"/>
/// stamp the conversion for sales-rep commission tracking.
/// </summary>
public sealed class Lead
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public ArabicEnglishText Name { get; private set; } = new("", "");
    public string? CompanyName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Source { get; private set; }
    public LeadStage Stage { get; private set; } = LeadStage.New;

    /// <summary>Sales rep working this lead. Defaults to creator;
    /// can be re-assigned from the kanban.</summary>
    public Guid? AssignedToUserId { get; private set; }

    public DateOnly? ExpectedCloseDate { get; private set; }
    /// <summary>Operator's best-guess deal value in EGP.</summary>
    public decimal? ExpectedValueEgp { get; private set; }

    /// <summary>Free-text "what's the next thing I should do".</summary>
    public string? NextAction { get; private set; }
    public DateOnly? NextActionDate { get; private set; }

    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }

    public DateTime? WonAtUtc { get; private set; }
    public DateTime? LostAtUtc { get; private set; }
    /// <summary>Why the lead was Lost. Operator-supplied free text.</summary>
    public string? LostReason { get; private set; }

    public Guid? ConvertedToCustomerId { get; private set; }
    public DateTime? ConvertedAtUtc { get; private set; }

    private readonly List<LeadActivity> _activities = new();
    public IReadOnlyCollection<LeadActivity> Activities => _activities;

    private Lead() { }

    public static Lead Create(
        ArabicEnglishText name,
        string? companyName,
        string? phone,
        string? email,
        string? source,
        Guid? assignedToUserId,
        DateTime createdAtUtc,
        Guid? createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name.Arabic) && string.IsNullOrWhiteSpace(name.English))
            throw new ArgumentException("Lead name (Ar or En) is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Lead must have phone or email — at least one contact point.", nameof(phone));

        return new Lead
        {
            Name = name,
            CompanyName = companyName,
            Phone = phone,
            Email = email,
            Source = source,
            AssignedToUserId = assignedToUserId,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    public void UpdateContact(string? phone, string? email, string? companyName)
    {
        Phone = phone;
        Email = email;
        CompanyName = companyName;
    }

    public void Reassign(Guid? newAssigneeUserId) => AssignedToUserId = newAssigneeUserId;

    public void SetForecast(DateOnly? expectedClose, decimal? expectedValueEgp)
    {
        if (expectedValueEgp is < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedValueEgp));
        ExpectedCloseDate = expectedClose;
        ExpectedValueEgp = expectedValueEgp;
    }

    public void SetNextAction(string? action, DateOnly? actionDate)
    {
        NextAction = action;
        NextActionDate = actionDate;
    }

    /// <summary>Move forward in the pipeline. Idempotent — same stage = no-op.</summary>
    public void MoveToStage(LeadStage stage)
    {
        if (stage == Stage) return;
        if (Stage is LeadStage.Won or LeadStage.Lost)
            throw new InvalidOperationException(
                $"Lead {Id} is in terminal state {Stage}; no further stage changes.");

        Stage = stage;
        if (stage == LeadStage.Won) WonAtUtc = DateTime.UtcNow;
    }

    public void MarkLost(string? reason, DateTime nowUtc)
    {
        if (Stage == LeadStage.Won)
            throw new InvalidOperationException(
                $"Lead {Id} is already Won; cannot mark Lost.");
        Stage = LeadStage.Lost;
        LostAtUtc = nowUtc;
        LostReason = reason;
    }

    public void RecordConversion(Guid customerId, DateTime nowUtc)
    {
        if (Stage != LeadStage.Won)
            throw new InvalidOperationException(
                $"Lead {Id} is in stage {Stage}; convert only from Won.");
        if (ConvertedToCustomerId is not null)
            throw new InvalidOperationException(
                $"Lead {Id} was already converted to {ConvertedToCustomerId}.");
        ConvertedToCustomerId = customerId;
        ConvertedAtUtc = nowUtc;
    }

    public LeadActivity AddActivity(
        LeadActivityKind kind,
        string note,
        DateTime occurredAtUtc,
        Guid? loggedByUserId)
    {
        var activity = new LeadActivity(Id, kind, note, occurredAtUtc, loggedByUserId);
        _activities.Add(activity);
        return activity;
    }
}

public enum LeadStage
{
    New = 0,
    Qualified = 1,
    ProposalSent = 2,
    Negotiation = 3,
    Won = 4,
    Lost = 5,
}
