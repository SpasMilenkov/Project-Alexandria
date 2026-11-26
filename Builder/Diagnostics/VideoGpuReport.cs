namespace Builder.Diagnostics;

public sealed record VideoGpuReport
{
    public required string Folder { get; init; }

    // What the installation is set to encode on, or null when there is no installation to read.
    public string? Setting { get; init; }

    // The probe run through the installation's own compose file, so it sees exactly the access the
    // worker gets. Null when the installation uses no GPU or nothing could be tested.
    public bool? WorkerEncodes { get; init; }

    public string? WorkerDetail { get; init; }

    public IReadOnlyList<VideoGpuNodeReport> Nodes { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    // The installation expects a GPU that its worker cannot encode on.
    public bool WiredGpuFails { get; init; }

    // Set when nothing was tested for lack of a worker image: the source one can be built from.
    public string? BuildSource { get; init; }

    public bool BuildFailed { get; init; }

    // A check the report depends on could not run, so it vouches for nothing it did not test.
    public bool Incomplete { get; init; }
}
