namespace Builder.UI.Shell.Splash;

// A city block: the cells of the four streets around it and the second its
// construction starts.
internal readonly record struct CityBlock
{
    public required int Left { get; init; }
    public required int Top { get; init; }
    public required int Right { get; init; }
    public required int Bottom { get; init; }
    public required double Start { get; init; }
}
