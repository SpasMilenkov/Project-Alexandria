namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// Decides once per process whether video is encoded on the GPU or the CPU.
/// </summary>
public interface IVideoEncoderSelector
{
    /// <summary>
    /// Returns the encoder to use. The first call probes the GPU; later calls return that answer.
    /// </summary>
    Task<VideoEncoderSelection> SelectAsync(CancellationToken ct);
}
