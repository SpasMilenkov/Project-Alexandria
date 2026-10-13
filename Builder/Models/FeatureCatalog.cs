namespace Builder.Models;

// Copy written for non-technical users (locked decision D5): each entry says
// what the feature does and what it costs to run, never assuming IT background.
public static class FeatureCatalog
{
    public static readonly InstallFeature MediaProcessing = new()
    {
        Id = "media-processing",
        EnvironmentFlag = "MEDIA_PROCESSING_ENABLED",
        Label = "Media previews",
        Description = "Creates thumbnails, preview images and short video clips for your uploaded media, so browsing stays fast.",
        Cost = "Recommended. Uses memory and CPU while preparing previews.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature DocumentPreviews = new()
    {
        Id = "document-previews",
        EnvironmentFlag = "DOCUMENT_PREVIEWS_ENABLED",
        Label = "Document previews",
        Description = "Generates preview images for documents such as PDFs, so you can glance at them without opening downloads.",
        Cost = "Recommended. Uses memory and CPU while preparing previews.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature AdaptiveStreaming = new()
    {
        Id = "adaptive-streaming",
        EnvironmentFlag = "STREAMING_ENABLED",
        Label = "Adaptive streaming",
        Description = "Converts large audio and video into versions that start playing instantly and adjust their quality to your connection.",
        Cost = "Uses memory and noticeable CPU while converting.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature AudioTaggingFast = new()
    {
        Id = "audio-tagging-fast",
        Label = "Audio tagging - fast",
        Description = "Automatically recognizes genre and mood of your music shortly after upload.",
        Cost = "Light to moderate analysis, with a background coordinator.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature AudioTaggingDeep = new()
    {
        Id = "audio-tagging-deep",
        Label = "Audio tagging - deep analysis",
        Description = "Produces richer and more detailed tags than fast tagging, but each upload takes much longer to analyze.",
        Cost = "Heavy, sustained CPU work. Best on stronger machines.",
        DefaultEnabled = false,
    };

    public static readonly InstallFeature Autotagging = new()
    {
        Id = "autotagging",
        EnvironmentFlag = "AUTOTAGGING_ENABLED",
        Label = "Automatic tagging",
        Description = "Analyzes uploads with your chosen audio model. Descriptive media metadata is collected even when analysis is off.",
        Cost = "No extra resources on its own.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature Lyrics = new()
    {
        Id = "lyrics",
        EnvironmentFlag = "LYRICS_ENABLED",
        Label = "Lyrics fetching",
        Description = "Automatically finds and saves lyrics for your music.",
        Cost = "Very light. Needs internet access.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature Monitoring = new()
    {
        Id = "monitoring",
        Label = "Monitoring dashboard",
        Description = "Adds a health dashboard showing how Alexandria and your machine are doing, with graphs and logs.",
        Cost = "Runs several background helpers. Opens under /monitoring/ at your HTTPS address.",
        DefaultEnabled = false,
    };

    public static readonly IReadOnlyList<InstallFeature> All =
    [
        MediaProcessing,
        DocumentPreviews,
        AdaptiveStreaming,
        AudioTaggingFast,
        AudioTaggingDeep,
        Autotagging,
        Lyrics,
        Monitoring,
    ];

    public static readonly IReadOnlyList<InstallFeature> ToggleFeatures =
    [
        MediaProcessing, DocumentPreviews, AdaptiveStreaming, Autotagging, Lyrics, Monitoring,
    ];
}
