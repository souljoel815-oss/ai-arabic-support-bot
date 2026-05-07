using EgyptTax.Application.Eta;
using EgyptTax.Domain.Eta;
using EgyptTax.Web.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace EgyptTax.UnitTests.Web.Realtime;

/// <summary>
/// T125 — proves the fan-out invariants of EtaStatusNotifier:
/// (1) every NotifyAsync call sends a "StatusChanged" message via
///     IHubContext to all connected clients;
/// (2) the in-process StatusChanged event fires for every
///     subscriber after the hub broadcast (not before — so a
///     thrown subscriber doesn't suppress the broadcast);
/// (3) NotifyAsync rejects null events early.
/// </summary>
public class EtaStatusNotifierTests
{
    [Fact]
    public async Task NotifyAsync_BroadcastsToAllHubClients_WithCanonicalMethodName()
    {
        var clientProxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(clientProxy);
        var hubContext = Substitute.For<IHubContext<EtaStatusHub>>();
        hubContext.Clients.Returns(clients);
        var notifier = new EtaStatusNotifier(hubContext);

        var evt = NewEvent();
        await notifier.NotifyAsync(evt);

        await clientProxy.Received(1).SendCoreAsync(
            EtaStatusHub.StatusChangedMethod,
            Arg.Is<object?[]>(args => args.Length == 1
                && ReferenceEquals(args[0], evt)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_RaisesLocalStatusChangedEvent_WithEachSubscriberSeeingTheSameEvent()
    {
        var hubContext = NewBenignHubContext();
        var notifier = new EtaStatusNotifier(hubContext);
        var captured = new List<EtaStatusChangedEvent>();
        notifier.StatusChanged += (_, e) => captured.Add(e);

        var evt = NewEvent();
        await notifier.NotifyAsync(evt);

        captured.Should().ContainSingle().Which.Should().Be(evt);
    }

    [Fact]
    public async Task NotifyAsync_FiresHubBroadcastBeforeLocalEvent_SoAThrowingSubscriberDoesNotSuppressBroadcast()
    {
        var clientProxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(clientProxy);
        var hubContext = Substitute.For<IHubContext<EtaStatusHub>>();
        hubContext.Clients.Returns(clients);
        var notifier = new EtaStatusNotifier(hubContext);
        notifier.StatusChanged += (_, _) => throw new InvalidOperationException("subscriber explosion");

        var act = async () => await notifier.NotifyAsync(NewEvent());

        // The subscriber's exception propagates (we want it loud, not
        // swallowed) but the hub broadcast already happened.
        await act.Should().ThrowAsync<InvalidOperationException>();
        await clientProxy.Received(1).SendCoreAsync(
            EtaStatusHub.StatusChangedMethod,
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_NullEvent_Throws()
    {
        var notifier = new EtaStatusNotifier(NewBenignHubContext());
        var act = async () => await notifier.NotifyAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static EtaStatusChangedEvent NewEvent() => new(
        SalesInvoiceId: Guid.NewGuid(),
        EtaSubmissionId: Guid.NewGuid(),
        DocumentNumber: "INV-2026-000001",
        PreviousStatus: EtaSubmissionStatus.Pending,
        NewStatus: EtaSubmissionStatus.Submitted,
        AttemptCount: 1,
        AtUtc: new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc));

    private static IHubContext<EtaStatusHub> NewBenignHubContext()
    {
        var clientProxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(clientProxy);
        var hubContext = Substitute.For<IHubContext<EtaStatusHub>>();
        hubContext.Clients.Returns(clients);
        return hubContext;
    }
}
