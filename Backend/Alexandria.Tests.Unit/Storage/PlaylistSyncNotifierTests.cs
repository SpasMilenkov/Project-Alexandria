using Alexandria.Common.Playlists;
using Alexandria.Common.Services;
using Alexandria.Services.Storage.Playlists;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Storage;

public class PlaylistSyncNotifierTests
{
    private static readonly Guid FileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task PublishFileChangedAsync_publishes_file_scoped_message()
    {
        var publisher = Substitute.For<IPublisherService>();

        byte[]? published = null;
        publisher.When(p => p.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>()))
            .Do(call => published = call.Arg<byte[]>());

        await PlaylistSyncNotifier.PublishFileChangedAsync(
            publisher, FileId, OwnerId, NullLogger.Instance);

        await publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), PlaylistSyncMessage.SyncRoutingKey);
        published.Should().NotBeNull();
        PlaylistSyncMessage.TryParse(published!, out var fileId, out var ownerId)
            .Should().BeTrue();
        fileId.Should().Be(FileId);
        ownerId.Should().Be(OwnerId);
    }

    [Fact]
    public async Task PublishFileChangedAsync_broker_failure_does_not_throw()
    {
        var publisher = Substitute.For<IPublisherService>();
        publisher.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        var act = () => PlaylistSyncNotifier.PublishFileChangedAsync(
            publisher, FileId, OwnerId, NullLogger.Instance);

        await act.Should().NotThrowAsync();
    }
}