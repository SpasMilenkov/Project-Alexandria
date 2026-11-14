namespace Builder.UI.Shell.Splash;

// A label after placement, in cells; a street name is drawn over its street.
internal readonly record struct PlacedLabel
{
    public required string Text { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public bool OnStreet { get; init; }
}
