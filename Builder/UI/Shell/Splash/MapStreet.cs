namespace Builder.UI.Shell.Splash;

// A street of the plan: its position across the other axis and the stretch
// it covers along its own, all in map units.
internal readonly record struct MapStreet
{
    public required double Position { get; init; }
    public required double From { get; init; }
    public required double To { get; init; }
    public bool Major { get; init; }

    // Drawn dashed on the plan: a street Puchstein inferred rather than found
    public bool Conjectured { get; init; }
}
