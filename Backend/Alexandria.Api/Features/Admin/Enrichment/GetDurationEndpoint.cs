using Alexandria.Common.Repositories;
using Alexandria.Dto.Enrichment;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Enrichment;

/// <summary>
/// Per-backbone avg/median/max processing duration over terminal batch-files.
/// </summary>
internal sealed class GetDurationEndpoint(IEssentiaBatchFileRepository repository)
    : EndpointWithoutRequest<IReadOnlyList<EnrichmentDurationDto>>
{
    public override void Configure()
    {
        Get("/admin/enrichment/duration");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await repository.GetDurationAsync(ct);
        await Send.OkAsync(rows, ct);
    }
}