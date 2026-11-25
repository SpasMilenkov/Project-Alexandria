namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Tests whether a GPU render node can encode H.264 video.
/// </summary>
public interface IGpuEncoderProbe
{
    /// <summary>
    /// Runs a one-frame test encode on <paramref name="device"/>. Never throws for a device that
    /// cannot encode; the result says why.
    /// </summary>
    Task<GpuProbeResult> ProbeAsync(string device, CancellationToken ct);
}
