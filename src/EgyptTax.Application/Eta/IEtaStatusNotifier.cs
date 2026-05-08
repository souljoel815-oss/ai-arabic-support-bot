using EgyptTax.Domain.Eta;

namespace EgyptTax.Application.Eta;

/// <summary>
/// FR-035 / R-22 / T125 — port over the ETA status-change broadcast.
/// Publishers (the wrapper handler that posts + auto-submits) call
/// <see cref="NotifyAsync"/> after recording an attempt; subscribers
/// (the ETA Compliance Dashboard, an external mobile client over the
/// SignalR hub) react. The implementation in Web/Realtime fans the
/// event out to both transport surfaces:
///
///   * <c>IHubContext&lt;EtaStatusHub&gt;</c> for external SignalR
///     clients (e.g. a future mobile app polling for status changes).
///   * The in-process <see cref="StatusChanged"/> event for Blazor
///     pages that already have a SignalR connection through the
///     Server-render pipeline and don't need a second one.
/// </summary>
public interface IEtaStatusNotifier
{
    Task NotifyAsync(EtaStatusChangedEvent e, CancellationToken cancellationToken = default);

    event EventHandler<EtaStatusChangedEvent>? StatusChanged;
}

public sealed record EtaStatusChangedEvent(
    Guid SalesInvoiceId,
    Guid EtaSubmissionId,
    string DocumentNumber,
    EtaSubmissionStatus PreviousStatus,
    EtaSubmissionStatus NewStatus,
    int AttemptCount,
    DateTime AtUtc
);
