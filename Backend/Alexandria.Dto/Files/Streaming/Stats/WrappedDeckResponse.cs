namespace Alexandria.Dto.Files.Streaming.Stats;

/// <summary>On-demand Wrapped response: the applied range plus its curated deck.</summary>
public sealed record WrappedDeckResponse(DateTime From, DateTime To, CuratedDeck Deck)
{
    public int SchemaVersion { get; init; } = 3;
    public int RecipeVersion { get; init; } = 2;
    public DateTime GeneratedAt { get; init; }
    public string VisualIdentity { get; init; } = "";
    public WrappedSummaryFacts Summary { get; init; } = new();
}

public sealed record WrappedSummaryFacts
{
    public long Seconds { get; init; }
    public int Sessions { get; init; }
    public int Tracks { get; init; }
    public int Artists { get; init; }
    public int ActiveDays { get; init; }
    public double KnownArtistShare { get; init; }
    public bool HasPriorHistory { get; init; }
    public DateTime? HistoryFrom { get; init; }
}