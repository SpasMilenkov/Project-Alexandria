using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class QualifiedListeningTests
{
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ListeningSessionRow Row(Guid file, long seconds, int day = 1, double? duration = null)
        => new(Guid.NewGuid(), file, "song.mp3", "Song", file.ToString(),
            From.AddDays(day - 1).AddHours(10), seconds, false, duration);

    [Fact]
    public void Compute_and_timeline_use_persisted_qualification_when_metadata_changes()
    {
        var rows = new[]
        {
            Row(FileA, 10, duration: 20) with { QualifiedPlay = false },
            Row(FileB, 10, duration: 300) with { QualifiedPlay = true }
        };

        var stats = ListeningStatsCompute.Compute(From, To, rows, []);
        var timeline = ListeningTimeline.Build(From, To, rows);

        stats.QualifiedPlayCount.Should().Be(1);
        stats.TopSongs.Single(s => s.FileId == FileA).QualifiedPlayCount.Should().Be(0);
        stats.TopSongs.Single(s => s.FileId == FileB).QualifiedPlayCount.Should().Be(1);
        stats.CompletedSessions.Should().Be(0);
        timeline.QualifiedPlayCount.Should().Be(1);
        timeline.TotalSeconds.Should().Be(20);
    }

    [Fact]
    public void Compute_and_timeline_keep_short_listening_but_count_only_qualified_plays()
    {
        var rows = new[]
        {
            Row(FileA, 20, duration: 300),
            Row(FileA, 30, duration: 300),
            Row(FileB, 10, duration: 20),
            Row(FileB, 4, duration: 20)
        };

        var stats = ListeningStatsCompute.Compute(From, To, rows, []);
        var timeline = ListeningTimeline.Build(From, To, rows);

        stats.TotalListenedSeconds.Should().Be(64);
        stats.SessionCount.Should().Be(4);
        stats.QualifiedPlayCount.Should().Be(2);
        stats.TopSongs.Sum(s => s.SessionCount).Should().Be(4);
        stats.TopSongs.Sum(s => s.QualifiedPlayCount).Should().Be(2);
        stats.TopArtists.Sum(a => a.QualifiedPlayCount).Should().Be(2);
        stats.Insights.Days.Sum(d => d.Plays).Should().Be(2);
        stats.Insights.Months.Sum(m => m.Plays).Should().Be(2);

        timeline.TotalSeconds.Should().Be(64);
        timeline.SessionCount.Should().Be(4);
        timeline.QualifiedPlayCount.Should().Be(2);
        timeline.Days.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Seconds = 64L, Plays = 2, SessionCount = 4
        });
        timeline.Months.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Seconds = 64L, Plays = 2, SessionCount = 4
        });
    }

    [Fact]
    public void Compute_zero_qualified_plays_preserves_the_listening_story_and_time()
    {
        var rows = new[] { Row(FileA, 2), Row(FileA, 3), Row(FileA, 5) };

        var stats = ListeningStatsCompute.Compute(From, To, rows, []);
        var timeline = ListeningTimeline.Build(From, To, rows);
        var deck = ListeningStatsCurate.Curate(stats);

        stats.HasSessions.Should().BeTrue();
        stats.SessionCount.Should().Be(3);
        stats.QualifiedPlayCount.Should().Be(0);
        stats.TotalListenedSeconds.Should().Be(10);
        stats.MostReplayed.Should().BeNull();
        stats.TopSongs.Should().ContainSingle().Which.QualifiedPlayCount.Should().Be(0);
        stats.Insights.ActiveDays.Should().Be(1);
        timeline.Days.Should().ContainSingle().Which.Plays.Should().Be(0);
        timeline.TotalSeconds.Should().Be(10);
        deck.Cards.Should().Contain(c => c.Type == WrappedCardType.ListeningTime && c.Facts.Seconds == 10);
        deck.Cards.Should().NotContain(c => c.Type == WrappedCardType.MostReplayed);
    }

    [Fact]
    public void Compute_replay_and_duration_ties_use_qualified_counts_instead_of_fragments()
    {
        var rows = Enumerable.Range(0, 10).Select(_ => Row(FileA, 3)).ToList();
        rows.Add(Row(FileB, 30, 2));

        var stats = ListeningStatsCompute.Compute(From, To, rows, []);

        stats.SessionCount.Should().Be(11);
        stats.QualifiedPlayCount.Should().Be(1);
        stats.TopSongs[0].FileId.Should().Be(FileB);
        stats.TopArtists[0].Key.Should().Be(FileB.ToString());
        stats.MostReplayed.Should().Be(new ReplayExtreme(FileB, 1, 1));
    }

    [Fact]
    public void Compute_replay_days_require_a_qualified_play_on_each_day()
    {
        var rows = Enumerable.Range(1, 5).Select(day => Row(FileA, 1, day)).ToList();
        rows.Add(Row(FileA, 100, 6));

        var stats = ListeningStatsCompute.Compute(From, To, rows, []);

        stats.Insights.ActiveDays.Should().Be(6);
        stats.MostReplayed.Should().Be(new ReplayExtreme(FileA, 1, 1));
    }

    [Fact]
    public void Compute_rhythm_and_chapters_require_qualified_plays_without_losing_short_time()
    {
        var rows = Enumerable.Range(0, 9).Select(i => Row(FileA, 1800, i % 5 + 1)).ToList();
        rows.AddRange(Enumerable.Range(0, 20).Select(_ => Row(FileA, 1)));

        var below = ListeningStatsCompute.Compute(From, To, rows, []);

        below.SessionCount.Should().Be(29);
        below.QualifiedPlayCount.Should().Be(9);
        below.TotalListenedSeconds.Should().Be(16220);
        below.Insights.Chapters.Should().BeEmpty();
        ListeningRhythm.Select(below).Evidence.Should().Be(WrappedRhythmEvidence.Sparse);

        rows.Add(Row(FileA, 30, 6));
        var enough = ListeningStatsCompute.Compute(From, To, rows, []);

        enough.QualifiedPlayCount.Should().Be(10);
        enough.Insights.Chapters.Should().ContainSingle();
        ListeningRhythm.Select(enough).Evidence.Should().Be(WrappedRhythmEvidence.Pronounced);
    }
}
