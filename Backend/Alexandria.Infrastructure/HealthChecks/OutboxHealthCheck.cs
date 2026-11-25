using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Alexandria.Infrastructure.HealthChecks;

/// <summary>
/// Fails once outbox messages wait longer than they should. A failed publish is quiet by design
/// (the row just retries), so this is what notices a relay that stopped or a backlog that grows.
/// </summary>
public class OutboxHealthCheck(
    IServiceScopeFactory scopeFactory,
    IMessageBrokerStatus broker,
    IOptions<OutboxOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // The rabbitmq check already reports an outage; a backlog during one is expected.
        if (!broker.IsAvailable)
            return HealthCheckResult.Healthy("Waiting for the message broker");

        await using var scope = scopeFactory.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var oldest = await unitOfWork.OutboxMessages.GetOldestPendingCreatedAtAsync(cancellationToken);
        if (oldest is null)
            return HealthCheckResult.Healthy("No messages waiting");

        var waited = DateTime.UtcNow - oldest.Value;
        var limit = TimeSpan.FromMinutes(Math.Max(options.Value.BacklogAlertMinutes, 1));

        if (waited <= limit)
            return HealthCheckResult.Healthy($"Oldest message waiting {waited.TotalSeconds:F0}s");

        return HealthCheckResult.Unhealthy(
            $"Outbox messages have been waiting {waited.TotalMinutes:F0} min; the relay may be stuck or the broker refusing them");
    }
}
