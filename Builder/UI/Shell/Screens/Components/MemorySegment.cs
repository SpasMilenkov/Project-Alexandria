using Spectre.Console;

namespace Builder.UI.Shell.Screens.Components;

// One slice of the memory bar: a service group's estimate, its color, and
// whether it belongs to the feature under the cursor or would only be added
// if that feature were turned on.
internal readonly record struct MemorySegment
{
    public required string Label { get; init; }
    public required long MemoryMb { get; init; }
    public required Color Color { get; init; }
    public bool Focused { get; init; }
    public bool Preview { get; init; }
}
