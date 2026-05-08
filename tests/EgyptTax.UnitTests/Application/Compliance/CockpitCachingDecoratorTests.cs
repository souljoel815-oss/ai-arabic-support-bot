using EgyptTax.Application.Compliance;
using EgyptTax.SharedKernel;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace EgyptTax.UnitTests.Application.Compliance;

/// <summary>
/// T236a / Round-6 F13 — pins the
/// <see cref="CockpitCachingDecorator"/> contract: identical
/// (year, month) calls within the sliding window hit the cache (the
/// inner query is invoked exactly once); calling
/// <see cref="ICockpitCacheInvalidator.InvalidateForMonth(int, int)"/>
/// busts the entry so the next call re-runs the inner query;
/// different months don't share cache entries.
/// </summary>
public class CockpitCachingDecoratorTests
{
    [Fact]
    public async Task SecondCall_ForSameMonth_ServesFromCache_WithoutHittingInner()
    {
        var inner = Substitute.For<IMonthlyTaxClosingCockpitQuery>();
        inner
            .RunAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(BuildEmptyCockpit(2026, 4)));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorator = new CockpitCachingDecorator(inner, cache);

        var first = await decorator.RunAsync(2026, 4, CancellationToken.None);
        var second = await decorator.RunAsync(2026, 4, CancellationToken.None);

        first.Should().NotBeNull();
        second
            .Should()
            .BeSameAs(
                first,
                because: "the cached entry is returned by reference for identical (year, month) calls within the sliding window"
            );
        await inner.Received(1).RunAsync(2026, 4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DifferentMonths_DontShareCacheEntries()
    {
        var inner = Substitute.For<IMonthlyTaxClosingCockpitQuery>();
        inner
            .RunAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(BuildEmptyCockpit(ci.ArgAt<int>(0), ci.ArgAt<int>(1))));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorator = new CockpitCachingDecorator(inner, cache);

        await decorator.RunAsync(2026, 4, CancellationToken.None);
        await decorator.RunAsync(2026, 5, CancellationToken.None);
        await decorator.RunAsync(2026, 4, CancellationToken.None);
        await decorator.RunAsync(2026, 5, CancellationToken.None);

        await inner.Received(1).RunAsync(2026, 4, Arg.Any<CancellationToken>());
        await inner.Received(1).RunAsync(2026, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateForMonth_BustsTheEntry_NextCallReHitsInner()
    {
        var inner = Substitute.For<IMonthlyTaxClosingCockpitQuery>();
        inner
            .RunAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(BuildEmptyCockpit(2026, 4)));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorator = new CockpitCachingDecorator(inner, cache);

        await decorator.RunAsync(2026, 4, CancellationToken.None);
        await decorator.RunAsync(2026, 4, CancellationToken.None);
        decorator.InvalidateForMonth(2026, 4);
        await decorator.RunAsync(2026, 4, CancellationToken.None);

        await inner.Received(2).RunAsync(2026, 4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateForMonth_OnlyAffectsTheTargetedMonth()
    {
        var inner = Substitute.For<IMonthlyTaxClosingCockpitQuery>();
        inner
            .RunAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(BuildEmptyCockpit(ci.ArgAt<int>(0), ci.ArgAt<int>(1))));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var decorator = new CockpitCachingDecorator(inner, cache);

        await decorator.RunAsync(2026, 4, CancellationToken.None);
        await decorator.RunAsync(2026, 5, CancellationToken.None);

        // Bust April only.
        decorator.InvalidateForMonth(2026, 4);

        await decorator.RunAsync(2026, 4, CancellationToken.None);
        await decorator.RunAsync(2026, 5, CancellationToken.None);

        // April was invalidated → 2 inner hits; May was untouched → 1 inner hit.
        await inner.Received(2).RunAsync(2026, 4, Arg.Any<CancellationToken>());
        await inner.Received(1).RunAsync(2026, 5, Arg.Any<CancellationToken>());
    }

    private static MonthlyTaxClosingCockpit BuildEmptyCockpit(int year, int month) =>
        new(
            Year: year,
            Month: month,
            PeriodStart: new DateOnly(year, month, 1),
            PeriodEnd: new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            PeriodIsLocked: false,
            VatReadinessPercent: 100m,
            TotalPostsInPeriod: 0,
            CleanPostsCount: 0,
            MissingDocuments: Array.Empty<MissingDocumentBucket>(),
            FailedEtaSubmissionCount: 0,
            FailedEtaSubmissionTotalGrand: MoneyEgp.Zero,
            DraftsInPeriodCount: 0,
            NonRecoverableInputVat: MoneyEgp.Zero,
            PeriodLockChecklist: Array.Empty<PeriodLockChecklistItem>()
        );
}
