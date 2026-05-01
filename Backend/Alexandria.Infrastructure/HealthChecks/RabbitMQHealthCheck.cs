using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.HealthChecks;

public class RabbitMqHealthCheck(IConnection connection) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = connection.IsOpen
            ? HealthCheckResult.Healthy("Connected")
            : HealthCheckResult.Unhealthy("Connection closed");
        return Task.FromResult(result);
    }
}