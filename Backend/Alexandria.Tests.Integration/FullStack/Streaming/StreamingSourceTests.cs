using System.Net;
using System.Net.Http.Json;
using Alexandria.Data.Context;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Streaming;

public class StreamingSourceTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/streaming/files";

    private static readonly DateTime SeedBase = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private async Task<List<StreamableFileSeed>> SeedAudioAsync(int count)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var seeds = new List<StreamableFileSeed>(count);
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < count; i++)
            seeds.Add(await StreamingSeedHelper.SeedStreamableFileAsync(
                db, UserId, $"track-{i}.mp3", SeedBase.AddMinutes(i), 120.0, ct: ct));
        return seeds;
    }

    private async Task<PaginatedResult<MediaFileDto>> GetPageAsync(string query)
    {
        var response = await Auth.GetAsync($"{Route}?{query}",
            cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PaginatedResult<MediaFileDto>>(
            cancellationToken: TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Library_anchor_returns_containing_page()
    {
        var seeds = await SeedAudioAsync(5);
        var ct = TestContext.Current.CancellationToken;

        // Library order is CreatedAt DESC: track-4, track-3, track-2, track-1, track-0.
        var page = await GetPageAsync(
            $"page=1&pageSize=2&isVideo=false&anchorFileId={seeds[2].FileId}");

        page.CurrentPage.Should().Be(2);
        page.TotalCount.Should().Be(5);
        page.Items.Select(i => i.FileId).Should()
            .Equal(seeds[2].FileId, seeds[1].FileId);
    }

    [Fact]
    public async Task Library_missing_anchor_returns_404()
    {
        await SeedAudioAsync(2);

        var response = await Auth.GetAsync(
            $"{Route}?page=1&pageSize=2&isVideo=false&anchorFileId={Guid.NewGuid()}",
            cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anchor_with_search_returns_400()
    {
        var seeds = await SeedAudioAsync(2);

        var response = await Auth.GetAsync(
            $"{Route}?page=1&pageSize=2&isVideo=false&query=track&anchorFileId={seeds[0].FileId}",
            cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anchor_item_without_playlist_returns_400()
    {
        await SeedAudioAsync(1);

        var response = await Auth.GetAsync(
            $"{Route}?page=1&pageSize=2&isVideo=false&anchorFileId={Guid.NewGuid()}&anchorPlaylistItemId={Guid.NewGuid()}",
            cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Playlist_pages_follow_item_order_and_keep_duplicates()
    {
        var seeds = await SeedAudioAsync(3);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, itemIds) = await StreamingSeedHelper.SeedPlaylistAsync(db, UserId, "mix",
            [seeds[0].JobId, seeds[1].JobId, seeds[0].JobId, seeds[2].JobId], ct);

        var first = await GetPageAsync($"playlistId={playlistId}&page=1&pageSize=2&isVideo=false");
        first.TotalCount.Should().Be(4);
        first.Items.Select(i => i.FileId).Should().Equal(seeds[0].FileId, seeds[1].FileId);

        var second = await GetPageAsync($"playlistId={playlistId}&page=2&pageSize=2&isVideo=false");
        second.Items.Select(i => i.FileId).Should().Equal(seeds[0].FileId, seeds[2].FileId);
        second.Items.Select(i => i.PlaylistItemId).Should().OnlyHaveUniqueItems();

        // File-only anchor resolves to the first eligible occurrence.
        var anchored = await GetPageAsync(
            $"playlistId={playlistId}&page=1&pageSize=2&isVideo=false&anchorFileId={seeds[0].FileId}");
        anchored.CurrentPage.Should().Be(1);
        anchored.Items.First().PlaylistItemId.Should().Be(itemIds[0]);

        // Exact item anchor locates its own page.
        var exact = await GetPageAsync(
            $"playlistId={playlistId}&page=1&pageSize=2&isVideo=false" +
            $"&anchorFileId={seeds[0].FileId}&anchorPlaylistItemId={itemIds[2]}");
        exact.CurrentPage.Should().Be(2);
        exact.Items.First().PlaylistItemId.Should().Be(itemIds[2]);
    }

    [Fact]
    public async Task Playlist_missing_anchor_and_foreign_playlist_return_404()
    {
        var seeds = await SeedAudioAsync(2);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, _) = await StreamingSeedHelper.SeedPlaylistAsync(db, UserId, "mix",
            [seeds[0].JobId, seeds[1].JobId], ct);

        var missing = await Auth.GetAsync(
            $"{Route}?playlistId={playlistId}&page=1&pageSize=2&isVideo=false&anchorFileId={Guid.NewGuid()}",
            cancellationToken: ct);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var foreign = await Auth.GetAsync(
            $"{Route}?playlistId={Guid.NewGuid()}&page=1&pageSize=2&isVideo=false",
            cancellationToken: ct);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}