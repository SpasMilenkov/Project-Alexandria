using Builder.Models;

namespace Builder.Services;

public interface IResourceCalculator
{
    long CalculateMinimumRamMb(FeatureSelection features);
    List<string> GetWarnings(SystemResources resources, FeatureSelection features);
}

public class ResourceCalculator : IResourceCalculator
{
    private const long BaseStackMb = 2560; // postgres 512 + garage 512 + rabbitmq 512 + api 512 + doc-worker 512 + frontend 128 + headroom
    private const long MediaWorkerMb = 1024;
    private const long MonitoringMb = 512; // prometheus 256 + grafana 256

    public long CalculateMinimumRamMb(FeatureSelection features)
    {
        var total = BaseStackMb;

        if (features.MediaProcessing)
            total += MediaWorkerMb;

        if (features.Monitoring)
            total += MonitoringMb;

        return total;
    }

    public List<string> GetWarnings(SystemResources resources, FeatureSelection features)
    {
        var warnings = new List<string>();
        var minimumRam = CalculateMinimumRamMb(features);

        if (resources.TotalMemoryMb < minimumRam)
        {
            warnings.Add(
                $"Your system has {resources.TotalMemoryMb} MB RAM but the selected configuration needs at least {minimumRam} MB. " +
                "Services may fail to start or become unstable.");
        }
        else if (resources.TotalMemoryMb < minimumRam * 1.25)
        {
            warnings.Add(
                $"Your system has {resources.TotalMemoryMb} MB RAM which is close to the minimum {minimumRam} MB. " +
                "Performance may be limited under load.");
        }

        if (resources.CpuCores < 2)
        {
            warnings.Add(
                "Single-core systems will experience degraded performance. " +
                "At least 2 CPU cores are recommended.");
        }

        if (features.MediaProcessing && resources.CpuCores < 4)
        {
            warnings.Add(
                "Media processing (audio/video transcoding) is CPU-intensive. " +
                "At least 4 CPU cores are recommended for smooth media processing.");
        }

        if (resources.AvailableDiskMb > 0 && resources.AvailableDiskMb < 10240)
        {
            warnings.Add(
                $"Only {resources.AvailableDiskMb / 1024.0:F1} GB of disk space available. " +
                "At least 10 GB is recommended for storing files, database, and Docker images.");
        }

        return warnings;
    }
}
