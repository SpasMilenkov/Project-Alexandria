using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>Running totals for one pass over the period's session rows.</summary>
internal sealed class ListeningSessionTotals
{
    private int _songSequence;
    private int _votePosition;

    public long TotalSeconds { get; private set; }
    public long WeekdaySeconds { get; private set; }
    public long WeekendSeconds { get; private set; }
    public int CompletedSessions { get; private set; }
    public int QualifiedPlayCount { get; private set; }

    public long[] SecondsByHour { get; } = new long[24];
    public long[] SecondsByBucket { get; } = new long[4];
    public Dictionary<DateOnly, long> SecondsByDay { get; } = new();
    public Dictionary<string, ArtistAccumulator> Artists { get; } = new(StringComparer.Ordinal);
    public Dictionary<Guid, SongAccumulator> Songs { get; } = new();

    public ListeningSessionRow? LongestRow { get; private set; }
    public ListeningSessionRow? FirstRow { get; private set; }
    public ListeningSessionRow? LastRow { get; private set; }

    public void Add(ListeningSessionRow row)
    {
        var start = row.StartedAt.ToUniversalTime();

        TotalSeconds += row.ListenedSeconds;

        if (row.IsQualifiedPlay()) QualifiedPlayCount++;

        if (row.PlaybackFinished) CompletedSessions++;

        SecondsByHour[start.Hour] += row.ListenedSeconds;
        SecondsByBucket[(int)ListeningStatsCompute.BucketForHour(start.Hour)] += row.ListenedSeconds;

        if (start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) WeekendSeconds += row.ListenedSeconds;
        else WeekdaySeconds += row.ListenedSeconds;

        var day = DateOnly.FromDateTime(start);

        SecondsByDay[day] = SecondsByDay.GetValueOrDefault(day) + row.ListenedSeconds;

        var artistKey = ListeningStatsCompute.NormalizeArtist(row.Artist);
        var artistName = artistKey is null ? null : row.Artist!.Trim();

        if (artistKey is not null) AddArtist(artistKey, artistName!, row);

        AddSong(row, artistName);

        TrackExtremes(row, start);
    }

    private void AddArtist(string key, string name, ListeningSessionRow row)
    {
        if (!Artists.TryGetValue(key, out var artist))
        {
            artist = new ArtistAccumulator();
            Artists[key] = artist;
        }

        artist.TotalSeconds += row.ListenedSeconds;
        artist.SessionCount++;

        if (row.IsQualifiedPlay()) artist.QualifiedPlayCount++;

        artist.Vote(name, _votePosition++);
    }

    private void AddSong(ListeningSessionRow row, string? artistName)
    {
        if (!Songs.TryGetValue(row.FileId, out var song))
        {
            song = new SongAccumulator(ListeningStatsCompute.DisplayTitle(row.Title, row.FileName), _songSequence++);
            Songs[row.FileId] = song;
        }

        song.TotalSeconds += row.ListenedSeconds;
        song.SessionCount++;

        if (row.IsQualifiedPlay()) song.QualifiedPlayCount++;

        if (artistName is not null) song.VoteArtist(artistName, _votePosition++);
    }

    private void TrackExtremes(ListeningSessionRow row, DateTime start)
    {
        if (ReplacesBest(LongestRow, row, static (best, current) => current.ListenedSeconds > best.ListenedSeconds))
            LongestRow = row;

        if (ReplacesBest(FirstRow, row,
                static (best, current) => current.StartedAt.ToUniversalTime() < best.StartedAt.ToUniversalTime()))
            FirstRow = row;

        if (LastRow is null
            || start > LastRow.StartedAt.ToUniversalTime()
            || (start == LastRow.StartedAt.ToUniversalTime() && row.SessionId.CompareTo(LastRow.SessionId) > 0))
            LastRow = row;
    }

    /// <summary>A candidate replaces the current best if it wins outright, or ties and has the smaller session id.</summary>
    private static bool ReplacesBest(ListeningSessionRow? best, ListeningSessionRow candidate,
        Func<ListeningSessionRow, ListeningSessionRow, bool> wins)
    {
        if (best is null) return true;
        if (wins(best, candidate)) return true;
        if (wins(candidate, best)) return false;

        return candidate.SessionId.CompareTo(best.SessionId) < 0;
    }
}
