using Alexandria.Common.Repositories;
using Alexandria.Dto.Enrichment;
using FastEndpoints;

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
        ResponseCache(5);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await repository.GetDurationAsync(ct);
        await Send.OkAsync(rows, ct);
    }
}