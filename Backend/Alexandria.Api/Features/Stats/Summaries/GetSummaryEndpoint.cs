using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.OverviewSummaries;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Stats.Summaries;

internal sealed class GetSummaryRequest
{
    public SummaryKind Kind { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

internal sealed class GetSummaryValidator : Validator<GetSummaryRequest>
{
    public GetSummaryValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.To.Value >= x.From.Value)
            .WithMessage("To must be after From");

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || (x.To.Value - x.From.Value).TotalDays <= 366)
            .WithMessage("Range must not exceed 366 days");
    }
}

internal sealed class GetSummaryEndpoint(IOverviewSummaryService summaries)
    : Endpoint<GetSummaryRequest, OverviewSummaryDto>
{
    public override void Configure()
    {
        Get("/stats/summaries/{Kind}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetSummaryRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var now = DateTime.UtcNow;
        var from = req.From?.UtcDateTime ?? new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = req.To?.UtcDateTime ?? now;

        if (to <= from || (to - from).TotalDays > 366 || from > now || to > now)
        {
            AddError(x => x.To, "Choose a past or current range after From, no longer than 366 days.");
            await Send.ErrorsAsync(cancellation: ct);

            return;
        }

        var period = new Common.Summaries.SummaryPeriod(from, to);

        await Send.OkAsync(await summaries.GetAsync(userId, req.Kind, period, ct), ct);
    }
}
