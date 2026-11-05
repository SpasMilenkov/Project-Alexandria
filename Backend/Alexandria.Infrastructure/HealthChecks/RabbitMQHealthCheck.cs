using Alexandria.Common.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Alexandria.Infrastructure.HealthChecks;

// Reads the supervisor-maintained state instead of connecting, so /health answers instantly
// even while the broker is down.
public class RabbitMqHealthCheck(IMessageBrokerStatus broker) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = broker.IsAvailable
            ? HealthCheckResult.Healthy("Connected")
            : HealthCheckResult.Unhealthy("RabbitMQ is not connected");

        return Task.FromResult(result);
    }
}
