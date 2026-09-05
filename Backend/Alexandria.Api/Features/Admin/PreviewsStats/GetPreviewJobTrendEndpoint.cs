using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.PreviewsStats;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.PreviewsStats;

internal sealed class GetPreviewJobTrendRequest
{
    /// <summary>Range start (inclusive, UTC). Defaults to 24h before <c>To</c>.</summary>
    public DateTime? From { get; set; }

    /// <summary>Range end (exclusive, UTC). Defaults to now.</summary>
    public DateTime? To { get; set; }

    /// <summary>Bucketing granularity: <c>Hour</c> or <c>Day</c>.</summary>
    public PreviewStatsBucket Bucket { get; set; } = PreviewStatsBucket.Hour;

    /// <summary>Optional job type filter. Only preview job types are accepted.</summary>
    public JobType? Type { get; set; }
}

sealed class GetPreviewJobTrendRequestValidator : Validator<GetPreviewJobTrendRequest>
{
    public GetPreviewJobTrendRequestValidator()
    {
        RuleFor(x => x.Bucket)
            .IsInEnum();

        RuleFor(x => x.Type)
            .Must(t => t is JobType.MediaPreview or JobType.DocumentPreview)
            .When(x => x.Type.HasValue)
            .WithMessage("Only preview job types can be queried here.");

        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must be after 'From'.");
    }
}

sealed class GetPreviewJobTrendEndpoint(IPreviewStatsService statsService)
    : Endpoint<GetPreviewJobTrendRequest, PreviewJobTrendResponse>
{
    public override void Configure()
    {
        Get("/admin/previews/job-trend");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(GetPreviewJobTrendRequest req, CancellationToken ct)
    {
        var to = (req.To ?? DateTime.UtcNow).ToUniversalTime();
        var from = (req.From ?? to.AddHours(-24)).ToUniversalTime();

        var result = await statsService.GetJobTrendAsync(req.Type, from, to, req.Bucket, ct);
        await Send.OkAsync(result, ct);
    }
}