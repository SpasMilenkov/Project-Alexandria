namespace Builder.Models;

// Copy written for non-technical users (locked decision D5): each entry says
// what the feature does and what it costs to run, never assuming IT background.
public static class FeatureCatalog
{
    public static readonly InstallFeature MediaProcessing = new()
    {
        Id = "media-processing",
        Label = "Media processing",
        Description = "Creates thumbnails, preview images and short video clips for your uploaded media, so browsing stays fast.",
        Cost = "Recommended. Adds about 1 GB of memory use while working.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature DocumentPreviews = new()
    {
        Id = "document-previews",
        Label = "Document previews",
        Description = "Generates preview images for documents such as PDFs, so you can glance at them without opening downloads.",
        Cost = "Recommended. Adds about 1 GB of memory use while working.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature AdaptiveStreaming = new()
    {
        Id = "adaptive-streaming",
        Label = "Adaptive streaming",
        Description = "Converts large audio and video into versions that start playing instantly and adjust their quality to your connection.",
        Cost = "Uses around 1 GB of memory and noticeable CPU while converting.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature AudioTaggingFast = new()
    {
        Id = "audio-tagging-fast",
        Label = "Audio tagging - fast",
        Description = "Automatically recognizes genre and mood of your music shortly after upload.",
        Cost = "Light to moderate. Adds about 2 GB of memory use while analyzing.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature AudioTaggingDeep = new()
    {
        Id = "audio-tagging-deep",
        Label = "Audio tagging - deep analysis",
        Description = "Produces richer and more detailed tags than fast tagging, but each upload takes much longer to analyze.",
        Cost = "Heavy: around 2 GB of memory and sustained hard CPU work. Best on stronger machines.",
        DefaultEnabled = false,
    };

    public static readonly InstallFeature Autotagging = new()
    {
        Id = "autotagging",
        Label = "Automatic tagging",
        Description = "The master switch for analyzing uploads. The audio taggers above only have an effect when this is on.",
        Cost = "No extra resources on its own.",
        DefaultEnabled = true,
    };

    public static readonly InstallFeature Lyrics = new()
    {
        Id = "lyrics",
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
        Cost = "Runs several background helpers, about 1 GB memory in total. Opens at http://localhost:3000.",
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
}
