using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class ListeningTimelineTests
{
    private static readonly DateTime From = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ListeningSessionRow Row(Guid file, DateTime startedAt, long seconds, string? artist = "arcane") =>
        new(Guid.NewGuid(), file, "drift.mp3", "Drift", artist, startedAt, seconds, true);

    [Fact]
    public void Build_buckets_sessions_on_utc_day_boundaries()
    {
        var file = Guid.NewGuid();

        var sessions = new List<ListeningSessionRow>
        {
            Row(file, new DateTime(2026, 3, 10, 23, 30, 0, DateTimeKind.Utc), 600),
            Row(file, new DateTime(2026, 3, 11, 0, 30, 0, DateTimeKind.Utc), 300),
        };

        var result = ListeningTimeline.Build(From, To, sessions);

        result.Days.Should().HaveCount(2);
        result.Days[0].Date.Should().Be("2026-03-10");
        result.Days[0].Seconds.Should().Be(600);
        result.Days[1].Date.Should().Be("2026-03-11");
        result.ActiveDays.Should().Be(2);
        result.SessionCount.Should().Be(2);
        result.TotalSeconds.Should().Be(900);
    }

    [Fact]
    public void Build_caps_day_top_tracks_at_three_ordered_by_seconds()
    {
        var sessions = Enumerable.Range(0, 5)
            .Select(i => Row(
                Guid.Parse($"11111111-1111-1111-1111-11111111111{i}"),
                new DateTime(2026, 3, 10, 10 + i, 0, 0, DateTimeKind.Utc),
                100L * (i + 1)))
            .ToList();

        var result = ListeningTimeline.Build(From, To, sessions);

        var top = result.Days[0].Top;

        top.Should().HaveCount(3);
        top[0].Seconds.Should().Be(500);
        top[1].Seconds.Should().Be(400);
        top[2].Seconds.Should().Be(300);
    }

    [Fact]
    public void Build_empty_range_returns_empty_points()
    {
        var result = ListeningTimeline.Build(From, To, []);

        result.Days.Should().BeEmpty();
        result.Months.Should().BeEmpty();
        result.TotalSeconds.Should().Be(0);
        result.ActiveDays.Should().Be(0);
    }

    [Fact]
    public void Build_rolls_days_into_month_points()
    {
        var file = Guid.NewGuid();

        var sessions = new List<ListeningSessionRow>
        {
            Row(file, new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc), 600),
            Row(file, new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc), 300),
        };

        var result = ListeningTimeline.Build(From, To, sessions);

        result.Months.Should().HaveCount(2);
        result.Months[0].Date.Should().Be("2026-03-01");
        result.Months[0].Seconds.Should().Be(600);
        result.Months[0].Plays.Should().Be(1);
        result.Months[1].Date.Should().Be("2026-04-01");
    }
}
