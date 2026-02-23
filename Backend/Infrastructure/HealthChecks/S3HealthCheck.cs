using Amazon.S3;
using Common.Config;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Infrastructure.HealthChecks;

public class S3HealthCheck(IAmazonS3 s3Client, IOptions<S3Config> config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await s3Client.ListBucketsAsync(ct);
            var provider = config.Value.Provider;
            return HealthCheckResult.Healthy($"Provider: {provider}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("S3 unreachable", ex);
        }
    }
}
