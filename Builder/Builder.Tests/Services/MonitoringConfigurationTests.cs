using System.Text.Json;
using AwesomeAssertions;
using Builder.Models;

namespace Builder.Tests.Services;

public sealed class MonitoringConfigurationTests
{
    internal static readonly string[] DashboardFiles =
    [
        "alexandria-overview.json", "alexandria-api.json", "alexandria-logs.json",
    ];

    [Fact]
    public void Generate_monitoringSelected_scopesContainerMetricsWithoutEmbeddingSecrets()
    {
        using var fixture = new ConfigurationArtifactFixture(monitoring: true);
        var config = fixture.Resolve();
        fixture.Generate(config);
        var prometheus = File.ReadAllText(Path.Combine(fixture.Target, "prometheus", "prometheus.yml"));

        prometheus.Should().Contain("regex: '" + config.ProjectName + "'")
            .And.Contain("container_label_com_docker_compose_project")
            .And.Contain("credentials_file: /etc/prometheus/secrets/garage-metrics-token")
            .And.Contain("job_name: alexandria-api\n    metric_name_validation_scheme: legacy")
            .And.Contain("'api:8080'").And.Contain("'loki:3100'").And.Contain("'alloy:12345'")
            .And.NotContain("{{").And.NotContain("-worker:")
            .And.NotContain(config.EnvironmentValues["GARAGE_S3_METRICS_TOKEN"]);

        var compose = File.ReadAllText(Path.Combine(fixture.Target, "docker-compose.yml"));
        compose.Should().Contain("--storage.tsdb.retention.time=15d")
            .And.Contain("--storage.tsdb.retention.size=2GB")
            .And.Contain("ALLOY_DOCKER_PROJECT: ${COMPOSE_PROJECT_NAME:?COMPOSE_PROJECT_NAME is required}")
            .And.Contain("GF_AUTH_ANONYMOUS_ENABLED=false");

        ServiceCatalog.Resolve(config.Features).Services.Single(service => service.Id == "prometheus")
            .Networks.Should().NotContain("essentia-net");
    }

    [Fact]
    public void Generate_monitoringOmitted_writesNoMetricsSecretOrCollectorConfig()
    {
        using var fixture = new ConfigurationArtifactFixture(monitoring: false);
        fixture.Generate(fixture.Resolve());

        Directory.Exists(Path.Combine(fixture.Target, "prometheus")).Should().BeFalse();
    }

    [Fact]
    public void Provisioning_repoDashboards_haveStableDatasourcesAndExecutableQueries()
    {
        var source = ConfigurationArtifactFixture.FindRepositoryRoot();
        var uids = new HashSet<string>(StringComparer.Ordinal);
        var requiredFiles = ServiceCatalog.All.Single(service => service.Id == "grafana").SourceFiles;

        foreach (var filename in DashboardFiles)
        {
            var relative = "grafana/provisioning/dashboards/" + filename;
            requiredFiles.Should().Contain(relative, "missing dashboards must fail source preflight");
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, relative)));
            var dashboard = document.RootElement;
            uids.Add(dashboard.GetProperty("uid").GetString()!).Should().BeTrue();
            dashboard.GetProperty("panels").GetArrayLength().Should().BeGreaterThan(2);

            foreach (var panel in dashboard.GetProperty("panels").EnumerateArray())
            {
                var datasource = panel.GetProperty("datasource").GetProperty("uid").GetString();
                datasource.Should().BeOneOf("alexandria-prometheus", "alexandria-loki");

                foreach (var target in panel.GetProperty("targets").EnumerateArray())
                {
                    target.GetProperty("datasource").GetProperty("uid").GetString().Should().Be(datasource);
                    target.GetProperty("expr").GetString().Should().NotBeNullOrWhiteSpace();
                }
            }
        }
    }

    [Fact]
    public void Provisioning_apiQueries_referenceConfiguredOpenTelemetryMetrics()
    {
        var source = ConfigurationArtifactFixture.FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(source, "grafana", "provisioning", "dashboards", "alexandria-api.json"));

        dashboard.Should().Contain("http_server_request_duration_seconds_count")
            .And.Contain("http_server_request_duration_seconds_bucket")
            .And.Contain("process_memory_usage_bytes")
            .And.Contain("process_cpu_time_seconds_total");
    }
}
