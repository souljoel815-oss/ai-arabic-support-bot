using EgyptTax.Application.Eta;
using Microsoft.AspNetCore.SignalR;

namespace EgyptTax.Web.Realtime;

/// <summary>
/// FR-035 / R-22 — SignalR hub at <c>/hubs/eta</c>. The hub is
/// transport-only: it doesn't accept any client→server method calls,
/// it just acts as the named landing point that
/// <see cref="EtaStatusNotifier"/> broadcasts <c>StatusChanged</c>
/// events through. External clients (a future mobile app, an admin
/// dashboard hosted on a different process) subscribe to the
/// <c>StatusChanged</c> message; in-process Blazor pages subscribe
/// to the <see cref="IEtaStatusNotifier.StatusChanged"/> event
/// directly because they're already connected through Blazor
/// Server's own SignalR pipe and a second one would just duplicate
/// the wire traffic.
/// </summary>
public sealed class EtaStatusHub : Hub
{
    /// <summary>
    /// Canonical message name. Clients invoke
    /// <c>connection.on("StatusChanged", payload =&gt; …)</c>.
    /// </summary>
    public const string StatusChangedMethod = "StatusChanged";
}
