using Alexandria.Common.Services;
using Alexandria.Infrastructure.Messaging;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Alexandria.Tests.Unit.Infrastructure;

public class DeliveryRetryTests
{
    private const ulong Tag = 42;

    private static readonly DeliveryRetryOptions NoDelay = new() { MaxAttempts = 3, RetryDelay = TimeSpan.Zero };

    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();
    private readonly IChannel _channel = Substitute.For<IChannel>();
    private readonly List<int> _parked = [];

    private DeliveryRetry CreateSut() => new(_publisher, NullLogger<DeliveryRetry>.Instance);

    private static IReadOnlyBasicProperties PropsWithHeaders(IDictionary<string, object?>? headers)
    {
        var props = Substitute.For<IReadOnlyBasicProperties>();
        props.Headers.Returns(headers);
        props.MessageId.Returns("message-1");
        return props;
    }

    private static BasicDeliverEventArgs Delivery(int storedAttempts) => new(
        "consumer", Tag, false, "content-exchange", "lyrics.job",
        PropsWithHeaders(new Dictionary<string, object?> { [DeliveryRetry.AttemptsHeader] = storedAttempts }),
        new byte[] { 1, 2, 3 }, CancellationToken.None);

    private Task ParkAsync(int attempt)
    {
        _parked.Add(attempt);
        return Task.CompletedTask;
    }

    [Fact]
    public void read_attempts_missing_properties_or_headers_returns_zero()
    {
        DeliveryRetry.ReadAttempts(null).Should().Be(0);
        DeliveryRetry.ReadAttempts(PropsWithHeaders(null)).Should().Be(0);
        DeliveryRetry.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>())).Should().Be(0);
    }

    [Fact]
    public void read_attempts_parses_int_long_and_text_headers()
    {
        DeliveryRetry.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>
            { [DeliveryRetry.AttemptsHeader] = 2 })).Should().Be(2);
        DeliveryRetry.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>
            { [DeliveryRetry.AttemptsHeader] = 4L })).Should().Be(4);
        DeliveryRetry.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>
            { [DeliveryRetry.AttemptsHeader] = "3"u8.ToArray() })).Should().Be(3);
    }

    [Fact]
    public void read_attempts_clamps_garbage_to_zero()
    {
        DeliveryRetry.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>
            { [DeliveryRetry.AttemptsHeader] = -3 })).Should().Be(0);
        DeliveryRetry.ReadAttempts(PropsWithHeaders(new Dictionary<string, object?>
            { [DeliveryRetry.AttemptsHeader] = "nope" })).Should().Be(0);
    }

    [Fact]
    public async Task failure_below_the_limit_republishes_with_the_count_raised_then_acks()
    {
        var delivery = Delivery(storedAttempts: 0);

        await CreateSut().HandleFailureAsync(_channel, delivery, NoDelay, ParkAsync,
            TestContext.Current.CancellationToken);

        _parked.Should().BeEmpty();
        Received.InOrder(() =>
        {
            _publisher.PublishAsync(
                Arg.Is<OutgoingMessage>(m => m.RoutingKey == "lyrics.job"
                                             && m.MessageId == "message-1"
                                             && m.Body.SequenceEqual(new byte[] { 1, 2, 3 })
                                             && (int)m.Headers![DeliveryRetry.AttemptsHeader]! == 1),
                Arg.Any<CancellationToken>());
            _channel.BasicAckAsync(Tag, false, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task failure_at_the_limit_parks_then_acks_without_republishing()
    {
        var delivery = Delivery(storedAttempts: 2);

        await CreateSut().HandleFailureAsync(_channel, delivery, NoDelay, ParkAsync,
            TestContext.Current.CancellationToken);

        _parked.Should().Equal(3);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, TestContext.Current.CancellationToken);
        await _channel.Received(1).BasicAckAsync(Tag, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task failed_republish_requeues_the_original()
    {
        _publisher.PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        await CreateSut().HandleFailureAsync(_channel, Delivery(storedAttempts: 0), NoDelay, ParkAsync,
            TestContext.Current.CancellationToken);

        await _channel.Received(1).BasicNackAsync(Tag, false, true, Arg.Any<CancellationToken>());
        await _channel.DidNotReceiveWithAnyArgs().BasicAckAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task republish_timing_out_requeues_the_original_instead_of_escaping()
    {
        // The client reports an RPC timeout as a cancellation even though nothing is stopping.
        _publisher.PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException("rpc timed out")));

        await CreateSut().HandleFailureAsync(_channel, Delivery(storedAttempts: 0), NoDelay, ParkAsync,
            TestContext.Current.CancellationToken);

        await _channel.Received(1).BasicNackAsync(Tag, false, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task closed_channel_while_requeueing_does_not_throw()
    {
        _publisher.PublishAsync(Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));
        _channel.BasicNackAsync(default, default, default, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(_ => ValueTask.FromException(
                new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"))));

        var act = () => CreateSut().HandleFailureAsync(_channel, Delivery(storedAttempts: 0), NoDelay, ParkAsync,
            TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task failed_park_requeues_the_original_instead_of_dropping_it()
    {
        await CreateSut().HandleFailureAsync(_channel, Delivery(storedAttempts: 2), NoDelay,
            _ => Task.FromException(new InvalidOperationException("database down")),
            TestContext.Current.CancellationToken);

        await _channel.Received(1).BasicNackAsync(Tag, false, true, Arg.Any<CancellationToken>());
        await _channel.DidNotReceiveWithAnyArgs().BasicAckAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task failed_park_waits_the_retry_delay_before_requeueing()
    {
        // The delay outlasts the test, so a nack can only happen if the pause is skipped.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var options = new DeliveryRetryOptions { MaxAttempts = 3, RetryDelay = TimeSpan.FromHours(1) };

        var act = () => CreateSut().HandleFailureAsync(_channel, Delivery(storedAttempts: 2), options,
            _ =>
            {
                stop.Cancel();
                return Task.FromException(new InvalidOperationException("database down"));
            },
            stop.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _channel.DidNotReceiveWithAnyArgs().BasicNackAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task park_now_parks_regardless_of_the_count()
    {
        await CreateSut().ParkNowAsync(_channel, Delivery(storedAttempts: 0), ParkAsync,
            TestContext.Current.CancellationToken);

        _parked.Should().Equal(1);
        await _channel.Received(1).BasicAckAsync(Tag, false, Arg.Any<CancellationToken>());
    }
}
