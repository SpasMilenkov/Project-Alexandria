namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// The encoder video transcoding uses. <paramref name="GpuDevice"/> is the render node to encode
/// on, or null for the CPU.
/// </summary>
public sealed record VideoEncoderSelection(string? GpuDevice)
{
    public static readonly VideoEncoderSelection Cpu = new((string?)null);

    public bool UsesGpu => GpuDevice is not null;
}
