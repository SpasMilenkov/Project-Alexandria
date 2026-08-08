using Alexandria.Common.Repositories;
using Alexandria.Common.Settings;
using Alexandria.Dto.Enrichment;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace Alexandria.Api.Features.Admin.Enrichment;

/// <summary>
/// Batch-files still <c>Pending</c> inside a dispatched batch past the configured stuck
/// threshold — the admin "stuck" panel.
/// </summary>
internal sealed class GetStuckEndpoint(
    IEssentiaBatchFileRepository repository,
    IOptions<EnrichmentMonitoringOptions> options)
    : EndpointWithoutRequest<IReadOnlyList<EnrichmentStuckDto>>
{
    public override void Configure()
    {
        Get("/admin/enrichment/stuck");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(5);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var threshold = Math.Max(options.Value.StuckThresholdMinutes, 1);
        var olderThan = DateTime.UtcNow.AddMinutes(-threshold);

        var rows = await repository.GetStuckAsync(olderThan, ct);
        await Send.OkAsync(rows, ct);
    }
}