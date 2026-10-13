using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Stats;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Stats.JumpBackIn;

internal sealed class GetJumpBackInRequest
{
    public int InactiveDays { get; init; } = 30;
    public int Limit { get; init; } = 6;
}

internal sealed class GetJumpBackInValidator : Validator<GetJumpBackInRequest>
{
    public GetJumpBackInValidator()
    {
        RuleFor(x => x.InactiveDays).InclusiveBetween(7, 365);
        RuleFor(x => x.Limit).InclusiveBetween(1, 20);
    }
}

internal sealed class GetJumpBackInEndpoint(IListeningStatsService statsService)
    : Endpoint<GetJumpBackInRequest, JumpBackInResponse>
{
    public override void Configure()
    {
        Get("/stats/jump-back-in");
        Policies(Common.Auth.Policies.RequireUser);
        ResponseCache(60);
    }

    public override async Task HandleAsync(GetJumpBackInRequest req, CancellationToken ct)
    {
        var response = await statsService.GetJumpBackInAsync(
            User.GetUserId(), DateTime.UtcNow, req.InactiveDays, req.Limit, ct);

        await Send.OkAsync(response, ct);
    }
}
