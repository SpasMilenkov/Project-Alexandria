using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.PreviewsStats;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.PreviewsStats;

internal sealed class GetPreviewJobOverviewRequest
{
    /// <summary>Optional job type filter. Only preview job types are accepted.</summary>
    public JobType? Type { get; set; }

    public int DurationWindowDays { get; set; } = 30;
}

sealed class GetPreviewJobOverviewRequestValidator : Validator<GetPreviewJobOverviewRequest>
{
    public GetPreviewJobOverviewRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => t is JobType.MediaPreview or JobType.DocumentPreview)
            .When(x => x.Type.HasValue)
            .WithMessage("Only preview job types can be queried here.");

        RuleFor(x => x.DurationWindowDays)
            .InclusiveBetween(1, 365);
    }
}

sealed class GetPreviewJobOverviewEndpoint(IPreviewStatsService statsService)
    : Endpoint<GetPreviewJobOverviewRequest, PreviewJobOverviewResponse>
{
    public override void Configure()
    {
        Get("/admin/previews/job-overview");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(10);
    }

    public override async Task HandleAsync(GetPreviewJobOverviewRequest req, CancellationToken ct)
    {
        var result = await statsService.GetJobOverviewAsync(req.Type, req.DurationWindowDays, ct);
        await Send.OkAsync(result, ct);
    }
}