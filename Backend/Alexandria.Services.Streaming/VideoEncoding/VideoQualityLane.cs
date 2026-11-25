namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// One lane of the video ladder: the size it is encoded at, <paramref name="Crf"/> as its quality
/// target (libx264's CRF, and the constant QP on the GPU) and <paramref name="BitrateKbps"/> as
/// its ceiling, which is also the bandwidth the manifest advertises.
/// </summary>
internal sealed record VideoQualityLane(int Width, int Height, int BitrateKbps, int Crf);
