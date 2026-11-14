namespace Builder.UI.Shell.Splash;

// One cell-to-cell stretch of street: its line weight, the second of the
// reveal at which it appears, whether it belongs to the avenues or the
// causeway (drawn in the accent color) and whether the plan only infers it.
internal readonly record struct CityEdge
{
    public required int Weight { get; init; }
    public required double Reveal { get; init; }
    public required bool Major { get; init; }
    public bool Dashed { get; init; }
}
