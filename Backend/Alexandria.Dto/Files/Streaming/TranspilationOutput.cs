namespace Alexandria.Dto.Files.Streaming;

/// <summary>
/// Describes the local filesystem output produced by a single transpilation pass
/// </summary>
public sealed class TranspilationOutput
{
    /// <summary>Absolute local path to the DASH output directory (contains manifest.mpd and segments).</summary>
    public required string DashDirectory { get; init; }

    /// <summary>
    /// Absolute local path of the codec root directory that contains both
    /// and deleted during cleanup.
    /// </summary>
    public required string RootDirectory { get; init; }
}