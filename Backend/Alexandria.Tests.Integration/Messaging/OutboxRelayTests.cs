using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Data.Models.Enumerators.Monitoring;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Infrastructure.Messaging.Outbox;
using Alexandria.Tests.Common.Fakes;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

namespace Alexandria.Tests.Integration.Messaging;

/// <summary>
/// Confirms and mandatory routing only mean something against a real broker: a routed message
/// must arrive with its row id, and an unroutable one must come back as a refusal instead of
/// vanishing.
/// </summary>
[Collection("FullStack")]
public class OutboxRelayTests(AlexandriaFixture fixture) : IAsyncLifetime
{
    private const string Exchange = "content-exchange";
    private const string QueueName = "outbox-relay-test-queue";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;
    private MessageBrokerSupervisor _supervisor = null!;
    private IChannel _channel = null!;

    public async ValueTask InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(fixture.GetConfigurationOverrides())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddWorkerDatabase(config);
        services.AddServices();
        services.AddRabbitMqAsync(config, ServiceType.Api);
        _provider = services.BuildServiceProvider();

        // Only the supervisor is started: a running relay would race the dispatcher under test.
        _supervisor = ActivatorUtilities.CreateInstance<MessageBrokerSupervisor>(_provider);
        await _supervisor.StartAsync(Ct);

        var connection = await _provider.GetRequiredService<MessageBrokerConnection>()
            .WaitForConnectionAsync(Ct).WaitAsync(TimeSpan.FromSeconds(30), Ct);

        _channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: Ct);
        await _channel.QueueDeclareAsync(QueueName, durable: false, exclusive: false, autoDelete: false,
            cancellationToken: Ct);
        await _channel.QueuePurgeAsync(QueueName, Ct);
        await _channel.QueueBindAsync(QueueName, Exchange, "outbox-test.#", cancellationToken: Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.QueueDeleteAsync(QueueName, cancellationToken: CancellationToken.None);
        await _channel.CloseAsync(CancellationToken.None);
        await _supervisor.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
        await fixture.ResetAsync();
    }

    private static OutboxWriter Writer(IUnitOfWork unitOfWork) => new(
        unitOfWork, new FakeMessageBrokerStatus(), new RecordingOutboxSignal(), NullLogger<OutboxWriter>.Instance);

    private OutboxDispatcher Dispatcher(IUnitOfWork unitOfWork) => new(
        unitOfWork,
        _provider.GetRequiredService<IPublisherService>(),
        _provider.GetRequiredService<IMessageBrokerStatus>(),
        _provider.GetRequiredService<OutboxHost>(),
        Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 }),
        NullLogger<OutboxDispatcher>.Instance);

    [Fact]
    public async Task routed_message_arrives_with_its_row_id_and_is_marked_sent()
    {
        await using (var scope = _provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.InTransactionAsync(
                () => Writer(unitOfWork).EnqueueAsync("outbox-test.ok", "hello"u8.ToArray(), Ct), Ct);
        }

        await using (var scope = _provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await Dispatcher(unitOfWork).DispatchDueAsync(DateTime.UtcNow, Ct);
        }

        await using var db = fixture.CreateDbContext();
        var row = await db.OutboxMessages.AsNoTracking().SingleAsync(Ct);
        row.SentAt.Should().NotBeNull();
        row.Attempts.Should().Be(0);

        var delivered = await _channel.BasicGetAsync(QueueName, autoAck: true, Ct);
        delivered.Should().NotBeNull();
        delivered!.Body.ToArray().Should().Equal("hello"u8.ToArray());
        delivered.BasicProperties.MessageId.Should().Be(row.Id.ToString());
        delivered.BasicProperties.Persistent.Should().BeTrue();
    }

    [Fact]
    public async Task unroutable_message_is_refused_counted_and_kept()
    {
        await using (var scope = _provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.InTransactionAsync(
                () => Writer(unitOfWork).EnqueueAsync("nobody-listens.here", [1], Ct), Ct);
        }

        await using (var scope = _provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await Dispatcher(unitOfWork).DispatchDueAsync(DateTime.UtcNow, Ct);
        }

        await using var db = fixture.CreateDbContext();
        var row = await db.OutboxMessages.AsNoTracking().SingleAsync(Ct);
        row.SentAt.Should().BeNull();
        row.DeadAt.Should().BeNull();
        row.Attempts.Should().Be(1);
        row.LastError.Should().Contain("No queue is bound for routing key 'nobody-listens.here'");
        row.AvailableAt.Should().BeAfter(DateTime.UtcNow);
    }
}
