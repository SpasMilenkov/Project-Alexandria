namespace Builder.Models;

public sealed record MemoryEstimate(string Label, long MemoryMb);

public sealed class ResolvedCapabilities
{
    private readonly HashSet<string> _enabledFeatures;

    internal ResolvedCapabilities(FeatureSelection features)
    {
        _enabledFeatures = FeatureCatalog.All.Where(feature => features.IsEnabled(feature.Id))
            .Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
        var selection = new FeatureSelection { Enabled = _enabledFeatures };
        Services = ServiceCatalog.All.Where(service => service.IsEnabled(selection)).ToArray();
        ServiceIds = Services.Select(service => service.Id).ToArray();
        InfrastructureServiceIds = Services.Where(service => service.Infrastructure).Select(service => service.Id).ToArray();
        OneShotServiceIds = Services.Where(service => service.OneShot).Select(service => service.Id).ToArray();
        ComposeTemplates = Services.Select(service => service.ComposeTemplate).Distinct(StringComparer.Ordinal).ToArray();
        RequiredTemplates = new[] { "docker-compose/base.yml.template" }.Concat(ComposeTemplates)
            .Concat(Services.SelectMany(service => service.ExtraTemplates)).Distinct(StringComparer.Ordinal).ToArray();
        RequiredSourceFiles = Services.SelectMany(service => service.SourceFiles).Distinct(StringComparer.Ordinal).ToArray();
        Volumes = Services.SelectMany(service => service.Volumes).Distinct(StringComparer.Ordinal).ToArray();
        Networks = Services.SelectMany(service => service.Networks).Distinct(StringComparer.Ordinal).ToArray();
        ModelAssets = Services.SelectMany(service => service.ModelAssets).Distinct().ToArray();
        MemoryEstimates = Services.Where(service => !service.OneShot).GroupBy(service => service.Group)
            .Select(group => new MemoryEstimate(group.Key, group.Sum(service => service.MemoryMb + service.MemoryHeadroomMb)))
            .Where(estimate => estimate.MemoryMb > 0).ToArray();
        var flags = FeatureCatalog.All.Where(feature => feature.EnvironmentFlag is not null)
            .ToDictionary(feature => feature.EnvironmentFlag!, feature => IsEnabled(feature.Id) ? "true" : "false", StringComparer.Ordinal);

        AudioBackbone = selection.AudioBackbone;
        flags["AUDIO_ANALYSIS_ENABLED"] = AudioAnalysisEnabled ? "true" : "false";
        flags["ESSENTIA_BACKBONE"] = AudioAnalysisEnabled ? AudioBackbone : "off";
        flags["MEDIA_WORKER_ENABLED"] = "true";
        flags["PLAYLIST_WORKER_ENABLED"] = "true";
        flags["METADATA_WORKER_ENABLED"] = AudioAnalysisEnabled ? "true" : "false";
        EnvironmentFlags = flags;
    }

    public IReadOnlyList<ServiceInfo> Services { get; }
    public IReadOnlyList<string> ServiceIds { get; }
    public IReadOnlyList<string> InfrastructureServiceIds { get; }
    public IReadOnlyList<string> OneShotServiceIds { get; }
    public IReadOnlyList<string> ComposeTemplates { get; }
    public IReadOnlyList<string> RequiredTemplates { get; }
    public IReadOnlyList<string> RequiredSourceFiles { get; }
    public IReadOnlyList<string> Volumes { get; }
    public IReadOnlyList<string> Networks { get; }
    public IReadOnlyList<ModelAssetRequirement> ModelAssets { get; }
    public IReadOnlyList<MemoryEstimate> MemoryEstimates { get; }
    public IReadOnlyDictionary<string, string> EnvironmentFlags { get; }
    public string AudioBackbone { get; }
    public int RunningServiceCount => Services.Count(service => !service.OneShot);
    public long MinimumRamMb => MemoryEstimates.Sum(estimate => estimate.MemoryMb);
    public bool MonitoringEnabled => IsEnabled(FeatureCatalog.Monitoring.Id);
    public bool AudioAnalysisEnabled => Services.Any(service => service.ModelAssets.Count > 0);

    public bool IsEnabled(string featureId) => _enabledFeatures.Contains(featureId);
}
