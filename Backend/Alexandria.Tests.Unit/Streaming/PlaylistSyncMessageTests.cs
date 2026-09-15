using Alexandria.Common.Playlists;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class PlaylistSyncMessageTests
{
    private static readonly Guid FileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void TryParse_valid_request_returns_ids()
    {
        PlaylistSyncMessage.TryParse(PlaylistSyncMessage.Encode(FileId, OwnerId), out var fileId, out var ownerId)
            .Should().BeTrue();
        fileId.Should().Be(FileId);
        ownerId.Should().Be(OwnerId);
    }

    [Fact]
    public void TryParse_garbage_returns_false()
    {
        PlaylistSyncMessage.TryParse("not-a-request"u8.ToArray(), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_empty_body_returns_false()
    {
        PlaylistSyncMessage.TryParse([], out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_empty_object_returns_false()
    {
        PlaylistSyncMessage.TryParse("{}"u8.ToArray(), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Encode_round_trips_through_parse()
    {
        var fileId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        PlaylistSyncMessage.TryParse(PlaylistSyncMessage.Encode(fileId, ownerId), out var parsedFile,
                out var parsedOwner)
            .Should().BeTrue();
        parsedFile.Should().Be(fileId);
        parsedOwner.Should().Be(ownerId);
    }
}