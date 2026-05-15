using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Identity;

/// <summary>
/// v5 B.1 — group of <see cref="User"/>s reporting to one manager
/// for revenue-rollup purposes. v3 already shipped per-user
/// commission via <see cref="User.CommissionRatePercent"/>; what
/// was missing was a way to roll up "team Cairo" or "team field
/// sales" totals for the per-period revenue report. Each User can
/// belong to at most one team (via <c>User.SalesTeamId</c>); the
/// team's <see cref="ManagerUserId"/> is itself one of the users —
/// usually the senior rep.
///
/// Per-team commission is intentionally OUT OF SCOPE — keep the
/// per-user rate v3 already ships. Teams are an analytical lens,
/// not a separate compensation lever.
///
/// <see cref="MonthlyTargetEgp"/> drives the "attainment %" column
/// on the Sales by-team report. Null = no target set; the report
/// shows "—" for that team's attainment.
/// </summary>
public sealed class SalesTeam
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ArabicEnglishText Name { get; private set; }

    /// <summary>The senior rep / manager of the team. Nullable so a
    /// team can be created before the manager is decided. Must be
    /// a user that's already <c>SalesTeamId == this.Id</c> when set
    /// (enforced at the application layer; the entity just stores
    /// the FK).</summary>
    public Guid? ManagerUserId { get; private set; }

    /// <summary>Monthly revenue target in EGP (used by the
    /// attainment column on the by-team report). Null = no target.
    /// Stored as decimal? so a zero-target case (rare but valid)
    /// stays distinct from "not configured."</summary>
    public decimal? MonthlyTargetEgp { get; private set; }

    public SalesTeamStatus Status { get; private set; } = SalesTeamStatus.Active;
    public DateTime CreatedAtUtc { get; init; }

    private SalesTeam() { }

    public SalesTeam(ArabicEnglishText name, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name.Arabic) && string.IsNullOrWhiteSpace(name.English))
        {
            throw new ArgumentException("Sales team name (Ar or En) is required.", nameof(name));
        }
        Name = name;
        CreatedAtUtc = createdAtUtc;
    }

    public void Rename(ArabicEnglishText name)
    {
        if (string.IsNullOrWhiteSpace(name.Arabic) && string.IsNullOrWhiteSpace(name.English))
        {
            throw new ArgumentException("Sales team name (Ar or En) is required.", nameof(name));
        }
        Name = name;
    }

    public void SetManager(Guid? managerUserId)
    {
        if (managerUserId == Guid.Empty) managerUserId = null;
        ManagerUserId = managerUserId;
    }

    public void SetMonthlyTarget(decimal? targetEgp)
    {
        if (targetEgp is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetEgp),
                "Monthly target cannot be negative.");
        }
        MonthlyTargetEgp = targetEgp;
    }

    public void Deactivate() => Status = SalesTeamStatus.Inactive;
    public void Reactivate() => Status = SalesTeamStatus.Active;
}

public enum SalesTeamStatus
{
    Active,
    Inactive,
}
