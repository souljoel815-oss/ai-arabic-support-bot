namespace EgyptTax.Application.Inspection;

/// <summary>
/// T231 / US9 / FR-048 — fan-out for inspection-bundle progress
/// events. The <see cref="InspectionBundleJob"/> emits one
/// <see cref="InspectionBundleProgressEvent"/> per phase (Started,
/// AuditExtractDone, AttachmentsDone, ZipDone, Complete) plus a
/// terminal Failed event on exceptions. Implementation in
/// Web/Realtime fans the events to two consumers:
/// <list type="bullet">
///   <item>An <c>IHubContext&lt;InspectionBundleHub&gt;</c> for
///   external SignalR clients (the GenerateBundlePage progress UI,
///   future operations dashboards).</item>
///   <item>A local <c>EventHandler</c> for in-process Blazor
///   subscribers that already have a SignalR pipe via Blazor Server
///   and shouldn't pay for a second round-trip.</item>
/// </list>
/// </summary>
public interface IInspectionBundleProgressNotifier
{
    event EventHandler<InspectionBundleProgressEvent>? ProgressChanged;
    Task NotifyAsync(InspectionBundleProgressEvent e, CancellationToken cancellationToken = default);
}

/// <summary>
/// One progress signal from the inspection-bundle job. <see cref="JobId"/>
/// scopes the event to a single bundle build so the UI can multiplex
/// progress across multiple concurrent jobs (rare for inspection
/// bundles, but the design supports it for free).
/// </summary>
public sealed record InspectionBundleProgressEvent(
    string JobId,
    InspectionBundleProgressPhase Phase,
    string Message,
    int? FilesProcessed = null,
    int? FilesTotal = null,
    string? ResultPath = null,
    string? ErrorMessage = null);

public enum InspectionBundleProgressPhase
{
    Started,
    AuditExtractDone,
    AttachmentsDone,
    ZipDone,
    Complete,
    Failed,
}
