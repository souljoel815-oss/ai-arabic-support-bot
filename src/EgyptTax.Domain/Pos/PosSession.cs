using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.Pos;

/// <summary>
/// v5 A.6 — POS cash-drawer accountability. Operator opens a
/// session at the start of their shift (types opening cash), runs
/// sales through <c>/pos</c>, then closes the session by typing
/// the actual cash count. The variance (actual − expected) is
/// recorded so the owner can reconcile shrinkage / over-rings /
/// theft.
///
/// Expected-closing math: opening cash + sum of cash-method
/// CustomerReceiptVoucher.NetCashReceived posted between
/// <see cref="OpenedAtUtc"/> and <see cref="ClosedAtUtc"/>
/// (computed at close time; no per-CRV FK to keep the schema
/// surface area small).
///
/// State: Open → Closed (terminal). A new session can only open
/// if no other session is currently Open (prevents two cashiers
/// double-counting the same drawer).
/// </summary>
public sealed class PosSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OpenedByUserId { get; init; }
    public DateTime OpenedAtUtc { get; init; }
    public MoneyEgp OpeningCash { get; init; } = MoneyEgp.Zero;

    public DateTime? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }

    // NB: stored as plain decimal? rather than MoneyEgp? because
    // EF Core 8 ComplexProperty doesn't handle nullable struct
    // owners cleanly. The Close() factory keeps MoneyEgp on the
    // input surface for type-safety at call sites.
    public decimal? ExpectedClosingCashEgp { get; private set; }
    public decimal? ActualClosingCashEgp { get; private set; }
    public decimal? VarianceEgp { get; private set; }
    public string? Notes { get; private set; }

    public PosSessionStatus Status =>
        ClosedAtUtc is null ? PosSessionStatus.Open : PosSessionStatus.Closed;

    private PosSession() { }

    public PosSession(
        Guid openedByUserId,
        DateTime openedAtUtc,
        MoneyEgp openingCash)
    {
        if (openedByUserId == Guid.Empty)
        {
            throw new ArgumentException("OpenedByUserId required.", nameof(openedByUserId));
        }
        if (openingCash.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(openingCash), "Opening cash cannot be negative.");
        }
        OpenedByUserId = openedByUserId;
        OpenedAtUtc = openedAtUtc;
        OpeningCash = openingCash;
    }

    /// <summary>Close the session with the operator's counted cash.
    /// <paramref name="expectedClosingCash"/> is derived by the
    /// caller (sums cash receipts in the open-window); the variance
    /// = actual − expected is computed and stored so an audit can
    /// reconstruct the math without re-querying.</summary>
    public void Close(
        Guid closedByUserId,
        DateTime closedAtUtc,
        MoneyEgp expectedClosingCash,
        MoneyEgp actualClosingCash,
        string? notes = null)
    {
        if (Status == PosSessionStatus.Closed)
        {
            throw new InvalidOperationException(
                $"PosSession {Id} is already closed at {ClosedAtUtc:o}.");
        }
        if (closedByUserId == Guid.Empty)
        {
            throw new ArgumentException("ClosedByUserId required.", nameof(closedByUserId));
        }
        if (actualClosingCash.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualClosingCash), "Actual closing cash cannot be negative.");
        }

        ClosedByUserId = closedByUserId;
        ClosedAtUtc = closedAtUtc;
        ExpectedClosingCashEgp = expectedClosingCash.Amount;
        ActualClosingCashEgp = actualClosingCash.Amount;
        VarianceEgp = actualClosingCash.Amount - expectedClosingCash.Amount;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}

public enum PosSessionStatus
{
    Open,
    Closed,
}
