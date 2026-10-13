using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Stats;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Stats.TopSongs;

internal sealed class GetTopSongsRequest
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Limit { get; init; } = 5;
}

internal sealed class GetTopSongsValidator : Validator<GetTopSongsRequest>
{
    public GetTopSongsValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 20);

        RuleFor(x => x.To)
            .GreaterThan(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("To must be after From");

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || (x.To.Value - x.From.Value).TotalDays <= 366)
            .WithMessage("Range must not exceed 366 days");
    }
}

internal sealed class GetTopSongsEndpoint(IListeningStatsService statsService)
    : Endpoint<GetTopSongsRequest, TopSongsResponse>
{
    public override void Configure()
    {
        Get("/stats/top-songs");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetTopSongsRequest req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var from = req.From?.UtcDateTime ?? now.Date.AddDays(-6);
        var to = req.To?.UtcDateTime ?? now;

        if (to <= from || (to - from).TotalDays > 366 || from > now || to > now)
        {
            AddError(x => x.To, "Choose a past or current range after From, no longer than 366 days.");
            await Send.ErrorsAsync(cancellation: ct);

            return;
        }

        var response = await statsService.GetTopSongsAsync(User.GetUserId(), from, to, req.Limit, ct);

        await Send.OkAsync(response, ct);
    }
}
