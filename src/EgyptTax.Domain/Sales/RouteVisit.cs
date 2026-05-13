namespace EgyptTax.Domain.Sales;

/// <summary>
/// Phase H — a planned visit by a sales rep to a customer on a given
/// day. Reps build their daily route from the Routes page (or as a
/// quick action from a customer statement) and tick visits off as
/// they happen. Used for: organizing the rep's day, capturing
/// notes from the field, and later for managers to see coverage.
///
/// Independent of any tax / accounting flow — this is purely a
/// commercial / CRM-lite concept. The Customer link is the only
/// FK; everything else is on this row.
/// </summary>
public sealed class RouteVisit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RepUserId { get; init; }
    public DateOnly VisitDate { get; private set; }
    public Guid CustomerId { get; init; }

    /// <summary>
    /// Order within the day (1, 2, 3 …). Used to render the route in
    /// the rep's intended sequence. Reps can re-order from the page.
    /// </summary>
    public int Sequence { get; private set; }

    public RouteVisitKind Kind { get; private set; }
    public RouteVisitStatus Status { get; private set; } = RouteVisitStatus.Planned;
    public string? PlannedNote { get; private set; }
    public string? VisitNote { get; private set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }

    private RouteVisit() { }

    public RouteVisit(
        Guid repUserId,
        DateOnly visitDate,
        Guid customerId,
        int sequence,
        RouteVisitKind kind,
        string? plannedNote = null)
    {
        if (repUserId == Guid.Empty) throw new ArgumentException("RepUserId required.", nameof(repUserId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId required.", nameof(customerId));
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        RepUserId = repUserId;
        VisitDate = visitDate;
        CustomerId = customerId;
        Sequence = sequence;
        Kind = kind;
        PlannedNote = string.IsNullOrWhiteSpace(plannedNote) ? null : plannedNote.Trim();
    }

    public void Reschedule(DateOnly newDate, int newSequence)
    {
        if (Status != RouteVisitStatus.Planned)
            throw new InvalidOperationException("Only Planned visits can be rescheduled.");
        ArgumentOutOfRangeException.ThrowIfLessThan(newSequence, 1);
        VisitDate = newDate;
        Sequence = newSequence;
    }

    public void MarkVisited(string? note, DateTime nowUtc)
    {
        if (Status != RouteVisitStatus.Planned)
            throw new InvalidOperationException(
                $"Cannot complete a visit in state {Status}; only Planned visits can be marked visited.");
        Status = RouteVisitStatus.Visited;
        VisitNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CompletedAtUtc = nowUtc;
    }

    public void MarkSkipped(string? reason, DateTime nowUtc)
    {
        if (Status != RouteVisitStatus.Planned)
            throw new InvalidOperationException(
                $"Cannot skip a visit in state {Status}; only Planned visits can be skipped.");
        Status = RouteVisitStatus.Skipped;
        VisitNote = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        CompletedAtUtc = nowUtc;
    }
}

public enum RouteVisitKind
{
    Sales,
    Collection,
    Delivery,
    FollowUp,
    Other,
}

public enum RouteVisitStatus
{
    Planned,
    Visited,
    Skipped,
}
