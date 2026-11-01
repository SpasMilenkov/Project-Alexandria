using Alexandria.Common.Repositories;
using Alexandria.Dto.Enrichment;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Enrichment;

/// <summary>
/// Live count of queued (dispatched) batch-files, split by backbone and file status.
/// </summary>
internal sealed class GetQueueDepthEndpoint(IEssentiaBatchFileRepository repository)
    : EndpointWithoutRequest<IReadOnlyList<EnrichmentQueueDepthDto>>
{
    public override void Configure()
    {
        Get("/admin/enrichment/queue-depth");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await repository.GetQueueDepthAsync(ct);
        await Send.OkAsync(rows, ct);
    }
}