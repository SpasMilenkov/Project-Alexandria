using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.OverviewSummaries;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Stats.Summaries;

internal sealed class ListSummariesRequest
{
    public SummaryKind Kind { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

internal sealed class ListSummariesValidator : Validator<ListSummariesRequest>
{
    public ListSummariesValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.To.Value >= x.From.Value)
            .WithMessage("To must be after From");
    }
}

internal sealed class ListSummariesEndpoint(IOverviewSummaryService summaries)
    : Endpoint<ListSummariesRequest, IReadOnlyList<OverviewSummaryHeaderDto>>
{
    public override void Configure()
    {
        Get("/stats/summaries");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(ListSummariesRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await summaries.ListAsync(
            userId, req.Kind, req.From?.UtcDateTime, req.To?.UtcDateTime, ct);

        await Send.OkAsync(result, ct);
    }
}
