namespace EgyptTax.Domain.Crm;

/// <summary>
/// N.2 — append-only log entry on a Lead. Operator-supplied note
/// + a kind classifier so the My Dashboard "next action" widget
/// can show "5 calls due today / 2 meetings logged this week".
/// </summary>
public sealed class LeadActivity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LeadId { get; init; }
    public LeadActivityKind Kind { get; init; }
    public string Note { get; init; } = "";
    public DateTime OccurredAtUtc { get; init; }
    public Guid? LoggedByUserId { get; init; }

    private LeadActivity() { }

    public LeadActivity(
        Guid leadId,
        LeadActivityKind kind,
        string note,
        DateTime occurredAtUtc,
        Guid? loggedByUserId)
    {
        if (leadId == Guid.Empty) throw new ArgumentException("LeadId required.", nameof(leadId));
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        LeadId = leadId;
        Kind = kind;
        Note = note.Length > 1000 ? note[..1000] : note;
        OccurredAtUtc = occurredAtUtc;
        LoggedByUserId = loggedByUserId;
    }
}

public enum LeadActivityKind
{
    Note = 0,
    Call = 1,
    Meeting = 2,
    Email = 3,
    WhatsApp = 4,
    StageChange = 5,
}
