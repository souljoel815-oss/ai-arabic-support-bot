using EgyptTax.Application.Audit;
using EgyptTax.Application.Compliance.TinRevalidation;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.BackgroundJobs;
using EgyptTax.Infrastructure.Compliance;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.UnitTests.Infrastructure.BackgroundJobs;

public class SupplierTinRevalidationJobTests
{
    [Fact]
    public async Task EmptySource_StillEmits_RunSummary_WithZeroCounts()
    {
        var audit = new CaptureAuditLogStore();
        var job = new SupplierTinRevalidationJob(
            new EmptySupplierTinSource(),
            new AlwaysValidTinRevalidator(),
            audit,
            new FixedClock(new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc))
        );

        var result = await job.RunOnceAsync();

        result.CheckedCount.Should().Be(0);
        audit
            .Captured.Should()
            .ContainSingle(
                e => e.Kind == "supplier.tin_revalidation_run",
                because: "even an empty tick MUST audit-log so an inspector knows the cron is firing"
            );
    }

    [Fact]
    public async Task ValidSuppliers_Increment_ValidCount_NoInvalidatedEvents()
    {
        var audit = new CaptureAuditLogStore();
        var source = new StubSource(
            new[]
            {
                new SupplierTinRow(Guid.NewGuid(), "111111111", "Supplier A"),
                new SupplierTinRow(Guid.NewGuid(), "222222222", "Supplier B"),
            }
        );
        var job = new SupplierTinRevalidationJob(
            source,
            new AlwaysValidTinRevalidator(),
            audit,
            new FixedClock(DateTime.UtcNow)
        );

        var result = await job.RunOnceAsync();

        result.CheckedCount.Should().Be(2);
        result.ValidCount.Should().Be(2);
        result.InvalidCount.Should().Be(0);
        audit
            .Captured.Should()
            .NotContain(
                e => e.Kind == "supplier.tin_invalidated",
                because: "no invalidations expected when every TIN is reported valid"
            );
    }

    [Fact]
    public async Task InvalidatedTin_Emits_PerSupplier_AuditEvent()
    {
        var supplierId = Guid.NewGuid();
        var audit = new CaptureAuditLogStore();
        var source = new StubSource(
            new[] { new SupplierTinRow(supplierId, "999999999", "Bad Supplier") }
        );
        var job = new SupplierTinRevalidationJob(
            source,
            new InvalidatingRevalidator(),
            audit,
            new FixedClock(DateTime.UtcNow)
        );

        var result = await job.RunOnceAsync();

        result.InvalidCount.Should().Be(1);
        var invalidEvent = audit.Captured.Single(e => e.Kind == "supplier.tin_invalidated");
        invalidEvent.PayloadJson.Should().Contain(supplierId.ToString("D"));
        invalidEvent.PayloadJson.Should().Contain("999999999");
    }

    [Fact]
    public async Task PerRowException_LoggedAndDoesNotAbortBatch()
    {
        var audit = new CaptureAuditLogStore();
        var source = new StubSource(
            new[]
            {
                new SupplierTinRow(Guid.NewGuid(), "100000001", "Supplier 1"),
                new SupplierTinRow(Guid.NewGuid(), "200000002", "Supplier 2"),
                new SupplierTinRow(Guid.NewGuid(), "300000003", "Supplier 3"),
            }
        );
        var job = new SupplierTinRevalidationJob(
            source,
            new ThrowsOnSecondRevalidator(),
            audit,
            new FixedClock(DateTime.UtcNow)
        );

        var result = await job.RunOnceAsync();

        result.CheckedCount.Should().Be(3);
        result
            .ValidCount.Should()
            .Be(
                2,
                because: "the first and third suppliers were processed cleanly despite the middle one throwing"
            );
        audit.Captured.Should().ContainSingle(e => e.Kind == "supplier.tin_revalidation_failed");
        audit.Captured.Should().ContainSingle(e => e.Kind == "supplier.tin_revalidation_run");
    }

    private sealed class StubSource(IReadOnlyList<SupplierTinRow> rows) : ISupplierTinSource
    {
        public Task<IReadOnlyList<SupplierTinRow>> GetSuppliersToRevalidateAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult(rows);
    }

    private sealed class InvalidatingRevalidator : ISupplierTinRevalidator
    {
        public Task<TinRevalidationResult> RevalidateAsync(
            string tin,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new TinRevalidationResult(
                    tin,
                    IsValid: false,
                    "stub:always-invalid",
                    "Test invalidation"
                )
            );
    }

    private sealed class ThrowsOnSecondRevalidator : ISupplierTinRevalidator
    {
        private int _calls;

        public Task<TinRevalidationResult> RevalidateAsync(
            string tin,
            CancellationToken cancellationToken = default
        )
        {
            _calls++;
            if (_calls == 2)
            {
                throw new InvalidOperationException("Simulated registry hiccup");
            }
            return Task.FromResult(new TinRevalidationResult(tin, true, "stub", null));
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public List<AuditLogPayload> Captured { get; } = [];

        public Task<AuditLogEntry> AppendAsync(
            AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            Captured.Add(payload);
            return Task.FromResult(
                new AuditLogEntry(
                    index: Captured.Count,
                    tsUtc: DateTime.UtcNow,
                    actorUserId: payload.ActorUserId,
                    actorFirmName: payload.ActorFirmName,
                    companyId: payload.CompanyId,
                    kind: payload.Kind,
                    payloadJson: payload.PayloadJson,
                    prevHash: new byte[32],
                    thisHash: new byte[32]
                )
            );
        }
    }
}
