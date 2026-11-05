using Alexandria.Infrastructure.Messaging;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.Infrastructure;

public class MessageBrokerSupervisorTests
{
    private readonly IConnectionFactory _factory = Substitute.For<IConnectionFactory>();

    private MessageBrokerSupervisor CreateSut(MessageBrokerConnection broker) =>
        new(broker, _factory, NullLogger<MessageBrokerSupervisor>.Instance)
        {
            InitialRetryDelay = TimeSpan.FromMilliseconds(10),
            MaxRetryDelay = TimeSpan.FromMilliseconds(20)
        };

    [Fact]
    public async Task disabled_broker_never_connects()
    {
        var broker = new MessageBrokerConnection(isEnabled: false);
        var sut = CreateSut(broker);
        var ct = TestContext.Current.CancellationToken;

        await sut.StartAsync(ct);
        await sut.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), ct);

        await _factory.DidNotReceive().CreateConnectionAsync(Arg.Any<CancellationToken>());
        broker.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task retries_until_broker_appears_then_publishes_connection()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var connection = Substitute.For<IConnection>();
        var ct = TestContext.Current.CancellationToken;

        connection.IsOpen.Returns(true);
        _factory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<IConnection>(new InvalidOperationException("refused")),
                _ => Task.FromException<IConnection>(new InvalidOperationException("refused")),
                _ => Task.FromResult(connection));

        var sut = CreateSut(broker);

        await sut.StartAsync(ct);

        var connected = await broker.WaitForConnectionAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct);

        connected.Should().BeSameAs(connection);
        broker.IsAvailable.Should().BeTrue();
        await _factory.Received(3).CreateConnectionAsync(Arg.Any<CancellationToken>());

        await sut.StopAsync(ct);
    }

    [Fact]
    public async Task stop_during_outage_ends_loop_without_throwing()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var ct = TestContext.Current.CancellationToken;

        _factory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("refused"));

        var sut = CreateSut(broker);

        await sut.StartAsync(ct);
        await Task.Delay(50, ct);
        await sut.StopAsync(ct);

        sut.ExecuteTask!.IsFaulted.Should().BeFalse();
        broker.IsAvailable.Should().BeFalse();
    }
}
