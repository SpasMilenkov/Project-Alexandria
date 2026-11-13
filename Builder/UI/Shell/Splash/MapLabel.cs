namespace Builder.UI.Shell.Splash;

// A place name and the map point it is centred on; water names only go on
// water and land names only on open land, so neither hides the streets.
internal readonly record struct MapLabel
{
    public required string Text { get; init; }
    public required double U { get; init; }
    public required double V { get; init; }
    public required bool OnWater { get; init; }
}
