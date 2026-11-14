using Builder.Models;

namespace Builder.Services;

public static class StorageSummary
{
    public const string PreservedLayout = "existing size, read and preserved during setup";

    public static StorageOverview Describe(InstallationConfig config, SystemResources resources)
    {
        var settings = config.Storage;
        string layout;

        if (settings.ActualGarageCapacityBytes.HasValue)
        {
            layout = $"{settings.ActualGarageCapacityBytes.Value / (double)StorageSettings.BytesPerGb:0.###} GB · preserved";
        }
        else if (settings.PreserveExistingCapacity)
        {
            layout = PreservedLayout;
        }
        else
        {
            layout = $"{settings.GarageCapacityGb} GB, new layout";
        }

        var disk = resources.DockerAvailableDiskMb.HasValue
            ? $"{resources.DockerAvailableDiskMb.Value / 1024.0:0} GB free at {resources.DockerDataDirectory}"
            : $"unknown · {resources.DiskSpaceDetails}";

        return new StorageOverview
        {
            Layout = layout,
            Facts =
            [
                ("Docker disk", disk),
                ("Files & database", "Docker-managed volumes"),
                ("Settings & models", "the installation folder"),
            ],
            Points =
            [
                "The layout size does not reserve disk space or set user quotas.",
                "User quotas stay in Alexandria's settings.",
                "Leave room for existing data, previews, temporary processing, container images, models and backups.",
            ],
        };
    }
}
