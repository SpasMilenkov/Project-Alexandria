namespace Alexandria.Common.Processes;

/// <summary>
/// Thread flags that keep ffmpeg inside the container's CPU limit. ffmpeg sizes its decoder,
/// encoder and filter pools from the host's cores and ignores the CPU quota, so a capped
/// container would start far more threads than it can run and stall them. .NET's processor
/// count follows the quota, so it is the count to hand over.
/// </summary>
public static class FfmpegThreads
{
    public static int Count { get; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>Global filter pools and the input decoders. Goes before <c>-i</c>.</summary>
    public static string Input { get; } =
        $"-filter_threads {Count} -filter_complex_threads {Count} -threads {Count}";

    /// <summary>The output encoders. Goes after the last output option, before the output path.</summary>
    public static string Output { get; } = $"-threads {Count}";
}
