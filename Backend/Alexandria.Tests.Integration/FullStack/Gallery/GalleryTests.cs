using System.Net;
using System.Net.Http.Json;
using Alexandria.Common;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Integration.FullStack.Gallery;

public class GalleryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string PhotosRoute = "/api/gallery/photos";
    private const string TimelineRoute = "/api/gallery/timeline";

    private static DateTime Utc(int year, int month, int day, int hour = 12, int minute = 0)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private Task<File> SeedPhotoAsync(DateTime uploadedAt, string mimeType = "image/jpeg", Guid? ownerId = null)
        => SeedFileWithVersionAsync(
            file => file.WithMimeType(mimeType).WithCreatedAt(uploadedAt).WithOwner(ownerId ?? UserId),
            version => version.WithMimeType(mimeType));

    private async Task CaptureAsync(Guid versionId, DateTime capturedUtc, short? offsetMinutes)
    {
        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.CaptureMetadata.UpsertAsync(new CaptureMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Width = 4000,
            Height = 3000,
            CapturedAt = capturedUtc,
            CapturedOffsetMinutes = offsetMinutes,
            RawJson = "{}",
            ExtractorVersion = 1,
        }, TestContext.Current.CancellationToken);
    }

    private async Task<File> SeedTakenPhotoAsync(DateTime capturedUtc, short? offsetMinutes = null)
    {
        var file = await SeedPhotoAsync(Utc(2026, 1, 1));
        await CaptureAsync(file.CurrentVersionId!.Value, capturedUtc, offsetMinutes);

        return file;
    }

    private async Task<Guid> SwitchToNewVersionAsync(File file, Action<FileVersionBuilder>? configure = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var content = new ContentObjectBuilder().Build();
        var builder = new FileVersionBuilder()
            .WithContentObject(content)
            .WithCreatedBy(UserId)
            .WithMimeType(file.MimeType)
            .WithVersionNumber(2)
            .WithFileId(file.Id);
        configure?.Invoke(builder);
        var version = builder.Build();

        db.ContentObjects.Add(content);
        db.FileVersions.Add(version);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await unitOfWork.Files.UpdateCurrentVersionAsync(file.Id, version.Id, TestContext.Current.CancellationToken);

        return version.Id;
    }

    private async Task<GalleryPageResponse> GetPageAsync(string query = "", HttpClient? client = null)
    {
        var response = await (client ?? Auth).GetAsync($"{PhotosRoute}{query}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GalleryPageResponse>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!;
    }

    private async Task<GalleryTimelineResponse> GetTimelineAsync(string query)
    {
        var response = await Auth.GetAsync($"{TimelineRoute}{query}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GalleryTimelineResponse>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!;
    }

    [Fact]
    public async Task Photos_TakenSort_OrdersByCameraWallClockAndSkipsNonPhotos()
    {
        var withExif = await SeedTakenPhotoAsync(Utc(2019, 6, 12, 15, 15), offsetMinutes: 180);
        var withoutExif = await SeedPhotoAsync(Utc(2024, 1, 5));
        await SeedFileWithVersionAsync(file => file.WithMimeType("text/plain"));
        await SeedPhotoAsync(Utc(2025, 1, 1), mimeType: "image/svg+xml");
        await SeedFileWithVersionAsync(file => file.WithMimeType("image/jpeg").WithDeletedAt(DateTime.UtcNow));

        var page = await GetPageAsync();

        page.Items.Select(p => p.FileId).Should().Equal(withoutExif.Id, withExif.Id);
        page.Items[1].TakenAt.Should().Be(Utc(2019, 6, 12, 18, 15));
        page.Items[1].Width.Should().Be(4000);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Photos_OtherUsersPhotos_AreNotVisible()
    {
        var (otherClient, otherId) = await CreateOtherUserAsync();
        await SeedPhotoAsync(Utc(2024, 1, 5), ownerId: otherId);

        var page = await GetPageAsync();
        var otherPage = await GetPageAsync(client: otherClient);

        page.Items.Should().BeEmpty();
        otherPage.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Photos_FollowingCursors_VisitsEveryPhotoOnceInOrder()
    {
        // Three photos share a timestamp, so page boundaries fall inside the burst.
        var burst = Utc(2022, 8, 20, 10, 0);
        var seeded = new List<File>
        {
            await SeedTakenPhotoAsync(burst),
            await SeedTakenPhotoAsync(burst),
            await SeedTakenPhotoAsync(burst),
            await SeedTakenPhotoAsync(Utc(2021, 3, 1)),
            await SeedTakenPhotoAsync(Utc(2023, 5, 9)),
        };

        var visited = new List<GalleryPhotoDto>();
        string? cursor = null;
        var requests = 0;

        do
        {
            var query = cursor is null ? "?limit=2" : $"?limit=2&cursor={cursor}";
            var page = await GetPageAsync(query);

            visited.AddRange(page.Items);
            cursor = page.NextCursor;
            requests++;
        } while (cursor is not null && requests < 10);

        requests.Should().Be(3);
        visited.Select(p => p.FileId).Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(seeded.Select(f => f.Id));
        visited.Select(p => p.TakenAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Photos_BeforeAndSince_ReturnOnlyThatMonth()
    {
        await SeedTakenPhotoAsync(Utc(2019, 5, 31, 23, 59));
        var june = await SeedTakenPhotoAsync(Utc(2019, 6, 15));
        await SeedTakenPhotoAsync(Utc(2019, 7, 1, 0, 0));

        var page = await GetPageAsync("?before=2019-07-01T00:00:00Z&since=2019-06-01T00:00:00Z");

        page.Items.Should().ContainSingle().Which.FileId.Should().Be(june.Id);
    }

    [Fact]
    public async Task Photos_UploadedSort_IgnoresCaptureDates()
    {
        var oldUploadNewPhoto = await SeedPhotoAsync(Utc(2020, 1, 1));
        await CaptureAsync(oldUploadNewPhoto.CurrentVersionId!.Value, Utc(2025, 1, 1), offsetMinutes: null);
        var recentUpload = await SeedPhotoAsync(Utc(2024, 1, 1));

        var page = await GetPageAsync($"?sort={(int)GallerySort.Uploaded}");

        page.Items.Select(p => p.FileId).Should().Equal(recentUpload.Id, oldUploadNewPhoto.Id);
    }

    [Fact]
    public async Task Photos_NewVersionWithoutExif_KeepsOriginalCaptureDate()
    {
        var file = await SeedTakenPhotoAsync(Utc(2019, 6, 12, 15, 15), offsetMinutes: 180);

        await SwitchToNewVersionAsync(file);
        var page = await GetPageAsync();

        page.Items.Should().ContainSingle().Which.TakenAt.Should().Be(Utc(2019, 6, 12, 18, 15));
    }

    [Fact]
    public async Task Photos_EncryptedCurrentVersion_LeavesGallery()
    {
        var file = await SeedTakenPhotoAsync(Utc(2019, 6, 12));

        await SwitchToNewVersionAsync(file, version => version
            .WithIsEncrypted(true)
            .WithEncryptionIv(new byte[12])
            .WithEncryptionSalt(new byte[16])
            .WithIntegrityTag(new byte[16]));
        var page = await GetPageAsync();

        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Photos_MetadataArrivingLater_MovesPhotoToCaptureDate()
    {
        var file = await SeedPhotoAsync(Utc(2026, 1, 1));
        var before = await GetPageAsync();

        await CaptureAsync(file.CurrentVersionId!.Value, Utc(2018, 4, 2, 8, 0), offsetMinutes: 60);
        var after = await GetPageAsync();

        before.Items.Should().ContainSingle().Which.TakenAt.Should().Be(Utc(2026, 1, 1));
        after.Items.Should().ContainSingle().Which.TakenAt.Should().Be(Utc(2018, 4, 2, 9, 0));
    }

    [Fact]
    public async Task Timeline_TakenSort_CountsPerMonthNewestFirst()
    {
        await SeedTakenPhotoAsync(Utc(2019, 6, 1));
        await SeedTakenPhotoAsync(Utc(2019, 6, 20));
        await SeedTakenPhotoAsync(Utc(2023, 2, 14));

        var timeline = await GetTimelineAsync("?sort=0");

        timeline.Total.Should().Be(3);
        timeline.Months.Select(m => (m.Year, m.Month, m.Count)).Should().Equal((2023, 2, 1), (2019, 6, 2));
    }

    [Fact]
    public async Task Timeline_UploadedSort_BucketsInViewerZone()
    {
        await SeedPhotoAsync(Utc(2024, 1, 31, 23, 30));

        var utc = await GetTimelineAsync($"?sort={(int)GallerySort.Uploaded}&timeZone=UTC");
        var sofia = await GetTimelineAsync($"?sort={(int)GallerySort.Uploaded}&timeZone=Europe/Sofia");

        utc.Months.Should().ContainSingle().Which.Month.Should().Be(1);
        sofia.Months.Should().ContainSingle().Which.Month.Should().Be(2);
    }

    [Theory]
    [InlineData(PhotosRoute + "?cursor=garbage")]
    [InlineData(PhotosRoute + "?limit=0")]
    [InlineData(PhotosRoute + "?limit=501")]
    [InlineData(PhotosRoute + "?since=2020-01-01T00:00:00Z&before=2019-01-01T00:00:00Z")]
    [InlineData(TimelineRoute + "?timeZone=Pacific%20Standard%20Time")]
    [InlineData(TimelineRoute + "?timeZone=Mars/Olympus")]
    public async Task InvalidRequest_Returns400(string route)
    {
        var response = await Auth.GetAsync(route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Photos_CursorFromOtherSort_Returns400()
    {
        await SeedTakenPhotoAsync(Utc(2019, 6, 1));
        await SeedTakenPhotoAsync(Utc(2019, 6, 2));
        var takenPage = await GetPageAsync("?limit=1");

        var response = await Auth.GetAsync(
            $"{PhotosRoute}?sort={(int)GallerySort.Uploaded}&cursor={takenPage.NextCursor}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Photos_CursorAndBeforeTogether_Returns400()
    {
        await SeedTakenPhotoAsync(Utc(2019, 6, 1));
        await SeedTakenPhotoAsync(Utc(2019, 6, 2));
        var page = await GetPageAsync("?limit=1");

        var response = await Auth.GetAsync(
            $"{PhotosRoute}?cursor={page.NextCursor}&before=2019-01-01T00:00:00Z",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}