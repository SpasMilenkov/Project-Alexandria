namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Outcome of a one-frame test encode on a GPU render node.
/// </summary>
/// <param name="Device">The render node that was tested.</param>
/// <param name="Usable">Whether the test encode succeeded.</param>
/// <param name="Driver">The VAAPI driver ffmpeg reported, when it got that far.</param>
/// <param name="Detail">Why the device is not usable; null when it is.</param>
public sealed record GpuProbeResult(string Device, bool Usable, string? Driver, string? Detail);
