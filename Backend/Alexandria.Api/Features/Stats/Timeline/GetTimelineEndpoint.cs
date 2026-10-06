using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Stats;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Stats.Timeline;

internal sealed class GetTimelineRequest
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

internal sealed class GetTimelineValidator : Validator<GetTimelineRequest>
{
    public GetTimelineValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("To must be after From");

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || (x.To.Value - x.From.Value).TotalDays <= 366)
            .WithMessage("Range must not exceed 366 days");
    }
}

internal sealed class GetTimelineEndpoint(IListeningStatsService statsService)
    : Endpoint<GetTimelineRequest, TimelineResponse>
{
    public override void Configure()
    {
        Get("/stats/timeline");
        Policies(Common.Auth.Policies.RequireUser);
        // No ResponseCache: the payload is per-user and the cache key
        // carries no user dimension, so caching would leak rows across users.
    }

    public override async Task HandleAsync(GetTimelineRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var now = DateTime.UtcNow;
        var from = req.From?.UtcDateTime ?? now.AddDays(-90);
        var to = req.To?.UtcDateTime ?? now;

        if (to <= from || (to - from).TotalDays > 366 || from > now || to > now)
        {
            AddError(x => x.To, "Choose a past or current range after From, no longer than 366 days.");
            await Send.ErrorsAsync(cancellation: ct);

            return;
        }

        await Send.OkAsync(await statsService.GetTimelineAsync(userId, from, to, ct), ct);
    }
}
