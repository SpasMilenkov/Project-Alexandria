namespace Alexandria.Workers.MediaMetadata.Config;

/// <summary>
/// Configuration for the Essentia enrichment worker.
/// Bind from <c>appsettings.json</c> under the <c>"Essentia"</c> section.
/// </summary>
public class EssentiaConfig
{
    /// <summary>
    /// Which backbone is active: <c>effnet</c> or <c>maest</c>. Exactly one runs.
    /// </summary>
    public string Backbone { get; set; } = "effnet";

    /// <summary>
    /// Maximum number of staged files dispatched per batch.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// How long the accumulator waits for more triggers before flushing a partial batch.
    /// </summary>
    public int MaxBatchWaitSeconds { get; set; } = 10;

    /// <summary>
    /// How often the timeout sweep re-checks for dispatched batches past their timeout.
    /// </summary>
    public int SweepIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// How long an effnet-dispatched batch may run before the sweep marks it timed out.
    /// </summary>
    public int EffnetTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// How long a maest-dispatched batch may run before the sweep marks it timed out.
    /// </summary>
    public int MaestTimeoutSeconds { get; set; } = 3600;

    /// <summary>
    /// Directory the worker writes staged audio into before dispatch.
    /// Container default: <c>/data/staged</c> (mounted from <c>./data/media-audio</c>).
    /// Dev override: <c>../data/media-audio/staged</c> (cwd is <c>Backend/</c>).
    /// </summary>
    public string StagedDir { get; set; } = "/data/staged";

    /// <summary>
    /// Base directory where per-file JSON outputs land after processing.
    /// Container default: <c>/data/output</c>. Dev override: <c>../data/media-audio/output</c>.
    /// </summary>
    public string OutputDir { get; set; } = "/data/output";

    /// <summary>
    /// Local filesystem path of the shared volume root (dev: <c>../data/media-audio</c>, container: <c>/data</c>).
    /// </summary>
    public string LocalVolumePath { get; set; } = "/data";

    /// <summary>
    /// Path at which the same volume appears inside the classifier container (always <c>/data</c>).
    /// Used to translate local staged paths into the absolute paths the Python worker reads.
    /// </summary>
    public string VolumeMountPath { get; set; } = "/data";

    /// <summary>
    /// Translates a local filesystem path under <see cref="LocalVolumePath"/> into the equivalent
    /// container-visible path under <see cref="VolumeMountPath"/>. Identity when both roots match.
    /// </summary>
    public string ToVolumePath(string localPath)
    {
        if (string.Equals(LocalVolumePath, VolumeMountPath, StringComparison.Ordinal))
            return localPath;

        var prefix = LocalVolumePath.TrimEnd('/');
        if (localPath.StartsWith(prefix, StringComparison.Ordinal))
            return VolumeMountPath.TrimEnd('/') + localPath[prefix.Length..];

        return localPath;
    }

    /// <summary>
    /// Inverse of <see cref="ToVolumePath"/>: translates a container-visible path
    /// (e.g. the <c>output_dir</c> echoed back in the completion message) into the
    /// equivalent local filesystem path. Identity when both roots match.
    /// </summary>
    public string ToLocalPath(string volumePath)
    {
        if (string.Equals(LocalVolumePath, VolumeMountPath, StringComparison.Ordinal))
            return volumePath;

        var prefix = VolumeMountPath.TrimEnd('/');
        if (volumePath.StartsWith(prefix, StringComparison.Ordinal))
            return LocalVolumePath.TrimEnd('/') + volumePath[prefix.Length..];

        return volumePath;
    }
}