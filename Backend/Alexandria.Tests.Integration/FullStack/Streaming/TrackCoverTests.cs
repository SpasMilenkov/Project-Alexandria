using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Alexandria.Dto.Files.Streaming.Shuffle;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using Amazon.S3;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Streaming;

public class TrackCoverTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record UploadResponse(DateTime CoverUpdatedAt);

    private async Task<StreamableFileSeed> SeedTrackAsync(bool isVideo = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var seed = await StreamingSeedHelper.SeedStreamableFileAsync(
            db, UserId, "track.mp3", DateTime.UtcNow, 120.0, isVideo, Ct);

        var contentObject = await db.FileVersions
            .Where(v => v.Id == seed.VersionId)
            .Select(v => v.ContentObject)
            .SingleAsync(Ct);
        contentObject.IsPromoted = true;
        await db.SaveChangesAsync(Ct);

        return seed;
    }

    private static MultipartFormDataContent ImageForm(byte[] image)
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", "cover.bin");

        return form;
    }

    private Task<HttpResponseMessage> PostCoverAsync(StreamableFileSeed seed, HttpClient? client = null, byte[]? image = null) =>
        (client ?? Auth).PostAsync(
            $"/api/files/{seed.FileId}/versions/{seed.VersionId}/cover", ImageForm(image ?? Png), Ct);

    private async Task<DateTime> UploadCoverAsync(StreamableFileSeed seed)
    {
        var response = await PostCoverAsync(seed);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UploadResponse>(Ct);

        return body!.CoverUpdatedAt;
    }

    private HttpClient NonRedirectingClient()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = Auth.DefaultRequestHeaders.Authorization;

        return client;
    }

    private async Task<HttpResponseMessage> PreviewCheckAsync(string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/preview-check");
        request.Headers.Add("X-Original-URI", uri);

        return await Auth.SendAsync(request, Ct);
    }

    private async Task<string> ServedThumbnailPathAsync(StreamableFileSeed seed)
    {
        var response = await PreviewCheckAsync($"/preview/{seed.VersionId}/thumbnail");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return response.Headers.GetValues("X-Content-Path").Single();
    }

    private async Task<bool> ObjectExistsAsync(string key)
    {
        using var scope = Factory.Services.CreateScope();
        var s3 = scope.ServiceProvider.GetRequiredService<IAmazonS3>();
        var bucket = scope.ServiceProvider.GetRequiredService<IOptions<S3Config>>().Value.PreviewBucket;

        try
        {
            await s3.GetObjectMetadataAsync(bucket, key, Ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    [Fact]
    public async Task Upload_exposes_the_stamp_through_every_listing_projection()
    {
        var seed = await SeedTrackAsync();
        var stamp = await UploadCoverAsync(seed);

        var single = await Auth.GetFromJsonAsync<MediaFileDto>($"/api/streaming/files/{seed.FileId}", Ct);
        single!.CoverUpdatedAt.Should().BeCloseTo(stamp, TimeSpan.FromMilliseconds(1));

        var library = await Auth.GetFromJsonAsync<PaginatedResult<MediaFileDto>>(
            "/api/streaming/files?page=1&pageSize=10&isVideo=false", Ct);
        library!.Items.Single().CoverUpdatedAt.Should().BeCloseTo(stamp, TimeSpan.FromMilliseconds(1));

        Guid playlistId;
        List<Guid> itemIds;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
            (playlistId, itemIds) = await StreamingSeedHelper.SeedPlaylistAsync(
                db, UserId, "mix", [seed.JobId], Ct);
        }

        var playlist = await Auth.GetFromJsonAsync<PaginatedResult<MediaFileDto>>(
            $"/api/streaming/files?playlistId={playlistId}&page=1&pageSize=10&isVideo=false", Ct);
        playlist!.Items.Single().CoverUpdatedAt.Should().BeCloseTo(stamp, TimeSpan.FromMilliseconds(1));

        using (var scope = Factory.Services.CreateScope())
        {
            var files = scope.ServiceProvider.GetRequiredService<IFileRepository>();

            var libraryEntries = await files.GetPlaybackEntriesAsync(
                UserId,
                new PlaybackSourceDto { IsVideo = false },
                [new PlaybackSourceEntryRef { FileId = seed.FileId }],
                Ct);
            libraryEntries.Single().CoverUpdatedAt.Should().BeCloseTo(stamp, TimeSpan.FromMilliseconds(1));

            var playlistEntries = await files.GetPlaybackEntriesAsync(
                UserId,
                new PlaybackSourceDto { IsVideo = false, PlaylistId = playlistId },
                [new PlaybackSourceEntryRef { FileId = seed.FileId, PlaylistItemId = itemIds[0] }],
                Ct);
            playlistEntries.Single().CoverUpdatedAt.Should().BeCloseTo(stamp, TimeSpan.FromMilliseconds(1));
        }

        using var fileResult = JsonDocument.Parse(await Auth.GetStringAsync($"/api/files/{seed.FileId}", Ct));
        fileResult.RootElement.GetProperty("currentVersion").GetProperty("coverUpdatedAt").GetDateTime()
            .ToUniversalTime().Should().BeCloseTo(stamp, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Automatic_cover_has_no_stamp_anywhere()
    {
        var seed = await SeedTrackAsync();

        var single = await Auth.GetFromJsonAsync<MediaFileDto>($"/api/streaming/files/{seed.FileId}", Ct);
        single!.CoverUpdatedAt.Should().BeNull();

        var library = await Auth.GetFromJsonAsync<PaginatedResult<MediaFileDto>>(
            "/api/streaming/files?page=1&pageSize=10&isVideo=false", Ct);
        library!.Items.Single().CoverUpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Manual_cover_redirects_carry_the_cache_buster()
    {
        var seed = await SeedTrackAsync();
        await UploadCoverAsync(seed);
        using var client = NonRedirectingClient();
        var versionUrl = $"/api/files/{seed.FileId}/versions/{seed.VersionId}/thumbnail";

        var stamped = await client.GetAsync($"{versionUrl}?v=client-stamp", Ct);
        stamped.StatusCode.Should().Be(HttpStatusCode.Redirect);
        stamped.Headers.Location!.ToString().Should().StartWith($"/preview/{seed.VersionId}/thumbnail?v=");
        stamped.Headers.CacheControl!.ToString().Should().Contain("immutable");

        // Stale client data must not pin this cover under the plain URL.
        var unstamped = await client.GetAsync(versionUrl, Ct);
        unstamped.Headers.Location!.ToString().Should().StartWith($"/preview/{seed.VersionId}/thumbnail?v=");
        unstamped.Headers.CacheControl!.NoStore.Should().BeTrue();

        var fileOnly = await client.GetAsync($"/api/files/{seed.FileId}/thumbnail", Ct);
        fileOnly.Headers.Location!.ToString().Should().StartWith($"{versionUrl}?v=");
        fileOnly.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_check_serves_the_manual_key_and_accepts_the_cache_buster()
    {
        var seed = await SeedTrackAsync();
        await UploadCoverAsync(seed);

        var response = await PreviewCheckAsync($"/preview/{seed.VersionId}/thumbnail?v=123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Path").Single()
            .Should().StartWith($"track-covers/{seed.VersionId}/");
    }

    [Fact]
    public async Task Preview_check_keeps_the_shared_hash_path_for_automatic_covers()
    {
        var seed = await SeedTrackAsync();

        var path = await ServedThumbnailPathAsync(seed);

        path.Should().StartWith("thumbnails/");
    }

    [Fact]
    public async Task Replacing_a_cover_rotates_the_key_and_removes_the_old_object()
    {
        var seed = await SeedTrackAsync();
        await UploadCoverAsync(seed);
        var firstKey = await ServedThumbnailPathAsync(seed);
        (await ObjectExistsAsync(firstKey)).Should().BeTrue();

        await UploadCoverAsync(seed);
        var secondKey = await ServedThumbnailPathAsync(seed);

        secondKey.Should().NotBe(firstKey);
        (await ObjectExistsAsync(secondKey)).Should().BeTrue();
        (await ObjectExistsAsync(firstKey)).Should().BeFalse();
    }

    [Fact]
    public async Task Removing_a_cover_falls_back_to_the_automatic_path()
    {
        var seed = await SeedTrackAsync();
        await UploadCoverAsync(seed);
        var key = await ServedThumbnailPathAsync(seed);

        var response = await Auth.DeleteAsync($"/api/files/{seed.FileId}/versions/{seed.VersionId}/cover", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ServedThumbnailPathAsync(seed)).Should().StartWith("thumbnails/");
        (await ObjectExistsAsync(key)).Should().BeFalse();

        var single = await Auth.GetFromJsonAsync<MediaFileDto>($"/api/streaming/files/{seed.FileId}", Ct);
        single!.CoverUpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Generation_run_does_not_overwrite_a_manual_cover()
    {
        var seed = await SeedTrackAsync();
        var stamp = await UploadCoverAsync(seed);
        var key = await ServedThumbnailPathAsync(seed);

        using (var scope = Factory.Services.CreateScope())
        {
            var artifacts = scope.ServiceProvider.GetRequiredService<IPreviewArtifactService>();
            await artifacts.UploadMediaData(
                new MemoryStream([1, 2, 3]),
                new MemoryStream([4, 5, 6]),
                3,
                3,
                "generated-hash",
                seed.VersionId,
                new MediaMetadataDto { Duration = 120 },
                Ct);
        }

        (await ServedThumbnailPathAsync(seed)).Should().Be(key);

        await using var db = Fixture.CreateDbContext();
        var thumbnail = await db.Previews.SingleAsync(
            p => p.VersionId == seed.VersionId && p.Kind == PreviewKind.Thumbnail, Ct);
        thumbnail.IsManual.Should().BeTrue();
        thumbnail.ObjectKey.Should().Be(key);
        thumbnail.UpdatedAt.Should().BeCloseTo(stamp, TimeSpan.FromMilliseconds(1));

        var preview = await db.Previews.SingleAsync(
            p => p.VersionId == seed.VersionId && p.Kind == PreviewKind.Preview, Ct);
        preview.ObjectKey.Should().Be("previews/generated-hash");
    }

    [Fact]
    public async Task Another_users_version_is_not_found_and_stores_nothing()
    {
        var seed = await SeedTrackAsync();
        var (otherClient, _) = await CreateOtherUserAsync();
        using var other = otherClient;

        var response = await PostCoverAsync(seed, other);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var db = Fixture.CreateDbContext();
        (await db.Previews.AnyAsync(p => p.VersionId == seed.VersionId, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Video_file_is_rejected()
    {
        var seed = await SeedTrackAsync(isVideo: true);

        var response = await PostCoverAsync(seed);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_image_upload_is_rejected()
    {
        var seed = await SeedTrackAsync();

        var response = await PostCoverAsync(seed, image: "<svg></svg>"u8.ToArray());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Oversize_upload_is_rejected()
    {
        var seed = await SeedTrackAsync();
        var oversize = new byte[4 * 1024 * 1024 + 1];
        Png.CopyTo(oversize, 0);

        var response = await PostCoverAsync(seed, image: oversize);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
