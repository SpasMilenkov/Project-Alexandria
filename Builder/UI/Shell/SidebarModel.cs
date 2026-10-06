namespace Builder.UI.Shell;

// Right-pane content model. Screens update stats as they learn things;
// the shell renders whatever is current on every refresh (locked decision D36).
public sealed class SidebarModel
{
    public string StepLabel { get; set; } = "Welcome";

    public List<(string Label, string Value)> Stats { get; } = [];

    public string? SaveWarning { get; set; }

    public static string[] Legend => ["↑↓ move", "Space toggle", "T look", "Enter continue", "Esc back"];
}
