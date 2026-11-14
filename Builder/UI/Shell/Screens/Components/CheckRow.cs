namespace Builder.UI.Shell.Screens.Components;

// One line of the system check: what was measured and, for capacity checks,
// what the current selection needs from it
public sealed record CheckRow
{
    public required string Label { get; init; }
    public CheckState State { get; init; } = CheckState.Pending;
    public string? Detail { get; init; }
    public string? Requirement { get; init; }
}
