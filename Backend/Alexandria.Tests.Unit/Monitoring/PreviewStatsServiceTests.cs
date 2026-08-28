using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.PreviewsStats;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class PreviewStatsServiceTests
{
    private readonly IPreviewRepository _repo = Substitute.For<IPreviewRepository>();
    private readonly PreviewStatsService _sut;

    public PreviewStatsServiceTests()
    {
        _sut = new PreviewStatsService(_repo);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static Preview Preview(PreviewKind kind, DateTime createdAt, long size = 100) =>
        new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            MimeType = "image/png",
            Size = size,
            CreatedAt = createdAt,
            VersionId = Guid.NewGuid()
        };

    [Fact]
    public async Task hourly_buckets_truncate_to_the_hour()
    {
        var windowFrom = Utc(2026, 8, 21, 10);
        var windowTo = Utc(2026, 8, 21, 12);

        var previews = new List<Preview>
        {
            Preview(PreviewKind.Thumbnail, Utc(2026, 8, 21, 10, 41)),
            Preview(PreviewKind.Preview, Utc(2026, 8, 21, 11, 3)),
        };
        _repo.GetCreatedBetweenAsync(windowFrom, windowTo, Arg.Any<CancellationToken>())
            .Returns(previews);

        var result = await _sut.GetVolumeAsync(windowFrom, windowTo, PreviewStatsBucket.Hour,
            TestContext.Current.CancellationToken);

        result.Points.Select(p => p.BucketStart).Should().Equal(
            Utc(2026, 8, 21, 10), Utc(2026, 8, 21, 11));
        result.Points[0].Thumbnails.Should().Be(1);
        result.Points[1].Previews.Should().Be(1);
    }

    [Fact]
    public async Task daily_buckets_truncate_to_utc_midnight_and_split_kinds()
    {
        var windowFrom = Utc(2026, 8, 20);
        var windowTo = Utc(2026, 8, 22);

        var previews = new List<Preview>
        {
            Preview(PreviewKind.Thumbnail, Utc(2026, 8, 20, 23)),
            Preview(PreviewKind.Thumbnail, Utc(2026, 8, 21, 2)),
            Preview(PreviewKind.Preview, Utc(2026, 8, 21, 9)),
        };
        _repo.GetCreatedBetweenAsync(windowFrom, windowTo, Arg.Any<CancellationToken>())
            .Returns(previews);

        var result = await _sut.GetVolumeAsync(windowFrom, windowTo, PreviewStatsBucket.Day,
            TestContext.Current.CancellationToken);

        result.Points.Should().HaveCount(2);
        result.Points[0].Thumbnails.Should().Be(1);
        result.Points[0].Previews.Should().Be(0);
        result.Points[1].Thumbnails.Should().Be(1);
        result.Points[1].Previews.Should().Be(1);
    }

    [Fact]
    public async Task overview_totals_count_and_sum_size_per_kind()
    {
        var totals = new List<PreviewKindTotals>
        {
            new(PreviewKind.Thumbnail, 12, 4096),
            new(PreviewKind.Preview, 3, 999_999),
        };
        _repo.GetKindTotalsAsync(Arg.Any<CancellationToken>()).Returns(totals);

        var result = await _sut.GetOverviewAsync(TestContext.Current.CancellationToken);

        result.ByKind.Should().BeSameAs(totals);
        result.ByKind[0].Count.Should().Be(12);
        result.ByKind[1].TotalSizeBytes.Should().Be(999_999);
    }

    [Fact]
    public async Task empty_window_yields_no_points()
    {
        _repo.GetCreatedBetweenAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetVolumeAsync(Utc(2026, 8, 21), Utc(2026, 8, 22),
            PreviewStatsBucket.Hour, TestContext.Current.CancellationToken);

        result.Points.Should().BeEmpty();
    }
}