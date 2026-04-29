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

    private static StorageInfo ParseStorageMetrics(string metricsText)
    {
        var storageInfo = new StorageInfo();

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
        }

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