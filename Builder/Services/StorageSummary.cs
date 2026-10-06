using Builder.Models;

namespace Builder.Services;

public static class StorageSummary
{
    public static IReadOnlyList<string> Describe(InstallationConfig config, SystemResources resources)
    {
        var settings = config.Storage;
        string capacity;

        if (settings.ActualGarageCapacityBytes.HasValue)
        {
            capacity = $"Garage layout: {settings.ActualGarageCapacityBytes.Value / (double)StorageSettings.BytesPerGb:0.###} GB, preserved.";
        }
        else if (settings.PreserveExistingCapacity)
        {
            capacity = "Garage layout: existing capacity will be read and preserved during initialization.";
        }
        else
        {
            capacity = $"Garage layout: {settings.GarageCapacityGb} GB for the new installation.";
        }
        var available = resources.DockerAvailableDiskMb.HasValue
            ? $"Docker data filesystem free: {resources.DockerAvailableDiskMb.Value / 1024.0:0.#} GiB ({resources.DockerDataDirectory})."
            : $"Docker data filesystem free: unknown. {resources.DiskSpaceDetails}";

        return
        [
            capacity,
            "This layout capacity does not reserve disk space or enforce per-user quotas.",
            available,
            "Database and file data use Docker-managed volumes; setup settings and verified model cache use the installation folder.",
            "Leave additional free space for existing data, temporary processing, container images, models, and backups. User quotas stay in Alexandria's settings.",
        ];
    }
}
