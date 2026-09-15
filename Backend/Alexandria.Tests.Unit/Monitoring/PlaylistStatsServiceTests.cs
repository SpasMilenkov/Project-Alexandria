using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.TranspilationStats;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class PlaylistStatsServiceTests
{
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly PlaylistStatsService _sut;

    public PlaylistStatsServiceTests()
    {
        _sut = new PlaylistStatsService(_jobs);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static Job Run(
        JobStatus status,
        JobType type,
        DateTime createdAt,
        DateTime? startedAt = null,
        DateTime? completedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            Type = type,
            UserId = Guid.NewGuid(),
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };

    [Fact]
    public async Task GetOverviewAsync_returns_counts_and_duration_stats()
    {
        var now = DateTime.UtcNow;
        _jobs.GetStatusCountsAsync(JobType.PlaylistSync, Arg.Any<CancellationToken>())
            .Returns(new List<JobStatusCount> { new(JobStatus.Ready, 4), new(JobStatus.Failed, 1) });
        _jobs.GetJobsTouchingWindowAsync(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>(),
                Arg.Is<JobType?>(t => t == JobType.PlaylistSync))
            .Returns(new List<Job>
            {
                Run(JobStatus.Ready, JobType.PlaylistSync, now.AddDays(-1),
                    now.AddDays(-1), now.AddDays(-1).AddMinutes(2)),
                Run(JobStatus.Ready, JobType.PlaylistSync, now.AddDays(-1),
                    now.AddDays(-1), now.AddDays(-1).AddMinutes(4)),
            });

        var result = await _sut.GetOverviewAsync(30, TestContext.Current.CancellationToken);

        result.StatusCounts.Should().HaveCount(2);
        result.Duration.Should().NotBeNull();
        result.Duration!.SampleCount.Should().Be(2);
        result.Duration.AvgMinutes.Should().Be(3);
    }

    [Fact]
    public async Task GetOverviewAsync_returns_null_duration_without_samples()
    {
        _jobs.GetStatusCountsAsync(JobType.PlaylistSync, Arg.Any<CancellationToken>())
            .Returns(new List<JobStatusCount>());
        _jobs.GetJobsTouchingWindowAsync(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>(),
                Arg.Is<JobType?>(t => t == JobType.PlaylistSync))
            .Returns(new List<Job>());

        var result = await _sut.GetOverviewAsync(30, TestContext.Current.CancellationToken);

        result.Duration.Should().BeNull();
    }

    [Fact]
    public async Task GetTrendAsync_requests_playlist_only_rows_and_buckets()
    {
        var from = Utc(2026, 8, 21, 10);
        var to = Utc(2026, 8, 21, 13);
        _jobs.GetJobsTouchingWindowAsync(
                from, to, Arg.Any<CancellationToken>(), Arg.Is<JobType?>(t => t == JobType.PlaylistSync))
            .Returns(new List<Job>
            {
                Run(JobStatus.Ready, JobType.PlaylistSync, Utc(2026, 8, 21, 12, 5),
                    Utc(2026, 8, 21, 12, 10), Utc(2026, 8, 21, 12, 37)),
                Run(JobStatus.Failed, JobType.PlaylistSync, Utc(2026, 8, 21, 11),
                    Utc(2026, 8, 21, 11), Utc(2026, 8, 21, 11, 30)),
                Run(JobStatus.Queued, JobType.PlaylistSync, Utc(2026, 8, 21, 12, 52)),
            });

        var result = await _sut.GetTrendAsync(
            from, to, StatsBucket.Hour, TestContext.Current.CancellationToken);

        result.FailureRate.Should().HaveCount(2);
        result.FailureRate[0].Should().Be(new TranspilationRatePoint(Utc(2026, 8, 21, 11), 1, 1));
        result.FailureRate[1].Should().Be(new TranspilationRatePoint(Utc(2026, 8, 21, 12), 1, 0));
        result.Volume.Select(v => v.Count).Should().Equal(1, 2);
    }
}