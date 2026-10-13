using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Models;

public sealed class AudioBackboneTests
{
    [Theory]
    [InlineData("off", null)]
    [InlineData("effnet", "essentia-effnet-worker")]
    [InlineData("maest", "essentia-maest-worker")]
    public void Choice_installsExactlyItsConsumerWhenMasterIsOn(string backbone, string? expectedConsumer)
    {
        var selection = new FeatureSelection { Enabled = ["autotagging"] };
        selection.SetAudioBackbone(backbone);
        var capabilities = ServiceCatalog.Resolve(selection);
        var classifiers = capabilities.ServiceIds.Where(id => id.StartsWith("essentia-", StringComparison.Ordinal));

        if (expectedConsumer is null)
        {
            classifiers.Should().BeEmpty();
            capabilities.ServiceIds.Should().NotContain("media-metadata-worker");
        }
        else
        {
            classifiers.Should().Equal(expectedConsumer);
            capabilities.ServiceIds.Should().Contain("media-metadata-worker");
        }

        capabilities.ServiceIds.Should().Contain("media-worker").And.Contain("playlist-worker");
        capabilities.EnvironmentFlags["ESSENTIA_BACKBONE"].Should().Be(backbone);
    }

    [Theory]
    [InlineData("effnet")]
    [InlineData("maest")]
    public void MasterOff_preservesChoiceWithoutInstallingModelsOrCoordinator(string backbone)
    {
        var selection = new FeatureSelection();
        selection.SetAudioBackbone(backbone);
        var resolved = ServiceCatalog.Resolve(selection);

        selection.AudioBackbone.Should().Be(backbone);
        resolved.AudioAnalysisEnabled.Should().BeFalse();
        resolved.ModelAssets.Should().BeEmpty();
        resolved.ServiceIds.Should().NotContain("media-metadata-worker");
        resolved.EnvironmentFlags["AUDIO_ANALYSIS_ENABLED"].Should().Be("false");
        resolved.EnvironmentFlags["ESSENTIA_BACKBONE"].Should().Be("off");
    }

    [Fact]
    public void LegacyDualSelection_keepsHistoricalEffnetUntilExplicitChoice()
    {
        const string saved = "{\"Enabled\":[\"audio-tagging-fast\",\"audio-tagging-deep\",\"autotagging\"]}";
        var selection = JsonSerializer.Deserialize(saved, AlexandriaJsonContext.Default.FeatureSelection)!;
        var resolved = ServiceCatalog.Resolve(selection);

        selection.HasLegacyDualAudioSelection.Should().BeTrue();
        selection.Enabled.Should().Contain("audio-tagging-deep");
        selection.AudioBackbone.Should().Be("effnet");
        resolved.ServiceIds.Should().Contain("essentia-effnet-worker").And.NotContain("essentia-maest-worker");

        selection.SetAudioBackbone("maest");

        selection.HasLegacyDualAudioSelection.Should().BeFalse();
        selection.AudioBackbone.Should().Be("maest");
        selection.Enabled.Should().NotContain("audio-tagging-fast");
    }

    [Fact]
    public void InvalidChoice_preservesExistingChoice()
    {
        var selection = FeatureSelection.FromDefaults();

        var change = () => selection.SetAudioBackbone("both");

        change.Should().Throw<ArgumentException>();
        selection.AudioBackbone.Should().Be("effnet");
    }

    [Fact]
    public void CoreOnly_keepsMediaHealthEnabledAndDisablesOptionalHealth()
    {
        var features = new FeatureSelection();
        var environment = EnvGenerator.BuildValues(features, new Dictionary<string, string>(), new AdminAccountInput(), null);
        var compose = new ConfigurationService(new TemplateService("Local"))
            .GenerateDockerCompose(new InstallationConfig { Features = features, Ports = new() { ["HTTPS_PORT"] = 8443 } });

        environment["MEDIA_WORKER_ENABLED"].Should().Be("true");
        environment["PLAYLIST_WORKER_ENABLED"].Should().Be("true");
        environment["METADATA_WORKER_ENABLED"].Should().Be("false");
        compose.Should().Contain("VITE_STREAMING_ENABLED: \"true\"");
        compose.Should().Contain("Features__AdaptiveStreaming: \"${STREAMING_ENABLED:-false}\"");
        compose.Should().Contain("Features__MediaPreviews: \"${MEDIA_PROCESSING_ENABLED:-false}\"");
        compose.Should().Contain("HealthChecks__Workers__5__Name: PlaylistWorker");
        compose.Should().Contain("HealthChecks__Workers__5__Url: http://playlist-worker:8080/health");
        compose.Should().Contain("RabbitMQ__Consumer__SyncQueueName: playlist-queue");
    }
}
