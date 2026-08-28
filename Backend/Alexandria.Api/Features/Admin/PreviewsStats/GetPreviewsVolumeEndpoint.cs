using Alexandria.Common.Services;
using Alexandria.Dto.PreviewsStats;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.PreviewsStats;

internal sealed class GetPreviewsVolumeRequest
{
    /// <summary>Range start (inclusive, UTC). Defaults to 24h before <c>To</c>.</summary>
    public DateTime? From { get; set; }

    /// <summary>Range end (exclusive, UTC). Defaults to now.</summary>
    public DateTime? To { get; set; }

    /// <summary>Bucketing granularity: <c>Hour</c> or <c>Day</c>.</summary>
    public PreviewStatsBucket Bucket { get; set; } = PreviewStatsBucket.Hour;
}

sealed class GetPreviewsVolumeRequestValidator : Validator<GetPreviewsVolumeRequest>
{
    public GetPreviewsVolumeRequestValidator()
    {
        RuleFor(x => x.Bucket)
            .IsInEnum();

        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must be after 'From'.");
    }
}

sealed class GetPreviewsVolumeEndpoint(IPreviewStatsService statsService)
    : Endpoint<GetPreviewsVolumeRequest, PreviewVolumeResponse>
{
    public override void Configure()
    {
        Get("/admin/previews/volume");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(GetPreviewsVolumeRequest req, CancellationToken ct)
    {
        var to = (req.To ?? DateTime.UtcNow).ToUniversalTime();
        var from = (req.From ?? to.AddHours(-24)).ToUniversalTime();

        var result = await statsService.GetVolumeAsync(from, to, req.Bucket, ct);
        await Send.OkAsync(result, ct);
    }
}