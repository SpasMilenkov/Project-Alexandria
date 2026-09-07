using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.PreviewsStats;
using Alexandria.Dto.TranspilationStats;
using Alexandria.Services.Monitoring;
using Alexandria.Tests.Common.Builders;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class PreviewStatsServiceTests
{
    private readonly IPreviewRepository _repo = Substitute.For<IPreviewRepository>();
    private readonly IJobRepository _jobRepo = Substitute.For<IJobRepository>();
    private readonly PreviewStatsService _sut;

    public PreviewStatsServiceTests()
    {
        _sut = new PreviewStatsService(_repo, _jobRepo);
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
            VersionId = Guid.NewGuid(),
            ObjectKey = kind == PreviewKind.Thumbnail ? "thumbnails/abc" : "previews/abc"
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

    [Fact]
    public async Task job_overview_combines_both_types_and_computes_duration()
    {
        _jobRepo.GetStatusCountsAsync(JobType.MediaPreview, Arg.Any<CancellationToken>())
            .Returns(new List<JobStatusCount> { new(JobStatus.Queued, 2), new(JobStatus.Failed, 1) });
        _jobRepo.GetStatusCountsAsync(JobType.DocumentPreview, Arg.Any<CancellationToken>())
            .Returns(new List<JobStatusCount> { new(JobStatus.Queued, 1), new(JobStatus.Ready, 1) });
        var start = Utc(2026, 8, 21, 9);
        _jobRepo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Job>
            {
                new JobBuilder().WithStatus(JobStatus.Ready).WithType(JobType.MediaPreview)
                    .WithStartedAt(start).WithCompletedAt(start.AddMinutes(30)).Build(),
                new JobBuilder().WithStatus(JobStatus.Failed).WithType(JobType.DocumentPreview)
                    .WithStartedAt(start).WithCompletedAt(start.AddMinutes(10)).Build()
            });

        var result = await _sut.GetJobOverviewAsync(null, 30, TestContext.Current.CancellationToken);

        result.StatusCounts.Should().BeEquivalentTo(
            new List<JobStatusCount>
            {
                new(JobStatus.Queued, 3),
                new(JobStatus.Failed, 1),
                new(JobStatus.Ready, 1)
            });
        result.Duration.Should().NotBeNull();
        result.Duration!.SampleCount.Should().Be(2);
        result.Duration.AvgMinutes.Should().Be(20);
    }

    [Fact]
    public async Task job_overview_type_filter_queries_single_type_only()
    {
        _jobRepo.GetStatusCountsAsync(JobType.MediaPreview, Arg.Any<CancellationToken>())
            .Returns(new List<JobStatusCount> { new(JobStatus.Queued, 2) });
        var start = Utc(2026, 8, 21, 9);
        _jobRepo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Job>
            {
                new JobBuilder().WithStatus(JobStatus.Ready).WithType(JobType.MediaPreview)
                    .WithStartedAt(start).WithCompletedAt(start.AddMinutes(30)).Build(),
                new JobBuilder().WithStatus(JobStatus.Ready).WithType(JobType.Transpilation)
                    .WithStartedAt(start).WithCompletedAt(start.AddMinutes(30)).Build()
            });

        var result = await _sut.GetJobOverviewAsync(
            JobType.MediaPreview, 30, TestContext.Current.CancellationToken);

        await _jobRepo.DidNotReceive().GetStatusCountsAsync(
            JobType.DocumentPreview, Arg.Any<CancellationToken>());
        result.StatusCounts.Should().BeEquivalentTo(
            new List<JobStatusCount> { new(JobStatus.Queued, 2) });
        result.Duration!.SampleCount.Should().Be(1);
    }

    [Fact]
    public async Task job_trend_buckets_failure_rate_and_volume()
    {
        _jobRepo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Job>
            {
                new JobBuilder().WithStatus(JobStatus.Ready).WithType(JobType.MediaPreview)
                    .WithCreatedAt(Utc(2026, 8, 21, 11, 5))
                    .WithStartedAt(Utc(2026, 8, 21, 11, 5))
                    .WithCompletedAt(Utc(2026, 8, 21, 11, 37)).Build(),
                new JobBuilder().WithStatus(JobStatus.Queued).WithType(JobType.MediaPreview)
                    .WithCreatedAt(Utc(2026, 8, 21, 12, 52)).Build(),
                new JobBuilder().WithStatus(JobStatus.Failed).WithType(JobType.DocumentPreview)
                    .WithCreatedAt(Utc(2026, 8, 21, 11, 10))
                    .WithStartedAt(Utc(2026, 8, 21, 11, 10))
                    .WithCompletedAt(Utc(2026, 8, 21, 11, 30)).Build()
            });

        var result = await _sut.GetJobTrendAsync(null, Utc(2026, 8, 21, 10), Utc(2026, 8, 21, 13),
            PreviewStatsBucket.Hour, TestContext.Current.CancellationToken);

        result.FailureRate.Select(p => p.BucketStart).Should().Equal(Utc(2026, 8, 21, 11));
        result.FailureRate.Single().Total.Should().Be(2);
        result.FailureRate.Single().Failed.Should().Be(1);
        result.Volume.Select(p => p.BucketStart).Should().Equal(Utc(2026, 8, 21, 11), Utc(2026, 8, 21, 12));
        result.Volume[^1].Count.Should().Be(1);
    }

    [Fact]
    public async Task job_overview_empty_window_yields_null_duration()
    {
        _jobRepo.GetStatusCountsAsync(Arg.Any<JobType?>(), Arg.Any<CancellationToken>())
            .Returns(new List<JobStatusCount>());
        _jobRepo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<Job>());

        var result = await _sut.GetJobOverviewAsync(null, 30, TestContext.Current.CancellationToken);

        result.StatusCounts.Should().BeEmpty();
        result.Duration.Should().BeNull();
    }
}