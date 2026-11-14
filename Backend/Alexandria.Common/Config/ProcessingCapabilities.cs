namespace Alexandria.Common.Config;

public sealed class ProcessingCapabilities
{
    public const string SectionName = "Features";

    public bool DocumentPreviews { get; set; } = true;

    public bool MediaPreviews { get; set; } = true;

    public bool AdaptiveStreaming { get; set; } = true;

    public bool AudioAnalysis { get; set; } = true;

    public bool Lyrics { get; set; } = true;

    // The automatic tagging toggle; off unless configured, as before it moved here
    public bool Autotagging { get; set; }

    // Tagging needs both the toggle and an installed analysis model, so every
    // auto-tagging gate reads this rather than either flag alone
    public bool AutoTaggingActive => Autotagging && AudioAnalysis;
}
