using Alexandria.Infrastructure.Messaging;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;

namespace Alexandria.Tests.Unit.Infrastructure;

public class ConsumerStartupTests
{
    private static IConnection OpenConnection()
    {
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        return connection;
    }

    [Fact]
    public async Task waits_for_connection_before_running_setup()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var connection = OpenConnection();
        var ct = TestContext.Current.CancellationToken;
        IConnection? seen = null;

        var run = ConsumerStartup.RunAsync(broker, (c, _) =>
        {
            seen = c;
            return Task.CompletedTask;
        }, NullLogger.Instance, ct: ct);

        await Task.Delay(50, ct);

        seen.Should().BeNull();

        broker.SetConnection(connection);
        await run.WaitAsync(TimeSpan.FromSeconds(5), ct);

        seen.Should().BeSameAs(connection);
    }

    [Fact]
    public async Task retries_setup_after_failure()
    {
        var broker = new MessageBrokerConnection(isEnabled: true);
        var ct = TestContext.Current.CancellationToken;
        var attempts = 0;

        broker.SetConnection(OpenConnection());

        await ConsumerStartup.RunAsync(broker, (_, _) =>
        {
            attempts++;

            if (attempts < 3)
                throw new InvalidOperationException("channel closed");

            return Task.CompletedTask;
        }, NullLogger.Instance, TimeSpan.FromMilliseconds(10), ct).WaitAsync(TimeSpan.FromSeconds(5), ct);

        attempts.Should().Be(3);
    }

    [Fact]
    public async Task disabled_broker_idles_until_cancelled()
    {
        var broker = new MessageBrokerConnection(isEnabled: false);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var setupRan = false;

        var run = ConsumerStartup.RunAsync(broker, (_, _) =>
        {
            setupRan = true;
            return Task.CompletedTask;
        }, NullLogger.Instance, ct: cts.Token);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        var act = () => run;

        await act.Should().ThrowAsync<OperationCanceledException>();
        setupRan.Should().BeFalse();
    }
}
