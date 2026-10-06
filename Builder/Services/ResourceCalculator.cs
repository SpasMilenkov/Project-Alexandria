using Builder.Models;

namespace Builder.Services;

public interface IResourceCalculator
{
    long CalculateMinimumRamMb(FeatureSelection features);

    int CountServices(FeatureSelection features);

    List<string> GetWarnings(SystemResources resources, FeatureSelection features);
}

public class ResourceCalculator : IResourceCalculator
{
    // Core stack: postgres, garage, garage-init, rabbitmq(+config), api,
    // nginx, frontend plus headroom
    private const long BaseStackMb = 2560;

    private static readonly Dictionary<string, long> FeatureCostsMb = new()
    {
        [FeatureCatalog.MediaProcessing.Id] = 1024,
        [FeatureCatalog.DocumentPreviews.Id] = 1024,
        [FeatureCatalog.AdaptiveStreaming.Id] = 1024,
        [FeatureCatalog.AudioTaggingFast.Id] = 2048,
        [FeatureCatalog.AudioTaggingDeep.Id] = 2048,
        [FeatureCatalog.Autotagging.Id] = 0,
        [FeatureCatalog.Lyrics.Id] = 512,
        // prometheus + grafana + cadvisor + postgres-exporter + alloy + loki
        [FeatureCatalog.Monitoring.Id] = 1536,
    };

    public long CalculateMinimumRamMb(FeatureSelection features)
    {
        var total = BaseStackMb;

        foreach (var feature in FeatureCatalog.All)
        {
            if (features.IsEnabled(feature.Id) &&
                FeatureCostsMb.TryGetValue(feature.Id, out var cost))
            {
                total += cost;
            }
        }

        return total;
    }

    public int CountServices(FeatureSelection features)
    {
        var count = 8; // postgres, garage, garage-init, rabbitmq-config, rabbitmq, api, frontend, nginx

        if (features.IsEnabled(FeatureCatalog.DocumentPreviews.Id)) count++;
        if (features.IsEnabled(FeatureCatalog.MediaProcessing.Id)) count++;
        if (features.IsEnabled(FeatureCatalog.AdaptiveStreaming.Id)) count++;

        var taggingEnabled =
            features.IsEnabled(FeatureCatalog.AudioTaggingFast.Id) ||
            features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id);

        if (taggingEnabled) count += 1; // media-metadata orchestrator
        if (features.IsEnabled(FeatureCatalog.AudioTaggingFast.Id)) count++;
        if (features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id)) count++;
        if (features.IsEnabled(FeatureCatalog.Lyrics.Id)) count++;

        if (features.IsEnabled(FeatureCatalog.Monitoring.Id)) count += 6;

        return count;
    }

    public List<string> GetWarnings(SystemResources resources, FeatureSelection features)
    {
        var warnings = new List<string>();
        var minimumRam = CalculateMinimumRamMb(features);

        if (resources.TotalMemoryMb > 0 && resources.TotalMemoryMb < minimumRam)
        {
            warnings.Add(
                $"Your system has {resources.TotalMemoryMb} MB of memory but the selected features need at least {minimumRam} MB. " +
                "Services may fail to start or become unstable. You can turn off heavy features like deep audio tagging or monitoring.");
        }
        else if (resources.TotalMemoryMb > 0 && resources.TotalMemoryMb < minimumRam * 1.25)
        {
            warnings.Add(
                $"Your system has {resources.TotalMemoryMb} MB of memory which is close to the minimum of {minimumRam} MB. " +
                "Performance may be limited under load.");
        }

        if (resources.CpuCores < 2)
        {
            warnings.Add(
                "Single-core systems will feel slow. At least 2 CPU cores are recommended.");
        }

        if (features.IsEnabled(FeatureCatalog.AudioTaggingFast.Id) ||
            features.IsEnabled(FeatureCatalog.AudioTaggingDeep.Id))
        {
            if (resources.CpuCores < 4)
            {
                warnings.Add(
                    "Audio tagging works your CPU hard for a while after each upload. " +
                    "At least 4 CPU cores are recommended for a smooth experience.");
            }
        }

        if (resources.AvailableDiskMb > 0 && resources.AvailableDiskMb < 10240)
        {
            warnings.Add(
                $"Only {resources.AvailableDiskMb / 1024.0:F1} GB of disk space is available. " +
                "At least 10 GB is recommended for storing files, the database and Docker images.");
        }

        return warnings;
    }
}
