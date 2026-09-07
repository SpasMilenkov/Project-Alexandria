using System.Net.Http.Headers;
using Alexandria.Common.Config;
using Alexandria.Dto.Metrics;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Storage;

public class MetricsService(HttpClient httpClient, IOptions<S3Config> config)
{
    public async Task<StorageInfo> GetStorageInfoAsync()
    {
        var providerSettings = config.Value.ProviderSettings["Garage"];

        var request = new HttpRequestMessage(HttpMethod.Get, providerSettings.MetricsUrl);

        if (!string.IsNullOrEmpty(providerSettings.MetricsToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", providerSettings.MetricsToken);
        }

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var metricsText = await response.Content.ReadAsStringAsync();
        return ParseStorageMetrics(metricsText);
    }

    public async Task<List<StorageInfo>> GetAllNodesStorageAsync(string[] nodes)
    {
        var providerSettings = config.Value.ProviderSettings["Garage"];

        var tasks = nodes.Select(async node =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, node);

            if (!string.IsNullOrEmpty(providerSettings.MetricsToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", providerSettings.MetricsToken);
            }

            var response = await httpClient.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            return ParseStorageMetrics(text);
        });

        return [.. await Task.WhenAll(tasks)];
    }

    /// <summary>
    /// Parses per-node assigned capacities from cluster layout series. Only the
    /// connected series carries role_capacity AND connectivity; the disconnected_time
    /// series repeats the same labels and must not be double counted.
    /// </summary>
    internal static IReadOnlyList<GarageNodeCapacity> ParseClusterNodes(string metricsText)
    {
        var nodes = new Dictionary<string, GarageNodeCapacity>();

        foreach (var line in metricsText.Split('\n'))
        {
            if (!line.StartsWith("cluster_layout_node_connected", StringComparison.Ordinal))
                continue;

            var node = ParseNodeLine(line);
            if (node is not null)
                nodes[node.NodeId] = node;
        }

        return [.. nodes.Values];
    }

    internal static GarageNodeCapacity? ParseNodeLine(string line)
    {
        // cluster_layout_node_connected{id="07bf..",role_capacity="10000000000",...} 1
        var braceStart = line.IndexOf('{');
        var braceEnd = line.IndexOf('}');
        if (braceStart < 0 || braceEnd <= braceStart)
            return null;

        var labels = line.Substring(braceStart + 1, braceEnd - braceStart - 1);
        var nodeId = GetLabel(labels, "id");
        var capacity = GetLabel(labels, "role_capacity");

        if (nodeId == null || capacity == null || !long.TryParse(capacity, out var bytes))
            return null;

        var connected = line[(braceEnd + 1)..].Trim().Equals("1", StringComparison.Ordinal);

        return new GarageNodeCapacity
        {
            NodeId = nodeId,
            RoleCapacityBytes = bytes,
            Connected = connected
        };
    }

    private static string? GetLabel(string labels, string name)
    {
        var key = name + "=\"";
        var start = labels.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += key.Length;
        var end = labels.IndexOf('"', start);
        if (end <= start)
            return null;

        return labels.Substring(start, end - start);
    }

    private static StorageInfo ParseStorageMetrics(string metricsText)
    {
        var storageInfo = new StorageInfo();
        var nodes = new Dictionary<string, GarageNodeCapacity>();

        foreach (var line in metricsText.Split('\n'))
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("garage_local_disk_avail"))
            {
                if (line.Contains("volume=\"data\""))
                    storageInfo.DataAvailableBytes = ExtractValue(line);
                else if (line.Contains("volume=\"metadata\""))
                    storageInfo.MetadataAvailableBytes = ExtractValue(line);
            }
            else if (line.StartsWith("garage_local_disk_total"))
            {
                if (line.Contains("volume=\"data\""))
                    storageInfo.DataTotalBytes = ExtractValue(line);
                else if (line.Contains("volume=\"metadata\""))
                    storageInfo.MetadataTotalBytes = ExtractValue(line);
            }
            else if (line.StartsWith("cluster_layout_node_connected", StringComparison.Ordinal))
            {
                var node = ParseNodeLine(line);
                if (node is not null)
                    nodes[node.NodeId] = node;
            }
        }

        storageInfo.GarageNodes = [.. nodes.Values];
        storageInfo.GarageCapacityBytes = nodes.Values.Sum(n => n.RoleCapacityBytes);

        return storageInfo;
    }

    private static long ExtractValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && long.TryParse(parts[^1], out var value))
            return value;
        return 0;
    }
}