using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Alexandria.Infrastructure.HealthChecks;

public class RabbitMqHealthCheck(Lazy<Task<IConnection>> connectionLazy) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connection = await connectionLazy.Value;
        return connection.IsOpen
            ? HealthCheckResult.Healthy("Connected")
            : HealthCheckResult.Unhealthy("Connection closed");
    }
}
