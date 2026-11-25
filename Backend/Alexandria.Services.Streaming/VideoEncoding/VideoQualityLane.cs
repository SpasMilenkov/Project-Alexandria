namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// One rung of the video ladder. <paramref name="Crf"/> is the quality target (libx264's CRF,
/// and the constant QP on the GPU) and <paramref name="BitrateKbps"/> its ceiling, which is also
/// the bandwidth the manifest advertises.
/// </summary>
internal sealed record VideoQualityLane(int Width, int Height, int BitrateKbps, int Crf);
