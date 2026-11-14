namespace Builder.UI.Shell.Splash;

// A street after projection: the first and last cell it covers along its
// own direction, and whether it is one of the two avenues.
internal readonly record struct StreetCells
{
    public required int From { get; init; }
    public required int To { get; init; }
    public bool Major { get; init; }
    public bool Conjectured { get; init; }

    public bool Covers(int cell) => cell >= From && cell <= To;
}
