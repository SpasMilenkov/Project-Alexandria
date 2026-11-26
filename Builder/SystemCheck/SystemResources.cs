namespace Builder.SystemCheck;

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

    // Render nodes video may be encoded on, first choice first. Detection fills it; the GPU test
    // after the build drops the nodes that ran and could not encode, and keeps those whose test
    // never ran so a retry tries them again.
    public IReadOnlyList<RenderNode> VideoGpus { get; set; } = [];

    // Why no GPU is listed when detection itself failed, as opposed to finding none.
    public string? VideoGpuDetectionError { get; set; }

    // Why no GPU is listed although the host has one Docker cannot use, or the GPU test ruled
    // every detected one out.
    public string? VideoGpuUnavailableReason { get; set; }

    // Moves a node to the front, so it is the one wired and tested first. A node this machine
    // does not list changes nothing.
    public void PreferVideoGpu(string? path) =>
        VideoGpus = VideoGpus.OrderByDescending(node => node.Path == path).ToArray();

    // The same machine with other GPUs in play, for planning a wiring without changing what was
    // detected.
    public SystemResources WithVideoGpus(IReadOnlyList<RenderNode> gpus)
    {
        var copy = (SystemResources)MemberwiseClone();
        copy.VideoGpus = gpus;

        return copy;
    }
}
