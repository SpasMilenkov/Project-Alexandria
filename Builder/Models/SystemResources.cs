namespace Builder.Models;

public class SystemResources
{
    public int CpuCores { get; set; }
    public long TotalMemoryMb { get; set; }
    public long AvailableMemoryMb { get; set; }
    public long AvailableDiskMb { get; set; }
}
