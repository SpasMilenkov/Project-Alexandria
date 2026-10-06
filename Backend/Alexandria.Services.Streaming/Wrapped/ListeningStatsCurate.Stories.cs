using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using static Alexandria.Services.Streaming.Wrapped.ListeningStatsFormat;

namespace Alexandria.Services.Streaming.Wrapped;

internal static partial class ListeningStatsCurate
{
    private static StoryCandidate? ExplorationCandidate(RawListeningStats stats)
    {
        var insight = stats.Insights;

        if (!insight.HasPriorHistory || insight.ActiveDays < 5 || stats.TopSongs.Count < 5) return null;

        var card = Card(stats, WrappedCardType.Exploration, ExplorationHeadline(insight.NewListeningShare),
            $"{Percent(insight.NewListeningShare)} of your listening went to tracks first heard in this period.",
            "orbit",
            new WrappedCardFacts
            {
                Share = insight.NewListeningShare, Count = stats.DiscoveryCount,
                Detail =
                    "A mix of fresh finds and familiar favorites. A song is a fresh find when you play it in Alexandria for the first time."
            });

        return new StoryCandidate(card, "discovery", 75, 30);
    }

    private static string ExplorationHeadline(double share)
    {
        if (share >= 0.6) return "You made room for the unfamiliar";
        if (share <= 0.25) return "Some favorites never get old";

        return "New paths. Familiar places.";
    }

    private static StoryCandidate? RetainedDiscoveryCandidate(RawListeningStats stats)
    {
        var insight = stats.Insights;

        if (insight.RetainedDiscovery is not { } discovery || !insight.HasPriorHistory) return null;

        var card = Card(stats, WrappedCardType.RetainedDiscovery, "A new find. A lasting favorite.",
            $"{discovery.Title} found its way into {discovery.ActiveDays} different days.", "constellation",
            new WrappedCardFacts
            {
                Seconds = discovery.Seconds, Count = discovery.ActiveDays,
                Share = discovery.Seconds / (double)stats.TotalListenedSeconds, From = Iso(discovery.FirstHeard),
                Detail = $"First played in Alexandria on {Day(discovery.FirstHeard)}. You kept finding your way back to it on different days."
            },
            [
                new WrappedCardEntry(1, discovery.Title, Duration(discovery.Seconds), Iso(discovery.FirstHeard),
                    discovery.FileId.ToString(), discovery.Artist, discovery.Seconds)
            ]);

        return new StoryCandidate(card, "discovery", 95, 30);
    }

    private static StoryCandidate? NewArtistsCandidate(RawListeningStats stats)
    {
        var insight = stats.Insights;

        if (stats.NewArtists.Count < 3 || !insight.HasPriorHistory) return null;

        var entries = stats.NewArtists.Take(5)
            .Select((a, i) => new WrappedCardEntry(i + 1, a.DisplayName, null, EntityId: a.Key))
            .ToList();

        var card = Card(stats, WrappedCardType.NewArtists, $"{stats.NewArtists.Count} new voices in your rotation",
            "New voices that joined your Alexandria rotation during this chapter.", "constellation",
            new WrappedCardFacts { Count = stats.NewArtists.Count, Share = insight.NewListeningShare }, entries);

        return new StoryCandidate(card, "discovery", 65, 30);
    }

    private static StoryCandidate? DiscoveriesCandidate(RawListeningStats stats)
    {
        var insight = stats.Insights;

        if (stats.DiscoveryCount < 5 || !insight.HasPriorHistory) return null;

        var card = Card(stats, WrappedCardType.Discoveries, $"{stats.DiscoveryCount} first listens",
            "Songs you played in Alexandria for the first time during this chapter.", "constellation",
            new WrappedCardFacts { Count = stats.DiscoveryCount, Share = insight.NewListeningShare });

        return new StoryCandidate(card, "discovery", 55, 30);
    }

    private static StoryCandidate? ChaptersCandidate(RawListeningStats stats)
    {
        var insight = stats.Insights;

        var changes = insight.Chapters.Zip(insight.Chapters.Skip(1))
            .Count(pair => pair.First.LeaderKey != pair.Second.LeaderKey);

        if (changes == 0) return null;

        var entries = insight.Chapters
            .Select((m, i) => new WrappedCardEntry(i + 1, m.Leader!, MonthName(m.Date), m.Date, m.LeaderKey,
                Seconds: m.Seconds, Plays: m.Plays))
            .ToList();

        var card = Card(stats, WrappedCardType.Chapters, "Your soundtrack had chapters",
            $"The leading artist changed {changes} {Plural(changes, "time", "times")} across your active months.",
            "ribbons",
            new WrappedCardFacts
            {
                Count = changes, Series = insight.Months,
                Detail =
                    "Different artists took the lead as your year unfolded. These chapters highlight the months you spent enough time listening for a favorite to stand out."
            }, entries);

        return new StoryCandidate(card, "chapters", 90, 40);
    }

