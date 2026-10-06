using AwesomeAssertions;
using Builder.Models;
using Builder.Services;

namespace Builder.Tests.Services;

public class ConfigurationServiceTests
{
    private static ConfigurationService CreateSut() => new(new TemplateService("Local"));

    private static InstallationConfig CreateConfig(Action<FeatureSelection>? configure = null)
    {
        var features = new FeatureSelection();

        configure?.Invoke(features);

        return new InstallationConfig
        {
            Features = features,
            Ports = new Dictionary<string, int>
            {
                ["HTTP_PORT"] = 80,
                ["HTTPS_PORT"] = 443,
                ["GRAFANA_PORT"] = 3000,
            },
            Credentials = new Dictionary<string, string>
            {
                ["GARAGE_RPC_SECRET"] = "rpc-secret-value",
                ["GARAGE_ADMIN_TOKEN"] = "admin-token-value",
                ["GARAGE_METRICS_TOKEN"] = "metrics-token-value",
            },
            InstallPath = "/opt/alexandria",
            SourceRoot = "/opt/alexandria",
        };
    }

    [Fact]
    public void GenerateDockerCompose_always_containsCoreServices()
    {
        var sut = CreateSut();

        var compose = sut.GenerateDockerCompose(CreateConfig());

        compose.Should().Contain("  postgres:");
        compose.Should().Contain("  garage:");
        compose.Should().Contain("  garage-init:");
        compose.Should().Contain("  rabbitmq-config:");
        compose.Should().Contain("  rabbitmq:");
        compose.Should().Contain("  api:");
        compose.Should().Contain("  frontend:");
        compose.Should().Contain("  nginx:");
    }

    [Fact]
    public void GenerateDockerCompose_defaultFeatures_omitsOptionalWorkersAndMonitoring()
    {
        var sut = CreateSut();
        var config = CreateConfig(f => f.SetEnabled(FeatureCatalog.Monitoring.Id, false));

        var compose = sut.GenerateDockerCompose(config);

        compose.Should().NotContain("  media-worker:");
        compose.Should().NotContain("  document-worker:");
        compose.Should().NotContain("  transpilation-worker:");
        compose.Should().NotContain("  lyrics-worker:");
        compose.Should().NotContain("  essentia-effnet-worker:");
        compose.Should().NotContain("  essentia-maest-worker:");
        compose.Should().NotContain("  media-metadata-worker:");
        compose.Should().NotContain("  prometheus:");
        compose.Should().NotContain("  grafana:");
    }

    [Fact]
    public void GenerateDockerCompose_allFeaturesEnabled_includesEveryService()
    {
        var sut = CreateSut();

        var config = CreateConfig(f =>
        {
            foreach (var feature in FeatureCatalog.All)
            {
                f.SetEnabled(feature.Id, true);
            }
        });

        var compose = sut.GenerateDockerCompose(config);

        compose.Should().Contain("  media-worker:");
        compose.Should().Contain("  document-worker:");
        compose.Should().Contain("  transpilation-worker:");
        compose.Should().Contain("  lyrics-worker:");
        compose.Should().Contain("  media-metadata-worker:");
        compose.Should().Contain("  essentia-effnet-worker:");
        compose.Should().Contain("  essentia-maest-worker:");
        compose.Should().Contain("  prometheus:");
        compose.Should().Contain("  grafana:");
        compose.Should().Contain("  cadvisor:");
        compose.Should().Contain("  postgres-exporter:");
        compose.Should().Contain("  alloy:");
        compose.Should().Contain("  loki:");
    }

    [Fact]
    public void GenerateDockerCompose_never_leavesUnresolvedTemplateTokens()
    {
        var sut = CreateSut();

        var compose = sut.GenerateDockerCompose(CreateConfig(f => f.SetEnabled(FeatureCatalog.Monitoring.Id, true)));

        compose.Should().NotContain("{{");
    }

    [Fact]
    public void GenerateDockerCompose_internalTarget_usesRelativeSourcePaths()
    {
        var sut = CreateSut();

        var compose = sut.GenerateDockerCompose(CreateConfig());

        compose.Should().Contain("context: ./Backend");
        compose.Should().Contain("context: .\n");
    }

    [Fact]
    public void GenerateDockerCompose_externalTarget_absolutizesSourcePaths()
    {
        var sut = CreateSut();
        var config = CreateConfig();

        config.SourceRoot = "/home/user/repos/Project-Alexandria";

        var compose = sut.GenerateDockerCompose(config);

        compose.Should().Contain("context: /home/user/repos/Project-Alexandria/Backend");
        compose.Should().Contain("/home/user/repos/Project-Alexandria/nginx/nginx.conf");
        compose.Should().NotContain("context: ./Backend");
    }

    [Fact]
    public void GenerateDockerCompose_monitoringDisabled_hasNoObservabilityNetwork()
    {
        var sut = CreateSut();

        var compose = sut.GenerateDockerCompose(CreateConfig());

        compose.Should().NotContain("observability-net");
    }

    [Fact]
    public void GenerateDockerCompose_taggingWithoutMonitoring_keepsEssentiaNetworkForRabbitmqMetricsPath()
    {
        var sut = CreateSut();
        var config = CreateConfig(f => f.SetEnabled(FeatureCatalog.AudioTaggingFast.Id, true));

        var compose = sut.GenerateDockerCompose(config);

        compose.Should().Contain("essentia-net");
        compose.Should().NotContain("observability-net");
    }

    [Fact]
    public void GenerateGarageToml_embedsGeneratedSecretTokens()
    {
        var sut = CreateSut();

        var toml = sut.GenerateGarageToml(CreateConfig());

        toml.Should().Contain("rpc_secret = \"rpc-secret-value\"");
        toml.Should().Contain("admin_token = \"admin-token-value\"");
        toml.Should().Contain("metrics_token = \"metrics-token-value\"");
        toml.Should().NotContain("{{");
    }

    [Fact]
    public void GeneratePrometheusYml_readsTokenFromFile_neverInlinesCredentials()
    {
        var sut = CreateSut();

        var yml = sut.GeneratePrometheusYml();

        yml.Should().Contain("credentials_file: /etc/prometheus/secrets/garage-metrics-token");
        yml.Should().NotContain("credentials:");
        yml.Should().NotContain("metrics-token-value");
    }
}
