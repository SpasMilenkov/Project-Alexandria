using Alexandria.Common.Repositories;
using Alexandria.Dto.Enrichment;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Enrichment;

internal sealed class GetFailureRateRequest
{
    /// <summary>Range start (inclusive). Defaults to 24h before <c>To</c>.</summary>
    public DateTime? From { get; set; }

    /// <summary>Range end (exclusive). Defaults to now.</summary>
    public DateTime? To { get; set; }

    /// <summary>Bucketing granularity: <c>hour</c> or <c>day</c>.</summary>
    public EnrichmentBucket Bucket { get; set; } = EnrichmentBucket.Hour;
}

internal sealed class GetFailureRateEndpoint(IEssentiaBatchFileRepository repository)
    : Endpoint<GetFailureRateRequest, IReadOnlyList<EnrichmentFailureRatePointDto>>
{
    public override void Configure()
    {
        Get("/admin/enrichment/failure-rate");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(5);
    }

    public override async Task HandleAsync(GetFailureRateRequest req, CancellationToken ct)
    {
        var to = (req.To ?? DateTime.UtcNow).ToUniversalTime();
        var from = (req.From ?? to.AddHours(-24)).ToUniversalTime();

        var rows = await repository.GetFailureRateAsync(from, to, req.Bucket, ct);
        await Send.OkAsync(rows, ct);
    }
}