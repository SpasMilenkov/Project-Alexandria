using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.LyricsStats;
using Alexandria.Services.Monitoring;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Monitoring;

public class LyricsStatsServiceTests
{
    private readonly ITrackLyricsRepository _repo = Substitute.For<ITrackLyricsRepository>();
    private readonly LyricsStatsService _sut;

    public LyricsStatsServiceTests()
    {
        _sut = new LyricsStatsService(_repo);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static TrackLyrics Row(
        LyricsStatus status,
        DateTime createdAt,
        LyricsProvider provider = LyricsProvider.LrclibPublic,
        decimal? confidence = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TranspilationJobId = Guid.NewGuid(),
            Status = status,
            SourceProvider = provider,
            CreatedAt = createdAt,
            ConfidenceScore = confidence
        };

    [Fact]
    public async Task hourly_buckets_truncate_to_the_hour_and_split_outcomes()
    {
        var windowFrom = Utc(2026, 8, 21, 10);
        var windowTo = Utc(2026, 8, 21, 12);

        var rows = new List<TrackLyrics>
        {
            Row(LyricsStatus.Fetched, Utc(2026, 8, 21, 10, 20)),
            Row(LyricsStatus.FetchFailed, Utc(2026, 8, 21, 10, 45)),
            // in-flight row created at :52 — excluded from terminal counts but
            // still an attempt bucketed into 10:00? No — it lands in its own hour.
            Row(LyricsStatus.Fetching, Utc(2026, 8, 21, 11, 52)),
            Row(LyricsStatus.NoMatch, Utc(2026, 8, 21, 11, 15)),
        };
        _repo.GetCreatedBetweenAsync(windowFrom, windowTo, Arg.Any<CancellationToken>())
            .Returns(rows);

        var result = await _sut.GetTrendAsync(windowFrom, windowTo, LyricsStatsBucket.Hour,
            TestContext.Current.CancellationToken);

        result.Points.Should().HaveCount(2);
        result.Points[0].BucketStart.Should().Be(Utc(2026, 8, 21, 10));
        result.Points[0].Total.Should().Be(2);
        result.Points[0].Fetched.Should().Be(1);
        result.Points[0].Failed.Should().Be(1);
        result.Points[1].Total.Should().Be(1); // Fetching is not terminal
        result.Points[1].Fetched.Should().Be(0);
        result.Points[1].Failed.Should().Be(0);
    }

    [Fact]
    public async Task rate_uses_terminal_denominator_including_no_match()
    {
        var windowFrom = Utc(2026, 8, 21);
        var windowTo = Utc(2026, 8, 22);
        var at = Utc(2026, 8, 21, 9);

        var rows = new List<TrackLyrics>
        {
            Row(LyricsStatus.Fetched, at),
            Row(LyricsStatus.NoMatch, at),
            Row(LyricsStatus.FetchFailed, at),
            Row(LyricsStatus.PendingFetch, at),
        };
        _repo.GetCreatedBetweenAsync(windowFrom, windowTo, Arg.Any<CancellationToken>())
            .Returns(rows);

        var result = await _sut.GetTrendAsync(windowFrom, windowTo, LyricsStatsBucket.Day,
            TestContext.Current.CancellationToken);

        var point = result.Points.Single();
        point.Total.Should().Be(3); // PendingFetch excluded
        point.FailureRate.Should().Be(33.33); // 1 failed / 3 terminal
    }

    [Fact]
    public async Task overview_composes_counts_providers_and_confidence()
    {
        var counts = new List<LyricsStatusCount>
        {
            new(LyricsStatus.Fetched, 7),
            new(LyricsStatus.FetchFailed, 2),
        };
        var providers = new List<LyricsProviderBreakdownRow>
        {
            new(LyricsProvider.LrclibPublic, 6, 1),
            new(LyricsProvider.Manual, 3, 1),
        };
        _repo.GetStatusCountsAsync(Arg.Any<CancellationToken>()).Returns(counts);
        _repo.GetProviderBreakdownAsync(Arg.Any<CancellationToken>()).Returns(providers);
        _repo.GetAvgConfidenceAsync(Arg.Any<CancellationToken>()).Returns(0.87);

        var result = await _sut.GetOverviewAsync(TestContext.Current.CancellationToken);

        result.StatusCounts.Should().BeSameAs(counts);
        result.Providers.Should().BeSameAs(providers);
        result.FetchedTotal.Should().Be(7);
        result.AvgConfidence.Should().Be(0.87);
    }

    [Fact]
    public async Task empty_window_yields_no_points()
    {
        _repo.GetCreatedBetweenAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.GetTrendAsync(Utc(2026, 8, 21), Utc(2026, 8, 22),
            LyricsStatsBucket.Hour, TestContext.Current.CancellationToken);

        result.Points.Should().BeEmpty();
    }
}