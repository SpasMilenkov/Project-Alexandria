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
    public long CalculateMinimumRamMb(FeatureSelection features) => ServiceCatalog.Resolve(features).MinimumRamMb;

    public int CountServices(FeatureSelection features) => ServiceCatalog.Resolve(features).Services.Count;

    public List<string> GetWarnings(SystemResources resources, FeatureSelection features)
    {
        var warnings = new List<string>();
        var minimumRam = CalculateMinimumRamMb(features);
        var memoryMb = resources.DockerMemoryMb ?? resources.TotalMemoryMb;
        var cpuCores = resources.DockerCpuCores ?? resources.CpuCores;
        var memorySource = resources.DockerMemoryMb.HasValue ? "Docker" : "Your host";

        if (memoryMb > 0 && memoryMb < minimumRam)
        {
            warnings.Add(
                $"{memorySource} has {memoryMb} MB of memory but the selected features need at least {minimumRam} MB. " +
                "Services may fail to start or become unstable. You can turn off heavy features like deep audio tagging or monitoring.");
        }
        else if (memoryMb > 0 && memoryMb < minimumRam * 1.25)
        {
            warnings.Add(
                $"{memorySource} has {memoryMb} MB of memory which is close to the minimum of {minimumRam} MB. " +
                "Performance may be limited under load.");
        }

        if (cpuCores < 2)
        {
            warnings.Add(
                "Single-core systems will feel slow. At least 2 CPU cores are recommended.");
        }

        if (ServiceCatalog.Resolve(features).AudioAnalysisEnabled)
        {
            if (cpuCores < 4)
            {
                warnings.Add(
                    "Audio tagging works your CPU hard for a while after each upload. " +
                    "At least 4 CPU cores are recommended for a smooth experience.");
            }
        }

        if (resources.DockerAvailableDiskMb.GetValueOrDefault() > 0 && resources.DockerAvailableDiskMb.GetValueOrDefault() < 10240)
        {
            warnings.Add(
                $"Only {resources.DockerAvailableDiskMb.GetValueOrDefault() / 1024.0:F1} GB of disk space is available. " +
                "At least 10 GB is recommended for storing files, the database and Docker images.");
        }

        return warnings;
    }
}
