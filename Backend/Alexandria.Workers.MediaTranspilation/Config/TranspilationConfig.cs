namespace Alexandria.Workers.MediaTranspilation.Config;

/// <summary>
/// Configuration for the media transpilation background worker.
/// Bind from <c>appsettings.json</c> under the <c>"Transpilation"</c> section.
/// </summary>
public class TranspilationConfig
{
    /// <summary>
    /// Volume-backed base directory where per-job ffmpeg output is written.
    /// Each job gets its own subdirectory: <c>{OutputBasePath}/{jobId}/</c>.
    /// This directory must be on a volume with sufficient capacity for the
    /// transcoded segments; it is always cleaned up after upload or failure.
    /// </summary>
    public string OutputBasePath { get; set; } =
        "/home/spasmilenkov/repos/Project-Alexandria/Backend/Alexandria.Workers.MediaTranspilation/j";

    /// <summary>
    /// Seconds between database polls for <c>Queued</c> transpilation jobs.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;
}