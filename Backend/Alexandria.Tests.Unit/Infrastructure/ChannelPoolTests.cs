using Alexandria.Infrastructure;
using AwesomeAssertions;
using NSubstitute;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.Infrastructure;

public class ChannelPoolTests
{
    private static (ChannelPool Pool, Func<bool> ConnectionTouched, IConnection Connection) CreateSut()
    {
        var touched = false;
        var connection = Substitute.For<IConnection>();

        var lazy = new Lazy<Task<IConnection>>(() =>
        {
            touched = true;

            return Task.FromResult(connection);
        });

        return (new ChannelPool(lazy), () => touched, connection);
    }

    [Fact]
    public void ctor_does_not_touch_connection()
    {
        var (_, touched, _) = CreateSut();

        touched().Should().BeFalse();
    }

    [Fact]
    public async Task acquire_creates_channel_on_miss()
    {
        var (pool, touched, connection) = CreateSut();

        var channel = Substitute.For<IChannel>();

        channel.IsOpen.Returns(true);

        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(channel);

        var ct = TestContext.Current.CancellationToken;

        var acquired = await pool.AcquireChannelAsync(ct);

        acquired.Should().BeSameAs(channel);
        touched().Should().BeTrue();
        await connection.Received(1).CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task acquire_reuses_open_pooled_channel()
    {
        var (pool, _, connection) = CreateSut();

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
}