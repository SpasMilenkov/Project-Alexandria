using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Infrastructure.HealthChecks;

public class RabbitMqHealthCheck(IConnection connection) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var result = connection.IsOpen
            ? HealthCheckResult.Healthy("Connected")
            : HealthCheckResult.Unhealthy("Connection closed");
        return Task.FromResult(result);
    }
}
