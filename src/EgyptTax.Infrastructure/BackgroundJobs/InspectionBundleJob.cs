using System.Globalization;
using EgyptTax.Application.Inspection;

namespace EgyptTax.Infrastructure.BackgroundJobs;

/// <summary>
/// T231 / US9 / FR-048 — Hangfire-hosted inspection-bundle generator.
///
/// The synchronous in-request <see cref="IInspectionBundleBuilder"/>
/// path stays available for small periods; for inspector handoffs
/// covering long windows (a full fiscal year of attachments + audit
/// extract) the build can take minutes, so this job decouples the
/// generation from the request thread:
/// <list type="number">
///   <item>Operator triggers the job from
///   <c>GenerateBundlePage.razor</c> with a <c>jobId</c>.</item>
///   <item>Hangfire enqueues + executes <see cref="ExecuteAsync"/>
///   on a background worker.</item>
///   <item>Job emits <c>Started</c> → <c>Complete</c> (or
///   <c>Failed</c>) progress events through
///   <see cref="IInspectionBundleProgressNotifier"/> — the
///   SignalR hub fan-out delivers them to every connected client
///   subscribed to <c>/hubs/inspection-bundle</c>.</item>
///   <item>On success, the resulting ZIP is written to
///   <see cref="InspectionBundleStorageOptions.RootDirectory"/>
///   under a <c>{jobId}.zip</c> filename — the <c>Complete</c>
///   event carries the path so the client can request the file
///   via a server-side download endpoint.</item>
/// </list>
///
/// Per-phase progress beyond Started/Complete (e.g., per-attachment
/// counter, audit-extract row count) requires instrumenting
/// <see cref="IInspectionBundleBuilder.BuildAsync"/> with progress
/// callbacks — a follow-up. Today's events are coarse-grained:
/// Started, Complete (with file count + total bytes), or Failed
/// (with the error message).
/// </summary>
public sealed class InspectionBundleJob
{
    private readonly IInspectionBundleBuilder _builder;
    private readonly IInspectionBundleProgressNotifier _notifier;
    private readonly InspectionBundleStorageOptions _storage;

    public InspectionBundleJob(
        IInspectionBundleBuilder builder,
        IInspectionBundleProgressNotifier notifier,
        InspectionBundleStorageOptions storage)
    {
        _builder = builder;
        _notifier = notifier;
        _storage = storage;
    }

    public async Task ExecuteAsync(
        string jobId,
        InspectionBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(request);

        await _notifier.NotifyAsync(new InspectionBundleProgressEvent(
            JobId: jobId,
            Phase: InspectionBundleProgressPhase.Started,
            Message: $"Starting bundle for {request.PeriodStart:yyyy-MM-dd}..{request.PeriodEnd:yyyy-MM-dd}"),
            cancellationToken);

        try
        {
            var result = await _builder.BuildAsync(request, cancellationToken);

            Directory.CreateDirectory(_storage.RootDirectory);
            var path = Path.Combine(_storage.RootDirectory, $"{jobId}.zip");
            await File.WriteAllBytesAsync(path, result.ZipBytes, cancellationToken);

            await _notifier.NotifyAsync(new InspectionBundleProgressEvent(
                JobId: jobId,
                Phase: InspectionBundleProgressPhase.Complete,
                Message: $"Bundle ready: {result.SuggestedFilename} "
                    + $"({result.ZipBytes.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes, "
                    + $"{result.Manifest.Files.Count} files)",
                FilesProcessed: result.Manifest.Files.Count,
                FilesTotal: result.Manifest.Files.Count,
                ResultPath: path),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation isn't a "failure" — surface as Failed with a
            // clear message so the UI can distinguish operator-cancel
            // from a build error.
            await _notifier.NotifyAsync(new InspectionBundleProgressEvent(
                JobId: jobId,
                Phase: InspectionBundleProgressPhase.Failed,
                Message: "Bundle build was cancelled.",
                ErrorMessage: "OperationCanceledException"),
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _notifier.NotifyAsync(new InspectionBundleProgressEvent(
                JobId: jobId,
                Phase: InspectionBundleProgressPhase.Failed,
                Message: $"Bundle build failed: {ex.Message}",
                ErrorMessage: ex.GetType().Name + ": " + ex.Message),
                CancellationToken.None);
            throw;
        }
    }
}

/// <summary>
/// Where the job persists completed bundle ZIPs. Operators configure
/// via appsettings; defaults to <c>{ContentRoot}/inspection-bundles</c>
/// during development. The directory is created on demand.
/// </summary>
public sealed class InspectionBundleStorageOptions
{
    public string RootDirectory { get; init; } = "inspection-bundles";
}
