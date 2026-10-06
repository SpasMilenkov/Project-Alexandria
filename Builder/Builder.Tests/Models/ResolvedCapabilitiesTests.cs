using System.Text.RegularExpressions;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Models;

public sealed class ResolvedCapabilitiesTests
{
    private static readonly string[] CoreServices =
        ["postgres", "garage", "garage-init", "rabbitmq-config", "rabbitmq", "api", "frontend", "nginx", "media-worker", "playlist-worker"];

    public static IEnumerable<object[]> AllSelections => Enumerable.Range(0, (1 << FeatureCatalog.ToggleFeatures.Count) * 3).Select(mask => new object[] { mask });

    private static FeatureSelection Selection(int combination)
    {
        var selection = new FeatureSelection
        {
            Enabled = FeatureCatalog.ToggleFeatures.Where((_, bit) => ((combination / 3) & (1 << bit)) != 0)
                .Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal),
        };
        selection.SetAudioBackbone(new[] { "off", "effnet", "maest" }[combination % 3]);

        return selection;
    }

    [Theory]
    [MemberData(nameof(AllSelections))]
    public void Resolve_everySelection_matchesGeneratedServicesResourcesAndEnvironment(int mask)
    {
        var features = Selection(mask);
        var expected = new List<string>(CoreServices);
        var expectedRam = 4096L;

        void Include(string feature, long memoryMb, params string[] services)
        {
            if (!features.IsEnabled(feature)) return;

            expected.AddRange(services);
            expectedRam += memoryMb;
        }

        Include("document-previews", 1024, "document-worker");
        Include("adaptive-streaming", 1024, "transpilation-worker");
        Include("lyrics", 512, "lyrics-worker");
        if (features.IsEnabled("autotagging"))
        {
            Include("audio-tagging-fast", 2048, "essentia-effnet-worker");
            Include("audio-tagging-deep", 2048, "essentia-maest-worker");
        }
        Include("monitoring", 1536, "prometheus", "grafana", "cadvisor", "postgres-exporter", "alloy", "loki");

        if (features.IsEnabled("autotagging") && features.AudioBackbone != "off")
        {
            expected.Add("media-metadata-worker");
            expectedRam += 1024;
        }

        var capabilities = ServiceCatalog.Resolve(features);
        var calculator = new ResourceCalculator();
        var config = new InstallationConfig { Features = features, Ports = new() { ["HTTPS_PORT"] = 8443 } };
        var compose = new ConfigurationService(new TemplateService("Local")).GenerateDockerCompose(config);

        capabilities.ServiceIds.Should().BeEquivalentTo(expected);
        SectionKeys(compose, "services").Should().BeEquivalentTo(expected);
        SectionKeys(compose, "volumes").Should().BeEquivalentTo(capabilities.Volumes);
        SectionKeys(compose, "networks").Should().BeEquivalentTo(capabilities.Networks);
        calculator.CountServices(features).Should().Be(expected.Count);
        capabilities.RunningServiceCount.Should().Be(expected.Count - 2);
        capabilities.OneShotServiceIds.Should().BeEquivalentTo("garage-init", "rabbitmq-config");
        calculator.CalculateMinimumRamMb(features).Should().Be(expectedRam);
        capabilities.MemoryEstimates.Sum(estimate => estimate.MemoryMb).Should().Be(expectedRam);

        var environment = EnvGenerator.BuildValues(features, new Dictionary<string, string>(), new AdminAccountInput(), null);

        foreach (var (feature, variable) in new[]
        {
            ("media-processing", "MEDIA_PROCESSING_ENABLED"), ("document-previews", "DOCUMENT_PREVIEWS_ENABLED"),
            ("adaptive-streaming", "STREAMING_ENABLED"), ("lyrics", "LYRICS_ENABLED"), ("autotagging", "AUTOTAGGING_ENABLED"),
        })
        {
            environment[variable].Should().Be(features.IsEnabled(feature) ? "true" : "false");
        }
    }

    [Fact]
    public void Resolve_snapshot_keepsReviewedSelectionWhenDraftChanges()
    {
        var features = new FeatureSelection();
        var reviewed = ServiceCatalog.Resolve(features);

        features.SetEnabled("document-previews", true);

        reviewed.ServiceIds.Should().NotContain("document-worker");
        reviewed.EnvironmentFlags["DOCUMENT_PREVIEWS_ENABLED"].Should().Be("false");
        ServiceCatalog.Resolve(features).ServiceIds.Should().Contain("document-worker");
    }

    [Fact]
    public void Resolve_sharedCoordinatorAndAssets_areIncludedOnlyOnce()
    {
        var features = new FeatureSelection { Enabled = ["audio-tagging-fast", "audio-tagging-deep", "autotagging"] };
        var capabilities = ServiceCatalog.Resolve(features);

        capabilities.ServiceIds.Count(id => id == "media-metadata-worker").Should().Be(1);
        capabilities.ModelAssets.Should().ContainSingle();
        capabilities.ModelAssets[0].ChecksumManifest.Should().Be("classificators/essentia/models/models.sha256");
        capabilities.RequiredSourceFiles.Should().OnlyHaveUniqueItems();
        capabilities.ComposeTemplates.Should().OnlyHaveUniqueItems();
        capabilities.Volumes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Resolve_noAnalysis_omitsModelAndClassifierRequirements()
    {
        var capabilities = ServiceCatalog.Resolve(new FeatureSelection { Enabled = ["autotagging", "lyrics"] });

        capabilities.ModelAssets.Should().BeEmpty();
        capabilities.RequiredSourceFiles.Should().NotContain(path => path.StartsWith("classificators/", StringComparison.Ordinal));
        capabilities.ComposeTemplates.Should().NotContain(path => path.Contains("essentia", StringComparison.Ordinal));
        capabilities.Volumes.Should().Contain("media_metadata_worker_scratch").And.NotContain("media_audio_data");
    }

    [Fact]
    public void Catalog_allDefinitions_referToExistingSourceAndTemplates()
    {
        var source = Services.ConfigurationArtifactFixture.FindRepositoryRoot();
        var capabilities = ServiceCatalog.Resolve(Selection(((1 << FeatureCatalog.ToggleFeatures.Count) - 1) * 3 + 1));

        foreach (var path in capabilities.RequiredSourceFiles)
        {
            File.Exists(Path.Combine(source, path)).Should().BeTrue($"selected source asset {path} must exist");
        }

        var templates = Path.Combine(AppContext.BaseDirectory, "Templates", "Local");

        foreach (var path in capabilities.RequiredTemplates)
        {
            File.Exists(Path.Combine(templates, path)).Should().BeTrue($"selected template {path} must exist");
        }

        ServiceCatalog.All.Select(service => service.Id).Should().OnlyHaveUniqueItems();
        ServiceCatalog.All.SelectMany(service => service.RequiresFeatures)
            .Should().OnlyContain(id => FeatureCatalog.All.Any(feature => feature.Id == id));
    }

    private static IEnumerable<string> SectionKeys(string compose, string section)
    {
        var body = Regex.Match(compose, $@"(?ms)^{section}:\r?\n(?<body>.*?)(?=^[a-z][a-z_-]*:|\z)").Groups["body"].Value;

        return Regex.Matches(body, @"(?m)^  (?<id>[a-z][a-z0-9_-]*):\s*$")
            .Select(match => match.Groups["id"].Value);
    }
}
