using Alexandria.Common.Repositories;
using Alexandria.Dto.Enrichment;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Enrichment;

internal sealed class GetBatchesRequest
{
    /// <summary>Number of most recent batches to return. Defaults to 20.</summary>
    public int Limit { get; set; } = 20;
}

internal sealed class GetBatchesEndpoint(IEssentiaBatchRepository repository)
    : Endpoint<GetBatchesRequest, IReadOnlyList<EnrichmentBatchDto>>
{
    public override void Configure()
    {
        Get("/admin/enrichment/batches");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(5);
    }

    public override async Task HandleAsync(GetBatchesRequest req, CancellationToken ct)
    {
        var limit = Math.Clamp(req.Limit, 1, 100);

        var rows = await repository.GetRecentBatchesAsync(limit, ct);
        await Send.OkAsync(rows, ct);
    }
}