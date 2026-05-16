using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Projects;

/// <summary>
/// v5 D.2.1 — one row of logged work per (user, day, project, task?).
/// The weekly entry page lets a user fill a 7×project grid; each
/// non-zero cell becomes one of these rows. The Project P&L page
/// (D.2.2) sums (hours × hourly_rate) for the linked project to
/// surface labor cost; the team view aggregates rows for the manager.
///
/// Hours-per-day-per-user is capped at 16 (catches typos like "80"
/// instead of "8.0"); the cap lives on the entry-form callsite, not
/// on the aggregate, because it depends on cross-row state (sum of
/// hours in the same day).
///
/// HourlyRateEgp is captured per-row so historical cost stays stable
/// when the user's default rate later changes — the same pattern
/// invoice lines use for unit price.
/// </summary>
public sealed class TimesheetEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public DateOnly EntryDate { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? ProjectTaskId { get; private set; }
    public decimal Hours { get; private set; }
    public bool Billable { get; private set; }
    public decimal HourlyRateEgp { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    private TimesheetEntry() { }

    public TimesheetEntry(
        Guid userId,
        DateOnly entryDate,
        Guid projectId,
        Guid? projectTaskId,
        decimal hours,
        bool billable,
        decimal hourlyRateEgp,
        string? note = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (hours <= 0m || hours > 24m)
        {
            throw new ArgumentOutOfRangeException(nameof(hours),
                $"Hours must be in (0, 24]. Got {hours}.");
        }
        if (hourlyRateEgp < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(hourlyRateEgp),
                "Hourly rate cannot be negative.");
        }
        if (billable && hourlyRateEgp <= 0m)
        {
            throw new ArgumentException(
                "A billable entry must carry a positive hourly rate.", nameof(billable));
        }
        UserId = userId;
        EntryDate = entryDate;
        ProjectId = projectId;
        ProjectTaskId = projectTaskId == Guid.Empty ? null : projectTaskId;
        Hours = hours;
        Billable = billable;
        HourlyRateEgp = hourlyRateEgp;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void Update(decimal hours, bool billable, decimal hourlyRateEgp, string? note)
    {
        if (hours <= 0m || hours > 24m)
        {
            throw new ArgumentOutOfRangeException(nameof(hours),
                $"Hours must be in (0, 24]. Got {hours}.");
        }
        if (hourlyRateEgp < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(hourlyRateEgp),
                "Hourly rate cannot be negative.");
        }
        if (billable && hourlyRateEgp <= 0m)
        {
            throw new ArgumentException(
                "A billable entry must carry a positive hourly rate.", nameof(billable));
        }
        Hours = hours;
        Billable = billable;
        HourlyRateEgp = hourlyRateEgp;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public decimal AmountEgp() => Hours * HourlyRateEgp;
}
