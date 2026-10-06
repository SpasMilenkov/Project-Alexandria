namespace Builder.Models;

// Plain-language description of every component the installer can run.
// Feeds the Features detail pane and the Summary "what will run" view -
// written for non-technical users (locked decision D5).
public sealed class ServiceInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    // "Core", a FeatureCatalog label, or "Monitoring"
    public required string Group { get; init; }
    public required double MemoryGb { get; init; }
    // Empty = always runs; otherwise runs when ANY of these features is on
    public IReadOnlyList<string> RequiresFeatures { get; init; } = [];
    public bool OneShot { get; init; }

    public bool IsEnabled(FeatureSelection features) =>
        RequiresFeatures.Count == 0 || RequiresFeatures.Any(features.IsEnabled);
}

public static class ServiceCatalog
{
    private static ServiceInfo S(
        string name,
        string group,
        double memoryGb,
        string description,
        string[]? requires = null,
        bool oneShot = false)
    {
        return new ServiceInfo
        {
            Name = name,
            Group = group,
            MemoryGb = memoryGb,
            Description = description,
            RequiresFeatures = requires ?? [],
            OneShot = oneShot,
        };
    }

    public static readonly IReadOnlyList<ServiceInfo> All =
    [
        // --- Core ---
        S("Database", "Core", 0.5,
            "Stores all your account info, tags and file details."),
        S("File storage", "Core", 0.25,
            "Keeps the actual files safe on disk, with version history."),
        S("Storage setup", "Core", 0, 
            "One-time helper that creates secure keys and storage folders.",
            oneShot: true),
        S("Message hub", "Core", 0.5,
            "Lets the parts of Alexandria talk without blocking each other."),
        S("Messaging setup", "Core", 0,
            "One-time helper that prepares message queues and permissions.",
            oneShot: true),
        S("Alexandria server", "Core", 0.5,
            "The heart of the app - serves pages, uploads, accounts and search."),
        S("Web interface", "Core", 0.13,
            "What you see and click in the browser."),
        S("Front door", "Core", 0.06,
            "One web address for everything; routes traffic and keeps it secure."),

        // --- Optional features ---
        S("Media worker", "Media processing", 1,
            "Creates thumbnails, preview images and short video clips.",
            [FeatureCatalog.MediaProcessing.Id]),
        S("Document previews", "Document previews", 1,
            "Generates preview images for documents like PDFs.",
            [FeatureCatalog.DocumentPreviews.Id]),
        S("Transpilation worker", "Adaptive streaming", 1,
            "Converts large media so it starts playing instantly anywhere.",
            [FeatureCatalog.AdaptiveStreaming.Id]),
        S("Tagging coordinator", "Automatic tagging", 0,
            "Organizes the tag analysis pipeline after each upload.",
            [FeatureCatalog.AudioTaggingFast.Id, FeatureCatalog.AudioTaggingDeep.Id]),
        S("Fast tagger (Effnet)", "Audio tagging - fast", 2,
            "Recognizes genre and mood of music right after upload.",
            [FeatureCatalog.AudioTaggingFast.Id]),
        S("Deep tagger (MAEST)", "Audio tagging - deep", 2,
            "Richer, more detailed tags - slower and heavier than fast tagging.",
            [FeatureCatalog.AudioTaggingDeep.Id]),
        S("Lyrics finder", "Lyrics fetching", 0.5,
            "Searches online services for lyrics and saves them to your library.",
            [FeatureCatalog.Lyrics.Id]),

        // --- Monitoring ---
        S("Metrics collector", "Monitoring", 0.38,
            "Gathers performance numbers from every part of the system.",
            [FeatureCatalog.Monitoring.Id]),
        S("Dashboards", "Monitoring", 0.13,
            "Draws the health graphs you can open in the browser.",
            [FeatureCatalog.Monitoring.Id]),
        S("Container stats", "Monitoring", 0.15,
            "Watches how much CPU and memory each service uses.",
            [FeatureCatalog.Monitoring.Id]),
        S("Database stats", "Monitoring", 0.06,
            "Reports database health and speed.",
            [FeatureCatalog.Monitoring.Id]),
        S("Log shipper", "Monitoring", 0.13,
            "Collects logs from all services into one searchable place.",
            [FeatureCatalog.Monitoring.Id]),
        S("Log storage", "Monitoring", 0.26,
            "Keeps the collected logs so you can look back in time.",
            [FeatureCatalog.Monitoring.Id]),
    ];

    public static IEnumerable<ServiceInfo> EnabledServices(FeatureSelection features) =>
        All.Where(s => s.IsEnabled(features));

    public static IEnumerable<ServiceInfo> ForFeature(string featureId) =>
        All.Where(s => s.RequiresFeatures.Contains(featureId));
}
