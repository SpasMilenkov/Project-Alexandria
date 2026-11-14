namespace Builder.Services;

// What setup will do with storage, split so screens can lay it out: the
// Garage layout, aligned facts about where data lives, and the caveats.
// The layout stands apart because the capacity field shows its own value.
public sealed record StorageOverview
{
    public required string Layout { get; init; }
    public required IReadOnlyList<(string Label, string Value)> Facts { get; init; }
    public required IReadOnlyList<string> Points { get; init; }
}
