using EgyptTax.Application.Eta;
using Microsoft.AspNetCore.SignalR;

namespace EgyptTax.Web.Realtime;

/// <summary>
/// FR-035 / R-22 — fan-out implementation: every
/// <see cref="NotifyAsync"/> call broadcasts to all SignalR clients
/// connected to <c>/hubs/eta</c> AND raises the local
/// <see cref="StatusChanged"/> event for in-process Blazor
/// subscribers. The hub broadcast is fire-and-forget on a per-message
/// basis (one client failing to receive doesn't block the others —
/// SignalR's <c>SendAsync</c> already has that semantic). Subscribers
/// to the local event MUST handle exceptions in their handler — the
/// notifier doesn't catch them so a misbehaving subscriber surfaces
/// loudly rather than silently breaking the chain.
/// </summary>
public sealed class EtaStatusNotifier : IEtaStatusNotifier
{
    private readonly IHubContext<EtaStatusHub> _hubContext;

    public EtaStatusNotifier(IHubContext<EtaStatusHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public event EventHandler<EtaStatusChangedEvent>? StatusChanged;

    public async Task NotifyAsync(EtaStatusChangedEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Hub broadcast first — it's the externally-observable
        // surface. The local event fires after so an in-process
        // subscriber that throws doesn't prevent the broadcast from
        // happening.
        await _hubContext.Clients.All.SendAsync(
            EtaStatusHub.StatusChangedMethod, e, cancellationToken);

        StatusChanged?.Invoke(this, e);
    }
}
