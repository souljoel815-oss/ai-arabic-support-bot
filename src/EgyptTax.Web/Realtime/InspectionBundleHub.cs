using Microsoft.AspNetCore.SignalR;

namespace EgyptTax.Web.Realtime;

/// <summary>
/// T231 / US9 / FR-048 — SignalR hub at <c>/hubs/inspection-bundle</c>.
/// Transport-only (no client→server methods); the
/// <see cref="InspectionBundleProgressNotifier"/> broadcasts
/// <c>ProgressChanged</c> events through it. Mirrors the
/// <see cref="EtaStatusHub"/> pattern shipped at T125.
/// </summary>
public sealed class InspectionBundleHub : Hub
{
    /// <summary>
    /// Canonical message name. Clients invoke
    /// <c>connection.on("ProgressChanged", payload =&gt; …)</c>.
    /// </summary>
    public const string ProgressChangedMethod = "ProgressChanged";
}
