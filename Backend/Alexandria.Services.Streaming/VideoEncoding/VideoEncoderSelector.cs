using Alexandria.Common.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Probes the configured GPU once and keeps the answer for the life of the process. A driver
/// that breaks later shows up as failed GPU passes, which fall back to the CPU per job.
/// </summary>
public sealed partial class VideoEncoderSelector(
    IOptions<VideoEncodingConfig> options,
    IGpuEncoderProbe probe,
    ILogger<VideoEncoderSelector> logger) : IVideoEncoderSelector, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private VideoEncoderSelection? _selection;

    /// <inheritdoc/>
    public async Task<VideoEncoderSelection> SelectAsync(CancellationToken ct)
    {
        if (_selection is { } known)
            return known;

        await _gate.WaitAsync(ct);

        try
        {
            _selection ??= await DecideAsync(ct);

            return _selection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<VideoEncoderSelection> DecideAsync(CancellationToken ct)
    {
        var config = options.Value;

        if (config.Mode == VideoEncoderMode.Cpu)
        {
            LogCpuConfigured(logger);
            return VideoEncoderSelection.Cpu;
        }

        // No GPU mapped into the container is the normal state on a machine without one.
        if (!File.Exists(config.Device))
        {
            LogNoGpu(logger, config.Device);
            return VideoEncoderSelection.Cpu;
        }

        GpuProbeResult result;

        try
        {
            result = await probe.ProbeAsync(config.Device, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The CPU always works, so a probe that breaks in an unforeseen way must not stop the
            // worker or its jobs.
            LogProbeFailed(logger, ex, config.Device);
            return VideoEncoderSelection.Cpu;
        }

        if (!result.Usable)
        {
            LogGpuUnusable(logger, config.Device, result.Detail ?? "no reason reported");
            return VideoEncoderSelection.Cpu;
        }

        LogGpuSelected(logger, config.Device, result.Driver ?? "unknown driver");

        return new VideoEncoderSelection(config.Device);
    }
}
