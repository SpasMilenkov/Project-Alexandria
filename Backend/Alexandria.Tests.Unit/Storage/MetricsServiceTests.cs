using System.Net;
using Alexandria.Common.Config;
using Alexandria.Services.Storage;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.Storage;

public class MetricsServiceTests
{
    private const string SampleMetrics = """
                                         cluster_layout_node_connected{id="node1",role_capacity="10000000000",role_gateway="0",role_zone="dc1"} 1
                                         cluster_layout_node_connected{id="node2",role_capacity="20000000000",role_gateway="0",role_zone="dc1"} 0
                                         cluster_layout_node_disconnected_time{id="node1",role_capacity="10000000000",role_gateway="0",role_zone="dc1"} 0
                                         garage_local_disk_avail{volume="data"} 36120403968
                                         garage_local_disk_total{volume="data"} 493408342016
                                         garage_local_disk_avail{volume="metadata"} 36120403968
                                         garage_local_disk_total{volume="metadata"} 493408342016
                                         """;

    private sealed class StubHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
    }

    private static MetricsService CreateSut(string payload)
    {
        var httpClient = new HttpClient(new StubHandler(payload));
        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config
        {
            ProviderSettings = new Dictionary<string, ProviderSettings>
            {
                ["Garage"] = new ProviderSettings { MetricsUrl = "http://garage:3903/metrics" }
            }
        });
        return new MetricsService(httpClient, config);
    }

    [Fact]
    public async Task parses_role_capacity_sum_and_nodes_without_double_counting()
    {
        var sut = CreateSut(SampleMetrics);

        var result = await sut.GetStorageInfoAsync();

        result.GarageCapacityBytes.Should().Be(30_000_000_000L);
        result.GarageNodes.Should().HaveCount(2);
        result.GarageNodes.Should().ContainSingle(n => n.NodeId == "node1" && n.Connected);
        result.GarageNodes.Should()
            .ContainSingle(n => n.NodeId == "node2" && !n.Connected && n.RoleCapacityBytes == 20_000_000_000L);
    }

    [Fact]
    public async Task keeps_disk_parsing()
    {
        var sut = CreateSut(SampleMetrics);

        var result = await sut.GetStorageInfoAsync();

        result.DataAvailableBytes.Should().Be(36_120_403_968L);
        result.DataTotalBytes.Should().Be(493_408_342_016L);
        result.MetadataAvailableBytes.Should().Be(36_120_403_968L);
        result.MetadataTotalBytes.Should().Be(493_408_342_016L);
    }

    [Fact]
    public void malformed_node_lines_are_skipped()
    {
        var text = string.Join('\n',
            "cluster_layout_node_connected without braces",
            "cluster_layout_node_connected{id=\"n1\"} 1",
            "cluster_layout_node_connected{id=\"n1\",role_capacity=\"nope\"} 1",
            "cluster_layout_node_connected{id=\"n1\",role_capacity=\"100\"} 1");

        var nodes = MetricsService.ParseClusterNodes(text);

        nodes.Should().ContainSingle(n => n.NodeId == "n1" && n.RoleCapacityBytes == 100L);
    }

    [Fact]
    public void node_line_without_braces_returns_null()
    {
        MetricsService.ParseNodeLine("cluster_layout_node_connected 1").Should().BeNull();
    }
}