namespace Alexandria.Services.Streaming.VideoEncoding;

/// <summary>
/// The first real video stream of a source as players show it: turned upright and measured in
/// square pixels, which is the shape every lane is cut to.
/// </summary>
internal sealed record VideoSource(int Width, int Height, bool HasAudio)
{
    public int LongSide => Math.Max(Width, Height);

    public int ShortSide => Math.Min(Width, Height);
}
