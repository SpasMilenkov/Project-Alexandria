namespace Alexandria.Services.Streaming.Wrapped;

internal sealed class ArtistAccumulator
{
    private readonly DisplayVotes _display = new();

    public long TotalSeconds { get; set; }
    public int SessionCount { get; set; }
    public int QualifiedPlayCount { get; set; }
    public string DisplayName => _display.Pick();
    public int FirstSeenPosition => _display.FirstSeenPosition;

    public void Vote(string display, int position) => _display.Vote(display, position);
}
