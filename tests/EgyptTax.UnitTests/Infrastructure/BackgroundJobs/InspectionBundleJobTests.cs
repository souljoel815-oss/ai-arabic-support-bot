using System.Text;
using EgyptTax.Application.Inspection;
using EgyptTax.Infrastructure.BackgroundJobs;
using NSubstitute;

namespace EgyptTax.UnitTests.Infrastructure.BackgroundJobs;

/// <summary>
/// T231 / US9 / FR-048 — pins the InspectionBundleJob's behaviour:
/// happy-path emits Started → Complete with the persisted ZIP path;
/// builder failure surfaces a Failed event AND re-throws (so
/// Hangfire's retry policy kicks in); operator-cancel emits Failed
/// with the cancellation marker AND propagates the
/// OperationCanceledException (Hangfire treats cancelled jobs
/// distinctly from failed ones).
/// </summary>
public class InspectionBundleJobTests : IDisposable
{
    private readonly string _tempRoot;

    public InspectionBundleJobTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"egypttax-bundle-tests-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task HappyPath_EmitsStartedThenComplete_AndPersistsZip()
    {
        var notifier = new CapturingNotifier();
        var fakeBundle = BuildFakeBundle();
        var builder = Substitute.For<IInspectionBundleBuilder>();
        builder.BuildAsync(Arg.Any<InspectionBundleRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(fakeBundle));

        var job = new InspectionBundleJob(builder, notifier,
            new InspectionBundleStorageOptions { RootDirectory = _tempRoot });

        var jobId = "job-" + Guid.NewGuid().ToString("N");
        var request = new InspectionBundleRequest(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), Guid.NewGuid(), AllowDrafts: false);

        await job.ExecuteAsync(jobId, request, CancellationToken.None);

        notifier.Events.Select(e => e.Phase).Should().Equal(
            InspectionBundleProgressPhase.Started,
            InspectionBundleProgressPhase.Complete);

        var complete = notifier.Events[^1];
        complete.JobId.Should().Be(jobId);
        complete.ResultPath.Should().NotBeNull();
        File.Exists(complete.ResultPath!).Should().BeTrue(
            because: "the Complete event's ResultPath MUST point to the on-disk ZIP so the client can fetch it");
        (await File.ReadAllBytesAsync(complete.ResultPath!)).Should().Equal(fakeBundle.ZipBytes);
        complete.FilesProcessed.Should().Be(fakeBundle.Manifest.Files.Count);
    }

    [Fact]
    public async Task BuilderThrows_EmitsFailed_AndRethrowsForHangfireRetry()
    {
        var notifier = new CapturingNotifier();
        var builder = Substitute.For<IInspectionBundleBuilder>();
        builder.BuildAsync(Arg.Any<InspectionBundleRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<InspectionBundleResult>>(_ => throw new InvalidOperationException("disk full"));

        var job = new InspectionBundleJob(builder, notifier,
            new InspectionBundleStorageOptions { RootDirectory = _tempRoot });

        var act = async () => await job.ExecuteAsync(
            "job-1",
            new InspectionBundleRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), Guid.NewGuid(), false),
            CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("disk full",
            because: "the job MUST re-throw so Hangfire's retry policy kicks in — the Failed event is for the UI, the throw is for Hangfire");

        notifier.Events.Select(e => e.Phase).Should().Equal(
            InspectionBundleProgressPhase.Started,
            InspectionBundleProgressPhase.Failed);
        notifier.Events[^1].ErrorMessage.Should().Contain("disk full");
    }

    [Fact]
    public async Task Cancellation_EmitsFailed_WithCancellationMarker_AndPropagates()
    {
        var notifier = new CapturingNotifier();
        var builder = Substitute.For<IInspectionBundleBuilder>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        builder.BuildAsync(Arg.Any<InspectionBundleRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<InspectionBundleResult>>(_ => throw new OperationCanceledException(cts.Token));

        var job = new InspectionBundleJob(builder, notifier,
            new InspectionBundleStorageOptions { RootDirectory = _tempRoot });

        var act = async () => await job.ExecuteAsync(
            "job-2",
            new InspectionBundleRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), Guid.NewGuid(), false),
            cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        notifier.Events.Select(e => e.Phase).Should().Equal(
            InspectionBundleProgressPhase.Started,
            InspectionBundleProgressPhase.Failed);
        notifier.Events[^1].ErrorMessage.Should().Be("OperationCanceledException",
            because: "the cancellation path uses a distinct marker so the UI can show 'cancelled' rather than 'error'");
    }

    private static InspectionBundleResult BuildFakeBundle()
    {
        var bytes = Encoding.UTF8.GetBytes("fake-zip-payload-for-test");
        var manifest = new InspectionBundleManifest(
            BundleVersion: "1.0",
            Company: new BundleCompany(Guid.NewGuid(), "123456789", new BundleBilingualName("شركة", "Co")),
            Period: new BundlePeriod(2026, "Custom", null, "2026-06-01", "2026-06-30"),
            GeneratedAt: DateTime.UtcNow,
            GeneratedByUserId: Guid.NewGuid(),
            Files: new List<BundleFile>
            {
                new("audit-extract.json", "deadbeef", 100, "audit"),
                new("manifest.json", "cafebabe", 50, "manifest"),
            },
            AuditChainExtract: new BundleAuditChainExtract(1, 100, "genesis", VerifiedAtGeneration: true),
            TopLevelArchiveSha256: "abcdef0123",
            DraftsExcluded: false);
        return new InspectionBundleResult(bytes, manifest, "egypttax-inspection-2026-06.zip");
    }

    private sealed class CapturingNotifier : IInspectionBundleProgressNotifier
    {
        public List<InspectionBundleProgressEvent> Events { get; } = new();
        public event EventHandler<InspectionBundleProgressEvent>? ProgressChanged;
        public Task NotifyAsync(InspectionBundleProgressEvent e, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(e);
            Events.Add(e);
            ProgressChanged?.Invoke(this, e);
            return Task.CompletedTask;
        }
    }
}
