using Alexandria.Common.Exceptions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Gallery;
using Alexandria.Services.Storage.Gallery;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Gallery;

public class GalleryServiceTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    private static readonly GalleryCursor LastOnPage = new()
    {
        SortDate = new DateTime(2024, 3, 1, 9, 30, 0, DateTimeKind.Utc),
        FileId = Guid.NewGuid(),
    };

    private static GalleryPhotoRow Row(DateTime date) => new()
    {
        FileId = Guid.NewGuid(),
        VersionId = Guid.NewGuid(),
        Name = "IMG_0001.jpg",
        MimeType = "image/jpeg",
        SortDate = date,
        TakenAt = date,
        UploadedAt = date.AddDays(3),
        Width = 4000,
        Height = 3000,
    };

    private static (GalleryService Service, IGalleryRepository Repository) CreateSut(GalleryPage page)
    {
        var repository = Substitute.For<IGalleryRepository>();
        repository.GetPageAsync(Arg.Any<GalleryPageQuery>(), Arg.Any<CancellationToken>()).Returns(page);

        return (new GalleryService(repository, NullLogger<GalleryService>.Instance), repository);
    }

    [Fact]
    public async Task GetPageAsync_WithCursor_PassesDecodedKeyAndOwner()
    {
        var (service, repository) = CreateSut(new GalleryPage { Items = [], Next = null });
        var filter = new GalleryPageFilter
        {
            Sort = GallerySort.Uploaded,
            Cursor = GalleryCursorCodec.Encode(LastOnPage, GallerySort.Uploaded),
            Limit = 50,
        };

        await service.GetPageAsync(Owner, filter, TestContext.Current.CancellationToken);

        await repository.Received(1).GetPageAsync(
            Arg.Is<GalleryPageQuery>(q => q.OwnerId == Owner
                                          && q.Sort == GallerySort.Uploaded
                                          && q.Cursor == LastOnPage
                                          && q.Limit == 50),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetPageAsync_MorePhotosExist_ReturnsCursorThatDecodesToRepositoryKey()
    {
        var row = Row(LastOnPage.SortDate);
        var (service, _) = CreateSut(new GalleryPage { Items = [row], Next = LastOnPage });

        var response = await service.GetPageAsync(
            Owner,
            new GalleryPageFilter { Sort = GallerySort.Taken, Limit = 1 },
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.FileId.Should().Be(row.FileId);
        GalleryCursorCodec.TryDecode(response.NextCursor, GallerySort.Taken, out var next).Should().BeTrue();
        next.Should().Be(LastOnPage);
    }

    [Fact]
    public async Task GetPageAsync_LastPage_HasNoCursor()
    {
        var (service, _) = CreateSut(new GalleryPage { Items = [Row(LastOnPage.SortDate)], Next = null });

        var response = await service.GetPageAsync(
            Owner,
            new GalleryPageFilter { Sort = GallerySort.Taken, Limit = 10 },
            TestContext.Current.CancellationToken);

        response.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetPageAsync_CursorFromOtherSort_ThrowsWithoutQuerying()
    {
        var (service, repository) = CreateSut(new GalleryPage { Items = [], Next = null });
        var filter = new GalleryPageFilter
        {
            Sort = GallerySort.Taken,
            Cursor = GalleryCursorCodec.Encode(LastOnPage, GallerySort.Uploaded),
            Limit = 10,
        };

        var act = () => service.GetPageAsync(Owner, filter, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidGalleryCursorException>();
        await repository.DidNotReceiveWithAnyArgs().GetPageAsync(default!, default);
    }

    [Fact]
    public async Task GetTimelineAsync_Months_AreSummedIntoTotal()
    {
        var repository = Substitute.For<IGalleryRepository>();
        repository.GetMonthBucketsAsync(Owner, GallerySort.Taken, "UTC", TestContext.Current.CancellationToken)
            .Returns([
                new GalleryMonthBucketRow { Year = 2024, Month = 3, Count = 12 },
                new GalleryMonthBucketRow { Year = 2019, Month = 6, Count = 30 },
            ]);
        var service = new GalleryService(repository, NullLogger<GalleryService>.Instance);

        var timeline = await service.GetTimelineAsync(Owner, GallerySort.Taken, "UTC",
            TestContext.Current.CancellationToken);

        timeline.Total.Should().Be(42);
        timeline.Months.Select(m => (m.Year, m.Month, m.Count))
            .Should().Equal((2024, 3, 12), (2019, 6, 30));
    }
}