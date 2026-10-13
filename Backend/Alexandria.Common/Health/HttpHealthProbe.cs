namespace Alexandria.Common.Health;

public static class HttpHealthProbe
{
    public static async Task<int?> TryRunAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0 || args[0] != "--healthcheck")
            return null;

        var address = args.Length == 2 ? args[1] : "http://127.0.0.1:8080/health";
        if (args.Length > 2 || !Uri.TryCreate(address, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
            return 1;

        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };

        try
        {
            using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return 1;
        }
    }
}
