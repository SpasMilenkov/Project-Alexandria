namespace Builder.Models;

public sealed class ServiceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Group { get; init; }
    public required string ComposeTemplate { get; init; }
    public required long MemoryMb { get; init; }
    public long MemoryHeadroomMb { get; init; }
    public IReadOnlyList<string> RequiresFeatures { get; init; } = [];
    public IReadOnlyList<string> RequiresAllFeatures { get; init; } = [];
    public bool AlwaysEnabled { get; init; }
    public IReadOnlyList<string> SourceFiles { get; init; } = [];
    public IReadOnlyList<string> ExtraTemplates { get; init; } = [];
    public IReadOnlyList<string> Volumes { get; init; } = [];
    public IReadOnlyList<string> Networks { get; init; } = [];
    public IReadOnlyList<ModelAssetRequirement> ModelAssets { get; init; } = [];
    public bool Infrastructure { get; init; }
    public bool OneShot { get; init; }

    public bool IsEnabled(FeatureSelection features) =>
        (AlwaysEnabled || RequiresFeatures.Count == 0 || RequiresFeatures.Any(features.IsEnabled)) &&
        RequiresAllFeatures.All(features.IsEnabled);
}

public sealed record ModelAssetRequirement(string Name, string Directory, string ChecksumManifest, string PreparationScript);

public static class ServiceCatalog
{
    private const string StorageTemplate = "docker-compose/storage.yml.template";
    private const string AppsTemplate = "docker-compose/apps.yml.template";
    private const string MonitoringTemplate = "docker-compose/monitoring.yml.template";

    private static readonly ModelAssetRequirement AudioModels = new(
        "Audio analysis models", "classificators/essentia/models",
        "classificators/essentia/models/models.sha256", "classificators/essentia/download-models.sh");

    private static readonly string[] ClassifierFiles =
    [
        "classificators/essentia/Dockerfile", "classificators/essentia/entrypoint.sh",
        AudioModels.PreparationScript, "classificators/essentia/essentia_core.py",
        "classificators/essentia/classify.py", "classificators/essentia/worker.py", AudioModels.ChecksumManifest,
        "classificators/essentia/model_assets.py", "classificators/essentia/model-requirements.tsv",
    ];

    private static ServiceInfo S(
        string id, string name, string group, long memoryMb, string template, string description,
        string[]? features = null, string[]? sourceFiles = null, string[]? volumes = null,
        string[]? networks = null, string[]? extraTemplates = null, bool infrastructure = false,
        bool oneShot = false, long headroomMb = 0, ModelAssetRequirement[]? modelAssets = null,
        string[]? allFeatures = null, bool alwaysEnabled = false) => new()
    {
        Id = id,
        Name = name,
        Group = group,
        MemoryMb = memoryMb,
        MemoryHeadroomMb = headroomMb,
        ComposeTemplate = template,
        Description = description,
        RequiresFeatures = features ?? [],
        RequiresAllFeatures = allFeatures ?? [],
        AlwaysEnabled = alwaysEnabled,
        SourceFiles = sourceFiles ?? [],
        Volumes = volumes ?? [],
        Networks = networks ?? [],
        ExtraTemplates = extraTemplates ?? [],
        Infrastructure = infrastructure,
        OneShot = oneShot,
        ModelAssets = modelAssets ?? [],
    };

