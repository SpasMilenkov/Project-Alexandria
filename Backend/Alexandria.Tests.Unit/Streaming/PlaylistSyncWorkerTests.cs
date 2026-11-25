using Alexandria.Common.Playlists;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using Alexandria.Infrastructure.Messaging;
using Alexandria.Tests.Unit.Common;
using Alexandria.Workers.Playlist.Config;
using Alexandria.Workers.Playlist.Queueing;
using Alexandria.Workers.Playlist.Workers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Alexandria.Tests.Unit.Streaming;

public class PlaylistSyncWorkerTests
{
    private static readonly TimeSpan HangLimit = TimeSpan.FromSeconds(10);
    private static readonly Guid Owner = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly IChannel _channel = Substitute.For<IChannel>();
    private readonly IAutoPlaylistSyncService _sync = Substitute.For<IAutoPlaylistSyncService>();
    private readonly RecordingLogger<PlaylistSyncWorker> _logger = new();
    private readonly SemaphoreSlim _synced = new(0);

    private readonly TaskCompletionSource<IAsyncBasicConsumer> _consumer =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private PlaylistSyncWorker CreateSut()
    {
        _channel.BasicConsumeAsync(default!, default, default!, default, default, default, default!, default)
            .ReturnsForAnyArgs(call =>
            {
                _consumer.TrySetResult(call.ArgAt<IAsyncBasicConsumer>(6));
                return Task.FromResult("tag");
            });

        _sync.SyncFilesAsync(Guid.Empty, default!, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs(_ =>
            {
                _synced.Release();
                return Task.FromResult(new AutoPlaylistSyncResult(1, 1, 0, 0));
            });

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>()).Returns(_channel);

        var broker = new MessageBrokerConnection(isEnabled: true);
        broker.SetConnection(connection);

        var scoped = Substitute.For<IServiceProvider>();
        scoped.GetService(typeof(IAutoPlaylistSyncService)).Returns(_sync);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scoped);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IServiceScopeFactory)).Returns(factory);

        var config = Options.Create(new PlaylistConsumerConfig { MaxBatchSize = 1, CoalesceWindowMilliseconds = 0 });

        return new PlaylistSyncWorker(_logger, broker, provider, config, new PlaylistSyncBuffer());
    }

    private static Task DeliverAsync(IAsyncBasicConsumer consumer, ulong deliveryTag) =>
        consumer.HandleBasicDeliverAsync(
            "tag",
            deliveryTag,
            false,
            "content-exchange",
            "playlist.sync.file",
            Substitute.For<IReadOnlyBasicProperties>(),
            PlaylistSyncMessage.Encode(Guid.NewGuid(), Owner),
            CancellationToken.None);

    [Fact]
    public async Task closed_channel_on_ack_keeps_the_worker_running_and_is_not_a_failed_sync()
    {
        var ct = TestContext.Current.CancellationToken;

        _channel.BasicAckAsync(default, default, ct)
            .ReturnsForAnyArgs(_ => ValueTask.FromException(
                new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"))));

        var worker = CreateSut();
        await worker.StartAsync(ct);
        var consumer = await _consumer.Task.WaitAsync(HangLimit, ct);

        await DeliverAsync(consumer, 1);
        (await _synced.WaitAsync(HangLimit, ct)).Should().BeTrue();

        // A second delivery only syncs if the drain loop survived the lost ack.
        await DeliverAsync(consumer, 2);
        (await _synced.WaitAsync(HangLimit, ct)).Should().BeTrue();

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        await _channel.DidNotReceiveWithAnyArgs().BasicNackAsync(default, default, default, ct);
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);

        await worker.StopAsync(ct);
    }
}
