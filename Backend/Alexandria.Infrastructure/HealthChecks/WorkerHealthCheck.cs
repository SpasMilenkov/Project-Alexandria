using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Alexandria.Infrastructure.HealthChecks;

public class WorkerHealthCheck(IHttpClientFactory factory, string workerUrl, string workerName)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var client = factory.CreateClient("worker-health");
            var response = await client.GetAsync(workerUrl, ct);

            if (!response.IsSuccessStatusCode)
                return HealthCheckResult.Unhealthy($"HTTP {(int)response.StatusCode}");

            // Forward the worker's own health JSON as data
            var body = await response.Content.ReadAsStringAsync(ct);
            var data = new Dictionary<string, object> { ["response"] = body };
            return HealthCheckResult.Healthy("Responding", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Unreachable", ex);
        }
    }
}