    private static StoryCandidate? ReturningFavoriteCandidate(RawListeningStats stats)
    {
        if (stats.Insights.ReturningFavorite is not { } returning) return null;

        var card = Card(stats, WrappedCardType.ReturningFavorite,
            $"After {returning.GapDays} days, {returning.Title} came back",
            "Then it stayed for another listening day.", "heartbeat",
            new WrappedCardFacts
            {
                Count = returning.GapDays, From = Iso(returning.PreviousPlay), To = Iso(returning.ReturnedAt),
                Detail = "You gave this song a break, then welcomed it back on more than one day. The quiet stretch shows the time between those listens in Alexandria."
            },
            [
                new WrappedCardEntry(1, returning.Title, returning.Artist, Iso(returning.ReturnedAt),
                    returning.FileId.ToString(), returning.Artist)
            ]);

        return new StoryCandidate(card, "return", 85, 45);
    }

    private static StoryCandidate? StreakCandidate(RawListeningStats stats)
    {
        if (stats.LongestStreakDays < 7) return null;

        var start = stats.StreakStart!.Value;
        var end = stats.StreakEnd!.Value;

        var card = Card(stats, WrappedCardType.Streak, "You kept the music going",
            $"{stats.LongestStreakDays} listening days in a row · {Day(start)} to {Day(end)}",
            "calendar", new WrappedCardFacts
            {
                Count = stats.LongestStreakDays, From = IsoDate(start), To = IsoDate(end),
                Series = stats.Insights.Days,
                Detail =
                    "Your longest stretch of days with music. The outlined dates belong to that streak, and deeper colors show days with more listening."
            });

        return new StoryCandidate(card, "consistency", 80, 50);
    }

    private static StoryCandidate? BusiestDayCandidate(RawListeningStats stats)
    {
        var insight = stats.Insights;

        if (stats.BusiestDay is not { } busiest || insight.ActiveDays < 7 || insight.MedianDaySeconds <= 0
            || stats.BusiestDaySeconds < 3600) return null;

        var ratio = stats.BusiestDaySeconds / insight.MedianDaySeconds;

        if (ratio < 1.5) return null;

        var card = Card(stats, WrappedCardType.BusiestDay, $"{Day(busiest)} turned up the volume",
            $"{Number(ratio)}× your usual listening day.", "waveform",
            new WrappedCardFacts
            {
                Seconds = stats.BusiestDaySeconds, BaselineSeconds = insight.MedianDaySeconds,
                From = IsoDate(busiest), Series = insight.Days,
                Detail =
                    $"You spent {Duration(stats.BusiestDaySeconds)} with music that day, compared with {Duration((long)insight.MedianDaySeconds)} on a typical listening day."
            });

        return new StoryCandidate(card, "record", 75, 55);
    }

    private static StoryCandidate? MostReplayedCandidate(RawListeningStats stats)
    {
        if (stats.MostReplayed is not { QualifiedPlayCount: >= 5 } replay) return null;

        var isTopSong = replay.FileId == stats.TopSongs[0].FileId;
        var isLoyalFavorite = replay.QualifiedPlayCount >= 10 && replay.ActiveDays >= 3;

        if (isTopSong && !isLoyalFavorite) return null;

        var total = (double)stats.TotalListenedSeconds;
        var song = stats.TopSongs.First(s => s.FileId == replay.FileId);

        var card = Card(stats, WrappedCardType.MostReplayed, $"{song.DisplayTitle}, {replay.QualifiedPlayCount} times",
            $"You kept coming back · {replay.ActiveDays} different listening days", "tally",
            new WrappedCardFacts
            {
                Count = replay.QualifiedPlayCount, Seconds = song.TotalListenedSeconds,
                Share = song.TotalListenedSeconds / total,
                Detail =
                    "Each mark is another listen to this song. A quick listen counts too; you didn't have to finish it every time."
            },
            [Entry(song, 1, total)]);

        return new StoryCandidate(card, "replay", 70, 60);
    }
}
