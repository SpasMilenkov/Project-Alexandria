using Alexandria.Infrastructure.Messaging;
using Alexandria.Tests.Unit.Common;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Alexandria.Tests.Unit.Infrastructure;

public class DeliverySettlementTests
{
    private const ulong Tag = 7;

    private readonly IChannel _channel = Substitute.For<IChannel>();
    private readonly RecordingLogger<DeliverySettlementTests> _logger = new();

    private static AlreadyClosedException BrokerShutdown() =>
        new(new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));

    [Fact]
    public async Task ack_on_an_open_channel_reports_it_was_sent()
    {
        var sent = await DeliverySettlement.AckAsync(_channel, Tag, _logger, TestContext.Current.CancellationToken);

        sent.Should().BeTrue();
        await _channel.Received(1).BasicAckAsync(Tag, false, Arg.Any<CancellationToken>());
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ack_on_a_closed_channel_is_absorbed_and_logged_as_requeued()
    {
        _channel.BasicAckAsync(default, default, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(_ => ValueTask.FromException(BrokerShutdown()));

        var sent = await DeliverySettlement.AckAsync(_channel, Tag, _logger, TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
        _logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Information && e.Message.Contains("CONNECTION_FORCED")
                                            && e.Message.Contains("requeues"));
    }

    [Fact]
    public async Task nack_on_a_closed_channel_is_absorbed_and_logged_as_requeued()
    {
        _channel.BasicNackAsync(default, default, default, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(_ => ValueTask.FromException(BrokerShutdown()));

        var sent = await DeliverySettlement.NackAsync(_channel, Tag, requeue: true, _logger,
            TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task other_ack_failures_still_surface()
    {
        _channel.BasicAckAsync(default, default, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(_ => ValueTask.FromException(new InvalidOperationException("not a closed channel")));

        var act = () => DeliverySettlement.AckAsync(_channel, Tag, _logger, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
