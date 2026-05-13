namespace EgyptTax.Domain.Settings;

/// <summary>
/// Sales-rep workflow toggle. Single-row config (one per install)
/// following the same pattern as <see cref="InvoiceSettings"/>.
///
/// When <see cref="RequireApproval"/> is true, an invoice created by
/// a user with the SALES_REP role (and not ADMIN) must be submitted
/// for approval rather than posted directly. The approval lands in
/// the existing <c>/approvals</c> queue and an admin/manager
/// approves before the invoice is posted + sent to ETA.
///
/// Defaults to false so first-install reps can post directly out of
/// the box; the operator flips this on from Settings → Sales reps
/// once they want oversight.
/// </summary>
public sealed class SalesRepSettings
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public bool RequireApproval { get; private set; }

    private SalesRepSettings() { }

    public static SalesRepSettings CreateDefault() => new();

    public void UpdateRequireApproval(bool require) => RequireApproval = require;
}
