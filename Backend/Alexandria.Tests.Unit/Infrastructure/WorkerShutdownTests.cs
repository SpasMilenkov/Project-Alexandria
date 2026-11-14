using System.Text;
using Alexandria.Common.Config;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Tests.Unit.Common;
using Alexandria.Workers.Lyrics;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Queueing;
using Alexandria.Workers.MediaMetadata.Workers;
using Alexandria.Workers.MediaTranspilation;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using DocumentWorker = Alexandria.Workers.Document.Worker;
using MediaWorker = Alexandria.Workers.Media.Worker;

namespace Alexandria.Tests.Unit.Infrastructure;

public class WorkerShutdownTests
{
    private const string Media = "media";
    private const string Document = "document";
    private const string Transpilation = "transpilation";
    private const string Lyrics = "lyrics";
    private const string Trigger = "trigger";
    private const string Results = "results";
    private const string Backfill = "backfill";

    private static readonly TimeSpan HangLimit = TimeSpan.FromSeconds(10);

    public static TheoryData<string> AllConsumers => [Media, Document, Transpilation, Lyrics, Trigger, Results, Backfill];

    // Results and Backfill are left out: they already let cancellation pass through their catch.
    public static TheoryData<string> ConsumersWithBroadCatch => [Media, Document, Transpilation, Lyrics, Trigger];

    private sealed record Harness(
        BackgroundService Worker,
        IChannel Channel,
        Func<IReadOnlyCollection<(LogLevel Level, string Message)>> Logs,
        TaskCompletionSource<IAsyncBasicConsumer> Consumer);

    private static Harness CreateHarness(string name, IServiceProvider provider)
    {
        var channel = Substitute.For<IChannel>();
        var consumer = new TaskCompletionSource<IAsyncBasicConsumer>(TaskCreationOptions.RunContinuationsAsynchronously);

        channel.QueueDeclareAsync(default!, default, default, default, default, default, default, default)
            .ReturnsForAnyArgs(new QueueDeclareOk("queue", 0, 0));
        channel.BasicConsumeAsync(default!, default, default!, default, default, default, default!, default)
            .ReturnsForAnyArgs(call =>
            {
                consumer.TrySetResult(call.ArgAt<IAsyncBasicConsumer>(6));
                return Task.FromResult("tag");
            });

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>()).Returns(channel);

        var broker = new MessageBrokerConnection(isEnabled: true);
        broker.SetConnection(connection);

        var configuration = new ConfigurationBuilder().Build();
        var rabbit = Options.Create(new RabbitMqConsumerConfig());

        Harness Build<T>(Func<ILogger<T>, BackgroundService> create)
        {
            var logger = new RecordingLogger<T>();
            return new Harness(create(logger), channel, () => logger.Entries, consumer);
        }

        return name switch
        {
            Media => Build<MediaWorker>(l => new MediaWorker(l, broker, configuration, provider)),
            Document => Build<DocumentWorker>(l => new DocumentWorker(l, broker, configuration, provider)),
            Transpilation => Build<TranspilationWorker>(l => new TranspilationWorker(l, broker, configuration, provider)),
            Lyrics => Build<LyricsWorker>(l => new LyricsWorker(l, broker, configuration, provider)),
            Trigger => Build<TriggerConsumerWorker>(l =>
                new TriggerConsumerWorker(l, broker, provider, rabbit, new AccumulatorBuffer())),
            Results => Build<ResultsConsumerWorker>(l => new ResultsConsumerWorker(
                l, broker, provider, rabbit, Options.Create(new EssentiaConfig()), Options.Create(new ProcessingCapabilities()),
                Substitute.For<IAutoTagQueue>())),
            Backfill => Build<EnrichmentBackfillWorker>(l => new EnrichmentBackfillWorker(l, broker, provider, rabbit)),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static IServiceProvider ProviderWithScopedServices(IServiceProvider scoped)
    {
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scoped);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IServiceScopeFactory)).Returns(factory);
        return provider;
    }

    [Theory]
    [MemberData(nameof(AllConsumers))]
    public async Task StopAsync_channelOpen_cancelsHandlersBeforeClosing(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = CreateHarness(name, Substitute.For<IServiceProvider>());

        bool? cancelledWhenClosing = null;
        harness.Channel.CloseAsync(default, default!, default, ct)
            .ReturnsForAnyArgs(_ =>
            {
                cancelledWhenClosing = harness.Worker.ExecuteTask!.IsCompleted;
                return Task.CompletedTask;
            });

        await harness.Worker.StartAsync(ct);
        await harness.Consumer.Task.WaitAsync(HangLimit, ct);

        await harness.Worker.StopAsync(ct);

        cancelledWhenClosing.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ConsumersWithBroadCatch))]
    public async Task StopAsync_handlerRunning_leavesMessageUnackedForRequeue(string name)
    {
        var ct = TestContext.Current.CancellationToken;

        using var handlerStarted = new ManualResetEventSlim();
        using var channelClosing = new ManualResetEventSlim();

        // Stands in for a handler that is mid-job and notices the cancellation once stopped.
        var scoped = Substitute.For<IServiceProvider>();
        scoped.GetService(Arg.Any<Type>()).Returns(_ =>
        {
            handlerStarted.Set();
            channelClosing.Wait(HangLimit);
            throw new OperationCanceledException();
        });

        var harness = CreateHarness(name, ProviderWithScopedServices(scoped));

        harness.Channel.CloseAsync(default, default!, default, ct)
            .ReturnsForAnyArgs(_ =>
            {
                channelClosing.Set();
                return Task.CompletedTask;
            });

        await harness.Worker.StartAsync(ct);
        var consumer = await harness.Consumer.Task.WaitAsync(HangLimit, ct);

        var delivery = Task.Run(() => consumer.HandleBasicDeliverAsync(
            "tag",
            1,
            false,
            "content-exchange",
            "media.mp4",
            Substitute.For<IReadOnlyBasicProperties>(),
            Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()),
            CancellationToken.None), ct);

        handlerStarted.Wait(HangLimit, ct).Should().BeTrue();

        await harness.Worker.StopAsync(ct);
        await delivery.WaitAsync(HangLimit, ct);

        await harness.Channel.DidNotReceiveWithAnyArgs().BasicAckAsync(default, default, ct);
        await harness.Channel.DidNotReceiveWithAnyArgs().BasicNackAsync(default, default, default, ct);
        harness.Logs().Should().NotContain(e => e.Level >= LogLevel.Warning);
        harness.Logs().Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("back to the queue"));
    }
}
