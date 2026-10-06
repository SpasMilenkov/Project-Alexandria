namespace Builder.Models;

public class SystemResources
{
    public int CpuCores { get; set; }
    public long TotalMemoryMb { get; set; }
    public long AvailableMemoryMb { get; set; }
    public long AvailableDiskMb { get; set; }
    public int? DockerCpuCores { get; set; }
    public long? DockerMemoryMb { get; set; }
    public string DockerDataDirectory { get; set; } = string.Empty;
    public long? DockerAvailableDiskMb { get; set; }
    public string DiskSpaceDetails { get; set; } = "Docker storage could not be measured.";
}
