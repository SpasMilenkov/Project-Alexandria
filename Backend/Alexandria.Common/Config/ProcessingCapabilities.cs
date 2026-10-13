namespace Alexandria.Common.Config;

public sealed class ProcessingCapabilities
{
    public const string SectionName = "Features";

    public bool DocumentPreviews { get; set; } = true;

    public bool MediaPreviews { get; set; } = true;

    public bool AdaptiveStreaming { get; set; } = true;

    public bool AudioAnalysis { get; set; } = true;

    public bool Lyrics { get; set; } = true;
}
