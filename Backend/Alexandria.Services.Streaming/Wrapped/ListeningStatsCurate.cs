using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using static Alexandria.Services.Streaming.Wrapped.ListeningStatsFormat;

namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>Chooses supported, varied stories. Facts and comparison arithmetic stay on the server.</summary>
internal static partial class ListeningStatsCurate
{
    private const int MaxCards = 10;

    public static CuratedDeck Curate(RawListeningStats stats, string visualIdentity = "")
    {
        if (!stats.HasSessions || stats.TotalListenedSeconds <= 0) return new CuratedDeck([]);

        var cards = new List<WrappedCard>();

        if (stats.TopArtists.Count > 0) cards.Add(TopArtistsCard(stats));

        cards.Add(TopSongsCard(stats));
        cards.Add(ListeningTimeCard(stats, visualIdentity));
        cards.Add(PersonaCard(stats));

        // One slot is reserved for the closing bookends card.
        var available = MaxCards - cards.Count - 1;

        cards.AddRange(PickStoryCards(stats, available));

        if (stats.FirstSession != null && stats.LastSession != null) cards.Add(BookendsCard(stats));

        return new CuratedDeck(cards);
    }

    private static IEnumerable<WrappedCard> PickStoryCards(RawListeningStats stats, int available)
    {
        var candidates = new[]
        {
            ExplorationCandidate(stats),
            RetainedDiscoveryCandidate(stats),
            NewArtistsCandidate(stats),
            DiscoveriesCandidate(stats),
            ChaptersCandidate(stats),
            ReturningFavoriteCandidate(stats),
            StreakCandidate(stats),
            BusiestDayCandidate(stats),
            MostReplayedCandidate(stats)
        }.OfType<StoryCandidate>();

        // One candidate per topic prevents repeated versions of the same discovery or duration fact filling the page.
        return candidates
            .GroupBy(c => c.Topic)
            .Select(g => g.OrderByDescending(c => c.Score).ThenBy(c => c.Card.Type).First())
            .OrderByDescending(c => c.Score).ThenBy(c => c.Card.Type)
            .Take(available)
            .OrderBy(c => c.Order)
            .Select(c => c.Card);
    }

    private static WrappedCard Card(RawListeningStats stats, WrappedCardType type, string headline, string? subline,
        string grammar, WrappedCardFacts facts, IReadOnlyList<WrappedCardEntry>? entries = null)
    {
        var color = stats.DominantBucket switch
        {
            TimeOfDayBucket.Night => "midnight-blue",
            TimeOfDayBucket.Morning => "pale-gold",
            TimeOfDayBucket.Afternoon => "daylight",
            _ => "ember"
        };

        var intensity = Math.Clamp(Math.Log10(1 + facts.Seconds / 3600.0) / 4, 0, 1);
        var seed = new CardVisualSeed(color, "balanced", "crisp", facts.Share, intensity, grammar);

        return new WrappedCard(type, headline, subline, entries ?? [], seed) { Facts = facts };
    }

    private static WrappedCardEntry Entry(RankedSong song, int rank, double total)
    {
        return new WrappedCardEntry(rank, song.DisplayTitle,
            $"{Duration(song.TotalListenedSeconds)} · {Plays(song.QualifiedPlayCount)}", EntityId: song.FileId.ToString(),
            Artist: song.DisplayArtist, Seconds: song.TotalListenedSeconds, Plays: song.QualifiedPlayCount,
            Share: song.TotalListenedSeconds / total);
    }
}
