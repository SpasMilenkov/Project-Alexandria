using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Audit;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Activity.GetActivitySummary;

internal sealed class ActivityQueryValidator : Validator<ActivityQuery>
{
    public ActivityQueryValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty()
            .LessThan(x => x.EndDate)
            .WithMessage("StartDate must be before EndDate.");

        RuleFor(x => x.EndDate)
            .NotEmpty()
            .GreaterThan(x => x.StartDate)
            .WithMessage("EndDate must be after StartDate.");

        RuleFor(x => x)
            .Must(x => x.StartDate.Date.AddYears(1) == x.EndDate.Date)
            .WithMessage("The date range must be exactly 1 year.");
    }
}

internal sealed class GetActivitySummaryEndpoint(IAuditService auditService)
    : Endpoint<ActivityQuery, ActivityStatisticsOverview>
{
    public override void Configure()
    {
        Get("/activity/summary");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(ActivityQuery req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await auditService.GetActivityOverviewAsync(userId, req, ct);

        await Send.OkAsync(result, cancellation: ct);
    }
}