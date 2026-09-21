namespace Alexandria.Dto.Files.Streaming.Shuffle;

public sealed record ShuffleCandidate
{
    public PlaybackSourceEntryRef Entry { get; init; } = null!;
    public double? DurationSeconds { get; init; }
}