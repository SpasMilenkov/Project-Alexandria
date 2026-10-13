using System.Text.Json;
using AwesomeAssertions;
using Builder.Services;
using Builder.Tests.Services;

namespace Builder.Tests.Integration;

public sealed class MonitoringRuntimeTests(ITestOutputHelper output)
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Monitoring_pinnedImages_scrapeCurrentApiAndInfrastructureAndCollectOnlyOwnLogs()
    {
        using var fixture = new CachedMonitoringFixture();
        fixture.Prepare();
        fixture.Start();
        await fixture.WaitFor(() => ApiHealthy(fixture), "the current API production Docker healthcheck");
        await fixture.WaitFor(() => fixture.Request("loki", "ready").Succeeded, "actual Loki HTTP readiness");
        await fixture.WaitFor(() => AllScrapesHealthy(fixture), "all eight configured Prometheus scrapes");
        await fixture.WaitFor(() => HasApiRequestRate(fixture), "current API HTTP telemetry across two scrapes");

        var token = fixture.Compose(["exec", "--no-TTY", "prometheus", "/bin/sh", "-c",
            "test -s /etc/prometheus/secrets/garage-metrics-token && cat /etc/prometheus/secrets/garage-metrics-token >/dev/null"]);
        fixture.RequireSuccess(token, "read the protected Garage token as the actual Prometheus container user");
        await fixture.WaitFor(() => HasLogs(fixture, "monitoring-log-probe", "ERR", fixture.Marker + "-plain") &&
            HasLogs(fixture, "monitoring-log-probe", "Information", fixture.Marker + "-json"), "plain and JSON Serilog log ingestion");
        HasLogs(fixture, "foreign-probe", "ERR", fixture.Marker + "-foreign").Should().BeFalse("Alloy must filter Docker discovery to this installation");
        await fixture.WaitFor(() => HasOwnContainerMetrics(fixture), "project-scoped cAdvisor labels and metrics");
        await fixture.WaitFor(() => fixture.Request("grafana", "api/dashboards/uid/alexandria-overview", grafanaAuth: true).Succeeded,
            "Grafana provisioning readiness");
        fixture.Request("grafana", "api/dashboards/uid/alexandria-overview").Succeeded.Should().BeFalse("dashboard access requires authentication");
        VerifyProvisionedQueries(fixture, output);

        var status = fixture.Compose(["ps", "--all", "--format", "json"]);
        fixture.RequireSuccess(status, "inspect monitoring host publications");

        foreach (var container in DockerJson.Objects(status.StandardOutput))
        {
            if (container.TryGetProperty("Publishers", out var publishers) && publishers.ValueKind == JsonValueKind.Array)
            {
                publishers.EnumerateArray().Should().NotContain(publisher => publisher.GetProperty("PublishedPort").GetInt32() != 0);
            }
        }
    }

    private static bool ApiHealthy(CachedMonitoringFixture fixture)
    {
        var status = fixture.Compose(["ps", "--all", "--format", "json", "api"]);

        return status.Succeeded && DockerJson.Objects(status.StandardOutput)
            .Any(container => DockerJson.Text(container, "Health") == "healthy");
    }

    private static bool AllScrapesHealthy(CachedMonitoringFixture fixture)
    {
        var response = fixture.Request("prometheus", "api/v1/query?query=up");
        if (!response.Succeeded) return false;
        using var document = JsonDocument.Parse(response.StandardOutput);
        var result = document.RootElement.GetProperty("data").GetProperty("result").EnumerateArray().ToArray();

        return result.Length == 8 && result.All(item => item.GetProperty("value")[1].GetString() == "1");
    }

    private static bool HasApiRequestRate(CachedMonitoringFixture fixture)
    {
        if (!fixture.Request("api", "api/status").Succeeded) return false;
        var query = "sum(rate(http_server_request_duration_seconds_count{job=\"alexandria-api\",http_route!=\"/metrics\"}[1m]))";
        var response = fixture.Request("prometheus", "api/v1/query?query=" + Uri.EscapeDataString(query));
        if (!response.Succeeded) return false;
        using var document = JsonDocument.Parse(response.StandardOutput);

        return document.RootElement.GetProperty("data").GetProperty("result").GetArrayLength() > 0;
    }

    private static bool HasLogs(CachedMonitoringFixture fixture, string service, string level, string marker)
    {
        var query = "{service=\"" + service + "\",level=\"" + level + "\"} |= \"" + marker + "\"";
        var response = fixture.Request("loki", "loki/api/v1/query_range?query=" + Uri.EscapeDataString(query) + "&limit=5");
        if (!response.Succeeded) return false;
        using var document = JsonDocument.Parse(response.StandardOutput);

        return document.RootElement.GetProperty("data").GetProperty("result").GetArrayLength() > 0;
    }

    private static bool HasOwnContainerMetrics(CachedMonitoringFixture fixture)
    {
        var query = "count by (container_label_com_docker_compose_project) (container_memory_working_set_bytes{job=\"cadvisor\"})";
        var response = fixture.Request("prometheus", "api/v1/query?query=" + Uri.EscapeDataString(query));
        if (!response.Succeeded) return false;
        using var document = JsonDocument.Parse(response.StandardOutput);
        var results = document.RootElement.GetProperty("data").GetProperty("result").EnumerateArray().ToArray();

        return results.Length == 1 && results[0].GetProperty("metric").GetProperty("container_label_com_docker_compose_project").GetString() == fixture.Project;
    }

    private static void VerifyProvisionedQueries(CachedMonitoringFixture fixture, ITestOutputHelper output)
    {
        var executed = 0;

        foreach (var filename in MonitoringConfigurationTests.DashboardFiles)
        {
            var response = fixture.Request("grafana", "api/dashboards/uid/" + Path.GetFileNameWithoutExtension(filename), grafanaAuth: true);
            fixture.RequireSuccess(response, "read provisioned dashboard through Grafana");
            using var document = JsonDocument.Parse(response.StandardOutput);
            var populated = 0;
            var counts = new List<string>();

            foreach (var panel in document.RootElement.GetProperty("dashboard").GetProperty("panels").EnumerateArray())
            {
                foreach (var target in panel.GetProperty("targets").EnumerateArray())
                {
                    var uid = target.GetProperty("datasource").GetProperty("uid").GetString();
                    var expression = target.GetProperty("expr").GetString()!
                        .Replace("$__rate_interval", "1m", StringComparison.Ordinal)
                        .Replace("$__auto", "1m", StringComparison.Ordinal)
                        .Replace("${service:regex}", ".+", StringComparison.Ordinal)
                        .Replace("${search:doublequote}", "\"\"", StringComparison.Ordinal);
                    var endpoint = uid == "alexandria-loki" ? "loki/api/v1/query_range" : "api/v1/query";
                    var query = fixture.Request("grafana", "api/datasources/proxy/uid/" + uid + "/" + endpoint + "?query=" + Uri.EscapeDataString(expression), grafanaAuth: true);
                    fixture.RequireSuccess(query, "execute dashboard panel query " + panel.GetProperty("title").GetString());
                    using var result = JsonDocument.Parse(query.StandardOutput);
                    result.RootElement.GetProperty("status").GetString().Should().Be("success");
                    executed++;
                    var count = result.RootElement.GetProperty("data").GetProperty("result").GetArrayLength();
                    if (count > 0) populated++;
                    counts.Add(panel.GetProperty("title").GetString() + ": " + count);
                }
            }

            var series = "";
            if (populated == 0)
            {
                var query = "count by (__name__) ({job=\"alexandria-api\"})";
                var metrics = fixture.Request("prometheus", "api/v1/query?query=" + Uri.EscapeDataString(query));
                fixture.RequireSuccess(metrics, "inspect actual API metric names");
                using var names = JsonDocument.Parse(metrics.StandardOutput);
                series = string.Join(", ", names.RootElement.GetProperty("data").GetProperty("result").EnumerateArray()
                    .Select(item => item.GetProperty("metric").GetProperty("__name__").GetString()));
            }

            populated.Should().BeGreaterThan(0, "dashboard {0} must display actual runtime data ({1}); actual API series: {2}", filename, string.Join("; ", counts), series);
            output.WriteLine(filename + ": " + string.Join("; ", counts));
        }

        output.WriteLine("Dashboard queries executed successfully: " + executed);
    }
}
