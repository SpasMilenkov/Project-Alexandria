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
    public const int MinimumCpuCores = 2;
    public const int AudioAnalysisCpuCores = 4;
    public const long MinimumDockerDiskMb = 10240;

    // Below this multiple of the estimate the selection fits, but without
    // room for load spikes
    public const double MemoryComfortFactor = 1.25;

    public long CalculateMinimumRamMb(FeatureSelection features) => ServiceCatalog.Resolve(features).MinimumRamMb;

    public int CountServices(FeatureSelection features) => ServiceCatalog.Resolve(features).Services.Count;

    // Docker's own limits when it reports them, otherwise the host totals
    public static long MemoryLimitMb(SystemResources resources) => resources.DockerMemoryMb ?? resources.TotalMemoryMb;

    public static int CpuLimit(SystemResources resources) => resources.DockerCpuCores ?? resources.CpuCores;

    public static int RecommendedCpuCores(FeatureSelection features) =>
        ServiceCatalog.Resolve(features).AudioAnalysisEnabled ? AudioAnalysisCpuCores : MinimumCpuCores;

    public List<string> GetWarnings(SystemResources resources, FeatureSelection features)
    {
        var warnings = new List<string>();
        var minimumRam = CalculateMinimumRamMb(features);
        var memoryMb = MemoryLimitMb(resources);
        var cpuCores = CpuLimit(resources);
        var memorySource = resources.DockerMemoryMb.HasValue ? "Docker" : "This computer";

        if (memoryMb > 0 && memoryMb < minimumRam)
        {
            warnings.Add(
                $"{memorySource} has {Gigabytes(memoryMb)} of memory, less than the {Gigabytes(minimumRam)} this selection needs. " +
                "Services may fail to start or become unstable. Turn off heavy features like audio tagging or monitoring to make room.");
        }
        else if (memoryMb > 0 && memoryMb < minimumRam * MemoryComfortFactor)
        {
            warnings.Add(
                $"Memory is close to the {Gigabytes(minimumRam)} this selection needs. " +
                "Performance may be limited under load.");
        }

        if (cpuCores < MinimumCpuCores)
        {
            warnings.Add(
                $"Single-core systems will feel slow. At least {MinimumCpuCores} CPU cores are recommended.");
        }

        if (ServiceCatalog.Resolve(features).AudioAnalysisEnabled && cpuCores < AudioAnalysisCpuCores)
        {
            warnings.Add(
                "Audio tagging works your CPU hard for a while after each upload. " +
                $"At least {AudioAnalysisCpuCores} CPU cores are recommended for a smooth experience.");
        }

        if (resources.DockerAvailableDiskMb is { } freeMb && freeMb < MinimumDockerDiskMb)
        {
            warnings.Add(
                $"Only {Gigabytes(freeMb)} of disk is free for Docker. " +
                $"At least {Gigabytes(MinimumDockerDiskMb)} is recommended for files, the database and Docker images.");
        }

        return warnings;
    }

    private static string Gigabytes(long megabytes) => $"{megabytes / 1024.0:0.#} GB";
}
