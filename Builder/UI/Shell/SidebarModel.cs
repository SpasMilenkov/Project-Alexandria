namespace Builder.UI.Shell;

// Right-pane content model. Screens update stats as they learn things; the
// shell renders whatever is current on every refresh.
public sealed class SidebarModel
{
    public List<SidebarStat> Stats { get; } = [];

    public string? SaveWarning { get; set; }
}
