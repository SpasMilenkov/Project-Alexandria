namespace Alexandria.Services.Streaming.Wrapped;

internal sealed class SongAccumulator(string title, int ordinal)
{
    private readonly DisplayVotes _artist = new();
    private bool _hasArtist;

    public string Title { get; } = title;
    public int Ordinal { get; } = ordinal;
    public long TotalSeconds { get; set; }
    public int SessionCount { get; set; }
    public int QualifiedPlayCount { get; set; }
    public string? DisplayArtist => _hasArtist ? _artist.Pick() : null;
    public int FirstSeenPosition => Ordinal;

    public void VoteArtist(string display, int position)
    {
        _hasArtist = true;
        _artist.Vote(display, position);
    }
}
