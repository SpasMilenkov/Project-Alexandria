using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using Alexandria.Services.Streaming.Wrapped;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Streaming;

public class ListeningStatsCurateTests
{
    private static readonly Guid FileA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FileC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FileD = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    private static ListeningSessionRow Session(
        Guid fileId, DateTime startedAt, long listenedSeconds,
        string? artist = null, string? title = null, bool completed = false) =>
        new(Guid.NewGuid(), fileId, $"{fileId}.mp3", title, artist, startedAt, listenedSeconds, completed);

    private static DateTime At(int month, int day, int hour) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static RawListeningStats Compute(
        List<ListeningSessionRow> sessions,
        List<ListeningHistoryRef>? histories = null) =>
        ListeningStatsCompute.Compute(PeriodStart, PeriodEnd, sessions, histories ?? []);

    private static WrappedCard? CardOf(CuratedDeck deck, WrappedCardType type) =>
        deck.Cards.FirstOrDefault(c => c.Type == type);

    [Fact]
    public void Curate_empty_stats_yields_empty_deck()
    {
        var deck = ListeningStatsCurate.Curate(Compute([]));

        deck.Cards.Should().BeEmpty();
    }

    [Fact]
    public void Curate_minimal_session_yields_base_cards_only()
    {
        var deck = ListeningStatsCurate.Curate(Compute(
            [Session(FileA, At(3, 1, 10), 120, artist: "arcane", title: "Drift")]));

        deck.Cards.Select(c => c.Type).Should().Equal(
            WrappedCardType.TopArtists,
            WrappedCardType.TopSongs,
            WrappedCardType.ListeningTime,
            WrappedCardType.Persona,
            WrappedCardType.Bookends);
    }

    [Fact]
    public void Curate_caps_countdown_entries_at_five_with_ranks()
    {
        var sessions = new List<ListeningSessionRow>();

        for (var index = 0; index < 7; index++)
            sessions.Add(Session(Guid.NewGuid(), At(3, 1 + index, 10), 600 - index, artist: $"artist {index}"));

        var deck = ListeningStatsCurate.Curate(Compute(sessions));
        var card = CardOf(deck, WrappedCardType.TopArtists);

        card.Should().NotBeNull();
        card!.Entries.Should().HaveCount(5);
        card.Entries.Select(e => e.Rank).Should().Equal(1, 2, 3, 4, 5);
        card.Entries[0].Title.Should().Be("artist 0");
    }

    [Fact]
    public void Curate_repeated_duration_facts_keeps_deck_within_cap_without_padding()
    {
        var sessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 14; day++)
            sessions.Add(Session(FileA, At(3, day, 22), 7200, artist: "arcane", title: "Drift", completed: true));

        for (var day = 1; day <= 6; day++)
            sessions.Add(Session(FileB, At(4, day, 22), 3600, artist: "arcane", title: "Echo", completed: true));

        var histories = new List<ListeningHistoryRef>();

        for (var index = 0; index < 8; index++)
            histories.Add(new ListeningHistoryRef(Guid.NewGuid(), $"fresh {index % 2}", At(5, 1 + index, 10)));

        var deck = ListeningStatsCurate.Curate(Compute(sessions, histories));

