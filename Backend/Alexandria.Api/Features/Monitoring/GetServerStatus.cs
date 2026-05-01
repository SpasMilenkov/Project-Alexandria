using Alexandria.Dto.Metrics;
using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Alexandria.Api.Features.Monitoring;

sealed class GetServerStatusEndpoint(HealthCheckService healthService)
    : EndpointWithoutRequest<ServerStatusResponse>
{
    public override void Configure()
    {
        Get("/monitoring");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var report = await healthService.CheckHealthAsync(ct);

        var response = new ServerStatusResponse(
            Status: report.Status.ToString(),
            CheckedAt: DateTimeOffset.UtcNow,
            Duration: report.TotalDuration.TotalMilliseconds,
            Summary: new HealthSummary(
                Healthy: report.Entries.Count(e => e.Value.Status == HealthStatus.Healthy),
                Degraded: report.Entries.Count(e => e.Value.Status == HealthStatus.Degraded),
                Unhealthy: report.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy)
            ),
            Checks: report.Entries.Select(e => new HealthCheckEntry(
                Name: e.Key,
                Status: e.Value.Status.ToString(),
                Description: e.Value.Description,
                Duration: e.Value.Duration.TotalMilliseconds,
                Tags: e.Value.Tags,
                Error: e.Value.Exception?.Message,
                Data: e.Value.Data
            ))
        );

        if (report.Status == HealthStatus.Healthy)
            await Send.OkAsync(response, ct);
        else
            await Send.ResponseAsync(response, statusCode: 503, cancellation: ct);
    }
}