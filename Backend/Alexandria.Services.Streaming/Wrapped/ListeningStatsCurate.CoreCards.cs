using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming.Stats;
using static Alexandria.Services.Streaming.Wrapped.ListeningStatsFormat;

namespace Alexandria.Services.Streaming.Wrapped;

internal static partial class ListeningStatsCurate
{
    private static WrappedCard TopArtistsCard(RawListeningStats stats)
    {
        var total = (double)stats.TotalListenedSeconds;

        var entries = stats.TopArtists.Take(5)
            .Select((a, i) => new WrappedCardEntry(i + 1, a.DisplayName,
                $"{Duration(a.TotalListenedSeconds)} · {Plays(a.QualifiedPlayCount)}", EntityId: a.Key,
                Seconds: a.TotalListenedSeconds, Plays: a.QualifiedPlayCount, Share: a.TotalListenedSeconds / total))
            .ToList();

        return Card(stats, WrappedCardType.TopArtists, "What your year was built on",
            "The artists you spent the most time with, including every repeat.", "totem",
            new WrappedCardFacts { Seconds = stats.TotalListenedSeconds, Share = stats.Insights.TopArtistShare },
            entries);
    }

    private static WrappedCard TopSongsCard(RawListeningStats stats)
    {
        var total = (double)stats.TotalListenedSeconds;
        var entries = stats.TopSongs.Take(5).Select((s, i) => Entry(s, i + 1, total)).ToList();

        return Card(stats, WrappedCardType.TopSongs, "The tracks that defined it",
            "The songs you spent the most time with, including every repeat.", "totem",
            new WrappedCardFacts { Seconds = stats.TotalListenedSeconds, Share = stats.Insights.TopSongShare },
            entries);
    }

    private static WrappedCard ListeningTimeCard(RawListeningStats stats, string visualIdentity)
    {
        var comparisons = ListeningComparisons.Select(stats.TotalListenedSeconds, visualIdentity);
        var activeDays = stats.Insights.ActiveDays;

        return Card(stats, WrappedCardType.ListeningTime,
            $"{Duration(stats.TotalListenedSeconds)}. Entirely your sound.",
            $"{Duration(stats.TotalListenedSeconds)} of music · {activeDays} {Plural(activeDays, "listening day", "listening days")}",
            "landscape", new WrappedCardFacts
            {
                Seconds = stats.TotalListenedSeconds, Count = activeDays,
                Series = stats.Insights.Months, Weights = stats.SecondsByHourUtc,
                Comparison = comparisons.FirstOrDefault(),
                ComparisonAlternatives = comparisons.Skip(1).ToList(),
                Detail =
                    "Every listen adds up, from a quick song to a long evening of favorites. The milestone puts all that time into perspective."
            });
    }

    private static WrappedCard PersonaCard(RawListeningStats stats)
    {
        var rhythm = ListeningRhythm.Select(stats);

        var (name, habit) = rhythm.Evidence switch
        {
            WrappedRhythmEvidence.Sparse => ("Your rhythm is taking shape",
                "A few more listening days will reveal the rhythm of your music."),
            WrappedRhythmEvidence.Pronounced => (PersonaName(rhythm.Scene),
                PronouncedHabit(stats, rhythm.Scene, rhythm.Share)),
            _ => ("Music on your own schedule",
                "A little morning, a little midnight. You made room for music throughout the day.")
        };

        return Card(stats, WrappedCardType.Persona, name, habit, "sky",
            new WrappedCardFacts
            {
                Weights = stats.SecondsByHourUtc, Share = rhythm.Share,
                Count = stats.QualifiedPlayCount, Rhythm = rhythm,
                Detail =
                    "A glimpse of when music most often found its way into your day. The scene brings that rhythm to life."
            });
    }

    private static string PersonaName(WrappedTimeScene? scene)
    {
        return scene switch
        {
            WrappedTimeScene.Night => "Late nights have your sound",
            WrappedTimeScene.Morning => "Early mornings have your sound",
            WrappedTimeScene.Midday => "Midday has your sound",
            WrappedTimeScene.Afternoon => "Afternoons have your sound",
            _ => "Evenings have your sound"
        };
    }

    private static string PronouncedHabit(RawListeningStats stats, WrappedTimeScene? scene, double share)
    {
        var window = scene switch
        {
            WrappedTimeScene.Night => "the late-night hours",
            WrappedTimeScene.Morning => "the morning",
            WrappedTimeScene.Midday => "the middle of the day",
            WrappedTimeScene.Afternoon => "the afternoon",
            _ => "the evening"
        };

        var habit = $"{Percent(share)} of your listening found its home in {window}.";

        var weekendShare = stats.WeekendSeconds / (double)stats.TotalListenedSeconds;

        if (weekendShare >= 0.6) habit += " Mostly on weekends.";
        else if (weekendShare <= 0.1) habit += " Mostly on weekdays.";

        return habit;
    }

    private static WrappedCard BookendsCard(RawListeningStats stats)
    {
        var first = stats.FirstSession!;
        var last = stats.LastSession!;
        var single = stats.SessionCount == 1;

        var entries = new[] { first, last }.Take(single ? 1 : 2).Select((b, i) =>
        {
            var song = stats.TopSongs.First(s => s.FileId == b.FileId);
            var label = i == 0 ? "First listen" : "Latest listen";

            return new WrappedCardEntry(i + 1, song.DisplayTitle, $"{label} · {Day(b.StartedAt)}",
                Iso(b.StartedAt), song.FileId.ToString(), song.DisplayArtist);
        }).ToList();

        return Card(stats, WrappedCardType.Bookends,
            single ? "The opening note" : "From the first note to the latest",
            single ? "One listen. The beginning of your story." : "The tracks at the edges of this chapter.", "vinyl",
            new WrappedCardFacts
            {
                From = Iso(first.StartedAt), To = Iso(last.StartedAt), Series = stats.Insights.Days,
                Count = stats.Insights.ActiveDays,
                Detail = "The songs at the edges of this chapter: your first listen and the latest one in this recap."
            }, entries);
    }
}