    public static readonly IReadOnlyList<ServiceInfo> All =
    [
        S("postgres", "Database", "Core", 512, StorageTemplate,
            "Stores all your account info, tags and file details.",
            sourceFiles: ["scripts/postgres/init/01-create-exporter-user.sh"],
            volumes: ["postgres_local_data"], networks: ["data-net"], infrastructure: true),
        S("garage", "File storage", "Core", 256, StorageTemplate,
            "Keeps the actual files safe on disk, with version history.",
            volumes: ["garage_local_meta_data", "garage_local_storage_data"], networks: ["app-net", "data-net"],
            extraTemplates: ["config/garage.toml.template"], infrastructure: true),
        S("garage-init", "Storage setup", "Core", 0, StorageTemplate,
            "One-time helper that creates secure keys and storage folders.",
            sourceFiles: ["scripts/garage-init/Dockerfile", "scripts/garage-init/init.sh", "scripts/garage-init/cors.json"],
            volumes: ["garage_local_meta_data"], networks: ["data-net"], infrastructure: true, oneShot: true),
        S("rabbitmq-config", "Messaging setup", "Core", 0, StorageTemplate,
            "One-time helper that prepares message queues and permissions.",
            sourceFiles: ["rabbitmq/definitions.template.json"], volumes: ["rabbitmq_definitions"],
            networks: ["default"], infrastructure: true, oneShot: true),
        S("rabbitmq", "Message hub", "Core", 512, StorageTemplate,
            "Lets the parts of Alexandria talk without blocking each other.",
            sourceFiles: ["rabbitmq/rabbitmq.template.conf", "rabbitmq/enabled_plugins"],
            volumes: ["rabbitmq_local_data", "rabbitmq_definitions"], networks: ["data-net"], infrastructure: true),
        S("api", "Alexandria server", "Core", 512, AppsTemplate,
            "The heart of the app - serves pages, uploads, accounts and search.",
            sourceFiles: ["Backend/Alexandria.Api/Dockerfile", "Backend/Alexandria.Api/Alexandria.Api.csproj",
                "Backend/Directory.Packages.props", "Backend/Directory.Build.props", "Backend/nuget.config"],
            networks: ["app-net", "data-net"], headroomMb: 576),
        S("frontend", "Web interface", "Core", 128, AppsTemplate,
            "What you see and click in the browser.",
            sourceFiles: ["Frontend/Dockerfile", "Frontend/package.json", "Frontend/pnpm-lock.yaml",
                "Frontend/pnpm-workspace.yaml", "Frontend/src/main.ts", "Frontend/nginx.conf"], networks: ["app-net"]),
        S("nginx", "Front door", "Core", 64, AppsTemplate,
            "One web address for everything; routes traffic and keeps it secure.",
            sourceFiles: ["nginx/Dockerfile", "nginx/nginx.conf", "nginx/gpg/arut.key"], networks: ["frontend-net", "app-net"]),

        S("document-worker", "Document previews", "Document previews", 1024,
            "docker-compose/document-worker.yml.template", "Generates preview images for documents like PDFs.",
            features: [FeatureCatalog.DocumentPreviews.Id], sourceFiles: ["Backend/Alexandria.Workers.Document/Dockerfile"],
            volumes: ["document_worker_scratch"], networks: ["data-net"]),
        S("media-worker", "Media metadata and previews", "Core", 1024,
            "docker-compose/media-worker.yml.template", "Reads media details; creates thumbnails and clips when previews are enabled.",
            features: [FeatureCatalog.MediaProcessing.Id], sourceFiles: ["Backend/Alexandria.Workers.Media/Dockerfile"],
            volumes: ["media_worker_scratch"], networks: ["data-net"], alwaysEnabled: true),
        S("playlist-worker", "Playlist synchronization", "Core", 512,
            "docker-compose/playlist-worker.yml.template", "Keeps playlists synchronized with your library.",
            sourceFiles: ["Backend/Alexandria.Workers.Playlist/Dockerfile"], networks: ["data-net"]),
        S("transpilation-worker", "Transpilation worker", "Adaptive streaming", 1024,
            "docker-compose/transpilation-worker.yml.template", "Converts large media so it starts playing instantly anywhere.",
            features: [FeatureCatalog.AdaptiveStreaming.Id], sourceFiles: ["Backend/Alexandria.Workers.MediaTranspilation/Dockerfile"],
            volumes: ["transpilation_worker_scratch"], networks: ["data-net"]),
        S("media-metadata-worker", "Tagging coordinator", "Audio analysis support", 1024,
            "docker-compose/media-metadata-worker.yml.template", "Organizes the tag analysis pipeline after each upload.",
            features: [FeatureCatalog.AudioTaggingFast.Id, FeatureCatalog.AudioTaggingDeep.Id],
            allFeatures: [FeatureCatalog.Autotagging.Id],
            sourceFiles: ["Backend/Alexandria.Workers.MediaMetadata/Dockerfile"],
            volumes: ["media_metadata_worker_scratch", "media_audio_data"], networks: ["data-net"]),
        S("lyrics-worker", "Lyrics finder", "Lyrics fetching", 512,
            "docker-compose/lyrics-worker.yml.template", "Searches online services for lyrics and saves them to your library.",
            features: [FeatureCatalog.Lyrics.Id], sourceFiles: ["Backend/Alexandria.Workers.Lyrics/Dockerfile"],
            volumes: ["media_metadata_worker_scratch"], networks: ["data-net"]),
        S("essentia-effnet-worker", "Fast tagger (Effnet)", "Audio tagging - fast", 2048,
            "docker-compose/essentia-effnet-worker.yml.template", "Recognizes genre and mood of music right after upload.",
            features: [FeatureCatalog.AudioTaggingFast.Id], sourceFiles: ClassifierFiles,
            allFeatures: [FeatureCatalog.Autotagging.Id],
            volumes: ["media_audio_data"], networks: ["essentia-net"], modelAssets: [AudioModels]),
        S("essentia-maest-worker", "Deep tagger (MAEST)", "Audio tagging - deep", 2048,
            "docker-compose/essentia-maest-worker.yml.template", "Richer, more detailed tags - slower and heavier than fast tagging.",
            features: [FeatureCatalog.AudioTaggingDeep.Id], sourceFiles: ClassifierFiles,
            allFeatures: [FeatureCatalog.Autotagging.Id],
            volumes: ["media_audio_data"], networks: ["essentia-net"], modelAssets: [AudioModels]),

        S("prometheus", "Metrics collector", "Monitoring", 384, MonitoringTemplate,
            "Gathers performance numbers from every part of the system.",
            features: [FeatureCatalog.Monitoring.Id], volumes: ["prometheus_local_data"],
            networks: ["data-net", "observability-net"],
            extraTemplates: ["config/prometheus.yml.template"], headroomMb: 416),
        S("grafana", "Dashboards", "Monitoring", 128, MonitoringTemplate,
            "Draws the health graphs you can open in the browser.",
            features: [FeatureCatalog.Monitoring.Id], volumes: ["grafana_local_data"], networks: ["observability-net"],
            sourceFiles: ["grafana/provisioning/dashboards/dashboard.yml", "grafana/provisioning/datasources/prometheus.yml",
                "grafana/provisioning/datasources/loki.yml",
                "grafana/provisioning/dashboards/alexandria-overview.json",
                "grafana/provisioning/dashboards/alexandria-api.json",
                "grafana/provisioning/dashboards/alexandria-logs.json"]),
        S("cadvisor", "Container stats", "Monitoring", 160, MonitoringTemplate,
            "Watches how much CPU and memory each service uses.",
            features: [FeatureCatalog.Monitoring.Id], networks: ["observability-net"]),
        S("postgres-exporter", "Database stats", "Monitoring", 64, MonitoringTemplate,
            "Reports database health and speed.",
            features: [FeatureCatalog.Monitoring.Id], networks: ["data-net", "observability-net"]),
        S("alloy", "Log shipper", "Monitoring", 128, MonitoringTemplate,
            "Collects logs from all services into one searchable place.",
            features: [FeatureCatalog.Monitoring.Id], sourceFiles: ["alloy/config.alloy"],
            volumes: ["alloy_local_data"], networks: ["observability-net"]),
        S("loki", "Log storage", "Monitoring", 256, MonitoringTemplate,
            "Keeps the collected logs so you can look back in time.",
            features: [FeatureCatalog.Monitoring.Id], sourceFiles: ["loki/loki-config.yml"],
            volumes: ["loki_local_data"], networks: ["observability-net"]),
    ];

    public static ResolvedCapabilities Resolve(FeatureSelection features) => new(features);

    public static IEnumerable<ServiceInfo> EnabledServices(FeatureSelection features) => Resolve(features).Services;

    public static IEnumerable<ServiceInfo> ForFeature(string featureId) =>
        All.Where(service => service.RequiresFeatures.Contains(featureId));
}
