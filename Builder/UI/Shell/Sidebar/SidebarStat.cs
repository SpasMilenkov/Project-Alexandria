namespace Builder.UI.Shell.Sidebar;

// One label/value line in the sidebar's stats block
public sealed record SidebarStat
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public StatTone Tone { get; init; } = StatTone.Normal;
}