        deck.Cards.Count.Should().BeLessThanOrEqualTo(10);
        CardOf(deck, WrappedCardType.Streak).Should().NotBeNull();
        CardOf(deck, WrappedCardType.LongestSitting).Should().BeNull();
        CardOf(deck, WrappedCardType.Discoveries).Should().BeNull();
        CardOf(deck, WrappedCardType.LoyalListener).Should().BeNull();
    }

    [Fact]
    public void Curate_omits_loyal_listener_when_rate_is_low()
    {
        var sessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 12; day++)
            sessions.Add(Session(FileA, At(3, day, 10), 200, artist: "arcane", completed: day <= 4));

        var deck = ListeningStatsCurate.Curate(Compute(sessions));

        CardOf(deck, WrappedCardType.LoyalListener).Should().BeNull();
    }

    [Fact]
    public void Curate_omits_loyal_listener_when_completions_are_few()
    {
        var sessions = new List<ListeningSessionRow>
        {
            Session(FileA, At(3, 1, 10), 200, completed: true),
            Session(FileA, At(3, 2, 10), 200, completed: true),
            Session(FileA, At(3, 3, 10), 200, completed: true),
        };

        var deck = ListeningStatsCurate.Curate(Compute(sessions));

        CardOf(deck, WrappedCardType.LoyalListener).Should().BeNull();
    }

    [Fact]
    public void Curate_completion_flags_do_not_establish_no_skipping()
    {
        var sessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 12; day++)
            sessions.Add(Session(FileA, At(3, day, 10), 200, artist: "arcane", completed: true));

        var deck = ListeningStatsCurate.Curate(Compute(sessions));
        var card = CardOf(deck, WrappedCardType.LoyalListener);

        card.Should().BeNull();
    }

    [Fact]
    public void Curate_omits_short_streak_but_keeps_week_run()
    {
        var shortSessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 3; day++)
            shortSessions.Add(Session(FileA, At(3, day, 10), 60));

        CardOf(ListeningStatsCurate.Curate(Compute(shortSessions)), WrappedCardType.Streak)
            .Should().BeNull();

        var longSessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 7; day++)
            longSessions.Add(Session(FileA, At(3, day, 10), 60));

        var streak = CardOf(ListeningStatsCurate.Curate(Compute(longSessions)), WrappedCardType.Streak);

        streak.Should().NotBeNull();
        streak!.Visual.ShapeGrammar.Should().Be("calendar");
        streak.Facts.Count.Should().Be(7);
    }

    [Fact]
    public void Curate_seeds_night_persona_with_midnight_palette()
    {
        var deck = ListeningStatsCurate.Curate(Compute(
            [Session(FileA, At(3, 1, 2), 600, artist: "arcane")]));

        var card = CardOf(deck, WrappedCardType.Persona);

        card.Should().NotBeNull();
        card!.Headline.Should().Be("Your rhythm is taking shape");
        card.Visual.ColorKey.Should().Be("midnight-blue");
        card.Facts.Weights[2].Should().Be(600);
    }

    [Fact]
    public void Curate_top_song_art_uses_listening_share_instead_of_completion()
    {
        var sessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 4; day++)
            sessions.Add(Session(FileA, At(3, day, 10), 200, completed: day == 1));

        var deck = ListeningStatsCurate.Curate(Compute(sessions));
        var card = CardOf(deck, WrappedCardType.TopSongs);

        card!.Facts.Share.Should().Be(1);
        card.Entries[0].Seconds.Should().Be(800);
    }

    [Fact]
    public void Curate_same_replay_and_duration_winner_omits_duplicate_records()
    {
        var sessions = new List<ListeningSessionRow>();

        for (var day = 1; day <= 6; day++)
            sessions.Add(Session(FileA, At(3, day, 10), 600, artist: "arcane", title: "Same Song"));

        sessions.Add(Session(FileB, At(3, 20, 10), 100, artist: "arcane", title: "Other"));

        var deck = ListeningStatsCurate.Curate(Compute(sessions));

        CardOf(deck, WrappedCardType.MostReplayed).Should().BeNull();
        CardOf(deck, WrappedCardType.MostMinutes).Should().BeNull();
    }

    [Fact]
    public void Curate_folds_pronounced_weekend_lean_into_persona()
    {
        var sessions = new List<ListeningSessionRow>
        {
            Session(FileA, At(3, 7, 20), 3600, artist: "arcane"),
            Session(FileA, At(3, 8, 20), 3600, artist: "arcane"),
            Session(FileA, At(3, 9, 20), 60, artist: "arcane"),
        };

        for (var day = 10; day <= 16; day++)
            sessions.Add(Session(FileA, At(3, day, 20), 60, artist: "arcane"));

        var deck = ListeningStatsCurate.Curate(Compute(sessions));

        CardOf(deck, WrappedCardType.Persona)!.Subline.Should().Contain("weekends");
    }

    [Fact]
    public void Curate_bookends_resolve_song_titles_with_dates()
    {
        var deck = ListeningStatsCurate.Curate(Compute(new List<ListeningSessionRow>
        {
            Session(FileA, At(1, 3, 9), 120, title: "Opener"),
            Session(FileB, At(6, 15, 20), 120, title: "Closer"),
        }));

        var card = CardOf(deck, WrappedCardType.Bookends);

        card.Should().NotBeNull();
        card!.Entries.Should().HaveCount(2);
        card.Entries[0].Title.Should().Be("Opener");
        card.Entries[0].Subtitle.Should().Contain("January 3");
        card.Entries[0].Date.Should().Be(new DateTime(2026, 1, 3, 9, 0, 0, DateTimeKind.Utc).ToString("o"));
        card.Entries[1].Title.Should().Be("Closer");
        card.Entries[1].Date.Should().Be(new DateTime(2026, 6, 15, 20, 0, 0, DateTimeKind.Utc).ToString("o"));
    }

    [Fact]
    public void Curate_comparisons_carry_named_catalog_anchors()
    {
        var minutes = ListeningStatsCurate.Curate(Compute(
            [Session(FileA, At(3, 1, 10), 1500)]));

        CardOf(minutes, WrappedCardType.ListeningTime)!.Facts.Comparison.Should().BeNull();

        var films = ListeningStatsCurate.Curate(Compute(
            [Session(FileA, At(3, 1, 10), 36000)]));

        CardOf(films, WrappedCardType.ListeningTime)!.Facts.Comparison!.DurationMinutes.Should().BeGreaterThan(0);

        var albums = ListeningStatsCurate.Curate(Compute(
            [Session(FileA, At(3, 1, 10), 180000)]));

        CardOf(albums, WrappedCardType.ListeningTime)!.Facts.Comparison!.Name.Should().NotBeNullOrWhiteSpace();

        var days = ListeningStatsCurate.Curate(Compute(
            [Session(FileA, At(3, 1, 10), 1000000)]));

        CardOf(days, WrappedCardType.ListeningTime)!.Facts.Comparison!.CatalogVersion.Should().Be(ListeningComparisons.CatalogVersion);
    }

    [Fact]
    public void Curate_omits_small_busiest_day_sitting_and_discoveries()
    {
        var sessions = new List<ListeningSessionRow>
        {
            Session(FileA, At(3, 5, 10), 900, title: "Brief"),
            Session(FileB, At(3, 6, 10), 300, title: "Shorter"),
        };

        var histories = new List<ListeningHistoryRef>
        {
            new(FileA, "arcane", At(3, 1, 10)),
            new(FileB, "other", At(3, 2, 10)),
        };

        var deck = ListeningStatsCurate.Curate(Compute(sessions, histories));

        CardOf(deck, WrappedCardType.BusiestDay).Should().BeNull();
        CardOf(deck, WrappedCardType.LongestSitting).Should().BeNull();
        CardOf(deck, WrappedCardType.Discoveries).Should().BeNull();
        CardOf(deck, WrappedCardType.NewArtists).Should().BeNull();
    }

    [Fact]
    public void Curate_anchors_busiest_day_to_its_date()
    {
        var sessions = new List<ListeningSessionRow>
        {
            Session(FileA, At(3, 5, 10), 5400),
            Session(FileB, At(3, 6, 10), 300),
        };

        for (var day = 7; day <= 11; day++)
            sessions.Add(Session(FileB, At(3, day, 10), 300));

        var deck = ListeningStatsCurate.Curate(Compute(sessions));
        var card = CardOf(deck, WrappedCardType.BusiestDay);

        card.Should().NotBeNull();
        card!.Facts.From.Should().Be("2026-03-05");
        card.Facts.BaselineSeconds.Should().Be(300);
    }

    [Fact]
    public void Curate_accumulated_time_keeps_actual_total_alongside_comparison()
    {
        var deck = ListeningStatsCurate.Curate(Compute(
            [Session(FileA, At(3, 1, 10), 72000)]));

        var card = CardOf(deck, WrappedCardType.ListeningTime);

        card!.Headline.Should().Contain("20 hours");
        card.Subline.Should().Contain("20 hours");
        card.Facts.Seconds.Should().Be(72000);
        card.Visual.ShapeGrammar.Should().Be("landscape");
        card.Visual.FillLevel.Should().BeInRange(0, 1);
    }
}
