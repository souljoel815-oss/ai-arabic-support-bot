using EgyptTax.Application.Inspection;
using Microsoft.AspNetCore.SignalR;

namespace EgyptTax.Web.Realtime;

/// <summary>
/// T231 / US9 / FR-048 — fan-out implementation. Mirrors the
/// <see cref="EtaStatusNotifier"/> contract: hub broadcast first
/// (the externally-observable surface), then the local event for
/// in-process Blazor subscribers. A misbehaving local subscriber
/// surfaces loudly because the notifier doesn't catch its
/// exceptions — same semantic as the eta notifier so the
/// behaviour is consistent across the codebase.
/// </summary>
public sealed class InspectionBundleProgressNotifier : IInspectionBundleProgressNotifier
{
    private readonly IHubContext<InspectionBundleHub> _hubContext;

    public InspectionBundleProgressNotifier(IHubContext<InspectionBundleHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public event EventHandler<InspectionBundleProgressEvent>? ProgressChanged;

    public async Task NotifyAsync(InspectionBundleProgressEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);

        await _hubContext.Clients.All.SendAsync(
            InspectionBundleHub.ProgressChangedMethod, e, cancellationToken);

        ProgressChanged?.Invoke(this, e);
    }
}
