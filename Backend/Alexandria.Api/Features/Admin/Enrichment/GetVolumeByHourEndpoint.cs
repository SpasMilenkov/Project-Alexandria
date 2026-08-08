using Alexandria.Common.Repositories;
using Alexandria.Dto.Enrichment;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Enrichment;

internal sealed class GetVolumeByHourRequest
{
    /// <summary>Range start (inclusive). Defaults to 24h before <c>To</c>.</summary>
    public DateTime? From { get; set; }

    /// <summary>Range end (exclusive). Defaults to now.</summary>
    public DateTime? To { get; set; }
}

internal sealed class GetVolumeByHourEndpoint(IEssentiaBatchFileRepository repository)
    : Endpoint<GetVolumeByHourRequest, IReadOnlyList<EnrichmentVolumePointDto>>
{
    public override void Configure()
    {
        Get("/admin/enrichment/volume-by-hour");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(5);
    }

    public override async Task HandleAsync(GetVolumeByHourRequest req, CancellationToken ct)
    {
        var to = (req.To ?? DateTime.UtcNow).ToUniversalTime();
        var from = (req.From ?? to.AddHours(-24)).ToUniversalTime();

        var rows = await repository.GetVolumeByHourAsync(from, to, ct);
        await Send.OkAsync(rows, ct);
    }
}