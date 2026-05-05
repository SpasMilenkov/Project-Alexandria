using Alexandria.Common.Config;
using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Alexandria.Infrastructure.HealthChecks;

public class S3HealthCheck(IAmazonS3 s3Client, IOptions<S3Config> config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.ListBucketsAsync(cancellationToken);
            var provider = config.Value.Provider;
            return HealthCheckResult.Healthy($"Provider: {provider}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("S3 unreachable", ex);
        }
    }
}