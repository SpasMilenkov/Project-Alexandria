using API.Features.Auth.Extensions;
using Common.Services;
using DTO.Audit;
using FastEndpoints;
using FluentValidation;

namespace API.Features.Activity.GetActivitySummary;

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

internal sealed class GetActivitySummaryEndpoint(IAuditService auditService) : Endpoint<ActivityQuery, ActivityStatisticsOverview>
{
    public override void Configure()
    {
        Get("/activity/summary");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(ActivityQuery req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await auditService.GetActivityOverview(userId, req, ct);

        await Send.OkAsync(result, cancellation: ct);
    }
}
