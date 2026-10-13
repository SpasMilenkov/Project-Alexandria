using System.Text.Json;
using AwesomeAssertions;
using Builder.Services;
using Builder.Tests.Services;

namespace Builder.Tests.Integration;

public sealed class GrafanaProvisioningTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Grafana_cachedPinnedImage_provisionsRepoDashboardsAndRequiresAuthentication()
    {
        using var fixture = new CachedGrafanaProvisioningFixture();
        fixture.Prepare();
        await fixture.StartAsync();

        fixture.Request("api/search", authenticated: false).Succeeded.Should().BeFalse("anonymous dashboard access must be denied");
        var datasourceResponse = fixture.Request("api/datasources");
        fixture.RequireSuccess(datasourceResponse, "read provisioned datasources");
        using var datasources = JsonDocument.Parse(datasourceResponse.StandardOutput);
        datasources.RootElement.EnumerateArray().Select(item => item.GetProperty("uid").GetString())
            .Should().BeEquivalentTo(["alexandria-prometheus", "alexandria-loki"]);

        foreach (var filename in MonitoringConfigurationTests.DashboardFiles)
        {
            var uid = Path.GetFileNameWithoutExtension(filename);
            var response = fixture.Request("api/dashboards/uid/" + uid);
            fixture.RequireSuccess(response, "read provisioned dashboard " + uid);
            using var document = JsonDocument.Parse(response.StandardOutput);
            var metadata = document.RootElement.GetProperty("meta");
            metadata.GetProperty("provisioned").GetBoolean().Should().BeTrue();
            metadata.GetProperty("folderUid").GetString().Should().Be("alexandria");
            metadata.GetProperty("url").GetString().Should().StartWith("/monitoring/d/");
            document.RootElement.GetProperty("dashboard").GetProperty("panels").GetArrayLength().Should().BeGreaterThan(2);
        }

        var status = fixture.Status();
        fixture.RequireSuccess(status, "read Grafana health and publication status");
        var container = DockerJson.Objects(status.StandardOutput).Select(element => element.Clone()).Single();
        container.GetProperty("Health").GetString().Should().Be("healthy");

        if (container.TryGetProperty("Publishers", out var publishers) && publishers.ValueKind == JsonValueKind.Array)
        {
            publishers.EnumerateArray().Should().NotContain(publisher => publisher.GetProperty("PublishedPort").GetInt32() != 0);
        }
    }
}
