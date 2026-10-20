using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Dto.Files.Streaming.Playlist;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PlaylistService = Alexandria.Services.Streaming.Playlists.PlaylistService;

namespace Alexandria.Tests.Unit.Streaming;

public class PlaylistServiceTests
{
    private static readonly Guid Owner = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid JobId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();

    private PlaylistService CreateSut() => new(
        _playlists, NullLogger<PlaylistService>.Instance);

    [Fact]
    public async Task AddItemAsync_persists_item_pinned_to_the_user()
    {
        _playlists.IsOwnerAsync(PlaylistId, Owner, Arg.Any<CancellationToken>())
            .Returns(true);
        _playlists.AddItemAsync(Arg.Any<PlaylistItem>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new PlaylistItemDto
            {
                Id = call.Arg<PlaylistItem>().Id,
                Position = 0,
                TranspilationJobId = JobId,
            }));

        var result = await CreateSut().AddItemAsync(
            PlaylistId, JobId, Owner, TestContext.Current.CancellationToken);

        result.TranspilationJobId.Should().Be(JobId);
        await _playlists.Received(1).AddItemAsync(
            Arg.Is<PlaylistItem>(i =>
                i.AddedByUser
                && i.PlaylistId == PlaylistId
                && i.TranspilationJobId == JobId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItemAsync_access_denied_does_not_add()
    {
        _playlists.IsOwnerAsync(PlaylistId, Owner, Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => CreateSut().AddItemAsync(
            PlaylistId, JobId, Owner, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<PlaylistAccessDeniedException>();
        await _playlists.DidNotReceive().AddItemAsync(
            Arg.Any<PlaylistItem>(), Arg.Any<CancellationToken>());
    }
}