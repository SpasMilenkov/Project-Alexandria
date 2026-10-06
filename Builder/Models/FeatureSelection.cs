namespace Builder.Models;

// Tracks which optional features the user switched on in the TUI. Ids match
// FeatureCatalog entries and are stable across saved configurations.
public class FeatureSelection
{
    public HashSet<string> Enabled { get; set; } = [];

    public static FeatureSelection FromDefaults()
    {
        var selection = new FeatureSelection();

        foreach (var feature in FeatureCatalog.All.Where(f => f.DefaultEnabled))
        {
            selection.Enabled.Add(feature.Id);
        }

        return selection;
    }

    public bool IsEnabled(string featureId) => Enabled.Contains(featureId);

    public void SetEnabled(string featureId, bool enabled)
    {
        if (enabled)
        {
            Enabled.Add(featureId);
        }
        else
        {
            Enabled.Remove(featureId);
        }
    }
}
