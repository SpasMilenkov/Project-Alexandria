using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.TranspilationStats;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class TranspilationStatsServiceTests
{
    private readonly ITranspilationJobRepository _repo = Substitute.For<ITranspilationJobRepository>();
    private readonly TranspilationStatsService _sut;

    public TranspilationStatsServiceTests()
    {
        _sut = new TranspilationStatsService(_repo);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static TranspilationJob Job(
        TranspilationStatus status,
        DateTime createdAt,
        DateTime? startedAt = null,
        DateTime? completedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Status = status,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            UserId = Guid.NewGuid()
        };

    [Fact]
    public async Task hourly_buckets_truncate_to_the_hour()
    {
        var windowFrom = Utc(2026, 8, 21, 10);
        var windowTo = Utc(2026, 8, 21, 13);

        var jobs = new List<TranspilationJob>
        {
            // Terminal job completing at :37 → bucket 12:00
            Job(TranspilationStatus.Ready, Utc(2026, 8, 21, 12, 5),
                startedAt: Utc(2026, 8, 21, 12, 10), completedAt: Utc(2026, 8, 21, 12, 37)),
            // Volume-only job created at :52 → bucket 12:00 as well
            Job(TranspilationStatus.Queued, Utc(2026, 8, 21, 12, 52)),
            // Terminal job in a different hour → separate bucket
            Job(TranspilationStatus.Failed, Utc(2026, 8, 21, 11),
                startedAt: Utc(2026, 8, 21, 11), completedAt: Utc(2026, 8, 21, 11, 30)),
        };
        _repo.GetJobsTouchingWindowAsync(windowFrom, windowTo, Arg.Any<CancellationToken>())
            .Returns(jobs);

        var result = await _sut.GetTrendAsync(windowFrom, windowTo, StatsBucket.Hour,
            TestContext.Current.CancellationToken);

        result.FailureRate.Select(p => p.BucketStart).Should().Equal(
            Utc(2026, 8, 21, 11), Utc(2026, 8, 21, 12));
        result.Volume.Select(p => p.BucketStart).Should().Equal(
            Utc(2026, 8, 21, 11), Utc(2026, 8, 21, 12));
        result.Volume[^1].Count.Should().Be(2);
    }

    [Fact]
    public async Task daily_buckets_truncate_to_utc_midnight()
    {
        var windowFrom = Utc(2026, 8, 20);
        var windowTo = Utc(2026, 8, 22);

        var jobs = new List<TranspilationJob>
        {
            Job(TranspilationStatus.Ready, Utc(2026, 8, 20, 23),
                startedAt: Utc(2026, 8, 20, 23), completedAt: Utc(2026, 8, 21, 1)),
            Job(TranspilationStatus.Failed, Utc(2026, 8, 21, 9),
                startedAt: Utc(2026, 8, 21, 9), completedAt: Utc(2026, 8, 21, 10)),
        };
        _repo.GetJobsTouchingWindowAsync(windowFrom, windowTo, Arg.Any<CancellationToken>())
            .Returns(jobs);

        var result = await _sut.GetTrendAsync(windowFrom, windowTo, StatsBucket.Day,
            TestContext.Current.CancellationToken);

        result.Volume.Select(p => p.BucketStart).Should().Equal(
            Utc(2026, 8, 20), Utc(2026, 8, 21));
        result.FailureRate.Select(p => p.BucketStart).Should().Equal(
            Utc(2026, 8, 21)); // completion day only
    }

    [Fact]
    public async Task cancelled_jobs_are_excluded_from_the_rate_denominator()
    {
        var windowFrom = Utc(2026, 8, 21);
        var windowTo = Utc(2026, 8, 22);
        var start = Utc(2026, 8, 21, 9);

        var jobs = new List<TranspilationJob>
        {
            Job(TranspilationStatus.Ready, start, start, Utc(2026, 8, 21, 10)),
            Job(TranspilationStatus.Partial, start, start, Utc(2026, 8, 21, 10)),
            Job(TranspilationStatus.Failed, start, start, Utc(2026, 8, 21, 10)),
            Job(TranspilationStatus.Cancelled, start, start, Utc(2026, 8, 21, 10)),
        };
        _repo.GetJobsTouchingWindowAsync(windowFrom, windowTo, Arg.Any<CancellationToken>())
            .Returns(jobs);

        var result = await _sut.GetTrendAsync(windowFrom, windowTo, StatsBucket.Day,
            TestContext.Current.CancellationToken);

        var point = result.FailureRate.Single();
        point.Total.Should().Be(3);
        point.Failed.Should().Be(1);
        point.FailureRate.Should().Be(33.33);
    }

    [Fact]
    public async Task duration_stats_compute_avg_and_percentiles_from_known_sample()
    {
        var baseTime = Utc(2026, 8, 21, 8);
        // Durations: 10, 20, 30, 40, 50 minutes → p50 = 30, p90 = 50, avg = 30
        var jobs = Enumerable.Range(1, 5)
            .Select(i =>
            {
                var started = baseTime;
                return Job(
                    TranspilationStatus.Ready,
                    started,
                    started,
                    started.AddMinutes(i * 10));
            })
            .ToList();

        _repo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(jobs);

        var result = await _sut.GetOverviewAsync(30, TestContext.Current.CancellationToken);

        result.Duration.Should().NotBeNull();
        result.Duration!.SampleCount.Should().Be(5);
        result.Duration.AvgMinutes.Should().Be(30);
        result.Duration.P50Minutes.Should().Be(30);
        result.Duration.P90Minutes.Should().Be(50);
    }

    [Fact]
    public async Task overview_status_counts_pass_through()
    {
        var counts = new List<TranspilationStatusCount>
        {
            new(TranspilationStatus.Queued, 2),
            new(TranspilationStatus.Processing, 1),
            new(TranspilationStatus.Failed, 1),
        };
        _repo.GetStatusCountsAsync(Arg.Any<CancellationToken>()).Returns(counts);
        _repo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetOverviewAsync(30, TestContext.Current.CancellationToken);

        result.StatusCounts.Should().BeSameAs(counts);
        result.Duration.Should().BeNull();
    }

    [Fact]
    public async Task empty_window_yields_empty_points_and_null_duration()
    {
        _repo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        _repo.GetStatusCountsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var trend = await _sut.GetTrendAsync(Utc(2026, 8, 21), Utc(2026, 8, 22), StatsBucket.Hour,
            TestContext.Current.CancellationToken);
        var overview = await _sut.GetOverviewAsync(30, TestContext.Current.CancellationToken);

        trend.FailureRate.Should().BeEmpty();
        trend.Volume.Should().BeEmpty();
        overview.Duration.Should().BeNull();
        overview.StatusCounts.Should().BeEmpty();
    }
}