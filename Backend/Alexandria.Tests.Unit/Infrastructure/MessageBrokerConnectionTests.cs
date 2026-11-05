using Alexandria.Common.Exceptions;
using Alexandria.Infrastructure.HealthChecks;
using Alexandria.Infrastructure.Messaging;
using AwesomeAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.Infrastructure;

public class MessageBrokerConnectionTests
{
    private static IConnection OpenConnection()
    {
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        return connection;
    }

    [Fact]
    public async Task get_connection_disabled_throws_disabled()
    {
        var broker = new MessageBrokerConnection(isEnabled: false);

        var act = () => broker.GetConnectionAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<MessageBrokerUnavailableException>())
            .Which.Disabled.Should().BeTrue();
    }

    [Fact]
    public async Task get_connection_before_first_connect_times_out_as_unavailable()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);

        var act = () => broker.GetConnectionAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<MessageBrokerUnavailableException>())
            .Which.Disabled.Should().BeFalse();
    }

    [Fact]
    public async Task get_connection_returns_open_connection()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var connection = OpenConnection();

        broker.SetConnection(connection);

        var result = await broker.GetConnectionAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(connection);
        broker.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task get_connection_while_recovering_fails_fast()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var connection = Substitute.For<IConnection>();

        connection.IsOpen.Returns(false);
        broker.SetConnection(connection);

        var act = () => broker.GetConnectionAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<MessageBrokerUnavailableException>();
        broker.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task wait_for_connection_completes_when_connection_arrives()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var connection = OpenConnection();

        var waiting = broker.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        waiting.IsCompleted.Should().BeFalse();

        broker.SetConnection(connection);

        (await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            .Should().BeSameAs(connection);
    }

    [Fact]
    public async Task health_check_reports_without_waiting_for_broker()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var check = new RabbitMqHealthCheck(broker);

        var down = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        broker.SetConnection(OpenConnection());

        var up = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        down.Status.Should().Be(HealthStatus.Unhealthy);
        up.Status.Should().Be(HealthStatus.Healthy);
    }
}
