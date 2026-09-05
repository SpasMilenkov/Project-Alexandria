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
    private readonly IJobRepository _jobRepo = Substitute.For<IJobRepository>();
    private readonly TranspilationStatsService _sut;

    public TranspilationStatsServiceTests()
    {
        _sut = new TranspilationStatsService(_repo, _jobRepo);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static TranspilationJob CreateTranspilationJob(
        JobStatus status,
        DateTime createdAt,
        DateTime? startedAt = null,
        DateTime? completedAt = null)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Type = JobType.Transpilation,
            UserId = Guid.NewGuid()
        };

        return new TranspilationJob
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Job = job,
            VersionId = Guid.NewGuid(),
            CreatedAt = createdAt,
            UserId = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task hourly_buckets_truncate_to_the_hour()
    {
        var windowFrom = Utc(2026, 8, 21, 10);
        var windowTo = Utc(2026, 8, 21, 13);

        var jobs = new List<TranspilationJob>
        {
            // Terminal job completing at :37 → bucket 12:00
            CreateTranspilationJob(JobStatus.Ready, Utc(2026, 8, 21, 12, 5),
                startedAt: Utc(2026, 8, 21, 12, 10), completedAt: Utc(2026, 8, 21, 12, 37)),
            // Volume-only job created at :52 → bucket 12:00 as well
            CreateTranspilationJob(JobStatus.Queued, Utc(2026, 8, 21, 12, 52)),
            // Terminal job in a different hour → separate bucket
            CreateTranspilationJob(JobStatus.Failed, Utc(2026, 8, 21, 11),
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
            CreateTranspilationJob(JobStatus.Ready, Utc(2026, 8, 20, 23),
                startedAt: Utc(2026, 8, 20, 23), completedAt: Utc(2026, 8, 21, 1)),
            CreateTranspilationJob(JobStatus.Failed, Utc(2026, 8, 21, 9),
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
            CreateTranspilationJob(JobStatus.Ready, start, start, Utc(2026, 8, 21, 10)),
            CreateTranspilationJob(JobStatus.Partial, start, start, Utc(2026, 8, 21, 10)),
            CreateTranspilationJob(JobStatus.Failed, start, start, Utc(2026, 8, 21, 10)),
            CreateTranspilationJob(JobStatus.Cancelled, start, start, Utc(2026, 8, 21, 10)),
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
                return CreateTranspilationJob(
                    JobStatus.Ready,
                    started,
                    started,
                    started.AddMinutes(i * 10));
            })
            .ToList();

        _jobRepo.GetJobsTouchingWindowAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(jobs.Select(j => j.Job).ToList());

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
        var counts = new List<JobStatusCount>
        {
            new(JobStatus.Queued, 2),
            new(JobStatus.Processing, 1),
            new(JobStatus.Failed, 1),
        };
        _jobRepo.GetStatusCountsAsync(JobType.Transpilation, Arg.Any<CancellationToken>()).Returns(counts);
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
        _jobRepo.GetStatusCountsAsync(JobType.Transpilation, Arg.Any<CancellationToken>()).Returns([]);

        var trend = await _sut.GetTrendAsync(Utc(2026, 8, 21), Utc(2026, 8, 22), StatsBucket.Hour,
            TestContext.Current.CancellationToken);
        var overview = await _sut.GetOverviewAsync(30, TestContext.Current.CancellationToken);

        trend.FailureRate.Should().BeEmpty();
        trend.Volume.Should().BeEmpty();
        overview.Duration.Should().BeNull();
        overview.StatusCounts.Should().BeEmpty();
    }
}