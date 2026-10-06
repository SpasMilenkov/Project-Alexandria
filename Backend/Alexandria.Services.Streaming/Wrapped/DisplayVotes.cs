namespace Alexandria.Services.Streaming.Wrapped;

/// <summary>Picks a display spelling by most votes, breaking ties by first-seen position.</summary>
internal sealed class DisplayVotes
{
    private readonly Dictionary<string, int> _votes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _firstSeen = new(StringComparer.Ordinal);

    public int FirstSeenPosition { get; private set; } = int.MaxValue;

    public void Vote(string display, int position)
    {
        _votes[display] = _votes.GetValueOrDefault(display) + 1;
        _firstSeen.TryAdd(display, position);
        FirstSeenPosition = Math.Min(FirstSeenPosition, _firstSeen[display]);
    }

    public string Pick()
    {
        return _votes
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => _firstSeen[kv.Key])
            .First().Key;
    }
}
