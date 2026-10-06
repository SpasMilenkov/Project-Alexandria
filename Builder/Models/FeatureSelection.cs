using System.Text.Json.Serialization;

namespace Builder.Models;

// Tracks which optional features the user switched on in the TUI. Ids match
// FeatureCatalog entries and are stable across saved configurations.
public class FeatureSelection
{
    public HashSet<string> Enabled { get; set; } = [];

    [JsonIgnore]
    public string AudioBackbone
    {
        get
        {
            if (Enabled.Contains(FeatureCatalog.AudioTaggingFast.Id)) return "effnet";
            if (Enabled.Contains(FeatureCatalog.AudioTaggingDeep.Id)) return "maest";

            return "off";
        }
    }

    [JsonIgnore]
    public bool HasLegacyDualAudioSelection => Enabled.Contains(FeatureCatalog.AudioTaggingFast.Id) &&
        Enabled.Contains(FeatureCatalog.AudioTaggingDeep.Id);

    public static FeatureSelection FromDefaults()
    {
        var selection = new FeatureSelection();

        foreach (var feature in FeatureCatalog.All.Where(f => f.DefaultEnabled))
        {
            selection.Enabled.Add(feature.Id);
        }

        return selection;
    }

    public bool IsEnabled(string featureId)
    {
        if (featureId == FeatureCatalog.AudioTaggingFast.Id) return AudioBackbone == "effnet";
        if (featureId == FeatureCatalog.AudioTaggingDeep.Id) return AudioBackbone == "maest";

        return Enabled.Contains(featureId);
    }

    public void SetAudioBackbone(string backbone)
    {
        if (backbone is not ("off" or "effnet" or "maest"))
            throw new ArgumentException("Choose off, effnet, or maest.", nameof(backbone));

        Enabled.Remove(FeatureCatalog.AudioTaggingFast.Id);
        Enabled.Remove(FeatureCatalog.AudioTaggingDeep.Id);

        if (backbone == "effnet") Enabled.Add(FeatureCatalog.AudioTaggingFast.Id);
        if (backbone == "maest") Enabled.Add(FeatureCatalog.AudioTaggingDeep.Id);
    }

    public void SetEnabled(string featureId, bool enabled)
    {
        if (enabled)
        {
            if (featureId == FeatureCatalog.AudioTaggingFast.Id)
            {
                SetAudioBackbone("effnet");
                return;
            }

            if (featureId == FeatureCatalog.AudioTaggingDeep.Id)
            {
                SetAudioBackbone("maest");
                return;
            }

            Enabled.Add(featureId);
        }
        else
        {
            Enabled.Remove(featureId);
        }
    }
}
