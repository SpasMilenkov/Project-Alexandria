using Alexandria.Common.Exceptions;
using Alexandria.Infrastructure.Messaging;
using AwesomeAssertions;
using NSubstitute;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.Infrastructure;

public class ChannelPoolTests
{
    private static (ChannelPool Pool, IConnection Connection) CreateConnectedSut(int maxSize = 10)
    {
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);

        var broker = new MessageBrokerConnection(isEnabled: true);
        broker.SetConnection(connection);

        return (new ChannelPool(broker, TimeSpan.Zero, maxSize), connection);
    }

    [Fact]
    public async Task acquire_creates_channel_on_miss()
    {
        var (pool, connection) = CreateConnectedSut();
        var channel = Substitute.For<IChannel>();

        channel.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(channel);

        var acquired = await pool.AcquireChannelAsync(TestContext.Current.CancellationToken);

        acquired.Should().BeSameAs(channel);
        await connection.Received(1).CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task acquire_reuses_open_pooled_channel()
    {
        var (pool, connection) = CreateConnectedSut();
        var channel = Substitute.For<IChannel>();

        channel.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(channel);

        var ct = TestContext.Current.CancellationToken;

        var first = await pool.AcquireChannelAsync(ct);

        pool.ReleaseChannel(first);

        var second = await pool.AcquireChannelAsync(ct);

        second.Should().BeSameAs(first);
        await connection.Received(1).CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task acquire_without_connection_fails_fast_with_broker_unavailable()
    {
        var pool = new ChannelPool(new MessageBrokerConnection(isEnabled: true), TimeSpan.Zero);

        var act = async () => await pool.AcquireChannelAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<MessageBrokerUnavailableException>())
            .Which.Disabled.Should().BeFalse();
    }

    [Fact]
    public async Task acquire_failures_do_not_leak_permits()
    {
        var pool = new ChannelPool(new MessageBrokerConnection(isEnabled: true), TimeSpan.Zero, maxSize: 1);
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 3; i++)
        {
            var act = async () => await pool.AcquireChannelAsync(ct);

            await act.Should().ThrowAsync<MessageBrokerUnavailableException>();
        }

        // A leaked permit would make this wait forever instead of failing fast again.
        var final = async () => await pool.AcquireChannelAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), ct);

        await final.Should().ThrowAsync<MessageBrokerUnavailableException>();
    }

    [Fact]
    public async Task acquire_channel_creation_failure_releases_permit()
    {
        var (pool, connection) = CreateConnectedSut(maxSize: 1);
        var channel = Substitute.For<IChannel>();
        var ct = TestContext.Current.CancellationToken;

        channel.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<IChannel>(new InvalidOperationException("boom")),
                _ => Task.FromResult(channel));

        var failing = async () => await pool.AcquireChannelAsync(ct);

        await failing.Should().ThrowAsync<InvalidOperationException>();

        var acquired = await pool.AcquireChannelAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), ct);

        acquired.Should().BeSameAs(channel);
    }
}
