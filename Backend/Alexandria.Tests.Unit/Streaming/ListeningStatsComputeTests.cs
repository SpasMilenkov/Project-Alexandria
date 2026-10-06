using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class ListeningStatsComputeTests
{
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FileC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    private static ListeningSessionRow Session(
        Guid fileId,
        DateTime startedAt,
        long listenedSeconds,
        string? artist = null,
        string? title = null,
        bool completed = false,
        string fileName = "track.mp3") =>
        new(Guid.NewGuid(), fileId, fileName, title, artist, startedAt, listenedSeconds, completed);

    private static ListeningHistoryRef History(Guid fileId, DateTime createdAt, string? artist = null) =>
        new(fileId, artist, createdAt);

    private static DateTime At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Compute_empty_period_returns_empty_stats_not_null()
    {
        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, [], []);

        stats.HasSessions.Should().BeFalse();
        stats.TotalListenedSeconds.Should().Be(0);
        stats.TopArtists.Should().BeEmpty();
        stats.TopSongs.Should().BeEmpty();
        stats.MostReplayed.Should().BeNull();
        stats.MostMinutesOnSong.Should().BeNull();
        stats.LongestSitting.Should().BeNull();
        stats.FirstSession.Should().BeNull();
        stats.SecondsByHourUtc.Should().HaveCount(24);
    }

    [Fact]
    public void Compute_ranks_artists_by_seconds_with_mode_display_name()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 1, 10), 100, artist: "arcane"),
            Session(FileB, At(3, 2, 10), 300, artist: "  ARCANE "),
            Session(FileC, At(3, 3, 10), 200, artist: "Arcane"),
            Session(FileA, At(3, 4, 10), 50, artist: "other"),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.TopArtists.Should().HaveCount(2);
        stats.TopArtists[0].Key.Should().Be("arcane");
        stats.TopArtists[0].TotalListenedSeconds.Should().Be(600);
        stats.TopArtists[0].SessionCount.Should().Be(3);
        stats.TopArtists[0].DisplayName.Should().Be("arcane");
        stats.TopArtists[1].Key.Should().Be("other");
    }

    [Fact]
    public void Compute_display_name_prefers_most_frequent_casing()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 1, 10), 100, artist: "THE BEATLES"),
            Session(FileA, At(3, 2, 10), 100, artist: "The Beatles"),
            Session(FileA, At(3, 3, 10), 100, artist: "The Beatles"),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.TopArtists.Should().ContainSingle()
            .Which.DisplayName.Should().Be("The Beatles");
    }

    [Fact]
    public void Compute_top_songs_falls_back_to_filename_without_title()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 1, 10), 120, artist: "arcane", fileName: "mystery.mp3"),
            Session(FileB, At(3, 2, 10), 60, artist: "arcane", title: "Named Song"),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.TopSongs[0].FileId.Should().Be(FileA);
        stats.TopSongs[0].DisplayTitle.Should().Be("mystery.mp3");
        stats.TopSongs[0].DisplayArtist.Should().Be("arcane");
        stats.TopSongs[1].DisplayTitle.Should().Be("Named Song");
    }

    [Fact]
    public void Compute_separates_replay_count_from_duration_extremes()
    {
        var sessions = new List<ListeningSessionRow>
        {
            Session(FileA, At(3, 1, 10), 30),
            Session(FileA, At(3, 2, 10), 30),
            Session(FileA, At(3, 3, 10), 30),
            Session(FileB, At(3, 4, 10), 500),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.MostReplayed.Should().Be(new ReplayExtreme(FileA, 3, 3));
        stats.MostMinutesOnSong.Should().Be(new DurationExtreme(FileB, 500));
    }

    [Fact]
    public void Compute_persona_weights_by_seconds_not_session_count()
    {
        var sessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 5; day++)
            sessions.Add(Session(FileA, At(3, day, 9), 10));

        sessions.Add(Session(FileB, At(3, 10, 22), 1000));

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.DominantBucket.Should().Be(TimeOfDayBucket.Evening);
        stats.SecondsByHourUtc[9].Should().Be(50);
        stats.SecondsByHourUtc[22].Should().Be(1000);
    }

    [Fact]
    public void Compute_splits_weekday_and_weekend_seconds()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 7, 10), 100),
            Session(FileB, At(3, 8, 10), 200),
            Session(FileC, At(3, 9, 10), 400),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.WeekendSeconds.Should().Be(300);
        stats.WeekdaySeconds.Should().Be(400);
    }

    [Fact]
    public void Compute_finds_longest_streak_with_dates()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 1, 10), 60),
            Session(FileA, At(3, 2, 10), 60),
            Session(FileA, At(3, 3, 10), 60),
            Session(FileA, At(3, 10, 10), 60),
            Session(FileA, At(3, 11, 10), 60),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.LongestStreakDays.Should().Be(3);
        stats.StreakStart.Should().Be(new DateOnly(2026, 3, 1));
        stats.StreakEnd.Should().Be(new DateOnly(2026, 3, 3));
    }

    [Fact]
    public void Compute_anchors_busiest_day_sitting_and_bookends()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 1, 8), 60),
            Session(FileB, At(3, 5, 20), 700),
            Session(FileC, At(3, 5, 21), 100),
            Session(FileA, At(3, 9, 12), 60),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.BusiestDay.Should().Be(new DateOnly(2026, 3, 5));
        stats.BusiestDaySeconds.Should().Be(800);
        stats.LongestSitting.Should().NotBeNull();
        stats.LongestSitting!.FileId.Should().Be(FileB);
        stats.LongestSitting.ListenedSeconds.Should().Be(700);
        stats.FirstSession.Should().Be(new BookendSession(FileA, At(3, 1, 8)));
        stats.LastSession.Should().Be(new BookendSession(FileA, At(3, 9, 12)));
    }

    [Fact]
    public void Compute_reports_completion_rate_for_skip_gate()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 1, 10), 200, completed: true),
            Session(FileA, At(3, 2, 10), 200, completed: true),
            Session(FileA, At(3, 3, 10), 200, completed: true),
            Session(FileB, At(3, 4, 10), 20),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.CompletedSessions.Should().Be(3);
        stats.CompletionRate.Should().BeApproximately(0.75, 0.0001);
        stats.TotalListenedSeconds.Should().Be(620);
        stats.SessionCount.Should().Be(4);
    }

    [Fact]
    public void Compute_counts_discoveries_and_new_artists_from_history_refs()
    {
        var sessions = new[] { Session(FileA, At(3, 1, 10), 60, artist: "arcane") };

        var histories = new[]
        {
            History(FileA, new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc), "arcane"),
            History(FileB, new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc), "veteran"),
            History(FileC, new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc), "newcomer"),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, histories);

        stats.DiscoveryCount.Should().Be(2);
        stats.NewArtists.Should().HaveCount(2);
        stats.NewArtists[0].Key.Should().Be("arcane");
        stats.NewArtists[1].Key.Should().Be("newcomer");
    }

    [Fact]
    public void Compute_ignores_artist_less_rows_in_rankings_but_counts_sessions()
    {
        var sessions = new[]
        {
            Session(FileA, At(3, 1, 10), 60),
            Session(FileB, At(3, 2, 10), 60, artist: "arcane"),
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.SessionCount.Should().Be(2);
        stats.TotalListenedSeconds.Should().Be(120);
        stats.TopArtists.Should().ContainSingle().Which.Key.Should().Be("arcane");
        stats.TopSongs.Should().HaveCount(2);
    }

    [Fact]
    public void BucketForHour_maps_boundaries()
    {
        ListeningStatsCompute.BucketForHour(0).Should().Be(TimeOfDayBucket.Night);
        ListeningStatsCompute.BucketForHour(5).Should().Be(TimeOfDayBucket.Night);
        ListeningStatsCompute.BucketForHour(6).Should().Be(TimeOfDayBucket.Morning);
        ListeningStatsCompute.BucketForHour(11).Should().Be(TimeOfDayBucket.Morning);
        ListeningStatsCompute.BucketForHour(12).Should().Be(TimeOfDayBucket.Afternoon);
        ListeningStatsCompute.BucketForHour(17).Should().Be(TimeOfDayBucket.Afternoon);
        ListeningStatsCompute.BucketForHour(18).Should().Be(TimeOfDayBucket.Evening);
        ListeningStatsCompute.BucketForHour(23).Should().Be(TimeOfDayBucket.Evening);
    }

    [Fact]
    public void Compute_filters_period_boundaries_and_nonpositive_sessions()
    {
        var sessions = new[]
        {
            Session(FileA, PeriodStart.AddTicks(-1), 500),
            Session(FileA, PeriodStart, 60, completed: true),
            Session(FileB, PeriodEnd.AddTicks(-1), 120),
            Session(FileC, PeriodEnd, 500),
            Session(FileC, At(3, 1, 10), 0),
            Session(FileC, At(3, 1, 11), -60)
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);

        stats.SessionCount.Should().Be(2);
        stats.TotalListenedSeconds.Should().Be(180);
        stats.CompletedSessions.Should().Be(1);
        stats.CompletionRate.Should().Be(0.5);
        stats.TopSongs.Select(s => s.FileId).Should().Equal(FileB, FileA);
        stats.FirstSession.Should().Be(new BookendSession(FileA, PeriodStart));
        stats.LastSession.Should().Be(new BookendSession(FileB, PeriodEnd.AddTicks(-1)));
    }

    [Fact]
    public void Compute_tied_sessions_keep_deterministic_rankings_names_and_bookends()
    {
        var start = At(3, 1, 10);

        var sessions = new[]
        {
            new ListeningSessionRow(FileC, FileB, "b.mp3", "Second", "  ARCANE  ", start, 60, false),
            new ListeningSessionRow(FileB, FileA, "a.mp3", "Later title", "ARCANE", start, 60, false),
            new ListeningSessionRow(FileA, FileA, "a.mp3", " First title ", "Arcane", start, 60, true)
        };

        var stats = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, []);
        var reversed = ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions.Reverse(), []);

        reversed.Should().BeEquivalentTo(stats, options => options.WithStrictOrdering());
        stats.TopArtists.Should().ContainSingle().Which.DisplayName.Should().Be("ARCANE");
        stats.TopSongs[0].DisplayTitle.Should().Be("First title");
        stats.TopSongs[0].DisplayArtist.Should().Be("Arcane");
        stats.MostReplayed.Should().Be(new ReplayExtreme(FileA, 2, 1));
        stats.MostMinutesOnSong.Should().Be(new DurationExtreme(FileA, 120));
        stats.LongestSitting.Should().Be(new LongestSitting(FileA, FileA, 60, start));
        stats.FirstSession.Should().Be(new BookendSession(FileA, start));
        stats.LastSession.Should().Be(new BookendSession(FileB, start));
    }
}
